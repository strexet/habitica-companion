using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Habitica.Domain.Workspace;

namespace Habitica.Domain.Tests.Workspace;

public sealed class RecurrenceCalendarTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Fact]
    public void Neutral_calendar_fixture_matches_browser_domain()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "recurrence-calendar-v1.json")));
        foreach (var test in fixture.RootElement.GetProperty("expansions").EnumerateArray())
        {
            var rule = test.GetProperty("rule").Deserialize<RecurrenceRule>(Options);
            var from = DateOnly.Parse(test.GetProperty("from").GetString()!, CultureInfo.InvariantCulture);
            var through = DateOnly.Parse(test.GetProperty("through").GetString()!, CultureInfo.InvariantCulture);
            var result = RecurrenceCalendar.Expand(rule, from, through);
            Assert.True(test.GetProperty("failure").GetString() == result.Failure, test.GetProperty("name").GetString());
            Assert.Equal(test.GetProperty("dates").EnumerateArray().Select(value => value.GetString()),
                result.Dates.Select(value => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }
        foreach (var test in fixture.RootElement.GetProperty("localDates").EnumerateArray())
        {
            var instant = DateTimeOffset.Parse(test.GetProperty("instantUtc").GetString()!, CultureInfo.InvariantCulture);
            var result = RecurrenceCalendar.LocalDateAt(instant, test.GetProperty("timeZoneId").GetString()!);
            Assert.True(test.GetProperty("failure").GetString() == result.Failure, test.GetProperty("name").GetString());
            Assert.Equal(test.GetProperty("date").GetString(), result.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
    }

    [Fact]
    public void Inclusive_limit_accepts_leap_year_and_rejects_one_additional_day()
    {
        var start = new DateOnly(2024, 1, 1);
        var rule = new RecurrenceRule(RecurrenceFrequency.Daily, 1, start, "UTC", [], []);
        Assert.Equal(366, RecurrenceCalendar.Expand(rule, start, new(2024, 12, 31)).Dates.Count);
        var rejected = RecurrenceCalendar.Expand(rule, start, new(2025, 1, 1));
        Assert.Equal("invalid_range", rejected.Failure); Assert.Empty(rejected.Dates);
    }

    [Fact]
    public void Malformed_rules_fail_without_partial_dates_or_rule_mutation()
    {
        var start = new DateOnly(2026, 10, 17);
        var rule = new RecurrenceRule(RecurrenceFrequency.Weekly, 1, start, "UTC", [DayOfWeek.Monday], []);
        foreach (var invalid in new[] { rule with { Interval = 367 }, rule with { Frequency = (RecurrenceFrequency)99 }, rule with { Weekdays = null! }, rule with { DaysOfMonth = [1] } })
        {
            var result = RecurrenceCalendar.Expand(invalid, start, start.AddDays(7));
            Assert.Equal("invalid_recurrence", result.Failure); Assert.Empty(result.Dates);
        }
        Assert.Single(rule.Weekdays); Assert.Empty(rule.DaysOfMonth); Assert.Equal(1, rule.Interval);
    }
}
