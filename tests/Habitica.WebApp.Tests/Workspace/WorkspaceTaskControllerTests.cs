using Habitica.Domain.Workspace;
using Habitica.WebApp.State;

namespace Habitica.WebApp.Tests.Workspace;

public sealed class WorkspaceTaskControllerTests
{
    [Fact]
    public async Task UnavailableCloudReloadRetainsUncertainPreviewUntilRenewedReceiptRecovery()
    {
        var f = new WorkspaceTaskFixture(); f.Client.Connected = true;
        await f.Controller.LoadAsync(true); await f.Controller.PrepareAsync(WorkspaceTaskFixture.Todo());
        var id = f.Controller.Preview!.PreparedCommandId; f.Client.LoseAcceptance = true;
        await f.Controller.ConfirmAsync(); f.Client.FailLoads = true;
        await f.Controller.LoadAsync(true);
        Assert.True(f.Controller.AcceptanceUnknown); Assert.True(f.Controller.RequiresReload);
        Assert.NotNull(f.Controller.Snapshot); Assert.Equal(id, f.Controller.Preview!.PreparedCommandId);
        Assert.Throws<InvalidOperationException>(() => f.Controller.CancelPreview());
        f.Client.FailLoads = false; // The same account's explicit renewal restores access.
        await f.Controller.CheckOriginalAsync();
        Assert.False(f.Controller.AcceptanceUnknown); Assert.False(f.Controller.RequiresReload);
        Assert.Equal(id, f.Controller.LastReceipt!.CommandId); Assert.Single(f.Client.AcceptedIds);
        Assert.Single(f.Controller.Snapshot!.Document.Tasks);
    }

