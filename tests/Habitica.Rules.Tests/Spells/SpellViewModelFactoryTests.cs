using Habitica.Domain.Tasks;
using Habitica.Domain.User;
using Habitica.Rules.Spells;

namespace Habitica.Rules.Tests.Spells;

public sealed class SpellViewModelFactoryTests
{
    [Fact]
    public void Create_returns_current_class_spells_and_marks_locked_spells_by_level()
    {
        var snapshot = CreateUserSnapshot(className: "wizard", level: 12);
        var factory = new SpellViewModelFactory();

        var viewModel = factory.Create(snapshot, null, null);

        Assert.Equal("wizard", viewModel.ClassName);
        Assert.Equal(new[] { "fireball", "mpheal", "earth", "frost" }, viewModel.Spells.Select(spell => spell.Id));
        Assert.True(viewModel.Spells.Single(spell => spell.Id == "fireball").IsUnlocked);
        Assert.True(viewModel.Spells.Single(spell => spell.Id == "mpheal").IsUnlocked);
        Assert.False(viewModel.Spells.Single(spell => spell.Id == "earth").IsUnlocked);
        Assert.Equal("Unlocks at level 13", viewModel.Spells.Single(spell => spell.Id == "earth").AvailabilityLabel);
    }

    [Fact]
    public void Create_marks_stat_allocation_locked_before_level_ten()
    {
        var snapshot = CreateUserSnapshot(className: "wizard", level: 9) with
        {
            UnallocatedStatPoints = 3
        };
        var factory = new SpellViewModelFactory();

        var viewModel = factory.Create(snapshot, null, null);

        Assert.Equal(3, viewModel.UnallocatedStatPoints);
        Assert.False(viewModel.IsStatAllocationUnlocked);
        Assert.Equal("Stat allocation unlocks at level 10.", viewModel.StatAllocationLockedReason);
    }

