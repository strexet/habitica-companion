using Habitica.Domain.Workspace;
using Habitica.Application.Workspace;
using System.Text.Json;
namespace Habitica.Application.Tests.Workspace;
public sealed class TaskTypeConversionPolicyTests
{
    private static readonly DateTimeOffset Now=DateTimeOffset.Parse("2026-11-03T00:00:00Z");
    private static CompanionTask Source=>new(Guid.NewGuid(),1,"Retain exact title",CompanionTaskKind.Todo,CompanionTaskState.Completed,CompanionPriority.High,Now.AddDays(-10),Now.AddDays(-1),Notes:"Retain exact notes",Deadline:new(2026,11,10),ExecutionStage:TodoExecutionStage.InProgress);
    private static IReadOnlyList<CompletionEvent> History(CompanionTask source)=>source.Kind==CompanionTaskKind.Todo&&source.State==CompanionTaskState.Completed?[new(Guid.NewGuid(),1,source.Id,Guid.NewGuid(),source.UpdatedAtUtc)]:[];
    [Fact]public void A_reviewed_native_type_copy_retains_source_and_resets_target_history_and_game_fields()
    {
        var source=Source;var document=new WorkspaceDocument{Id=Guid.NewGuid(),Revision=10,Tasks=[source],Completions=History(source)};Assert.Empty(WorkspaceRules.Validate(document));var targetId=Guid.NewGuid();var review=WorkspaceTaskTypeCopyPolicy.Review(document,new(source.Id,source.Revision,CompanionTaskKind.Habit,false),targetId,Now);
        Assert.True(review.Ready,review.Failure);Assert.Same(source,review.Source);var target=review.Target!;Assert.Equal(targetId,target.Id);Assert.Equal(CompanionTaskKind.Habit,target.Kind);Assert.Equal(CompanionTaskState.Active,target.State);Assert.Equal(source.Text,target.Text);Assert.Equal(source.Notes,target.Notes);Assert.Equal(source.Priority,target.Priority);Assert.Null(target.Deadline);Assert.Null(target.ExecutionStage);Assert.Null(target.HabiticaDetails);Assert.Null(target.Recurrence);Assert.Empty(document.HabitScores);Assert.Single(document.Tasks);Assert.Same(source,document.Tasks[0]);
    }
    [Theory]
    [InlineData(CompanionTaskKind.Todo,CompanionTaskKind.Daily)]
    [InlineData(CompanionTaskKind.Todo,CompanionTaskKind.Habit)]
    [InlineData(CompanionTaskKind.Daily,CompanionTaskKind.Todo)]
    [InlineData(CompanionTaskKind.Daily,CompanionTaskKind.Habit)]
    [InlineData(CompanionTaskKind.Habit,CompanionTaskKind.Todo)]
    [InlineData(CompanionTaskKind.Habit,CompanionTaskKind.Daily)]
    public void All_distinct_kind_pairs_create_active_unlinked_previews_without_mutating_historical_source(CompanionTaskKind from,CompanionTaskKind to)
    {
        var source=Source with{Kind=from,IsArchived=true,State=from==CompanionTaskKind.Todo?CompanionTaskState.Completed:CompanionTaskState.Paused,ExecutionStage=from==CompanionTaskKind.Todo?TodoExecutionStage.InProgress:null};
        var mapping=new HabiticaProjection(Guid.NewGuid(),2,source.Id,ProjectionState.Linked,Guid.NewGuid().ToString("D"));var document=new WorkspaceDocument{Id=Guid.NewGuid(),Revision=10,Tasks=[source],Completions=History(source),Projections=[mapping]};
        var recurrence=to==CompanionTaskKind.Daily?new RecurrenceRule(RecurrenceFrequency.Monthly,1,new(2026,11,3),"America/New_York",[],[3,31]):null;
        Assert.Empty(WorkspaceRules.Validate(document));var review=WorkspaceTaskTypeCopyPolicy.Review(document,new(source.Id,source.Revision,to,false,recurrence),Guid.NewGuid(),Now);Assert.True(review.Ready,review.Failure);Assert.Equal(to,review.Target!.Kind);Assert.False(review.Target.IsArchived);Assert.Equal(CompanionTaskState.Active,review.Target.State);Assert.Same(recurrence,review.Target.Recurrence);Assert.Same(source,review.Source);Assert.Single(document.Projections);Assert.Equal(mapping,document.Projections[0]);Assert.Null(review.Target.ParentTaskId);Assert.Empty(review.Target.DependsOn!);Assert.Null(review.Target.HabiticaDetails);Assert.Contains("Habitica correspondence",review.ResetFields);
    }
    [Fact]public void Type_copy_refuses_same_kind_stale_source_missing_daily_rule_or_unreviewed_kind_fields()
    {
        var source=Source;var doc=new WorkspaceDocument{Id=Guid.NewGuid(),Revision=10,Tasks=[source],Completions=History(source)};var choice=new WorkspaceTaskTypeCopy(source.Id,source.Revision,CompanionTaskKind.Habit,false);var target=Guid.NewGuid();
        Assert.Equal("different_task_kind_required",WorkspaceTaskTypeCopyPolicy.Review(doc,choice with{TargetKind=CompanionTaskKind.Todo},target,Now).Failure);
        Assert.Equal("source_revision_mismatch",WorkspaceTaskTypeCopyPolicy.Review(doc,choice with{SourceRevision=2},target,Now).Failure);
        Assert.Equal("invalid_type_copy",WorkspaceTaskTypeCopyPolicy.Review(doc,choice with{TargetKind=CompanionTaskKind.Daily},target,Now).Failure);
        var rule=new RecurrenceRule(RecurrenceFrequency.Daily,1,new(2026,11,3),"UTC",[],[]);Assert.Equal("invalid_type_copy",WorkspaceTaskTypeCopyPolicy.Review(doc,choice with{Recurrence=rule},target,Now).Failure);
        Assert.Equal("invalid_type_copy",WorkspaceTaskTypeCopyPolicy.Review(doc,choice,source.Id,Now).Failure);
    }
    [Fact]public void Retaining_organization_requires_active_exact_references_and_explicit_clearing_never_changes_the_source()
    {
        var category=new WorkspaceCategory(Guid.NewGuid(),1,"Category");var project=new WorkspaceProject(Guid.NewGuid(),2,"Project",category.Id);var section=new WorkspaceSection(Guid.NewGuid(),3,"Section",project.Id);var label=new WorkspaceLabel(Guid.NewGuid(),4,"Label");var source=Source with{CategoryId=category.Id,ProjectId=project.Id,SectionId=section.Id,LabelIds=[label.Id]};var doc=new WorkspaceDocument{Id=Guid.NewGuid(),Revision=10,Tasks=[source],Completions=History(source),Categories=[category],Projects=[project],Sections=[section],Labels=[label]};var choice=new WorkspaceTaskTypeCopy(source.Id,source.Revision,CompanionTaskKind.Habit,true);var target=Guid.NewGuid();
        var review=WorkspaceTaskTypeCopyPolicy.Review(doc,choice,target,Now);Assert.True(review.Ready,review.Failure);Assert.Equal(source.ProjectId,review.Target!.ProjectId);Assert.Equal(source.LabelIds,review.Target.LabelIds);
        var archived=doc with{Labels=[label with{IsArchived=true}]};Assert.Equal("copy_organization_repair_required",WorkspaceTaskTypeCopyPolicy.Review(archived,choice,target,Now).Failure);var cleared=WorkspaceTaskTypeCopyPolicy.Review(archived,choice with{CopyOrganization=false},target,Now);Assert.True(cleared.Ready,cleared.Failure);Assert.Null(cleared.Target!.ProjectId);Assert.Empty(cleared.Target.LabelIds!);Assert.Equal(project.Id,cleared.Source!.ProjectId);
    }
    [Fact]public void Shallow_per_task_copy_lineage_survives_document_export_and_source_revision_offset_import()
    {
        var source=Source;var doc=new WorkspaceDocument{Id=Guid.NewGuid(),Revision=10,Tasks=[source],Completions=History(source)};var choice=new WorkspaceTaskTypeCopy(source.Id,source.Revision,CompanionTaskKind.Habit,false);var target=WorkspaceTaskTypeCopyPolicy.Review(doc,choice,Guid.NewGuid(),Now).Target!;
        var lineage=new WorkspaceTaskCopyLineage(doc.Id,source,target.Id,target.Kind,false,target.Revision,Guid.NewGuid(),new string('a',64),Now);target=target with{CopyLineage=lineage};var result=doc with{Revision=11,Tasks=[source,target]};var text=WorkspaceJson.Serialize(result);Assert.DoesNotContain("copyLineage",WorkspaceJson.Serialize(doc));var loaded=WorkspaceJson.Deserialize(text);Assert.Equal(source.Id,loaded.Tasks[1].CopyLineage!.SourceSnapshot.Id);Assert.Equal(1,loaded.Tasks[1].CopyLineage!.SourceSnapshot.Revision);
        var imported=result with{Id=Guid.NewGuid(),Revision=111,Tasks=result.Tasks.Select(task=>task with{Revision=task.Revision+100}).ToArray(),Completions=result.Completions.Select(e=>e with{Revision=e.Revision+100}).ToArray(),SourceImports=[new(Guid.NewGuid(),doc.Id,11,100,[source.Id,target.Id])]};var recovered=WorkspaceJson.Deserialize(WorkspaceJson.Serialize(imported));Assert.Equal(doc.Id,recovered.Tasks[1].CopyLineage!.SourceWorkspaceId);Assert.Equal(11,recovered.Tasks[1].CopyLineage!.InitialTargetRevision);Assert.Equal(1,recovered.Tasks[1].CopyLineage!.SourceSnapshot.Revision);Assert.Empty(WorkspaceRules.Validate(recovered));
    }
    [Fact]public void Copy_lineage_refuses_changed_target_kind_missing_source_or_recursive_source_snapshots()
    {
        var source=Source;var doc=new WorkspaceDocument{Id=Guid.NewGuid(),Revision=10,Tasks=[source],Completions=History(source)};var target=WorkspaceTaskTypeCopyPolicy.Review(doc,new(source.Id,source.Revision,CompanionTaskKind.Habit,false),Guid.NewGuid(),Now).Target!;var lineage=new WorkspaceTaskCopyLineage(doc.Id,source,target.Id,target.Kind,false,target.Revision,Guid.NewGuid(),new string('a',64),Now);target=target with{CopyLineage=lineage};var result=doc with{Revision=11,Tasks=[source,target]};Assert.Empty(WorkspaceRules.Validate(result));
        Assert.Contains(WorkspaceRules.Validate(result with{Tasks=[source,target with{Kind=CompanionTaskKind.Daily}]}),e=>e.Code=="invalid_task_copy_lineage");Assert.Contains(WorkspaceRules.Validate(result with{Tasks=[target],Completions=[]}),e=>e.Code=="invalid_task_copy_lineage");var recursive=lineage with{SourceSnapshot=source with{CopyLineage=lineage}};Assert.Contains(WorkspaceRules.Validate(result with{Tasks=[source,target with{CopyLineage=recursive}]}),e=>e.Code=="invalid_task_copy_lineage");
    }
    [Fact]public void Atomic_type_copy_retains_source_receipts_and_history_and_recovers_one_exact_target_after_lost_acknowledgement()
    {
        var source=Source;var mapping=new HabiticaProjection(Guid.NewGuid(),2,source.Id,ProjectionState.Linked,Guid.NewGuid().ToString("D"));var doc=new WorkspaceDocument{Id=Guid.NewGuid(),Revision=10,Tasks=[source],Completions=History(source),Projections=[mapping]};var state=new WorkspaceJournalSnapshot(doc,10,[],[]);var rule=new RecurrenceRule(RecurrenceFrequency.Monthly,1,new(2026,11,3),"America/New_York",[],[3,31]);var command=new WorkspaceCommand(Guid.NewGuid(),doc.Id,doc.Revision,WorkspaceOperation.CopyTaskType,Guid.NewGuid(),new(TypeCopy:new(source.Id,source.Revision,CompanionTaskKind.Daily,false,rule)));
        Assert.True(WorkspaceJournalValidation.IsValid(state),"The source journal fixture must be valid.");var result=WorkspaceCommandEngine.Accept(state,command,Now);Assert.True(result.Disposition==SubmissionDisposition.Accepted,result.ErrorCode);var saved=result.Snapshot!;Assert.Equal(11,saved.Document.Revision);Assert.Equal(source,saved.Document.Tasks[0]);Assert.Equal(doc.Completions,saved.Document.Completions);Assert.Equal(doc.Projections,saved.Document.Projections);Assert.Equal(2,saved.Document.Tasks.Count);var target=saved.Document.Tasks[1];Assert.Equal(command.TaskId,target.Id);Assert.Equal(command.Id,target.CopyLineage!.CommandId);Assert.Equal(result.Entry!.PayloadHash,target.CopyLineage.PayloadHash);Assert.Empty(saved.Document.HabitScores);Assert.Empty(result.Entry.Effects);Assert.Equal(ProjectionCommandState.NotRequested,result.Entry.ProjectionState);Assert.Equal(0,saved.Document.ActivityCoverage!.Single().PreviousTaskRevision);
        var reloaded=WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(saved));var duplicate=WorkspaceCommandEngine.Accept(reloaded,command,Now.AddDays(1));Assert.Equal(SubmissionDisposition.Duplicate,duplicate.Disposition);Assert.Equal(command.TaskId,duplicate.Entry!.TaskSnapshot!.Id);Assert.Equal(2,duplicate.Snapshot!.Document.Tasks.Count);Assert.Equal(source.Kind,reloaded.Document.Tasks[0].Kind);Assert.Equal(SubmissionDisposition.IdempotencyConflict,WorkspaceCommandEngine.Accept(reloaded,command with{TaskId=Guid.NewGuid()},Now).Disposition);
    }
    [Fact]public void Type_copy_forbids_projection_mixed_patches_stale_source_and_existing_target_identity_before_mutation()
    {
        var source=Source;var doc=new WorkspaceDocument{Id=Guid.NewGuid(),Revision=10,Tasks=[source],Completions=History(source)};var state=new WorkspaceJournalSnapshot(doc,10,[],[]);var command=new WorkspaceCommand(Guid.NewGuid(),doc.Id,10,WorkspaceOperation.CopyTaskType,Guid.NewGuid(),new(TypeCopy:new(source.Id,source.Revision,CompanionTaskKind.Habit,false)));
        foreach(var invalid in new[]{command with{ProjectToHabitica=true},command with{Patch=command.Patch! with{Text="Unrequested rewrite"}},command with{TaskId=source.Id},command with{Patch=new(TypeCopy:command.Patch!.TypeCopy! with{SourceRevision=2})}}){Assert.NotEqual(SubmissionDisposition.Accepted,WorkspaceCommandEngine.Accept(state,invalid,Now).Disposition);Assert.Single(state.Document.Tasks);Assert.Empty(state.Commands);}
        Assert.Equal(SubmissionDisposition.RevisionConflict,WorkspaceCommandEngine.Accept(state,command with{ExpectedRevision=9},Now).Disposition);
    }
    [Fact]public void Accepted_copy_lineage_cannot_change_when_the_original_source_advances_later()
    {
        var source=Source;var doc=new WorkspaceDocument{Id=Guid.NewGuid(),Revision=10,Tasks=[source],Completions=History(source)};var state=new WorkspaceJournalSnapshot(doc,10,[],[]);var command=new WorkspaceCommand(Guid.NewGuid(),doc.Id,10,WorkspaceOperation.CopyTaskType,Guid.NewGuid(),new(TypeCopy:new(source.Id,source.Revision,CompanionTaskKind.Habit,false)));var accepted=WorkspaceCommandEngine.Accept(state,command,Now);Assert.Equal(SubmissionDisposition.Accepted,accepted.Disposition);var saved=accepted.Snapshot!;
        var later=saved with{Document=saved.Document with{Revision=13,Tasks=saved.Document.Tasks.Select(t=>t.Id==source.Id?t with{Revision=12,Notes="A legitimate later source edit",UpdatedAtUtc=Now.AddSeconds(1)}:t).ToArray()},StoreVersion=13};Assert.True(WorkspaceJournalValidation.IsValid(later));
        var target=later.Document.Tasks.Single(t=>t.Id==command.TaskId);var tampered=target.CopyLineage! with{SourceSnapshot=target.CopyLineage!.SourceSnapshot with{Notes="Forged earlier source details"}};var corrupt=later with{Document=later.Document with{Tasks=later.Document.Tasks.Select(t=>t.Id==target.Id?t with{CopyLineage=tampered}:t).ToArray()}};Assert.False(WorkspaceJournalValidation.IsValid(corrupt));
    }
    [Fact]public void Stored_copy_rules_remain_readable_when_a_timezone_is_unavailable_for_new_native_acceptance()
    {
        var source=Source;var doc=new WorkspaceDocument{Id=Guid.NewGuid(),Revision=10,Tasks=[source],Completions=History(source)};var rule=new RecurrenceRule(RecurrenceFrequency.Daily,1,new(2026,11,3),"Missing/Historical_Zone",[],[]);var choice=new WorkspaceTaskTypeCopy(source.Id,source.Revision,CompanionTaskKind.Daily,false,rule);Assert.True(choice.IsValid());Assert.Equal("invalid_type_copy",WorkspaceTaskTypeCopyPolicy.Review(doc,choice,Guid.NewGuid(),Now).Failure);
        var target=new CompanionTask(Guid.NewGuid(),11,source.Text,CompanionTaskKind.Daily,CompanionTaskState.Active,source.Priority,Now,Now,Notes:source.Notes,Recurrence:rule);target=target with{CopyLineage=new(doc.Id,source,target.Id,target.Kind,false,11,Guid.NewGuid(),new string('a',64),Now,rule)};var stored=doc with{Revision=11,Tasks=[source,target]};Assert.Equal(rule.TimeZoneId,WorkspaceJson.Deserialize(WorkspaceJson.Serialize(stored)).Tasks[1].CopyLineage!.Recurrence!.TimeZoneId);
    }
    [Fact]public void Independent_portable_type_copy_examples_match_all_kind_pairs_fields_and_v13_hashes()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","task-type-copy-v1.json")));Assert.Equal(6,data.RootElement.GetProperty("cases").GetArrayLength());
        foreach(var row in data.RootElement.GetProperty("cases").EnumerateArray())
        {
            var document=WorkspaceJson.Deserialize(row.GetProperty("document").GetRawText());var command=WorkspaceTaskApiJson.Deserialize<WorkspaceCommand>(row.GetProperty("command").GetRawText());var before=WorkspaceJson.Serialize(document);Assert.Equal(row.GetProperty("payloadHash").GetString(),WorkspaceCommandIdentity.Hash(command));var accepted=WorkspaceCommandEngine.Accept(new(document,10,[],[]),command,row.GetProperty("now").GetDateTimeOffset());Assert.True(accepted.Disposition==SubmissionDisposition.Accepted,accepted.ErrorCode);var target=accepted.Entry!.TaskSnapshot!;var expected=WorkspaceTaskApiJson.Deserialize<CompanionTask>(row.GetProperty("expectedTarget").GetRawText());Assert.Equal(expected.Text,target.Text);Assert.Equal(expected.Notes,target.Notes);Assert.Equal(expected.Kind,target.Kind);Assert.Equal(expected.State,target.State);Assert.Equal(expected.Priority,target.Priority);Assert.Equal(expected.CategoryId,target.CategoryId);Assert.Equal(expected.ProjectId,target.ProjectId);Assert.Equal(expected.SectionId,target.SectionId);Assert.Equal(expected.LabelIds,target.LabelIds);Assert.Null(target.Deadline);Assert.Null(target.ReviewAfterUtc);Assert.Null(target.HabiticaDetails);Assert.Null(target.ExecutionStage);Assert.Empty(accepted.Entry.Effects);Assert.Equal(before,WorkspaceJson.Serialize(document));Assert.Equal(document.Completions,accepted.Snapshot!.Document.Completions);Assert.Equal(document.HabitScores,accepted.Snapshot.Document.HabitScores);WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(accepted.Snapshot));
        }
    }
}
