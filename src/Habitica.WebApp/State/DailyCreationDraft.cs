using System.Globalization;
using Habitica.Domain.Workspace;
namespace Habitica.WebApp.State;

public sealed class DailyCreationDraft
{
    public RecurrenceFrequency Frequency { get; set; } = RecurrenceFrequency.Daily;
    public int Interval { get; set; } = 1;
    public string StartDate { get; set; } = "";
    public string TimeZoneId { get; set; } = "UTC";
    public HashSet<DayOfWeek> Weekdays { get; } = [];
    public string MonthDays { get; set; } = "";
    public RecurrenceRule Rule()
    {
        if (TodoTaskDraft.Date(StartDate) is not { } start) throw new InvalidOperationException("Choose an explicit Daily start date.");
        var days = new List<int>();
        if (Frequency == RecurrenceFrequency.Monthly)
            foreach (var value in MonthDays.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var day)) throw new InvalidOperationException("Use comma-separated month days from 1 to 31.");
                days.Add(day);
            }
        var rule = new RecurrenceRule(Frequency, Interval, start, TimeZoneId, Frequency == RecurrenceFrequency.Weekly ? Weekdays.Order().ToArray() : [], days.Order().ToArray());
        if (DailyProjectionSchedule.RuleFailure(rule) is { } failure) throw new InvalidOperationException($"Daily schedule unavailable ({failure}). Use a fixed timezone and a Monday start for multiweek intervals.");
        return rule;
    }
}
