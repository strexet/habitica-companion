namespace Habitica.Domain.User;

public static class PetsMountsCatalog
{
    private static readonly string[] DropEggKeys =
    {
        "Wolf", "TigerCub", "PandaCub", "LionCub", "Fox", "FlyingPig", "Dragon", "Cactus", "BearCub"
    };

    private static readonly string[] QuestEggKeys =
    {
        "Gryphon", "Hedgehog", "Deer", "Egg", "Rat", "Octopus", "Seahorse", "Parrot", "Rooster", "Spider",
        "Owl", "Penguin", "TRex", "Rock", "Bunny", "Slime", "Sheep", "Cuttlefish", "Whale", "Cheetah",
        "Horse", "Frog", "Snake", "Unicorn", "Sabretooth", "Monkey", "Snail", "Falcon", "Treeling", "Axolotl",
        "Turtle", "Armadillo", "Cow", "Beetle", "Ferret", "Sloth", "Triceratops", "GuineaPig", "Peacock",
        "Butterfly", "Nudibranch", "Hippo", "Yarn", "Pterodactyl", "Badger", "Squirrel", "SeaSerpent",
        "Kangaroo", "Alligator", "Velociraptor", "Dolphin", "Robot", "Giraffe", "Chameleon", "Crab", "Raccoon",
        "Dog", "Cat", "Otter", "Alpaca", "Platypus"
    };

    private static readonly string[] DropPotionKeys =
    {
        "Base", "White", "Desert", "Red", "Shade", "Skeleton", "Zombie", "CottonCandyPink", "CottonCandyBlue", "Golden"
    };

    private static readonly string[] PremiumPotionKeys =
    {
        "RoyalPurple", "Cupid", "Shimmer", "Fairy", "Floral", "Aquatic", "Ember", "Thunderstorm", "Spooky",
        "Ghost", "Holly", "Peppermint", "StarryNight", "Rainbow", "Glass", "Glow", "Frost", "IcySnow",
        "RoseQuartz", "Celestial", "Sunshine", "Bronze", "Watery", "Silver", "Shadow", "Amber", "Aurora",
        "Ruby", "BirchBark", "Fluorite", "SandSculpture", "Windup", "Turquoise", "Vampire", "AutumnLeaf",
        "BlackPearl", "StainedGlass", "PolkaDot", "MossyStone", "Sunset", "Moonglow", "SolarSystem", "Onyx",
        "Porcelain", "PinkMarble", "RoseGold", "Koi", "Gingerbread", "Jade", "Balloon", "Opal"
    };

    private static readonly string[] WackyPotionKeys =
    {
        "Veggie", "Dessert", "VirtualPet", "TeaShop", "Fungi", "Cryptid", "Alien"
    };

    public static IReadOnlyList<CompanionCatalogGroup> Groups { get; } =
    [
        new("base", "Base collection"),
        new("magic-potion", "Magic potion collection"),
        new("quest", "Quest collection"),
        new("premium", "Premium collection"),
        new("wacky", "Wacky collection"),
        new("special", "Special and other")
    ];

    public static IReadOnlyList<FoodCatalogItem> Food { get; } =
    [
        new("Meat", "Meat", "Base"),
        new("Milk", "Milk", "White"),
        new("Potatoe", "Potato", "Desert"),
        new("Strawberry", "Strawberry", "Red"),
        new("Chocolate", "Chocolate", "Shade"),
        new("Fish", "Fish", "Skeleton"),
        new("RottenMeat", "Rotten meat", "Zombie"),
        new("CottonCandyPink", "Pink cotton candy", "CottonCandyPink"),
        new("CottonCandyBlue", "Blue cotton candy", "CottonCandyBlue"),
        new("Honey", "Honey", "Golden"),
        new("Saddle", "Saddle", null, true)
    ];

    public static IReadOnlyList<HatchingPotionCatalogItem> HatchingPotions { get; } =
        BuildHatchingPotions();

    public static IReadOnlyList<PetCatalogItem> Pets { get; } =
        BuildPets();

    public static IReadOnlyList<MountCatalogItem> Mounts { get; } =
        Pets
            .Where(static pet => pet.GroupKey != "wacky")
            .Select(static pet => new MountCatalogItem(
                pet.Key,
                $"{pet.DisplayName} mount",
                pet.GroupKey))
            .ToArray();

    public static PetCatalogItem? FindPet(string key)
    {
        return Pets.FirstOrDefault(pet => string.Equals(pet.Key, key, StringComparison.Ordinal));
    }

    public static MountCatalogItem? FindMount(string key)
    {
        return Mounts.FirstOrDefault(mount => string.Equals(mount.Key, key, StringComparison.Ordinal));
    }

