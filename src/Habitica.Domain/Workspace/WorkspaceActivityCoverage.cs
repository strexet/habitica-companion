namespace Habitica.Domain.Workspace;

// A witness observes one accepted task transition. Absence of witnesses never proves inactivity.
public sealed record TaskActivityWitness(Guid CommandId, string PayloadHash, Guid TaskId, long Revision,
    long PreviousTaskRevision, DateTimeOffset RecordedAtUtc, CompanionTaskKind Kind, CompanionTaskState State,
    bool IsArchived, RecurrenceRule? Recurrence);

public sealed record TaskActivityInterval(DateTimeOffset FromUtc, DateTimeOffset ThroughUtc, bool IsKnown,
    string? UnknownReason, TaskActivityWitness? Schedule);

public static class WorkspaceActivityCoverage
{
    public static TaskActivityWitness Witness(WorkspaceCommandEntry entry, long previousTaskRevision)
    {
        var task = entry.TaskSnapshot ?? throw new ArgumentException("Accepted task snapshot is required.");
        return new(entry.Command.Id, entry.PayloadHash, task.Id, task.Revision, previousTaskRevision,
            entry.AcceptedAtUtc, task.Kind, task.State, task.IsArchived, task.Recurrence);
    }

    public static bool ValidDocument(WorkspaceDocument document)
    {
        var rows = document.ActivityCoverage;
        if (rows is null) return true;
        if (rows.Count > 100000 || rows.Any(row => row is null)) return false;
        var commands = new HashSet<Guid>(); var revisions = new HashSet<long>();
        var tasks = document.Tasks.Select(task => task.Id).ToHashSet();
        foreach (var row in rows)
            if (row.CommandId == Guid.Empty || !commands.Add(row.CommandId) || !revisions.Add(row.Revision)
                || !tasks.Contains(row.TaskId) || row.Revision < 2 || row.Revision > document.Revision
                || row.PreviousTaskRevision < 0 || row.PreviousTaskRevision >= row.Revision
                || row.RecordedAtUtc == default || row.RecordedAtUtc.Offset != TimeSpan.Zero
                || row.PayloadHash is null || row.PayloadHash.Length != 64 || row.PayloadHash.Any(c => !char.IsAsciiHexDigitLower(c) && !char.IsAsciiDigit(c))
                || !Enum.IsDefined(row.Kind) || !Enum.IsDefined(row.State)
                || row.Kind != CompanionTaskKind.Todo && row.State is CompanionTaskState.Completed or CompanionTaskState.Cancelled
                || row.Recurrence is not null && (row.Kind != CompanionTaskKind.Daily || !RecurrenceCalendar.IsStructurallyValid(row.Recurrence))) return false;
        return true;
    }

