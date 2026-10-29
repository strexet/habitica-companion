using Bunit;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Components;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
namespace Habitica.WebApp.Tests.Workspace;
public sealed class TodoTaskLinkTests : BunitContext
{
    private async Task<(WorkspaceTaskFixture Fixture, TodoWorkspacePresentation Todo, CompanionTask Task)> Setup(bool cloud = false, CompanionTaskKind kind = CompanionTaskKind.Todo, bool completed = false, bool archived = false)
    {
        JSInterop.Mode=JSRuntimeMode.Loose;Services.AddMudServices();var f=new WorkspaceTaskFixture();
        Services.AddSingleton<IAppSessionController>(f.Session);Services.AddSingleton<IWorkspaceCloudClient>(f.Client);Services.AddSingleton(f.Database);Services.AddSingleton(f.Controller);Services.AddSingleton<TodoTaskDraftStore>();
        var todo=new TodoWorkspacePresentation(f.Session,f.Client,f.Database,f.Controller);Services.AddSingleton(todo);
        var now=DateTimeOffset.UtcNow;var task=new CompanionTask(Guid.NewGuid(),1,new string('W',130)+" exact task",kind,completed?CompanionTaskState.Completed:CompanionTaskState.Active,CompanionPriority.High,now,now,Notes:"Retained native notes",IsArchived:archived);
        if(cloud){f.Client.Connected=true;f.Client.State=f.Client.State with {Document=f.Client.State.Document with {Tasks=[task],Completions=completed?[new(Guid.NewGuid(),1,task.Id,Guid.NewGuid(),now)]:[]}};await todo.RefreshSourcesAsync();await todo.SelectAsync(new(TodoWorkspaceSource.Cloud,f.Client.State.Document.Id));}
        else {await f.SeedLegacyLocalAsync(task.Text,completed,kind);task=f.Controller.Snapshot!.Document.Tasks.Single();await todo.RefreshSourcesAsync();}
        return(f,todo,task);
    }
    private static void Save(string name,string markup,string state,string test)=>Qualification.PageLayoutEvidence.Save(name,markup,name.Contains("history")?"/todo/history":"/tasks",state,["form","actions","confirmation","disabled-controls"],test);
    [Fact]
    public async Task Direct_link_creation_reviews_the_unchanged_task_and_requests_no_score()
    {
        var(f,todo,task)=await Setup();var cut=Render<TodoTaskPanel>();cut.Find($"[data-testid='todo-link-{task.Id:D}']").Click();
        Save("todo-link-choice",cut.Markup,"exact-create-versus-adopt",nameof(Direct_link_creation_reviews_the_unchanged_task_and_requests_no_score));
        cut.Find("[data-testid='todo-link-create']").Click();var preview=f.Controller.Preview!;
        Assert.Equal(WorkspaceOperation.EnsureProjection,preview.Command.Operation);Assert.Equal(task.Id,preview.Command.TaskId);Assert.Null(preview.Command.Patch);
        Assert.Contains(task.Text,cut.Find("[data-testid='todo-review']").TextContent);Assert.Contains("No canonical fields",cut.Find("[data-testid='todo-review']").TextContent);
        Save("todo-link-create-review",cut.Markup,"unchanged-counterpart-review",nameof(Direct_link_creation_reviews_the_unchanged_task_and_requests_no_score));
        cut.Find("[data-testid='todo-confirm']").Click();var saved=todo.Snapshot!.Document;
        Assert.Single(saved.Tasks);Assert.Equal(task.Text,saved.Tasks[0].Text);Assert.Empty(saved.Completions);Assert.Empty(saved.HabitScores);Assert.Equal(new[]{ProjectionEffectKind.Create},f.Controller.Snapshot!.Commands.Last().Effects);
    }
    [Fact]
    public async Task Checkbox_only_intent_uses_ensure_projection_without_fabricated_fields()
    {
        var(f,todo,task)=await Setup();var cut=Render<TodoTaskPanel>();cut.Find($"[data-testid='todo-edit-{task.Id:D}']").Click();cut.Find("form").Submit();
        Assert.Equal(WorkspaceOperation.EnsureProjection,f.Controller.Preview!.Command.Operation);Assert.Null(f.Controller.Preview.Command.Patch);Assert.Equal(task.Id,f.Controller.Preview.Command.TaskId);Assert.Single(todo.Snapshot!.Document.Tasks);
    }
    [Fact]
    public async Task Definitively_rejected_preparation_offers_correction_and_cancellation_but_unknown_preparation_keeps_identity()
    {
        var(f,todo,task)=await Setup(true);f.Client.RejectPreparation=true;var cut=Render<TodoTaskPanel>();cut.Find($"[data-testid='todo-edit-{task.Id:D}']").Click();cut.Find("form").Submit();
        Assert.True(f.Controller.PreparationRejected);Assert.True(f.Controller.HasPendingIntent);Assert.False(todo.CanSwitchSource);Assert.Empty(f.Client.AcceptedIds);
        Save("todo-link-rejected",cut.Markup,"definitive-rejected-intent-can-cancel",nameof(Definitively_rejected_preparation_offers_correction_and_cancellation_but_unknown_preparation_keeps_identity));
        var rejected=f.Client.PreparationIds.Single();cut.Find("[data-testid='todo-rejected-edit']").Click();Assert.False(f.Controller.HasPendingIntent);Assert.Equal(task.Text,cut.Find("[data-testid='todo-title']").GetAttribute("value"));
        f.Client.RejectPreparation=false;f.Client.LosePreparation=true;cut.Find("form").Submit();var request=f.Client.PreparationIds.Last();Assert.NotEqual(rejected,request);Assert.True(f.Controller.PreparationUnknown);
        Assert.Empty(cut.FindAll("[data-testid='todo-rejected-cancel']"));Assert.Throws<InvalidOperationException>(()=>f.Controller.CancelPreview());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>todo.CreateBrowserAsync());
        cut.Find("[data-testid='todo-retry-preview']").Click();Assert.Equal(new[]{request,request},f.Client.PreparationIds.Skip(1));Assert.NotNull(f.Controller.Preview);
        cut.Find("[data-testid='todo-cancel']").Click();Assert.False(f.Controller.HasPendingIntent);
        f.Client.RejectPreparation=true;cut.Find("form").Submit();cut.Find("[data-testid='todo-rejected-cancel']").Click();Assert.False(f.Controller.HasPendingIntent);Assert.Empty(f.Client.AcceptedIds);
    }
    [Theory]
    [InlineData(CompanionTaskKind.Todo,false,false)]
    [InlineData(CompanionTaskKind.Daily,false,false)]
    [InlineData(CompanionTaskKind.Todo,true,false)]
    [InlineData(CompanionTaskKind.Todo,true,true)]
    public async Task Existing_record_adoption_retains_exact_review_across_lost_acknowledgement(CompanionTaskKind kind,bool completed,bool archived)
    {
        var(f,todo,task)=await Setup(true,kind,completed,archived);
        var active=completed?null:Render<TodoTaskPanel>(p=>p.Add(x=>x.Kind,kind).Add(x=>x.ShowKindSelector,true));
        var history=completed?Render<TodoHistoryPanel>():null;
        if(completed)history!.Find($"[data-testid='history-link-{todo.Snapshot!.Document.Completions.Single().Id:D}']").Click();else active!.Find($"[data-testid='todo-link-{task.Id:D}']").Click();
        IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> cut=completed?history!:active!;
        if(completed)Save("todo-link-history-choice",cut.Markup,"retained-historical-record-link-entry",nameof(Existing_record_adoption_retains_exact_review_across_lost_acknowledgement));
        Assert.Equal(kind!=CompanionTaskKind.Todo||completed,cut.Find("[data-testid='todo-link-create']").HasAttribute("disabled"));
        cut.Find("[data-testid='todo-link-id']").Change("not a UUID");cut.Find("[data-testid='todo-link-preview']").Click();Assert.Empty(f.Client.LinkPreviewRequests);
        var remote=Guid.NewGuid();cut.Find("[data-testid='todo-link-id']").Change(remote.ToString("D"));cut.Find("[data-testid='todo-link-preview']").Click();var original=f.Controller.LinkPreview!;Assert.Equal(task.Id,original.Task.Id);Assert.Equal(remote,original.External.Id);Assert.False(todo.CanSwitchSource);
        Save(completed?"todo-link-history-review":"todo-link-adoption-review",cut.Markup,"exact-existing-record-comparison",nameof(Existing_record_adoption_retains_exact_review_across_lost_acknowledgement));
        f.Client.LoseLinkAcceptance=true;cut.Find("[data-testid='todo-link-confirm']").Click();Assert.True(f.Controller.LinkAcceptanceUnknown);Assert.Same(original,f.Controller.LinkPreview);Assert.False(todo.CanSwitchSource);
        if(!completed)Assert.True(cut.Find("[data-testid='todo-kind']").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid='todo-link-cancel']").HasAttribute("disabled"));Assert.Empty(cut.FindAll("[data-testid='todo-retry-preview']"));Assert.Throws<InvalidOperationException>(()=>todo.CancelLinkPreview());await Assert.ThrowsAsync<InvalidOperationException>(()=>todo.CreateBrowserAsync());
        Save(completed?"todo-link-history-unknown":"todo-link-adoption-unknown",cut.Markup,"original-link-acceptance-fence",nameof(Existing_record_adoption_retains_exact_review_across_lost_acknowledgement));
        cut.Find("[data-testid='todo-link-confirm']").Click();Assert.False(f.Controller.LinkAcceptanceUnknown);Assert.Null(f.Controller.LinkPreview);Assert.True(todo.CanSwitchSource);Assert.Equal(2,f.Client.AdoptionRequests.Count);Assert.All(f.Client.AdoptionRequests,p=>Assert.Same(original,p));
        Assert.Equal(remote.ToString("D"),Assert.Single(todo.Snapshot!.Document.Projections).HabiticaTaskId);Assert.Equal(task,todo.Snapshot.Document.Tasks.Single());Assert.Equal(completed?1:0,todo.Snapshot.Document.Completions.Count);Assert.Empty(todo.Snapshot.Document.HabitScores);Assert.Empty(f.Client.AcceptedIds);
    }
    [Fact]
    public async Task Original_terminal_nonacceptance_clears_only_the_adoption_uncertainty_fence()
    {
        var(f,todo,task)=await Setup(true);await todo.PreviewLinkAsync(task.Id,Guid.NewGuid());var original=f.Controller.LinkPreview;
        f.Client.LinkAction=_=>new(false,"Synthetic lost response",OutcomeUnknown:true);await todo.AdoptLinkAsync();Assert.True(f.Controller.LinkAcceptanceUnknown);
        f.Client.LinkAction=_=>new(false,"Original review refused",KnownNotAccepted:true);await todo.AdoptLinkAsync();
        var cut=Render<TodoTaskLinkEditor>(p=>p.Add(x=>x.TaskId,task.Id));Save("todo-link-original-refused",cut.Markup,"original-terminal-nonacceptance-fresh-review",nameof(Original_terminal_nonacceptance_clears_only_the_adoption_uncertainty_fence));Assert.True(cut.Find("[data-testid='todo-link-cancel']").HasAttribute("disabled")==false);Assert.False(f.Controller.LinkAcceptanceUnknown);Assert.Same(original,f.Controller.LinkPreview);
        todo.CancelLinkPreview();Assert.Null(f.Controller.LinkPreview);Assert.True(todo.CanSwitchSource);Assert.Empty(f.Client.State.Document.Projections);
    }
    [Fact]
    public async Task A_later_definitive_error_cannot_discard_an_earlier_uncertain_preparation()
    {
        var(f,todo,task)=await Setup(true);f.Client.LosePreparation=true;
        await todo.PrepareAsync(new(WorkspaceOperation.EnsureProjection,task.Id,task.Text,"",task.Priority,null,null,true,HabitScoreDirection.Up,1,null));
        var id=f.Controller.OriginalRequestId;f.Client.RejectPreparation=true;await todo.RetryOriginalPreviewAsync();
        Assert.True(f.Controller.PreparationUnknown);Assert.False(f.Controller.PreparationRejected);Assert.Throws<InvalidOperationException>(()=>todo.CancelPreview());
        f.Client.RejectPreparation=false;await todo.RetryOriginalPreviewAsync();Assert.All(f.Client.PreparationIds,request=>Assert.Equal(id,request));Assert.NotNull(f.Controller.Preview);Assert.Empty(f.Client.AcceptedIds);
    }
    [Fact]
    public async Task An_expired_known_preview_can_be_cancelled_but_is_never_accepted()
    {
        var(f,todo,task)=await Setup(true);f.Client.ExpirePreparation=true;var cut=Render<TodoTaskPanel>();cut.Find($"[data-testid='todo-link-{task.Id:D}']").Click();cut.Find("[data-testid='todo-link-create']").Click();
        var original=f.Controller.Preview!;Assert.Contains("expired or was superseded",cut.Markup);Save("defaults-expired-review",cut.Markup,"superseded-known-preview-needs-new-review",nameof(An_expired_known_preview_can_be_cancelled_but_is_never_accepted));cut.Find("[data-testid='todo-confirm']").Click();Assert.Empty(f.Client.AcceptedIds);Assert.Contains("expired",f.Controller.Message);Assert.False(f.Controller.AcceptanceUnknown);
        cut.Find("[data-testid='todo-cancel']").Click();f.Client.ExpirePreparation=false;cut.Find("[data-testid='todo-link-create']").Click();Assert.NotEqual(original.PreparedCommandId,f.Controller.Preview!.PreparedCommandId);
    }
    [Fact]
    public async Task Account_change_discards_private_link_review_and_the_previous_account_response_cannot_be_reused()
    {
        var(f,todo,task)=await Setup(true);await todo.PreviewLinkAsync(task.Id,Guid.NewGuid());f.Client.LoseLinkAcceptance=true;await todo.AdoptLinkAsync();Assert.True(f.Controller.LinkAcceptanceUnknown);
        f.Session.SetState(f.Session.State with {UserId=Guid.NewGuid().ToString("D")});
        Assert.Null(f.Controller.LinkPreview);Assert.Null(todo.Snapshot);Assert.False(f.Controller.LinkAcceptanceUnknown);Assert.Single(f.Client.AdoptionRequests);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>todo.AdoptLinkAsync());Assert.Single(f.Client.AdoptionRequests);
    }

    [Fact]
    public async Task A_confirmed_adoption_with_failed_refresh_stays_read_only_and_does_not_repeat_the_write()
    {
        var(f,todo,task)=await Setup(true);await todo.PreviewLinkAsync(task.Id,Guid.NewGuid());f.Client.FailLoads=true;await todo.AdoptLinkAsync();
        Assert.False(f.Controller.LinkAcceptanceUnknown);Assert.Null(f.Controller.LinkPreview);Assert.True(f.Controller.RequiresReload);Assert.True(todo.IsReadOnly);Assert.Contains("confirmed",f.Controller.Message);Assert.Single(f.Client.AdoptionRequests);
    }
    [Fact]
    public async Task Advanced_panel_retains_an_enabled_original_link_recovery_outside_disabled_fields()
    {
        var(f,todo,task)=await Setup(true);var cut=Render<CompanionTasksPanel>();cut.Find($"[data-testid='canonical-task-{task.Id:D}']").Click();cut.Find("[data-testid='canonical-link-id']").Change(Guid.NewGuid().ToString("D"));cut.Find("[data-testid='canonical-link-preview']").Click();
        f.Client.LoseLinkAcceptance=true;cut.Find("[data-testid='canonical-link-confirm']").Click();Assert.True(f.Controller.LinkAcceptanceUnknown);
        var recovery=cut.Find("[data-testid='canonical-link-confirm']");Assert.Null(recovery.Closest("fieldset"));Assert.False(recovery.HasAttribute("disabled"));
        Save("canonical-link-unknown",cut.Markup,"advanced-original-link-receipt-recovery",nameof(Advanced_panel_retains_an_enabled_original_link_recovery_outside_disabled_fields));
        recovery.Click();Assert.False(f.Controller.LinkAcceptanceUnknown);Assert.Equal(2,f.Client.AdoptionRequests.Count);Assert.Empty(f.Client.AcceptedIds);
    }

}
