using Habitica.Domain.User;
using Habitica.Rules.Stats;

namespace Habitica.Rules.Equipment;

public sealed class EquipmentRecommendationFactory
{
    public EquipmentRecommendation Create(
        UserSnapshot snapshot,
        GearCatalogSnapshot? catalog,
        EquipmentRecommendationGoal goal)
    {
        if (catalog is null)
        {
            return EquipmentRecommendation.Empty(goal, GetGoalLabel(goal), "Refresh gear data before using gear optimization.");
        }

        var candidates = snapshot.Inventory.OwnedGearKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Where(key => catalog.Items.ContainsKey(key))
            .Select(key => BuildCandidate(snapshot, catalog.Items[key]))
            .Where(static item => item.TotalStats != GearStatBlock.Zero)
            .ToArray();

        var recommendedSlots = new GearSlotsSnapshot(
            Head: PickBestSlot(candidates, "Head", goal)?.Key,
            Armor: PickBestSlot(candidates, "Armor", goal)?.Key,
            Weapon: null,
            Shield: null,
            Back: PickBestSlot(candidates, "Back", goal)?.Key,
            HeadAccessory: PickBestSlot(candidates, "Head Accessory", goal)?.Key,
            Eyewear: PickBestSlot(candidates, "Eyewear", goal)?.Key,
            Body: PickBestSlot(candidates, "Body", goal)?.Key);

        var oneHandedWeapon = PickBestSlot(candidates.Where(static item => !item.TwoHanded).ToArray(), "Weapon", goal);
        var twoHandedWeapon = PickBestSlot(candidates.Where(static item => item.TwoHanded).ToArray(), "Weapon", goal);
        var shield = PickBestSlot(candidates, "Shield", goal);
        var oneHandedScore = ScoreStats(oneHandedWeapon?.TotalStats ?? GearStatBlock.Zero, goal)
            + ScoreStats(shield?.TotalStats ?? GearStatBlock.Zero, goal);
        var twoHandedScore = ScoreStats(twoHandedWeapon?.TotalStats ?? GearStatBlock.Zero, goal);

        recommendedSlots = twoHandedWeapon is not null && twoHandedScore > oneHandedScore
            ? recommendedSlots with { Weapon = twoHandedWeapon.Key, Shield = null }
            : recommendedSlots with { Weapon = oneHandedWeapon?.Key, Shield = shield?.Key };

        var items = EnumerateSlots(recommendedSlots)
            .Where(slot => !string.IsNullOrWhiteSpace(slot.Key))
            .Select(slot => BuildItem(snapshot, catalog.Items[slot.Key!]))
            .ToArray();
        var currentStats = ToGearStatBlock(CharacterStatsCalculator.CalculateBattleGearStats(snapshot, catalog));
        var recommendedStats = ToGearStatBlock(CharacterStatsCalculator.CalculateRecommendedGearStats(snapshot, catalog, recommendedSlots));
        var isEquipped = SlotsContainRecommendedKeys(recommendedSlots, snapshot.Equipment.Battle);

        return new EquipmentRecommendation(
            goal,
            GetGoalLabel(goal),
            GetGoalDescription(goal),
            recommendedSlots,
            items,
            currentStats,
            recommendedStats,
            recommendedStats.Subtract(currentStats),
            items.Length > 0,
            isEquipped);
    }

    private static EquipmentRecommendationItem BuildCandidate(UserSnapshot snapshot, GearCatalogItem item)
    {
        return BuildItem(snapshot, item);
    }

    private static EquipmentRecommendationItem BuildItem(UserSnapshot snapshot, GearCatalogItem item)
    {
        return new EquipmentRecommendationItem(
            item.SlotTitle,
            item.Key,
            string.IsNullOrWhiteSpace(item.Text) ? item.Key : item.Text,
            ToGearStatBlock(CharacterStatsCalculator.CalculateItemStats(snapshot, item)),
            item.TwoHanded);
    }

