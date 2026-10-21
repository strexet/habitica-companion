namespace Habitica.Domain.Workspace;

public sealed record DailyAccountSchedule(int TimezoneOffset, int DayStart);
public sealed record DailyProjectionSchedule(RecurrenceFrequency Frequency, int EveryX, DateTimeOffset StartDate,
    IReadOnlyDictionary<string, bool> Repeat, IReadOnlyList<int> DaysOfMonth, IReadOnlyList<int> WeeksOfMonth)
{
    public static string? RuleFailure(RecurrenceRule? rule)
    {
        if (rule is null) return "daily_schedule_required";
        if (!RecurrenceCalendar.Expand(rule, rule.StartDate, rule.StartDate).Succeeded) return "invalid_recurrence";
        if (FixedOffset(rule.TimeZoneId) is null) return "daily_timezone_unrepresentable";
        if (rule.Frequency == RecurrenceFrequency.Weekly && rule.Interval > 1 && rule.StartDate.DayOfWeek != DayOfWeek.Monday)
            return "daily_week_anchor_unrepresentable";
        return null;
    }
    public static (DailyProjectionSchedule? Schedule, string? Failure) Normalize(RecurrenceRule? rule, DailyAccountSchedule account)
    {
        if (RuleFailure(rule) is { } failure) return (null, failure);
        if (account.DayStart != 0 || account.TimezoneOffset is < -840 or > 720) return (null, "daily_account_day_unrepresentable");
        if (FixedOffset(rule!.TimeZoneId) != account.TimezoneOffset) return (null, "daily_timezone_mismatch");
        var start = new DateTimeOffset(rule.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)).AddMinutes(account.TimezoneOffset);
        string[] keys = ["su", "m", "t", "w", "th", "f", "s"];
        var repeat = Enumerable.Range(0, 7).ToDictionary(i => keys[i], i => rule.Frequency == RecurrenceFrequency.Weekly && rule.Weekdays.Contains((DayOfWeek)i));
        return (new(rule.Frequency, rule.Interval, start, repeat, rule.DaysOfMonth.Order().ToArray(), []), null);
    }
    private static int? FixedOffset(string zone)
    {
        if (zone is "UTC" or "Etc/GMT") return 0;
        if (!zone.StartsWith("Etc/GMT", StringComparison.Ordinal) || zone.Length is < 9 or > 10 || zone[7] is not ('+' or '-')) return null;
        var digits = zone.AsSpan(8);
        if (digits[0] == '0' || !int.TryParse(digits, out var hour) || hour is < 1 or > 14) return null;
        return (zone[7] == '+' ? 1 : -1) * hour * 60;
    }
}
