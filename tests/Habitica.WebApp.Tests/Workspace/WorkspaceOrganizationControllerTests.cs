using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.State;

namespace Habitica.WebApp.Tests.Workspace;

public sealed class WorkspaceOrganizationControllerTests
{
    private static WorkspaceOrganizationInput Create(WorkspaceOrganizationKind kind = WorkspaceOrganizationKind.Category, string name = "Synthetic category", WorkspaceOrganizationPatch? patch = null) => new(kind, WorkspaceOrganizationOperation.Create, Patch: patch ?? new(Name: name));
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Fixture : IDisposable
    {
        public WorkspaceTaskFixture Task { get; } = new();
        public Clock Clock { get; } = new();
        public Api Api { get; }
        public WorkspaceOrganizationController Controller { get; }
        public Fixture(Habitica.Application.Diagnostics.DiagnosticsLogWriter? diagnostics = null)
        {
            Api = new(Task, Clock); Controller = new(Task.Session, Task.Client, Api, Task.Database, Clock, diagnostics);
        }
        public async Task Local()
        {
            await Task.Controller.CreateLocalAsync(); await Task.Controller.PrepareAsync(WorkspaceTaskFixture.Todo()); await Task.Controller.ConfirmAsync();
            await Controller.LoadAsync(false, Task.Controller.Snapshot!.Document.Id);
        }
        public async Task Cloud() { Task.Client.Connected = true; await Controller.LoadAsync(true); }
        public void Dispose() => Controller.Dispose();
    }
    private sealed class Api(WorkspaceTaskFixture task, Clock clock) : IWorkspaceOrganizationApi
    {
        private readonly Dictionary<Guid, WorkspaceOrganizationPreview> _previews = [];
        private readonly Dictionary<Guid, WorkspaceOrganizationReceipt> _receipts = [];
        public List<Guid> Requests { get; } = [];
        public List<Guid> Accepted { get; } = [];
        public List<Guid> Reads { get; } = [];
        public bool LosePreview, LoseAcceptance, FabricateEffects, ThrowPrivateFailure, CancelAfterCommit;
        public TaskCompletionSource? PrepareEntered, PrepareGate, AcceptEntered, AcceptGate;
        public async Task<WorkspaceTaskResponse<WorkspaceOrganizationPreview>> PrepareOrganizationAsync(Guid requestId, WorkspaceOrganizationCommand intent, CancellationToken token)
        {
            Requests.Add(requestId); PrepareEntered?.TrySetResult();
            if (PrepareGate is not null) await PrepareGate.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
            if (ThrowPrivateFailure) throw new InvalidOperationException("private-transport-canary");
            if (!_previews.TryGetValue(requestId, out var preview))
            {
                var command = intent with { Id = Guid.NewGuid(), EntityId = intent.Operation == WorkspaceOrganizationOperation.Create ? Guid.NewGuid() : intent.EntityId };
                var result = WorkspaceOrganizationEngine.Accept(task.Client.State with { Authority = null }, command, clock.Now);
                if (result.Disposition != SubmissionDisposition.Accepted) return new(false, null, "private-api-refusal");
                preview = new(command.Id, Guid.NewGuid(), clock.Now.AddMinutes(10), false, command, result.Entry!.Snapshot, false, false); _previews[requestId] = preview;
            }
            if (LosePreview) { LosePreview = false; return new(false, null, "private-preview-loss", true); }
            if (_receipts.TryGetValue(preview.PreparedCommandId, out var receipt)) preview = preview with { SavedInCompanion = true, Receipt = receipt };
            return new(true, preview, "private-preview-message");
        }
        public async Task<WorkspaceTaskResponse<WorkspaceOrganizationReceipt>> AcceptOrganizationAsync(WorkspaceOrganizationPreview preview, CancellationToken token)
        {
            Accepted.Add(preview.PreparedCommandId); AcceptEntered?.TrySetResult();
            if (AcceptGate is not null) await AcceptGate.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
            var result = WorkspaceOrganizationEngine.Accept(task.Client.State with { Authority = null }, preview.Command, clock.Now);
            if (result.SavedInCompanion != true) return new(false, null, "private-stale-response");
            task.Client.State = result.Snapshot!;
            var receipt = WorkspaceOrganizationController.Receipt(result.Entry!, preview.CorrelationId) with { RefreshSucceeded = true };
            _receipts[receipt.CommandId] = receipt;
            if (CancelAfterCommit) { CancelAfterCommit = false; throw new OperationCanceledException(token); }
            if (LoseAcceptance) { LoseAcceptance = false; return new(false, null, "private-acceptance-loss", true); }
            if (FabricateEffects) return new(true, receipt with { MayAffectHabitica = true, ConfirmedEffectCount = 1 }, "private-fabricated-effects");
            return new(true, receipt, "private-success-message");
        }
        public Task<WorkspaceTaskResponse<WorkspaceOrganizationReceipt>> ReadOrganizationAsync(Guid commandId, CancellationToken token)
        {
            Reads.Add(commandId); return System.Threading.Tasks.Task.FromResult(_receipts.TryGetValue(commandId, out var receipt)
                ? new WorkspaceTaskResponse<WorkspaceOrganizationReceipt>(true, receipt, "private-recovery-message") : new(false, null, "private-receipt-missing"));
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task All_native_kinds_create_update_archive_with_independent_history_and_no_task_or_game_effects(bool cloud)
    {
        using var f = new Fixture(); if (cloud) await f.Cloud(); else await f.Local();
        var c = f.Controller; var originalTasks = c.Snapshot!.Document.Tasks; var originalJournal = c.Snapshot.Commands;
        var ids = new Dictionary<WorkspaceOrganizationKind, Guid>();
        foreach (var kind in Enum.GetValues<WorkspaceOrganizationKind>())
        {
            var patch = kind switch
            {
                WorkspaceOrganizationKind.Project => new WorkspaceOrganizationPatch(Name: "Project", CategoryId: ids[WorkspaceOrganizationKind.Category]),
                WorkspaceOrganizationKind.Section => new(Name: "Section", ProjectId: ids[WorkspaceOrganizationKind.Project], Position: 1),
                WorkspaceOrganizationKind.Goal => new(Name: "Goal", TaskIds: originalTasks.Select(task => task.Id).ToArray()),
                WorkspaceOrganizationKind.SavedTaskView => new(Name: "Saved list", Query: new(WorkspaceSavedTaskSource.Tasks, new(Kind: CompanionTaskKind.Todo))),
                _ => new(Name: kind.ToString())
            };
            await c.PrepareAsync(Create(kind, patch: patch)); var preview = c.Preview!;
            Assert.False(preview.MayAffectHabitica); Assert.False(preview.SavedInCompanion); ids[kind] = preview.Command.EntityId;
            await c.ConfirmAsync(); Assert.True(c.LastReceipt!.SavedInCompanion); Assert.True(c.LastReceipt.RefreshSucceeded); Assert.Equal(0, c.LastReceipt.AcceptedEventCount); Assert.Equal(0, c.LastReceipt.ConfirmedEffectCount);
        }
        foreach (var pair in ids)
        {
            await c.PrepareAsync(new(pair.Key, WorkspaceOrganizationOperation.Update, pair.Value, new(Name: "Renamed " + pair.Key))); await c.ConfirmAsync();
        }
        foreach (var pair in ids.Reverse()) { await c.PrepareAsync(new(pair.Key, WorkspaceOrganizationOperation.Archive, pair.Value)); await c.ConfirmAsync(); }
        Assert.Equal(18, c.Snapshot!.Document.OrganizationHistory!.Count); Assert.Equal(WorkspaceTaskApiJson.Serialize(originalTasks), WorkspaceTaskApiJson.Serialize(c.Snapshot.Document.Tasks)); Assert.Equal(WorkspaceTaskApiJson.Serialize(originalJournal), WorkspaceTaskApiJson.Serialize(c.Snapshot.Commands));
        Assert.True(WorkspaceJournalValidation.IsValid(c.Snapshot)); // Task journal was already compared verbatim above.
        Assert.All(ids, pair => Assert.True(WorkspaceOrganizationSnapshot.From(c.Snapshot.Document, pair.Key, pair.Value)!.IsArchived));
        Assert.Equal(ids[WorkspaceOrganizationKind.Project], c.Snapshot.Document.Sections.Single().ProjectId);
        Assert.Equal(ids[WorkspaceOrganizationKind.Category], c.Snapshot.Document.Projects.Single().CategoryId);
        Assert.Equal(originalTasks.Select(task => task.Id), c.Snapshot.Document.Goals.Single().TaskIds);
        Assert.NotNull(c.Snapshot.Document.SavedTaskViews!.Single().Query);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Project_removal_requires_exact_impact_and_recovers_one_native_tombstone(bool cloud)
    {
        using var f=new Fixture();if(cloud)await f.Cloud();else await f.Local();var c=f.Controller;
        await c.PrepareAsync(Create(WorkspaceOrganizationKind.Project));await c.ConfirmAsync();var project=c.LastReceipt!.EntityId;
        await c.PrepareAsync(Create(WorkspaceOrganizationKind.Section,patch:new(Name:"Retained section",ProjectId:project)));await c.ConfirmAsync();
        await c.PrepareAsync(new(WorkspaceOrganizationKind.Project,WorkspaceOrganizationOperation.Remove,project,new(Removal:new([],[]))));Assert.False(c.HasPendingIntent);
        var before=c.Snapshot!;var impact=WorkspaceProjectRemovalQuery.Evaluate(before.Document,project,before.Document.Revision);
        await c.PrepareAsync(new(WorkspaceOrganizationKind.Project,WorkspaceOrganizationOperation.Remove,project,new(Removal:impact.Removal)));var preview=c.Preview!;
        Assert.True(preview.Snapshot.IsRemoved);Assert.False(preview.MayAffectHabitica);Assert.Single(preview.Command.Patch!.Removal!.SectionIds);
        if(cloud)f.Api.LoseAcceptance=true;else f.Task.Js.LoseCommitAcknowledgement=true;
        await c.ConfirmAsync();Assert.True(c.AcceptanceUnknown);await c.CheckOriginalAsync();Assert.False(c.HasPendingIntent);Assert.Equal(preview.PreparedCommandId,c.LastReceipt!.CommandId);
        Assert.True(c.Snapshot!.Document.Projects.Single().IsRemoved);Assert.Equal(WorkspaceTaskApiJson.Serialize(before.Document.Tasks),WorkspaceTaskApiJson.Serialize(c.Snapshot.Document.Tasks));
        Assert.Equal(before.Document.Sections,c.Snapshot.Document.Sections);Assert.Equal(WorkspaceTaskApiJson.Serialize(before.Commands),WorkspaceTaskApiJson.Serialize(c.Snapshot.Commands));Assert.Single(c.Snapshot.Document.OrganizationHistory!,row=>row.Command.Operation==WorkspaceOrganizationOperation.Remove);
    }

    [Fact]
    public async Task Cloud_preview_loss_reuses_request_and_server_assigned_identity_then_recovers_original_acceptance()
    {
        using var f = new Fixture(); await f.Cloud(); var c = f.Controller; f.Api.LosePreview = true;
        await c.PrepareAsync(Create()); Assert.Null(c.Preview); Assert.True(c.HasPendingIntent); var request = Assert.Single(f.Api.Requests);
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => c.PrepareAsync(Create(name: "Replacement intent"))); Assert.Single(f.Api.Requests);
        await c.RetryOriginalPreviewAsync(); Assert.Equal(new[] { request, request }, f.Api.Requests); var preview = c.Preview!;
        f.Api.LoseAcceptance = true; await c.ConfirmAsync(); Assert.True(c.AcceptanceUnknown); Assert.ThrowsAny<InvalidOperationException>(c.CancelPreview);
        await c.CheckOriginalAsync(); Assert.Equal(preview.PreparedCommandId, c.LastReceipt!.CommandId); Assert.Single(f.Api.Accepted); Assert.Single(f.Api.Reads); Assert.False(c.HasPendingIntent);
        Assert.Single(c.Snapshot!.Document.Categories); Assert.Single(c.Snapshot.Document.OrganizationHistory!); Assert.DoesNotContain("private-", c.Message);
    }

    [Fact]
    public async Task Local_acknowledgement_loss_preserves_exact_command_and_source_until_original_recovery()
    {
        using var f = new Fixture(); await f.Local(); var c = f.Controller; await c.PrepareAsync(Create()); var original = c.Preview!;
        f.Task.Js.LoseCommitAcknowledgement = true; await c.ConfirmAsync(); Assert.True(c.AcceptanceUnknown); Assert.ThrowsAny<InvalidOperationException>(c.CancelPreview);
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => c.LoadAsync(true));
        await c.CheckOriginalAsync(); Assert.Equal(original.PreparedCommandId, c.LastReceipt!.CommandId); Assert.False(c.AcceptanceUnknown); Assert.Single(c.Snapshot!.Document.Categories); Assert.Single(c.Snapshot.Document.OrganizationHistory!);
        Assert.Equal(original.CorrelationId, c.LastReceipt.CorrelationId); Assert.Equal(0, c.LastReceipt.ConfirmedEffectCount);
    }

