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
    InventorySnapshot Inventory,
    int UnallocatedStatPoints = 0,
    CharacterStatsSnapshot? Stats = null,
    CharacterStatsSnapshot? Buffs = null,
    BuffFlagsSnapshot? BuffFlags = null);

public sealed record CharacterStatsSnapshot(
    decimal Strength,
    decimal Intelligence,
    decimal Constitution,
    decimal Perception)
{
    public static CharacterStatsSnapshot Zero { get; } = new(0m, 0m, 0m, 0m);
}

public sealed record BuffFlagsSnapshot(
    bool ChillingFrost,
    int Stealth)
{
    public static BuffFlagsSnapshot Empty { get; } = new(false, 0);
}

public sealed record StatAllocation(
    int Strength,
    int Intelligence,
    int Constitution,
    int Perception);

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
    string[] OwnedGearKeys,
    IReadOnlyDictionary<string, int>? OwnedQuestScrolls = null)
{
    public IReadOnlyDictionary<string, int> QuestScrolls => OwnedQuestScrolls ?? EmptyQuestScrolls;

    private static readonly IReadOnlyDictionary<string, int> EmptyQuestScrolls =
        new Dictionary<string, int>(StringComparer.Ordinal);
}

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
    IReadOnlyDictionary<string, GearCatalogItem> Items,
    IReadOnlyDictionary<string, QuestCatalogItem>? Quests = null)
{
    public IReadOnlyDictionary<string, QuestCatalogItem> QuestItems => Quests ?? EmptyQuestCatalog;

    private static readonly IReadOnlyDictionary<string, QuestCatalogItem> EmptyQuestCatalog =
        new Dictionary<string, QuestCatalogItem>(StringComparer.Ordinal);
}

public sealed record GearCatalogItem(
    string Key,
    string Text,
    string SlotTitle,
    string? ClassName,
    string? Notes,
    GearStatBlock Stats,
    bool TwoHanded = false);

public sealed record QuestCatalogItem(
    string Key,
    string Text,
    string? Notes,
    string Category,
    string QuestType,
    IReadOnlyList<string> RewardSummary);

public sealed record EquipmentPreset(
    string Id,
    string UserId,
    EquipmentSetKind Kind,
    string Name,
    DateTimeOffset CreatedAtUtc,
    GearSlotsSnapshot Slots);
