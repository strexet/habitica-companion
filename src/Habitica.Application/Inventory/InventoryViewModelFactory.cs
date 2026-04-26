using Habitica.Domain.User;

namespace Habitica.Application.Inventory;

public sealed class InventoryViewModelFactory
{
    private static readonly string[] SlotOrder = { "Head", "Armor", "Weapon", "Shield", "Back", "Other" };

    public InventoryViewModel Create(UserSnapshot snapshot)
    {
        var battle = snapshot.Equipment.Battle;
        var costume = snapshot.Equipment.Costume;

        var groups = snapshot.Inventory.OwnedGearKeys
            .GroupBy(ParseSlotTitle)
            .OrderBy(group => Array.IndexOf(SlotOrder, group.Key))
            .Select(group => new InventoryGearGroupViewModel(
                SlotTitle: group.Key,
                BattleEquippedKey: GetEquippedKey(group.Key, battle),
                CostumeEquippedKey: GetEquippedKey(group.Key, costume),
                Items: group
                    .OrderBy(key => key, StringComparer.Ordinal)
                    .Select(key => new InventoryGearItemViewModel(
                        Key: key,
                        IsBattleEquipped: string.Equals(key, GetEquippedKey(group.Key, battle), StringComparison.Ordinal),
                        IsCostumeEquipped: string.Equals(key, GetEquippedKey(group.Key, costume), StringComparison.Ordinal)))
                    .ToArray()))
            .ToArray();

        return new InventoryViewModel(groups);
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
}

public sealed record InventoryViewModel(
    IReadOnlyList<InventoryGearGroupViewModel> Groups);

public sealed record InventoryGearGroupViewModel(
    string SlotTitle,
    string? BattleEquippedKey,
    string? CostumeEquippedKey,
    IReadOnlyList<InventoryGearItemViewModel> Items);

public sealed record InventoryGearItemViewModel(
    string Key,
    bool IsBattleEquipped,
    bool IsCostumeEquipped);