    [Fact]
    public async Task No_op_invalid_reference_stale_revision_and_expiry_never_create_a_new_implicit_intent()
    {
        using var f = new Fixture(); await f.Cloud(); var c = f.Controller; await c.PrepareAsync(Create()); await c.ConfirmAsync(); var id = c.LastReceipt!.EntityId;
        await c.PrepareAsync(new(WorkspaceOrganizationKind.Category, WorkspaceOrganizationOperation.Update, id, new(Name: "Synthetic category"))); Assert.False(c.HasPendingIntent); Assert.Contains("No organization fields changed", c.Message);
        await c.PrepareAsync(Create(WorkspaceOrganizationKind.Project, patch: new(Name: "Invalid reference", CategoryId: Guid.NewGuid()))); Assert.False(c.HasPendingIntent);
        await c.PrepareAsync(Create(name: "Stale")); var preview = c.Preview!;
        f.Task.Client.State = f.Task.Client.State with { Document = f.Task.Client.State.Document with { Revision = f.Task.Client.State.Document.Revision + 1 }, StoreVersion = f.Task.Client.State.StoreVersion + 1 };
        await c.ConfirmAsync(); Assert.Null(c.LastReceipt); Assert.True(c.HasPendingIntent); Assert.False(c.AcceptanceUnknown); Assert.Equal(preview.PreparedCommandId, c.Preview!.PreparedCommandId);
        c.CancelPreview(); await c.LoadAsync(true); await c.PrepareAsync(Create(name: "Expire")); var expires = c.Preview!; var calls = f.Api.Accepted.Count;
        f.Clock.Now = expires.ExpiresAtUtc.AddSeconds(1); await c.ConfirmAsync(); Assert.Equal(calls, f.Api.Accepted.Count); Assert.Equal(expires.PreparedCommandId, c.Preview!.PreparedCommandId); Assert.Contains("expired", c.Message);
    }

