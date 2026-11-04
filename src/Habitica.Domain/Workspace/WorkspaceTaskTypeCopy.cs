using System.Text.Json.Serialization;
namespace Habitica.Domain.Workspace;

public sealed record WorkspaceTaskTypeCopy(Guid SourceTaskId,long SourceRevision,
    [property:JsonRequired] CompanionTaskKind TargetKind,
    [property:JsonRequired] bool CopyOrganization,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RecurrenceRule? Recurrence=null)
{
    public bool IsValid()=>SourceTaskId!=Guid.Empty&&SourceRevision is >=1 and <WorkspaceDocument.MaxRevision&&Enum.IsDefined(TargetKind)
        &&(TargetKind==CompanionTaskKind.Daily?Recurrence is not null&&RecurrenceCalendar.IsStructurallyValid(Recurrence):Recurrence is null);
}
public sealed record WorkspaceTaskTypeCopyReview(CompanionTask? Source,CompanionTask? Target,IReadOnlyList<string> CopiedFields,IReadOnlyList<string> ResetFields,string? Failure)
{
    public bool Ready=>Source is not null&&Target is not null&&Failure is null;
}
public static class WorkspaceTaskTypeCopyPolicy
{
    public static bool RulesEqual(RecurrenceRule? a,RecurrenceRule? b)=>a is null?b is null:b is not null&&a.Frequency==b.Frequency&&a.Interval==b.Interval&&a.StartDate==b.StartDate&&a.TimeZoneId==b.TimeZoneId&&a.Weekdays.SequenceEqual(b.Weekdays)&&a.DaysOfMonth.SequenceEqual(b.DaysOfMonth);
    public static bool ReviewMatches(WorkspaceDocument document,WorkspaceTaskTypeCopy choice,Guid targetId,WorkspaceTaskTypeCopyReview review)
    {
        if(!review.Ready||review.Source is null||review.Target is null||review.Target.CopyLineage is not null||review.Target.HabiticaDetails is not null)return false;
        var expected=Review(document,choice,targetId,review.Target.CreatedAtUtc);
        return expected.Ready&&WorkspaceTaskCopyLineage.SourceFieldsEqual(expected.Source!,review.Source)&&WorkspaceTaskCopyLineage.SourceFieldsEqual(expected.Target!,review.Target)
            &&(expected.Source!.CopyLineage is null?review.Source.CopyLineage is null:expected.Source.CopyLineage.SameAs(review.Source.CopyLineage))
            &&expected.CopiedFields.SequenceEqual(review.CopiedFields)&&expected.ResetFields.SequenceEqual(review.ResetFields);
    }
    public static bool InitialTargetMatches(CompanionTask target,WorkspaceTaskCopyLineage lineage)
    {
        var source=lineage.SourceSnapshot;
        static bool SameRule(RecurrenceRule? a,RecurrenceRule? b)=>a is null?b is null:b is not null&&a.Frequency==b.Frequency&&a.Interval==b.Interval&&a.StartDate==b.StartDate&&a.TimeZoneId==b.TimeZoneId&&a.Weekdays.SequenceEqual(b.Weekdays)&&a.DaysOfMonth.SequenceEqual(b.DaysOfMonth);
        return target.Id==lineage.TargetTaskId&&target.Revision==lineage.InitialTargetRevision&&target.Kind==lineage.TargetKind&&target.State==CompanionTaskState.Active&&!target.IsArchived
            &&target.CreatedAtUtc==lineage.CopiedAtUtc&&target.UpdatedAtUtc==lineage.CopiedAtUtc&&target.Text==source.Text&&target.Notes==source.Notes&&target.Priority==source.Priority
            &&target.CategoryId==(lineage.CopyOrganization?source.CategoryId:null)&&target.ProjectId==(lineage.CopyOrganization?source.ProjectId:null)&&target.SectionId==(lineage.CopyOrganization?source.SectionId:null)
            &&(target.LabelIds??[]).SequenceEqual(lineage.CopyOrganization?source.LabelIds??[]:[])&&(target.DependsOn?.Count??0)==0&&target.ParentTaskId is null
            &&target.PlannedDate is null&&target.Deadline is null&&target.ReviewAfterUtc is null&&target.StaleSinceUtc is null&&target.ExecutionStage is null&&target.HabiticaDetails is null&&SameRule(target.Recurrence,lineage.Recurrence);
    }