    [Fact]
    public void Create_selects_highest_value_open_non_reward_task_for_task_spells()
    {
        var snapshot = CreateUserSnapshot(
            className: "rogue",
            level: 15,
            stats: new CharacterStatsSnapshot(10m, 8m, 6m, 30m));
        var tasks = new TaskCollectionSnapshot(
            DateTimeOffset.Parse("2026-04-30T06:00:00Z"),
            new[]
            {
                new TaskSnapshot("priority-high", "High priority but dull", TaskType.Habit, false, 9m, null, null, 1m),
                new TaskSnapshot("todo-blue", "Bluest todo", TaskType.Todo, false, 1m, null, null, 18m),
                new TaskSnapshot("challenge-blue", "Challenge blue", TaskType.Daily, false, 1m, null, null, 50m, true),
                new TaskSnapshot("daily-complete", "Done daily", TaskType.Daily, true, 1m, null, null, 4m),
                new TaskSnapshot("todo-complete", "Done todo", TaskType.Todo, true, 1m, null, null, 60m),
                new TaskSnapshot("reward", "Reward", TaskType.Reward, false, 1m, null, null, 50m)
            });
        var factory = new SpellViewModelFactory();

        var viewModel = factory.Create(snapshot, tasks, null);
        var pickpocket = viewModel.Spells.Single(spell => spell.Id == "pickPocket");

        Assert.Equal("todo-blue", pickpocket.SelectedTargetTaskId);
        Assert.DoesNotContain(viewModel.TargetTasks, task => task.Id == "challenge-blue");
        Assert.Contains(viewModel.TargetTasks, task => task.Id == "daily-complete");
        Assert.DoesNotContain(viewModel.TargetTasks, task => task.Id == "todo-complete");
        Assert.Contains("Bluest todo", pickpocket.Description, StringComparison.Ordinal);
        Assert.Contains("value 18", pickpocket.Description, StringComparison.Ordinal);
        Assert.Contains("Bluest todo", pickpocket.EstimatedEffect, StringComparison.Ordinal);
        Assert.Contains("8.33 GP", pickpocket.EstimatedEffect, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_uses_current_battle_gear_and_buffs_for_spell_estimates()
    {
        var snapshot = CreateUserSnapshot(
            className: "rogue",
            level: 15,
            stats: new CharacterStatsSnapshot(10m, 8m, 6m, 236.14m),
            buffs: new CharacterStatsSnapshot(0m, 0m, 0m, 4m),
            equipped: new GearSlotsSnapshot("head_per", null, null, null, null),
            ownedGearKeys: new[] { "head_per" });
        var catalog = new GearCatalogSnapshot(
            DateTimeOffset.Parse("2026-04-30T06:00:00Z"),
            new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
            {
                ["head_per"] = new("head_per", "Per Hood", "Head", "rogue", null, new GearStatBlock(0m, 0m, 0m, 8m))
            });
        var tasks = new TaskCollectionSnapshot(
            DateTimeOffset.Parse("2026-04-30T06:00:00Z"),
            new[]
            {
                new TaskSnapshot("task-1", "Blue daily", TaskType.Daily, false, 1m, null, null, 18m)
            });
        var factory = new SpellViewModelFactory();

        var viewModel = factory.Create(snapshot, tasks, catalog);
        var pickpocket = viewModel.Spells.Single(spell => spell.Id == "pickPocket");

        Assert.Contains("16.61 GP", pickpocket.EstimatedEffect, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_builds_dynamic_equipment_recommendations_and_marks_already_equipped_recommendations()
    {
        var snapshot = CreateUserSnapshot(
            className: "wizard",
            level: 15,
            equipped: new GearSlotsSnapshot("head_int", "armor_other", "weapon_int", null, null),
            ownedGearKeys: new[] { "head_int", "head_per", "armor_other", "weapon_int", "weapon_balanced" });
        var catalog = new GearCatalogSnapshot(
            DateTimeOffset.Parse("2026-04-30T06:00:00Z"),
            new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
            {
                ["head_int"] = new("head_int", "Int Hood", "Head", "wizard", null, new GearStatBlock(0m, 8m, 0m, 0m)),
                ["head_per"] = new("head_per", "Per Hood", "Head", "wizard", null, new GearStatBlock(0m, 0m, 0m, 8m)),
                ["armor_other"] = new("armor_other", "Other Robe", "Armor", null, null, new GearStatBlock(0m, 0m, 1m, 0m)),
                ["weapon_int"] = new("weapon_int", "Int Wand", "Weapon", "wizard", null, new GearStatBlock(0m, 10m, 0m, 0m)),
                ["weapon_balanced"] = new("weapon_balanced", "Balanced Wand", "Weapon", "wizard", null, new GearStatBlock(0m, 6m, 0m, 6m))
            });
        var factory = new SpellViewModelFactory();

        var viewModel = factory.Create(snapshot, null, catalog);
        var fireball = viewModel.Spells.Single(spell => spell.Id == "fireball");

        Assert.Contains(fireball.EquipmentRecommendations, recommendation => recommendation.Name == "Maximize INT" && recommendation.IsEquipped);
        Assert.Contains(fireball.EquipmentRecommendations, recommendation => recommendation.Name == "Maximize PER" && !recommendation.IsEquipped);
        Assert.Contains(fireball.EquipmentRecommendations, recommendation => recommendation.Name == "Balanced INT/PER");
    }

    [Fact]
    public void Create_builds_recommendation_effect_estimates_from_recommended_gear()
    {
        var snapshot = CreateUserSnapshot(
            className: "rogue",
            level: 15,
            stats: CharacterStatsSnapshot.Zero,
            equipped: new GearSlotsSnapshot(null, null, null, null, null),
            ownedGearKeys: new[] { "head_per" });
        var catalog = new GearCatalogSnapshot(
            DateTimeOffset.Parse("2026-04-30T06:00:00Z"),
            new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
            {
                ["head_per"] = new("head_per", "Per Hood", "Head", "rogue", null, new GearStatBlock(0m, 0m, 0m, 8m))
            });
        var factory = new SpellViewModelFactory();

        var viewModel = factory.Create(snapshot, null, catalog);
        var toolsOfTrade = viewModel.Spells.Single(spell => spell.Id == "toolsOfTrade");
        var recommendation = toolsOfTrade.EquipmentRecommendations.Single();

        Assert.Contains("Adds approximately 13 PER", toolsOfTrade.EstimatedEffect, StringComparison.Ordinal);
        Assert.Contains("Adds approximately 28 PER", recommendation.EstimatedEffect, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_includes_level_bonus_and_server_rounding_for_buff_spell_estimates()
    {
        var factory = new SpellViewModelFactory();

        var wizard = factory.Create(CreateUserSnapshot("wizard", 15), null, null);
        var warrior = factory.Create(CreateUserSnapshot("warrior", 15), null, null);
        var healer = factory.Create(CreateUserSnapshot("healer", 15), null, null);

        Assert.Contains("Restores approximately 2 MP", wizard.Spells.Single(spell => spell.Id == "mpheal").EstimatedEffect, StringComparison.Ordinal);
        Assert.Contains("Adds approximately 2 INT", wizard.Spells.Single(spell => spell.Id == "earth").EstimatedEffect, StringComparison.Ordinal);
        Assert.Contains("Adds approximately 2 CON", warrior.Spells.Single(spell => spell.Id == "defensiveStance").EstimatedEffect, StringComparison.Ordinal);
        Assert.Contains("Adds approximately 1 STR", warrior.Spells.Single(spell => spell.Id == "valorousPresence").EstimatedEffect, StringComparison.Ordinal);
        Assert.Contains("Adds approximately 1 CON", warrior.Spells.Single(spell => spell.Id == "intimidate").EstimatedEffect, StringComparison.Ordinal);
        Assert.Contains("Adds approximately 7 CON", healer.Spells.Single(spell => spell.Id == "protectAura").EstimatedEffect, StringComparison.Ordinal);
    }

    private static UserSnapshot CreateUserSnapshot(
        string className,
        int level,
        CharacterStatsSnapshot? stats = null,
        CharacterStatsSnapshot? buffs = null,
        GearSlotsSnapshot? equipped = null,
        IReadOnlyList<string>? ownedGearKeys = null)
    {
        return new UserSnapshot(
            DateTimeOffset.Parse("2026-04-30T06:00:00Z"),
            "Tester",
            className,
            level,
            50m,
            50m,
            40m,
            50m,
            0m,
            100m,
            10m,
            "party-1",
            null,
            null,
            new EquipmentSnapshot(
                equipped ?? new GearSlotsSnapshot(null, null, null, null, null),
                new GearSlotsSnapshot(null, null, null, null, null)),
            new InventorySnapshot(0, 0, 0, 0, 0, 0, (ownedGearKeys ?? Array.Empty<string>()).ToArray()),
            Stats: stats ?? CharacterStatsSnapshot.Zero,
            Buffs: buffs ?? CharacterStatsSnapshot.Zero,
            BuffFlags: BuffFlagsSnapshot.Empty);
    }
}
