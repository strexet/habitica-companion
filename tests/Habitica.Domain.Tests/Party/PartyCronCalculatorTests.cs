using Habitica.Domain.Party;

namespace Habitica.Domain.Tests.Party;

public sealed class PartyCronCalculatorTests
{
    private static readonly TimeZoneInfo ViewerTimeZone = TimeZoneInfo.CreateCustomTimeZone(
        "ViewerUtc",
        TimeSpan.Zero,
        "Viewer UTC",
        "Viewer UTC");

    [Fact]
    public void ClassifyMember_marks_member_croned_when_last_cron_is_after_member_day_start()
    {
        var member = PartyCronCalculator.ClassifyMember(
            new PartyMemberCronInput(
                "member-1",
                "Alpha",
                DateTimeOffset.Parse("2026-04-26T22:30:00Z"),
                0,
                -120),
            fetchedAtUtc: DateTimeOffset.Parse("2026-04-27T07:00:00Z"),
            nowUtc: DateTimeOffset.Parse("2026-04-27T07:00:00Z"));

        Assert.Equal(PartyCronState.CronedToday, member.CronState);
        Assert.Equal("2026-04-27", member.CurrentHabiticaDayKey);
        Assert.Equal(DateTimeOffset.Parse("2026-04-26T22:00:00Z"), member.CurrentHabiticaDayStartUtc);
    }

    [Fact]
    public void ClassifyMember_marks_member_not_croned_when_snapshot_was_fetched_after_day_start()
    {
        var member = PartyCronCalculator.ClassifyMember(
            new PartyMemberCronInput(
                "member-1",
                "Alpha",
                DateTimeOffset.Parse("2026-04-26T20:00:00Z"),
                2,
                0),
            fetchedAtUtc: DateTimeOffset.Parse("2026-04-27T04:00:00Z"),
            nowUtc: DateTimeOffset.Parse("2026-04-27T04:00:00Z"));

        Assert.Equal(PartyCronState.NotCronedYet, member.CronState);
    }

