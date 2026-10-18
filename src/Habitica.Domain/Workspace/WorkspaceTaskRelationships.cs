namespace Habitica.Domain.Workspace;

public sealed record WorkspaceTaskRelationships(Guid? ParentTaskId = null, bool ClearParentTask = false, IReadOnlyList<Guid>? DependsOn = null)
{
    public bool IsValid() => this != new WorkspaceTaskRelationships() && ParentTaskId != Guid.Empty
        && !(ClearParentTask && ParentTaskId is not null)
        && (DependsOn is null || DependsOn.Count <= 100 && DependsOn.All(id => id != Guid.Empty) && DependsOn.Distinct().Count() == DependsOn.Count);

    public CompanionTask Apply(CompanionTask task) => task with
    {
        ParentTaskId = ClearParentTask ? null : ParentTaskId ?? task.ParentTaskId,
        DependsOn = DependsOn ?? task.DependsOn
    };

    public bool ReferencesAvailable(WorkspaceDocument document, CompanionTask task)
    {
        bool Available(Guid id) => id != task.Id && document.Tasks.Any(row => row.Id == id && row.Kind == CompanionTaskKind.Todo && !row.IsArchived);
        return task.Kind == CompanionTaskKind.Todo && (ParentTaskId is null || Available(ParentTaskId.Value))
            && (DependsOn is null || DependsOn.All(Available));
    }

    public bool Matches(CompanionTask? task) => task is { Kind: CompanionTaskKind.Todo, IsArchived: false } && IsValid()
        && (ParentTaskId is null || task.ParentTaskId == ParentTaskId) && (!ClearParentTask || task.ParentTaskId is null)
        && (DependsOn is null || task.DependsOn is not null && task.DependsOn.SequenceEqual(DependsOn));
}
