using Bunit;
using Habitica.Domain.Sync;
using Habitica.Domain.Tasks;
using Habitica.Domain.User;
using Habitica.Rules.Stats;
using Habitica.WebApp.Pages;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Pages;

public sealed class DashboardPageTests : BunitContext
{
    [Fact]
    public void Renders_cached_user_snapshot_cards_and_freshness_state()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new CharacterStatsViewModelFactory());
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: new TaskCollectionSnapshot(
                    DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                    new[]
                    {
                        new TaskSnapshot("todo-1", "Buy milk", TaskType.Todo, false, 1.5m, null, null),
                        new TaskSnapshot("daily-1", "Exercise", TaskType.Daily, false, 1m, null, null)
                    }),
                ClassName: "wizard",
                Level: 15,
                UserSnapshot: new UserSnapshot(
                    DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                    "Mage Tester",
                    "wizard",
                    15,
                    42.5m,
                    50m,
                    33.5m,
                    40m,
                    125.1m,
                    74.9m,
                    88.25m,
                    "party-123",
                    "Wolf-Base",
                    "Wolf-Base",
                    new EquipmentSnapshot(
                        new GearSlotsSnapshot("head_wizard_3", "armor_wizard_4", "weapon_wizard_5", "shield_wizard_2", "back_wizard_1"),
                        new GearSlotsSnapshot("head_special_2", "armor_special_2", "weapon_special_2", "shield_special_2", "back_special_2")),
                    new InventorySnapshot(1, 1, 1, 1, 1, 1, new[] { "armor_wizard_4", "head_wizard_3" }),
                    UnallocatedStatPoints: 3,
                    Stats: new CharacterStatsSnapshot(12m, 34m, 18m, 21m),
                    Buffs: new CharacterStatsSnapshot(1m, 2m, 3m, 4m)),
                UserFreshness: SnapshotFreshnessState.Fresh,
                GearCatalogSnapshot: new GearCatalogSnapshot(
                    DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                    new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
                    {
                        ["head_wizard_3"] = new("head_wizard_3", "Wizard Hat", "Head", "wizard", null, new GearStatBlock(0m, 0m, 0m, 5m)),
                        ["armor_wizard_4"] = new("armor_wizard_4", "Wizard Robe", "Armor", "wizard", null, new GearStatBlock(0m, 6m, 0m, 0m)),
                        ["weapon_wizard_5"] = new("weapon_wizard_5", "Wizard Staff", "Weapon", "wizard", null, new GearStatBlock(0m, 10m, 0m, 3m)),
                        ["shield_wizard_2"] = new("shield_wizard_2", "Focus", "Shield", "wizard", null, new GearStatBlock(0m, 0m, 2m, 0m)),
                        ["back_wizard_1"] = new("back_wizard_1", "Cape", "Back", null, null, new GearStatBlock(1m, 1m, 1m, 1m))
                    }))));

        var cut = Render<DashboardPage>();

        Assert.Contains("Account data is up to date", cut.Markup);
        Assert.Contains("Mage Tester", cut.Markup);
        Assert.Contains("Level 15", cut.Markup);
        Assert.Contains("HP", cut.Markup);
        Assert.Contains("Wolf Base", cut.Markup);
        Assert.Contains("Open tasks", cut.Markup);
        Assert.Contains("3 unspent stat points", cut.Markup);
        Assert.Contains("#stats", cut.Markup);
        Assert.Contains("Equipment", cut.Markup);
        Assert.Contains("Effective", cut.Markup);
        Assert.Contains("STR", cut.Markup);
    }

    [Fact]
    public void Does_not_render_zero_stat_targets_when_snapshot_does_not_include_caps()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new CharacterStatsViewModelFactory());
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Strixetus",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: new TaskCollectionSnapshot(
                    DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
                    Array.Empty<TaskSnapshot>()),
                ClassName: "rogue",
                Level: 32,
                UserSnapshot: new UserSnapshot(
                    DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
                    "Strixetus",
                    "rogue",
                    32,
                    50m,
                    0m,
                    171.99m,
                    0m,
                    488.17m,
                    0m,
                    1127.82m,
                    "party-123",
                    "Wolf-Base",
                    "Wolf-Base",
                    new EquipmentSnapshot(
                        new GearSlotsSnapshot("head_rogue_6", "armor_rogue_6", "weapon_rogue_6", "shield_rogue_6", "back_rogue_6"),
                        new GearSlotsSnapshot(null, null, null, null, null)),
                    new InventorySnapshot(1, 1, 1, 1, 1, 1, Array.Empty<string>())),
                UserFreshness: SnapshotFreshnessState.Fresh)));

        var cut = Render<DashboardPage>();

        Assert.DoesNotContain("50 / 0", cut.Markup);
        Assert.DoesNotContain("171.99 / 0", cut.Markup);
        Assert.DoesNotContain("488.17 / 0", cut.Markup);
        Assert.Contains(">50<", cut.Markup);
        Assert.Contains(">171.99<", cut.Markup);
        Assert.Contains("Current XP", cut.Markup);
    }

    [Fact]
    public void Stat_allocation_uses_plus_buttons_and_applies_selected_points()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new CharacterStatsViewModelFactory());
        var controller = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: new TaskCollectionSnapshot(DateTimeOffset.Parse("2026-04-25T08:00:00Z"), Array.Empty<TaskSnapshot>()),
                ClassName: "wizard",
                Level: 15,
                UserSnapshot: new UserSnapshot(
                    DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                    "Mage Tester",
                    "wizard",
                    15,
                    42.5m,
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
                    new InventorySnapshot(0, 0, 0, 0, 0, 0, Array.Empty<string>()),
                    UnallocatedStatPoints: 3,
                    Stats: new CharacterStatsSnapshot(12m, 34m, 18m, 21m),
                    Buffs: CharacterStatsSnapshot.Zero),
                UserFreshness: SnapshotFreshnessState.Fresh));
        Services.AddSingleton<IAppSessionController>(controller);

        var cut = Render<DashboardPage>();

        cut.Find("[data-testid='allocate-int-plus']").Click();
        cut.Find("[data-testid='allocate-int-plus']").Click();
        cut.Find("[data-testid='allocate-per-plus']").Click();
        cut.Find("[data-testid='apply-stat-allocation']").Click();

        Assert.Equal(new StatAllocation(0, 2, 0, 1), controller.StatAllocationCalls.Single());
    }

    [Fact]
    public void Start_new_day_card_requires_confirmation_and_calls_session_controller()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new CharacterStatsViewModelFactory());
        var controller = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: new TaskCollectionSnapshot(DateTimeOffset.Parse("2026-04-25T08:00:00Z"), Array.Empty<TaskSnapshot>()),
                ClassName: "wizard",
                Level: 15,
                UserSnapshot: new UserSnapshot(
                    DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                    "Mage Tester",
                    "wizard",
                    15,
                    42.5m,
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
                    new InventorySnapshot(0, 0, 0, 0, 0, 0, Array.Empty<string>()),
                    CurrentHabiticaDayKey: "2026-04-25",
                    NeedsCron: true),
                UserFreshness: SnapshotFreshnessState.Fresh));
        Services.AddSingleton<IAppSessionController>(controller);

        var cut = Render<DashboardPage>();

        cut.Find("[data-testid='start-new-day']").Click();
        Assert.Contains("Missed Dailies may be processed", cut.Markup);

        cut.Find("[data-testid='confirm-start-new-day']").Click();

        Assert.Equal(1, controller.StartNewDayCalls);
    }
}