    [Fact]
    public void ClassifyMember_marks_member_unknown_when_last_cron_is_missing()
    {
        var member = PartyCronCalculator.ClassifyMember(
            new PartyMemberCronInput(
                "member-1",
                "Alpha",
                null,
                null,
                -120),
            fetchedAtUtc: DateTimeOffset.Parse("2026-04-27T07:00:00Z"),
            nowUtc: DateTimeOffset.Parse("2026-04-27T07:00:00Z"));

        Assert.Equal(PartyCronState.Unknown, member.CronState);
        Assert.Contains("CRON", member.CronStateReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClassifyMember_uses_public_cron_timestamp_when_member_time_settings_are_hidden()
    {
        var member = PartyCronCalculator.ClassifyMember(
            new PartyMemberCronInput(
                "member-1",
                "Alpha",
                DateTimeOffset.Parse("2026-04-27T06:00:00Z"),
                null,
                null),
            fetchedAtUtc: DateTimeOffset.Parse("2026-04-27T07:00:00Z"),
            nowUtc: DateTimeOffset.Parse("2026-04-27T07:00:00Z"));

        Assert.Equal(PartyCronState.CronedToday, member.CronState);
        Assert.Equal("2026-04-27", member.CurrentHabiticaDayKey);
        Assert.Equal(DateTimeOffset.Parse("2026-04-27T00:00:00Z"), member.CurrentHabiticaDayStartUtc);
        Assert.Contains("public", member.CronStateReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateHistoryEvents_marks_public_member_time_data_as_low_confidence()
    {
        var party = CreatePartySnapshot(new[]
        {
            new PartyMemberSnapshot(
                "member-1",
                "Alpha",
                DateTimeOffset.Parse("2026-04-27T06:00:00Z"),
                null,
                null,
                PartyCronState.CronedToday,
                "Public CRON timestamp.",
                "2026-04-27",
                DateTimeOffset.Parse("2026-04-27T00:00:00Z"))
        });

        var historyEvents = PartyCronCalculator.CreateHistoryEvents(party);

        Assert.Single(historyEvents);
        Assert.Equal(PartyCronEventConfidence.Low, historyEvents[0].Confidence);
    }

    [Fact]
    public void ClassifyMember_marks_member_possibly_stale_when_fetch_precedes_day_start()
    {
        var member = PartyCronCalculator.ClassifyMember(
            new PartyMemberCronInput(
                "member-1",
                "Alpha",
                DateTimeOffset.Parse("2026-04-26T02:30:00Z"),
                2,
                0),
            fetchedAtUtc: DateTimeOffset.Parse("2026-04-27T01:30:00Z"),
            nowUtc: DateTimeOffset.Parse("2026-04-27T04:00:00Z"));

        Assert.Equal(PartyCronState.PossiblyStale, member.CronState);
    }

    [Fact]
    public void BuildDashboard_starts_statistics_from_first_refresh_and_marks_low_confidence()
    {
        var party = CreatePartySnapshot(new[]
        {
            CreateMember("member-1", "Alpha", "2026-04-27T08:15:00Z", PartyCronState.CronedToday)
        });
        var history = new PartyCronHistorySnapshot(new[]
        {
            new PartyCronHistoryEvent(
                "party-123",
                "member-1",
                "Alpha",
                DateTimeOffset.Parse("2026-04-27T08:15:00Z"),
                "2026-04-27",
                DateTimeOffset.Parse("2026-04-27T08:20:00Z"),
                PartyCronEventConfidence.High)
        });

        var dashboard = PartyCronCalculator.BuildDashboard(
            party,
            history,
            currentUserId: "member-1",
            nowUtc: DateTimeOffset.Parse("2026-04-27T09:00:00Z"),
            viewerTimeZone: ViewerTimeZone);

        Assert.True(dashboard.IsLowConfidence);
        Assert.Equal("Early estimate: based on 1 day of CRON history.", dashboard.SampleSizeWarning);
        Assert.Equal(TimeSpan.Parse("08:15"), dashboard.AverageBestBuffTime);
        Assert.Equal(TimeSpan.Parse("08:15"), dashboard.SelfFirstBuffTime);
        Assert.Single(dashboard.GraphPoints);
        Assert.Equal(1, dashboard.SampleCount);
    }

    [Fact]
    public void BuildDashboard_uses_circular_average_for_member_cron_times_around_midnight()
    {
        var party = CreatePartySnapshot(new[]
        {
            CreateMember("member-1", "Alpha", "2026-04-27T00:10:00Z", PartyCronState.CronedToday)
        });
        var history = new PartyCronHistorySnapshot(new[]
        {
            new PartyCronHistoryEvent("party-123", "member-1", "Alpha", DateTimeOffset.Parse("2026-04-25T23:50:00Z"), "2026-04-25", DateTimeOffset.Parse("2026-04-25T23:55:00Z"), PartyCronEventConfidence.High),
            new PartyCronHistoryEvent("party-123", "member-1", "Alpha", DateTimeOffset.Parse("2026-04-27T00:10:00Z"), "2026-04-27", DateTimeOffset.Parse("2026-04-27T00:15:00Z"), PartyCronEventConfidence.High)
        });

        var dashboard = PartyCronCalculator.BuildDashboard(
            party,
            history,
            currentUserId: "member-1",
            nowUtc: DateTimeOffset.Parse("2026-04-27T01:00:00Z"),
            viewerTimeZone: ViewerTimeZone);

        Assert.Equal(TimeSpan.Zero, dashboard.Members.Single().AverageCronTime);
        Assert.Equal(2, dashboard.Members.Single().AverageCronSampleCount);
    }

    [Fact]
    public void BuildDashboard_buckets_utc_events_into_viewer_local_hours()
    {
        var viewerTimeZone = TimeZoneInfo.CreateCustomTimeZone(
            "ViewerUtcPlus3",
            TimeSpan.FromHours(3),
            "Viewer UTC+3",
            "Viewer UTC+3");
        var party = CreatePartySnapshot(new[]
        {
            CreateMember("member-1", "Alpha", "2026-04-27T05:15:00Z", PartyCronState.CronedToday),
            CreateMember("member-2", "Beta", "2026-04-27T07:45:00Z", PartyCronState.CronedToday)
        });
        var history = new PartyCronHistorySnapshot(new[]
        {
            new PartyCronHistoryEvent("party-123", "member-1", "Alpha", DateTimeOffset.Parse("2026-04-27T05:15:00Z"), "2026-04-27", DateTimeOffset.Parse("2026-04-27T05:20:00Z"), PartyCronEventConfidence.High),
            new PartyCronHistoryEvent("party-123", "member-2", "Beta", DateTimeOffset.Parse("2026-04-27T07:45:00Z"), "2026-04-27", DateTimeOffset.Parse("2026-04-27T07:50:00Z"), PartyCronEventConfidence.High)
        });

        var dashboard = PartyCronCalculator.BuildDashboard(
            party,
            history,
            currentUserId: "member-1",
            nowUtc: DateTimeOffset.Parse("2026-04-27T09:00:00Z"),
            viewerTimeZone: viewerTimeZone);

        Assert.Contains(dashboard.GraphPoints, point => point.Hour == 8 && point.TodayCount == 1);
        Assert.Contains(dashboard.GraphPoints, point => point.Hour == 10 && point.TodayCount == 2);
    }

    [Fact]
    public void BuildDashboard_counts_observed_storage_days_not_cron_event_days()
    {
        var party = CreatePartySnapshot(new[]
        {
            CreateMember("member-1", "Alpha", "2026-04-27T08:15:00Z", PartyCronState.CronedToday),
            CreateMember("member-2", "Beta", "2026-04-26T09:30:00Z", PartyCronState.NotCronedYet),
            CreateMember("member-3", "Gamma", "2026-04-25T10:45:00Z", PartyCronState.NotCronedYet)
        });
        var history = new PartyCronHistorySnapshot(new[]
        {
            new PartyCronHistoryEvent("party-123", "member-1", "Alpha", DateTimeOffset.Parse("2026-04-27T08:15:00Z"), "2026-04-27", DateTimeOffset.Parse("2026-04-27T11:00:00Z"), PartyCronEventConfidence.Low),
            new PartyCronHistoryEvent("party-123", "member-2", "Beta", DateTimeOffset.Parse("2026-04-26T09:30:00Z"), "2026-04-26", DateTimeOffset.Parse("2026-04-27T11:00:00Z"), PartyCronEventConfidence.Low),
            new PartyCronHistoryEvent("party-123", "member-3", "Gamma", DateTimeOffset.Parse("2026-04-25T10:45:00Z"), "2026-04-25", DateTimeOffset.Parse("2026-04-27T11:00:00Z"), PartyCronEventConfidence.Low)
        });

        var dashboard = PartyCronCalculator.BuildDashboard(
            party,
            history,
            currentUserId: "member-1",
            nowUtc: DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
            viewerTimeZone: ViewerTimeZone);

        Assert.Equal(1, dashboard.HistoryDayCount);
        Assert.Equal("Early estimate: based on 1 day of CRON history.", dashboard.SampleSizeWarning);
    }

    [Fact]
    public void BuildDashboard_self_first_waits_for_nearby_members_but_not_far_global_best_time()
    {
        var party = CreatePartySnapshot(new[]
        {
            CreateMember("member-1", "Self", "2026-04-27T08:00:00Z", PartyCronState.CronedToday),
            CreateMember("member-2", "Near One", "2026-04-27T08:20:00Z", PartyCronState.CronedToday),
            CreateMember("member-3", "Near Two", "2026-04-27T08:45:00Z", PartyCronState.CronedToday),
            CreateMember("member-4", "Far", "2026-04-27T16:00:00Z", PartyCronState.CronedToday)
        });
        var history = new PartyCronHistorySnapshot(new[]
        {
            new PartyCronHistoryEvent("party-123", "member-1", "Self", DateTimeOffset.Parse("2026-04-27T08:00:00Z"), "2026-04-27", DateTimeOffset.Parse("2026-04-27T16:05:00Z"), PartyCronEventConfidence.High),
            new PartyCronHistoryEvent("party-123", "member-2", "Near One", DateTimeOffset.Parse("2026-04-27T08:20:00Z"), "2026-04-27", DateTimeOffset.Parse("2026-04-27T16:05:00Z"), PartyCronEventConfidence.High),
            new PartyCronHistoryEvent("party-123", "member-3", "Near Two", DateTimeOffset.Parse("2026-04-27T08:45:00Z"), "2026-04-27", DateTimeOffset.Parse("2026-04-27T16:05:00Z"), PartyCronEventConfidence.High),
            new PartyCronHistoryEvent("party-123", "member-4", "Far", DateTimeOffset.Parse("2026-04-27T16:00:00Z"), "2026-04-27", DateTimeOffset.Parse("2026-04-27T16:05:00Z"), PartyCronEventConfidence.High)
        });

        var dashboard = PartyCronCalculator.BuildDashboard(
            party,
            history,
            currentUserId: "member-1",
            nowUtc: DateTimeOffset.Parse("2026-04-27T16:05:00Z"),
            viewerTimeZone: ViewerTimeZone);

        Assert.Equal(TimeSpan.Parse("16:00"), dashboard.AverageBestBuffTime);
        Assert.Equal(TimeSpan.Parse("08:45"), dashboard.SelfFirstBuffTime);
    }

    private static PartySnapshot CreatePartySnapshot(IReadOnlyList<PartyMemberSnapshot> members)
    {
        return new PartySnapshot(
            DateTimeOffset.Parse("2026-04-27T09:00:00Z"),
            "party-123",
            "Night Owls",
            "Quest-focused party",
            members.Count,
            null,
            members);
    }

    private static PartyMemberSnapshot CreateMember(string id, string name, string lastCronUtc, PartyCronState state)
    {
        return new PartyMemberSnapshot(
            id,
            name,
            DateTimeOffset.Parse(lastCronUtc),
            0,
            0,
            state,
            "test",
            "2026-04-27",
            DateTimeOffset.Parse("2026-04-27T00:00:00Z"));
    }
}
