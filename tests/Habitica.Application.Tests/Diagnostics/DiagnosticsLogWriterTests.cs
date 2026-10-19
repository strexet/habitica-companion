using System.Text.Json;
using Habitica.Application.Diagnostics;
using Habitica.Domain.Diagnostics;
using Habitica.Storage;

namespace Habitica.Application.Tests.Diagnostics;

public sealed class DiagnosticsLogWriterTests
{
    private sealed class Store : IDiagnosticsLogStore
    {
        public List<DiagnosticsLogEntry> Entries { get; } = [];
        public bool Fail { get; set; }
        public bool Hang { get; set; }
        public int Writes { get; private set; }
        public Task<IReadOnlyList<DiagnosticsLogEntry>> GetRecentAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<DiagnosticsLogEntry>>(Entries);
        public Task ClearAsync(CancellationToken token) { Entries.Clear(); return Task.CompletedTask; }
        public Task AppendAsync(DiagnosticsLogEntry entry, CancellationToken token)
        {
            Writes++;
            if (Fail) throw new InvalidOperationException("credential-canary");
            if (Hang) return new TaskCompletionSource().Task;
            Entries.Add(entry); return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Writes_and_legacy_export_sanitization_remove_free_text_secrets_and_preserve_bounded_counters()
    {
        const string secret = "credential-canary", note = "Private task shopping description";
        var store = new Store(); var writer = new DiagnosticsLogWriter(store, TimeProvider.System);
        await writer.WriteAsync(DiagnosticsFeatureArea.Auth, "sign-in", DiagnosticsSeverity.Success, DiagnosticsMode.LiveRead,
            secret + note, new Dictionary<string,string> { ["authorization"] = secret, ["notes"] = note, ["requestCount"] = "2", ["completed"] = "-1" }, default);
        var entry = Assert.Single(store.Entries); Assert.Equal("2", Assert.Single(entry.Metadata).Value);
        var legacy = entry with { Id = secret, Message = secret + note, Metadata = new Dictionary<string,string> { ["managementToken"] = secret, ["taskName"] = note } };
        var json = JsonSerializer.Serialize(DiagnosticsLogSanitizer.Sanitize(legacy));
        Assert.DoesNotContain(secret, json); Assert.DoesNotContain(note, json);
        Assert.DoesNotContain(secret, JsonSerializer.Serialize(entry));
        var safe = DiagnosticsLogSanitizer.Sanitize(legacy);
        Assert.Equal(safe.Id, DiagnosticsLogSanitizer.Sanitize(safe).Id);
        Assert.Equal("app-operation", DiagnosticsLogSanitizer.Sanitize(legacy with { Operation = secret }).Operation);
    }

    [Fact]
    public async Task Correlation_and_receipt_counts_survive_without_logging_operation_payloads()
    {
        var store = new Store(); var writer = new DiagnosticsLogWriter(store, TimeProvider.System);
        var command = Guid.NewGuid(); var correlation = Guid.NewGuid(); var calls = 0;
        var value = await writer.ObserveAsync(DiagnosticsFeatureArea.Workspace, "workspace-task-accept", () => { calls++; return Task.FromResult("private-task-canary"); },
            _ => new(DiagnosticsResult.Success, CorrelationId: correlation, CommandId: command, AcceptedCount: 1, ConfirmedCount: 0, RequestedCount: 1));
        Assert.Equal("private-task-canary", value); Assert.Equal(1, calls);
        var entry = Assert.Single(store.Entries); Assert.Equal(command, entry.Context!.CommandId); Assert.Equal(correlation, entry.Context.CorrelationId);
        Assert.Equal(1, entry.Context.RequestedCount); Assert.Equal(0, entry.Context.ConfirmedCount); Assert.InRange(entry.Context.DurationMs, 0, 3_600_000);
        Assert.Equal(TimeSpan.Zero, entry.OccurredAtUtc.Offset); Assert.DoesNotContain(value, JsonSerializer.Serialize(entry));
    }

    [Fact]
    public async Task Failed_or_stalled_logging_and_failed_formatters_preserve_the_original_result_without_replay()
    {
        foreach (var hang in new[] { false, true })
        {
            var store = new Store { Fail = !hang, Hang = hang }; var writer = new DiagnosticsLogWriter(store, TimeProvider.System); var calls = 0;
            var result = await writer.ObserveAsync(DiagnosticsFeatureArea.Workspace, "workspace-task-accept",
                () => { calls++; return Task.FromResult(17); }, _ => new(DiagnosticsResult.Success));
            Assert.Equal(17, result); Assert.Equal(1, calls); Assert.Equal(1, store.Writes);
        }
        var normal = new DiagnosticsLogWriter(new Store(), TimeProvider.System);
        Assert.Equal(19, await normal.ObserveAsync(DiagnosticsFeatureArea.Workspace, "workspace-task-accept", () => Task.FromResult(19), _ => throw new Exception("formatter-canary")));
    }

    [Fact]
    public async Task Cancellation_and_original_exceptions_remain_intact_and_account_changes_drop_old_command_context()
    {
        var store = new Store(); var writer = new DiagnosticsLogWriter(store, TimeProvider.System);
        using var source = new CancellationTokenSource(); source.Cancel(); var cancelled = new OperationCanceledException(source.Token);
        var actual = await Assert.ThrowsAsync<OperationCanceledException>(() => writer.ObserveAsync<int>(DiagnosticsFeatureArea.Workspace,
            "workspace-load", () => Task.FromException<int>(cancelled), _ => new(DiagnosticsResult.Success)));
        Assert.Same(cancelled, actual); Assert.Equal(DiagnosticsResult.Cancelled, Assert.Single(store.Entries).Context!.Result);
        var failure = new InvalidOperationException("secret-error-canary");
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => writer.ObserveAsync<int>(DiagnosticsFeatureArea.Workspace,
            "workspace-load", () => Task.FromException<int>(failure), _ => new(DiagnosticsResult.Success))));
        await writer.ObserveAsync(DiagnosticsFeatureArea.Workspace, "workspace-task-accept", () => Task.FromResult(true),
            _ => new(DiagnosticsResult.Success, CommandId: Guid.NewGuid()), () => false);
        var changed = store.Entries.Last(); Assert.Equal(DiagnosticsResult.AccountChanged, changed.Context!.Result); Assert.Null(changed.Context.CommandId);
        Assert.DoesNotContain("secret-error-canary", JsonSerializer.Serialize(store.Entries));
    }
}