    private static EquipmentRecommendationItem? PickBestSlot(
        IReadOnlyList<EquipmentRecommendationItem> candidates,
        string slotTitle,
        EquipmentRecommendationGoal goal)
    {
        return candidates
            .Where(item => string.Equals(item.SlotTitle, slotTitle, StringComparison.Ordinal))
            .OrderByDescending(item => ScoreStats(item.TotalStats, goal))
            .ThenByDescending(static item => item.TotalStats.Strength + item.TotalStats.Intelligence + item.TotalStats.Constitution + item.TotalStats.Perception)
            .ThenBy(static item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(item => ScoreStats(item.TotalStats, goal) > 0m);
    }

    private static decimal ScoreStats(GearStatBlock stats, EquipmentRecommendationGoal goal)
    {
        return goal switch
        {
            // Single-stat goals strictly maximize the prioritized stat; other stats
            // only break ties (via PickBestSlot) when the prioritized value is equal.
            EquipmentRecommendationGoal.Intelligence => stats.Intelligence,
            EquipmentRecommendationGoal.Constitution => stats.Constitution,
            EquipmentRecommendationGoal.Survival => stats.Constitution * 2.4m + stats.Intelligence * 0.8m + stats.Strength * 0.6m,
            _ => stats.Strength + stats.Intelligence + stats.Constitution + stats.Perception
        };
    }

    private static string GetGoalLabel(EquipmentRecommendationGoal goal)
    {
        return goal switch
        {
            EquipmentRecommendationGoal.Intelligence => "INT for mana",
            EquipmentRecommendationGoal.Constitution => "CON for less damage",
            EquipmentRecommendationGoal.Survival => "Survival",
            _ => "Balanced"
        };
    }

    private static string GetGoalDescription(EquipmentRecommendationGoal goal)
    {
        return goal switch
        {
            EquipmentRecommendationGoal.Intelligence => "Assumption-based: prioritizes Intelligence so post-CRON mana is more likely to improve.",
            EquipmentRecommendationGoal.Constitution => "Assumption-based: prioritizes Constitution because Habitica damage mitigation is server-side.",
            EquipmentRecommendationGoal.Survival => "Assumption-based: prioritizes Constitution with smaller Intelligence and Strength weights for broad CRON safety.",
            _ => "Assumption-based: balances stat-bearing battle gear before CRON."
        };
    }

    private static bool SlotsContainRecommendedKeys(GearSlotsSnapshot recommendation, GearSlotsSnapshot current)
    {
        return SlotMatches(recommendation.Head, current.Head)
            && SlotMatches(recommendation.HeadAccessory, current.HeadAccessory)
            && SlotMatches(recommendation.Eyewear, current.Eyewear)
            && SlotMatches(recommendation.Armor, current.Armor)
            && SlotMatches(recommendation.Body, current.Body)
            && SlotMatches(recommendation.Weapon, current.Weapon)
            && SlotMatches(recommendation.Shield, current.Shield)
            && SlotMatches(recommendation.Back, current.Back);
    }

    private static bool SlotMatches(string? recommendedKey, string? currentKey)
    {
        return string.IsNullOrWhiteSpace(recommendedKey)
            || string.Equals(recommendedKey, currentKey, StringComparison.Ordinal);
    }

    private static GearStatBlock ToGearStatBlock(CharacterStatsSnapshot stats)
    {
        return new GearStatBlock(stats.Strength, stats.Intelligence, stats.Constitution, stats.Perception);
    }

    private static IEnumerable<(string SlotTitle, string? Key)> EnumerateSlots(GearSlotsSnapshot slots)
    {
        yield return ("Head", slots.Head);
        yield return ("Head Accessory", slots.HeadAccessory);
        yield return ("Eyewear", slots.Eyewear);
        yield return ("Armor", slots.Armor);
        yield return ("Body", slots.Body);
        yield return ("Weapon", slots.Weapon);
        yield return ("Shield", slots.Shield);
        yield return ("Back", slots.Back);
    }
}

public enum EquipmentRecommendationGoal
{
    Balanced,
    Intelligence,
    Constitution,
    Survival
}

public sealed record EquipmentRecommendation(
    EquipmentRecommendationGoal Goal,
    string GoalLabel,
    string Description,
    GearSlotsSnapshot Slots,
    IReadOnlyList<EquipmentRecommendationItem> Items,
    GearStatBlock CurrentStats,
    GearStatBlock RecommendedStats,
    GearStatBlock Delta,
    bool HasRecommendation,
    bool IsEquipped)
{
    public static EquipmentRecommendation Empty(EquipmentRecommendationGoal goal, string goalLabel, string description)
    {
        return new EquipmentRecommendation(
            goal,
            goalLabel,
            description,
            new GearSlotsSnapshot(null, null, null, null, null),
            Array.Empty<EquipmentRecommendationItem>(),
            GearStatBlock.Zero,
            GearStatBlock.Zero,
            GearStatBlock.Zero,
            false,
            false);
    }
}

public sealed record EquipmentRecommendationItem(
    string SlotTitle,
    string Key,
    string DisplayName,
    GearStatBlock TotalStats,
    bool TwoHanded);
