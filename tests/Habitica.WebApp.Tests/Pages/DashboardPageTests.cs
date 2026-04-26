using Bunit;
using Habitica.Domain.Sync;
using Habitica.Domain.Tasks;
using Habitica.Domain.User;
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
                    new InventorySnapshot(1, 1, 1, 1, 1, 1, new[] { "armor_wizard_4", "head_wizard_3" })),
                UserFreshness: SnapshotFreshnessState.Fresh)));

        var cut = Render<DashboardPage>();

        Assert.Contains("Fresh local account snapshot", cut.Markup);
        Assert.Contains("Mage Tester", cut.Markup);
        Assert.Contains("Level 15", cut.Markup);
        Assert.Contains("HP", cut.Markup);
        Assert.Contains("Wolf-Base", cut.Markup);
        Assert.Contains("Open tasks", cut.Markup);
    }

    [Fact]
    public void Does_not_render_zero_stat_targets_when_snapshot_does_not_include_caps()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
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
}
