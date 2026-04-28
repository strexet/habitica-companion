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
        var controller = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                UserId: "user-id",
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
                UserFreshness: SnapshotFreshnessState.Fresh,
                GearCatalogSnapshot: new GearCatalogSnapshot(
                    DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
                    new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
                    {
                        ["head_wizard_3"] = new("head_wizard_3", "Wizard Hat", "Head", "wizard", null, new GearStatBlock(0m, 2m, 0m, 0m)),
                        ["weapon_wizard_5"] = new("weapon_wizard_5", "Wizard Wand", "Weapon", "wizard", null, new GearStatBlock(0m, 12m, 0m, 2m))
                    }),
                EquipmentPresets: new[]
                {
                    new EquipmentPreset(
                        "preset-1",
                        "user-id",
                        EquipmentSetKind.Battle,
                        "Casting",
                        DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                        new GearSlotsSnapshot("head_wizard_3", null, "weapon_wizard_5", null, null))
                }));
        Services.AddSingleton<IAppSessionController>(controller);

        var cut = Render<InventoryPage>();

        Assert.Contains("Equipment explorer", cut.Markup);
        Assert.Contains("Equipped battle gear", cut.Markup);
        Assert.Contains("Equipped costume", cut.Markup);
        Assert.Contains("Battle gear presets", cut.Markup);
        Assert.DoesNotContain("Costume presets", cut.Markup);
        Assert.Contains("Casting", cut.Markup);
        Assert.Contains("preset-1", cut.Markup);
        Assert.Contains("Wizard Hat", cut.Markup);
        Assert.Contains("Wizard Wand", cut.Markup);
        Assert.Contains("Head", cut.Markup);
        Assert.Contains("Weapon", cut.Markup);
        Assert.Contains("Battle equipped", cut.Markup);
        Assert.Contains("Costume equipped", cut.Markup);
    }

    [Fact]
    public void Inventory_buttons_call_session_controller_actions()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new InventoryViewModelFactory());
        var controller = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                UserId: "user-id",
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: null,
                ClassName: "wizard",
                Level: 15,
                UserSnapshot: CreateSnapshot(),
                UserFreshness: SnapshotFreshnessState.Fresh,
                GearCatalogSnapshot: new GearCatalogSnapshot(
                    DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
                    new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
                    {
                        ["head_wizard_3"] = new("head_wizard_3", "Wizard Hat", "Head", "wizard", null, new GearStatBlock(0m, 2m, 0m, 0m)),
                        ["weapon_wizard_5"] = new("weapon_wizard_5", "Wizard Wand", "Weapon", "wizard", null, new GearStatBlock(0m, 12m, 0m, 2m)),
                        ["weapon_warrior_6"] = new("weapon_warrior_6", "Warrior Sword", "Weapon", "warrior", null, new GearStatBlock(10m, 0m, 1m, 0m))
                    }),
                EquipmentPresets: new[]
                {
                    new EquipmentPreset(
                        "preset-1",
                        "user-id",
                        EquipmentSetKind.Battle,
                        "Casting",
                        DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                        new GearSlotsSnapshot(null, null, "weapon_wizard_5", null, null))
                }));
        Services.AddSingleton<IAppSessionController>(controller);

        var cut = Render<InventoryPage>();

        cut.Find("[data-testid='equip-battle-weapon_warrior_6']").Click();
        cut.Find("[data-testid='equip-preset-preset-1']").Click();
        cut.Find("[data-testid='rename-preset-preset-1']").Click();
        cut.Find("[data-testid='rename-preset-name-preset-1']").Input("Focused Casting");
        cut.Find("[data-testid='save-rename-preset-preset-1']").Click();
        cut.Find("[data-testid='remove-preset-preset-1']").Click();
        cut.Find("[data-testid='confirm-remove-preset-preset-1']").Click();

        Assert.Equal((EquipmentSetKind.Battle, "weapon_warrior_6"), controller.EquipItemCalls.Single());
        Assert.Equal("preset-1", controller.EquipPresetCalls.Single());
        Assert.Equal(("preset-1", "Focused Casting"), controller.RenamePresetCalls.Single());
        Assert.Equal("preset-1", controller.RemovePresetCalls.Single());
    }

    private static UserSnapshot CreateSnapshot()
    {
        return new UserSnapshot(
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
                new[] { "head_wizard_3", "head_special_2", "weapon_wizard_5", "weapon_warrior_6" }));
    }
}