    public static WorkspaceTaskTypeCopyReview Review(WorkspaceDocument document,WorkspaceTaskTypeCopy choice,Guid targetId,DateTimeOffset now)
    {
        WorkspaceTaskTypeCopyReview Refuse(string code,CompanionTask? source=null)=>new(source,null,[],[],code);
        if(document is null||choice is null||!choice.IsValid()||targetId==Guid.Empty||targetId==choice.SourceTaskId)return Refuse("invalid_type_copy");
        if(WorkspaceRules.Validate(document).Count!=0||document.Revision>=WorkspaceDocument.MaxRevision)return Refuse("invalid_workspace");
        var source=document.Tasks.FirstOrDefault(task=>task.Id==choice.SourceTaskId);
        if(source is null||source.Revision!=choice.SourceRevision)return Refuse("source_revision_mismatch",source);
        if(source.Kind==choice.TargetKind)return Refuse("different_task_kind_required",source);
        if(now==default||now.Offset!=TimeSpan.Zero||now<source.UpdatedAtUtc)return Refuse("invalid_copy_clock",source);
        if(choice.Recurrence is {} requested&&!RecurrenceCalendar.Expand(requested,requested.StartDate,requested.StartDate).Succeeded)return Refuse("invalid_type_copy",source);
        if(choice.CopyOrganization&&(
            source.CategoryId is {} category&&!document.Categories.Any(row=>row.Id==category&&!row.IsArchived)
            ||source.ProjectId is {} project&&!document.Projects.Any(row=>row.Id==project&&!row.IsArchived&&!row.IsRemoved)
            ||source.SectionId is {} section&&!document.Sections.Any(row=>row.Id==section&&!row.IsArchived&&row.ProjectId==source.ProjectId)
            ||(source.LabelIds??[]).Any(id=>!document.Labels.Any(row=>row.Id==id&&!row.IsArchived))))return Refuse("copy_organization_repair_required",source);
        var target=new CompanionTask(targetId,document.Revision+1,source.Text,choice.TargetKind,CompanionTaskState.Active,source.Priority,now,now,source.Notes,
            CategoryId:choice.CopyOrganization?source.CategoryId:null,ProjectId:choice.CopyOrganization?source.ProjectId:null,SectionId:choice.CopyOrganization?source.SectionId:null,
            LabelIds:choice.CopyOrganization?(source.LabelIds??[]).ToArray():[],DependsOn:[],Recurrence:choice.Recurrence);
        if(WorkspaceRules.Validate(document with{Revision=document.Revision+1,Tasks=document.Tasks.Append(target).ToArray()}).Count!=0)return Refuse("invalid_type_copy_target",source);
        return new(source,target,choice.CopyOrganization?["Name","Notes","Native urgency","Category/project/section/labels"]:["Name","Notes","Native urgency"],
            ["State becomes active","Creation/update timestamps","Habitica correspondence","Completion/score/occurrence history","Game difficulty/attribute/checklist/direction settings","Planned/deadline/review dates","Todo stage/parent/dependencies/goal membership","Source schedule is retained only on the source; Daily target recurrence is a new explicit choice",choice.CopyOrganization?"No organization mutation":"Target category/project/section/labels are empty"],null);
    }
}

