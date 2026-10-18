namespace Habitica.Domain.Workspace;

public enum TodoExecutionStage { Inbox, Next, InProgress }
public sealed record WorkspaceTodoStage(TodoExecutionStage? Stage=null,bool ClearStage=false)
{
    public bool IsValid()=>ClearStage ? Stage is null : Stage is {} stage&&Enum.IsDefined(stage);
    public bool Matches(CompanionTask? task)=>IsValid()&&task is {Kind:CompanionTaskKind.Todo,State:CompanionTaskState.Active,IsArchived:false}&&task.ExecutionStage==(ClearStage?null:Stage);
}