    [Fact]
    public async Task A_saved_command_with_failed_refresh_retains_receipt_and_blocks_edits_until_reload()
    {
        using var f = new Fixture(); await f.Local(); var c = f.Controller; await c.PrepareAsync(Create()); f.Task.Js.FailReadAfterCommit = true;
        await c.ConfirmAsync(); Assert.True(c.LastReceipt!.SavedInCompanion); Assert.False(c.LastReceipt.RefreshSucceeded); Assert.True(c.IsReadOnly); Assert.Null(c.Snapshot);
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => c.PrepareAsync(Create()));
        f.Task.Js.FailReads = false; await c.LoadAsync(false, c.WorkspaceId); Assert.NotNull(c.Snapshot); Assert.Single(c.Snapshot!.Document.Categories); Assert.False(c.IsReadOnly);
    }

    [Theory]
    [InlineData(WorkspaceAuthorityMode.Frozen)] [InlineData(WorkspaceAuthorityMode.Cloud)]
    public async Task Frozen_or_cloud_owned_local_authority_never_prepares_native_writes(WorkspaceAuthorityMode mode)
    {
        using var f = new Fixture(); await f.Local(); var original = f.Controller.Snapshot!;
        var frozen = original with { StoreVersion = original.StoreVersion + 1, Authority = new(mode, Guid.NewGuid(), mode == WorkspaceAuthorityMode.Cloud ? original.Document.Id : null) };
        Assert.True(await f.Task.Database.ForScope(WorkspaceTaskFixture.User).TryCommitAsync(original.StoreVersion, frozen, default));
        await f.Controller.LoadAsync(false, original.Document.Id); Assert.True(f.Controller.IsReadOnly);
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => f.Controller.PrepareAsync(Create())); Assert.Null(f.Controller.Preview); Assert.Empty(f.Api.Requests);
    }

    [Fact]
    public async Task Account_change_discards_late_preview_and_restores_only_the_original_accounts_pending_request()
    {
        using var f = new Fixture(); await f.Cloud(); var c = f.Controller;
        f.Api.PrepareEntered = new(TaskCreationOptions.RunContinuationsAsynchronously); f.Api.PrepareGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = c.PrepareAsync(Create()); await f.Api.PrepareEntered.Task.WaitAsync(TimeSpan.FromSeconds(5)); var request = Assert.Single(f.Api.Requests);
        var owner = f.Task.Session.State; f.Task.Session.SetState(owner with { UserId = "00000000-0000-4000-8000-000000000012" });
        Assert.Null(c.Preview); Assert.Null(c.Snapshot); Assert.False(c.HasPendingIntent);
        f.Api.PrepareGate.SetResult(); await pending; Assert.Null(c.Preview); Assert.Null(c.LastReceipt);
        f.Task.Session.SetState(owner); Assert.True(c.HasPendingIntent); Assert.True(c.RequiresReload); Assert.Null(c.Preview);
        await c.LoadAsync(true); await c.RetryOriginalPreviewAsync(); Assert.Equal(new[] { request, request }, f.Api.Requests); Assert.NotNull(c.Preview);
    }

    [Fact]
    public async Task Account_switch_during_acceptance_keeps_unknown_original_for_owner_receipt_recovery_without_replay()
    {
        using var f = new Fixture(); await f.Cloud(); var c = f.Controller; await c.PrepareAsync(Create()); var original = c.Preview!;
        f.Api.AcceptEntered = new(TaskCreationOptions.RunContinuationsAsynchronously); f.Api.AcceptGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = c.ConfirmAsync(); await f.Api.AcceptEntered.Task.WaitAsync(TimeSpan.FromSeconds(5)); var owner = f.Task.Session.State;
        f.Task.Session.SetState(SessionViewModel.Empty); f.Task.Session.SetState(owner); Assert.True(c.AcceptanceUnknown);
        f.Api.AcceptGate.SetResult(); await pending; Assert.Null(c.LastReceipt); Assert.Equal(original.PreparedCommandId, c.Preview!.PreparedCommandId);
        await c.CheckOriginalAsync(); Assert.Equal(original.PreparedCommandId, c.LastReceipt!.CommandId); Assert.Single(f.Api.Accepted); Assert.Single(c.Snapshot!.Document.OrganizationHistory!);
    }

    [Fact]
    public async Task Fabricated_game_effects_and_private_transport_failures_never_become_success_or_raw_messages()
    {
        using var f = new Fixture(); await f.Cloud(); var c = f.Controller; f.Api.ThrowPrivateFailure = true; await c.PrepareAsync(Create()); Assert.Null(c.Preview); Assert.True(c.HasPendingIntent); Assert.DoesNotContain("private-", c.Message);
        f.Api.ThrowPrivateFailure = false; await c.RetryOriginalPreviewAsync(); f.Api.FabricateEffects = true; await c.ConfirmAsync(); Assert.True(c.AcceptanceUnknown); Assert.Null(c.LastReceipt); Assert.DoesNotContain("private-", c.Message);
        await c.CheckOriginalAsync(); Assert.True(c.LastReceipt!.SavedInCompanion); Assert.False(c.LastReceipt.MayAffectHabitica); Assert.Equal(0, c.LastReceipt.ConfirmedEffectCount);
    }

    [Fact]
    public async Task Acknowledged_original_on_preview_retry_finishes_without_a_second_acceptance()
    {
        using var f = new Fixture(); await f.Cloud(); var c = f.Controller; await c.PrepareAsync(Create()); var id = c.Preview!.PreparedCommandId;
        f.Api.LoseAcceptance = true; await c.ConfirmAsync(); Assert.True(c.AcceptanceUnknown);
        await c.RetryOriginalPreviewAsync(); Assert.Equal(id, c.LastReceipt!.CommandId); Assert.Single(f.Api.Accepted); Assert.Single(c.Snapshot!.Document.OrganizationHistory!);
    }

    [Fact]
    public async Task Transport_cancellation_after_commit_keeps_unknown_original_for_read_only_recovery()
    {
        using var f = new Fixture(); await f.Cloud(); var c = f.Controller; await c.PrepareAsync(Create()); var id = c.Preview!.PreparedCommandId;
        f.Api.CancelAfterCommit = true; await c.ConfirmAsync(); Assert.True(c.AcceptanceUnknown);
        await c.CheckOriginalAsync(); Assert.Equal(id, c.LastReceipt!.CommandId); Assert.Single(f.Api.Accepted); Assert.Single(c.Snapshot!.Document.Categories);
    }

    [Fact]
    public async Task Changed_cloud_source_discards_late_preview_and_blocks_acceptance_until_original_access_returns()
    {
        using var f = new Fixture(); await f.Cloud(); var c = f.Controller; var original = f.Task.Client.State;
        f.Api.PrepareEntered = new(TaskCreationOptions.RunContinuationsAsynchronously); f.Api.PrepareGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = c.PrepareAsync(Create()); await f.Api.PrepareEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        f.Task.Client.State = new(new WorkspaceDocument { Id = Guid.NewGuid() }, 1, [], []);
        f.Api.PrepareGate.SetResult(); await pending; Assert.Null(c.Preview); Assert.Null(c.Snapshot); Assert.True(c.HasPendingIntent); Assert.True(c.IsReadOnly);
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => c.LoadAsync(true));
        f.Task.Client.State = original; await c.LoadAsync(true); await c.RetryOriginalPreviewAsync(); Assert.NotNull(c.Preview);
        f.Task.Client.Connected = false; await Assert.ThrowsAnyAsync<InvalidOperationException>(() => c.ConfirmAsync()); Assert.Empty(f.Api.Accepted);
        f.Task.Client.Connected = true; await c.ConfirmAsync(); Assert.True(c.LastReceipt!.SavedInCompanion);
    }

    [Fact]
    public async Task Diagnostics_distinguish_cloud_read_missing_receipt_and_saved_native_result_without_private_fields()
    {
        var logs = new Habitica.Storage.DiagnosticsLogStore(new InMemoryKeyValueStorage());
        using var f = new Fixture(new Habitica.Application.Diagnostics.DiagnosticsLogWriter(logs, TimeProvider.System)); await f.Cloud(); var c = f.Controller;
        await c.PrepareAsync(Create(name: "private-organization-name")); await c.CheckOriginalAsync(); Assert.Null(c.LastReceipt);
        var entries = await logs.GetRecentAsync(default);
        Assert.Equal(Habitica.Domain.Diagnostics.DiagnosticsMode.LiveRead, entries.Single(row => row.Operation == "workspace-load").Mode);
        Assert.Equal(Habitica.Domain.Diagnostics.DiagnosticsResult.Unknown, entries.Single(row => row.Operation == "workspace-organization-recover").Context!.Result);
        await c.ConfirmAsync(); entries = await logs.GetRecentAsync(default); var accepted = entries.Single(row => row.Operation == "workspace-organization-accept");
        Assert.Equal(Habitica.Domain.Diagnostics.DiagnosticsResult.Success, accepted.Context!.Result); Assert.Equal(c.LastReceipt!.CommandId, accepted.Context.CommandId); Assert.Equal(0, accepted.Context.ConfirmedCount);
        var json = System.Text.Json.JsonSerializer.Serialize(entries); Assert.DoesNotContain("private-", json); Assert.DoesNotContain(WorkspaceTaskFixture.User, json);
    }

    [Fact]
    public async Task Caller_cancellation_after_dispatch_keeps_original_unknown_until_the_same_command_is_saved()
    {
        using var f = new Fixture(); await f.Cloud(); var c = f.Controller; await c.PrepareAsync(Create()); var preview = c.Preview!;
        f.Api.AcceptEntered = new(TaskCreationOptions.RunContinuationsAsynchronously); f.Api.AcceptGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancelled = new CancellationTokenSource(); var pending = c.ConfirmAsync(cancelled.Token);
        await f.Api.AcceptEntered.Task.WaitAsync(TimeSpan.FromSeconds(5)); cancelled.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.True(c.AcceptanceUnknown); Assert.Equal(preview.PreparedCommandId, c.Preview!.PreparedCommandId);
        await c.CheckOriginalAsync(); Assert.Null(c.LastReceipt); Assert.True(c.AcceptanceUnknown);
        f.Api.AcceptGate.SetResult(); await c.ConfirmAsync(); Assert.Equal(preview.PreparedCommandId, c.LastReceipt!.CommandId); Assert.Equal(2, f.Api.Accepted.Count);
        Assert.All(f.Api.Accepted, id => Assert.Equal(preview.PreparedCommandId, id)); Assert.Single(c.Snapshot!.Document.OrganizationHistory!);
    }
}
