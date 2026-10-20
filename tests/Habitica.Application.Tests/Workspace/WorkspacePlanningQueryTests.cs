using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspacePlanningQueryTests
{
    [Fact]
    public void Membership_is_independent_of_execution_stage_and_deadlines_and_reads_preserve_the_document()
    {
        var day = new DateOnly(2026, 10, 20); var now = DateTimeOffset.UtcNow;
        CompanionTask Task(string text, DateOnly? plan = null, DateOnly? due = null, Guid? project = null) => new(Guid.NewGuid(), 1, text, CompanionTaskKind.Todo, CompanionTaskState.Active, CompanionPriority.Normal, now, now, ProjectId: project, PlannedDate: plan, Deadline: due);
        var inbox = Task("Inbox with deadline", due: day.AddDays(-1)) with { ExecutionStage = TodoExecutionStage.InProgress };
        var owner = new WorkspaceProject(Guid.NewGuid(), 1, "Project");
        var project = Task("Unscheduled project", project: owner.Id);
        var planned = Task("Planned and deadline overdue", day.AddDays(-1), day.AddDays(-2));
        var future = Task("Future", day.AddDays(1), day);
        var completed = inbox with { Id = Guid.NewGuid(), State = CompanionTaskState.Completed };
        var excluded = new[] { inbox with { Id = Guid.NewGuid(), IsArchived = true }, completed, inbox with { Id = Guid.NewGuid(), State = CompanionTaskState.Waiting }, inbox with { Id = Guid.NewGuid(), Kind = CompanionTaskKind.Daily, ExecutionStage = null } };
        var doc = new WorkspaceDocument { Id = Guid.NewGuid(), Revision = 8, Tasks = new[] { inbox, project, planned, future }.Concat(excluded).ToArray(), Projects = [owner], Completions = [new(Guid.NewGuid(), 1, completed.Id, Guid.NewGuid(), now)] };
        Assert.Empty(WorkspaceRules.Validate(doc));
        var before = WorkspaceJson.Serialize(doc);
        Assert.Equal(new[] { inbox.Id }, WorkspacePlanningQuery.Evaluate(doc, WorkspacePlanningList.Inbox, day).Tasks.Select(task => task.Id));
        Assert.Equal(new[] { inbox.Id, project.Id }, WorkspacePlanningQuery.Evaluate(doc, WorkspacePlanningList.Anytime, day).Tasks.Select(task => task.Id));
        Assert.Equal(new[] { inbox.Id, planned.Id }, WorkspacePlanningQuery.Evaluate(doc, WorkspacePlanningList.Overdue, day).Tasks.Select(task => task.Id));
        Assert.Equal(before, WorkspaceJson.Serialize(doc));
    }

    [Fact]
    public void Eligibility_precedes_paging_and_stale_or_unbounded_reads_fail()
    {
        var now = DateTimeOffset.UtcNow; var day = new DateOnly(2026, 10, 20);
        var tasks = Enumerable.Range(0, 45).Select(i => new CompanionTask(Guid.NewGuid(), 1, "Task " + i, CompanionTaskKind.Todo, CompanionTaskState.Active, CompanionPriority.Normal, now, now, PlannedDate: i % 2 == 0 ? null : day)).ToArray();
        var doc = new WorkspaceDocument { Id = Guid.NewGuid(), Revision = 8, Tasks = tasks };
        var first = WorkspacePlanningQuery.Evaluate(doc, WorkspacePlanningList.Inbox, day);
        Assert.Equal(23, first.MatchedCount); Assert.Equal(20, first.NextOffset);
        var last = WorkspacePlanningQuery.Evaluate(doc, WorkspacePlanningList.Inbox, day, offset: 20, expectedRevision: 8);
        Assert.Equal(tasks.Where(task => task.PlannedDate is null).Skip(20), last.Tasks); Assert.Null(last.NextOffset);
        Assert.Throws<InvalidOperationException>(() => WorkspacePlanningQuery.Evaluate(doc, WorkspacePlanningList.Inbox, day, offset: 20, expectedRevision: 7));
        Assert.Throws<ArgumentException>(() => WorkspacePlanningQuery.Evaluate(doc, WorkspacePlanningList.Inbox, day, offset: 20));
        Assert.Throws<ArgumentException>(() => WorkspacePlanningQuery.Evaluate(doc, WorkspacePlanningList.Inbox, default));
        Assert.Throws<ArgumentException>(() => WorkspacePlanningQuery.Evaluate(doc, (WorkspacePlanningList)99, day));
        Assert.Throws<ArgumentException>(() => WorkspacePlanningQuery.Evaluate(doc, WorkspacePlanningList.Inbox, day, limit: 101));
    }
}
