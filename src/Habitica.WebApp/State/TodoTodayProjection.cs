using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.WebApp.State;

public enum TodoTodayGroup { Tasks, Dailies, Recorded, Inactive }
public sealed record TodoTodayRow(WorkspaceAgendaItem Item, TodoTodayGroup Group, bool CanAct);
public sealed record TodoTodayPage(TodoWorkspaceSelection Source, long Revision, DateOnly Day,
    IReadOnlyList<TodoTodayRow> Rows, int Total, int Offset, int? NextOffset);

public static class TodoTodayProjection
{
    public static TodoTodayPage Read(TodoWorkspacePresentation todo, DateOnly day, int offset = 0)
    {
        var source = todo.Selection ?? throw new InvalidOperationException("Choose a workspace before viewing Today.");
        var revision = todo.Revision ?? throw new InvalidOperationException("Reload available task data before viewing Today.");
        var page = todo.Agenda(new(day, day, Limit: 20, Offset: offset, ExpectedRevision: revision));
        var rows = page.Items.Select(item =>
        {
            var recorded = item.OccurrenceState is OccurrenceState.Completed or OccurrenceState.Skipped;
            var inactive = item.TaskState != CompanionTaskState.Active;
            var dueDaily = item.Kind == CompanionTaskKind.Daily && item.OccurrenceState == OccurrenceState.Pending;
            var group = recorded ? TodoTodayGroup.Recorded : inactive ? TodoTodayGroup.Inactive : dueDaily ? TodoTodayGroup.Dailies : TodoTodayGroup.Tasks;
            return new TodoTodayRow(item, group, !inactive && !recorded && (item.Kind != CompanionTaskKind.Daily || dueDaily));
        }).ToArray();
        return new(source, revision, day, rows, page.MatchedCount, page.Offset, page.NextOffset);
    }
}
