using Bunit;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Components;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Workspace;

public sealed class TodoReviewEditorTests:BunitContext
{
    private static readonly DateTimeOffset Now=new(2026,10,1,0,0,0,TimeSpan.Zero);
    private static CompanionTask Task(string title="Voluntary task review")=>new(Guid.NewGuid(),1,title,CompanionTaskKind.Todo,CompanionTaskState.Active,CompanionPriority.High,Now,Now,Deadline:new(2026,11,1),PlannedDate:new(2026,10,27));
    private async Task<TodoOrganizationFixture> Setup(CompanionTask task)
    {
        JSInterop.Mode=JSRuntimeMode.Loose;Services.AddMudServices();var f=new TodoOrganizationFixture();await f.Load(document:new(){Id=Guid.NewGuid(),Revision=10,Tasks=[task]});Services.AddSingleton<IAppSessionController>(f.Task.Session);Services.AddSingleton<IWorkspaceCloudClient>(f.Task.Client);Services.AddSingleton(f.Task.Database);Services.AddSingleton(f.Task.Controller);Services.AddSingleton(f.Organization);Services.AddSingleton(f.Todo);Services.AddSingleton<TodoTaskDraftStore>();Services.AddSingleton(TimeProvider.System);return f;
    }
    private static void Save(string name,string markup,string state,string test)=>Qualification.PageLayoutEvidence.Save(name,markup,"/todo/tasks",state,["form","actions","confirmation","disabled-controls","error","navigation"],test);
    [Fact]
    public async Task Set_review_mark_reviewed_and_original_uncertain_receipt_preserve_task_and_game_state()
    {
        var task=Task(new string('W',150)+" Optional review");using var f=await Setup(task);var cut=Render<TodoHierarchyPanel>();cut.Find($"[data-testid='tree-workflow-{task.Id:D}']").Click();Save("todo-review-empty",cut.Markup,"optional-review-no-cue",nameof(Set_review_mark_reviewed_and_original_uncertain_receipt_preserve_task_and_game_state));
        cut.Find("[data-testid='review-instant']").Input("2099-11-01T09:00:00Z");cut.Find("[data-testid='review-zone']").Input("Asia/Novosibirsk");Assert.Contains("2099-11-01T09:00:00Z",cut.Find("[data-testid='review-draft-instant']").TextContent);cut.Find("[data-testid='review-prepare']").Click();var preview=f.Task.Controller.Preview!;Assert.Equal(WorkspaceOperation.ReviewTask,preview.Command.Operation);Assert.False(preview.MayAffectHabitica);Assert.Equal("Asia/Novosibirsk",preview.Command.Patch!.Review!.TimeZoneId);Assert.Contains("2099-11-01T09:00:00.0000000Z",cut.Find("[data-testid='todo-review']").TextContent);Save("todo-review-confirm",cut.Markup,"explicit-set-review-instant-and-zone",nameof(Set_review_mark_reviewed_and_original_uncertain_receipt_preserve_task_and_game_state));
        f.Task.Client.LoseAcceptance=true;cut.Find("[data-testid='todo-confirm']").Click();Assert.True(f.Task.Controller.AcceptanceUnknown);Save("todo-review-unknown",cut.Markup,"original-review-acceptance-unknown",nameof(Set_review_mark_reviewed_and_original_uncertain_receipt_preserve_task_and_game_state));cut.Find("[data-testid='todo-recover']").Click();Assert.Equal(preview.PreparedCommandId,f.Task.Controller.LastReceipt!.CommandId);Assert.Single(f.Todo.Snapshot!.Document.ReviewEvents!);
        cut.Find("[data-testid='review-reload']").Click();cut.Find("[data-testid='review-action']").Change("MarkReviewed");Assert.Empty(cut.FindAll("[data-testid='review-instant']"));Assert.Contains("Mark reviewed",cut.Find("[data-testid='review-draft-action']").TextContent);cut.Find("[data-testid='review-prepare']").Click();Assert.Equal(TaskReviewAction.MarkReviewed,f.Task.Controller.Preview!.Command.Patch!.Review!.Action);cut.Find("[data-testid='todo-confirm']").Click();var saved=f.Todo.Snapshot!.Document.Tasks.Single();Assert.Null(saved.ReviewAfterUtc);Assert.Equal(task with{Revision=saved.Revision,UpdatedAtUtc=saved.UpdatedAtUtc},saved);Assert.Empty(f.Todo.Snapshot.Document.Completions);Assert.All(f.Todo.Snapshot.Commands,e=>Assert.Empty(e.Effects));Assert.Equal(2,f.Todo.Snapshot.Document.ReviewEvents!.Count);
        cut.Find("[data-testid='review-reload']").Click();Assert.Contains("MarkReviewed",cut.Markup);Save("todo-review-history",cut.Markup,"retained-review-actions-with-no-task-completion",nameof(Set_review_mark_reviewed_and_original_uncertain_receipt_preserve_task_and_game_state));
    }
    [Fact]
    public async Task Invalid_instant_timezone_snooze_and_future_manual_stale_flags_do_not_prepare()
    {
        var task=Task();using var f=await Setup(task);var cut=Render<TodoHierarchyPanel>();cut.Find($"[data-testid='tree-workflow-{task.Id:D}']").Click();
        foreach(var text in new[]{"2099-11-01","2099-11-01T09:00:00+07:00","2099-02-30T09:00:00Z"}){cut.Find("[data-testid='review-instant']").Input(text);cut.Find("[data-testid='review-prepare']").Click();Assert.Null(f.Task.Controller.Preview);}
        cut.Find("[data-testid='review-instant']").Input("2099-11-01T09:00:00Z");cut.Find("[data-testid='review-zone']").Input("Missing/Timezone");cut.Find("[data-testid='review-prepare']").Click();Assert.Null(f.Task.Controller.Preview);Save("todo-review-invalid",cut.Markup,"invalid-timezone-refuses-preparation",nameof(Invalid_instant_timezone_snooze_and_future_manual_stale_flags_do_not_prepare));
        cut.Find("[data-testid='review-zone']").Input("UTC");cut.Find("[data-testid='review-action']").Change("Snooze");cut.Find("[data-testid='review-instant']").Input("2099-11-01T09:00:00Z");cut.Find("[data-testid='review-prepare']").Click();Assert.Null(f.Task.Controller.Preview);
        cut.Find("[data-testid='review-action']").Change("SetStale");cut.Find("[data-testid='review-instant']").Input("2099-11-01T09:00:00Z");cut.Find("[data-testid='review-prepare']").Click();Assert.Null(f.Task.Controller.Preview);Assert.Empty(f.Task.Client.PreparationIds);
    }
    [Fact]
    public async Task Positive_snooze_and_manual_flag_controls_prepare_separate_native_actions()
    {
        var task=Task() with{ReviewAfterUtc=DateTimeOffset.Parse("2098-11-01T09:00:00Z")};using var f=await Setup(task);var cut=Render<TodoHierarchyPanel>();cut.Find($"[data-testid='tree-workflow-{task.Id:D}']").Click();
        foreach(var action in new[]{"Snooze","SetStale","ClearStale"})
        {
            cut.Find("[data-testid='review-action']").Change(action);
            if(action!="ClearStale")cut.Find("[data-testid='review-instant']").Input(action=="Snooze"?"2099-11-01T09:00:00Z":"2026-01-01T09:00:00Z");
            cut.Find("[data-testid='review-prepare']").Click();var preview=f.Task.Controller.Preview!;Assert.Equal(Enum.Parse<TaskReviewAction>(action),preview.Command.Patch!.Review!.Action);Assert.False(preview.MayAffectHabitica);cut.Find("[data-testid='todo-cancel']").Click();
        }
        Assert.Empty(f.Task.Client.AcceptedIds);Assert.Null(f.Todo.Snapshot!.Document.ReviewEvents);
    }
    [Fact]
    public async Task Legacy_due_cues_and_manual_stale_flags_display_explicitly_and_source_changes_fence_drafts()
    {
        var task=Task() with{ReviewAfterUtc=Now,StaleSinceUtc=Now.AddDays(-1)};using var f=await Setup(task);var cut=Render<TodoHierarchyPanel>();cut.Find($"[data-testid='tree-workflow-{task.Id:D}']").Click();Assert.Contains("reached",cut.Find("[data-testid='review-cue-state']").TextContent);Assert.Contains("UTC",cut.Markup);Assert.Contains("Legacy cues can exist",cut.Markup);
        cut.Find("[data-testid='review-action']").Change("Clear");cut.Find("[data-testid='review-prepare']").Click();Assert.Equal(TaskReviewAction.Clear,f.Task.Controller.Preview!.Command.Patch!.Review!.Action);var focusBeforeCancel=JSInterop.Invocations.Count(row=>row.Identifier=="Blazor._internal.domWrapper.focus");cut.Find("[data-testid='todo-cancel']").Click();cut.WaitForAssertion(()=>Assert.True(JSInterop.Invocations.Count(row=>row.Identifier=="Blazor._internal.domWrapper.focus")>focusBeforeCancel));
        f.Task.Client.State=f.Task.Client.State with{Document=f.Task.Client.State.Document with{Revision=11},StoreVersion=11};await f.Todo.LoadSelectedAsync();cut.WaitForAssertion(()=>Assert.True(cut.Find("[data-testid='task-review-editor'] fieldset").HasAttribute("disabled")));Save("todo-review-stale",cut.Markup,"changed-revision-disables-review-controls",nameof(Legacy_due_cues_and_manual_stale_flags_display_explicitly_and_source_changes_fence_drafts));
        f.Task.Session.SetState(f.Task.Session.State with{UserId="00000000-0000-4000-8000-000000000012"});cut.WaitForAssertion(()=>Assert.Empty(cut.FindAll("[data-testid='task-review-editor']")));Assert.Empty(f.Task.Client.AcceptedIds);
    }
}
