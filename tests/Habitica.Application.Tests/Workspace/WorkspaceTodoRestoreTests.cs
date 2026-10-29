using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceTodoRestoreTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-18T12:00:00Z");
    private static WorkspaceJournalSnapshot Seed()
    {
        var empty = new WorkspaceJournalSnapshot(new WorkspaceDocument { Id = Guid.NewGuid() }, 1, [], []);
        var task = Guid.NewGuid();
        var created = WorkspaceCommandEngine.Accept(empty, new(Guid.NewGuid(), empty.Document.Id, 1, WorkspaceOperation.CreateTodo, task, new(Text: "Retained work", Notes: "Original notes", Deadline: new(2026,10,21))), Now).Snapshot!;
        return WorkspaceCommandEngine.Accept(created, new(Guid.NewGuid(), created.Document.Id, 2, WorkspaceOperation.CompleteTask, task), Now).Snapshot!;
    }
    private static WorkspaceCommand Restore(WorkspaceJournalSnapshot state, Guid? completion = null) => new(Guid.NewGuid(),state.Document.Id,state.Document.Revision,WorkspaceOperation.RestoreTodo,state.Document.Tasks[0].Id,new(RestoreTodo:new(completion ?? state.Document.Completions.Last().Id)));

    [Fact]
    public void Portable_v5_identity_has_independent_fixtures_and_legacy_wire_shape_remains_unchanged()
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","todo-restore-command-v5.json")));
        foreach(var value in fixture.RootElement.GetProperty("cases").EnumerateArray())Assert.Equal(value.GetProperty("payloadHash").GetString(),WorkspaceCommandIdentity.Hash(WorkspaceTaskApiJson.Deserialize<WorkspaceCommand>(value.GetProperty("command").GetRawText())));
        Assert.Equal(11,(int)WorkspaceOperation.RestoreTodo);Assert.Equal(10,(int)WorkspaceOperation.PlanTask);
        Assert.False(WorkspaceTaskApiJson.Element(new WorkspaceTaskPatch(Text:"Legacy")).TryGetProperty("restoreTodo",out _));
    }
    [Fact]
    public void Repeated_cycles_retain_all_events_and_recover_original_receipts()
    {
        var state=Seed();var original=state.Document.Tasks[0];var first=Restore(state);var result=WorkspaceCommandEngine.Accept(state,first,Now);Assert.Equal(SubmissionDisposition.Accepted,result.Disposition);state=result.Snapshot!;
        Assert.Equal(original with { Revision=state.Document.Revision,State=CompanionTaskState.Active },state.Document.Tasks[0]);Assert.Single(state.Document.Completions);Assert.Single(state.Document.Restorations!);Assert.Empty(result.Entry!.Effects);
        state=WorkspaceCommandEngine.Accept(state,new(Guid.NewGuid(),state.Document.Id,state.Document.Revision,WorkspaceOperation.CompleteTask,original.Id),Now).Snapshot!;
        Assert.Equal("completion_already_restored",WorkspaceCommandEngine.Accept(state,Restore(state,first.Patch!.RestoreTodo!.CompletionId),Now).ErrorCode);
        state=WorkspaceCommandEngine.Accept(state,Restore(state),Now).Snapshot!;Assert.Equal(2,state.Document.Completions.Count);Assert.Equal(2,state.Document.Restorations!.Count);
        var duplicate=WorkspaceCommandEngine.Accept(state,first,Now);Assert.Equal(SubmissionDisposition.Duplicate,duplicate.Disposition);Assert.Equal(result.Entry,duplicate.Entry);
        Assert.Equal(SubmissionDisposition.IdempotencyConflict,WorkspaceCommandEngine.Accept(state,first with { Patch=new(RestoreTodo:new(state.Document.Completions.Last().Id)) },Now).Disposition);
        Assert.True(WorkspaceJournalValidation.IsValid(WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(state))));
        Assert.All(WorkspaceCompletionHistory.Evaluate(state,new()).Items,row=>Assert.NotNull(row.Restoration));
    }
    [Fact]
    public void Exact_completion_state_patch_and_revision_are_required()
    {
        var state=Seed();var command=Restore(state);
        foreach(var bad in new[]{command with { Patch=null },command with { Patch=new(RestoreTodo:new(Guid.Empty)) },command with { Patch=command.Patch! with { Text="Unexpected" } },command with { Operation=WorkspaceOperation.UpdateTask },command with { Count=2 },command with { Direction=HabitScoreDirection.Up },command with { OccurrenceDate=new(2026,10,18) }})Assert.Equal(SubmissionDisposition.Invalid,WorkspaceCommandEngine.Accept(state,bad,Now).Disposition);
        Assert.Equal("completion_unavailable",WorkspaceCommandEngine.Accept(state,Restore(state,Guid.NewGuid()),Now).ErrorCode);
        Assert.Equal(SubmissionDisposition.RevisionConflict,WorkspaceCommandEngine.Accept(state,command with { ExpectedRevision=1 },Now).Disposition);
        Assert.Equal("unsupported_projection",WorkspaceCommandEngine.Accept(state,command with { ProjectToHabitica=true },Now).ErrorCode);
        var restored=WorkspaceCommandEngine.Accept(state,command,Now).Snapshot!;Assert.Equal("todo_not_completed",WorkspaceCommandEngine.Accept(restored,Restore(restored),Now).ErrorCode);
        var archived=state with { Document=state.Document with { ActivityCoverage=null, Tasks=[state.Document.Tasks[0] with { IsArchived=true }] } };Assert.Equal("task_unavailable",WorkspaceCommandEngine.Accept(archived,command,Now).ErrorCode);
        var other=Guid.NewGuid();var added=WorkspaceCommandEngine.Accept(state,new(Guid.NewGuid(),state.Document.Id,state.Document.Revision,WorkspaceOperation.CreateTodo,other,new(Text:"Other")),Now).Snapshot!;
        added=WorkspaceCommandEngine.Accept(added,new(Guid.NewGuid(),added.Document.Id,added.Document.Revision,WorkspaceOperation.CompleteTask,other),Now).Snapshot!;
        Assert.Equal("completion_unavailable",WorkspaceCommandEngine.Accept(added,Restore(added,added.Document.Completions.Last().Id),Now).ErrorCode);
    }
    [Theory]
    [InlineData(ProjectionState.Linked,false)] [InlineData(ProjectionState.Linked,true)] [InlineData(ProjectionState.Pending,false)] [InlineData(ProjectionState.UnknownOutcome,true)] [InlineData(ProjectionState.ExternalConflict,false)] [InlineData(ProjectionState.Unlinked,true)]
    public void Retained_game_identity_even_on_archived_mapping_prevents_restore(ProjectionState mapping,bool archived)
    {
        var state=Seed();state=state with { Document=state.Document with { Projections=[new(Guid.NewGuid(),state.Document.Revision,state.Document.Tasks[0].Id,mapping,Guid.NewGuid().ToString("D"),archived)] } };
        Assert.Equal("unsupported_projection",WorkspaceCommandEngine.Accept(state,Restore(state),Now).ErrorCode);
    }
    [Fact]
    public void Portable_restoration_requires_matching_native_command_snapshot_and_record()
    {
        var state=Seed();state=WorkspaceCommandEngine.Accept(state,Restore(state),Now).Snapshot!;var entry=state.Commands.Last();
        foreach(var broken in new[]{entry with { TaskSnapshot=null },entry with { TaskSnapshot=entry.TaskSnapshot! with { State=CompanionTaskState.Completed } },entry with { Effects=[ProjectionEffectKind.ScoreUp],ProjectionState=ProjectionCommandState.Pending },entry with { Command=entry.Command with { Count=2 },PayloadHash=WorkspaceCommandIdentity.Hash(entry.Command with { Count=2 }) }})Assert.False(WorkspaceJournalValidation.IsValid(state with { Commands=state.Commands.SkipLast(1).Append(broken).ToArray() }));
        Assert.False(WorkspaceJournalValidation.IsValid(state with { Commands=state.Commands.SkipLast(1).Append(entry with { AcceptedAtUtc=Now.AddMinutes(1),TaskSnapshot=entry.TaskSnapshot! with { UpdatedAtUtc=Now.AddMinutes(1) } }).ToArray() }));
    }
    [Fact]
    public void Task_order_is_stable_and_non_Todo_kinds_are_refused()
    {
        var state=Seed();var target=state.Document.Tasks[0].Id;
        foreach(var kind in new[]{WorkspaceOperation.CreateDaily,WorkspaceOperation.CreateHabit})state=WorkspaceCommandEngine.Accept(state,new(Guid.NewGuid(),state.Document.Id,state.Document.Revision,kind,Guid.NewGuid(),new(Text:"Recurring work")),Now).Snapshot!;
        var order=state.Document.Tasks.Select(task=>task.Id).ToArray();var restored=WorkspaceCommandEngine.Accept(state,Restore(state),Now).Snapshot!;Assert.Equal(order,restored.Document.Tasks.Select(task=>task.Id));
        foreach(var task in state.Document.Tasks.Where(task=>task.Id!=target))Assert.Equal("todo_not_completed",WorkspaceCommandEngine.Accept(state,Restore(state) with { TaskId=task.Id },Now).ErrorCode);
        var mapping=new HabiticaProjection(Guid.NewGuid(),state.Document.Revision,target,ProjectionState.Unlinked,IsArchived:true);
        state=state with { Document=state.Document with { Projections=[mapping] } };var result=WorkspaceCommandEngine.Accept(state,Restore(state),Now);Assert.Equal(SubmissionDisposition.Accepted,result.Disposition);Assert.Equal(mapping,result.Snapshot!.Document.Projections[0]);
    }
}
