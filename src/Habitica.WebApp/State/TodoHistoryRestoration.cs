using Habitica.Domain.Workspace;

namespace Habitica.WebApp.State;

/// <summary>Presentation availability only; the command engine revalidates every acceptance.</summary>
public static class TodoHistoryRestoration
{
    public static string? UnavailableReason(WorkspaceDocument document, Guid taskId, Guid completionId)
    {
        var task = document.Tasks.FirstOrDefault(row => row.Id == taskId);
        if (task is null || task.IsArchived) return "Archived or unavailable task; restoration is unavailable here.";
        if (task.Kind != CompanionTaskKind.Todo) return "Daily and Habit undo is unavailable here.";
        if (document.Restorations?.Any(row => row.CompletionId == completionId) == true) return "This completion has already been restored.";
        if (task.State != CompanionTaskState.Completed) return "The task is not currently completed.";
        var mapping = document.Projections.FirstOrDefault(row => row.TaskId == taskId);
        if (mapping is not null && (mapping.State != ProjectionState.Unlinked || mapping.HabiticaTaskId is not null)) return "Habitica-linked or uncertain records require separate reconciliation; restoration is unavailable here.";
        return document.Completions.Any(row => row.Id == completionId && row.TaskId == taskId && row.OccurrenceId is null)
            ? null : "The exact completion is unavailable.";
    }
}
