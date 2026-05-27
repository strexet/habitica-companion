using Habitica.Domain.User;
using Habitica.Rules.Equipment;

namespace Habitica.Rules.Tests.Equipment;

public sealed class EquipmentRecommendationFactoryTests
{
    [Fact]
    public void Create_recommends_intelligence_gear_for_mana_goal()
    {
        var snapshot = CreateUserSnapshot(
            equipped: new GearSlotsSnapshot("head_con", null, null, null, null),
            ownedGearKeys: new[] { "head_int", "head_con", "weapon_int" });
        var catalog = CreateCatalog(new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
        {
            ["head_int"] = new("head_int", "INT Hood", "Head", "wizard", null, new GearStatBlock(0m, 8m, 0m, 0m)),
            ["head_con"] = new("head_con", "CON Helm", "Head", "warrior", null, new GearStatBlock(0m, 0m, 12m, 0m)),
            ["weapon_int"] = new("weapon_int", "INT Wand", "Weapon", "wizard", null, new GearStatBlock(0m, 10m, 0m, 0m))
        });
        var factory = new EquipmentRecommendationFactory();

        var recommendation = factory.Create(snapshot, catalog, EquipmentRecommendationGoal.Intelligence);

        Assert.True(recommendation.HasRecommendation);
        Assert.Equal("INT for mana", recommendation.GoalLabel);
        Assert.Equal("head_int", recommendation.Slots.Head);
        Assert.Equal("weapon_int", recommendation.Slots.Weapon);
        Assert.Equal(27m, recommendation.RecommendedStats.Intelligence);
        Assert.Contains("post-CRON mana", recommendation.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_uses_two_handed_weapon_only_when_it_outscores_one_handed_plus_shield()
    {
        var snapshot = CreateUserSnapshot(
            equipped: new GearSlotsSnapshot(null, null, null, null, null),
            ownedGearKeys: new[] { "weapon_int_5", "weapon_int_2h_20", "shield_int_6" });
        var catalog = CreateCatalog(new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
        {
            ["weapon_int_5"] = new("weapon_int_5", "Apprentice Wand", "Weapon", null, null, new GearStatBlock(0m, 5m, 0m, 0m)),
            ["weapon_int_2h_20"] = new("weapon_int_2h_20", "Greatstaff", "Weapon", null, null, new GearStatBlock(0m, 20m, 0m, 0m), true),
            ["shield_int_6"] = new("shield_int_6", "Crest Shield", "Shield", null, null, new GearStatBlock(0m, 6m, 0m, 0m))
        });
        var factory = new EquipmentRecommendationFactory();

        var recommendation = factory.Create(snapshot, catalog, EquipmentRecommendationGoal.Intelligence);

        Assert.Equal("weapon_int_2h_20", recommendation.Slots.Weapon);
        Assert.Null(recommendation.Slots.Shield);
    }

    [Fact]
    public void Create_keeps_one_handed_weapon_and_shield_when_the_pair_outscores_two_handed()
    {
        var snapshot = CreateUserSnapshot(
            equipped: new GearSlotsSnapshot(null, null, null, null, null),
            ownedGearKeys: new[] { "weapon_int_15", "weapon_int_2h_20", "shield_int_10" });
        var catalog = CreateCatalog(new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
        {
            ["weapon_int_15"] = new("weapon_int_15", "Sage Wand", "Weapon", null, null, new GearStatBlock(0m, 15m, 0m, 0m)),
            ["weapon_int_2h_20"] = new("weapon_int_2h_20", "Greatstaff", "Weapon", null, null, new GearStatBlock(0m, 20m, 0m, 0m), true),
            ["shield_int_10"] = new("shield_int_10", "Aegis", "Shield", null, null, new GearStatBlock(0m, 10m, 0m, 0m))
        });
        var factory = new EquipmentRecommendationFactory();

        var recommendation = factory.Create(snapshot, catalog, EquipmentRecommendationGoal.Intelligence);

        Assert.Equal("weapon_int_15", recommendation.Slots.Weapon);
        Assert.Equal("shield_int_10", recommendation.Slots.Shield);
    }

    private static UserSnapshot CreateUserSnapshot(GearSlotsSnapshot equipped, string[] ownedGearKeys)
    {
        return new UserSnapshot(
            DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
            "Mage Tester",
            "wizard",
            15,
            50m,
            50m,
            25m,
            40m,
            0m,
            100m,
            10m,
            null,
            null,
            null,
            new EquipmentSnapshot(
                equipped,
                new GearSlotsSnapshot(null, null, null, null, null)),
            new InventorySnapshot(0, 0, 0, 0, 0, 0, ownedGearKeys));
    }

    private static GearCatalogSnapshot CreateCatalog(IReadOnlyDictionary<string, GearCatalogItem> items)
    {
        return new GearCatalogSnapshot(DateTimeOffset.Parse("2026-04-25T08:00:00Z"), items);
    }
}
