using Habitica.Domain.Workspace;
namespace Habitica.Application.Workspace;

public sealed record WorkspaceHistoryProjectPlacement(Guid? ProjectId,string? ProjectName,bool ProjectArchived,bool ProjectRemoved,bool Unassigned,
    string Basis="currentRetainedTaskAssignment");

internal sealed class WorkspaceHistoryProjects
{
    private readonly IReadOnlyDictionary<Guid,WorkspaceProject> _projects;
    private readonly Guid? _selected;
    private readonly bool _unassigned;
    public bool Active=>_selected is not null||_unassigned;
    public WorkspaceHistoryProjects(WorkspaceDocument document,Guid? selected,bool unassigned)
    {
        if(selected==Guid.Empty||selected is not null&&unassigned)throw new ArgumentException("invalid_filter");
        _projects=document.Projects.ToDictionary(row=>row.Id);_selected=selected;_unassigned=unassigned;
        if(selected is {} id&&!_projects.ContainsKey(id))throw new InvalidOperationException("history_project_not_found");
    }
    public bool Matches(CompanionTask task)=>_selected is {} id?task.ProjectId==id:!_unassigned||task.ProjectId is null||_projects[task.ProjectId.Value].IsRemoved;
    public WorkspaceHistoryProjectPlacement? Placement(CompanionTask task)
    {
        if(!Active)return null;
        var project=task.ProjectId is {} id?_projects[id]:null;
        return new(task.ProjectId,project?.Name,project?.IsArchived==true,project?.IsRemoved==true,project is null||project.IsRemoved);
    }
}