    public static bool ValidJournal(WorkspaceJournalSnapshot snapshot)
    {
        if (snapshot.Document.ActivityCoverage is not { Count: > 0 } activityCoverage) return true;
        var entries = snapshot.Commands.ToDictionary(entry => entry.Command.Id);
        var currentTasks = snapshot.Document.Tasks.ToDictionary(task => task.Id);
        var witnessedRevisions = (snapshot.Document.ActivityCoverage ?? []).Select(e => (e.TaskId, e.Revision)).ToHashSet();
        var acceptedEvents = (snapshot.Events ?? []).Where(e => e.Kind == JournalEventKind.Accepted).Select(e => (e.CommandId, e.AtUtc)).ToHashSet();
        var sources = snapshot.Commands.Select(e => (TaskId: e.Command.TaskId, Revision: e.Command.ExpectedRevision + 1))
            .Concat((snapshot.Document.LinkedTaskRecoveries ?? []).Select(e => (e.TaskId, e.Revision)))
            .Concat((snapshot.Document.SourceRetirements ?? []).Select(e => (TaskId: e.SourceTaskId, e.Revision)))
            .Concat(snapshot.Document.Completions.Select(e => (e.TaskId, e.Revision)))
            .Concat(snapshot.Document.HabitScores.Select(e => (e.TaskId, e.Revision)))
            .Concat(snapshot.Document.Occurrences.Select(e => (e.TaskId, e.Revision)))
            .Concat((snapshot.Document.Restorations ?? []).Select(e => (e.TaskId, e.Revision)))
            .Concat((snapshot.Document.Unarchives ?? []).Select(e => (e.TaskId, e.Revision)))
            .Concat((snapshot.Document.OccurrenceCorrections ?? []).Select(e => (e.TaskId, e.Revision)))
            .Concat((snapshot.Document.ReviewEvents ?? []).Select(e => (e.TaskId, e.Revision))).Distinct().OrderBy(e => e.Revision);
        var previousSources = new Dictionary<(Guid, long), long>(); var latest = new Dictionary<Guid, long>();
        foreach (var source in sources) { previousSources[(source.TaskId, source.Revision)] = latest.GetValueOrDefault(source.TaskId); latest[source.TaskId] = source.Revision; }
        var witnesses = activityCoverage.ToDictionary(e => (e.TaskId, e.Revision));
        foreach (var activity in snapshot.Document.Completions.Select(e => (e.TaskId, e.Revision, e.CommandId)).Concat(snapshot.Document.HabitScores.Select(e => (e.TaskId, e.Revision, e.CommandId))))
            if (witnesses.TryGetValue((activity.TaskId, activity.Revision), out var witness) && witness.CommandId != activity.CommandId) return false;
        var completions = snapshot.Document.Completions.ToLookup(e => e.CommandId);
        var scores = snapshot.Document.HabitScores.ToLookup(e => e.CommandId);
        var terminals = snapshot.Document.Occurrences.Concat((snapshot.Document.OccurrenceCorrections ?? []).Select(e => e.PreviousOccurrence)).ToLookup(e => e.TerminalCommandId);
        var taskSnapshots = snapshot.Commands.Where(e => e.TaskSnapshot is not null).ToLookup(e => (e.Command.TaskId, e.Command.ExpectedRevision + 1));
        foreach (var row in snapshot.Document.ActivityCoverage ?? [])
        {
            if (!entries.TryGetValue(row.CommandId, out var entry) || entry.TaskSnapshot is not {} task
                || row.PayloadHash != entry.PayloadHash || row.TaskId != entry.Command.TaskId
                || row.Revision != entry.Command.ExpectedRevision + 1 || row.Revision != task.Revision
                || row.RecordedAtUtc != entry.AcceptedAtUtc || row.Kind != task.Kind || row.State != task.State
                || row.IsArchived != task.IsArchived || !SameRecurrence(row.Recurrence, task.Recurrence)
                || !acceptedEvents.Contains((row.CommandId, row.RecordedAtUtc))) return false;
            var current = currentTasks[row.TaskId];
            if (current.Revision < row.Revision || current.Revision == row.Revision && (current.Kind != row.Kind || current.State != row.State
                || current.IsArchived != row.IsArchived || !SameRecurrence(current.Recurrence, row.Recurrence))) return false;
            var creating = entry.Command.Operation is WorkspaceOperation.CreateTodo or WorkspaceOperation.CreateDaily or WorkspaceOperation.CreateHabit;
            if (creating != (row.PreviousTaskRevision == 0)) return false;
            if (creating && (row.State != CompanionTaskState.Active || row.IsArchived
                || row.Kind != (entry.Command.Operation == WorkspaceOperation.CreateTodo ? CompanionTaskKind.Todo : entry.Command.Operation == WorkspaceOperation.CreateDaily ? CompanionTaskKind.Daily : CompanionTaskKind.Habit)
                || !SameRecurrence(row.Recurrence, entry.Command.Patch?.Recurrence))) return false;
            var continuesWitness = witnessedRevisions.Contains((row.TaskId, row.PreviousTaskRevision));
            var before = continuesWitness ? taskSnapshots[(row.TaskId, row.PreviousTaskRevision)].FirstOrDefault()?.TaskSnapshot : null;
            if (before is not null && (row.Kind != before.Kind
                || !SameRecurrence(row.Recurrence, entry.Command.Operation == WorkspaceOperation.UpdateTask ? entry.Command.Patch!.ClearRecurrence ? null : entry.Command.Patch.Recurrence ?? before.Recurrence : before.Recurrence)
                || row.IsArchived != (entry.Command.Operation == WorkspaceOperation.DeleteTask || entry.Command.Operation != WorkspaceOperation.UnarchiveTask && before.IsArchived)
                || row.State != (entry.Command.Operation switch {
                    WorkspaceOperation.PauseTask => CompanionTaskState.Paused,
                    WorkspaceOperation.ResumeTask or WorkspaceOperation.RestoreTodo => CompanionTaskState.Active,
                    WorkspaceOperation.CompleteTask when row.Kind == CompanionTaskKind.Todo => CompanionTaskState.Completed,
                    WorkspaceOperation.SetTodoState => entry.Command.Patch!.TodoState!.State,
                    _ => before.State }))) return false;
            // A retained intervening source disproves the claimed previous-task link.
            if (continuesWitness && previousSources.GetValueOrDefault((row.TaskId, row.Revision)) > row.PreviousTaskRevision) return false;
            var completionRows = completions[row.CommandId].ToArray(); var scoreRows = scores[row.CommandId].ToArray();
            if (entry.Command.Operation == WorkspaceOperation.SkipOccurrence || entry.Command.Operation == WorkspaceOperation.CompleteTask && row.Kind == CompanionTaskKind.Daily)
            {
                var terminalRows = terminals[row.CommandId].Take(2).ToArray();
                if (terminalRows.Length != 1) return false;
                var terminal = terminalRows[0];
                if (terminal is null || terminal.TaskId != row.TaskId || terminal.Revision != row.Revision || terminal.Date != entry.Command.OccurrenceDate
                    || terminal.State != (entry.Command.Operation == WorkspaceOperation.SkipOccurrence ? OccurrenceState.Skipped : OccurrenceState.Completed)
                    || entry.Command.Operation == WorkspaceOperation.CompleteTask && (completionRows.Length != 1 || completionRows[0].OccurrenceId != terminal.Id)) return false;
            }
            if (completionRows.Length != (entry.Command.Operation == WorkspaceOperation.CompleteTask ? 1 : 0)
                || completionRows.Any(e => e.TaskId != row.TaskId || e.Revision != row.Revision || e.CompletedAtUtc != row.RecordedAtUtc)
                || scoreRows.Length != (entry.Command.Operation == WorkspaceOperation.ScoreHabit ? entry.Command.Count : 0)
                || scoreRows.Any(e => e.TaskId != row.TaskId || e.Revision != row.Revision || e.RecordedAtUtc != row.RecordedAtUtc || e.Direction != entry.Command.Direction)) return false;
        }
        return true;
    }

