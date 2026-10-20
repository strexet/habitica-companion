using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

public enum WorkspacePlanningList { Inbox, Anytime, Overdue }

/// <summary>Planning membership is independent of Todo execution stage and game schedules.</summary>
public static class WorkspacePlanningQuery
{
    public static WorkspaceTaskPage Evaluate(WorkspaceDocument document, WorkspacePlanningList list, DateOnly day,
        int limit = 20, int offset = 0, long? expectedRevision = null)
    {
        if (!Enum.IsDefined(list) || day == default) throw new ArgumentException("invalid_filter");
        var removedProjects = document.Projects.Where(project => project.IsRemoved).Select(project => project.Id).ToHashSet();
        return WorkspaceTaskQuery.Evaluate(document,
            new(Kind: CompanionTaskKind.Todo, State: CompanionTaskState.Active, Limit: limit, Offset: offset, ExpectedRevision: expectedRevision),
            task => list switch
            {
                WorkspacePlanningList.Inbox => task.PlannedDate is null && (task.ProjectId is null || removedProjects.Contains(task.ProjectId.Value)),
                WorkspacePlanningList.Anytime => task.PlannedDate is null,
                _ => task.PlannedDate < day || task.Deadline < day
            });
    }
}
