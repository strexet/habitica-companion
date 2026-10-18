using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

public sealed record WorkspaceHabitHistoryFilter(string? Query = null, Guid? TaskId = null, HabitScoreDirection? Direction = null,
    DateTimeOffset? RecordedFromUtc = null, DateTimeOffset? RecordedThroughUtc = null,
    int Limit = 50, int Offset = 0, long? ExpectedRevision = null);
public sealed record WorkspaceHabitHistoryItem(Guid EventId, Guid TaskId, Guid CommandId, HabitScoreDirection Direction,
    string TaskText, string? Notes, string DetailSource, DateTimeOffset RecordedAtUtc, bool TaskArchived, bool EventArchived);

public static class WorkspaceHabitHistory
{
    public static WorkspaceReadPage<WorkspaceHabitHistoryItem> Evaluate(WorkspaceJournalSnapshot snapshot, WorkspaceHabitHistoryFilter filter)
    {
        if (filter.Query?.Length > 256 || filter.TaskId == Guid.Empty || filter.Direction is { } direction && !Enum.IsDefined(direction)
            || filter.Limit is < 1 or > 100 || filter.Offset is < 0 or > 100000 || filter.ExpectedRevision is < 1 or > WorkspaceDocument.MaxRevision
            || filter.Offset > 0 && filter.ExpectedRevision is null || !Utc(filter.RecordedFromUtc) || !Utc(filter.RecordedThroughUtc)
            || filter.RecordedFromUtc is not null && filter.RecordedThroughUtc is not null && filter.RecordedFromUtc > filter.RecordedThroughUtc)
            throw new ArgumentException("invalid_filter");
        var document = snapshot.Document;
        if (filter.ExpectedRevision is { } revision && revision != document.Revision) throw new InvalidOperationException("stale_revision");
        if (WorkspaceRules.Validate(document).Count != 0) throw new InvalidOperationException("history_unavailable");
        var tasks = document.Tasks.ToDictionary(task => task.Id); var commands = snapshot.Commands.ToDictionary(entry => entry.Command.Id);
        var query = WorkspaceTaskQuery.Fold(filter.Query ?? ""); var rows = new List<(long Revision,int Order,WorkspaceHabitHistoryItem Item)>();
        for (var index = 0; index < document.HabitScores.Count; index++)
        {
            var value = document.HabitScores[index]; var task = tasks[value.TaskId];
            if (filter.TaskId is { } id && value.TaskId != id || filter.Direction is { } selected && value.Direction != selected
                || filter.RecordedFromUtc is { } from && value.RecordedAtUtc < from || filter.RecordedThroughUtc is { } through && value.RecordedAtUtc > through) continue;
            CompanionTask? accepted = null;
            if (commands.TryGetValue(value.CommandId,out var command))
            {
                if (command.Command.Operation != WorkspaceOperation.ScoreHabit || command.Command.TaskId != task.Id || command.Command.Direction != value.Direction)
                    throw new InvalidOperationException("history_unavailable");
                accepted = command.TaskSnapshot;
                if (accepted is not null && (accepted.Id != task.Id || accepted.Kind != CompanionTaskKind.Habit || string.IsNullOrWhiteSpace(accepted.Text)
                    || accepted.Text.Length > 2048 || accepted.Notes?.Length > 10000)) throw new InvalidOperationException("history_unavailable");
            }
            var text = accepted?.Text ?? task.Text; var notes = accepted is null ? task.Notes : accepted.Notes;
            if (query.Length > 0 && !WorkspaceTaskQuery.Fold(text).Contains(query,StringComparison.Ordinal) && !WorkspaceTaskQuery.Fold(notes ?? "").Contains(query,StringComparison.Ordinal)) continue;
            rows.Add((value.Revision,index,new(value.Id,task.Id,value.CommandId,value.Direction,text,notes,accepted is null ? "currentTask" : "commandSnapshot",value.RecordedAtUtc,task.IsArchived,value.IsArchived)));
        }
        var ordered = rows.OrderByDescending(row => row.Item.RecordedAtUtc).ThenByDescending(row => row.Revision).ThenBy(row => row.Order).ToArray();
        var items = ordered.Skip(filter.Offset).Take(filter.Limit).Select(row => row.Item).ToArray(); var next = filter.Offset + items.Length;
        return new(document.Id,document.Revision,items,rows.Count,next < rows.Count,filter.Offset,next < rows.Count ? next : null);
    }
    private static bool Utc(DateTimeOffset? value) => value is null || value != default(DateTimeOffset) && value.Value.Offset == TimeSpan.Zero;
}
