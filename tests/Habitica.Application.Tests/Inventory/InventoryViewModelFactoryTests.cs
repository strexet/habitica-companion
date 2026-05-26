using Habitica.Application.Inventory;
using Habitica.Domain.User;

namespace Habitica.Application.Tests.Inventory;

public sealed class InventoryViewModelFactoryTests
{
    [Fact]
    public void Create_groups_owned_battle_gear_by_slot_and_moves_no_stat_items_to_bottom_groups()
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
                    "headAccessory_special_1",
                    "eyewear_special_1",
                    "armor_wizard_4",
                    "body_special_1",
                    "weapon_wizard_5",
                    "weapon_warrior_6",
                    "shield_wizard_2",
                    "back_base_0",
                    "back_wizard_1",
                    "back_special_1"
                }));
        var catalog = new GearCatalogSnapshot(
            DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
            new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
            {
                ["head_wizard_3"] = new("head_wizard_3", "Wizard Hat", "Head", "wizard", null, new GearStatBlock(0m, 2m, 0m, 0m)),
                ["head_special_2"] = new("head_special_2", "Festival Mask", "Head", "special", null, GearStatBlock.Zero),
                ["headAccessory_special_1"] = new("headAccessory_special_1", "Moon Pin", "Head Accessory", "special", null, new GearStatBlock(0m, 0m, 0m, 4m)),
                ["eyewear_special_1"] = new("eyewear_special_1", "Scholar Spectacles", "Eyewear", "special", null, new GearStatBlock(0m, 5m, 0m, 0m)),
                ["armor_wizard_4"] = new("armor_wizard_4", "Wizard Robe", "Armor", "wizard", null, new GearStatBlock(0m, 3m, 0m, 0m)),
                ["body_special_1"] = new("body_special_1", "Utility Belt", "Body", "special", null, new GearStatBlock(1m, 0m, 0m, 1m)),
                ["weapon_wizard_5"] = new("weapon_wizard_5", "Wizard Wand", "Weapon", "wizard", null, new GearStatBlock(0m, 12m, 0m, 2m)),
                ["weapon_warrior_6"] = new("weapon_warrior_6", "Warrior Sword", "Weapon", "warrior", null, new GearStatBlock(10m, 0m, 1m, 0m)),
                ["shield_wizard_2"] = new("shield_wizard_2", "Wizard Shield", "Shield", "wizard", null, new GearStatBlock(0m, 1m, 2m, 0m)),
                ["back_wizard_1"] = new("back_wizard_1", "Wizard Cape", "Back", "wizard", null, new GearStatBlock(0m, 0m, 0m, 6m)),
                ["back_special_1"] = new("back_special_1", "Cape", "Back", "special", null, GearStatBlock.Zero)
            });

        var viewModel = factory.Create(snapshot, catalog);

        var headGroup = Assert.Single(viewModel.Groups, group => group.SlotTitle == "Head");
        var headAccessoryGroup = Assert.Single(viewModel.Groups, group => group.SlotTitle == "Head Accessory");
        var eyewearGroup = Assert.Single(viewModel.Groups, group => group.SlotTitle == "Eyewear");
        var bodyGroup = Assert.Single(viewModel.Groups, group => group.SlotTitle == "Body");
        var weaponGroup = Assert.Single(viewModel.Groups, group => group.SlotTitle == "Weapon");
        var backGroup = Assert.Single(viewModel.Groups, group => group.SlotTitle == "Back");
        var headAccessories = Assert.Single(viewModel.AccessoryGroups, group => group.SlotTitle == "Head");
        var backAccessories = Assert.Single(viewModel.AccessoryGroups, group => group.SlotTitle == "Back");

        Assert.Contains(headGroup.Items, item => item.Key == "head_wizard_3" && item.IsBattleEquipped);
        Assert.DoesNotContain(headGroup.Items, item => item.Key == "head_special_2");
        Assert.Equal("headAccessory_special_1", Assert.Single(headAccessoryGroup.Items).Key);
        Assert.Equal("eyewear_special_1", Assert.Single(eyewearGroup.Items).Key);
        Assert.Equal("body_special_1", Assert.Single(bodyGroup.Items).Key);
        Assert.Equal("back_wizard_1", Assert.Single(backGroup.Items).Key);
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
    public void Create_moves_two_handed_weapons_to_own_group_after_weapons_and_shields()
    {
        var factory = new InventoryViewModelFactory();
        var snapshot = CreateSnapshot("wizard") with
        {
            Inventory = new InventorySnapshot(
                1,
                5,
                1,
                1,
                1,
                1,
                new[] { "weapon_one_handed", "weapon_two_handed", "shield_wizard_2" })
        };
        var catalog = new GearCatalogSnapshot(
            DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
            new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
            {
                ["weapon_one_handed"] = new("weapon_one_handed", "Dagger", "Weapon", "special", null, new GearStatBlock(3m, 0m, 0m, 0m)),
                ["weapon_two_handed"] = new("weapon_two_handed", "Greatsword", "Weapon", "special", null, new GearStatBlock(12m, 0m, 0m, 0m), true),
                ["shield_wizard_2"] = new("shield_wizard_2", "Wizard Shield", "Shield", "wizard", null, new GearStatBlock(0m, 1m, 2m, 0m))
            });

        var viewModel = factory.Create(snapshot, catalog);

        Assert.Equal(new[] { "Weapon", "Shield", "Two-Handed Weapons" }, viewModel.Groups.Select(group => group.SlotTitle));
        var twoHandedGroup = Assert.Single(viewModel.Groups, group => group.SlotTitle == "Two-Handed Weapons");
        Assert.Equal("weapon_two_handed", Assert.Single(twoHandedGroup.Items).Key);
        Assert.Equal("weapon_two_handed", Assert.Single(twoHandedGroup.BestItems).Key);
    }

    [Fact]
    public void Create_selects_best_items_by_matching_stat_modifier_sets()
    {
        var factory = new InventoryViewModelFactory();
        var snapshot = CreateSnapshot("wizard") with
        {
            Inventory = new InventorySnapshot(
                1,
                5,
                1,
                1,
                1,
                1,
                new[] { "weapon_all_5", "weapon_int_16_a", "weapon_con_per_25", "weapon_str_16", "weapon_int_16_b", "weapon_int_10" })
        };
        var catalog = new GearCatalogSnapshot(
            DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
            new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
            {
                ["weapon_all_5"] = new("weapon_all_5", "Balanced Wand", "Weapon", "special", null, new GearStatBlock(5m, 5m, 5m, 5m)),
                ["weapon_int_16_a"] = new("weapon_int_16_a", "Sage Wand", "Weapon", "special", null, new GearStatBlock(0m, 16m, 0m, 0m)),
                ["weapon_con_per_25"] = new("weapon_con_per_25", "Sentinel Wand", "Weapon", "special", null, new GearStatBlock(0m, 0m, 25m, 25m)),
                ["weapon_str_16"] = new("weapon_str_16", "Might Wand", "Weapon", "special", null, new GearStatBlock(16m, 0m, 0m, 0m)),
                ["weapon_int_16_b"] = new("weapon_int_16_b", "Scholar Wand", "Weapon", "special", null, new GearStatBlock(0m, 16m, 0m, 0m)),
                ["weapon_int_10"] = new("weapon_int_10", "Apprentice Wand", "Weapon", "special", null, new GearStatBlock(0m, 10m, 0m, 0m))
            });

        var viewModel = factory.Create(snapshot, catalog);

        var weaponGroup = Assert.Single(viewModel.Groups, group => group.SlotTitle == "Weapon");
        Assert.Equal(
            new[] { "weapon_all_5", "weapon_con_per_25", "weapon_int_16_a", "weapon_int_16_b", "weapon_str_16" },
            weaponGroup.BestItems.Select(item => item.Key));
    }

    [Fact]
    public void Create_removes_items_dominated_by_better_multi_stat_items()
    {
        var factory = new InventoryViewModelFactory();
        var snapshot = CreateSnapshot("wizard") with
        {
            Inventory = new InventorySnapshot(
                1,
                5,
                1,
                1,
                1,
                1,
                new[] { "weapon_str_int_25", "weapon_int_7", "weapon_str_15" })
        };
        var catalog = new GearCatalogSnapshot(
            DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
            new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
            {
                ["weapon_str_int_25"] = new("weapon_str_int_25", "Archmage Blade", "Weapon", "special", null, new GearStatBlock(25m, 25m, 0m, 0m)),
                ["weapon_int_7"] = new("weapon_int_7", "Apprentice Wand", "Weapon", "special", null, new GearStatBlock(0m, 7m, 0m, 0m)),
                ["weapon_str_15"] = new("weapon_str_15", "Training Blade", "Weapon", "special", null, new GearStatBlock(15m, 0m, 0m, 0m))
            });

        var viewModel = factory.Create(snapshot, catalog);

        var weaponGroup = Assert.Single(viewModel.Groups, group => group.SlotTitle == "Weapon");
        Assert.Equal(new[] { "weapon_str_int_25" }, weaponGroup.BestItems.Select(item => item.Key));
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
                ["back_wizard_1"] = new("back_wizard_1", "Wizard Cape", "Back", "wizard", null, new GearStatBlock(0m, 0m, 0m, 10m)),
                ["eyewear_special_1"] = new("eyewear_special_1", "Scholar Spectacles", "Eyewear", "special", null, new GearStatBlock(0m, 3m, 0m, 0m)),
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
                new GearSlotsSnapshot("head_wizard_3", null, "weapon_wizard_5", null, "back_wizard_1", Eyewear: "eyewear_special_1")),
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
        Assert.Equal(new[] { "Wizard Hat", "Scholar Spectacles", "Wizard Wand", "Wizard Cape" }, battlePreset.Items.Select(item => item.DisplayName));
        Assert.Equal(new GearStatBlock(0m, 24m, 0m, 18m), battlePreset.TotalStats);
        Assert.Contains(battlePreset.Items, item => item.SlotTitle == "Back");
        Assert.Contains(battlePreset.Items, item => item.SlotTitle == "Eyewear");
        Assert.Equal("Party Look", costumePreset.Name);
        Assert.Equal(GearStatBlock.Zero, costumePreset.TotalStats);
    }

    [Fact]
    public void CreateRecommendation_scores_goal_and_respects_two_handed_weapon_tradeoff()
    {
        var factory = new InventoryViewModelFactory();
        var snapshot = CreateSnapshot("warrior") with
        {
            Inventory = new InventorySnapshot(
                1,
                5,
                1,
                1,
                1,
                1,
                new[] { "weapon_one_handed", "weapon_two_handed", "shield_con", "head_str" })
        };
        var catalog = new GearCatalogSnapshot(
            DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
            new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
            {
                ["weapon_one_handed"] = new("weapon_one_handed", "Sword", "Weapon", "warrior", null, new GearStatBlock(8m, 0m, 0m, 0m)),
                ["weapon_two_handed"] = new("weapon_two_handed", "Greatsword", "Weapon", "warrior", null, new GearStatBlock(18m, 0m, 0m, 0m), true),
                ["shield_con"] = new("shield_con", "Tower Shield", "Shield", "special", null, new GearStatBlock(0m, 0m, 10m, 0m)),
                ["head_str"] = new("head_str", "Horned Helm", "Head", "warrior", null, new GearStatBlock(4m, 0m, 0m, 0m))
            });

        var recommendation = factory.CreateRecommendation(snapshot, catalog, EquipmentOptimizationGoal.Strength);

        Assert.Equal("weapon_two_handed", recommendation.Slots.Weapon);
        Assert.Null(recommendation.Slots.Shield);
        Assert.Contains(recommendation.Items, item => item.Key == "head_str");
        Assert.True(recommendation.Delta.Strength > 0m);
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
