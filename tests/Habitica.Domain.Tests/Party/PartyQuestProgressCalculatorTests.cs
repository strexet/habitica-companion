using Habitica.Domain.Party;

namespace Habitica.Domain.Tests.Party;

public sealed class PartyQuestProgressCalculatorTests
{
    [Fact]
    public void Enrich_identifies_member_who_finishes_boss_quest()
    {
        var party = new PartySnapshot(
            DateTimeOffset.Parse("2026-05-20T00:00:00Z"),
            "party-123",
            "Night Owls",
            null,
            2,
            null,
            new[]
            {
                CreateAwaitingMember("alpha", "Alpha", "09:00", 4m),
                CreateAwaitingMember("beta", "Beta", "11:00", 7m)
            });
        var quest = new PartyQuestSnapshot(
            "boss",
            true,
            0m,
            0m,
            2,
            BossHealthRemaining: 10m,
            BossHealthTotal: 100m);

        var enriched = PartyQuestProgressCalculator.Enrich(
            party,
            quest,
            currentUserId: null,
            DateTimeOffset.Parse("2026-05-20T08:00:00Z"),
            TimeZoneInfo.Utc,
            includeStaleMembers: true);

        Assert.NotNull(enriched.CompletionEstimate);
        Assert.True(enriched.CompletionEstimate!.WillCompleteAfterAwaitingCron);
        Assert.Equal("Beta", enriched.CompletionEstimate.FinishingMemberDisplayName);
        Assert.Equal("beta", enriched.CompletionEstimate.FinishingMemberId);
        Assert.Contains("Beta", enriched.CompletionEstimate.Summary);
    }

    private static PartyMemberSnapshot CreateAwaitingMember(
        string memberId,
        string displayName,
        string averageCronTime,
        decimal pendingDamage)
    {
        return new PartyMemberSnapshot(
            memberId,
            displayName,
            LastCronUtc: null,
            DayStartHour: null,
            TimezoneOffsetMinutes: null,
            PartyCronState.NotCronedYet,
            "Not croned yet.",
            CurrentHabiticaDayKey: "2026-05-20",
            CurrentHabiticaDayStartUtc: DateTimeOffset.Parse("2026-05-20T00:00:00Z"),
            AverageCronTime: TimeSpan.Parse(averageCronTime),
            AverageCronSampleCount: 5,
            PendingQuestDamage: pendingDamage,
            ParticipationStatus: PartyQuestParticipationStatus.Accepted);
    }
}
