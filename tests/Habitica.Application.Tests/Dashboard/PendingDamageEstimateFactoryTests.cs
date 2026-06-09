using Habitica.Application.Dashboard;
using Habitica.Domain.Dashboard;
using Habitica.Domain.Party;
using Habitica.Domain.Tasks;
using Habitica.Domain.User;

namespace Habitica.Application.Tests.Dashboard;

public sealed class PendingDamageEstimateFactoryTests
{
    [Fact]
    public void Create_includes_incomplete_dailies_and_party_boss_damage()
    {
        var estimate = new PendingDamageEstimateFactory().Create(
            CreateUser(health: 20m),
            new TaskCollectionSnapshot(
                DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                new[]
                {
                    new TaskSnapshot("daily-1", "Exercise", TaskType.Daily, false, 1.5m, null, null, IsDue: true),
                    new TaskSnapshot("daily-2", "Done", TaskType.Daily, true, 2m, null, null),
                    new TaskSnapshot("todo-1", "Buy milk", TaskType.Todo, false, 10m, null, null)
                }),
            new PartySnapshot(
                DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                "party-1",
                "Party",
                null,
                1,
                new PartyQuestSnapshot(
                    "boss",
                    true,
                    0m,
                    4m,
                    1,
                    PendingPartyDamage: 4m,
                    QuestType: PartyQuestType.Boss)));

        Assert.Equal(7m, estimate.TotalDamage);
        Assert.Equal(13m, estimate.EstimatedHealthAfterCron);
        Assert.Equal(PendingDamageRisk.Info, estimate.Risk);
        Assert.Contains(estimate.IncludedSources, source => source.Label == "Due Dailies" && source.Damage == 3m);
        Assert.Contains(estimate.IncludedSources, source => source.Label == "Boss" && source.Damage == 4m);
    }

    [Fact]
    public void Create_marks_danger_when_damage_reaches_current_health()
    {
        var estimate = new PendingDamageEstimateFactory().Create(
            CreateUser(health: 3m),
            new TaskCollectionSnapshot(
                DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                new[]
                {
                    new TaskSnapshot("daily-1", "Exercise", TaskType.Daily, false, 1.5m, null, null, IsDue: true)
                }),
            party: null);

        Assert.Equal(3m, estimate.TotalDamage);
        Assert.Equal(PendingDamageRisk.Danger, estimate.Risk);
    }

    [Fact]
    public void GetIncompleteDailies_excludes_completed_and_not_due_dailies()
    {
        var tasks = new TaskCollectionSnapshot(
            DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
            new[]
            {
                new TaskSnapshot("daily-due", "Exercise", TaskType.Daily, false, 1m, null, null, IsDue: true),
                new TaskSnapshot("daily-not-due", "Weekly review", TaskType.Daily, false, 1m, null, null, IsDue: false),
                new TaskSnapshot("daily-done", "Done", TaskType.Daily, true, 1m, null, null, IsDue: true),
                new TaskSnapshot("todo", "Buy milk", TaskType.Todo, false, 1m, null, null)
            });

        var dailies = PendingDamageEstimateFactory.GetIncompleteDailies(tasks);

        Assert.Equal("daily-due", Assert.Single(dailies).Id);
    }

    [Fact]
    public void GetIncompleteDailies_excludes_unknown_due_state_from_confirmed_damage()
    {
        var tasks = new TaskCollectionSnapshot(
            DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
            new[]
            {
                new TaskSnapshot("daily-unknown", "Legacy daily", TaskType.Daily, false, 1m, null, null),
                new TaskSnapshot("daily-not-due", "Weekly review", TaskType.Daily, false, 1m, null, null, IsDue: false)
            });

        var dailies = PendingDamageEstimateFactory.GetIncompleteDailies(tasks);

        Assert.Empty(dailies);
        Assert.Equal("daily-unknown", Assert.Single(PendingDamageEstimateFactory.GetUnknownDueIncompleteDailies(tasks)).Id);
    }

    [Fact]
    public void Create_reports_unknown_due_dailies_without_counting_them()
    {
        var estimate = new PendingDamageEstimateFactory().Create(
            CreateUser(health: 20m),
            new TaskCollectionSnapshot(
                DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                new[]
                {
                    new TaskSnapshot("daily-unknown", "Legacy daily", TaskType.Daily, false, 3m, null, null)
                }),
            party: null);

        Assert.Equal(0m, estimate.TotalDamage);
        Assert.Equal(PendingDamageReadiness.Incomplete, estimate.Readiness);
        Assert.Contains(estimate.ExcludedSources, source => source.Contains("unknown due state", StringComparison.Ordinal));
    }

    [Fact]
    public void Create_excludes_boss_damage_when_current_user_is_not_participating()
    {
        var estimate = new PendingDamageEstimateFactory().Create(
            CreateUser(health: 20m),
            new TaskCollectionSnapshot(DateTimeOffset.Parse("2026-04-25T08:00:00Z"), Array.Empty<TaskSnapshot>()),
            new PartySnapshot(
                DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                "party-1",
                "Party",
                null,
                1,
                new PartyQuestSnapshot(
                    "boss",
                    true,
                    0m,
                    6m,
                    1,
                    PendingPartyDamage: 6m,
                    QuestType: PartyQuestType.Boss),
                new[]
                {
                    new PartyMemberSnapshot("user-id", "Mage Tester", null, null, null, PartyCronState.Unknown, "Unknown.", null, null, ParticipationStatus: PartyQuestParticipationStatus.Rejected)
                }),
            currentUserId: "user-id");

        Assert.Equal(0m, estimate.TotalDamage);
        Assert.Contains(estimate.ExcludedSources, source => source.Contains("not an active quest participant", StringComparison.Ordinal));
    }

    private static UserSnapshot CreateUser(decimal health)
    {
        return new UserSnapshot(
            DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
            "Mage Tester",
            "wizard",
            15,
            health,
            50m,
            33.5m,
            40m,
            125.1m,
            74.9m,
            88.25m,
            "party-123",
            null,
            null,
            new EquipmentSnapshot(
                new GearSlotsSnapshot(null, null, null, null, null),
                new GearSlotsSnapshot(null, null, null, null, null)),
            new InventorySnapshot(0, 0, 0, 0, 0, 0, Array.Empty<string>()));
    }
}
