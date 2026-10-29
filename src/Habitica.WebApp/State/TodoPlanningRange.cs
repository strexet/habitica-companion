namespace Habitica.WebApp.State;

public enum TodoPlanningView { Inbox, Anytime, Overdue, Upcoming, Week, Month }
public sealed record TodoPlacementReview(Guid TaskId, TodoWorkspaceSelection Source, long Revision, Habitica.Domain.Workspace.WorkspaceTaskPlanning Planning);
public static class TodoPlanningRange
{
    public static (DateOnly From, DateOnly Through) For(TodoPlanningView view, DateOnly day)
    {
        if (day == default || !Enum.IsDefined(view)) throw new ArgumentException("Choose a valid civil day and planning view.");
        if (view == TodoPlanningView.Month) return (new(day.Year, day.Month, 1), new(day.Year, day.Month, DateTime.DaysInMonth(day.Year, day.Month)));
        if (view == TodoPlanningView.Week)
        {
            var start = Math.Max(1, day.DayNumber - ((int)day.DayOfWeek + 6) % 7);
            return (DateOnly.FromDayNumber(start), DateOnly.FromDayNumber(Math.Min(DateOnly.MaxValue.DayNumber, start + 6)));
        }
        return (day, DateOnly.FromDayNumber(Math.Min(DateOnly.MaxValue.DayNumber, day.DayNumber + 29)));
    }
}
