using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

public sealed record WorkspaceTagDesiredGroup(WorkspaceEntity Entity,WorkspaceOrganizationKind Kind,string Name,IReadOnlySet<Guid> TaskIds);
public sealed record WorkspaceTagCorrespondenceResult(bool IdentityCurrent,bool Exact,bool Confirmed,WorkspaceTagIntent? Intent);
public static class WorkspaceTagCorrespondence
{
    public static IReadOnlyList<WorkspaceTagDesiredGroup> Desired(WorkspaceDocument document)
    {
        var retired=document.SourceRetirements is {Count:>0}&&WorkspaceSourceRetirements.IsValid(document)?document.SourceRetirements.Select(row=>row.SourceTaskId).ToHashSet():[];
        var tasks=document.Tasks.Where(row=>!retired.Contains(row.Id)).ToArray();
        var projects=document.Projects.ToDictionary(row=>row.Id);var groups=new List<WorkspaceTagDesiredGroup>();
        bool Historical(CompanionTask row)=>row.IsArchived||row.State is CompanionTaskState.Completed or CompanionTaskState.Cancelled;
        void Add(WorkspaceEntity row,WorkspaceOrganizationKind kind,string name,IEnumerable<Guid> ids)=>groups.Add(new(row,kind,name,ids.Where(id=>!retired.Contains(id)).ToHashSet()));
        foreach(var row in document.Projects)Add(row,WorkspaceOrganizationKind.Project,row.Name,tasks.Where(task=>task.ProjectId==row.Id&&(!row.IsRemoved||Historical(task))).Select(task=>task.Id));
        foreach(var row in document.Sections)Add(row,WorkspaceOrganizationKind.Section,row.Name,tasks.Where(task=>task.SectionId==row.Id&&(!projects[row.ProjectId].IsRemoved||Historical(task))).Select(task=>task.Id));
        foreach(var row in document.Categories)Add(row,WorkspaceOrganizationKind.Category,row.Name,tasks.Where(task=>(task.CategoryId??(task.ProjectId is {} id&&projects.TryGetValue(id,out var project)&&!project.IsRemoved?project.CategoryId:null))==row.Id).Select(task=>task.Id));
        foreach(var row in document.Labels)Add(row,WorkspaceOrganizationKind.Label,row.Name,tasks.Where(task=>task.LabelIds?.Contains(row.Id)==true).Select(task=>task.Id));
        foreach(var row in document.Goals)Add(row,WorkspaceOrganizationKind.Goal,row.Name,row.TaskIds);
        foreach(var row in document.SavedTaskViews??[])
        {
            var f=row.Query.Filter;var ids=new HashSet<Guid>();var offset=0;
            do
            {
                var filter=new WorkspaceTaskFilter(f.Query,f.Kind,f.State,f.Priority,f.ProjectId,f.CategoryId,f.LabelId,f.PlannedFrom,f.PlannedTo,f.DeadlineFrom,f.DeadlineTo,f.ProjectionState,100,offset,document.Revision,f.ExecutionStage);
                var page=row.Query.Source==WorkspaceSavedTaskSource.UnblockedTodos?WorkspaceUnblockedTodos.Evaluate(document,filter):WorkspaceTaskQuery.Evaluate(document,filter);
                foreach(var task in page.Tasks)ids.Add(task.Id);
                if(page.NextOffset is not {} next)break;offset=next;
            }while(offset<=document.Tasks.Count);
            Add(row,WorkspaceOrganizationKind.SavedTaskView,row.Name,ids);
        }
        return groups;
    }
    public static WorkspaceTagCorrespondenceResult Inspect(WorkspaceDocument document,WorkspaceTagDesiredGroup group,WorkspaceTagMapping? mapping)
    {
        var intents=(document.TagLedger?.Intents??[]).Where(row=>row.MappingId==mapping?.Id&&row.Operation==WorkspaceTagOperation.SetMembership).OrderByDescending(row=>row.DesiredRevision).ThenByDescending(row=>row.Revision).ToArray();
        var intent=intents.FirstOrDefault();var projections=document.Projections.ToLookup(row=>row.TaskId);
        var identity=mapping is {Evidence:not null,HabiticaTagId:not null}&&mapping.DesiredName==group.Name&&mapping.IsArchived==group.Entity.IsArchived&&mapping.DesiredRevision>=group.Entity.Revision;
        var present=intent?.Members?.Where(row=>row.Present).ToArray()??[];
        var exact=identity&&intent is not null&&intent.IsArchived==group.Entity.IsArchived&&intent.DesiredRevision>=group.Entity.Revision&&intent.DesiredRevision==mapping!.DesiredRevision&&!(document.TagLedger?.Intents.Any(row=>row.MappingId==mapping.Id&&row.State==WorkspaceTagState.UnknownOutcome)??false)&&intent.HabiticaTagId==mapping!.HabiticaTagId
            &&intents.Count(row=>row.DesiredRevision==intent.DesiredRevision&&row.Revision==intent.Revision)==1&&present.Length==group.TaskIds.Count&&present.All(row=>group.TaskIds.Contains(row.TaskId))
            &&intent.Members is not null&&intent.Members.All(member=>member.HabiticaTaskId is not null&&projections[member.TaskId].Count()==1&&projections[member.TaskId].Single() is {State:ProjectionState.Linked,IsArchived:false} p&&p.HabiticaTaskId==member.HabiticaTaskId);
        var confirmed=exact&&mapping!.State==WorkspaceTagState.Confirmed&&intent is {State:WorkspaceTagState.Confirmed,Evidence:not null}&&intent.Members!.All(member=>member.State==WorkspaceTagState.Confirmed&&member.Evidence is not null);
        return new(identity,exact,confirmed,intent);
    }
}
