using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceOrganizationModelTests
{
    [Fact]
    public void Native_task_writes_preserve_organization_history_and_goal_progress_uses_current_state()
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","organization-history-v1.json")));
        var document=WorkspaceJson.Deserialize(fixture.RootElement.GetProperty("cases").EnumerateArray().Single(row=>row.GetProperty("name").GetString()=="all entity kinds preserve create and partial update snapshots").GetProperty("document").GetRawText());
        var state=new WorkspaceJournalSnapshot(document,document.Revision,[],[]);var before=WorkspaceTaskApiJson.Serialize(document.OrganizationHistory);var now=DateTimeOffset.Parse("2026-10-18T13:00:00Z");
        var task=document.Tasks[0];var corrupted=state with { Document=document with { OrganizationHistory=document.OrganizationHistory!.Select((entry,index)=>index==0?entry with { PayloadHash=new string('0',64) }:entry).ToArray() } };
        Assert.Equal(SubmissionDisposition.Invalid,WorkspaceCommandEngine.Accept(corrupted,new(Guid.NewGuid(),document.Id,document.Revision,WorkspaceOperation.PlanTask,task.Id,new(Planning:new(PlannedDate:new(2026,10,25)))),now).Disposition);
        state=WorkspaceCommandEngine.Accept(state,new(Guid.NewGuid(),document.Id,document.Revision,WorkspaceOperation.PlanTask,task.Id,new(Planning:new(PlannedDate:new(2026,10,25)))),now).Snapshot!;
        Assert.Equal(before,WorkspaceTaskApiJson.Serialize(state.Document.OrganizationHistory));Assert.True(WorkspaceJournalValidation.IsValid(state));Assert.Empty(state.Commands[0].Effects);
        var originalCommands=WorkspaceTaskApiJson.Serialize(state.Commands);var originalEvents=WorkspaceTaskApiJson.Serialize(state.Events);var originalTasks=state.Document.Tasks;
        var project=state.Document.Projects.First(row=>!row.IsArchived);
        state=WorkspaceOrganizationEngine.Accept(state,new(Guid.NewGuid(),document.Id,state.Document.Revision,WorkspaceOrganizationKind.Project,WorkspaceOrganizationOperation.Update,project.Id,new(Alias:"Native-Project")),now).Snapshot!;
        Assert.Same(originalTasks,state.Document.Tasks);Assert.Equal(originalCommands,WorkspaceTaskApiJson.Serialize(state.Commands));Assert.Equal(originalEvents,WorkspaceTaskApiJson.Serialize(state.Events));Assert.Equal("native-project",state.Document.Projects.Single(row=>row.Id==project.Id).Alias);
        before=WorkspaceTaskApiJson.Serialize(state.Document.OrganizationHistory);
        var collision=new WorkspaceCommand(document.OrganizationHistory![0].Command.Id,document.Id,state.Document.Revision,WorkspaceOperation.UpdateTask,task.Id,new(Text:"Rejected"));Assert.Equal(SubmissionDisposition.IdempotencyConflict,WorkspaceCommandEngine.Accept(state,collision,now).Disposition);
        var goal=state.Document.Goals.Last();Assert.Equal(new GoalProgress(1,1),WorkspaceRules.Progress(goal,state.Document.Tasks));var completion=state.Document.Completions[0];
        state=WorkspaceCommandEngine.Accept(state,new(Guid.NewGuid(),document.Id,state.Document.Revision,WorkspaceOperation.RestoreTodo,completion.TaskId,new(RestoreTodo:new(completion.Id))),now).Snapshot!;
        Assert.Equal(new GoalProgress(0,1),WorkspaceRules.Progress(goal,state.Document.Tasks));Assert.Equal(before,WorkspaceTaskApiJson.Serialize(state.Document.OrganizationHistory));Assert.True(WorkspaceJournalValidation.IsValid(WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(state))));
    }
    [Fact]
    public void Name_only_goal_history_preserves_large_legacy_sets_but_new_replacements_are_bounded()
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","organization-history-v1.json")));
        var document=WorkspaceJson.Deserialize(fixture.RootElement.GetProperty("cases").EnumerateArray().Single(row=>row.GetProperty("name").GetString()=="name-only goal update retains legacy non-Todo references").GetProperty("document").GetRawText());
        var tasks=Enumerable.Range(0,120).Select(_=>document.Tasks[0] with { Id=Guid.NewGuid() }).ToArray();
        var ids=tasks.Select(task=>task.Id).ToArray();var goal=document.Goals.Last();
        document=document with { Tasks=document.Tasks.Concat(tasks).ToArray(),Goals=document.Goals.Select(row=>row.Id==goal.Id?row with { TaskIds=ids }:row).ToArray(),
            OrganizationHistory=document.OrganizationHistory!.Select(entry=>entry.Snapshot.Id==goal.Id?entry with { Snapshot=entry.Snapshot with { TaskIds=ids } }:entry).ToArray() };
        Assert.True(WorkspaceJournalValidation.IsValid(new(document,document.Revision,[],[])));
        var retained=document.OrganizationHistory!.Single(entry=>entry.Snapshot.Id==goal.Id);
        Assert.Null(retained.Command.Patch!.TaskIds);
        Assert.False(WorkspaceOrganizationCommands.IsValid(retained.Command with { Patch=retained.Command.Patch with { TaskIds=ids } }));
    }

}
