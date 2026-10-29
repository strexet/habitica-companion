using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceConversionRetirementTests
{
    private static readonly Guid Account=Guid.Parse("00000000-0000-4000-8000-000000000011");
    private static readonly DateTimeOffset Now=new(2026,10,22,0,0,0,TimeSpan.Zero);
    private static WorkspaceJournalSnapshot Fixture(string name="reuse")
    {
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","checklist-conversion-"+name+".json")));
        var document=WorkspaceTaskApiJson.Deserialize<WorkspaceDocument>(json.RootElement.GetProperty("acceptedDocument").GetRawText());
        var projections=document.Tasks.Select(task=>new HabiticaProjection(Guid.NewGuid(),document.Revision,task.Id,ProjectionState.Linked,Guid.NewGuid().ToString("D"),Fingerprint:new string('a',64))).ToArray();
        var project=document.Projects.Single();var tagId=Guid.NewGuid().ToString("D");var mapId=Guid.NewGuid();var evidence=new WorkspaceTagEvidence(Guid.NewGuid(),Now,new string('b',64));
        var mapping=new WorkspaceTagMapping(mapId,document.Revision,project.Id,WorkspaceOrganizationKind.Project,WorkspaceTagState.Confirmed,document.Revision,Guid.NewGuid(),WorkspaceTagProjection.Alias(document.Id,WorkspaceOrganizationKind.Project,project.Id),project.Name,tagId,evidence);
        var members=document.Tasks.Where(task=>task.ProjectId==project.Id).Select(task=>new WorkspaceTagMember(task.Id,projections.Single(row=>row.TaskId==task.Id).HabiticaTaskId,true,WorkspaceTagState.Confirmed,evidence)).ToArray();
        var intent=new WorkspaceTagIntent(Guid.NewGuid(),document.Revision,mapId,Guid.NewGuid(),Guid.NewGuid(),document.Revision,WorkspaceTagOperation.SetMembership,WorkspaceTagState.Confirmed,tagId,Members:members,Evidence:evidence);
        document=document with {Projections=projections,TagLedger=new(Account,[mapping],[intent])};var state=new WorkspaceJournalSnapshot(document,document.Revision,[],[]);Assert.True(WorkspaceJournalValidation.IsValid(state));return state;
    }
    [Fact]
    public void Complete_stored_coverage_can_only_prepare_a_fresh_native_review_and_changes_nothing()
    {
        var state=Fixture();var before=WorkspaceJournalJson.Serialize(state);var conversion=state.Document.ChecklistConversions!.Single();var result=WorkspaceConversionRetirementPolicy.Inspect(state,conversion.Id,Account);
        Assert.True(result.CanPrepareNativeReview,string.Join(',',result.Blockers));Assert.True(result.RequiresFreshNativeVerification);Assert.False(result.CanDelete);Assert.Equal(10,result.Plan!.Targets.Count);Assert.Equal(2,result.Plan.Targets.Count(row=>row.DesiredState==ChecklistConversionState.CompletedArchived));Assert.Equal(before,WorkspaceJournalJson.Serialize(state));Assert.Equal(64,result.Plan.ContextHash.Length);
        if(Environment.GetEnvironmentVariable("HABITICA_RETIREMENT_FIXTURES") is {} directory){Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"conversion-retirement-prerequisites.json"),WorkspaceTaskApiJson.Serialize(new {state=JsonDocument.Parse(before).RootElement.Clone(),accountId=Account,conversionId=conversion.Id,contextHash=result.Plan.ContextHash}));}
    }
    [Fact]
    public void Deferred_historical_matches_and_missing_or_uncertain_mappings_never_retire_sources()
    {
        var partial=Fixture("create");Assert.Contains("historical_target_required",WorkspaceConversionRetirementPolicy.Inspect(partial,partial.Document.ChecklistConversions!.Single().Id,Account).Blockers);
        var state=Fixture();var conversion=state.Document.ChecklistConversions!.Single();var target=conversion.Items[0].TargetTaskId!.Value;
        foreach(var stateKind in new[]{ProjectionState.Pending,ProjectionState.UnknownOutcome,ProjectionState.ExternalConflict})
        {var changed=state with {Document=state.Document with {Projections=state.Document.Projections.Select(row=>row.TaskId==target?row with {State=stateKind}:row).ToArray()}};var result=WorkspaceConversionRetirementPolicy.Inspect(changed,conversion.Id,Account);Assert.False(result.CanPrepareNativeReview);Assert.Contains("target_mapping_required",result.Blockers);}
        Assert.Contains("source_mapping_required",WorkspaceConversionRetirementPolicy.Inspect(state with {Document=state.Document with {Projections=state.Document.Projections.Where(row=>row.TaskId!=conversion.SourceTaskId).ToArray()}},conversion.Id,Account).Blockers);
    }
    [Fact]
    public void Changed_content_membership_and_account_proof_block_preparation()
    {
        var state=Fixture();var conversion=state.Document.ChecklistConversions!.Single();var source=conversion.SourceTaskId;
        var changed=state with {Document=state.Document with {Tasks=state.Document.Tasks.Select(task=>task.Id==source?task with {Notes="Changed even without a revision bump"}:task).ToArray()}};Assert.Contains("conversion_source_changed",WorkspaceConversionRetirementPolicy.Inspect(changed,conversion.Id,Account).Blockers);
        Assert.Contains("retirement_tag_account_unverified",WorkspaceConversionRetirementPolicy.Inspect(state,conversion.Id,Guid.NewGuid()).Blockers);
        var dependent=conversion.Items[0].TargetTaskId;changed=state with {Document=state.Document with {Tasks=state.Document.Tasks.Select(task=>task.Id==dependent?task with {DependsOn=[source]}:task).ToArray()}};Assert.Contains("source_relationships_require_review",WorkspaceConversionRetirementPolicy.Inspect(changed,conversion.Id,Account).Blockers);
        var ledger=state.Document.TagLedger!;var intent=ledger.Intents.Single();changed=state with {Document=state.Document with {TagLedger=ledger with {Intents=[intent with {Members=intent.Members!.Skip(1).ToArray()}]}}};Assert.Contains("project_tag_membership_requires_review",WorkspaceConversionRetirementPolicy.Inspect(changed,conversion.Id,Account).Blockers);
        var target=conversion.Items[0].TargetTaskId;changed=state with {Document=state.Document with {Tasks=state.Document.Tasks.Select(task=>task.Id==target?task with {ProjectId=null}:task).ToArray()}};Assert.Contains("conversion_target_placement_changed",WorkspaceConversionRetirementPolicy.Inspect(changed,conversion.Id,Account).Blockers);
    }
    [Fact]
    public void Unrelated_revisions_and_task_metadata_keep_exact_stored_membership()
    {
        var state=Fixture();var conversion=state.Document.ChecklistConversions!.Single();var target=conversion.Items[0].TargetTaskId!.Value;var revision=state.Document.Revision+1;
        var document=state.Document with {Revision=revision,Tasks=state.Document.Tasks.Select(row=>row.Id==target?row with {Revision=revision,Text="Independent metadata",Priority=CompanionPriority.Urgent,Deadline=new DateOnly(2030,12,31)}:row).ToArray(),Projections=state.Document.Projections.Select(row=>row.TaskId==target?row with {Revision=revision}:row).ToArray()};
        var changed=state with {Document=document,StoreVersion=state.StoreVersion+1};var result=WorkspaceConversionRetirementPolicy.Inspect(changed,conversion.Id,Account);Assert.True(result.CanPrepareNativeReview,string.Join(',',result.Blockers));Assert.True(result.RequiresFreshNativeVerification);Assert.False(result.CanDelete);
        var coverage=WorkspaceHabiticaCoveragePolicy.Audit(document,Account);Assert.Equal(1,coverage.ConfirmedRecordedMemberships);
        var completed=document with {Tasks=document.Tasks.Select(row=>row.Id==target?row with {State=CompanionTaskState.Completed}:row).ToArray(),Completions=[..document.Completions,new CompletionEvent(Guid.NewGuid(),revision,target,Guid.NewGuid(),Now.AddDays(1))]};
        Assert.Equal(1,WorkspaceHabiticaCoveragePolicy.Audit(completed,Account).ConfirmedRecordedMemberships);Assert.False(WorkspaceConversionRetirementPolicy.Inspect(changed with {Document=completed},conversion.Id,Account).CanPrepareNativeReview);
        var renamed=document with {Projects=document.Projects.Select(row=>row with {Name="Renamed"}).ToArray()};Assert.False(WorkspaceConversionRetirementPolicy.Inspect(changed with {Document=renamed},conversion.Id,Account).CanPrepareNativeReview);Assert.Equal(0,WorkspaceHabiticaCoveragePolicy.Audit(renamed,Account).ConfirmedRecordedMemberships);
    }
    [Fact]
    public void Pending_projection_blocks_even_when_all_stored_task_identities_exist()
    {
        var state=Fixture();var result=WorkspaceCommandEngine.Accept(state,new(Guid.NewGuid(),state.Document.Id,state.Document.Revision,WorkspaceOperation.CreateTodo,Guid.NewGuid(),new(Text:"Independent pending work"),ProjectToHabitica:true),Now);Assert.Equal(SubmissionDisposition.Accepted,result.Disposition);
        var readiness=WorkspaceConversionRetirementPolicy.Inspect(result.Snapshot!,state.Document.ChecklistConversions!.Single().Id,Account);Assert.Contains("unresolved_task_effects",readiness.Blockers);Assert.False(readiness.CanDelete);
    }
    [Theory]
    [InlineData(SourceRetirementOutcome.OriginalReceipt)]
    [InlineData(SourceRetirementOutcome.AlreadyAbsent)]
    [InlineData(SourceRetirementOutcome.ObservedAbsentAfterUnknown)]
    public void Retirement_receipt_retains_source_targets_history_and_portable_original_outcome(SourceRetirementOutcome outcome)
    {
        var state=Fixture();var conversion=state.Document.ChecklistConversions!.Single();var plan=WorkspaceConversionRetirementPolicy.Inspect(state,conversion.Id,Account).Plan!;var revision=state.Document.Revision+1;var at=Now.AddDays(1);
        var receipt=new WorkspaceSourceRetirement(Guid.NewGuid(),revision,conversion.Id,conversion.SourceTaskId,conversion.SourceRevision,state.Document.Revision,Account,plan.SourceMapping.HabiticaTaskId!,plan.ContextHash,outcome,outcome==SourceRetirementOutcome.AlreadyAbsent?null:Guid.NewGuid(),Now,at);
        var retired=state with {StoreVersion=state.StoreVersion+1,Document=state.Document with {Revision=revision,SourceRetirements=[receipt],Tasks=state.Document.Tasks.Select(task=>task.Id==conversion.SourceTaskId?task with {Revision=revision,UpdatedAtUtc=at,IsArchived=true}:task).ToArray(),Projections=state.Document.Projections.Select(row=>row.TaskId==conversion.SourceTaskId?row with {Revision=revision,IsArchived=true}:row).ToArray()}};
        Assert.True(WorkspaceJournalValidation.IsValid(retired));var restored=WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(retired));Assert.Equal(receipt,restored.Document.SourceRetirements!.Single());Assert.Equal(state.Document.Completions,retired.Document.Completions);Assert.Equal(state.Commands,retired.Commands);Assert.Equal(state.Document.TagLedger,retired.Document.TagLedger);
        Assert.Equal(state.Document.Tasks.Where(task=>task.Id!=conversion.SourceTaskId),retired.Document.Tasks.Where(task=>task.Id!=conversion.SourceTaskId));Assert.Equal(state.Document.ChecklistConversions,retired.Document.ChecklistConversions);var coverage=WorkspaceHabiticaCoveragePolicy.Audit(restored.Document,Account);Assert.Equal(1,coverage.RetiredSourceContainers);Assert.Equal(10,coverage.Tasks.Count);Assert.DoesNotContain(coverage.Tasks,row=>row.TaskId==conversion.SourceTaskId);Assert.False(WorkspaceConversionRetirementPolicy.Inspect(restored,conversion.Id,Account).CanPrepareNativeReview);
        var target=conversion.Items[0].TargetTaskId!.Value;var later=WorkspaceCommandEngine.Accept(retired,new(Guid.NewGuid(),retired.Document.Id,retired.Document.Revision,WorkspaceOperation.CompleteTask,target,ProjectToHabitica:true),at.AddMinutes(1));Assert.Equal(SubmissionDisposition.Accepted,later.Disposition);Assert.Equal(receipt,later.Snapshot!.Document.SourceRetirements!.Single());
        var forged=retired with {Document=retired.Document with {Tasks=retired.Document.Tasks.Select(task=>task.Id==conversion.SourceTaskId?task with {Notes="Lost original notes"}:task).ToArray()}};Assert.False(WorkspaceJournalValidation.IsValid(forged));
        forged=retired with {Document=retired.Document with {SourceRetirements=[receipt with {NativeAttemptId=outcome==SourceRetirementOutcome.AlreadyAbsent?Guid.NewGuid():null}]}};Assert.False(WorkspaceJournalValidation.IsValid(forged));
        if(Environment.GetEnvironmentVariable("HABITICA_RETIREMENT_FIXTURES") is {} directory){Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"conversion-retirement-"+(outcome switch {SourceRetirementOutcome.OriginalReceipt=>"original-receipt",SourceRetirementOutcome.AlreadyAbsent=>"already-absent",_=>"observed-absent-after-unknown"})+".json"),WorkspaceJournalJson.Serialize(retired));}
    }

}
