namespace Habitica.WebApp.Assets;

public sealed record HabiticaImageAsset(
    HabiticaImageKind Kind,
    string Key,
    string AltText,
    string FallbackText,
    HabiticaImageSize Size,
    string? SourceUrl);

public enum HabiticaImageKind
{
    Gear,
    Quest,
    Skill,
    Pet,
    Mount,
    Inventory,
    Achievement,
    Unknown
}

public enum HabiticaImageSize
{
    Small,
    Medium,
    Large
}
