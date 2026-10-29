using Habitica.Domain.Workspace;
namespace Habitica.WebApp.State;
public sealed record HistoryRecoverySelection(Guid TaskId,Guid? CompletionId=null,Guid? OccurrenceId=null);
public static class TaskHistoryRecovery
{
    public static string? NativeReason(WorkspaceJournalSnapshot snapshot,HistoryRecoverySelection selected)
    {
        var document=snapshot.Document;var task=document.Tasks.FirstOrDefault(row=>row.Id==selected.TaskId);
        if(task is null)return "The exact task is unavailable.";
        if(WorkspaceTaskRecovery.HasGameFence(document,task.Id))return "Habitica-linked or uncertain records cannot use native unarchive or occurrence correction. Linked completions require the separate Habitica uncheck review.";
        if(snapshot.Commands.Any(row=>row.Effects.Count>0&&row.ProjectionState is not (ProjectionCommandState.Confirmed or ProjectionCommandState.Cancelled)))return "Pending or unknown game effects must be resolved before native recovery.";
        if(task.IsArchived)return new WorkspaceTaskUnarchive(task.Revision).ReferencesAvailable(document,task)?null:"Archived or missing organization/dependency references require explicit repair before unarchive. No references are silently removed.";
        if(task.Kind!=CompanionTaskKind.Daily)return "Native Todo restoration uses the exact completion action in history; Habit inverse scoring is unsupported.";
        var occurrence=Occurrence(snapshot,selected);
        if(occurrence is null||occurrence.IsArchived||occurrence.State==OccurrenceState.Pending)return "The exact retained terminal occurrence is unavailable or already pending.";
        var source=Source(snapshot,occurrence);
        return source is null||source.Effects.Count!=0?"The original zero-game-effect occurrence command is unavailable; no correction is inferred.":null;
    }
    public static TaskOccurrence? Occurrence(WorkspaceJournalSnapshot snapshot,HistoryRecoverySelection selected)
    {
        var id=selected.OccurrenceId??snapshot.Document.Completions.FirstOrDefault(row=>row.Id==selected.CompletionId&&row.TaskId==selected.TaskId)?.OccurrenceId;
        return snapshot.Document.Occurrences.FirstOrDefault(row=>row.Id==id&&row.TaskId==selected.TaskId);
    }
    public static WorkspaceCommandEntry? Source(WorkspaceJournalSnapshot snapshot,TaskOccurrence occurrence)=>snapshot.Commands.FirstOrDefault(row=>row.Command.TaskId==occurrence.TaskId&&row.Command.ExpectedRevision+1==occurrence.Revision
        &&row.Command.OccurrenceDate==occurrence.Date&&row.Command.Operation==(occurrence.State==OccurrenceState.Completed?WorkspaceOperation.CompleteTask:WorkspaceOperation.SkipOccurrence)
        &&(occurrence.TerminalCommandId is null||row.Command.Id==occurrence.TerminalCommandId));
    public static bool LinkedCandidate(WorkspaceDocument document,HistoryRecoverySelection selected)=>selected.CompletionId is {} completionId&&document.Tasks.Any(row=>row.Id==selected.TaskId&&!row.IsArchived&&row.Kind is CompanionTaskKind.Todo or CompanionTaskKind.Daily)
        &&document.Completions.Any(row=>row.Id==completionId&&row.TaskId==selected.TaskId&&!row.IsArchived)
        &&document.Restorations?.Any(row=>row.CompletionId==completionId)!=true&&document.OccurrenceCorrections?.Any(row=>row.TargetCommandId==document.Completions.First(value=>value.Id==completionId).CommandId)!=true
        &&document.Projections.Any(row=>row.TaskId==selected.TaskId&&!row.IsArchived&&row.State==ProjectionState.Linked&&row.HabiticaTaskId is not null);
}
