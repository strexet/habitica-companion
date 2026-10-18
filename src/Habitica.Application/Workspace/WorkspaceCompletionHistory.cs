using Habitica.Domain.Workspace;
using System.Text.Json.Serialization;

namespace Habitica.Application.Workspace;

public sealed record WorkspaceHistoryFilter(string? Query = null, Guid? TaskId = null, CompanionTaskKind? Kind = null,
    DateTimeOffset? CompletedFromUtc = null, DateTimeOffset? CompletedThroughUtc = null,
    int Limit = 50, int Offset = 0, long? ExpectedRevision = null);
public sealed record WorkspaceRestorationDetail(Guid EventId, Guid CommandId, DateTimeOffset RestoredAtUtc, bool IsArchived);
public sealed record WorkspaceCompletionItem(Guid EventId, Guid TaskId, Guid CommandId, CompanionTaskKind Kind,
    string TaskText, string? Notes, string DetailSource, DateTimeOffset CompletedAtUtc,
    Guid? OccurrenceId, DateOnly? OccurrenceDate, bool TaskArchived, bool EventArchived,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] WorkspaceRestorationDetail? Restoration = null);

public static class WorkspaceCompletionHistory
{
    public static WorkspaceReadPage<WorkspaceCompletionItem> Evaluate(WorkspaceJournalSnapshot snapshot, WorkspaceHistoryFilter filter)
    {
        if (filter.Query?.Length > 256 || filter.TaskId == Guid.Empty || filter.Kind is { } kind && kind is not (CompanionTaskKind.Todo or CompanionTaskKind.Daily)
            || filter.Limit is < 1 or > 100 || filter.Offset is < 0 or > 100000 || filter.ExpectedRevision is < 1 or > WorkspaceDocument.MaxRevision
            || filter.Offset > 0 && filter.ExpectedRevision is null || !Utc(filter.CompletedFromUtc) || !Utc(filter.CompletedThroughUtc)
            || filter.CompletedFromUtc is not null && filter.CompletedThroughUtc is not null && filter.CompletedFromUtc > filter.CompletedThroughUtc)
            throw new ArgumentException("invalid_filter");
        var document = snapshot.Document;
        if (filter.ExpectedRevision is { } revision && revision != document.Revision) throw new InvalidOperationException("stale_revision");
        if (WorkspaceRules.Validate(document).Count != 0) throw new InvalidOperationException("history_unavailable");
        var tasks = document.Tasks.ToDictionary(task => task.Id); var occurrences = document.Occurrences.ToDictionary(value => value.Id);
        var restorations = (document.Restorations ?? Array.Empty<TaskRestoration>()).ToDictionary(value => value.CompletionId);
        var commands = snapshot.Commands.ToDictionary(entry => entry.Command.Id); var query = WorkspaceTaskQuery.Fold(filter.Query ?? "");
        var rows = new List<(long Revision,int Order,WorkspaceCompletionItem Item)>();
        for (var index = 0; index < document.Completions.Count; index++)
        {
            var value = document.Completions[index]; var task = tasks[value.TaskId];
            if (filter.TaskId is { } id && value.TaskId != id || filter.Kind is { } k && task.Kind != k
                || filter.CompletedFromUtc is { } from && value.CompletedAtUtc < from || filter.CompletedThroughUtc is { } through && value.CompletedAtUtc > through) continue;
            CompanionTask? accepted = null;
            if (commands.TryGetValue(value.CommandId,out var command))
            {
                if (command.Command.Operation != WorkspaceOperation.CompleteTask || command.Command.TaskId != task.Id) throw new InvalidOperationException("history_unavailable");
                accepted = command.TaskSnapshot;
                if (accepted is not null && (accepted.Id != task.Id || accepted.Kind != task.Kind || string.IsNullOrWhiteSpace(accepted.Text)
                    || accepted.Text.Length > 2048 || accepted.Notes?.Length > 10000)) throw new InvalidOperationException("history_unavailable");
            }
            var text = accepted?.Text ?? task.Text; var notes = accepted is null ? task.Notes : accepted.Notes;
            if (query.Length > 0 && !WorkspaceTaskQuery.Fold(text).Contains(query,StringComparison.Ordinal) && !WorkspaceTaskQuery.Fold(notes ?? "").Contains(query,StringComparison.Ordinal)) continue;
            var restoration = restorations.TryGetValue(value.Id,out var restored) ? new WorkspaceRestorationDetail(restored.Id,restored.CommandId,restored.RestoredAtUtc,restored.IsArchived) : null;
            rows.Add((value.Revision,index,new(value.Id,task.Id,value.CommandId,task.Kind,text,notes,accepted is null ? "currentTask" : "commandSnapshot",
                value.CompletedAtUtc,value.OccurrenceId,value.OccurrenceId is { } occurrenceId ? occurrences[occurrenceId].Date : null,task.IsArchived,value.IsArchived,restoration)));
        }
        var ordered = rows.OrderByDescending(row => row.Item.CompletedAtUtc).ThenByDescending(row => row.Revision).ThenBy(row => row.Order).ToArray();
        var items = ordered.Skip(filter.Offset).Take(filter.Limit).Select(row => row.Item).ToArray(); var next = filter.Offset + items.Length;
        return new(document.Id,document.Revision,items,rows.Count,next < rows.Count,filter.Offset,next < rows.Count ? next : null);
    }
    private static bool Utc(DateTimeOffset? value) => value is null || value != default(DateTimeOffset) && value.Value.Offset == TimeSpan.Zero;
}
