using Habitica.Domain.User;

namespace Habitica.Application.Inventory;

public sealed class InventoryViewModelFactory
{
    private static readonly string[] SlotOrder = { "Head", "Armor", "Weapon", "Shield", "Back", "Other" };
    private static readonly HashSet<string> MainBattleSlots = new(StringComparer.Ordinal)
    {
        "Head",
        "Armor",
        "Weapon",
        "Shield"
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
        return key.Split('_', 2, StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant() switch
        {
            "head" => "Head",
            "armor" => "Armor",
            "weapon" => "Weapon",
            "shield" => "Shield",
            "back" => "Back",
            _ => "Other"
        };
    }

    private static string? GetEquippedKey(string slotTitle, GearSlotsSnapshot slots)
    {
        return slotTitle switch
        {
            "Head" => slots.Head,
            "Armor" => slots.Armor,
            "Weapon" => slots.Weapon,
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
        return new InventoryGearItemViewModel(
            Key: key,
            DisplayName: ResolveDisplayName(key, catalog),
            SlotTitle: slotTitle,
            ClassName: ResolveClassName(key, catalog),
            Notes: ResolveNotes(key, catalog),
            TotalStats: CalculateStats(key, userClass, catalog),
            IsBattleEquipped: string.Equals(key, GetEquippedKey(slotTitle, battle), StringComparison.Ordinal),
            IsCostumeEquipped: string.Equals(key, GetEquippedKey(slotTitle, costume), StringComparison.Ordinal));
    }

    private static bool IsMainGearItem(InventoryGearItemViewModel item)
    {
        return IsBattleManagedSlot(item.SlotTitle) && item.TotalStats != GearStatBlock.Zero;
    }

    private static bool IsBattleManagedSlot(string slotTitle)
    {
        return MainBattleSlots.Contains(slotTitle);
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
        return catalog?.Items.TryGetValue(key, out var item) == true && !string.IsNullOrWhiteSpace(item.Text)
            ? item.Text
            : key;
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
        yield return ("Armor", slots.Armor);
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
