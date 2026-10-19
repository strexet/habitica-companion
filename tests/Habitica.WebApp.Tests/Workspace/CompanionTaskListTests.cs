using Bunit;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Components;

namespace Habitica.WebApp.Tests.Workspace;

public sealed class CompanionTaskListTests : BunitContext
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-19T00:00:00Z");
    private static CompanionTask Task(string title, CompanionTaskKind kind = CompanionTaskKind.Todo,
        CompanionTaskState state = CompanionTaskState.Active, CompanionPriority priority = CompanionPriority.Normal) =>
        new(Guid.NewGuid(), 1, title, kind, state, priority, Now, Now);

    [Fact]
    public void Combined_filters_keep_stage_semantics_and_do_not_modify_the_document()
    {
        var next = Task("Résumé work", priority: CompanionPriority.High) with { ExecutionStage = TodoExecutionStage.Next };
        var document = new WorkspaceDocument { Id = Guid.NewGuid(), Tasks = [next,
            Task("Résumé waiting", state: CompanionTaskState.Waiting, priority: CompanionPriority.High) with { ExecutionStage = TodoExecutionStage.Next },
            Task("Other next", priority: CompanionPriority.Low) with { ExecutionStage = TodoExecutionStage.Next },
            Task("Résumé habit", CompanionTaskKind.Habit, priority: CompanionPriority.High)] };
        var before = WorkspaceJson.Serialize(document); CompanionTask? selected = null;
        var cut = Render<CompanionTaskList>(p => p.Add(c => c.Document, document).Add(c => c.OnSelect, task => selected = task));
        cut.Find("[data-testid='canonical-search']").Change("résumé");
        cut.Find("[data-testid='canonical-filter-priority']").Change("High");
        cut.Find("[data-testid='canonical-filter-stage']").Change("Next");
        Assert.Single(cut.FindAll(".companion-task-list button"));
        cut.Find($"[data-testid='canonical-task-{next.Id}']").Click(); Assert.Equal(next, selected);
        cut.Find("[data-testid='canonical-filter-kind']").Change("Habit");
        Assert.True(cut.Find("[data-testid='canonical-filter-stage']").HasAttribute("disabled"));
        Assert.Contains("Résumé habit", cut.Find(".companion-task-list").TextContent);
        cut.Find("[data-testid='canonical-search']").Change("absent");
        Assert.Contains("No tasks match", cut.Markup);
        cut.Find("[data-testid='canonical-clear-filters']").Click();
        Assert.Equal(4, cut.FindAll(".companion-task-list button").Count);
        Assert.Equal(before, WorkspaceJson.Serialize(document));
    }

    [Fact]
    public void Paging_resets_on_revision_filter_and_workspace_changes()
    {
        var document = new WorkspaceDocument { Id = Guid.NewGuid(), Tasks = Enumerable.Range(1, 25).Select(i => Task($"Task {i}")).ToArray() };
        var cut = Render<CompanionTaskList>(p => p.Add(c => c.Document, document));
        Assert.Equal(20, cut.FindAll(".companion-task-list button").Count);
        cut.Find("[data-testid='canonical-next-page']").Click();
        Assert.Equal(5, cut.FindAll(".companion-task-list button").Count);
        Assert.Contains("showing 21–25", cut.Markup);
        cut.Render(p => p.Add(c => c.Document, document with { Revision = 2 }));
        Assert.Contains("showing 1–20", cut.Markup);
        cut.Find("[data-testid='canonical-next-page']").Click();
        cut.Find("[data-testid='canonical-search']").Change("Task 1");
        Assert.Contains("11 matching tasks", cut.Markup);
        var other = new WorkspaceDocument { Id = Guid.NewGuid(), Tasks = [Task("Different workspace")] };
        cut.Render(p => p.Add(c => c.Document, other));
        Assert.Contains("Different workspace", cut.Markup);
        Assert.Equal("", cut.Find("[data-testid='canonical-search']").GetAttribute("value"));
    }

    [Fact]
    public void Saved_lists_show_fixed_dates_and_recompute_dependency_eligibility()
    {
        var prerequisite = Task("Prerequisite");
        var task = Task("Dependent <script>text</script>") with { Deadline = new(2026, 10, 20), DependsOn = [prerequisite.Id], ExecutionStage = TodoExecutionStage.Next };
        var view = new WorkspaceSavedTaskView(Guid.NewGuid(), 1, "Named <script>list</script>", new(WorkspaceSavedTaskSource.UnblockedTodos,
            new(DeadlineFrom: new(2026, 10, 20), DeadlineTo: new(2026, 10, 20), ExecutionStage: TodoExecutionStage.Next)));
        var document = new WorkspaceDocument { Id = Guid.NewGuid(), Tasks = [prerequisite, task], SavedTaskViews = [view] };
        var cut = Render<CompanionTaskList>(p => p.Add(c => c.Document, document));
        cut.Find("[data-testid='canonical-saved-list']").Change(view.Id.ToString());
        Assert.Contains("fixed date): 2026-10-20", cut.Markup);
        Assert.Contains("0 matching tasks", cut.Markup);
        Assert.Empty(cut.FindAll("script"));
        var completed = WorkspaceCommandEngine.Accept(new(document, 1, [], []),
            new(Guid.NewGuid(), document.Id, document.Revision, WorkspaceOperation.CompleteTask, prerequisite.Id), Now);
        Assert.Equal(SubmissionDisposition.Accepted, completed.Disposition);
        cut.Render(p => p.Add(c => c.Document, completed.Snapshot!.Document));
        Assert.Single(cut.FindAll(".companion-task-list button"));
        Assert.Contains(task.Text, cut.Find(".companion-task-list").TextContent);
        WorkspaceLayoutEvidence.Save("canonical-saved-list", cut.Markup);
    }

    [Fact]
    public void Removed_or_invalid_saved_list_is_unavailable_without_all_task_fallback()
    {
        var view = new WorkspaceSavedTaskView(Guid.NewGuid(), 1, "Exact list", new(WorkspaceSavedTaskSource.Tasks, new()));
        var document = new WorkspaceDocument { Id = Guid.NewGuid(), Tasks = [Task("Unrelated task")], SavedTaskViews = [view] };
        var cut = Render<CompanionTaskList>(p => p.Add(c => c.Document, document));
        cut.Find("[data-testid='canonical-saved-list']").Change(view.Id.ToString());
        cut.Render(p => p.Add(c => c.Document, document with { Revision = 2, SavedTaskViews = [view with { IsArchived = true }] }));
        Assert.Contains("list is unavailable", cut.Find("[role='alert']").TextContent);
        Assert.Empty(cut.FindAll(".companion-task-list button"));
        cut.Render(p => p.Add(c => c.Document, document with { Revision = 3, SavedTaskViews = [view with { Query = new((WorkspaceSavedTaskSource)999, new()) }] }));
        Assert.Contains("list is unavailable", cut.Find("[role='alert']").TextContent);
        cut.Render(p => p.Add(c => c.Document, document with { Revision = 4, SavedTaskViews = [view with { Query = new(WorkspaceSavedTaskSource.Tasks, null!) }] }));
        Assert.Contains("list is unavailable", cut.Find("[role='alert']").TextContent);
        cut.Find("[data-testid='canonical-clear-filters']").Click();
        Assert.Single(cut.FindAll(".companion-task-list button"));
    }

    [Fact]
    public void Pending_command_disables_selection_without_losing_the_list()
    {
        var document = new WorkspaceDocument { Id = Guid.NewGuid(), Tasks = [Task(new string('W', 300) + " <untrusted>")] };
        var cut = Render<CompanionTaskList>(p => p.Add(c => c.Document, document));
        cut.Find("[data-testid='canonical-search']").Change("untrusted");
        cut.Render(p => p.Add(c => c.Disabled, true));
        Assert.True(cut.Find("fieldset").HasAttribute("disabled"));
        Assert.True(cut.Find(".companion-task-list button").HasAttribute("disabled"));
        Assert.Contains("1 matching tasks", cut.Markup);
        WorkspaceLayoutEvidence.Save("canonical-filter-disabled", cut.Markup);
    }
}
