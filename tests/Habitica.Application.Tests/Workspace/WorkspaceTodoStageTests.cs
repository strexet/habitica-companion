using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceTodoStageTests
{
    private static readonly DateTimeOffset Now=DateTimeOffset.Parse("2026-10-18T19:00:00Z");
    private static WorkspaceJournalSnapshot Initial()
    {
        var s=new WorkspaceJournalSnapshot(new WorkspaceDocument {Id=Guid.NewGuid()},1,[],[]);
        return WorkspaceCommandEngine.Accept(s,new(Guid.NewGuid(),s.Document.Id,1,WorkspaceOperation.CreateTodo,Guid.NewGuid(),new(Text:"Native task",Notes:"Retained")),Now).Snapshot!;
    }
    private static WorkspaceCommand Stage(WorkspaceJournalSnapshot s,WorkspaceTodoStage desired)=>new(Guid.NewGuid(),s.Document.Id,s.Document.Revision,WorkspaceOperation.SetTodoStage,s.Document.Tasks[0].Id,new(TodoStage:desired));
    private static WorkspaceJournalSnapshot Apply(WorkspaceJournalSnapshot s,WorkspaceCommand c){var result=WorkspaceCommandEngine.Accept(s,c,Now);Assert.Equal(SubmissionDisposition.Accepted,result.Disposition);Assert.True(WorkspaceJournalValidation.IsValid(result.Snapshot!));Assert.Empty(result.Entry!.Effects);return result.Snapshot!;}
    [Fact]
    public void Independent_stage_v8_hashes_and_optional_defaults_retain_legacy_encodings()
    {
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","todo-stage-command-v8.json")));foreach(var row in json.RootElement.GetProperty("cases").EnumerateArray()){var c=WorkspaceTaskApiJson.Deserialize<WorkspaceCommand>(row.GetProperty("command").GetRawText());Assert.Equal(row.GetProperty("payloadHash").GetString(),WorkspaceCommandIdentity.Hash(c));}
        Assert.Equal(14,(int)WorkspaceOperation.SetTodoStage);Assert.Equal(13,(int)WorkspaceOperation.SetTodoState);Assert.DoesNotContain("executionStage",WorkspaceJson.Serialize(Initial().Document));Assert.DoesNotContain("todoStage",WorkspaceTaskApiJson.Serialize(new WorkspaceTaskPatch(Text:"Legacy")));
    }
    [Theory]
    [InlineData(TodoExecutionStage.Inbox)][InlineData(TodoExecutionStage.Next)][InlineData(TodoExecutionStage.InProgress)]
    public void Native_stage_and_explicit_clear_preserve_current_state_and_every_mapping(TodoExecutionStage desired)
    {
        foreach(var mapping in Enum.GetValues<ProjectionState>())
        {
            var s=Initial();s=s with {Document=s.Document with {Projections=[new(Guid.NewGuid(),s.Document.Revision,s.Document.Tasks[0].Id,mapping,mapping==ProjectionState.Unlinked?null:Guid.NewGuid().ToString("D"))]}};var original=s;var c=Stage(s,new(desired));s=Apply(s,c);var task=s.Document.Tasks[0];Assert.Equal(original.Document.Tasks[0] with {ExecutionStage=desired,Revision=s.Document.Revision,UpdatedAtUtc=Now},task);Assert.Equal(original.Document.Projections,s.Document.Projections);Assert.Equal(original.Document.Completions,s.Document.Completions);
            var duplicate=WorkspaceCommandEngine.Accept(s,c,Now);Assert.Equal(SubmissionDisposition.Duplicate,duplicate.Disposition);Assert.Equal(SubmissionDisposition.IdempotencyConflict,WorkspaceCommandEngine.Accept(s,c with {Patch=new(TodoStage:new(ClearStage:true))},Now).Disposition);
            s=Apply(s,Stage(s,new(ClearStage:true)));Assert.Null(s.Document.Tasks[0].ExecutionStage);Assert.DoesNotContain("executionStage",WorkspaceJson.Serialize(s.Document));Assert.Equal(desired,s.Commands[^2].TaskSnapshot!.ExecutionStage);
            Assert.Equal(WorkspaceJournalJson.Serialize(s),WorkspaceJournalJson.Serialize(WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(s))));
        }
    }
    [Fact]
    public void Dormant_preference_survives_state_completion_and_exact_restoration_without_reopening_by_stage()
    {
        var s=Initial();s=Apply(s,Stage(s,new(TodoExecutionStage.InProgress)));var taskId=s.Document.Tasks[0].Id;
        foreach(var desired in new[]{CompanionTaskState.Waiting,CompanionTaskState.Someday,CompanionTaskState.Cancelled})
        {
            s=Apply(s,new(Guid.NewGuid(),s.Document.Id,s.Document.Revision,WorkspaceOperation.SetTodoState,taskId,new(TodoState:new(desired))));Assert.Equal(TodoExecutionStage.InProgress,s.Document.Tasks[0].ExecutionStage);Assert.Equal("active_todo_required",WorkspaceCommandEngine.Accept(s,Stage(s,new(TodoExecutionStage.Next)),Now).ErrorCode);
            s=Apply(s,new(Guid.NewGuid(),s.Document.Id,s.Document.Revision,WorkspaceOperation.SetTodoState,taskId,new(TodoState:new(CompanionTaskState.Active))));
        }
        s=Apply(s,new(Guid.NewGuid(),s.Document.Id,s.Document.Revision,WorkspaceOperation.CompleteTask,taskId));Assert.Equal(TodoExecutionStage.InProgress,s.Document.Tasks[0].ExecutionStage);Assert.Equal("active_todo_required",WorkspaceCommandEngine.Accept(s,Stage(s,new(ClearStage:true)),Now).ErrorCode);var completion=Assert.Single(s.Document.Completions);
        s=Apply(s,new(Guid.NewGuid(),s.Document.Id,s.Document.Revision,WorkspaceOperation.RestoreTodo,taskId,new(RestoreTodo:new(completion.Id))));Assert.Equal(TodoExecutionStage.InProgress,s.Document.Tasks[0].ExecutionStage);Assert.Single(s.Document.Completions);Assert.Single(s.Document.Restorations!);
        s=Apply(s,new(Guid.NewGuid(),s.Document.Id,s.Document.Revision,WorkspaceOperation.DeleteTask,taskId));Assert.True(s.Document.Tasks[0].IsArchived);Assert.Equal(TodoExecutionStage.InProgress,s.Document.Tasks[0].ExecutionStage);Assert.Equal(SubmissionDisposition.Invalid,WorkspaceCommandEngine.Accept(s,Stage(s,new(ClearStage:true)),Now).Disposition);
    }
    [Fact]
    public void Wrong_kind_invalid_stage_mixed_or_projected_intent_and_portable_tampering_fail()
    {
        var s=Initial();var c=Stage(s,new(TodoExecutionStage.Next));foreach(var invalid in new[]{c with {Patch=new(TodoStage:new())},c with {Patch=new(TodoStage:new((TodoExecutionStage)99))},c with {Patch=new(TodoStage:new(TodoExecutionStage.Inbox,true))},c with {Patch=new(Text:"Mixed",TodoStage:new(TodoExecutionStage.Next))},c with {Operation=WorkspaceOperation.UpdateTask},c with {ProjectToHabitica=true},c with {TaskId=Guid.NewGuid()}})Assert.Equal(SubmissionDisposition.Invalid,WorkspaceCommandEngine.Accept(s,invalid,Now).Disposition);
        var daily=s.Document.Tasks[0] with {Kind=CompanionTaskKind.Daily,ExecutionStage=TodoExecutionStage.Next};Assert.NotEmpty(WorkspaceRules.Validate(s.Document with {Tasks=[daily]}));
        Assert.Equal(SubmissionDisposition.RevisionConflict,WorkspaceCommandEngine.Accept(s,c with {ExpectedRevision=1},Now).Disposition);s=Apply(s,c);var entry=s.Commands[^1];Assert.False(WorkspaceJournalValidation.IsValid(s with {Commands=s.Commands.SkipLast(1).Append(entry with {TaskSnapshot=entry.TaskSnapshot! with {ExecutionStage=TodoExecutionStage.InProgress}}).ToArray()}));Assert.False(WorkspaceJournalValidation.IsValid(s with {Commands=s.Commands.SkipLast(1).Append(entry with {Effects=[ProjectionEffectKind.ScoreUp],ProjectionState=ProjectionCommandState.Pending}).ToArray()}));
    }
}
