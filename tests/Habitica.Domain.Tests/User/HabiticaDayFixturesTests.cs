using System.Text.Json;
using Habitica.Domain.User;

namespace Habitica.Domain.Tests.User;

public sealed class HabiticaDayFixturesTests
{
    [Fact]
    public void Fixed_offset_day_boundaries_match_the_neutral_gateway_fixture()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "cron-day-v1.json")));
        foreach (var row in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var now = DateTimeOffset.Parse(row.GetProperty("nowUtc").GetString()!);
            var dayStart = row.GetProperty("dayStart").GetInt32();
            var offset = row.GetProperty("timezoneOffset").GetInt32();
            Assert.Equal(row.GetProperty("day").GetString(), HabiticaDayCalculator.ComputeDayKey(now, dayStart, offset));
            Assert.Equal(DateTimeOffset.Parse(row.GetProperty("dayStartUtc").GetString()!), HabiticaDayCalculator.ComputeCurrentDayStartUtc(now, dayStart, offset));
        }
    }
}