    public static bool TryGetGrowableMountKey(PetCatalogItem pet, out string mountKey)
    {
        if (FindMount(pet.Key) is not null)
        {
            mountKey = pet.Key;
            return true;
        }

        mountKey = string.Empty;
        return false;
    }

    public static bool TryGetPetKeyForMount(string mountKey, out string petKey)
    {
        if (FindMount(mountKey) is not null)
        {
            petKey = mountKey;
            return true;
        }

        petKey = string.Empty;
        return false;
    }

    public static bool TryGetCreatureTypeKey(PetCatalogItem pet, out string creatureTypeKey)
    {
        if (!string.IsNullOrWhiteSpace(pet.EggKey))
        {
            creatureTypeKey = pet.EggKey;
            return true;
        }

        return TryGetCreatureTypeKey(pet.Key, out creatureTypeKey);
    }

    public static bool TryGetCreatureTypeKey(MountCatalogItem mount, out string creatureTypeKey)
    {
        if (FindPet(mount.Key) is { } pet)
        {
            creatureTypeKey = pet.EggKey;
            return true;
        }

        return TryGetCreatureTypeKey(mount.Key, out creatureTypeKey);
    }

    public static bool TryGetCreatureTypeKey(string companionKey, out string creatureTypeKey)
    {
        if (string.IsNullOrWhiteSpace(companionKey))
        {
            creatureTypeKey = string.Empty;
            return false;
        }

        var separatorIndex = companionKey.IndexOf('-', StringComparison.Ordinal);
        var candidate = separatorIndex > 0
            ? companionKey[..separatorIndex]
            : companionKey;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            creatureTypeKey = string.Empty;
            return false;
        }

        creatureTypeKey = candidate;
        return true;
    }

    public static string ToCreatureTypeDisplayName(string creatureTypeKey)
    {
        return ToReadableName(creatureTypeKey);
    }

    public static string ToReadableName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var characters = new List<char>(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if ((character is '_' or '-') && characters.Count > 0 && characters[^1] != ' ')
            {
                characters.Add(' ');
                continue;
            }

            if (index > 0
                && char.IsUpper(character)
                && char.IsLower(value[index - 1])
                && characters[^1] != ' ')
            {
                characters.Add(' ');
            }

            characters.Add(character);
        }

        return string.Join(
            ' ',
            new string(characters.ToArray())
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(static part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

    private static PetCatalogItem[] BuildPets()
    {
        return BuildPetSet(DropEggKeys, ["Base"], "base")
            .Concat(BuildPetSet(DropEggKeys, DropPotionKeys.Skip(1), "magic-potion"))
            .Concat(BuildPetSet(QuestEggKeys, DropPotionKeys, "quest"))
            .Concat(BuildPetSet(DropEggKeys, PremiumPotionKeys, "premium"))
            .Concat(BuildPetSet(DropEggKeys, WackyPotionKeys, "wacky"))
            .ToArray();
    }

    private static IEnumerable<PetCatalogItem> BuildPetSet(
        IEnumerable<string> eggKeys,
        IEnumerable<string> potionKeys,
        string groupKey)
    {
        foreach (var eggKey in eggKeys)
        {
            foreach (var potionKey in potionKeys)
            {
                yield return new PetCatalogItem(
                    $"{eggKey}-{potionKey}",
                    $"{ToReadableName(potionKey)} {ToReadableName(eggKey)}",
                    eggKey,
                    potionKey,
                    groupKey);
            }
        }
    }

    private static HatchingPotionCatalogItem[] BuildHatchingPotions()
    {
        return DropPotionKeys.Select(static key => new HatchingPotionCatalogItem(key, ToReadableName(key), "drop"))
            .Concat(PremiumPotionKeys.Select(static key => new HatchingPotionCatalogItem(key, ToReadableName(key), "premium")))
            .Concat(WackyPotionKeys.Select(static key => new HatchingPotionCatalogItem(key, ToReadableName(key), "wacky")))
            .ToArray();
    }
}

public sealed record CompanionCatalogGroup(string Key, string DisplayName);

public sealed record PetCatalogItem(
    string Key,
    string DisplayName,
    string EggKey,
    string HatchingPotionKey,
    string GroupKey);

public sealed record MountCatalogItem(
    string Key,
    string DisplayName,
    string GroupKey);

public sealed record FoodCatalogItem(
    string Key,
    string DisplayName,
    string? TargetPotionKey,
    bool IsGeneric = false);

public sealed record HatchingPotionCatalogItem(
    string Key,
    string DisplayName,
    string GroupKey);
