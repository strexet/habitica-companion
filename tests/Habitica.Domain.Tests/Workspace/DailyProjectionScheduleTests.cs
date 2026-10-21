using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Habitica.Domain.Workspace;
namespace Habitica.Domain.Tests.Workspace;
public sealed class DailyProjectionScheduleTests
{
    [Fact]
    public void Neutral_schedules_match_worker_dates_offsets_and_refusals()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","daily-projection-v1.json")));
        foreach (var test in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var rule=test.GetProperty("rule").Deserialize<RecurrenceRule>(options);
            var account=test.GetProperty("account").Deserialize<DailyAccountSchedule>(options)!;
            var result=DailyProjectionSchedule.Normalize(rule,account);
            Assert.True(test.GetProperty("failure").GetString()==result.Failure,test.GetProperty("name").GetString());
            if(result.Schedule is not { } schedule) continue;
            var expected=test.GetProperty("schedule");
            Assert.Equal(expected.GetProperty("startDate").GetString(),schedule.StartDate.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'",CultureInfo.InvariantCulture));
            Assert.Equal(expected.GetProperty("frequency").GetString(),schedule.Frequency.ToString().ToLowerInvariant());
            Assert.Equal(expected.GetProperty("everyX").GetInt32(),schedule.EveryX);
            Assert.Equal(expected.GetProperty("repeat").EnumerateObject().Select(p=>p.Value.GetBoolean()),schedule.Repeat.Values);
            Assert.Equal(expected.GetProperty("daysOfMonth").EnumerateArray().Select(p=>p.GetInt32()),schedule.DaysOfMonth);
            Assert.Empty(schedule.WeeksOfMonth);
            var dates=RecurrenceCalendar.Expand(rule,DateOnly.Parse(test.GetProperty("from").GetString()!),DateOnly.Parse(test.GetProperty("through").GetString()!));
            Assert.Equal(test.GetProperty("dates").EnumerateArray().Select(d=>d.GetString()),dates.Dates.Select(d=>d.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)));
        }
    }
}
