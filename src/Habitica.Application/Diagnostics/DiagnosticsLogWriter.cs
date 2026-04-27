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

    public Task WriteAsync(
        DiagnosticsFeatureArea featureArea,
        string operation,
        DiagnosticsSeverity severity,
        DiagnosticsMode mode,
        string message,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken cancellationToken)
    {
        return _logStore.AppendAsync(
            new DiagnosticsLogEntry(
                Id: Guid.NewGuid().ToString("N"),
                OccurredAtUtc: _timeProvider.GetUtcNow(),
                FeatureArea: featureArea,
                Operation: operation,
                Severity: severity,
                Mode: mode,
                Message: message,
                Metadata: metadata ?? new Dictionary<string, string>(StringComparer.Ordinal)),
            cancellationToken);
    }
}
