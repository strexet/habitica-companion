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
