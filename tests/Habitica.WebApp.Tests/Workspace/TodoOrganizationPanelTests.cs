using Bunit;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Components;
using Habitica.WebApp.Pages;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Workspace;

public sealed class TodoOrganizationPanelTests:BunitContext
{
    private static readonly DateTimeOffset Now=new(2026,10,1,0,0,0,TimeSpan.Zero);
    private static CompanionTask Task(string text)=>new(Guid.NewGuid(),1,text,CompanionTaskKind.Todo,CompanionTaskState.Active,CompanionPriority.Normal,Now,Now);
    private async Task<TodoOrganizationFixture> Setup(WorkspaceDocument? document=null)
    {
        JSInterop.Mode=JSRuntimeMode.Loose;Services.AddMudServices();var f=new TodoOrganizationFixture();await f.Load(document:document);
        Services.AddSingleton<IAppSessionController>(f.Task.Session);Services.AddSingleton<IWorkspaceCloudClient>(f.Task.Client);Services.AddSingleton(f.Task.Database);Services.AddSingleton(f.Task.Controller);Services.AddSingleton(f.Organization);Services.AddSingleton(f.Todo);Services.AddSingleton<TodoTaskDraftStore>();Services.AddSingleton(TimeProvider.System);return f;
    }
    private static void Save(string name,string markup,string state,string test)=>Qualification.PageLayoutEvidence.Save(name,markup,"/todo/projects",state,["form","actions","confirmation","disabled-controls","error","navigation"],test);
    private static void Kind(IRenderedComponent<TodoOrganizationPanel> cut,string kind)=>cut.Find("[data-testid='organization-kind']").Change(kind);
    private static void Review(IRenderedComponent<TodoOrganizationPanel> cut,string name){cut.Find("[data-testid='organization-name']").Input(name);cut.Find("form").Submit();}
    [Fact]
    public async Task Optional_native_records_and_Todo_goal_progress_use_reviewed_commands_without_game_effects()
    {
        var active=Task("Active Todo");var completed=Task("Completed Todo") with{State=CompanionTaskState.Completed};var cancelled=Task("Cancelled Todo") with{State=CompanionTaskState.Cancelled};var daily=Task("Daily") with{Kind=CompanionTaskKind.Daily};
        using var f=await Setup(new(){Id=Guid.NewGuid(),Revision=10,Tasks=[active,completed,cancelled,daily],Completions=[new(Guid.NewGuid(),2,completed.Id,Guid.NewGuid(),Now.AddDays(1))]});var cut=Render<TodoOrganizationPanel>();
        Kind(cut,"Category");Review(cut,"Personal");Assert.False(f.Organization.Preview!.MayAffectHabitica);cut.Find("[data-testid='organization-confirm']").Click();var category=f.Organization.LastReceipt!.EntityId;
        Kind(cut,"Project");cut.Find("[data-testid='plan-category']").Change(category.ToString());Review(cut,"Project");cut.Find("[data-testid='organization-confirm']").Click();var project=f.Organization.LastReceipt!.EntityId;Assert.Equal(category,f.Todo.Snapshot!.Document.Projects.Single().CategoryId);
        Kind(cut,"Section");cut.Find("[data-testid='plan-project']").Change(project.ToString());cut.Find("[data-testid='organization-position']").Change("3");Review(cut,"This week");cut.Find("[data-testid='organization-confirm']").Click();Assert.Equal(project,f.Todo.Snapshot.Document.Sections.Single().ProjectId);Assert.Equal(3,f.Todo.Snapshot.Document.Sections.Single().Position);var sectionId=f.Organization.LastReceipt!.EntityId;cut.Find($"[data-testid='organization-edit-{sectionId:D}']").Click();Assert.True(cut.Find("[data-testid='plan-project']").HasAttribute("disabled"));cut.Find("[data-testid='organization-position']").Change("4");
        Save("todo-organization-section",cut.Markup,"immutable-project-section-order-editor",nameof(Optional_native_records_and_Todo_goal_progress_use_reviewed_commands_without_game_effects));cut.Find("form").Submit();Assert.Null(f.Organization.Preview!.Command.Patch!.ProjectId);Assert.Equal(4,f.Organization.Preview.Command.Patch.Position);cut.Find("[data-testid='organization-confirm']").Click();Assert.Equal(4,f.Todo.Snapshot.Document.Sections.Single().Position);
        Kind(cut,"Label");Review(cut,"Home");cut.Find("[data-testid='organization-confirm']").Click();
        Kind(cut,"Goal");foreach(var task in new[]{active,completed,cancelled})cut.Find($"[data-testid='goal-member-{task.Id:D}']").Change(true);Assert.Empty(cut.FindAll($"[data-testid='goal-member-{daily.Id:D}']"));Review(cut,"Useful goal");Assert.Equal(3,f.Organization.Preview!.Command.Patch!.TaskIds!.Count);cut.Find("[data-testid='organization-confirm']").Click();var goal=f.Organization.LastReceipt!.EntityId;Assert.Contains("1 of 2 eligible Todos completed",cut.Markup);
        Save("todo-organization-goal",cut.Markup,"goal-membership-and-cancelled-progress",nameof(Optional_native_records_and_Todo_goal_progress_use_reviewed_commands_without_game_effects));
        await f.Todo.PrepareAsync(new(WorkspaceOperation.CompleteTask,active.Id,active.Text,"",active.Priority,null,null,false,HabitScoreDirection.Up,1,null));await f.Todo.ConfirmAsync();cut.WaitForAssertion(()=>Assert.Contains("2 of 2 eligible Todos completed",cut.Markup));
        cut.Find($"[data-testid='organization-archive-{goal:D}']").Click();Assert.Equal(WorkspaceOrganizationOperation.Archive,f.Organization.Preview!.Command.Operation);cut.Find("[data-testid='organization-confirm']").Click();Assert.True(f.Todo.Snapshot!.Document.Goals.Single().IsArchived);Assert.Equal(4,f.Todo.Snapshot.Document.Tasks.Count);Assert.Equal(7,f.Todo.Snapshot.Document.OrganizationHistory!.Count);Assert.Equal(new[]{ProjectionEffectKind.Create,ProjectionEffectKind.ScoreUp},Assert.Single(f.Todo.Snapshot.Commands).Effects);
    }
    [Fact]
    public async Task Rename_preserves_omitted_archived_references_and_recovers_original_preparation_and_acceptance()
    {
        var category=new WorkspaceCategory(Guid.NewGuid(),1,"Archived category",true);var project=new WorkspaceProject(Guid.NewGuid(),1,new string('W',180)+" Project",category.Id);
        using var f=await Setup(new(){Id=Guid.NewGuid(),Revision=10,Categories=[category],Projects=[project]});var cut=Render<TodoOrganizationPanel>();cut.Find($"[data-testid='organization-edit-{project.Id:D}']").Click();cut.Find("form").Submit();Assert.Null(f.Organization.Preview);Assert.Contains("Change a field",cut.Markup);Assert.Empty(f.Api.Requests);
        f.Api.LosePreparation=true;Review(cut,new string('N',180)+" Renamed");Assert.True(f.Organization.HasPendingIntent);Assert.Null(f.Organization.Preview);cut.Find("[data-testid='organization-retry']").Click();var preview=f.Organization.Preview!;Assert.Null(preview.Command.Patch!.CategoryId);Assert.False(preview.Command.Patch.ClearCategory);Assert.Equal(category.Id,preview.Snapshot.CategoryId);
        Save("todo-organization-review",cut.Markup,"long-native-project-review-and-task-fence",nameof(Rename_preserves_omitted_archived_references_and_recovers_original_preparation_and_acceptance));
        f.Api.LoseAcceptance=true;cut.Find("[data-testid='organization-confirm']").Click();Assert.True(f.Organization.AcceptanceUnknown);Assert.True(f.Todo.IsReadOnly);Save("todo-organization-unknown",cut.Markup,"unknown-native-original-receipt",nameof(Rename_preserves_omitted_archived_references_and_recovers_original_preparation_and_acceptance));cut.Find("[data-testid='organization-recover']").Click();Assert.Equal(preview.PreparedCommandId,f.Organization.LastReceipt!.CommandId);Assert.Single(f.Api.Acceptances);Assert.Equal(category.Id,f.Todo.Snapshot!.Document.Projects.Single().CategoryId);Assert.False(f.Todo.IsReadOnly);
    }
    [Fact]
    public async Task Logical_project_removal_requires_every_reference_page_and_preserves_all_tasks_and_links()
    {
        var project=new WorkspaceProject(Guid.NewGuid(),1,"Project");var tasks=Enumerable.Range(0,25).Select(i=>Task(new string('W',100)+" task "+i) with{ProjectId=project.Id}).ToArray();tasks[1]=tasks[1] with{ParentTaskId=tasks[0].Id};tasks[2]=tasks[2] with{IsArchived=true};
        var mapping=new HabiticaProjection(Guid.NewGuid(),1,tasks[3].Id,ProjectionState.UnknownOutcome,"00000000-0000-4000-8000-000000000099");using var f=await Setup(new(){Id=Guid.NewGuid(),Revision=10,Projects=[project],Tasks=tasks,Projections=[mapping]});var cut=Render<TodoOrganizationPanel>();
        cut.Find($"[data-testid='organization-remove-{project.Id:D}']").Click();Assert.Equal(25,f.Organization.Preview!.Command.Patch!.Removal!.TaskIds.Count);Assert.True(cut.Find("[data-testid='organization-confirm']").HasAttribute("disabled"));Assert.Equal(20,cut.FindAll("[data-testid='organization-review'] li").Count);
        cut.Find("[data-testid='organization-reference-ack']").Change(true);Assert.True(cut.Find("[data-testid='organization-confirm']").HasAttribute("disabled"));cut.FindAll("button").Single(button=>button.TextContent.Trim()=="Next references").Click();Assert.Equal(5,cut.FindAll("[data-testid='organization-review'] li").Count);cut.Find("[data-testid='organization-reference-ack']").Change(true);Assert.False(cut.Find("[data-testid='organization-confirm']").HasAttribute("disabled"));
        Save("todo-organization-removal",cut.Markup,"reviewed-complete-paged-project-removal",nameof(Logical_project_removal_requires_every_reference_page_and_preserves_all_tasks_and_links));cut.Find("[data-testid='organization-confirm']").Click();Assert.True(f.Todo.Snapshot!.Document.Projects.Single().IsRemoved);Assert.Equal(tasks,f.Todo.Snapshot.Document.Tasks);Assert.Equal(mapping,f.Todo.Snapshot.Document.Projections.Single());Assert.Empty(f.Todo.Snapshot.Commands);Assert.Single(f.Todo.Snapshot.Document.OrganizationHistory!);
    }
    [Fact]
    public async Task Empty_goals_are_unavailable_and_stale_metadata_drafts_require_explicit_reload()
    {
        var project=new WorkspaceProject(Guid.NewGuid(),1,"Original");var goal=new WorkspaceGoal(Guid.NewGuid(),1,"Empty goal",[]);using var f=await Setup(new(){Id=Guid.NewGuid(),Revision=10,Projects=[project],Goals=[goal]});var cut=Render<TodoOrganizationPanel>();Kind(cut,"Goal");Assert.Contains("Progress unavailable",cut.Markup);Assert.DoesNotContain("100%",cut.Markup);
        Kind(cut,"Project");cut.Find($"[data-testid='organization-edit-{project.Id:D}']").Click();cut.Find("[data-testid='organization-name']").Input("Stale replacement");f.Task.Client.State=f.Task.Client.State with{Document=f.Task.Client.State.Document with{Revision=11,Projects=[project with{Name="External edit",Revision=11}]},StoreVersion=11};await f.Todo.LoadSelectedAsync();cut.Find("form").Submit();Assert.Null(f.Organization.Preview);Assert.Contains("Reload",cut.Markup);Assert.Empty(f.Api.Requests);
        cut.FindAll("button").Single(button=>button.TextContent.Trim()=="Reload editor context").Click();Assert.Equal("External edit",cut.Find("[data-testid='organization-name']").GetAttribute("value"));Review(cut,"Reviewed replacement");Assert.Equal(11,f.Organization.Preview!.Command.ExpectedRevision);
    }
    [Fact]
    public async Task Duplicate_category_and_goal_candidates_disclose_exact_references_before_selection()
    {
        var a=new WorkspaceCategory(Guid.NewGuid(),1,"Same category");var b=new WorkspaceCategory(Guid.NewGuid(),1,"Same category");var first=Task("Same Todo");var second=Task("Same Todo");using var f=await Setup(new(){Id=Guid.NewGuid(),Revision=10,Categories=[a,b],Tasks=[first,second]});var cut=Render<TodoOrganizationPanel>();Assert.Contains(a.Id.ToString(),cut.Find($"option[value='{a.Id:D}']").TextContent);Assert.Contains(b.Id.ToString(),cut.Find($"option[value='{b.Id:D}']").TextContent);
        Kind(cut,"Goal");Assert.Contains(first.Id.ToString(),cut.Find($"[data-testid='goal-member-{first.Id:D}']").ParentElement!.TextContent);Assert.Contains(second.Id.ToString(),cut.Find($"[data-testid='goal-member-{second.Id:D}']").ParentElement!.TextContent);Assert.Empty(f.Api.Requests);
    }

