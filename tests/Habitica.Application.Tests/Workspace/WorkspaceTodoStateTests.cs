using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceTodoStateTests
{
    private static readonly DateTimeOffset Now=DateTimeOffset.Parse("2026-10-18T19:00:00Z");
    private static WorkspaceJournalSnapshot Initial()
    {
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","organization-history-v1.json")));
        var d=WorkspaceJson.Deserialize(json.RootElement.GetProperty("cases").EnumerateArray().Single(row=>row.GetProperty("name").GetString()=="all entity kinds preserve create and partial update snapshots").GetProperty("document").GetRawText());return new(d,d.Revision,[],[]);
    }
    private static WorkspaceCommand Command(WorkspaceJournalSnapshot state,CompanionTaskState desired)=>new(Guid.NewGuid(),state.Document.Id,state.Document.Revision,WorkspaceOperation.SetTodoState,state.Document.Tasks[0].Id,new(TodoState:new(desired)));
    [Fact]
    public void Independent_v7_hashes_and_unused_defaults_preserve_old_operation_identity()
    {
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","todo-state-command-v7.json")));
        foreach(var row in json.RootElement.GetProperty("cases").EnumerateArray()){var command=WorkspaceTaskApiJson.Deserialize<WorkspaceCommand>(row.GetProperty("command").GetRawText());Assert.Equal(row.GetProperty("payloadHash").GetString(),WorkspaceCommandIdentity.Hash(command));}
        Assert.Equal(13,(int)WorkspaceOperation.SetTodoState);Assert.Equal(12,(int)WorkspaceOperation.RelateTodo);
        Assert.DoesNotContain("todoState",WorkspaceTaskApiJson.Serialize(new WorkspaceTaskPatch(Text:"Legacy")));
        Assert.Throws<JsonException>(()=>WorkspaceTaskApiJson.Deserialize<WorkspaceTodoState>("{}"));
    }
    [Theory]
    [InlineData(CompanionTaskState.Active)][InlineData(CompanionTaskState.Waiting)][InlineData(CompanionTaskState.Someday)][InlineData(CompanionTaskState.Cancelled)]
    public void Complete_native_state_matrix_preserves_metadata_history_mappings_and_order(CompanionTaskState desired)
    {
        foreach(var source in new[]{CompanionTaskState.Active,CompanionTaskState.Waiting,CompanionTaskState.Someday,CompanionTaskState.Cancelled,CompanionTaskState.Paused})
        foreach(var mapping in Enum.GetValues<ProjectionState>())
        {
            var original=Initial();var task=original.Document.Tasks[0] with { State=source };var tasks=original.Document.Tasks.Select(row=>row.Id==task.Id?task:row).ToArray();
            original=original with { Document=original.Document with { Tasks=tasks,Projections=[new(Guid.NewGuid(),original.Document.Revision,task.Id,mapping,mapping==ProjectionState.Unlinked?null:Guid.NewGuid().ToString("D"))] } };
            var command=Command(original,desired);var result=WorkspaceCommandEngine.Accept(original,command,Now);Assert.Equal(SubmissionDisposition.Accepted,result.Disposition);var state=result.Snapshot!;
            Assert.Equal(desired,state.Document.Tasks[0].State);Assert.Equal(task with {State=desired,Revision=original.Document.Revision+1,UpdatedAtUtc=Now},state.Document.Tasks[0]);Assert.Equal(tasks.Select(row=>row.Id),state.Document.Tasks.Select(row=>row.Id));
            Assert.Equal(original.Document.Projections,state.Document.Projections);Assert.Same(original.Document.OrganizationHistory,state.Document.OrganizationHistory);Assert.Equal(original.Document.Completions,state.Document.Completions);Assert.Equal(original.Document.Restorations,state.Document.Restorations);Assert.Empty(result.Entry!.Effects);Assert.Equal(ProjectionCommandState.NotRequested,result.Entry.ProjectionState);
            Assert.True(WorkspaceJournalValidation.IsValid(WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(state))));var duplicate=WorkspaceCommandEngine.Accept(state,command,Now.AddDays(1));Assert.Equal(SubmissionDisposition.Duplicate,duplicate.Disposition);Assert.Equal(result.Entry,duplicate.Entry);
            Assert.Equal(SubmissionDisposition.IdempotencyConflict,WorkspaceCommandEngine.Accept(state,command with {Patch=new(TodoState:new(desired==CompanionTaskState.Waiting?CompanionTaskState.Active:CompanionTaskState.Waiting))},Now).Disposition);
        }
    }
    [Fact]
    public void Invalid_kind_archived_completed_mixed_or_projected_state_edits_do_not_save()
    {
        var s=Initial();var c=Command(s,CompanionTaskState.Waiting);var before=WorkspaceJournalJson.Serialize(s);
        foreach(var command in new[]{c with {Patch=new(TodoState:new(CompanionTaskState.Completed))},c with {Patch=new(TodoState:new(CompanionTaskState.Paused))},c with {Patch=new(TodoState:new((CompanionTaskState)99))},c with {Patch=new(Text:"Mixed",TodoState:new(CompanionTaskState.Waiting))},c with {Operation=WorkspaceOperation.UpdateTask},c with {ProjectToHabitica=true},c with {TaskId=Guid.NewGuid()},c with {TaskId=s.Document.Tasks.First(task=>task.Kind==CompanionTaskKind.Daily).Id},c with {TaskId=s.Document.Tasks.First(task=>task.Kind==CompanionTaskKind.Habit).Id},c with {TaskId=s.Document.Tasks.First(task=>task.State==CompanionTaskState.Completed).Id},c with {TaskId=s.Document.Tasks.First(task=>task.IsArchived).Id}})
            Assert.Equal(SubmissionDisposition.Invalid,WorkspaceCommandEngine.Accept(s,command,Now).Disposition);
        Assert.Equal(SubmissionDisposition.RevisionConflict,WorkspaceCommandEngine.Accept(s,c with {ExpectedRevision=1},Now).Disposition);Assert.Equal(before,WorkspaceJournalJson.Serialize(s));
    }
    [Fact]
    public void Portable_snapshot_or_effect_tampering_cannot_reclassify_native_state_as_completion()
    {
        var s=Initial();s=WorkspaceCommandEngine.Accept(s,Command(s,CompanionTaskState.Waiting),Now).Snapshot!;var entry=s.Commands[0];
        foreach(var invalid in new[]{entry with {TaskSnapshot=entry.TaskSnapshot! with {State=CompanionTaskState.Completed}},entry with {Effects=[ProjectionEffectKind.ScoreUp],ProjectionState=ProjectionCommandState.Pending},entry with {Command=entry.Command with {Patch=new(TodoState:new(CompanionTaskState.Active))}}})Assert.False(WorkspaceJournalValidation.IsValid(s with {Commands=[invalid]}));
    }
    [Fact]
    public void Current_goal_and_dependency_candidates_follow_native_state_without_history_rewriting()
    {
        var s=Initial();var task=s.Document.Tasks[0];var goal=new WorkspaceGoal(Guid.NewGuid(),s.Document.Revision,"Native goal",[task.Id]);s=s with {Document=s.Document with {Goals=s.Document.Goals.Append(goal).ToArray()}};
        Assert.Equal(new GoalProgress(0,1),WorkspaceRules.Progress(goal,s.Document.Tasks));s=WorkspaceCommandEngine.Accept(s,Command(s,CompanionTaskState.Cancelled),Now).Snapshot!;Assert.Equal(new GoalProgress(0,0),WorkspaceRules.Progress(goal,s.Document.Tasks));
        s=WorkspaceCommandEngine.Accept(s,Command(s,CompanionTaskState.Active),Now).Snapshot!;Assert.Equal(new GoalProgress(0,1),WorkspaceRules.Progress(goal,s.Document.Tasks));
        Assert.Equal(Initial().Document.Completions,s.Document.Completions);Assert.Empty(s.Commands.SelectMany(row=>row.Effects));
    }
}
