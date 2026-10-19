using Habitica.Domain.Diagnostics;
using Habitica.Storage;

namespace Habitica.Application.Diagnostics;

public sealed class DiagnosticsLogWriter
{
    private readonly IDiagnosticsLogStore _logStore;
    private readonly TimeProvider _timeProvider;

    public DiagnosticsLogWriter(IDiagnosticsLogStore logStore, TimeProvider timeProvider)
    {
        _logStore = logStore;
        _timeProvider = timeProvider;
    }

    public async Task WriteAsync(
        DiagnosticsFeatureArea featureArea,
        string operation,
        DiagnosticsSeverity severity,
        DiagnosticsMode mode,
        string message,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(1));
            await _logStore.AppendAsync(DiagnosticsLogSanitizer.Sanitize(new DiagnosticsLogEntry(
                Id: Guid.NewGuid().ToString("N"),
                OccurredAtUtc: _timeProvider.GetUtcNow(),
                FeatureArea: featureArea,
                Operation: operation,
                Severity: severity,
                Mode: mode,
                Message: message,
                Metadata: metadata ?? new Dictionary<string, string>(StringComparer.Ordinal))), deadline.Token).WaitAsync(deadline.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { /* Diagnostics cannot change the original operation result. */ }
    }

    public async Task<T> ObserveAsync<T>(DiagnosticsFeatureArea area, string operation, Func<Task<T>> action,
        Func<T, DiagnosticsContext> describe, Func<bool>? current = null, DiagnosticsMode mode = DiagnosticsMode.Local)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            var value = await action();
            await RecordAsync(() => current?.Invoke() == false ? new(DiagnosticsResult.AccountChanged) : describe(value));
            return value;
        }
        catch (OperationCanceledException) { await RecordAsync(() => new(DiagnosticsResult.Cancelled)); throw; }
        catch { await RecordAsync(() => new(DiagnosticsResult.Unavailable)); throw; }

        async Task RecordAsync(Func<DiagnosticsContext> context)
        {
            try
            {
                var detail = context() with { DurationMs = (long)System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds };
                var severity = detail.Result == DiagnosticsResult.Success ? DiagnosticsSeverity.Success : DiagnosticsSeverity.Warning;
                var entry = new DiagnosticsLogEntry(Guid.NewGuid().ToString("N"), _timeProvider.GetUtcNow(), area, operation, severity,
                    mode, "", new Dictionary<string, string>(), detail);
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await _logStore.AppendAsync(DiagnosticsLogSanitizer.Sanitize(entry), deadline.Token).WaitAsync(deadline.Token);
            }
            catch { /* Formatting, clock and storage failures cannot replace an accepted command or its exception. */ }
        }
    }
}
