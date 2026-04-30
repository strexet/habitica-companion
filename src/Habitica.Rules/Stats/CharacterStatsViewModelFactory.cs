using Habitica.Domain.User;

namespace Habitica.Rules.Stats;

public sealed class CharacterStatsViewModelFactory
{
    public CharacterStatsPanelViewModel Create(UserSnapshot snapshot, GearCatalogSnapshot? catalog)
    {
        var baseStats = snapshot.Stats ?? CharacterStatsSnapshot.Zero;
        var equipmentStats = CharacterStatsCalculator.CalculateBattleGearStats(snapshot, catalog);
        var buffStats = snapshot.Buffs ?? CharacterStatsSnapshot.Zero;
        var effectiveStats = CharacterStatsCalculator.Add(CharacterStatsCalculator.Add(baseStats, equipmentStats), buffStats);

        return new CharacterStatsPanelViewModel(
            snapshot.UnallocatedStatPoints,
            new[]
            {
                BuildRow("str", "STR", "Strength", baseStats.Strength, equipmentStats.Strength, buffStats.Strength, effectiveStats.Strength),
                BuildRow("int", "INT", "Intelligence", baseStats.Intelligence, equipmentStats.Intelligence, buffStats.Intelligence, effectiveStats.Intelligence),
                BuildRow("con", "CON", "Constitution", baseStats.Constitution, equipmentStats.Constitution, buffStats.Constitution, effectiveStats.Constitution),
                BuildRow("per", "PER", "Perception", baseStats.Perception, equipmentStats.Perception, buffStats.Perception, effectiveStats.Perception)
            });
    }

    private static CharacterStatRowViewModel BuildRow(
        string key,
        string label,
        string name,
        decimal baseValue,
        decimal equipmentValue,
        decimal buffValue,
        decimal effectiveValue)
    {
        return new CharacterStatRowViewModel(key, label, name, baseValue, equipmentValue, buffValue, effectiveValue);
    }
}

public static class CharacterStatsCalculator
{
    public static CharacterStatsSnapshot CalculateBattleGearStats(UserSnapshot snapshot, GearCatalogSnapshot? catalog)
    {
        if (catalog is null)
        {
            return CharacterStatsSnapshot.Zero;
        }

        var total = CharacterStatsSnapshot.Zero;
        foreach (var key in new[]
        {
            snapshot.Equipment.Battle.Head,
            snapshot.Equipment.Battle.Armor,
            snapshot.Equipment.Battle.Weapon,
            snapshot.Equipment.Battle.Shield,
            snapshot.Equipment.Battle.Back
        })
        {
            if (!string.IsNullOrWhiteSpace(key) && catalog.Items.TryGetValue(key, out var item))
            {
                total = Add(total, CalculateItemStats(snapshot, item));
            }
        }

        return total;
    }

    public static CharacterStatsSnapshot CalculateRecommendedGearStats(
        UserSnapshot snapshot,
        GearCatalogSnapshot catalog,
        GearSlotsSnapshot slots)
    {
        var total = CharacterStatsSnapshot.Zero;
        foreach (var key in new[] { slots.Head, slots.Armor, slots.Weapon, slots.Shield, slots.Back })
        {
            if (!string.IsNullOrWhiteSpace(key) && catalog.Items.TryGetValue(key, out var item))
            {
                total = Add(total, CalculateItemStats(snapshot, item));
            }
        }

        return total;
    }

    public static CharacterStatsSnapshot CalculateItemStats(UserSnapshot snapshot, GearCatalogItem item)
    {
        var multiplier = !string.IsNullOrWhiteSpace(snapshot.ClassName)
            && !string.IsNullOrWhiteSpace(item.ClassName)
            && string.Equals(snapshot.ClassName, item.ClassName, StringComparison.OrdinalIgnoreCase)
            ? 1.5m
            : 1m;

        return new CharacterStatsSnapshot(
            item.Stats.Strength * multiplier,
            item.Stats.Intelligence * multiplier,
            item.Stats.Constitution * multiplier,
            item.Stats.Perception * multiplier);
    }

    public static CharacterStatsSnapshot Add(CharacterStatsSnapshot left, CharacterStatsSnapshot right)
    {
        return new CharacterStatsSnapshot(
            left.Strength + right.Strength,
            left.Intelligence + right.Intelligence,
            left.Constitution + right.Constitution,
            left.Perception + right.Perception);
    }
}

public sealed record CharacterStatsPanelViewModel(
    int UnallocatedPoints,
    IReadOnlyList<CharacterStatRowViewModel> Rows);

public sealed record CharacterStatRowViewModel(
    string Key,
    string Label,
    string Name,
    decimal BaseValue,
    decimal EquipmentValue,
    decimal BuffValue,
    decimal EffectiveValue);
