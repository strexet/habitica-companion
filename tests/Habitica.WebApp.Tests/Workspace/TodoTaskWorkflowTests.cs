using Bunit;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Components;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Workspace;

public sealed class TodoTaskWorkflowTests:BunitContext
{
    private static readonly DateTimeOffset Now=new(2026,10,1,0,0,0,TimeSpan.Zero);
    private static CompanionTask Task(string text)=>new(Guid.NewGuid(),1,text,CompanionTaskKind.Todo,CompanionTaskState.Active,CompanionPriority.Normal,Now,Now);
    private async Task<TodoOrganizationFixture> Setup(WorkspaceDocument document)
    {
        JSInterop.Mode=JSRuntimeMode.Loose;Services.AddMudServices();var f=new TodoOrganizationFixture();await f.Load(document:document);Services.AddSingleton<IAppSessionController>(f.Task.Session);Services.AddSingleton<IWorkspaceCloudClient>(f.Task.Client);Services.AddSingleton(f.Task.Database);Services.AddSingleton(f.Task.Controller);Services.AddSingleton(f.Organization);Services.AddSingleton(f.Todo);Services.AddSingleton<TodoTaskDraftStore>();Services.AddSingleton(TimeProvider.System);return f;
    }
    private static void Save(string name,string markup,string state,string test)=>Qualification.PageLayoutEvidence.Save(name,markup,"/todo/tasks",state,["form","actions","confirmation","disabled-controls","error","navigation"],test);
    [Fact]
    public async Task Active_stage_and_inactive_state_have_separate_reviews_and_original_receipt_recovery()
    {
        var task=Task("Optional workflow");using var f=await Setup(new(){Id=Guid.NewGuid(),Revision=10,Tasks=[task]});var cut=Render<TodoHierarchyPanel>();cut.Find($"[data-testid='tree-workflow-{task.Id:D}']").Click();cut.Find("[data-testid='workflow-stage']").Change("Next");cut.Find("[data-testid='workflow-review-stage']").Click();Assert.Equal(WorkspaceOperation.SetTodoStage,f.Task.Controller.Preview!.Command.Operation);Assert.False(f.Task.Controller.Preview.MayAffectHabitica);Assert.Contains("active Todo stage",cut.Find("[data-testid='todo-review']").TextContent);cut.Find("[data-testid='todo-confirm']").Click();
        cut.Find("[data-testid='workflow-reload']").Click();cut.Find("[data-testid='workflow-state']").Change("Waiting");cut.Find("[data-testid='workflow-review-state']").Click();Assert.Equal(WorkspaceOperation.SetTodoState,f.Task.Controller.Preview!.Command.Operation);Save("todo-workflow-state-review",cut.Markup,"independent-state-review-with-retained-stage",nameof(Active_stage_and_inactive_state_have_separate_reviews_and_original_receipt_recovery));
        var id=f.Task.Controller.Preview.PreparedCommandId;f.Task.Client.LoseAcceptance=true;cut.Find("[data-testid='todo-confirm']").Click();cut.Find("[data-testid='todo-recover']").Click();Assert.Equal(id,f.Task.Controller.LastReceipt!.CommandId);cut.Find("[data-testid='workflow-reload']").Click();var saved=f.Todo.Snapshot!.Document.Tasks.Single();Assert.Equal(CompanionTaskState.Waiting,saved.State);Assert.Equal(TodoExecutionStage.Next,saved.ExecutionStage);Assert.Empty(f.Todo.Tasks(new(ExecutionStage:TodoExecutionStage.Next)).Tasks);Assert.True(cut.Find("[data-testid='workflow-stage']").HasAttribute("disabled"));Assert.Contains("take precedence",cut.Markup);Assert.All(f.Todo.Snapshot.Commands,entry=>Assert.Empty(entry.Effects));Save("todo-workflow-dormant",cut.Markup,"waiting-dominates-dormant-next-stage",nameof(Active_stage_and_inactive_state_have_separate_reviews_and_original_receipt_recovery));
    }
    [Fact]
    public async Task Full_dependency_review_refuses_incompatible_refs_and_combined_cycles_without_moving_parents()
    {
        var project=new WorkspaceProject(Guid.NewGuid(),1,"Project");var legacy=Task("Legacy Daily") with{Kind=CompanionTaskKind.Daily};var task=Task(new string('W',150)+" Task") with{ProjectId=project.Id,DependsOn=[legacy.Id]};var child=Task("Child") with{ParentTaskId=task.Id};var candidate=Task(new string('N',150)+" Dependency");
        using var f=await Setup(new(){Id=Guid.NewGuid(),Revision=10,Projects=[project],Tasks=[task,child,candidate,Task(candidate.Text),legacy]});var cut=Render<TodoHierarchyPanel>();cut.Find($"[data-testid='tree-workflow-{task.Id:D}']").Click();Assert.Contains(candidate.Id.ToString(),cut.Find($"[data-testid='workflow-dependency-{candidate.Id:D}']").ParentElement!.TextContent);cut.Find($"[data-testid='workflow-dependency-{child.Id:D}']").Change(true);cut.Find("[data-testid='workflow-review-dependencies']").Click();Assert.Null(f.Task.Controller.Preview);Assert.Empty(f.Task.Client.PreparationIds);
        cut.FindAll("button").Single(button=>button.GetAttribute("aria-label")==$"Remove dependency {legacy.Id:D}").Click();cut.Find("[data-testid='workflow-review-dependencies']").Click();Assert.Null(f.Task.Controller.Preview);Assert.Contains("cycles",cut.Markup);Assert.Empty(f.Task.Client.PreparationIds);
        cut.Find($"[data-testid='workflow-dependency-{child.Id:D}']").Change(false);cut.Find($"[data-testid='workflow-dependency-{candidate.Id:D}']").Change(true);cut.Find("[data-testid='workflow-review-dependencies']").Click();var preview=f.Task.Controller.Preview!;Assert.Equal(new[]{candidate.Id},preview.Command.Patch!.Relationships!.DependsOn);Assert.Null(preview.Command.Patch.Relationships.ParentTaskId);Assert.Contains("Replace all dependencies (1)",cut.Find("[data-testid='todo-review']").TextContent);Save("todo-workflow-dependencies",cut.Markup,"long-full-replacement-dependency-review",nameof(Full_dependency_review_refuses_incompatible_refs_and_combined_cycles_without_moving_parents));
        cut.Find("[data-testid='todo-confirm']").Click();var saved=f.Todo.Snapshot!.Document.Tasks.Single(row=>row.Id==task.Id);Assert.Equal(project.Id,saved.ProjectId);Assert.Equal(task.ParentTaskId,saved.ParentTaskId);Assert.Equal(child,f.Todo.Snapshot.Document.Tasks.Single(row=>row.Id==child.Id));Assert.Empty(f.Todo.Snapshot.Commands.Single().Effects);
    }
    [Fact]
    public async Task Native_placement_from_task_rows_reviews_category_project_section_and_label_assignment()
    {
        var category=new WorkspaceCategory(Guid.NewGuid(),1,"Category");var a=new WorkspaceProject(Guid.NewGuid(),1,"A");var b=new WorkspaceProject(Guid.NewGuid(),1,"B",category.Id);var section=new WorkspaceSection(Guid.NewGuid(),1,"Section B",b.Id);var label=new WorkspaceLabel(Guid.NewGuid(),1,"Label");var task=Task("Plan task") with{ProjectId=a.Id,Deadline=new(2026,11,2)};
        using var f=await Setup(new(){Id=Guid.NewGuid(),Revision=10,Categories=[category],Projects=[a,b],Sections=[section],Labels=[label],Tasks=[task]});var cut=Render<TodoHierarchyPanel>();cut.Find($"[data-testid='tree-plan-{task.Id:D}']").Click();cut.Find("[data-testid='plan-date']").Change("2026-11-01");cut.Find("[data-testid='plan-project']").Change(b.Id.ToString());cut.Find("[data-testid='plan-section']").Change(section.Id.ToString());cut.Find("[data-testid='plan-label']").Change(label.Id.ToString());cut.FindAll("button").Single(button=>button.TextContent.Trim()=="Add selected label").Click();cut.Find("[data-testid='plan-review']").Click();var preview=f.Task.Controller.Preview!;Assert.Equal(b.Id,preview.Command.Patch!.Planning!.ProjectId);Assert.Equal(section.Id,preview.Command.Patch.Planning.SectionId);Assert.Equal(new[]{label.Id},preview.Command.Patch.Planning.LabelIds);Assert.False(preview.MayAffectHabitica);
        Save("todo-workflow-placement",cut.Markup,"reviewed-project-section-label-and-day-placement",nameof(Native_placement_from_task_rows_reviews_category_project_section_and_label_assignment));cut.Find("[data-testid='todo-confirm']").Click();var saved=f.Todo.Snapshot!.Document.Tasks.Single();Assert.Equal(task.Deadline,saved.Deadline);Assert.Equal(new DateOnly(2026,11,1),saved.PlannedDate);Assert.Equal(b.Id,saved.ProjectId);Assert.Equal(section.Id,saved.SectionId);Assert.Equal(saved.Id,Assert.Single(f.Todo.Tasks(new(CategoryId:category.Id)).Tasks).Id);Assert.Empty(f.Todo.Snapshot.Commands.Single().Effects);Assert.Contains("Section: Section B",cut.Markup);Save("todo-workflow-placed",cut.Markup,"saved-project-and-section-placement",nameof(Native_placement_from_task_rows_reviews_category_project_section_and_label_assignment));
    }
    [Fact]
    public async Task Stale_workflow_cannot_mutate_and_owner_switch_clears_private_drafts()
    {
        var task=Task("Private workflow");using var f=await Setup(new(){Id=Guid.NewGuid(),Revision=10,Tasks=[task]});var cut=Render<TodoHierarchyPanel>();cut.Find($"[data-testid='tree-workflow-{task.Id:D}']").Click();cut.Find("[data-testid='workflow-stage']").Change("Unsupported");cut.Find("[data-testid='workflow-review-stage']").Click();Assert.Contains("supported active stage",cut.Markup);Assert.Null(f.Task.Controller.Preview);cut.Find("[data-testid='workflow-state']").Change("Someday");f.Task.Client.State=f.Task.Client.State with{Document=f.Task.Client.State.Document with{Revision=11},StoreVersion=11};await f.Todo.LoadSelectedAsync();Assert.True(cut.Find("[data-testid='workflow-editor'] fieldset").HasAttribute("disabled"));cut.Find("[data-testid='workflow-review-state']").Click();Assert.Null(f.Task.Controller.Preview);Assert.Empty(f.Task.Client.PreparationIds);
        f.Task.Session.SetState(f.Task.Session.State with{UserId="00000000-0000-4000-8000-000000000012"});cut.WaitForAssertion(()=>Assert.DoesNotContain("Private workflow",cut.Markup));Assert.Empty(cut.FindAll("[data-testid='workflow-editor']"));Assert.Empty(f.Task.Client.AcceptedIds);
    }
}
