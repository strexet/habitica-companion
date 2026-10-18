using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

public sealed record WorkspaceAgendaFilter(DateOnly From, DateOnly Through, int Limit = 50, int Offset = 0, long? ExpectedRevision = null);
public sealed record WorkspaceAgendaItem(Guid TaskId, string Text, CompanionTaskKind Kind, CompanionTaskState TaskState,
    DateOnly Date, bool Planned, bool Deadline, OccurrenceState? OccurrenceState, Guid? OccurrenceId, string? RuleTimeZoneId);
public sealed record WorkspaceAgendaPage(Guid WorkspaceId, long Revision, IReadOnlyList<WorkspaceAgendaItem> Items,
    int MatchedCount, bool Truncated, int Offset, int? NextOffset);

public static class WorkspaceAgenda
{
    public const int MaximumRows = 10000;
    public const int MaximumRuleDateEvaluations = 250000;

    public static WorkspaceAgendaPage Evaluate(WorkspaceDocument document, WorkspaceAgendaFilter filter)
    {
        var days = filter.Through.DayNumber - filter.From.DayNumber + 1;
        if (filter.From == default || filter.Through == default || days is < 1 or > RecurrenceCalendar.MaximumRangeDays
            || filter.Limit is < 1 or > 100 || filter.Offset is < 0 or > MaximumRows
            || filter.ExpectedRevision is < 1 or > WorkspaceDocument.MaxRevision || filter.Offset > 0 && filter.ExpectedRevision is null)
            throw new ArgumentException("invalid_filter", nameof(filter));
        if (filter.ExpectedRevision is { } revision && revision != document.Revision) throw new InvalidOperationException("stale_revision");
        var activeRules = document.Tasks.Count(task => !task.IsArchived && task.Kind == CompanionTaskKind.Daily && task.State == CompanionTaskState.Active && task.Recurrence is not null);
        if ((long)activeRules * days > MaximumRuleDateEvaluations) throw new InvalidOperationException("agenda_capacity");
        var occurrences = document.Occurrences.Where(value => value.Date >= filter.From && value.Date <= filter.Through).ToLookup(value => value.TaskId);
        var rows = new List<(int Order, WorkspaceAgendaItem Item)>();
        for (var order = 0; order < document.Tasks.Count; order++)
        {
            var task = document.Tasks[order];
            if (task.IsArchived || task.State is CompanionTaskState.Completed or CompanionTaskState.Cancelled) continue;
            var dates = new SortedDictionary<DateOnly, WorkspaceAgendaItem>();
            var archivedDates = occurrences[task.Id].Where(value => value.IsArchived).Select(value => value.Date).ToHashSet();
            WorkspaceAgendaItem Item(DateOnly date) => dates.TryGetValue(date, out var value) ? value : new(task.Id, task.Text, task.Kind, task.State, date, false, false, null, null, task.Recurrence?.TimeZoneId);
            void ExplicitDate(DateOnly? date, bool planned)
            {
                if (date is not { } value || value < filter.From || value > filter.Through) return;
                var item = Item(value); dates[value] = item with { Planned = item.Planned || planned, Deadline = item.Deadline || !planned };
            }
            ExplicitDate(task.PlannedDate, true); ExplicitDate(task.Deadline, false);
            if (task.Kind == CompanionTaskKind.Daily && task.State == CompanionTaskState.Active && task.Recurrence is { } rule)
            {
                var expansion = RecurrenceCalendar.Expand(rule, filter.From, filter.Through);
                if (!expansion.Succeeded) throw new InvalidOperationException("recurrence_unavailable");
                foreach (var date in expansion.Dates)
                    if (!archivedDates.Contains(date)) dates[date] = Item(date) with { OccurrenceState = OccurrenceState.Pending };
            }
            foreach (var occurrence in occurrences[task.Id])
            {
                if (occurrence.IsArchived) continue;
                if (occurrence.State == OccurrenceState.Pending && (!dates.TryGetValue(occurrence.Date, out var pending) || pending.OccurrenceState is null)) continue;
                dates[occurrence.Date] = Item(occurrence.Date) with { OccurrenceState = occurrence.State, OccurrenceId = occurrence.Id };
            }
            if (rows.Count + dates.Count > MaximumRows) throw new InvalidOperationException("agenda_capacity");
            rows.AddRange(dates.Values.Select(item => (order, item)));
        }
        var ordered = rows.OrderBy(row => row.Item.Date).ThenBy(row => row.Order).ToArray();
        var page = ordered.Skip(filter.Offset).Take(filter.Limit).Select(row => row.Item).ToArray();
        var next = filter.Offset + page.Length;
        return new(document.Id, document.Revision, page, rows.Count, next < rows.Count, filter.Offset, next < rows.Count ? next : null);
    }
}
