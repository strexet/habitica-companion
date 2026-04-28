using Habitica.Application.Inventory;
using Habitica.Domain.User;

namespace Habitica.Application.Tests.Inventory;

public sealed class InventoryViewModelFactoryTests
{
    [Fact]
    public void Create_groups_owned_battle_gear_by_slot_and_moves_accessories_to_bottom_groups()
    {
        var factory = new InventoryViewModelFactory();
        var snapshot = new UserSnapshot(
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
                new GearSlotsSnapshot("head_wizard_3", "armor_wizard_4", "weapon_wizard_5", "shield_wizard_2", "back_base_0"),
                new GearSlotsSnapshot("head_special_2", "armor_special_2", "weapon_special_2", "shield_special_2", "back_special_2")),
            new InventorySnapshot(
                1,
                5,
                1,
                1,
                1,
                1,
                new[]
                {
                    "head_wizard_3",
                    "head_special_2",
                    "armor_wizard_4",
                    "weapon_wizard_5",
                    "weapon_warrior_6",
                    "shield_wizard_2",
                    "back_base_0",
                    "back_special_1"
                }));
        var catalog = new GearCatalogSnapshot(
            DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
            new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
            {
                ["head_wizard_3"] = new("head_wizard_3", "Wizard Hat", "Head", "wizard", null, new GearStatBlock(0m, 2m, 0m, 0m)),
                ["head_special_2"] = new("head_special_2", "Festival Mask", "Head", "special", null, GearStatBlock.Zero),
                ["armor_wizard_4"] = new("armor_wizard_4", "Wizard Robe", "Armor", "wizard", null, new GearStatBlock(0m, 3m, 0m, 0m)),
                ["weapon_wizard_5"] = new("weapon_wizard_5", "Wizard Wand", "Weapon", "wizard", null, new GearStatBlock(0m, 12m, 0m, 2m)),
                ["weapon_warrior_6"] = new("weapon_warrior_6", "Warrior Sword", "Weapon", "warrior", null, new GearStatBlock(10m, 0m, 1m, 0m)),
                ["shield_wizard_2"] = new("shield_wizard_2", "Wizard Shield", "Shield", "wizard", null, new GearStatBlock(0m, 1m, 2m, 0m)),
                ["back_special_1"] = new("back_special_1", "Cape", "Back", "special", null, GearStatBlock.Zero)
            });

        var viewModel = factory.Create(snapshot, catalog);

        var headGroup = Assert.Single(viewModel.Groups, group => group.SlotTitle == "Head");
        var weaponGroup = Assert.Single(viewModel.Groups, group => group.SlotTitle == "Weapon");
        var headAccessories = Assert.Single(viewModel.AccessoryGroups, group => group.SlotTitle == "Head");
        var backAccessories = Assert.Single(viewModel.AccessoryGroups, group => group.SlotTitle == "Back");

        Assert.Contains(headGroup.Items, item => item.Key == "head_wizard_3" && item.IsBattleEquipped);
        Assert.DoesNotContain(headGroup.Items, item => item.Key == "head_special_2");
        Assert.Contains(headAccessories.Items, item => item.Key == "head_special_2" && item.IsCostumeEquipped);
        Assert.Contains(backAccessories.Items, item => item.Key == "back_special_1");
        Assert.DoesNotContain(backAccessories.Items, item => item.Key == "back_base_0");
        Assert.Equal("weapon_wizard_5", weaponGroup.BattleEquippedKey);
        Assert.Equal("weapon_special_2", weaponGroup.CostumeEquippedKey);
        Assert.Equal(new[] { "weapon_warrior_6", "weapon_wizard_5" }, weaponGroup.Items.Select(item => item.Key));
    }

    [Fact]
    public void Create_resolves_catalog_names_and_current_class_stat_totals()
    {
        var factory = new InventoryViewModelFactory();
        var snapshot = CreateSnapshot("wizard");
        var catalog = new GearCatalogSnapshot(
            DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
            new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
            {
                ["weapon_wizard_5"] = new(
                    "weapon_wizard_5",
                    "Wizard Wand",
                    "Weapon",
                    "wizard",
                    "Class weapon",
                    new GearStatBlock(0m, 12m, 0m, 2m)),
                ["weapon_warrior_6"] = new(
                    "weapon_warrior_6",
                    "Warrior Sword",
                    "Weapon",
                    "warrior",
                    "Cross-class weapon",
                    new GearStatBlock(10m, 0m, 1m, 0m))
            });

        var viewModel = factory.Create(snapshot, catalog, Array.Empty<EquipmentPreset>());

        var weaponGroup = Assert.Single(viewModel.Groups, group => group.SlotTitle == "Weapon");
        var wizardWeapon = Assert.Single(weaponGroup.Items, item => item.Key == "weapon_wizard_5");
        var warriorWeapon = Assert.Single(weaponGroup.Items, item => item.Key == "weapon_warrior_6");

        Assert.Equal("Wizard Wand", wizardWeapon.DisplayName);
        Assert.Equal(new GearStatBlock(0m, 18m, 0m, 3m), wizardWeapon.TotalStats);
        Assert.Equal("Warrior Sword", warriorWeapon.DisplayName);
        Assert.Equal(new GearStatBlock(10m, 0m, 1m, 0m), warriorWeapon.TotalStats);
    }

    [Fact]
    public void Create_builds_battle_and_costume_presets_with_battle_stat_totals()
    {
        var factory = new InventoryViewModelFactory();
        var snapshot = CreateSnapshot("wizard");
        var catalog = new GearCatalogSnapshot(
            DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
            new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
            {
                ["head_wizard_3"] = new("head_wizard_3", "Wizard Hat", "Head", "wizard", null, new GearStatBlock(0m, 2m, 0m, 0m)),
                ["weapon_wizard_5"] = new("weapon_wizard_5", "Wizard Wand", "Weapon", "wizard", null, new GearStatBlock(0m, 12m, 0m, 2m)),
                ["head_special_2"] = new("head_special_2", "Festival Mask", "Head", "special", null, new GearStatBlock(1m, 0m, 0m, 0m))
            });
        var presets = new[]
        {
            new EquipmentPreset(
                "preset-battle",
                "user-id",
                EquipmentSetKind.Battle,
                "Casting",
                DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                new GearSlotsSnapshot("head_wizard_3", null, "weapon_wizard_5", null, null)),
            new EquipmentPreset(
                "preset-costume",
                "user-id",
                EquipmentSetKind.Costume,
                "Party Look",
                DateTimeOffset.Parse("2026-04-26T09:05:00Z"),
                new GearSlotsSnapshot("head_special_2", null, null, null, null))
        };

        var viewModel = factory.Create(snapshot, catalog, presets);

        var battlePreset = Assert.Single(viewModel.BattlePresets);
        var costumePreset = Assert.Single(viewModel.CostumePresets);

        Assert.Equal("Casting", battlePreset.Name);
        Assert.Equal("preset-battle", battlePreset.Id);
        Assert.Equal(new[] { "Wizard Hat", "Wizard Wand" }, battlePreset.Items.Select(item => item.DisplayName));
        Assert.Equal(new GearStatBlock(0m, 21m, 0m, 3m), battlePreset.TotalStats);
        Assert.Equal("Party Look", costumePreset.Name);
        Assert.Equal(GearStatBlock.Zero, costumePreset.TotalStats);
    }

    private static UserSnapshot CreateSnapshot(string className)
    {
        return new UserSnapshot(
            DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
            "Mage Tester",
            className,
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
