using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

public static class WorkspaceUnblockedTodos
{
    public static WorkspaceTaskPage Evaluate(WorkspaceDocument document, WorkspaceTaskFilter filter)
    {
        if (filter.Kind is not null || filter.State is not null) throw new ArgumentException("invalid_filter");
        if (WorkspaceRules.Validate(document).Count != 0) throw new InvalidOperationException("candidates_unavailable");
        var completed = document.Tasks.Where(task => task.State == CompanionTaskState.Completed).Select(task => task.Id).ToHashSet();
        return WorkspaceTaskQuery.Evaluate(document,filter with { Kind=CompanionTaskKind.Todo,State=CompanionTaskState.Active },
            task => (task.DependsOn ?? Array.Empty<Guid>()).All(completed.Contains));
    }
}
