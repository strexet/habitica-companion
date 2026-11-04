using Bunit;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Components;
using Habitica.WebApp.Pages;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Workspace;

public sealed class TodoSavedListPanelTests:BunitContext
{
    private static readonly DateTimeOffset Now=new(2026,10,1,0,0,0,TimeSpan.Zero);
    private static CompanionTask Task(string text)=>new(Guid.NewGuid(),1,text,CompanionTaskKind.Todo,CompanionTaskState.Active,CompanionPriority.High,Now,Now);
    private static WorkspaceSavedTaskQuery All=>new(WorkspaceSavedTaskSource.Tasks,new());
    private async Task<TodoOrganizationFixture> Setup(WorkspaceDocument? document=null,bool cloud=true)
    {
        JSInterop.Mode=JSRuntimeMode.Loose;JSInterop.SetupModule("./js/field-focus.js").SetupVoid("revealFocusedElement", _=>true).SetVoidResult();Services.AddMudServices();var f=new TodoOrganizationFixture();await f.Load(cloud,document??new(){Id=Guid.NewGuid(),Revision=10});Services.AddSingleton<IAppSessionController>(f.Task.Session);Services.AddSingleton<IWorkspaceCloudClient>(f.Task.Client);Services.AddSingleton(f.Task.Database);Services.AddSingleton(f.Task.Controller);Services.AddSingleton(f.Organization);Services.AddSingleton(f.Todo);Services.AddSingleton<TodoTaskDraftStore>();Services.AddSingleton(TimeProvider.System);return f;
    }
    private static void Save(string name,string markup,string state,string test,string route="/todo/lists")=>Qualification.PageLayoutEvidence.Save(name,markup,route,state,["form","actions","confirmation","disabled-controls","error","navigation"],test);
    private static void Name(IRenderedComponent<TodoSavedListPanel> cut,string name)
    {
        if(cut.Find("[data-testid='saved-list-authoring']").HasAttribute("hidden"))cut.Find("[data-testid='saved-list-new']").Click();
        if(cut.Find("[data-testid='saved-list-advanced-toggle']").GetAttribute("aria-expanded")=="false")cut.Find("[data-testid='saved-list-advanced-toggle']").Click();
        cut.Find("[data-testid='saved-list-name']").Input(name);
    }
    private static void Review(IRenderedComponent<TodoSavedListPanel> cut)=>cut.Find("form").Submit();
    private static void Confirm(IRenderedComponent<TodoSavedListPanel> cut)=>cut.Find("[data-testid='saved-list-confirm']").Click();
    [Fact]
    public async Task Browse_first_lists_and_compact_authoring_preserve_hidden_definition_and_local_drafts()
    {
        var retained=new WorkspaceSavedTaskQuery(WorkspaceSavedTaskSource.Tasks,new(Priority:CompanionPriority.High,DeadlineRelative:WorkspaceRelativeDatePreset.ThisWeek),"Europe/Paris");
        var special=new WorkspaceSavedTaskView(Guid.NewGuid(),1,"Retained relative definition",retained);
        var views=Enumerable.Range(0,24).Select(i=>new WorkspaceSavedTaskView(Guid.NewGuid(),1,"Existing list "+i,All)).Append(special).ToArray();
        var tasks=Enumerable.Range(0,25).Select(i=>Task("Existing work "+i)).ToArray();
        using var f=await Setup(new(){Id=Guid.NewGuid(),Revision=10,SavedTaskViews=views,Tasks=tasks});var cut=Render<TodoSavedListPanel>();
        Assert.True(cut.Find("[data-testid='saved-list-authoring']").HasAttribute("hidden"));
        Assert.Equal(20,cut.FindAll("[data-testid^='saved-list-open-']").Count);
        Save("saved-layout-browse",cut.Markup,"existing-lists-before-optional-authoring",nameof(Browse_first_lists_and_compact_authoring_preserve_hidden_definition_and_local_drafts));
        var evidence=Environment.GetEnvironmentVariable("HABITICA_BROWSER_EVIDENCE");if(!string.IsNullOrEmpty(evidence))File.WriteAllText(Path.Combine(evidence,"saved-layout-journal.json"),WorkspaceJournalJson.Serialize(f.Todo.Snapshot!));
        cut.Find($"[data-testid='saved-list-open-{views[0].Id:D}']").Click();Assert.Equal(20,cut.FindAll("[data-testid^='saved-list-task-']").Count);
        cut.FindAll("button").Single(b=>b.TextContent.Trim()=="Next matching tasks").Click();Assert.Equal(5,cut.FindAll("[data-testid^='saved-list-task-']").Count);
        Save("saved-layout-results",cut.Markup,"existing-selected-results-without-authoring",nameof(Browse_first_lists_and_compact_authoring_preserve_hidden_definition_and_local_drafts));
        cut.FindAll("button").Single(b=>b.TextContent.Trim()=="Next definitions").Click();cut.Find($"[data-testid='saved-list-edit-{special.Id:D}']").Click();
        Assert.False(cut.Find("[data-testid='saved-list-authoring']").HasAttribute("hidden"));Assert.Equal("true",cut.Find("[data-testid='saved-list-advanced-toggle']").GetAttribute("aria-expanded"));
        Assert.Equal("Europe/Paris",cut.Find("[data-testid='saved-list-timezone']").GetAttribute("value"));
        cut.Find("[data-testid='saved-list-advanced-toggle']").Click();Assert.True(cut.Find("[data-testid='saved-list-additional-filters']").HasAttribute("hidden"));
        cut.Find("[data-testid='saved-list-name']").Input("Renamed with hidden exact dates");Review(cut);Assert.Null(f.Organization.Preview!.Command.Patch!.Query);Assert.Equal(retained,f.Organization.Preview.Snapshot.Query);
        Assert.Contains("Europe/Paris",cut.Find("[data-testid='saved-list-review']").TextContent);Assert.Contains("High",cut.Find("[data-testid='saved-list-review']").TextContent);
        Save("saved-layout-review",cut.Markup,"complete-definition-review-with-collapsed-filters",nameof(Browse_first_lists_and_compact_authoring_preserve_hidden_definition_and_local_drafts));
        cut.FindAll("button").Single(b=>b.TextContent.Trim()=="Cancel saved list review").Click();
        cut.Find("[data-testid='saved-list-close-authoring']").Click();Assert.True(cut.Find("[data-testid='saved-list-authoring']").HasAttribute("hidden"));
        cut.Find("[data-testid='saved-list-resume']").Click();Assert.Equal("Renamed with hidden exact dates",cut.Find("[data-testid='saved-list-name']").GetAttribute("value"));Assert.Equal("Europe/Paris",cut.Find("[data-testid='saved-list-timezone']").GetAttribute("value"));cut.Find("[data-testid='saved-list-close-authoring']").Click();
        cut.Find("[data-testid='saved-list-new']").Click();cut.Find("[data-testid='saved-list-name']").Input("Unsaved new list");cut.Find("[data-testid='saved-list-search']").Input("Existing work");
        Assert.Equal("false",cut.Find("[data-testid='saved-list-advanced-toggle']").GetAttribute("aria-expanded"));
        cut.Find("[data-testid='saved-list-close-authoring']").Click();cut.Find("[data-testid='saved-list-new']").Click();
        Assert.Equal("Unsaved new list",cut.Find("[data-testid='saved-list-name']").GetAttribute("value"));Assert.Equal("Existing work",cut.Find("[data-testid='saved-list-search']").GetAttribute("value"));
        Save("saved-layout-compact",cut.Markup,"three-basic-fields-and-retained-new-draft",nameof(Browse_first_lists_and_compact_authoring_preserve_hidden_definition_and_local_drafts));
        Assert.Equal(views,f.Todo.Snapshot!.Document.SavedTaskViews);Assert.Equal(tasks,f.Todo.Snapshot.Document.Tasks);Assert.Empty(f.Api.Acceptances);Assert.Empty(f.Todo.Snapshot.Commands);
    }
    [Fact]
    public async Task Collapsed_advanced_validation_reopens_the_exact_invalid_fields_without_clearing_values()
    {
        using var f=await Setup();var cut=Render<TodoSavedListPanel>();
        Save("saved-layout-empty",cut.Markup,"no-definitions-with-explicit-New-list",nameof(Collapsed_advanced_validation_reopens_the_exact_invalid_fields_without_clearing_values));
        cut.Find("[data-testid='saved-list-new']").Click();cut.Find("[data-testid='saved-list-name']").Input("Keep invalid fixed and relative draft");
        cut.Find("[data-testid='saved-list-advanced-toggle']").Click();cut.Find("[data-testid='saved-list-planned-from']").Change("2026-11-08");cut.Find("[data-testid='saved-list-planned-to']").Change("2026-11-01");cut.Find("[data-testid='saved-list-planned-relative']").Change("Today");cut.Find("[data-testid='saved-list-timezone']").Change("UTC");
        cut.Find("[data-testid='saved-list-advanced-toggle']").Click();Review(cut);
        Assert.Equal("true",cut.Find("[data-testid='saved-list-advanced-toggle']").GetAttribute("aria-expanded"));Assert.Contains("conflict",cut.Find("[data-testid='saved-list-errors']").TextContent,StringComparison.OrdinalIgnoreCase);
        Assert.Equal("2026-11-08",cut.Find("[data-testid='saved-list-planned-from']").GetAttribute("value"));Assert.Equal("Today",cut.Find("[data-testid='saved-list-planned-relative']").GetAttribute("value"));Assert.Null(f.Organization.Preview);Assert.Empty(f.Api.Requests);
        Save("saved-layout-invalid",cut.Markup,"collapsed-invalid-filters-revealed-with-exact-draft",nameof(Collapsed_advanced_validation_reopens_the_exact_invalid_fields_without_clearing_values));
    }
    [Fact]
    public async Task Create_reviews_all_fixed_filters_and_reads_exact_current_tasks_without_duplication()
    {
        var category=new WorkspaceCategory(Guid.NewGuid(),1,"Category");var project=new WorkspaceProject(Guid.NewGuid(),1,"Project",category.Id,Alias:new string('a',64));var label=new WorkspaceLabel(Guid.NewGuid(),1,"Label");var match=Task(new string('W',120)+" Read manual") with{Notes="Read carefully",ProjectId=project.Id,LabelIds=[label.Id],PlannedDate=new(2026,11,2),Deadline=new(2026,11,3),ExecutionStage=TodoExecutionStage.Next};var other=Task("Other task");var tasks=new[]{match,other};using var f=await Setup(new(){Id=Guid.NewGuid(),Revision=10,Categories=[category],Projects=[project],Labels=[label],Tasks=tasks});var cut=Render<TodoSavedListPanel>();Name(cut,new string('N',150)+" saved selection");var focus=JSInterop.Invocations.Count(row=>row.Identifier=="Blazor._internal.domWrapper.focus");cut.Find("[data-testid='saved-list-search']").Input("Read");Assert.Equal(focus,JSInterop.Invocations.Count(row=>row.Identifier=="Blazor._internal.domWrapper.focus"));
        foreach(var pair in new[]{("saved-list-kind","Todo"),("saved-list-state","Active"),("saved-list-priority","High"),("saved-list-stage","Next"),("saved-list-projection","Unlinked"),("plan-project",project.Id.ToString()),("plan-category",category.Id.ToString()),("plan-label",label.Id.ToString()),("saved-list-planned-from","2026-11-01"),("saved-list-planned-to","2026-11-07"),("saved-list-deadline-from","2026-11-02"),("saved-list-deadline-to","2026-11-08")})cut.Find($"[data-testid='{pair.Item1}']").Change(pair.Item2);
        Assert.Contains("Alias: "+project.Alias,cut.Markup);
        Save("todo-saved-editor",cut.Markup,"long-name-full-fixed-filter-editor",nameof(Create_reviews_all_fixed_filters_and_reads_exact_current_tasks_without_duplication));Review(cut);var preview=f.Organization.Preview!;Assert.Equal(WorkspaceOrganizationKind.SavedTaskView,preview.Command.Kind);var query=preview.Command.Patch!.Query!;Assert.Equal(WorkspaceSavedTaskSource.Tasks,query.Source);Assert.Equal(project.Id,query.Filter.ProjectId);Assert.Equal(category.Id,query.Filter.CategoryId);Assert.Equal(label.Id,query.Filter.LabelId);Assert.Equal(new DateOnly(2026,11,1),query.Filter.PlannedFrom);Assert.Equal(ProjectionState.Unlinked,query.Filter.ProjectionState);Assert.False(preview.MayAffectHabitica);Assert.Contains(project.Id.ToString(),cut.Find("[data-testid='saved-list-review']").TextContent);Assert.Contains("2026-11-01",cut.Find("[data-testid='saved-list-review']").TextContent);
        Save("todo-saved-review",cut.Markup,"complete-query-and-identity-review",nameof(Create_reviews_all_fixed_filters_and_reads_exact_current_tasks_without_duplication));Confirm(cut);var view=f.Todo.Snapshot!.Document.SavedTaskViews!.Single();Assert.Equal(query,view.Query);Assert.Equal(tasks,f.Todo.Snapshot.Document.Tasks);Assert.Single(cut.FindAll("[data-testid^='saved-list-task-']"));Assert.Contains(match.Text,cut.Find("[data-testid^='saved-list-task-']").TextContent);Assert.Empty(f.Task.Client.PreparationIds);Assert.Empty(f.Todo.Snapshot.Commands);Assert.Single(f.Todo.Snapshot.Document.OrganizationHistory!);Save("todo-saved-results",cut.Markup.Replace("<details class=\"saved-definition-details\">","<details class=\"saved-definition-details\" open>",StringComparison.Ordinal),"saved-fixed-query-and-matching-task",nameof(Create_reviews_all_fixed_filters_and_reads_exact_current_tasks_without_duplication));
    }
    [Fact]
    public async Task Full_replacement_clears_filters_and_name_only_edit_preserves_the_definition()
    {
        var query=new WorkspaceSavedTaskQuery(WorkspaceSavedTaskSource.Tasks,new(Query:"Specific",PlannedFrom:new(2026,11,1),PlannedTo:new(2026,11,7)));var view=new WorkspaceSavedTaskView(Guid.NewGuid(),1,"Original",query);var task=Task("Specific") with{PlannedDate=new(2026,11,2)};using var f=await Setup(new(){Id=Guid.NewGuid(),Revision=10,SavedTaskViews=[view],Tasks=[task,Task("Other")]});var cut=Render<TodoSavedListPanel>();cut.Find($"[data-testid='saved-list-edit-{view.Id:D}']").Click();Review(cut);Assert.Null(f.Organization.Preview);Assert.Empty(f.Api.Requests);Assert.Contains("Change the name or filters",cut.Markup);Name(cut,"Renamed");Review(cut);Assert.Null(f.Organization.Preview!.Command.Patch!.Query);Assert.Equal(query,f.Organization.Preview.Snapshot.Query);Confirm(cut);
        cut.Find($"[data-testid='saved-list-edit-{view.Id:D}']").Click();cut.Find("[data-testid='saved-list-clear']").Click();Review(cut);Assert.Equal(All,f.Organization.Preview!.Command.Patch!.Query);Assert.Contains("Unbounded",cut.Find("[data-testid='saved-list-review']").TextContent);Confirm(cut);Assert.Equal(2,cut.FindAll("[data-testid^='saved-list-task-']").Count);Assert.Equal(All,f.Todo.Snapshot!.Document.SavedTaskViews!.Single().Query);Assert.Empty(f.Todo.Snapshot.Commands);
    }
    [Fact]
    public async Task Unblocked_source_retains_conflicting_fields_until_explicit_correction_and_evaluates_dependencies_and_stage()
    {
        var completed=Task("Prerequisite") with{State=CompanionTaskState.Completed};var pending=Task("Pending prerequisite");var ready=Task("Work ready") with{DependsOn=[completed.Id]};var blocked=Task("Work blocked") with{DependsOn=[pending.Id]};var waiting=Task("Work waiting") with{State=CompanionTaskState.Waiting};using var f=await Setup(new(){Id=Guid.NewGuid(),Revision=10,Tasks=[completed,pending,ready,blocked,waiting],Completions=[new(Guid.NewGuid(),2,completed.Id,Guid.NewGuid(),Now.AddDays(1))]});var cut=Render<TodoSavedListPanel>();Name(cut,"Ready work");cut.Find("[data-testid='saved-list-kind']").Change("Daily");cut.Find("[data-testid='saved-list-state']").Change("Paused");cut.Find("[data-testid='saved-list-source']").Change("UnblockedTodos");Assert.Equal("Daily",cut.Find("[data-testid='saved-list-kind']").GetAttribute("value"));Assert.Equal("Paused",cut.Find("[data-testid='saved-list-state']").GetAttribute("value"));Review(cut);Assert.Null(f.Organization.Preview);Assert.False(cut.Find("[data-testid='saved-list-kind']").HasAttribute("disabled"));cut.Find("[data-testid='saved-list-kind']").Change("");cut.Find("[data-testid='saved-list-state']").Change("");cut.Find("[data-testid='saved-list-search']").Input("work");cut.Find("[data-testid='saved-list-stage']").Change("Inbox");Review(cut);var query=f.Organization.Preview!.Command.Patch!.Query!;Assert.Null(query.Filter.Kind);Assert.Null(query.Filter.State);Confirm(cut);Assert.Single(cut.FindAll("[data-testid^='saved-list-task-']"));Assert.NotNull(cut.Find($"[data-testid='saved-list-task-{ready.Id:D}']"));Assert.Empty(cut.FindAll($"[data-testid='saved-list-task-{blocked.Id:D}']"));Save("todo-saved-unblocked",cut.Markup,"unblocked-active-Todo-stage-source",nameof(Unblocked_source_retains_conflicting_fields_until_explicit_correction_and_evaluates_dependencies_and_stage));
    }
    [Fact]
    public async Task Invalid_combinations_ranges_and_reference_changes_never_prepare_a_native_command()
    {
        using var f=await Setup();var cut=Render<TodoSavedListPanel>();Name(cut,"Invalid filters");cut.Find("[data-testid='saved-list-kind']").Change("Daily");cut.Find("[data-testid='saved-list-stage']").Change("Next");Review(cut);Assert.Contains("Stage filters require active Todos",cut.Markup);cut.Find("[data-testid='saved-list-stage']").Change("");cut.Find("[data-testid='saved-list-planned-from']").Change("2026-11-08");cut.Find("[data-testid='saved-list-planned-to']").Change("2026-11-01");Review(cut);Assert.Contains("Planned from must be on or before planned through",cut.Markup);cut.Find("[data-testid='saved-list-clear']").Click();await cut.InvokeAsync(()=>cut.FindComponent<TodoPlacementPicker>().Instance.ValueChanged.InvokeAsync(Guid.NewGuid()));Review(cut);Assert.Null(f.Organization.Preview);Assert.Empty(f.Api.Requests);Assert.Empty(f.Task.Client.PreparationIds);Save("todo-saved-error",cut.Markup,"invalid-or-stale-reference-refusal",nameof(Invalid_combinations_ranges_and_reference_changes_never_prepare_a_native_command));
        foreach(var draft in new[]{new TodoSavedListDraft{Source="Unknown"},new TodoSavedListDraft{Source="UnblockedTodos",Kind="Todo"},new TodoSavedListDraft{State="Cancelled",Stage="Next"},new TodoSavedListDraft{PlannedFrom="2026-02-30"},new TodoSavedListDraft{Search=new string('x',257)}})Assert.Throws<InvalidOperationException>(()=>draft.Query());
    }
    [Fact]
    public async Task Archived_references_remain_explicit_and_archiving_the_selected_view_never_falls_back()
    {
        var project=new WorkspaceProject(Guid.NewGuid(),1,"Archived project",IsArchived:true);var query=new WorkspaceSavedTaskQuery(WorkspaceSavedTaskSource.Tasks,new(ProjectId:project.Id));var view=new WorkspaceSavedTaskView(Guid.NewGuid(),1,"Retained",query);var task=Task("Assigned") with{ProjectId=project.Id};using var f=await Setup(new(){Id=Guid.NewGuid(),Revision=10,Projects=[project],Tasks=[task,Task("Unassigned")],SavedTaskViews=[view]});var cut=Render<TodoSavedListPanel>();cut.Find($"[data-testid='saved-list-edit-{view.Id:D}']").Click();Assert.Contains("retained selection",cut.Find("[data-testid='plan-project']").TextContent);Name(cut,"Retained renamed");Review(cut);Assert.Null(f.Organization.Preview!.Command.Patch!.Query);Confirm(cut);Assert.Single(cut.FindAll("[data-testid^='saved-list-task-']"));cut.Find($"[data-testid='saved-list-archive-{view.Id:D}']").Click();Assert.Equal(query,f.Organization.Preview!.Snapshot.Query);Confirm(cut);Assert.True(f.Todo.Snapshot!.Document.SavedTaskViews!.Single().IsArchived);Assert.NotNull(cut.Find("[data-testid='saved-list-unavailable']"));Assert.Empty(cut.FindAll("[data-testid^='saved-list-task-']"));Assert.Equal(2,f.Todo.Snapshot.Document.Tasks.Count);Assert.Equal(2,f.Todo.Snapshot.Document.OrganizationHistory!.Count);Save("todo-saved-archived",cut.Markup,"archived-selected-definition-without-fallback",nameof(Archived_references_remain_explicit_and_archiving_the_selected_view_never_falls_back));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task Lost_native_preparation_and_acceptance_recover_original_list_without_duplicating_tasks(bool cloud)
    {
        using var f=await Setup(cloud:cloud);if(!cloud){await f.Todo.PrepareAsync(WorkspaceTaskFixture.Todo());await f.Todo.ConfirmAsync();}var before=f.Todo.Snapshot!.Document.Tasks;var cut=Render<TodoSavedListPanel>();Name(cut,"Original list");if(cloud)f.Api.LosePreparation=true;Review(cut);if(cloud){var request=Assert.Single(f.Api.Requests);cut.Find("[data-testid='saved-list-retry']").Click();Assert.Equal(new[]{request,request},f.Api.Requests);}var id=f.Organization.Preview!.PreparedCommandId;if(cloud)f.Api.LoseAcceptance=true;else f.Task.Js.LoseCommitAcknowledgement=true;Confirm(cut);Assert.True(f.Organization.AcceptanceUnknown);Assert.False(f.Todo.CanSwitchSource);if(cloud)Save("todo-saved-unknown",cut.Markup,"original-native-query-acceptance-unknown",nameof(Lost_native_preparation_and_acceptance_recover_original_list_without_duplicating_tasks));cut.Find("[data-testid='saved-list-recover']").Click();Assert.Equal(id,f.Organization.LastReceipt!.CommandId);Assert.Single(f.Todo.Snapshot!.Document.SavedTaskViews!);Assert.Single(f.Todo.Snapshot.Document.OrganizationHistory!);Assert.Equal(before,f.Todo.Snapshot.Document.Tasks);if(cloud)Assert.Single(f.Api.Acceptances);
    }
    [Fact]
    public async Task Revisions_keep_fixed_dates_reset_pages_and_fence_stale_private_drafts()
    {
        var view=new WorkspaceSavedTaskView(Guid.NewGuid(),1,"Fixed week",new(WorkspaceSavedTaskSource.Tasks,new(PlannedFrom:new(2026,11,1),PlannedTo:new(2026,11,7))));var tasks=Enumerable.Range(0,25).Select(i=>Task("Task "+i) with{PlannedDate=new(2026,11,2)}).ToArray();var views=Enumerable.Range(0,24).Select(i=>new WorkspaceSavedTaskView(Guid.NewGuid(),1,"Same name",All)).Append(view).ToArray();using var f=await Setup(new(){Id=Guid.NewGuid(),Revision=10,SavedTaskViews=views,Tasks=tasks});var cut=Render<TodoSavedListPanel>();Assert.Equal(20,cut.FindAll("[data-testid^='saved-list-open-']").Count);Assert.Contains(views[0].Id.ToString(),cut.Find("[aria-label='Saved definitions'] article strong").TextContent);cut.FindAll("button").Single(b=>b.TextContent.Trim()=="Next definitions").Click();Assert.Equal(5,cut.FindAll("[data-testid^='saved-list-open-']").Count);cut.Find($"[data-testid='saved-list-open-{view.Id:D}']").Click();Assert.Equal(20,cut.FindAll("[data-testid^='saved-list-task-']").Count);cut.FindAll("button").Single(b=>b.TextContent.Trim()=="Next matching tasks").Click();Assert.Equal(5,cut.FindAll("[data-testid^='saved-list-task-']").Count);cut.Find($"[data-testid='saved-list-edit-{view.Id:D}']").Click();Name(cut,"Private draft");f.Task.Client.State=f.Task.Client.State with{Document=f.Task.Client.State.Document with{Revision=11,Tasks=tasks.Select(row=>row with{PlannedDate=new(2026,11,8),Revision=11}).ToArray()},StoreVersion=11};await f.Todo.LoadSelectedAsync();Assert.Contains("0 matching nonarchived tasks",cut.Markup);Assert.True(cut.Find("form fieldset").HasAttribute("disabled"));Assert.Equal(view.Query,f.Todo.Snapshot!.Document.SavedTaskViews!.Last().Query);Review(cut);Assert.Null(f.Organization.Preview);Assert.Empty(f.Api.Requests);cut.Find("[data-testid='saved-list-reload']").Click();Assert.Equal("Fixed week",cut.Find("[data-testid='saved-list-name']").GetAttribute("value"));f.Task.Session.SetState(f.Task.Session.State with{UserId="00000000-0000-4000-8000-000000000012"});cut.WaitForAssertion(()=>Assert.DoesNotContain("Private draft",cut.Markup));Assert.Empty(cut.FindAll("[data-testid='saved-list-name']"));
    }
    [Fact]
    public async Task Saved_page_and_cross_page_native_recovery_show_complete_query_and_one_heading()
    {
        using var f=await Setup();var page=Render<TodoSavedListsPage>();Assert.Single(page.FindAll("h1"));var cut=page.FindComponent<TodoSavedListPanel>();Name(cut,"Full recovery target");Review(cut);var other=Render<TodoProjectsPage>();Assert.Single(other.FindAll("h1"));Assert.NotNull(other.Find(".todo-saved-query"));Assert.Contains("Unbounded",other.Find(".todo-saved-query").TextContent);Assert.Contains("Any label",other.Find(".todo-saved-query").TextContent);Save("todo-saved-project-recovery",other.Markup,"complete-saved-query-review-from-Projects",nameof(Saved_page_and_cross_page_native_recovery_show_complete_query_and_one_heading),"/todo/projects");Save("todo-saved-page",page.Markup,"composed-saved-list-page-with-native-review",nameof(Saved_page_and_cross_page_native_recovery_show_complete_query_and_one_heading));
        var direct=Render<TodoTaskPanel>(p=>p.Add(x=>x.ShowKindSelector,true));Assert.NotNull(direct.Find("a[href='/todo/lists']"));Assert.NotNull(direct.Find("[data-testid='todo-kind']"));Save("todo-saved-entrypoint",direct.Markup,"direct-task-saved-list-navigation",nameof(Saved_page_and_cross_page_native_recovery_show_complete_query_and_one_heading),"/tasks");Save("todo-saved-entrypoint-source",direct.Markup.Replace("<details class=\"todo-source-picker\">","<details class=\"todo-source-picker\" open>",StringComparison.Ordinal),"expanded-workspace-controls-and-navigation",nameof(Saved_page_and_cross_page_native_recovery_show_complete_query_and_one_heading),"/tasks");
    }
    [Fact]
    public async Task Confirmed_save_with_failed_refresh_keeps_receipt_without_fabricated_empty_results()
    {
        var task=Task("Retained task");using var f=await Setup(new(){Id=Guid.NewGuid(),Revision=10,Tasks=[task]});var cut=Render<TodoSavedListPanel>();Name(cut,"Confirmed list");Review(cut);var id=f.Organization.Preview!.PreparedCommandId;f.Task.Client.FailLoads=true;Confirm(cut);Assert.True(f.Organization.LastReceipt!.SavedInCompanion);Assert.Equal(id,f.Organization.LastReceipt.CommandId);Assert.Contains("reload required",cut.Find("[data-testid='saved-list-receipt']").TextContent);Assert.Null(f.Todo.Snapshot);Assert.Empty(cut.FindAll("[data-testid='saved-list-count']"));Save("todo-saved-unavailable",cut.Markup,"confirmed-native-save-with-unavailable-task-refresh",nameof(Confirmed_save_with_failed_refresh_keeps_receipt_without_fabricated_empty_results));f.Task.Client.FailLoads=false;await f.Todo.LoadSelectedAsync();cut.WaitForAssertion(()=>Assert.Contains("1 matching nonarchived tasks",cut.Markup));Assert.Single(f.Api.Acceptances);Assert.Equal(task,f.Todo.Snapshot!.Document.Tasks.Single());
    }
    [Theory]
    [InlineData("name")]
    [InlineData("invalid-date")]
    [InlineData("range")]
    [InlineData("relative-conflict")]
    [InlineData("missing-zone")]
    [InlineData("invalid-zone")]
    [InlineData("unavailable-zone")]
    [InlineData("search")]
    [InlineData("stage-kind")]
    [InlineData("stage-state")]
    [InlineData("source-conflict")]
    [InlineData("unknown-choice")]
    public async Task Invalid_review_identifies_fields_preserves_values_and_requests_first_error_focus(string scenario)
    {
        using var f = await Setup(); var cut = Render<TodoSavedListPanel>(); Name(cut, "Retained private selection");
        void Change(string field, string value) => cut.Find("[data-testid='saved-list-"+field+"']").Change(value);
        string[] fields;
        switch (scenario)
        {
            case "name": Name(cut, ""); fields = ["name"]; break;
            case "invalid-date": Change("planned-from", "2026-02-30"); fields = ["planned-from"]; break;
            case "range": Change("planned-from", "2026-11-08"); Change("planned-to", "2026-11-01"); fields = ["planned-from", "planned-to"]; break;
            case "relative-conflict": Change("planned-from", "2026-11-01"); Change("planned-relative", "Today"); Change("timezone", "UTC"); fields = ["planned-relative", "planned-from", "planned-to"]; break;
            case "missing-zone": Change("planned-relative", "Today"); fields = ["timezone"]; break;
            case "invalid-zone": Change("planned-relative", "Today"); Change("timezone", "NotZone"); fields = ["timezone"]; break;
            case "unavailable-zone": Change("planned-relative", "Today"); Change("timezone", "Unavailable/Zone"); fields = ["timezone"]; break;
            case "search": cut.Find("[data-testid='saved-list-search']").Input(new string('x',257)); fields = ["search"]; break;
            case "stage-kind": Change("kind", "Daily"); Change("stage", "Next"); fields = ["kind", "stage"]; break;
            case "stage-state": Change("state", "Paused"); Change("stage", "Next"); fields = ["state", "stage"]; break;
            case "source-conflict": Change("kind", "Daily"); Change("state", "Paused"); Change("source", "UnblockedTodos"); fields = ["source", "kind", "state"]; break;
            default: Change("priority", "Unsupported"); fields = ["priority"]; break;
        }
        var before = JSInterop.Invocations.Count(row => row.Identifier == "Blazor._internal.domWrapper.focus");
        Review(cut);
        Assert.Equal("alert", cut.Find("[data-testid='saved-list-errors']").GetAttribute("role"));
        foreach (var field in fields)
        {
            var control = cut.Find("[data-testid='saved-list-"+field+"']");
            Assert.Equal("true", control.GetAttribute("aria-invalid"));
            var description = control.GetAttribute("aria-describedby"); Assert.False(string.IsNullOrWhiteSpace(description));
            Assert.False(string.IsNullOrWhiteSpace(cut.Find("#"+description).TextContent));
        }
        Assert.True(JSInterop.Invocations.Count(row => row.Identifier == "Blazor._internal.domWrapper.focus") > before);
        Assert.Null(f.Organization.Preview); Assert.Empty(f.Api.Requests); Assert.Empty(f.Api.Acceptances);
        Assert.Empty(f.Todo.Snapshot!.Commands); Assert.Empty(f.Todo.Snapshot.Document.SavedTaskViews ?? []);
        if (scenario != "name") Assert.Equal("Retained private selection",cut.Find("[data-testid='saved-list-name']").GetAttribute("value"));
        if (scenario == "source-conflict")
        {
            Assert.Equal("Daily",cut.Find("[data-testid='saved-list-kind']").GetAttribute("value"));
            Assert.Equal("Paused",cut.Find("[data-testid='saved-list-state']").GetAttribute("value"));
        }
        if (scenario == "range") Save("saved-errors-range", cut.Markup, "paired-range-error-and-retained-values", nameof(Invalid_review_identifies_fields_preserves_values_and_requests_first_error_focus));
        if (scenario == "relative-conflict") Save("saved-errors-relative", cut.Markup, "fixed-relative-conflict-and-associated-fields", nameof(Invalid_review_identifies_fields_preserves_values_and_requests_first_error_focus));
        if (scenario == "source-conflict") Save("saved-errors-source", cut.Markup, "source-type-state-conflict-preserved", nameof(Invalid_review_identifies_fields_preserves_values_and_requests_first_error_focus));
        if (scenario == "missing-zone") Save("saved-errors-zone", cut.Markup, "relative-date-needs-explicit-zone", nameof(Invalid_review_identifies_fields_preserves_values_and_requests_first_error_focus));
    }

    [Fact]
    public async Task Multiple_invalid_constraints_report_together_and_explicit_correction_reviews_the_retained_definition()
    {
        using var f = await Setup(); var cut = Render<TodoSavedListPanel>(); Name(cut, "Correctable private selection");
        cut.Find("[data-testid='saved-list-kind']").Change("Daily"); cut.Find("[data-testid='saved-list-stage']").Change("Next");
        cut.Find("[data-testid='saved-list-planned-from']").Change("2026-11-08"); cut.Find("[data-testid='saved-list-planned-to']").Change("2026-11-01");
        cut.Find("[data-testid='saved-list-deadline-from']").Change("2026-11-02"); cut.Find("[data-testid='saved-list-deadline-to']").Change("2026-11-03");
        Review(cut);
        Assert.Equal(4,cut.FindAll("[aria-invalid='true']").Count);
        Save("saved-errors-multiple",cut.Markup,"independent-stage-and-range-errors",nameof(Multiple_invalid_constraints_report_together_and_explicit_correction_reviews_the_retained_definition));
        cut.Find("[data-testid='saved-list-kind']").Change("Todo");cut.Find("[data-testid='saved-list-planned-to']").Change("2026-11-09");Review(cut);
        Assert.Empty(cut.FindAll("[aria-invalid='true']"));Assert.Empty(cut.FindAll("[data-testid='saved-list-errors']"));
        var query=f.Organization.Preview!.Command.Patch!.Query!;Assert.Equal(CompanionTaskKind.Todo,query.Filter.Kind);Assert.Equal(TodoExecutionStage.Next,query.Filter.ExecutionStage);
        Assert.Equal(new DateOnly(2026,11,8),query.Filter.PlannedFrom);Assert.Equal(new DateOnly(2026,11,9),query.Filter.PlannedTo);
        Assert.Equal("Correctable private selection",f.Organization.Preview.Snapshot.Name);Assert.Empty(f.Api.Acceptances);
        Save("saved-errors-corrected",cut.Markup,"retained-definition-reviewed-after-explicit-correction",nameof(Multiple_invalid_constraints_report_together_and_explicit_correction_reviews_the_retained_definition));
    }

    [Theory]
    [InlineData("Source", TodoSavedListField.Source)]
    [InlineData("Kind", TodoSavedListField.Kind)]
    [InlineData("State", TodoSavedListField.State)]
    [InlineData("Priority", TodoSavedListField.Priority)]
    [InlineData("Stage", TodoSavedListField.Stage)]
    [InlineData("Projection", TodoSavedListField.Projection)]
    [InlineData("PlannedRelative", TodoSavedListField.PlannedRelative)]
    [InlineData("DeadlineRelative", TodoSavedListField.DeadlineRelative)]
    public void Unsupported_choices_return_typed_field_constraints_without_changing_the_draft(string property, TodoSavedListField field)
    {
        var draft=new TodoSavedListDraft { Name="Private retained name", Search="Retained search", TimeZoneId="UTC" };
        typeof(TodoSavedListDraft).GetProperty(property)!.SetValue(draft,"Unsupported");
        var before=System.Text.Json.JsonSerializer.Serialize(draft);var result=draft.Validate(requireName:true);
        Assert.False(result.IsValid);Assert.Null(result.Query);
        var issue=Assert.Single(result.Issues);Assert.Equal(TodoSavedListConstraint.Choice,issue.Constraint);Assert.Equal(new[]{field},issue.Fields);
        Assert.Equal(before,System.Text.Json.JsonSerializer.Serialize(draft));Assert.Throws<InvalidOperationException>(()=>draft.Query());
    }

    [Theory]
    [InlineData("Project", TodoSavedListField.Project)]
    [InlineData("Category", TodoSavedListField.Category)]
    [InlineData("Label", TodoSavedListField.Label)]
    public async Task Empty_reference_constraints_associate_the_picker_and_preserve_the_invalid_selection(string kind, TodoSavedListField field)
    {
        using var f=await Setup();var cut=Render<TodoSavedListPanel>();Name(cut,"Retained reference");
        var picker=cut.FindComponents<TodoPlacementPicker>().Single(component=>component.Instance.Kind==kind);
        await cut.InvokeAsync(()=>picker.Instance.ValueChanged.InvokeAsync(Guid.Empty));Review(cut);
        var control=cut.Find("#saved-list-"+kind.ToLowerInvariant());Assert.Equal("true",control.GetAttribute("aria-invalid"));
        Assert.Equal(Guid.Empty.ToString(),control.GetAttribute("value"));Assert.NotNull(cut.Find("#"+control.GetAttribute("aria-describedby")));
        Assert.Null(f.Organization.Preview);Assert.Empty(f.Api.Requests);
        var draft=new TodoSavedListDraft();typeof(TodoSavedListDraft).GetProperty(kind)!.SetValue(draft,Guid.Empty);
        var issue=Assert.Single(draft.Validate().Issues);Assert.Equal(TodoSavedListConstraint.Reference,issue.Constraint);Assert.Equal(new[]{field},issue.Fields);
    }

    [Fact]
    public void Valid_query_roundtrip_retains_explicit_empty_search_and_domain_backstop()
    {
        var query=new WorkspaceSavedTaskQuery(WorkspaceSavedTaskSource.Tasks,new(Query:"",Kind:CompanionTaskKind.Todo,ExecutionStage:TodoExecutionStage.Next,PlannedRelative:WorkspaceRelativeDatePreset.NextSevenDays),"UTC");
        var draft=TodoSavedListDraft.From("Retained name",query);var result=draft.Validate(requireName:true);
        Assert.True(result.IsValid);Assert.Equal(query,result.Query);Assert.True(result.Query!.IsValid());Assert.Equal("",result.Query.Filter.Query);
        Assert.Null(new TodoSavedListDraft().Query().Filter.Query);
        Assert.True(new TodoSavedListDraft().Validate().IsValid);Assert.False(new TodoSavedListDraft().Validate(requireName:true).IsValid);
    }

}