public sealed record WorkspaceTaskCopyLineage(
    Guid SourceWorkspaceId,CompanionTask SourceSnapshot,Guid TargetTaskId,
    [property:JsonRequired] CompanionTaskKind TargetKind,
    [property:JsonRequired] bool CopyOrganization,
    long InitialTargetRevision,Guid CommandId,string PayloadHash,DateTimeOffset CopiedAtUtc,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RecurrenceRule? Recurrence=null)
{
    public bool HasValidShape()=>SourceWorkspaceId!=Guid.Empty&&CommandId!=Guid.Empty&&TargetTaskId!=Guid.Empty&&SourceSnapshot is not null&&SnapshotValid(SourceSnapshot)
        &&SourceSnapshot.CopyLineage is null&&SourceSnapshot.Id!=TargetTaskId&&SourceSnapshot.Kind!=TargetKind&&Enum.IsDefined(TargetKind)
        &&InitialTargetRevision is >=2 and <=WorkspaceDocument.MaxRevision&&SourceSnapshot.Revision<InitialTargetRevision
        &&CopiedAtUtc!=default&&CopiedAtUtc.Offset==TimeSpan.Zero&&SourceSnapshot.UpdatedAtUtc<=CopiedAtUtc
        &&PayloadHash is not null&&PayloadHash.Length==64&&PayloadHash.All(c=>"0123456789abcdef".Contains(c))
        &&(TargetKind==CompanionTaskKind.Daily?Recurrence is not null&&RecurrenceCalendar.IsStructurallyValid(Recurrence):Recurrence is null);
    public bool IsValid(WorkspaceDocument document,CompanionTask target)
    {
        if(!HasValidShape()||TargetTaskId!=target.Id||TargetKind!=target.Kind||InitialTargetRevision>target.Revision||CopiedAtUtc!=target.CreatedAtUtc)return false;
        if(SourceWorkspaceId==document.Id)return document.Tasks.Any(row=>row.Id==SourceSnapshot.Id&&row.Kind==SourceSnapshot.Kind&&row.Revision>=SourceSnapshot.Revision);
        return document.SourceImports?.Any(origin=>origin.SourceWorkspaceId==SourceWorkspaceId&&origin.SourceRevision>=InitialTargetRevision&&origin.RecordIds.Contains(target.Id))==true;
    }
    public bool SameAs(WorkspaceTaskCopyLineage? other)=>other is not null&&SourceSnapshot is not null&&other.SourceSnapshot is not null
        &&SourceWorkspaceId==other.SourceWorkspaceId&&TargetTaskId==other.TargetTaskId&&TargetKind==other.TargetKind&&CopyOrganization==other.CopyOrganization
        &&InitialTargetRevision==other.InitialTargetRevision&&CommandId==other.CommandId&&PayloadHash==other.PayloadHash&&CopiedAtUtc==other.CopiedAtUtc
        &&System.Text.Json.JsonSerializer.Serialize(Recurrence)==System.Text.Json.JsonSerializer.Serialize(other.Recurrence)
        &&SourceSignature(SourceSnapshot)==SourceSignature(other.SourceSnapshot);
    public static bool SourceFieldsEqual(CompanionTask a,CompanionTask b)=>SourceSignature(a)==SourceSignature(b);
    private static string SourceSignature(CompanionTask source)
    {
        var details=source.HabiticaDetails??new HabiticaTaskDetails();
        return System.Text.Json.JsonSerializer.Serialize(source with{CopyLineage=null,LabelIds=source.LabelIds??[],DependsOn=source.DependsOn??[],HabiticaDetails=details with{Checklist=details.Checklist??[]}});
    }
    private static bool SnapshotValid(CompanionTask task)=>task.Id!=Guid.Empty&&task.Revision is >=1 and <WorkspaceDocument.MaxRevision
        &&Enum.IsDefined(task.Kind)&&Enum.IsDefined(task.State)&&Enum.IsDefined(task.Priority)
        &&(task.Kind==CompanionTaskKind.Todo||task.State is not (CompanionTaskState.Completed or CompanionTaskState.Cancelled))
        &&!string.IsNullOrWhiteSpace(task.Text)&&task.Text.Length<=2048&&task.Notes?.Length is not >10000
        &&task.CreatedAtUtc!=default&&task.CreatedAtUtc.Offset==TimeSpan.Zero&&task.UpdatedAtUtc.Offset==TimeSpan.Zero&&task.UpdatedAtUtc>=task.CreatedAtUtc
        &&task.CategoryId!=Guid.Empty&&task.ProjectId!=Guid.Empty&&task.SectionId!=Guid.Empty&&task.ParentTaskId!=Guid.Empty&&task.ParentTaskId!=task.Id
        &&(task.LabelIds is null||task.LabelIds.All(id=>id!=Guid.Empty)&&task.LabelIds.Distinct().Count()==task.LabelIds.Count)
        &&(task.DependsOn is null||task.DependsOn.All(id=>id!=Guid.Empty&&id!=task.Id)&&task.DependsOn.Distinct().Count()==task.DependsOn.Count)
        &&task.PlannedDate!=DateOnly.MinValue&&task.Deadline!=DateOnly.MinValue
        &&(task.ReviewAfterUtc is null||task.ReviewAfterUtc.Value!=default&&task.ReviewAfterUtc.Value.Offset==TimeSpan.Zero)
        &&(task.StaleSinceUtc is null||task.StaleSinceUtc.Value!=default&&task.StaleSinceUtc.Value.Offset==TimeSpan.Zero)
        &&(task.Recurrence is null||task.Kind==CompanionTaskKind.Daily&&RecurrenceCalendar.IsStructurallyValid(task.Recurrence))
        &&HabiticaTaskDetails.IsValid(task.HabiticaDetails)&&(task.Kind==CompanionTaskKind.Habit||task.HabiticaDetails?.Up is null&&task.HabiticaDetails?.Down is null)
        &&(task.ExecutionStage is null||task.Kind==CompanionTaskKind.Todo&&Enum.IsDefined(task.ExecutionStage.Value));
}
