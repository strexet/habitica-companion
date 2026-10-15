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
}
