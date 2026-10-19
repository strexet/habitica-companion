using System.Text.Json;
using Habitica.Application.Diagnostics;
using Habitica.Domain.Diagnostics;
using Habitica.Storage;
using Habitica.Application.Sync;

namespace Habitica.WebApp.Tests.Workspace;

public sealed class WorkspaceDiagnosticsTests
{
    private sealed class FailingLogStore : IDiagnosticsLogStore
    {
        public Task<IReadOnlyList<DiagnosticsLogEntry>> GetRecentAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<DiagnosticsLogEntry>>([]);
        public Task ClearAsync(CancellationToken token) => Task.CompletedTask;
        public Task AppendAsync(DiagnosticsLogEntry entry, CancellationToken token) => throw new InvalidOperationException("Synthetic diagnostics failure");
    }

    [Fact]
    public async Task Logging_failure_cannot_change_a_saved_task_or_create_an_additional_command()
    {
        var f = new WorkspaceTaskFixture(new DiagnosticsLogWriter(new FailingLogStore(), TimeProvider.System));
        await f.Controller.CreateLocalAsync(); await f.Controller.PrepareAsync(WorkspaceTaskFixture.Todo());
        var id = f.Controller.Preview!.PreparedCommandId; await f.Controller.ConfirmAsync();
        Assert.Equal(id, f.Controller.LastReceipt!.CommandId); Assert.True(f.Controller.LastReceipt.SavedInCompanion);
        Assert.Single(f.Controller.Snapshot!.Commands); Assert.Single(f.Controller.Snapshot.Document.Tasks); Assert.False(f.Controller.AcceptanceUnknown);
    }

