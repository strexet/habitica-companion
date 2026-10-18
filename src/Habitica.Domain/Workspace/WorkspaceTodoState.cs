using System.Text.Json.Serialization;

namespace Habitica.Domain.Workspace;

public sealed record WorkspaceTodoState([property:JsonRequired] CompanionTaskState State)
{
    public bool IsValid() => State is CompanionTaskState.Active or CompanionTaskState.Waiting or CompanionTaskState.Someday or CompanionTaskState.Cancelled;
    public bool Matches(CompanionTask? task) => IsValid() && task is { Kind:CompanionTaskKind.Todo,IsArchived:false } && task.State==State;
}
