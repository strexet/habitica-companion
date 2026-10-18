using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceTaskRelationshipTests
{
    private static readonly DateTimeOffset Now=DateTimeOffset.Parse("2026-10-18T12:00:00Z");
    private static WorkspaceJournalSnapshot Seed()
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","organization-v1.json")));
        return new(WorkspaceJson.Deserialize(fixture.RootElement.GetProperty("document").GetRawText()),10,[],[]);
    }
    private static WorkspaceCommand Intent(WorkspaceJournalSnapshot state,WorkspaceTaskRelationships relationships,Guid? target=null)=>new(Guid.NewGuid(),state.Document.Id,state.Document.Revision,WorkspaceOperation.RelateTodo,target??state.Document.Tasks[0].Id,new(Relationships:relationships));
    [Fact]
    public void Independent_v6_identities_and_appended_enum_preserve_legacy_wire_defaults()
    {
        using var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","task-relationships-command-v6.json")));
        foreach(var item in fixture.RootElement.GetProperty("cases").EnumerateArray())Assert.Equal(item.GetProperty("payloadHash").GetString(),WorkspaceCommandIdentity.Hash(WorkspaceTaskApiJson.Deserialize<WorkspaceCommand>(item.GetProperty("command").GetRawText())));
        Assert.Equal(12,(int)WorkspaceOperation.RelateTodo);Assert.Equal(11,(int)WorkspaceOperation.RestoreTodo);Assert.False(WorkspaceTaskApiJson.Element(new WorkspaceTaskPatch(Text:"Legacy")).TryGetProperty("relationships",out _));
    }
    [Fact]
    public void Partial_and_explicit_replacement_preserve_state_order_metadata_and_original_receipts()
    {
        var state=Seed();var task=state.Document.Tasks[0];var order=state.Document.Tasks.Select(row=>row.Id).ToArray();var parent=state.Document.Tasks[1].Id;var dependency=state.Document.Tasks[4].Id;
        var command=Intent(state,new(ParentTaskId:parent,DependsOn:[dependency]));var result=WorkspaceCommandEngine.Accept(state,command,Now);Assert.Equal(SubmissionDisposition.Accepted,result.Disposition);state=result.Snapshot!;
        Assert.Equal(order,state.Document.Tasks.Select(row=>row.Id));Assert.Equal(new[]{dependency},state.Document.Tasks[0].DependsOn);Assert.Equal(task with { ParentTaskId=parent,DependsOn=state.Document.Tasks[0].DependsOn,Revision=11,UpdatedAtUtc=Now },state.Document.Tasks[0]);Assert.Equal(task.ProjectId,state.Document.Tasks[0].ProjectId);
        state=WorkspaceCommandEngine.Accept(state,Intent(state,new(DependsOn:[])),Now).Snapshot!;Assert.Equal(parent,state.Document.Tasks[0].ParentTaskId);Assert.Empty(state.Document.Tasks[0].DependsOn!);
        state=WorkspaceCommandEngine.Accept(state,Intent(state,new(ClearParentTask:true)),Now).Snapshot!;Assert.Null(state.Document.Tasks[0].ParentTaskId);Assert.Empty(state.Document.Tasks[0].DependsOn!);
        var duplicate=WorkspaceCommandEngine.Accept(state,command,Now);Assert.Equal(SubmissionDisposition.Duplicate,duplicate.Disposition);Assert.Equal(result.Entry,duplicate.Entry);Assert.Equal(SubmissionDisposition.IdempotencyConflict,WorkspaceCommandEngine.Accept(state,command with { Patch=new(Relationships:new(ClearParentTask:true)) },Now).Disposition);Assert.Equal(3,state.Commands.Count);Assert.All(state.Commands,entry=>Assert.Empty(entry.Effects));Assert.Equal(Seed().Document.Completions,state.Document.Completions);
        Assert.True(WorkspaceJournalValidation.IsValid(WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(state))));
    }
    [Theory]
    [InlineData(ProjectionState.Unlinked)] [InlineData(ProjectionState.Linked)] [InlineData(ProjectionState.Pending)] [InlineData(ProjectionState.UnknownOutcome)] [InlineData(ProjectionState.ExternalConflict)]
    public void Native_relationships_preserve_mappings_and_enqueue_no_game_effects(ProjectionState mapping)
    {
        var state=Seed();var existing=state.Document.Projections[0] with { State=mapping,HabiticaTaskId=mapping==ProjectionState.Unlinked?null:state.Document.Projections[0].HabiticaTaskId };
        state=state with { Document=state.Document with { Projections=state.Document.Projections.Select(row=>row.Id==existing.Id?existing:row).ToArray() } };
        var command=Intent(state,new(DependsOn:[state.Document.Tasks[4].Id]));var result=WorkspaceCommandEngine.Accept(state,command,Now);Assert.Equal(SubmissionDisposition.Accepted,result.Disposition);Assert.Equal(existing,result.Snapshot!.Document.Projections[0]);Assert.Empty(result.Entry!.Effects);Assert.Equal(ProjectionCommandState.NotRequested,result.Entry.ProjectionState);
        Assert.Equal("unsupported_projection",WorkspaceCommandEngine.Accept(state,command with { ProjectToHabitica=true },Now).ErrorCode);
    }
    [Fact]
    public void Exact_nonarchived_Todo_references_and_combined_graph_acyclicity_are_required()
    {
        var state=Seed();var self=state.Document.Tasks[0].Id;
        foreach(var patch in new WorkspaceTaskRelationships[]{new(),new(ParentTaskId:Guid.Empty),new(ParentTaskId:self),new(DependsOn:[self]),new(ParentTaskId:Guid.NewGuid()),new(DependsOn:[Guid.Empty]),new(DependsOn:[self,self]),new(ParentTaskId:state.Document.Tasks[1].Id,ClearParentTask:true),new(DependsOn:Enumerable.Repeat(state.Document.Tasks[4].Id,101).ToArray())})Assert.Equal(SubmissionDisposition.Invalid,WorkspaceCommandEngine.Accept(state,Intent(state,patch),Now).Disposition);
        foreach(var index in new[]{2,3,5})foreach(var patch in new[]{new WorkspaceTaskRelationships(ParentTaskId:state.Document.Tasks[index].Id),new WorkspaceTaskRelationships(DependsOn:[state.Document.Tasks[index].Id])})Assert.Equal("invalid_relationship_reference",WorkspaceCommandEngine.Accept(state,Intent(state,patch),Now).ErrorCode);
        foreach(var index in new[]{2,3})Assert.Equal("invalid_relationship_reference",WorkspaceCommandEngine.Accept(state,Intent(state,new(ClearParentTask:true),state.Document.Tasks[index].Id),Now).ErrorCode);
        Assert.Equal("task_unavailable",WorkspaceCommandEngine.Accept(state,Intent(state,new(ClearParentTask:true),state.Document.Tasks[5].Id),Now).ErrorCode);
        state=WorkspaceCommandEngine.Accept(state,Intent(state,new(ParentTaskId:state.Document.Tasks[1].Id)),Now).Snapshot!;
        Assert.Equal("invalid_result",WorkspaceCommandEngine.Accept(state,Intent(state,new(DependsOn:[self]),state.Document.Tasks[1].Id),Now).ErrorCode);
        var mixed=Intent(Seed(),new(DependsOn:[]));Assert.Equal("invalid_patch",WorkspaceCommandEngine.Accept(Seed(),mixed with { Patch=mixed.Patch! with { Text="Unexpected" } },Now).ErrorCode);
    }
    [Fact]
    public void Portable_relationship_snapshots_require_exact_fields_and_zero_effects()
    {
        var state=Seed();var result=WorkspaceCommandEngine.Accept(state,Intent(state,new(ParentTaskId:state.Document.Tasks[1].Id,DependsOn:[state.Document.Tasks[4].Id])),Now);state=result.Snapshot!;var entry=result.Entry!;
        foreach(var altered in new[]{entry with { TaskSnapshot=null },entry with { TaskSnapshot=entry.TaskSnapshot! with { ParentTaskId=null } },entry with { TaskSnapshot=entry.TaskSnapshot! with { DependsOn=[] } },entry with { Effects=[ProjectionEffectKind.Update],ProjectionState=ProjectionCommandState.Pending }})Assert.False(WorkspaceJournalValidation.IsValid(state with { Commands=[altered] }));
    }
    [Theory]
    [InlineData(4)] [InlineData(7)]
    public void Completed_and_cancelled_Todos_keep_lifecycle_and_history(int index)
    {
        var state=Seed();var original=state.Document.Tasks[index];var result=WorkspaceCommandEngine.Accept(state,Intent(state,new(ParentTaskId:state.Document.Tasks[1].Id),original.Id),Now);
        Assert.Equal(SubmissionDisposition.Accepted,result.Disposition);Assert.Equal(original.State,result.Snapshot!.Document.Tasks[index].State);Assert.Equal(state.Document.Completions,result.Snapshot.Document.Completions);Assert.Empty(result.Entry!.Effects);
    }
    [Fact]
    public void Omitted_historical_references_remain_and_parent_can_be_an_explicit_dependency()
    {
        var state=Seed();var task=state.Document.Tasks[0] with { DependsOn=new[]{state.Document.Tasks[2].Id,state.Document.Tasks[5].Id} };
        state=state with { Document=state.Document with { Tasks=state.Document.Tasks.Select(row=>row.Id==task.Id?task:row).ToArray() } };
        var parent=state.Document.Tasks[1].Id;var first=WorkspaceCommandEngine.Accept(state,Intent(state,new(ParentTaskId:parent)),Now);Assert.Equal(SubmissionDisposition.Accepted,first.Disposition);Assert.Equal(task.DependsOn,first.Snapshot!.Document.Tasks[0].DependsOn);
        state=first.Snapshot;var both=WorkspaceCommandEngine.Accept(state,Intent(state,new(ParentTaskId:parent,DependsOn:[parent])),Now);Assert.Equal(SubmissionDisposition.Accepted,both.Disposition);Assert.Equal(parent,both.Snapshot!.Document.Tasks[0].ParentTaskId);Assert.Equal(new[]{parent},both.Snapshot.Document.Tasks[0].DependsOn);
    }
}
