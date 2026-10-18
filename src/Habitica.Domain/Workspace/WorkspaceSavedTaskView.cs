using System.Text.Json.Serialization;

namespace Habitica.Domain.Workspace;

public enum WorkspaceSavedTaskSource { Tasks, UnblockedTodos }
public sealed record WorkspaceSavedTaskPredicates(string? Query = null, CompanionTaskKind? Kind = null,
    CompanionTaskState? State = null, CompanionPriority? Priority = null, Guid? ProjectId = null,
    Guid? CategoryId = null, Guid? LabelId = null, DateOnly? PlannedFrom = null, DateOnly? PlannedTo = null,
    DateOnly? DeadlineFrom = null, DateOnly? DeadlineTo = null, ProjectionState? ProjectionState = null,
    TodoExecutionStage? ExecutionStage = null)
{
    public bool IsValid() => Query?.Length is not >256 && (Kind is null || Enum.IsDefined(Kind.Value))
        && (State is null || Enum.IsDefined(State.Value)) && (Priority is null || Enum.IsDefined(Priority.Value))
        && (ProjectionState is null || Enum.IsDefined(ProjectionState.Value))
        && ProjectId != Guid.Empty && CategoryId != Guid.Empty && LabelId != Guid.Empty
        && ValidRange(PlannedFrom,PlannedTo) && ValidRange(DeadlineFrom,DeadlineTo)
        && (ExecutionStage is null || Enum.IsDefined(ExecutionStage.Value)
            && Kind is null or CompanionTaskKind.Todo && State is null or CompanionTaskState.Active);
    private static bool ValidRange(DateOnly? from,DateOnly? to) => from != DateOnly.MinValue && to != DateOnly.MinValue && (from is null || to is null || from<=to);
}
public sealed record WorkspaceSavedTaskQuery([property: JsonRequired] WorkspaceSavedTaskSource Source,
    [property: JsonRequired] WorkspaceSavedTaskPredicates Filter)
{
    public bool IsValid() => Enum.IsDefined(Source) && Filter is not null && Filter.IsValid()
        && (Source != WorkspaceSavedTaskSource.UnblockedTodos || Filter.Kind is null && Filter.State is null);
}
public sealed record WorkspaceSavedTaskView(Guid Id,long Revision,[property: JsonRequired] string Name,
    [property: JsonRequired] WorkspaceSavedTaskQuery Query,bool IsArchived=false) : WorkspaceEntity(Id,Revision,IsArchived);
