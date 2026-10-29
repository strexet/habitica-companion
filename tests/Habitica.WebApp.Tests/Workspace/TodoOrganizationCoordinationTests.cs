using Habitica.Domain.Workspace;
using Habitica.WebApp.State;

namespace Habitica.WebApp.Tests.Workspace;

public sealed class TodoOrganizationCoordinationTests
{
    private static WorkspaceOrganizationInput Project(string name="Project")=>new(WorkspaceOrganizationKind.Project,WorkspaceOrganizationOperation.Create,Patch:new(Name:name));
    [Fact]
    public async Task Pending_task_and_native_reviews_are_mutually_exclusive_and_refresh_the_same_selected_source()
    {
        using var f=new TodoOrganizationFixture();await f.Load();var selected=f.Todo.Selection;
        await f.Todo.PrepareAsync(WorkspaceTaskFixture.Todo());await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Todo.PrepareOrganizationAsync(Project()));Assert.Empty(f.Api.Requests);f.Todo.CancelPreview();
        await f.Todo.PrepareOrganizationAsync(Project());Assert.True(f.Organization.HasPendingIntent);Assert.True(f.Todo.IsReadOnly);Assert.False(f.Todo.CanSwitchSource);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Todo.PrepareAsync(WorkspaceTaskFixture.Todo()));await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Todo.CreateBrowserAsync());
        await f.Todo.ConfirmOrganizationAsync();Assert.Equal(selected,f.Todo.Selection);Assert.Equal(f.Organization.Snapshot!.Document.Revision,f.Todo.Revision);Assert.Single(f.Todo.Projects(new()).Items);Assert.False(f.Todo.IsReadOnly);Assert.Null(f.Todo.OrganizationBlockReason);Assert.Empty(f.Task.Client.State.Commands);Assert.Empty(f.Task.Client.State.Events??[]);
        await f.Todo.PrepareAsync(WorkspaceTaskFixture.Todo());Assert.True(f.Task.Controller.HasPendingIntent);await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Todo.LoadOrganizationAsync());
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Uncertain_native_acceptance_keeps_original_source_and_blocks_task_writes_until_recovery(bool cloud)
    {
        using var f=new TodoOrganizationFixture();await f.Load(cloud);if(!cloud){await f.Todo.PrepareAsync(WorkspaceTaskFixture.Todo());await f.Todo.ConfirmAsync();}await f.Todo.PrepareOrganizationAsync(Project());var id=f.Organization.Preview!.PreparedCommandId;var selected=f.Todo.Selection;
        if(cloud)f.Api.LoseAcceptance=true;else f.Task.Js.LoseCommitAcknowledgement=true;
        await f.Todo.ConfirmOrganizationAsync();Assert.True(f.Organization.AcceptanceUnknown);Assert.True(f.Todo.IsReadOnly);Assert.Contains("original organization",f.Todo.OrganizationBlockReason);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Todo.PrepareAsync(WorkspaceTaskFixture.Todo()));await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Todo.SelectAsync(selected!));
        await f.Todo.RecoverOrganizationAsync();Assert.Equal(selected,f.Todo.Selection);Assert.Equal(id,f.Organization.LastReceipt!.CommandId);Assert.False(f.Todo.IsReadOnly);Assert.Single(f.Todo.Projects(new()).Items);if(cloud){Assert.Single(f.Api.Acceptances);Assert.Single(f.Api.Reads);}else Assert.Empty(f.Api.Acceptances);
    }
    [Fact]
    public async Task Account_change_between_native_load_and_prepare_cannot_transfer_the_old_draft_to_a_new_owner()
    {
        using var f=new TodoOrganizationFixture();await f.Load();var original=f.Task.Client.State;var entered=new TaskCompletionSource();var release=new TaskCompletionSource();
        f.Task.Client.LoadAction=async _=>{entered.TrySetResult();await release.Task.WaitAsync(TimeSpan.FromSeconds(5));return original;};
        var pending=f.Todo.PrepareOrganizationAsync(Project("Private previous-owner draft"));await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            f.Task.Session.SetState(f.Task.Session.State with {UserId="00000000-0000-4000-8000-000000000012"});f.Task.Client.LoadAction=null;f.Task.Client.State=new(new WorkspaceDocument{Id=Guid.NewGuid()},1,[],[]);
            await f.Task.Controller.LoadAsync(true);await f.Todo.RefreshSourcesAsync();await f.Todo.LoadSelectedAsync();
        }
        finally {release.TrySetResult();}
        await pending.WaitAsync(TimeSpan.FromSeconds(5));await f.Todo.LoadOrganizationAsync();Assert.Empty(f.Api.Requests);Assert.Null(f.Organization.Preview);Assert.Empty(f.Organization.Snapshot!.Document.Projects);Assert.Equal(f.Task.Client.State.Document.Id,f.Todo.Selection!.WorkspaceId);Assert.False(f.Todo.IsBusy);
    }

    [Fact]
    public async Task Returning_owner_restores_only_the_original_pending_source_and_can_recover_the_native_receipt()
    {
        using var f=new TodoOrganizationFixture();await f.Load();await f.Todo.PrepareOrganizationAsync(Project());var command=f.Organization.Preview!.PreparedCommandId;var original=f.Todo.Selection!;f.Api.LoseAcceptance=true;await f.Todo.ConfirmOrganizationAsync();var ownerState=f.Task.Client.State;
        f.Task.Session.SetState(f.Task.Session.State with {UserId="00000000-0000-4000-8000-000000000012"});f.Task.Client.State=new(new WorkspaceDocument{Id=Guid.NewGuid()},1,[],[]);await f.Task.Controller.LoadAsync(true);await f.Todo.RefreshSourcesAsync();await f.Todo.LoadSelectedAsync();Assert.Null(f.Organization.Preview);Assert.NotEqual(original,f.Todo.Selection);
        f.Task.Client.State=ownerState;f.Task.Session.SetState(f.Task.Session.State with {UserId=WorkspaceTaskFixture.User});Assert.Equal(original,f.Todo.Selection);Assert.False(f.Todo.CanSwitchSource);Assert.Equal(command,f.Organization.Preview!.PreparedCommandId);
        await f.Todo.LoadSelectedAsync();await f.Todo.RecoverOrganizationAsync();Assert.Equal(command,f.Organization.LastReceipt!.CommandId);Assert.Equal(original,f.Todo.Selection);Assert.False(f.Todo.IsReadOnly);Assert.Single(f.Todo.Projects(new()).Items);Assert.Single(f.Api.Acceptances);
    }

    [Fact]
    public async Task Native_reload_cannot_silently_rebase_an_editor_onto_a_newer_canonical_revision()
    {
        using var f=new TodoOrganizationFixture();await f.Load();f.Task.Client.State=f.Task.Client.State with {Document=f.Task.Client.State.Document with {Revision=2},StoreVersion=2};
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Todo.PrepareOrganizationAsync(Project()));Assert.Empty(f.Api.Requests);Assert.Null(f.Organization.Preview);Assert.True(f.Todo.IsReadOnly);
        await f.Todo.LoadSelectedAsync();await f.Todo.PrepareOrganizationAsync(Project());Assert.Equal(2,f.Organization.Preview!.Command.ExpectedRevision);
    }

    [Fact]
    public async Task Owner_transition_emits_an_empty_selection_before_restoring_a_reused_workspace_identity()
    {
        using var f=new TodoOrganizationFixture();await f.Load();var id=f.Todo.Selection!.WorkspaceId;await f.Todo.PrepareOrganizationAsync(Project("Owner A"));
        f.Task.Session.SetState(f.Task.Session.State with {UserId="00000000-0000-4000-8000-000000000012"});f.Task.Client.State=new(new WorkspaceDocument{Id=id},1,[],[]);await f.Task.Controller.LoadAsync(true);await f.Todo.RefreshSourcesAsync();await f.Todo.LoadSelectedAsync();await f.Todo.PrepareOrganizationAsync(Project("Owner B"));
        var observed=new List<TodoWorkspaceSelection?>();f.Todo.Changed+=()=>observed.Add(f.Todo.Selection);f.Task.Session.SetState(f.Task.Session.State with {UserId=WorkspaceTaskFixture.User});Assert.Contains(observed,source=>source is null);Assert.Equal(id,f.Todo.Selection!.WorkspaceId);Assert.Equal("Owner A",f.Organization.Preview!.Snapshot.Name);
    }

    [Fact]
    public async Task Original_native_preparation_recovery_and_failed_refresh_never_enable_stale_task_writes()
    {
        using var f=new TodoOrganizationFixture();await f.Load();f.Api.LosePreparation=true;await f.Todo.PrepareOrganizationAsync(Project());Assert.Null(f.Organization.Preview);Assert.True(f.Todo.IsReadOnly);var request=Assert.Single(f.Api.Requests);
        await f.Todo.RetryOrganizationPreviewAsync();Assert.Equal(new[]{request,request},f.Api.Requests);f.Task.Client.FailLoads=true;await f.Todo.ConfirmOrganizationAsync();Assert.True(f.Organization.LastReceipt!.SavedInCompanion);Assert.True(f.Todo.IsReadOnly);Assert.Null(f.Todo.Snapshot);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Todo.PrepareAsync(WorkspaceTaskFixture.Todo()));f.Task.Client.FailLoads=false;await f.Todo.LoadSelectedAsync();Assert.Single(f.Todo.Projects(new()).Items);Assert.False(f.Todo.IsReadOnly);
    }
}
