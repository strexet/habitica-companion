using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

public sealed record WorkspaceProjectRemovalImpact(Guid WorkspaceId, long Revision, WorkspaceProject Project,
    WorkspaceProjectRemoval Removal, bool CanRemove);
public static class WorkspaceProjectRemovalQuery
{
    public static WorkspaceProjectRemovalImpact Evaluate(WorkspaceDocument document, Guid projectId, long? expectedRevision = null)
    {
        if(projectId==Guid.Empty||expectedRevision is <1 or >WorkspaceDocument.MaxRevision)throw new ArgumentException("invalid_filter");
        if(expectedRevision is {} revision&&revision!=document.Revision)throw new InvalidOperationException("stale_revision");
        var project=document.Projects.FirstOrDefault(row=>row.Id==projectId)??throw new InvalidOperationException("project_unavailable");
        return new(document.Id,document.Revision,project,WorkspaceProjectRemoval.From(document,projectId),!project.IsRemoved);
    }
}
