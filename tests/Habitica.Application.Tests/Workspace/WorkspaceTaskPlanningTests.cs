using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceTaskPlanningTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-18T00:00:00Z");
    private static WorkspaceJournalSnapshot Seed()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","organization-v1.json")));
        return new(WorkspaceJson.Deserialize(fixture.RootElement.GetProperty("document").GetRawText()),10,[],[]);
    }
    private static WorkspaceCommand Intent(WorkspaceJournalSnapshot state, WorkspaceTaskPlanning plan) => new(Guid.NewGuid(),state.Document.Id,state.Document.Revision,WorkspaceOperation.PlanTask,state.Document.Tasks[0].Id,new(Planning:plan));
    private static CommandSubmission Apply(WorkspaceJournalSnapshot state, WorkspaceTaskPlanning plan) => WorkspaceCommandEngine.Accept(state,Intent(state,plan),Now);

    [Fact]
    public void Portable_v4_hashes_preserve_appended_values_and_legacy_defaults()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","planning-command-v4.json")));
        foreach(var value in fixture.RootElement.GetProperty("cases").EnumerateArray())Assert.Equal(value.GetProperty("payloadHash").GetString(),WorkspaceCommandIdentity.Hash(WorkspaceTaskApiJson.Deserialize<WorkspaceCommand>(value.GetProperty("command").GetRawText())));
        Assert.Equal(10,(int)WorkspaceOperation.PlanTask); Assert.Equal(9,(int)WorkspaceOperation.ResumeTask);
        using var legacy = JsonDocument.Parse(WorkspaceTaskApiJson.Serialize(new WorkspaceTaskPatch(Text:"Legacy")));Assert.False(legacy.RootElement.TryGetProperty("planning",out _));
    }
    [Fact]
    public void Placement_partial_edits_and_clearing_retain_order_history_and_original_receipts()
    {
        var state=Seed();var original=state.Document.Tasks[0];var order=state.Document.Tasks.Select(task=>task.Id).ToArray();
        var plan=new WorkspaceTaskPlanning(new(2026,10,20),CategoryId:state.Document.Categories[0].Id,ProjectId:state.Document.Projects[1].Id,SectionId:state.Document.Sections[3].Id,LabelIds:[state.Document.Labels[0].Id]);
        var accepted=Apply(state,plan);Assert.Equal(SubmissionDisposition.Accepted,accepted.Disposition);state=accepted.Snapshot!;
        Assert.Equal(order,state.Document.Tasks.Select(task=>task.Id));var task=state.Document.Tasks[0];Assert.Equal(plan.PlannedDate,task.PlannedDate);Assert.Equal(plan.ProjectId,task.ProjectId);Assert.Equal(plan.SectionId,task.SectionId);Assert.Equal(original.Deadline,task.Deadline);Assert.Equal(original.Text,task.Text);Assert.Equal(original.Notes,task.Notes);Assert.Equal(original.HabiticaDetails,task.HabiticaDetails);
        state=Apply(state,new(PlannedDate:new(2026,10,22))).Snapshot!;Assert.Equal(plan.ProjectId,state.Document.Tasks[0].ProjectId);Assert.Equal(plan.LabelIds,state.Document.Tasks[0].LabelIds);
        Assert.Equal("invalid_planning_reference",Apply(state,new(ClearProject:true)).ErrorCode);
        state=Apply(state,new(ClearPlannedDate:true,ClearCategory:true,ClearProject:true,ClearSection:true,LabelIds:[])).Snapshot!;
        Assert.Null(state.Document.Tasks[0].PlannedDate);Assert.Null(state.Document.Tasks[0].ProjectId);Assert.Null(state.Document.Tasks[0].SectionId);Assert.Empty(state.Document.Tasks[0].LabelIds!);
        var duplicate=WorkspaceCommandEngine.Accept(state,accepted.Entry!.Command,Now.AddMinutes(1));Assert.Equal(SubmissionDisposition.Duplicate,duplicate.Disposition);Assert.Equal(accepted.Entry,duplicate.Entry);Assert.Equal(3,state.Commands.Count);Assert.Single(state.Document.Completions);Assert.Empty(state.Document.HabitScores);
        Assert.Equal(SubmissionDisposition.IdempotencyConflict,WorkspaceCommandEngine.Accept(state,accepted.Entry.Command with { Patch=new(Planning:new(PlannedDate:new(2026,10,25))) },Now).Disposition);
        Assert.True(WorkspaceJournalValidation.IsValid(WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(state))));
    }
    [Theory]
    [InlineData(ProjectionState.Unlinked)] [InlineData(ProjectionState.Linked)] [InlineData(ProjectionState.Pending)] [InlineData(ProjectionState.UnknownOutcome)] [InlineData(ProjectionState.ExternalConflict)]
    public void Native_planning_preserves_any_existing_mapping_and_never_enqueues_game_effects(ProjectionState mapping)
    {
        var state=Seed();var existing=state.Document.Projections[0] with { State=mapping,HabiticaTaskId=mapping==ProjectionState.Unlinked?null:state.Document.Projections[0].HabiticaTaskId };
        state=state with { Document=state.Document with { Projections=state.Document.Projections.Select(value=>value.TaskId==existing.TaskId?existing:value).ToArray() } };
        var result=Apply(state,new(PlannedDate:new(2026,10,20)));Assert.Equal(SubmissionDisposition.Accepted,result.Disposition);Assert.Equal(existing,result.Snapshot!.Document.Projections[0]);Assert.Empty(result.Entry!.Effects);Assert.Equal(ProjectionCommandState.NotRequested,result.Entry.ProjectionState);
        Assert.Equal("unsupported_projection",WorkspaceCommandEngine.Accept(state,Intent(state,new(PlannedDate:new(2026,10,20))) with { ProjectToHabitica=true },Now).ErrorCode);
    }
    [Fact]
    public void Invalid_patches_archived_references_and_project_section_mismatch_never_accept()
    {
        var state=Seed();foreach(var plan in new WorkspaceTaskPlanning[]{new(),new(PlannedDate:DateOnly.MinValue),new(PlannedDate:new(2026,10,20),ClearPlannedDate:true),new(ProjectId:Guid.Empty),new(LabelIds:[Guid.Empty]),new(LabelIds:[state.Document.Labels[0].Id,state.Document.Labels[0].Id]),new(CategoryId:state.Document.Categories[2].Id),new(ProjectId:state.Document.Projects[2].Id),new(SectionId:state.Document.Sections[2].Id),new(LabelIds:[state.Document.Labels[1].Id]),new(ProjectId:state.Document.Projects[1].Id,SectionId:state.Document.Sections[0].Id),new(SectionId:state.Document.Sections[4].Id),new(CategoryId:Guid.NewGuid())})Assert.Equal(SubmissionDisposition.Invalid,Apply(state,plan).Disposition);
        var command=Intent(state,new(PlannedDate:new(2026,10,20)));
        Assert.Equal("invalid_patch",WorkspaceCommandEngine.Accept(state,command with { Patch=command.Patch! with { Text="Wrong field" } },Now).ErrorCode);
        Assert.Equal("invalid_patch",WorkspaceCommandEngine.Accept(state,command with { Operation=WorkspaceOperation.UpdateTask },Now).ErrorCode);
        Assert.Equal(SubmissionDisposition.RevisionConflict,WorkspaceCommandEngine.Accept(state,command with { ExpectedRevision=9 },Now).Disposition);
    }
    [Fact]
    public void Portable_planning_requires_matching_snapshot_and_zero_game_effects()
    {
        var accepted=Apply(Seed(),new(PlannedDate:new(2026,10,20)));var state=accepted.Snapshot!;var entry=accepted.Entry!;
        Assert.False(WorkspaceJournalValidation.IsValid(state with { Commands=[entry with { TaskSnapshot=null }] }));
        Assert.False(WorkspaceJournalValidation.IsValid(state with { Commands=[entry with { TaskSnapshot=entry.TaskSnapshot! with { PlannedDate=new(2026,10,21) } }] }));
        Assert.False(WorkspaceJournalValidation.IsValid(state with { Commands=[entry with { Effects=[ProjectionEffectKind.Update],ProjectionState=ProjectionCommandState.Pending }] }));
        var command=entry.Command with { ProjectToHabitica=true };Assert.False(WorkspaceJournalValidation.IsValid(state with { Commands=[entry with { Command=command,PayloadHash=WorkspaceCommandIdentity.Hash(command) }] }));
    }
    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void Daily_paused_Habit_and_completed_Todo_placement_preserves_lifecycle(int index)
    {
        var state=Seed();var original=state.Document.Tasks[index];var command=Intent(state,new(PlannedDate:new(2026,10,20))) with { TaskId=original.Id };
        var result=WorkspaceCommandEngine.Accept(state,command,Now);Assert.Equal(SubmissionDisposition.Accepted,result.Disposition);
        Assert.Equal(original.State,result.Snapshot!.Document.Tasks[index].State);Assert.Equal(original.Kind,result.Snapshot.Document.Tasks[index].Kind);
        Assert.Equal(state.Document.Completions,result.Snapshot.Document.Completions);Assert.Empty(result.Entry!.Effects);
    }
}
