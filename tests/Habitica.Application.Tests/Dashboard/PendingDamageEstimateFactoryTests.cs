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
                    new TaskSnapshot("daily-1", "Exercise", TaskType.Daily, false, 1.5m, null, null),
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
        Assert.Equal(PendingDamageRisk.Info, estimate.Risk);
        Assert.Contains(estimate.IncludedSources, source => source.Label == "Incomplete Dailies" && source.Damage == 3m);
        Assert.Contains(estimate.IncludedSources, source => source.Label == "Party boss pending damage" && source.Damage == 4m);
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
                    new TaskSnapshot("daily-1", "Exercise", TaskType.Daily, false, 1.5m, null, null)
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
    public void GetIncompleteDailies_keeps_unknown_due_state_conservative()
    {
        var tasks = new TaskCollectionSnapshot(
            DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
            new[]
            {
                new TaskSnapshot("daily-unknown", "Legacy daily", TaskType.Daily, false, 1m, null, null),
                new TaskSnapshot("daily-not-due", "Weekly review", TaskType.Daily, false, 1m, null, null, IsDue: false)
            });

        var dailies = PendingDamageEstimateFactory.GetIncompleteDailies(tasks);

        Assert.Equal("daily-unknown", Assert.Single(dailies).Id);
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
