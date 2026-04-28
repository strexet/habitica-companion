namespace Habitica.Domain.User;

public sealed record UserSnapshot(
    DateTimeOffset RetrievedAtUtc,
    string DisplayName,
    string? ClassName,
    int Level,
    decimal Health,
    decimal MaxHealth,
    decimal Mana,
    decimal MaxMana,
    decimal Experience,
    decimal ToNextLevel,
    decimal Gold,
    string? PartyId,
    string? CurrentPetKey,
    string? CurrentMountKey,
    EquipmentSnapshot Equipment,
    InventorySnapshot Inventory);

public sealed record EquipmentSnapshot(
    GearSlotsSnapshot Battle,
    GearSlotsSnapshot Costume);

public sealed record GearSlotsSnapshot(
    string? Head,
    string? Armor,
    string? Weapon,
    string? Shield,
    string? Back);

public sealed record InventorySnapshot(
    int EggCount,
    int FoodCount,
    int HatchingPotionCount,
    int QuestCount,
    int OwnedPetCount,
    int OwnedMountCount,
    string[] OwnedGearKeys);

public enum EquipmentSetKind
{
    Battle,
    Costume
}

public sealed record GearStatBlock(
    decimal Strength,
    decimal Intelligence,
    decimal Constitution,
    decimal Perception)
{
    public static GearStatBlock Zero { get; } = new(0m, 0m, 0m, 0m);

    public GearStatBlock Add(GearStatBlock other)
    {
        return new GearStatBlock(
            Strength + other.Strength,
            Intelligence + other.Intelligence,
            Constitution + other.Constitution,
            Perception + other.Perception);
    }

    public GearStatBlock Scale(decimal multiplier)
    {
        return new GearStatBlock(
            Strength * multiplier,
            Intelligence * multiplier,
            Constitution * multiplier,
            Perception * multiplier);
    }
}

public sealed record GearCatalogSnapshot(
    DateTimeOffset RetrievedAtUtc,
    IReadOnlyDictionary<string, GearCatalogItem> Items);

public sealed record GearCatalogItem(
    string Key,
    string Text,
    string SlotTitle,
    string? ClassName,
    string? Notes,
    GearStatBlock Stats,
    bool TwoHanded = false);

public sealed record EquipmentPreset(
    string Id,
    string UserId,
    EquipmentSetKind Kind,
    string Name,
    DateTimeOffset CreatedAtUtc,
    GearSlotsSnapshot Slots);
