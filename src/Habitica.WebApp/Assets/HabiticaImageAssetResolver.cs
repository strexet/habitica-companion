using System.Globalization;

namespace Habitica.WebApp.Assets;

public static class HabiticaImageAssetResolver
{
    public const string OfficialMobileImageBaseUrl = "https://habitica-assets.s3.amazonaws.com/mobileApp/images/";

    public static HabiticaImageAsset Gear(string key, string? displayName = null)
    {
        return Resolve(
            HabiticaImageKind.Gear,
            key,
            displayName,
            HabiticaImageSize.Medium,
            $"shop_{key}.png");
    }

    public static HabiticaImageAsset Skill(string key, string? displayName = null)
    {
        return Resolve(
            HabiticaImageKind.Skill,
            key,
            displayName,
            HabiticaImageSize.Medium,
            $"shop_{key}.png");
    }

    public static HabiticaImageAsset Pet(string? key, string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Missing(HabiticaImageKind.Pet, "none", displayName ?? "No pet", HabiticaImageSize.Small);
        }

        return Resolve(
            HabiticaImageKind.Pet,
            key,
            displayName,
            HabiticaImageSize.Small,
            $"Pet-{key}.png");
    }

    public static HabiticaImageAsset Mount(string? key, string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Missing(HabiticaImageKind.Mount, "none", displayName ?? "No mount", HabiticaImageSize.Small);
        }

        return Resolve(
            HabiticaImageKind.Mount,
            key,
            displayName,
            HabiticaImageSize.Small,
            $"Mount_Icon_{key}.png");
    }

    public static HabiticaImageAsset Quest(string? key, string? displayName = null, HabiticaImageSize size = HabiticaImageSize.Medium)
    {
        return Resolve(
            HabiticaImageKind.Quest,
            string.IsNullOrWhiteSpace(key) ? "quest-scroll" : key,
            displayName,
            size,
            "inventory_quest_scroll.png");
    }

    public static HabiticaImageAsset EggSummary()
    {
        return Resolve(HabiticaImageKind.Inventory, "egg", "Eggs", HabiticaImageSize.Small, "Pet_Egg_Wolf.png");
    }

    public static HabiticaImageAsset FoodSummary()
    {
        return Resolve(HabiticaImageKind.Inventory, "food", "Food", HabiticaImageSize.Small, "Pet_Food_Meat.png");
    }

    public static HabiticaImageAsset HatchingPotionSummary()
    {
        return Resolve(HabiticaImageKind.Inventory, "hatching-potion", "Hatching potions", HabiticaImageSize.Small, "Pet_HatchingPotion_Base.png");
    }

    public static HabiticaImageAsset QuestScrollSummary()
    {
        return Resolve(HabiticaImageKind.Quest, "quest-scroll", "Quest scrolls", HabiticaImageSize.Small, "inventory_quest_scroll.png");
    }

    public static HabiticaImageAsset Achievement(string key, string? displayName = null)
    {
        return Resolve(
            HabiticaImageKind.Achievement,
            key,
            displayName,
            HabiticaImageSize.Medium,
            $"{key}2x.png");
    }

    public static HabiticaImageAsset Missing(
        HabiticaImageKind kind,
        string key,
        string label,
        HabiticaImageSize size = HabiticaImageSize.Medium)
    {
        return new HabiticaImageAsset(
            kind,
            string.IsNullOrWhiteSpace(key) ? "unknown" : key,
            label,
            BuildFallbackLabel(label),
            size,
            null);
    }

    private static HabiticaImageAsset Resolve(
        HabiticaImageKind kind,
        string key,
        string? displayName,
        HabiticaImageSize size,
        string fileName)
    {
        var label = string.IsNullOrWhiteSpace(displayName) ? ToReadableName(key) : displayName;
        return new HabiticaImageAsset(
            kind,
            key,
            label,
            BuildFallbackLabel(label),
            size,
            $"{OfficialMobileImageBaseUrl}{fileName}");
    }

    private static string ToReadableName(string key)
    {
        var parts = key.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0
            ? "Habitica item"
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(string.Join(' ', parts));
    }

    private static string BuildFallbackLabel(string label)
    {
        var words = label.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return "?";
        }

        var letters = words
            .Take(2)
            .Select(static word => char.ToUpperInvariant(word[0]))
            .ToArray();

        return new string(letters);
    }
}