    [Fact]
    public async Task Backup_and_explicit_sync_section_exports_sanitize_legacy_diagnostics()
    {
        var storage = new InMemoryKeyValueStorage(); const string secret = "legacy-credential-canary";
        await storage.SetAsync(StorageKeys.DiagnosticsLogEntries, new[] { new DiagnosticsLogEntry(secret, DateTimeOffset.UtcNow, DiagnosticsFeatureArea.Tasks,
            "task-score", DiagnosticsSeverity.Warning, DiagnosticsMode.Local, secret, new Dictionary<string,string> { ["apiToken"] = secret, ["requestCount"] = "1" }) }, default);
        var portability = new LocalUserDataPortabilityService(storage, TimeProvider.System);
        var section = await portability.ExportSectionAsync(StorageKeys.DiagnosticsLogEntries, default);
        Assert.DoesNotContain(secret, section!.JsonText); Assert.Contains("requestCount", section.JsonText);
        await portability.ImportSectionAsync(section, LocalDataImportMode.Merge, default);
        Assert.Single(await new DiagnosticsLogStore(storage).GetRecentAsync(default));
        Assert.DoesNotContain(secret, await storage.GetRawJsonAsync(StorageKeys.DiagnosticsLogEntries, default) ?? "");
        var bundle = await portability.ExportAsync(null, default); Assert.DoesNotContain(secret, portability.Serialize(bundle));
        var external = new LocalUserDataBundle(1, DateTimeOffset.UtcNow, null, [new(StorageKeys.DiagnosticsLogEntries, await storage.GetRawJsonAsync(StorageKeys.DiagnosticsLogEntries, default) ?? "[]")]);
        Assert.DoesNotContain(secret, portability.Serialize(external));
        await storage.SetRawJsonAsync(StorageKeys.DiagnosticsLogEntries, "{\"apiToken\":\"" + secret + "\"}", default);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => portability.ExportSectionAsync(StorageKeys.DiagnosticsLogEntries, default));
        Assert.DoesNotContain(secret, error.Message);
    }

    [Fact]
    public async Task Appearance_selection_logs_an_outcome_while_read_only_loads_do_not_flood_history()
    {
        var storage = new InMemoryKeyValueStorage(); var logs = new DiagnosticsLogStore(storage);
        var themes = new Habitica.WebApp.Theme.ColorSchemeService(storage, new WorkspaceTaskFixture.Runtime(), diagnostics: new DiagnosticsLogWriter(logs, TimeProvider.System));
        var result = await themes.SelectAsync("gryphy-dark"); await themes.LoadAsync(); await themes.LoadAsync();
        Assert.Equal("gryphy-dark", result.ActiveScheme.Id);
        var entry = Assert.Single(await logs.GetRecentAsync(default)); Assert.Equal("appearance-select", entry.Operation); Assert.Empty(entry.Metadata);
    }

    [Fact]
    public async Task Local_save_emits_safe_correlated_receipt_without_task_text_and_retains_bounded_history()
    {
        var store = new DiagnosticsLogStore(new InMemoryKeyValueStorage(), 3);
        var f = new WorkspaceTaskFixture(new DiagnosticsLogWriter(store, TimeProvider.System));
        await f.Controller.CreateLocalAsync(); await f.Controller.PrepareAsync(WorkspaceTaskFixture.Todo("private-title-canary")); await f.Controller.ConfirmAsync();
        var entries = await store.GetRecentAsync(default); Assert.Equal(3, entries.Count);
        var accepted = Assert.Single(entries, e => e.Operation == "workspace-task-accept");
        Assert.Equal(f.Controller.LastReceipt!.CommandId, accepted.Context!.CommandId); Assert.Equal(DiagnosticsResult.Success, accepted.Context.Result);
        Assert.Equal(0, accepted.Context.ConfirmedCount); Assert.Single(f.Controller.Snapshot!.Commands);
        Assert.DoesNotContain("private-title-canary", JsonSerializer.Serialize(entries)); Assert.DoesNotContain("Private synthetic notes", JsonSerializer.Serialize(entries));
    }

    [Fact]
    public async Task Unknown_cloud_acceptance_and_original_recovery_never_duplicate_dispatch()
    {
        var store = new DiagnosticsLogStore(new InMemoryKeyValueStorage()); var f = new WorkspaceTaskFixture(new DiagnosticsLogWriter(store, TimeProvider.System));
        f.Client.Connected = true; await f.Controller.LoadAsync(true); await f.Controller.PrepareAsync(WorkspaceTaskFixture.Todo());
        var id = f.Controller.Preview!.PreparedCommandId; f.Client.LoseAcceptance = true; await f.Controller.ConfirmAsync();
        var uncertain = Assert.Single(await store.GetRecentAsync(default), e => e.Operation == "workspace-task-accept");
        Assert.Equal(DiagnosticsResult.Unknown, uncertain.Context!.Result); Assert.Equal(id, uncertain.Context.CommandId);
        Assert.Equal(DiagnosticsMode.LiveMutation, uncertain.Mode);
        await f.Controller.CheckOriginalAsync(); var recovered = Assert.Single(await store.GetRecentAsync(default), e => e.Operation == "workspace-task-recover");
        Assert.Equal(DiagnosticsResult.Success, recovered.Context!.Result); Assert.Equal(id, recovered.Context.CommandId); Assert.Single(f.Client.AcceptedIds);
        Assert.Equal(DiagnosticsMode.LiveRead, recovered.Mode);
    }

    [Fact]
    public async Task Missing_original_receipt_is_never_logged_as_success()
    {
        var store = new DiagnosticsLogStore(new InMemoryKeyValueStorage()); var f = new WorkspaceTaskFixture(new DiagnosticsLogWriter(store, TimeProvider.System));
        await f.Controller.CreateLocalAsync(); await f.Controller.PrepareAsync(WorkspaceTaskFixture.Todo()); await f.Controller.CheckOriginalAsync();
        var entry = Assert.Single(await store.GetRecentAsync(default), row => row.Operation == "workspace-task-recover");
        Assert.Equal(DiagnosticsResult.Unknown, entry.Context!.Result); Assert.Empty(f.Controller.Snapshot!.Commands);
    }

    [Fact]
    public async Task Late_account_results_are_discarded_and_cancellation_keeps_original_semantics()
    {
        var store = new DiagnosticsLogStore(new InMemoryKeyValueStorage()); var f = new WorkspaceTaskFixture(new DiagnosticsLogWriter(store, TimeProvider.System));
        f.Client.Connected = true; f.Client.LoadEntered = new(TaskCreationOptions.RunContinuationsAsynchronously); f.Client.LoadGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = f.Controller.LoadAsync(true); await f.Client.LoadEntered.Task;
        f.Session.SetState(f.Session.State with { UserId = "00000000-0000-4000-8000-000000000012" }); f.Client.LoadGate.SetResult(); await pending;
        var entry = Assert.Single(await store.GetRecentAsync(default)); Assert.Equal(DiagnosticsResult.AccountChanged, entry.Context!.Result); Assert.Null(entry.Context.CommandId); Assert.Null(f.Controller.Snapshot);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Controller.LoadAsync(false, cancellationToken: cancelled.Token));
        Assert.Equal(DiagnosticsResult.Cancelled, (await store.GetRecentAsync(default))[0].Context!.Result);
    }
}