    [Fact]
    public async Task Switching_sources_with_equal_revisions_resets_the_metadata_page()
    {
        var projects=Enumerable.Range(0,25).Select(i=>new WorkspaceProject(Guid.NewGuid(),1,"Cloud project "+i)).ToArray();using var f=await Setup(new(){Id=Guid.NewGuid(),Revision=1,Projects=projects});var cut=Render<TodoOrganizationPanel>();cut.FindAll("button").Single(button=>button.TextContent.Trim()=="Next records").Click();Assert.Equal(5,cut.FindAll("article").Count);
        await f.Todo.CreateBrowserAsync();Assert.Equal(1,f.Todo.Revision);Review(cut,"Browser project");cut.Find("[data-testid='organization-confirm']").Click();cut.WaitForAssertion(()=>Assert.Equal("Browser project",cut.Find("article strong").TextContent));Assert.True(cut.FindAll("button").Single(button=>button.TextContent.Trim()=="Previous records").HasAttribute("disabled"));
    }
    [Fact]
    public async Task Projects_page_composes_optional_management_and_independent_branch_selection()
    {
        var project=new WorkspaceProject(Guid.NewGuid(),1,"Project");var task=Task("Unassigned task");using var f=await Setup(new(){Id=Guid.NewGuid(),Revision=10,Projects=[project],Tasks=[task]});var cut=Render<TodoProjectsPage>();Assert.Single(cut.FindAll("h1"));Assert.NotNull(cut.Find("[data-testid='hierarchy-panel']"));Assert.NotNull(cut.Find("[data-testid='todo-direct-panel']"));Assert.Contains("Organization is optional",cut.Markup);
        Save("todo-organization-page",cut.Markup,"composed-project-management-and-branch-page",nameof(Projects_page_composes_optional_management_and_independent_branch_selection));Assert.Empty(f.Api.Acceptances);Assert.Empty(f.Task.Client.AcceptedIds);
    }
}