    public static IReadOnlyList<TaskActivityInterval> Read(WorkspaceJournalSnapshot snapshot, Guid taskId, DateTimeOffset fromUtc, DateTimeOffset throughUtc)
    {
        if (!WorkspaceJournalValidation.IsValid(snapshot)) throw new ArgumentException("Valid full journal is required.");
        if (fromUtc == default || fromUtc.Offset != TimeSpan.Zero || throughUtc.Offset != TimeSpan.Zero || throughUtc <= fromUtc)
            throw new ArgumentException("An increasing UTC interval is required.");
        if (!snapshot.Document.Tasks.Any(task => task.Id == taskId)) throw new ArgumentException("Task is unavailable.");
        var rows = (snapshot.Document.ActivityCoverage ?? []).Where(row => row.TaskId == taskId).OrderBy(row => row.Revision).ToArray();
        if (rows.Zip(rows.Skip(1)).Any(pair => pair.First.RecordedAtUtc > pair.Second.RecordedAtUtc))
            return [new(fromUtc, throughUtc, false, "clock_order_unknown", null)];
        var result = new List<TaskActivityInterval>(); var cursor = fromUtc;
        for (var index = 0; index + 1 < rows.Length; index++)
        {
            var row = rows[index]; var next = rows[index + 1];
            if (next.PreviousTaskRevision != row.Revision || next.RecordedAtUtc <= cursor || row.RecordedAtUtc >= throughUtc) continue;
            var start = row.RecordedAtUtc > cursor ? row.RecordedAtUtc : cursor;
            var end = next.RecordedAtUtc < throughUtc ? next.RecordedAtUtc : throughUtc;
            if (start > cursor) result.Add(new(cursor, start, false, "unproven_history", null));
            if (end > start) { result.Add(new(start, end, true, null, row)); cursor = end; }
        }
        if (cursor < throughUtc) result.Add(new(cursor, throughUtc, false, "unproven_history", null));
        return result;
    }

    private static bool SameRecurrence(RecurrenceRule? a, RecurrenceRule? b) => a is null ? b is null : b is not null
        && a.Frequency == b.Frequency && a.Interval == b.Interval && a.StartDate == b.StartDate && a.TimeZoneId == b.TimeZoneId
        && a.Weekdays.SequenceEqual(b.Weekdays) && a.DaysOfMonth.SequenceEqual(b.DaysOfMonth);
}
