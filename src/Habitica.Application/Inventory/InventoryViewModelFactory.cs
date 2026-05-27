using System.Globalization;
using Habitica.Domain.User;

namespace Habitica.Application.Inventory;

public sealed class InventoryViewModelFactory
{
    private const string TwoHandedWeaponsSlotTitle = "Two-Handed Weapons";
    private static readonly string[] SlotOrder =
    {
        "Head",
        "Head Accessory",
        "Eyewear",
        "Armor",
        "Body",
        "Weapon",
        "Shield",
        TwoHandedWeaponsSlotTitle,
        "Back",
        "Other"
    };

    private static readonly HashSet<string> MainBattleSlots = new(StringComparer.Ordinal)
    {
        "Head",
        "Head Accessory",
        "Eyewear",
        "Armor",
        "Body",
        "Weapon",
        "Shield",
        "Back"
    };

    public InventoryViewModel Create(
        UserSnapshot snapshot,
        GearCatalogSnapshot? catalog = null,
        IReadOnlyList<EquipmentPreset>? presets = null)
    {
        var battle = snapshot.Equipment.Battle;
        var costume = snapshot.Equipment.Costume;
        var presetList = presets ?? Array.Empty<EquipmentPreset>();

        var ownedItems = snapshot.Inventory.OwnedGearKeys
            .Where(key => !IsUnequippedBaseKey(key))
            .Select(key => BuildGearItem(key, snapshot.ClassName, battle, costume, catalog))
            .ToArray();

        var groups = ownedItems
            .Where(IsMainGearItem)
            .GroupBy(item => item.SlotTitle)
            .OrderBy(group => Array.IndexOf(SlotOrder, group.Key))
            .Select(group => new InventoryGearGroupViewModel(
                SlotTitle: group.Key,
                BattleEquippedKey: GetEquippedKey(group.Key, battle),
                CostumeEquippedKey: GetEquippedKey(group.Key, costume),
                BestItems: SelectBestItems(group),
                Items: group
                    .OrderBy(item => item.Key, StringComparer.Ordinal)
                    .ToArray()))
            .ToArray();

        var accessoryGroups = ownedItems
            .Where(item => !IsMainGearItem(item))
            .GroupBy(item => item.SlotTitle)
            .OrderBy(group => Array.IndexOf(SlotOrder, group.Key))
            .Select(group => new InventoryGearGroupViewModel(
                SlotTitle: group.Key,
                BattleEquippedKey: GetEquippedKey(group.Key, battle),
                CostumeEquippedKey: GetEquippedKey(group.Key, costume),
                BestItems: Array.Empty<InventoryGearItemViewModel>(),
                Items: group
                    .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .ToArray();

        return new InventoryViewModel(
            Groups: groups,
            AccessoryGroups: accessoryGroups,
            BattleEquipped: BuildEquippedSet(EquipmentSetKind.Battle, battle, snapshot.ClassName, catalog),
            CostumeEquipped: BuildEquippedSet(EquipmentSetKind.Costume, costume, snapshot.ClassName, catalog),
            BattlePresets: presetList
                .Where(preset => preset.Kind == EquipmentSetKind.Battle)
                .OrderBy(preset => preset.Name, StringComparer.OrdinalIgnoreCase)
                .Select(preset => BuildPreset(snapshot.ClassName, catalog, preset))
                .ToArray(),
            CostumePresets: presetList
                .Where(preset => preset.Kind == EquipmentSetKind.Costume)
                .OrderBy(preset => preset.Name, StringComparer.OrdinalIgnoreCase)
                .Select(preset => BuildPreset(snapshot.ClassName, catalog, preset))
                .ToArray());
    }

    private static string ParseSlotTitle(string key)
    {
        var normalized = key.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        if (normalized.StartsWith("headaccessory", StringComparison.Ordinal))
        {
            return "Head Accessory";
        }

        return key.Split('_', 2, StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant() switch
        {
            "head" => "Head",
            "armor" => "Armor",
            "weapon" => "Weapon",
            "shield" => "Shield",
            "back" => "Back",
            "eyewear" => "Eyewear",
            "body" => "Body",
            _ => "Other"
        };
    }

    private static string? GetEquippedKey(string slotTitle, GearSlotsSnapshot slots)
    {
        return slotTitle switch
        {
            "Head" => slots.Head,
            "Head Accessory" => slots.HeadAccessory,
            "Eyewear" => slots.Eyewear,
            "Armor" => slots.Armor,
            "Body" => slots.Body,
            "Weapon" or TwoHandedWeaponsSlotTitle => slots.Weapon,
            "Shield" => slots.Shield,
            "Back" => slots.Back,
            _ => null
        };
    }

    private static InventoryEquippedSetViewModel BuildEquippedSet(
        EquipmentSetKind kind,
        GearSlotsSnapshot slots,
        string? userClass,
        GearCatalogSnapshot? catalog)
    {
        var items = EnumerateSlots(slots)
            .Where(slot => !string.IsNullOrWhiteSpace(slot.Key))
            .Where(slot => !IsUnequippedBaseKey(slot.Key!))
            .Where(slot => kind != EquipmentSetKind.Battle || IsBattleManagedSlot(slot.SlotTitle))
            .Select(slot => new InventoryEquippedItemViewModel(
                SlotTitle: slot.SlotTitle,
                Key: slot.Key!,
                DisplayName: ResolveDisplayName(slot.Key!, catalog),
                TotalStats: kind == EquipmentSetKind.Battle ? CalculateStats(slot.Key!, userClass, catalog) : GearStatBlock.Zero))
            .ToArray();

        return new InventoryEquippedSetViewModel(
            Kind: kind,
            Items: items,
            TotalStats: kind == EquipmentSetKind.Battle
                ? items.Aggregate(GearStatBlock.Zero, (total, item) => total.Add(item.TotalStats))
                : GearStatBlock.Zero);
    }

    private static InventoryPresetViewModel BuildPreset(
        string? userClass,
        GearCatalogSnapshot? catalog,
        EquipmentPreset preset)
    {
        var items = EnumerateSlots(preset.Slots)
            .Where(slot => !string.IsNullOrWhiteSpace(slot.Key))
            .Where(slot => !IsUnequippedBaseKey(slot.Key!))
            .Where(slot => preset.Kind != EquipmentSetKind.Battle || IsBattleManagedSlot(slot.SlotTitle))
            .Select(slot => new InventoryPresetItemViewModel(
                SlotTitle: slot.SlotTitle,
                Key: slot.Key!,
                DisplayName: ResolveDisplayName(slot.Key!, catalog),
                TotalStats: preset.Kind == EquipmentSetKind.Battle ? CalculateStats(slot.Key!, userClass, catalog) : GearStatBlock.Zero))
            .ToArray();

        var totalStats = preset.Kind == EquipmentSetKind.Battle
            ? items.Aggregate(GearStatBlock.Zero, (total, item) => total.Add(item.TotalStats))
            : GearStatBlock.Zero;

        return new InventoryPresetViewModel(
            preset.Id,
            preset.Kind,
            preset.Name,
            preset.CreatedAtUtc,
            items,
            totalStats);
    }

    private static InventoryGearItemViewModel BuildGearItem(
        string key,
        string? userClass,
        GearSlotsSnapshot battle,
        GearSlotsSnapshot costume,
        GearCatalogSnapshot? catalog)
    {
        var slotTitle = ResolveSlotTitle(key, catalog);
        if (slotTitle == "Weapon" && catalog?.Items.TryGetValue(key, out var catalogItem) == true && catalogItem.TwoHanded)
        {
            slotTitle = TwoHandedWeaponsSlotTitle;
        }

        return new InventoryGearItemViewModel(
            Key: key,
            DisplayName: ResolveDisplayName(key, catalog),
            SlotTitle: slotTitle,
            ClassName: ResolveClassName(key, catalog),
            Notes: ResolveNotes(key, catalog),
            TotalStats: CalculateStats(key, userClass, catalog),
            BattleStatDelta: CalculateBattleDelta(key, slotTitle, userClass, battle, catalog),
            IsBattleEquipped: string.Equals(key, GetEquippedKey(slotTitle, battle), StringComparison.Ordinal),
            IsCostumeEquipped: string.Equals(key, GetEquippedKey(slotTitle, costume), StringComparison.Ordinal));
    }

    public EquipmentRecommendationViewModel CreateRecommendation(
        UserSnapshot snapshot,
        GearCatalogSnapshot? catalog,
        EquipmentOptimizationGoal goal)
    {
        var ownedItems = snapshot.Inventory.OwnedGearKeys
            .Where(key => !IsUnequippedBaseKey(key))
            .Select(key => BuildGearItem(key, snapshot.ClassName, snapshot.Equipment.Battle, snapshot.Equipment.Costume, catalog))
            .Where(IsMainGearItem)
            .ToArray();

        var recommendedSlots = new GearSlotsSnapshot(
            PickBestSlot(ownedItems, "Head", goal)?.Key,
            PickBestSlot(ownedItems, "Armor", goal)?.Key,
            null,
            null,
            PickBestSlot(ownedItems, "Back", goal)?.Key,
            PickBestSlot(ownedItems, "Head Accessory", goal)?.Key,
            PickBestSlot(ownedItems, "Eyewear", goal)?.Key,
            PickBestSlot(ownedItems, "Body", goal)?.Key);

        var oneHandedWeapon = PickBestSlot(ownedItems, "Weapon", goal);
        var shield = PickBestSlot(ownedItems, "Shield", goal);
        var twoHandedWeapon = PickBestSlot(ownedItems, TwoHandedWeaponsSlotTitle, goal);
        var oneHandedScore = ScoreStats(oneHandedWeapon?.TotalStats ?? GearStatBlock.Zero, goal)
            + ScoreStats(shield?.TotalStats ?? GearStatBlock.Zero, goal);
        var twoHandedScore = ScoreStats(twoHandedWeapon?.TotalStats ?? GearStatBlock.Zero, goal);

        recommendedSlots = twoHandedWeapon is not null && twoHandedScore > oneHandedScore
            ? recommendedSlots with { Weapon = twoHandedWeapon.Key, Shield = null }
            : recommendedSlots with { Weapon = oneHandedWeapon?.Key, Shield = shield?.Key };

        var recommendationItems = EnumerateSlots(recommendedSlots)
            .Where(slot => !string.IsNullOrWhiteSpace(slot.Key))
            .Select(slot => new EquipmentRecommendationItemViewModel(
                SlotTitle: slot.SlotTitle,
                Key: slot.Key!,
                DisplayName: ResolveDisplayName(slot.Key!, catalog),
                TotalStats: CalculateStats(slot.Key!, snapshot.ClassName, catalog)))
            .ToArray();
        var currentStats = BuildEquippedSet(EquipmentSetKind.Battle, snapshot.Equipment.Battle, snapshot.ClassName, catalog).TotalStats;
        var recommendedStats = recommendationItems.Aggregate(GearStatBlock.Zero, (total, item) => total.Add(item.TotalStats));

        return new EquipmentRecommendationViewModel(
            goal,
            GetGoalLabel(goal),
            recommendedSlots,
            recommendationItems,
            currentStats,
            recommendedStats,
            recommendedStats.Subtract(currentStats),
            recommendationItems.Count() > 0);
    }

    private static InventoryGearItemViewModel? PickBestSlot(
        IReadOnlyList<InventoryGearItemViewModel> candidates,
        string slotTitle,
        EquipmentOptimizationGoal goal)
    {
        return candidates
            .Where(item => string.Equals(item.SlotTitle, slotTitle, StringComparison.Ordinal))
            .OrderByDescending(item => ScoreStats(item.TotalStats, goal))
            .ThenByDescending(item => item.TotalStats.Strength + item.TotalStats.Intelligence + item.TotalStats.Constitution + item.TotalStats.Perception)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(item => ScoreStats(item.TotalStats, goal) > 0m);
    }

    private static bool IsMainGearItem(InventoryGearItemViewModel item)
    {
        return IsBattleManagedSlot(item.SlotTitle) && item.TotalStats != GearStatBlock.Zero;
    }

    private static bool IsBattleManagedSlot(string slotTitle)
    {
        return MainBattleSlots.Contains(slotTitle) || string.Equals(slotTitle, TwoHandedWeaponsSlotTitle, StringComparison.Ordinal);
    }

    private static IReadOnlyList<InventoryGearItemViewModel> SelectBestItems(IEnumerable<InventoryGearItemViewModel> items)
    {
        var itemArray = items.ToArray();
        return itemArray
            .Where(item => item.TotalStats != GearStatBlock.Zero)
            .Where(item => !itemArray.Any(candidate => Dominates(candidate.TotalStats, item.TotalStats)))
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool Dominates(GearStatBlock candidate, GearStatBlock item)
    {
        return candidate.Strength >= item.Strength
            && candidate.Intelligence >= item.Intelligence
            && candidate.Constitution >= item.Constitution
            && candidate.Perception >= item.Perception
            && (candidate.Strength > item.Strength
                || candidate.Intelligence > item.Intelligence
                || candidate.Constitution > item.Constitution
                || candidate.Perception > item.Perception);
    }

    private static bool IsUnequippedBaseKey(string key)
    {
        return key.EndsWith("_base_0", StringComparison.OrdinalIgnoreCase);
    }

    private static GearStatBlock CalculateBattleDelta(
        string key,
        string slotTitle,
        string? userClass,
        GearSlotsSnapshot battle,
        GearCatalogSnapshot? catalog)
    {
        var currentStats = EnumerateSlots(battle)
            .Where(slot => !string.IsNullOrWhiteSpace(slot.Key))
            .Where(slot => !IsUnequippedBaseKey(slot.Key!))
            .Aggregate(GearStatBlock.Zero, (total, slot) => total.Add(CalculateStats(slot.Key!, userClass, catalog)));
        var nextSlots = ApplyBattleGearKey(battle, key, slotTitle, catalog);
        var nextStats = EnumerateSlots(nextSlots)
            .Where(slot => !string.IsNullOrWhiteSpace(slot.Key))
            .Where(slot => !IsUnequippedBaseKey(slot.Key!))
            .Aggregate(GearStatBlock.Zero, (total, slot) => total.Add(CalculateStats(slot.Key!, userClass, catalog)));

        return nextStats.Subtract(currentStats);
    }

    private static GearSlotsSnapshot ApplyBattleGearKey(
        GearSlotsSnapshot slots,
        string key,
        string slotTitle,
        GearCatalogSnapshot? catalog)
    {
        return slotTitle switch
        {
            "Head" => slots with { Head = key },
            "Head Accessory" => slots with { HeadAccessory = key },
            "Eyewear" => slots with { Eyewear = key },
            "Armor" => slots with { Armor = key },
            "Body" => slots with { Body = key },
            "Weapon" => slots with { Weapon = key },
            "Shield" => IsTwoHanded(slots.Weapon, catalog)
                ? slots with { Weapon = null, Shield = key }
                : slots with { Shield = key },
            TwoHandedWeaponsSlotTitle => slots with { Weapon = key, Shield = null },
            "Back" => slots with { Back = key },
            _ => slots
        };
    }

    private static bool IsTwoHanded(string? key, GearCatalogSnapshot? catalog)
    {
        return !string.IsNullOrWhiteSpace(key)
            && catalog?.Items.TryGetValue(key, out var item) == true
            && item.TwoHanded;
    }

    private static decimal ScoreStats(GearStatBlock stats, EquipmentOptimizationGoal goal)
    {
        return goal switch
        {
            // Single-stat goals strictly maximize the prioritized stat. Other stats
            // never override a higher prioritized value; they only act as a
            // tie-breaker (handled by PickBestSlot) when the prioritized stat is equal.
            EquipmentOptimizationGoal.Strength => stats.Strength,
            EquipmentOptimizationGoal.Intelligence => stats.Intelligence,
            EquipmentOptimizationGoal.Constitution => stats.Constitution,
            EquipmentOptimizationGoal.Perception => stats.Perception,
            EquipmentOptimizationGoal.BossDamage => stats.Strength * 2.5m + stats.Perception * 1.1m + stats.Constitution * 0.4m,
            EquipmentOptimizationGoal.Survival => stats.Constitution * 2.4m + stats.Intelligence * 0.8m + stats.Strength * 0.6m,
            _ => stats.Strength + stats.Intelligence + stats.Constitution + stats.Perception
        };
    }

    private static string GetGoalLabel(EquipmentOptimizationGoal goal)
    {
        return goal switch
        {
            EquipmentOptimizationGoal.Strength => "Strength",
            EquipmentOptimizationGoal.Intelligence => "Intelligence",
            EquipmentOptimizationGoal.Constitution => "Constitution",
            EquipmentOptimizationGoal.Perception => "Perception",
            EquipmentOptimizationGoal.BossDamage => "Boss damage",
            EquipmentOptimizationGoal.Survival => "Survival",
            _ => "Balanced"
        };
    }

    private static GearStatBlock CalculateStats(string key, string? userClass, GearCatalogSnapshot? catalog)
    {
        if (catalog is null || !catalog.Items.TryGetValue(key, out var item))
        {
            return GearStatBlock.Zero;
        }

        var multiplier = !string.IsNullOrWhiteSpace(userClass)
            && !string.IsNullOrWhiteSpace(item.ClassName)
            && string.Equals(userClass, item.ClassName, StringComparison.OrdinalIgnoreCase)
            ? 1.5m
            : 1m;

        return item.Stats.Scale(multiplier);
    }

    private static string ResolveDisplayName(string key, GearCatalogSnapshot? catalog)
    {
        if (catalog?.Items.TryGetValue(key, out var item) == true && !string.IsNullOrWhiteSpace(item.Text))
        {
            return item.Text;
        }

        var parts = key.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "Unknown gear";
        }

        var readableParts = parts
            .Where(static part => !int.TryParse(part, out _))
            .Select(static part => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(part.Replace('-', ' ')))
            .ToArray();

        return readableParts.Length == 0 ? "Unknown gear" : string.Join(' ', readableParts);
    }

    private static string ResolveSlotTitle(string key, GearCatalogSnapshot? catalog)
    {
        return catalog?.Items.TryGetValue(key, out var item) == true && !string.IsNullOrWhiteSpace(item.SlotTitle)
            ? item.SlotTitle
            : ParseSlotTitle(key);
    }

    private static string? ResolveClassName(string key, GearCatalogSnapshot? catalog)
    {
        return catalog?.Items.TryGetValue(key, out var item) == true ? item.ClassName : null;
    }

    private static string? ResolveNotes(string key, GearCatalogSnapshot? catalog)
    {
        return catalog?.Items.TryGetValue(key, out var item) == true ? item.Notes : null;
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

public sealed record InventoryViewModel(
    IReadOnlyList<InventoryGearGroupViewModel> Groups,
    IReadOnlyList<InventoryGearGroupViewModel> AccessoryGroups,
    InventoryEquippedSetViewModel BattleEquipped,
    InventoryEquippedSetViewModel CostumeEquipped,
    IReadOnlyList<InventoryPresetViewModel> BattlePresets,
    IReadOnlyList<InventoryPresetViewModel> CostumePresets);

public sealed record InventoryGearGroupViewModel(
    string SlotTitle,
    string? BattleEquippedKey,
    string? CostumeEquippedKey,
    IReadOnlyList<InventoryGearItemViewModel> BestItems,
    IReadOnlyList<InventoryGearItemViewModel> Items);

public sealed record InventoryGearItemViewModel(
    string Key,
    string DisplayName,
    string SlotTitle,
    string? ClassName,
    string? Notes,
    GearStatBlock TotalStats,
    GearStatBlock BattleStatDelta,
    bool IsBattleEquipped,
    bool IsCostumeEquipped);

public sealed record InventoryEquippedSetViewModel(
    EquipmentSetKind Kind,
    IReadOnlyList<InventoryEquippedItemViewModel> Items,
    GearStatBlock TotalStats);

public sealed record InventoryEquippedItemViewModel(
    string SlotTitle,
    string Key,
    string DisplayName,
    GearStatBlock TotalStats);

public sealed record InventoryPresetViewModel(
    string Id,
    EquipmentSetKind Kind,
    string Name,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<InventoryPresetItemViewModel> Items,
    GearStatBlock TotalStats);

public sealed record InventoryPresetItemViewModel(
    string SlotTitle,
    string Key,
    string DisplayName,
    GearStatBlock TotalStats);

public enum EquipmentOptimizationGoal
{
    Balanced,
    Strength,
    Intelligence,
    Constitution,
    Perception,
    BossDamage,
    Survival
}

public sealed record EquipmentRecommendationViewModel(
    EquipmentOptimizationGoal Goal,
    string GoalLabel,
    GearSlotsSnapshot Slots,
    IReadOnlyList<EquipmentRecommendationItemViewModel> Items,
    GearStatBlock CurrentStats,
    GearStatBlock RecommendedStats,
    GearStatBlock Delta,
    bool HasRecommendation);

public sealed record EquipmentRecommendationItemViewModel(
    string SlotTitle,
    string Key,
    string DisplayName,
    GearStatBlock TotalStats);