    [Fact]
    public async Task LocalReviewDoesNotSaveUntilConfirmationAndLostAcknowledgementReusesOriginalCommand()
    {
        var f = new WorkspaceTaskFixture(); await f.Controller.CreateLocalAsync();
        await f.Controller.PrepareAsync(WorkspaceTaskFixture.Todo());
        var id = f.Controller.Preview!.Command.Id;
        Assert.Empty(f.Controller.Snapshot!.Commands);
        Assert.Single(f.Js.Rows);
        f.Js.LoseCommitAcknowledgement = true;
        await f.Controller.ConfirmAsync(); Assert.True(f.Controller.AcceptanceUnknown);
        Assert.Throws<InvalidOperationException>(() => f.Controller.CancelPreview());
        await f.Controller.ConfirmAsync();
        Assert.False(f.Controller.AcceptanceUnknown); Assert.Null(f.Controller.Preview);
        Assert.Single(f.Controller.Snapshot!.Commands); Assert.Single(f.Controller.Snapshot.Document.Tasks);
        Assert.Equal(id, f.Controller.LastReceipt!.CommandId); Assert.True(f.Controller.LastReceipt.SavedInCompanion);
        Assert.Empty(f.Client.AcceptedIds);
    }
    [Fact]
    public async Task CloudAcceptanceLossUsesServerCommandReceiptAndKeepsOneCanonicalTask()
    {
        var f = new WorkspaceTaskFixture(); f.Client.Connected = true;
        await f.Controller.LoadAsync(true); await f.Controller.PrepareAsync(WorkspaceTaskFixture.Todo());
        var id = f.Controller.Preview!.PreparedCommandId; f.Client.LoseAcceptance = true;
        await f.Controller.ConfirmAsync(); Assert.True(f.Controller.AcceptanceUnknown);
        await f.Controller.CheckOriginalAsync();
        Assert.Equal(id, f.Controller.LastReceipt!.CommandId); Assert.False(f.Controller.AcceptanceUnknown);
        Assert.Single(f.Client.State.Commands); Assert.Single(f.Client.AcceptedIds); Assert.Single(f.Client.State.Document.Tasks);
    }
    [Fact]
    public async Task SavedCommandSurvivesRefreshFailureAndEditorRequiresReload()
    {
        var f = new WorkspaceTaskFixture(); await f.Controller.CreateLocalAsync(); await f.Controller.PrepareAsync(WorkspaceTaskFixture.Todo());
        f.Js.FailReadAfterCommit = true; await f.Controller.ConfirmAsync();
        Assert.True(f.Controller.LastReceipt!.SavedInCompanion); Assert.False(f.Controller.LastReceipt.RefreshSucceeded);
        Assert.True(f.Controller.RequiresReload); Assert.True(f.Controller.IsReadOnly);
        f.Js.FailReads = false; await f.Controller.LoadAsync(false);
        Assert.False(f.Controller.RequiresReload); Assert.Single(f.Controller.Snapshot!.Commands);
    }
    [Fact]
    public async Task LateCloudReadCannotDisplayThePreviousAccountsWorkspace()
    {
        var f = new WorkspaceTaskFixture(); f.Client.Connected = true;
        f.Client.LoadEntered = new(TaskCreationOptions.RunContinuationsAsynchronously); f.Client.LoadGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = f.Controller.LoadAsync(true); await f.Client.LoadEntered.Task;
        f.Session.SetState(f.Session.State with { UserId = "00000000-0000-4000-8000-000000000012" }); f.Client.LoadGate.SetResult(); await pending;
        Assert.Null(f.Controller.Snapshot); Assert.Empty(f.Controller.LocalIds); Assert.Null(f.Controller.Preview);
    }
    [Fact]
    public async Task LocalSkippedOccurrenceUsesReviewedDateAndRetainsOneReceiptAfterAcknowledgementLoss()
    {
        var f = new WorkspaceTaskFixture();
        var empty = new WorkspaceJournalSnapshot(new WorkspaceDocument { Id = Guid.NewGuid() }, 1, [], []);
        var id = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var rule = new RecurrenceRule(RecurrenceFrequency.Daily, 1, new(2026, 10, 17), "UTC", [], []);
        var created = Habitica.Application.Workspace.WorkspaceCommandEngine.Accept(empty, new(Guid.NewGuid(), empty.Document.Id, 1, WorkspaceOperation.CreateDaily, id, new(Text: "Scheduled local work", Recurrence: rule)), now).Snapshot!;
        Assert.True(await f.Database.ForScope(WorkspaceTaskFixture.User).TryCommitAsync(0, created, default));
        await f.Controller.LoadAsync(false, created.Document.Id);
        await f.Controller.PrepareAsync(WorkspaceTaskFixture.Todo() with { Operation = WorkspaceOperation.SkipOccurrence, TaskId = id, OccurrenceDate = new(2026, 10, 17) });
        Assert.False(f.Controller.Preview!.MayAffectHabitica); Assert.Equal(new DateOnly(2026, 10, 17), f.Controller.Preview.Command.OccurrenceDate);
        var original = f.Controller.Preview.Command.Id; f.Js.LoseCommitAcknowledgement = true;
        await f.Controller.ConfirmAsync(); Assert.True(f.Controller.AcceptanceUnknown);
        await f.Controller.CheckOriginalAsync();
        Assert.Equal(original, f.Controller.LastReceipt!.CommandId); Assert.Equal(1, f.Controller.LastReceipt.AcceptedEventCount); Assert.Equal(0, f.Controller.LastReceipt.TotalProjectionCount);
        Assert.Equal(OccurrenceState.Skipped, Assert.Single(f.Controller.Snapshot!.Document.Occurrences).State);
        Assert.Empty(f.Controller.Snapshot.Document.Completions); Assert.Equal(2, f.Controller.Snapshot.Commands.Count);
    }
    [Fact]
    public async Task NativePlanningKeepsTheOriginalReceiptAfterAcknowledgementLoss()
    {
        var f=new WorkspaceTaskFixture();await f.Controller.CreateLocalAsync();await f.Controller.PrepareAsync(WorkspaceTaskFixture.Todo());await f.Controller.ConfirmAsync();
        var task=Assert.Single(f.Controller.Snapshot!.Document.Tasks);
        await f.Controller.PrepareAsync(WorkspaceTaskFixture.Todo() with { Operation=WorkspaceOperation.PlanTask,TaskId=task.Id,Planning=new(PlannedDate:new(2026,10,20)) });
        Assert.False(f.Controller.Preview!.MayAffectHabitica);var original=f.Controller.Preview.Command.Id;f.Js.LoseCommitAcknowledgement=true;
        await f.Controller.ConfirmAsync();Assert.True(f.Controller.AcceptanceUnknown);await f.Controller.CheckOriginalAsync();
        Assert.Equal(original,f.Controller.LastReceipt!.CommandId);Assert.Equal(0,f.Controller.LastReceipt.AcceptedEventCount);Assert.Equal(0,f.Controller.LastReceipt.TotalProjectionCount);
        var saved=Assert.Single(f.Controller.Snapshot!.Document.Tasks);Assert.Equal(new DateOnly(2026,10,20),saved.PlannedDate);Assert.Equal(task.Text,saved.Text);Assert.Equal(2,f.Controller.Snapshot.Commands.Count);
    }
    [Fact]
    public async Task TodoRestorationReviewsExactHistoryAndRecoversOneEventAfterLostAcknowledgement()
    {
        var f=new WorkspaceTaskFixture();await f.Controller.CreateLocalAsync();await f.Controller.PrepareAsync(WorkspaceTaskFixture.Todo());await f.Controller.ConfirmAsync();
        var task=Assert.Single(f.Controller.Snapshot!.Document.Tasks);
        await f.Controller.PrepareAsync(WorkspaceTaskFixture.Todo() with { Operation=WorkspaceOperation.CompleteTask,TaskId=task.Id });await f.Controller.ConfirmAsync();
        var completion=Assert.Single(f.Controller.Snapshot!.Document.Completions);
        await f.Controller.PrepareAsync(WorkspaceTaskFixture.Todo() with { Operation=WorkspaceOperation.RestoreTodo,TaskId=task.Id,RestoreTodo=new(completion.Id) });
        Assert.False(f.Controller.Preview!.MayAffectHabitica);Assert.Equal(completion.Id,f.Controller.Preview.Command.Patch!.RestoreTodo!.CompletionId);Assert.Contains("no Habitica effect",f.Controller.Message);
        var original=f.Controller.Preview.Command.Id;f.Js.LoseCommitAcknowledgement=true;await f.Controller.ConfirmAsync();Assert.True(f.Controller.AcceptanceUnknown);await f.Controller.CheckOriginalAsync();
        Assert.Equal(original,f.Controller.LastReceipt!.CommandId);Assert.Equal(1,f.Controller.LastReceipt.AcceptedEventCount);Assert.Equal(0,f.Controller.LastReceipt.TotalProjectionCount);
        Assert.Single(f.Controller.Snapshot!.Document.Completions);Assert.Single(f.Controller.Snapshot.Document.Restorations!);Assert.Equal(CompanionTaskState.Active,f.Controller.Snapshot.Document.Tasks[0].State);Assert.Equal(3,f.Controller.Snapshot.Commands.Count);
    }
    [Fact]
    public async Task TodoRelationshipsRecoverTheOriginalNativeReceiptWithoutChangingParentState()
    {
        var f=new WorkspaceTaskFixture();await f.Controller.CreateLocalAsync();await f.Controller.PrepareAsync(WorkspaceTaskFixture.Todo("Parent"));await f.Controller.ConfirmAsync();var parent=Assert.Single(f.Controller.Snapshot!.Document.Tasks);
        await f.Controller.PrepareAsync(WorkspaceTaskFixture.Todo("Child"));await f.Controller.ConfirmAsync();var child=f.Controller.Snapshot!.Document.Tasks.Single(task=>task.Text=="Child");
        await f.Controller.PrepareAsync(WorkspaceTaskFixture.Todo() with { Operation=WorkspaceOperation.RelateTodo,TaskId=child.Id,Relationships=new(ParentTaskId:parent.Id,DependsOn:[]) });
        var original=f.Controller.Preview!.Command.Id;Assert.False(f.Controller.Preview.MayAffectHabitica);Assert.Equal(parent.Id,f.Controller.Preview.Command.Patch!.Relationships!.ParentTaskId);
        f.Js.LoseCommitAcknowledgement=true;await f.Controller.ConfirmAsync();Assert.True(f.Controller.AcceptanceUnknown);await f.Controller.CheckOriginalAsync();
        Assert.Equal(original,f.Controller.LastReceipt!.CommandId);Assert.Equal(0,f.Controller.LastReceipt.AcceptedEventCount);Assert.Equal(0,f.Controller.LastReceipt.TotalProjectionCount);Assert.Equal(parent.Id,f.Controller.Snapshot!.Document.Tasks.Single(task=>task.Id==child.Id).ParentTaskId);Assert.Equal(parent,f.Controller.Snapshot.Document.Tasks.Single(task=>task.Id==parent.Id));Assert.Equal(3,f.Controller.Snapshot.Commands.Count);
    }
    [Fact]
    public async Task NativeTodoStateRetainsOriginalReceiptAndMetadataAfterLostAcknowledgement()
    {
        var f=new WorkspaceTaskFixture();await f.Controller.CreateLocalAsync();await f.Controller.PrepareAsync(WorkspaceTaskFixture.Todo());await f.Controller.ConfirmAsync();var task=Assert.Single(f.Controller.Snapshot!.Document.Tasks);
        await f.Controller.PrepareAsync(WorkspaceTaskFixture.Todo() with {Operation=WorkspaceOperation.SetTodoState,TaskId=task.Id,TodoState=new(CompanionTaskState.Waiting)});
        var original=f.Controller.Preview!.Command.Id;Assert.False(f.Controller.Preview.MayAffectHabitica);Assert.Equal(CompanionTaskState.Waiting,f.Controller.Preview.Command.Patch!.TodoState!.State);
        f.Js.LoseCommitAcknowledgement=true;await f.Controller.ConfirmAsync();Assert.True(f.Controller.AcceptanceUnknown);await f.Controller.CheckOriginalAsync();
        Assert.Equal(original,f.Controller.LastReceipt!.CommandId);Assert.Equal(0,f.Controller.LastReceipt.AcceptedEventCount);Assert.Equal(0,f.Controller.LastReceipt.TotalProjectionCount);var saved=Assert.Single(f.Controller.Snapshot!.Document.Tasks);Assert.Equal(CompanionTaskState.Waiting,saved.State);Assert.Equal(task.Text,saved.Text);Assert.Equal(task.Notes,saved.Notes);Assert.Empty(f.Controller.Snapshot.Document.Completions);
    }

}
