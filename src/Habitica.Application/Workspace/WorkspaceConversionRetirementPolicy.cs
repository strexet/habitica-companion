using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

public sealed record ConversionRetirementTarget(Guid SourceItemId,ChecklistConversionState DesiredState,CompanionTask Task,HabiticaProjection Mapping);
public sealed record ConversionRetirementPlan(Guid WorkspaceId,long Revision,Guid ConversionId,CompanionTask Source,HabiticaProjection SourceMapping,
    WorkspaceProject Project,Guid HabiticaProjectTagId,IReadOnlyList<ConversionRetirementTarget> Targets,string ContextHash);
/// <summary>Stored prerequisites only. Neither portable evidence nor this result authorizes a native delete.</summary>
public sealed record ConversionRetirementReadiness(ConversionRetirementPlan? Plan,IReadOnlyList<string> Blockers)
{
    public bool CanPrepareNativeReview=>Plan is not null&&Blockers.Count==0;
    public bool RequiresFreshNativeVerification=>true;
    public bool CanDelete=>false;
}

public static class WorkspaceConversionRetirementPolicy
{
    public static ConversionRetirementReadiness Inspect(WorkspaceJournalSnapshot state,Guid conversionId,Guid? expectedAccountId)
    {
        var blockers=new List<string>();
        ConversionRetirementReadiness Refuse(string code)=>new(null,[code]);
        if(!WorkspaceJournalValidation.IsValid(state))return Refuse("invalid_workspace");
        if(expectedAccountId is null||expectedAccountId==Guid.Empty)return Refuse("retirement_account_unverified");
        var document=state.Document;var conversion=document.ChecklistConversions?.SingleOrDefault(row=>row.Id==conversionId&&!row.IsArchived);
        if(conversion is null)return Refuse("conversion_unavailable");
        var source=document.Tasks.Single(task=>task.Id==conversion.SourceTaskId);
        if(source.IsArchived||source.Revision!=conversion.SourceRevision||WorkspaceTaskApiJson.Serialize(source)!=WorkspaceTaskApiJson.Serialize(conversion.SourceSnapshot))return Refuse("conversion_source_changed");
        var project=document.Projects.SingleOrDefault(row=>row.Id==conversion.ProjectId&&!row.IsArchived&&!row.IsRemoved);
        if(project is null)return Refuse("conversion_project_unavailable");
        if(source.ParentTaskId is not null||source.DependsOn is {Count:>0}||document.Tasks.Any(task=>task.ParentTaskId==source.Id||task.DependsOn?.Contains(source.Id)==true)||document.Goals.Any(goal=>goal.TaskIds.Contains(source.Id)))blockers.Add("source_relationships_require_review");
        if(state.Commands.Any(row=>row.ProjectionState is ProjectionCommandState.Pending or ProjectionCommandState.Leased or ProjectionCommandState.Executing or ProjectionCommandState.Blocked or ProjectionCommandState.UnknownOutcome))blockers.Add("unresolved_task_effects");
        // Trusted server work and Auth fences require separate checks even when this portable journal is clean.
        var sourceMapping=document.Projections.SingleOrDefault(row=>row.TaskId==source.Id);
        if(!Linked(sourceMapping)||conversion.SourceProjection?.HabiticaTaskId is {} original&&sourceMapping?.HabiticaTaskId!=original)blockers.Add("source_mapping_required");
        var targets=new List<ConversionRetirementTarget>();
        foreach(var binding in conversion.Items)
        {
            if(binding.TargetTaskId is not {} id){blockers.Add(binding.DesiredState==ChecklistConversionState.CompletedArchived?"historical_target_required":"conversion_target_required");continue;}
            var task=document.Tasks.Single(row=>row.Id==id);
            if(task.ProjectId!=project.Id||(binding.DesiredState==ChecklistConversionState.Active?task.State!=CompanionTaskState.Active||task.IsArchived:task.State!=CompanionTaskState.Completed||!task.IsArchived))blockers.Add("conversion_target_placement_changed");
            var mapping=document.Projections.SingleOrDefault(row=>row.TaskId==id);
            if(!Linked(mapping)){blockers.Add("target_mapping_required");continue;}
            targets.Add(new(binding.SourceItemId,binding.DesiredState,task,mapping!));
        }
        Guid nativeTag=Guid.Empty;var ledger=document.TagLedger;
        if(ledger?.AccountId!=expectedAccountId)blockers.Add("retirement_tag_account_unverified");
        else
        {
            var mapping=ledger.Mappings.SingleOrDefault(row=>row.EntityId==project.Id&&row.Kind==WorkspaceOrganizationKind.Project);
            var group=WorkspaceTagCorrespondence.Desired(document).Single(row=>row.Kind==WorkspaceOrganizationKind.Project&&row.Entity.Id==project.Id);
            if(!WorkspaceTagCorrespondence.Inspect(document,group,mapping).Confirmed||!Guid.TryParseExact(mapping?.HabiticaTagId,"D",out nativeTag))blockers.Add("project_tag_membership_requires_review");

        }
        if(blockers.Count>0)return new(null,blockers.Distinct().ToArray());
        var plan=new ConversionRetirementPlan(document.Id,document.Revision,conversion.Id,source,sourceMapping!,project,nativeTag,targets,"");
        return new(plan with {ContextHash=Hash(plan)},[]);
    }
    private static bool Linked(HabiticaProjection? mapping)=>mapping is {State:ProjectionState.Linked,IsArchived:false,Fingerprint:{Length:64}}
        &&Guid.TryParseExact(mapping.HabiticaTaskId,"D",out var id)&&id!=Guid.Empty;
    public static string Hash(ConversionRetirementPlan plan)
    {
        static string Number(long value)=>value.ToString(CultureInfo.InvariantCulture);
        static string Name<T>(T value) where T:struct,Enum=>System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
        var fields=new List<string?>{plan.WorkspaceId.ToString("D"),Number(plan.Revision),plan.ConversionId.ToString("D"),plan.Project.Id.ToString("D"),plan.Project.Name,plan.HabiticaProjectTagId.ToString("D"),Number(plan.Targets.Count)};
        void Task(CompanionTask task,HabiticaProjection mapping)
        {
            fields.AddRange([task.Id.ToString("D"),Number(task.Revision),task.Text,task.Notes,Name(task.Kind),Name(task.State),Name(task.Priority),task.IsArchived?"true":"false",task.ProjectId?.ToString("D"),task.Deadline?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),
                task.HabiticaDetails?.Difficulty?.ToString("R",CultureInfo.InvariantCulture),task.HabiticaDetails?.Attribute is {} attribute?Name(attribute):null,mapping.Id.ToString("D"),Number(mapping.Revision),mapping.HabiticaTaskId,mapping.Fingerprint]);
            var items=task.HabiticaDetails?.Checklist;fields.Add(items is null?null:Number(items.Count));foreach(var item in items??[])fields.AddRange([item.Id.ToString("D"),item.Text,item.Completed?"true":"false"]);
        }
        Task(plan.Source,plan.SourceMapping);
        foreach(var target in plan.Targets){fields.Add(target.SourceItemId.ToString("D"));fields.Add(Name(target.DesiredState));Task(target.Task,target.Mapping);}
        var text=new StringBuilder("companion-conversion-retirement-context-v1\n");foreach(var field in fields)text.Append(field is null?"-1:":Encoding.UTF8.GetByteCount(field).ToString(CultureInfo.InvariantCulture)+":"+field);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()))).ToLowerInvariant();
    }
}
