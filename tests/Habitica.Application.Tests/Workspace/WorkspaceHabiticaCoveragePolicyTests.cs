using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceHabiticaCoveragePolicyTests
{
    private static CompanionTask Task(CompanionTaskKind kind = CompanionTaskKind.Todo) =>
        new(Guid.NewGuid(), 1, "Synthetic task", kind, CompanionTaskState.Active, CompanionPriority.Normal,
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    [Fact]
    public void Every_task_kind_subtask_and_archived_completion_requires_a_counterpart_without_mutation()
    {
        var parent = Task(); var child = Task() with { ParentTaskId = parent.Id };
        var completed = Task() with { State = CompanionTaskState.Completed, IsArchived = true };
        var document = new WorkspaceDocument { Id = Guid.NewGuid(), Tasks = [parent, child, completed, Task(CompanionTaskKind.Daily), Task(CompanionTaskKind.Habit)],
            Completions = [new(Guid.NewGuid(), 1, completed.Id, Guid.NewGuid(), DateTimeOffset.UnixEpoch)] };
        var before = WorkspaceJson.Serialize(document);
        var result = WorkspaceHabiticaCoveragePolicy.Audit(document);
        Assert.Equal(5, result.MissingTaskLinks);
        Assert.Equal(document.Tasks.Select(task => task.Id), result.Tasks.Select(task => task.TaskId));
        Assert.False(result.HasCompleteStoredCoverage);
        Assert.Equal(before, WorkspaceJson.Serialize(document));
    }

    [Fact]
    public void Stored_identity_pending_and_uncertain_outcomes_remain_distinct()
    {
        var tasks = Enumerable.Range(0, 7).Select(_ => Task()).ToArray();
        tasks[1] = tasks[1] with { State = CompanionTaskState.Completed, IsArchived = true };
        var document = new WorkspaceDocument { Id = Guid.NewGuid(), Tasks = tasks, Projections = [
            new(Guid.NewGuid(),1,tasks[0].Id,ProjectionState.Linked,"remote-1"),
            new(Guid.NewGuid(),1,tasks[1].Id,ProjectionState.Linked,"remote-2",IsArchived:true),
            new(Guid.NewGuid(),1,tasks[2].Id,ProjectionState.Pending),
            new(Guid.NewGuid(),1,tasks[3].Id,ProjectionState.UnknownOutcome,"remote-4"),
            new(Guid.NewGuid(),1,tasks[4].Id,ProjectionState.ExternalConflict,"remote-5"),
            new(Guid.NewGuid(),1,tasks[5].Id,ProjectionState.Linked,"remote-6",IsArchived:true)] };
        var result = WorkspaceHabiticaCoveragePolicy.Audit(document);
        Assert.Equal(2, result.StoredTaskLinks);
        Assert.Equal(1, result.MissingTaskLinks);
        Assert.Equal(1, result.PendingTaskLinks);
        Assert.Equal(3, result.TaskLinksNeedingReview);
        Assert.Equal(HabiticaCoverageState.Retained, result.Tasks[1].State);
        Assert.False(result.HasCompleteStoredCoverage);
    }

    [Fact]
    public void All_organization_kinds_require_tags_without_fabricating_game_tasks_or_existing_tag_links()
    {
        var project = new WorkspaceProject(Guid.NewGuid(),1,"Synthetic project");
        var document = new WorkspaceDocument { Id = Guid.NewGuid(), Projects = [project],
            Categories = [new(Guid.NewGuid(),1,"Category")], Sections = [new(Guid.NewGuid(),1,"Section",project.Id)],
            Labels = [new(Guid.NewGuid(),1,"Label")], Goals = [new(Guid.NewGuid(),1,"Goal",[])],
            SavedTaskViews = [new(Guid.NewGuid(),1,"View",new(WorkspaceSavedTaskSource.Tasks,new()),IsArchived:true)] };
        var result = WorkspaceHabiticaCoveragePolicy.Audit(document);
        Assert.Empty(result.Tasks);
        Assert.Equal(6, result.MissingGroupTagLinks);
        Assert.Equal(6, result.RequiredTags.Select(tag => tag.Kind).Distinct().Count());
        Assert.Contains(result.RequiredTags, tag => tag.Archived);
        Assert.False(result.HasCompleteStoredCoverage);
    }
}
