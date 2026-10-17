namespace Habitica.Domain.Workspace;

public sealed record RecurrenceExpansion(IReadOnlyList<DateOnly> Dates, string? Failure = null)
{
    public bool Succeeded => Failure is null;
}
public sealed record RecurrenceLocalDate(DateOnly? Date, string? Failure = null);

public static class RecurrenceCalendar
{
    public const int MaximumRangeDays = 366;

    public static RecurrenceExpansion Expand(RecurrenceRule? rule, DateOnly from, DateOnly through)
    {
        RecurrenceExpansion Fail(string code) => new(Array.Empty<DateOnly>(), code);
        if (through < from || through.DayNumber - from.DayNumber + 1 > MaximumRangeDays) return Fail("invalid_range");
        if (rule is null || !Enum.IsDefined(rule.Frequency) || rule.StartDate == default || rule.Interval is < 1 or > 366
            || rule.Weekdays is null || rule.DaysOfMonth is null
            || rule.Weekdays.Any(day => !Enum.IsDefined(day)) || rule.Weekdays.Distinct().Count() != rule.Weekdays.Count
            || rule.DaysOfMonth.Any(day => day is < 1 or > 31) || rule.DaysOfMonth.Distinct().Count() != rule.DaysOfMonth.Count
            || rule.Frequency == RecurrenceFrequency.Weekly && rule.Weekdays.Count == 0
            || rule.Frequency == RecurrenceFrequency.Monthly && rule.DaysOfMonth.Count == 0
            || rule.Frequency != RecurrenceFrequency.Weekly && rule.Weekdays.Count != 0
            || rule.Frequency != RecurrenceFrequency.Monthly && rule.DaysOfMonth.Count != 0)
            return Fail("invalid_recurrence");
        if (Zone(rule.TimeZoneId) is null) return Fail("invalid_time_zone");
        var dates = new List<DateOnly>();
        var monday = rule.StartDate.DayNumber - ((int)rule.StartDate.DayOfWeek + 6) % 7;
        for (var number = Math.Max(from.DayNumber, rule.StartDate.DayNumber); number <= through.DayNumber; number++)
        {
            var date = DateOnly.FromDayNumber(number);
            var due = rule.Frequency switch
            {
                RecurrenceFrequency.Daily => (number - rule.StartDate.DayNumber) % rule.Interval == 0,
                RecurrenceFrequency.Weekly => (number - monday) / 7 % rule.Interval == 0 && rule.Weekdays.Contains(date.DayOfWeek),
                RecurrenceFrequency.Monthly => ((date.Year - rule.StartDate.Year) * 12 + date.Month - rule.StartDate.Month) % rule.Interval == 0 && rule.DaysOfMonth.Contains(date.Day),
                _ => false
            };
            if (due) dates.Add(date);
        }
        return new(dates);
    }

    public static RecurrenceLocalDate LocalDateAt(DateTimeOffset instantUtc, string timeZoneId)
    {
        if (instantUtc.Offset != TimeSpan.Zero) return new(null, "invalid_instant");
        var zone = Zone(timeZoneId);
        if (zone is null) return new(null, "invalid_time_zone");
        var localTicks = instantUtc.Ticks + zone.GetUtcOffset(instantUtc).Ticks;
        if (localTicks < DateTime.MinValue.Ticks || localTicks > DateTime.MaxValue.Ticks) return new(null, "invalid_instant");
        return new(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instantUtc, zone).DateTime));
    }

    private static TimeZoneInfo? Zone(string? id)
    {
        // The portable contract accepts UTC and IANA region-style IDs, not OS-specific aliases.
        if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || id != "UTC" && !id.Contains('/')) return null;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException) { return null; }
    }
}
