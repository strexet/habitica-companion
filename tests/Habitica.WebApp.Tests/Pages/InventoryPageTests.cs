using Bunit;
using Habitica.Application.Inventory;
using Habitica.Domain.Sync;
using Habitica.Domain.User;
using Habitica.WebApp.Pages;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Pages;

public sealed class InventoryPageTests : BunitContext
{
    [Fact]
    public void Renders_equipment_groups_from_cached_user_snapshot()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new InventoryViewModelFactory());
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: null,
                ClassName: "wizard",
                Level: 15,
                UserSnapshot: new UserSnapshot(
                    DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
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
                    new InventorySnapshot(
                        1,
                        5,
                        1,
                        1,
                        1,
                        1,
                        new[] { "head_wizard_3", "head_special_2", "weapon_wizard_5", "weapon_warrior_6" })),
                UserFreshness: SnapshotFreshnessState.Fresh)));

        var cut = Render<InventoryPage>();

        Assert.Contains("Equipment explorer", cut.Markup);
        Assert.Contains("Head", cut.Markup);
        Assert.Contains("Weapon", cut.Markup);
        Assert.Contains("head_wizard_3", cut.Markup);
        Assert.Contains("Battle equipped", cut.Markup);
        Assert.Contains("Costume equipped", cut.Markup);
    }
}
