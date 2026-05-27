using System.Text.RegularExpressions;

namespace Habitica.WebApp.Theme;

public static partial class ColorSchemeCatalog
{
    public const string AlphaId = "alpha";

    public static IReadOnlyList<ColorSchemeDefinition> BuiltInSchemes { get; } = new[]
    {
        new ColorSchemeDefinition(
            AlphaId,
            "Alpha",
            true,
            new ColorSchemeTokens(
                "#f5efe2",
                "rgba(255, 250, 241, 0.92)",
                "rgba(23, 63, 59, 0.12)",
                "#162423",
                "#5f6d67",
                "#2d746e",
                "#c5772b",
                "#a13f35",
                "#2d746e",
                "#43a397",
                "0 24px 60px rgba(22, 36, 35, 0.12)",
                "rgba(255, 255, 255, 0.72)",
                "rgba(255, 250, 241, 0.94)",
                "#2d746e",
                "#c5772b",
                "#f4b6a6",
                "#f7ead1",
                "#8ccfbd")),
        new ColorSchemeDefinition(
            "habitica",
            "Habitica",
            true,
            new ColorSchemeTokens(
                "#f7f4ff",
                "rgba(255, 255, 255, 0.94)",
                "rgba(79, 42, 147, 0.16)",
                "#24113f",
                "#6f6384",
                "#6133b4",
                "#ffbe5d",
                "#de3f5f",
                "#24cc8f",
                "#2995cd",
                "0 24px 60px rgba(44, 24, 82, 0.14)",
                "rgba(255, 255, 255, 0.78)",
                "rgba(255, 255, 255, 0.96)",
                "#6133b4",
                "#2995cd",
                "#de3f5f",
                "#e7dcff",
                "#24cc8f")),
        new ColorSchemeDefinition(
            "gryphy-light",
            "Gryphy Light",
            true,
            new ColorSchemeTokens(
                "#f7f1ff",
                "rgba(255, 252, 255, 0.94)",
                "rgba(103, 49, 184, 0.16)",
                "#201136",
                "#6a5c77",
                "#7b2dd6",
                "#f0a400",
                "#d94768",
                "#1f9f7a",
                "#3197e5",
                "0 24px 60px rgba(32, 17, 54, 0.14)",
                "rgba(255, 255, 255, 0.78)",
                "rgba(255, 252, 255, 0.96)",
                "#7b2dd6",
                "#3197e5",
                "#ff8ca0",
                "#eadcff",
                "#ffcf26")),
        new ColorSchemeDefinition(
            "gryphy-dark",
            "Gryphy Dark",
            true,
            new ColorSchemeTokens(
                "#12081f",
                "rgba(31, 15, 50, 0.94)",
                "rgba(178, 93, 255, 0.22)",
                "#fff7ff",
                "#c8b9d8",
                "#b25dff",
                "#ffcf26",
                "#ff6f8f",
                "#62d6bd",
                "#58b9ff",
                "0 24px 60px rgba(0, 0, 0, 0.36)",
                "rgba(50, 24, 80, 0.78)",
                "rgba(42, 20, 70, 0.96)",
                "#b25dff",
                "#58b9ff",
                "#ff6f8f",
                "#3d2464",
                "#ffcf26"))
    };

    public static IReadOnlyList<ColorSchemeTokenDescriptor> EditableTokens { get; } = new[]
    {
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.Background), "Background"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.CardBackground), "Card background"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.CardBorder), "Card border"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.Ink), "Text"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.Muted), "Muted text"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.Primary), "Primary"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.Accent), "Accent"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.Danger), "Danger"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.Success), "Success"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.Focus), "Focus"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.Shadow), "Shadow"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.Surface), "Surface"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.SurfaceStrong), "Strong surface"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.ChartPrimary), "Chart primary"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.ChartSecondary), "Chart secondary"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.TaskNegative), "Task negative"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.TaskNeutral), "Task neutral"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.TaskPositive), "Task positive")
    };

    public static ColorSchemeDefinition Alpha => BuiltInSchemes[0];

    public static ColorSchemeDefinition Resolve(string? schemeId, IReadOnlyList<ColorSchemeDefinition> customSchemes)
    {
        return BuiltInSchemes.Concat(customSchemes)
            .FirstOrDefault(scheme => string.Equals(scheme.Id, schemeId, StringComparison.Ordinal))
            ?? Alpha;
    }

    public static ColorSchemeDefinition CreateCustomCopy(ColorSchemeDefinition source, string name)
    {
        return new ColorSchemeDefinition(
            $"custom-{Guid.NewGuid():N}",
            NormalizeName(name, "Custom scheme"),
            false,
            source.Tokens);
    }

    public static string NormalizeName(string? name, string fallback)
    {
        var trimmed = name?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed.Length > 80 ? trimmed[..80] : trimmed;
    }

    public static bool IsBuiltIn(string? schemeId)
    {
        return BuiltInSchemes.Any(scheme => string.Equals(scheme.Id, schemeId, StringComparison.Ordinal));
    }

    public static bool IsValidTokenValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 120)
        {
            return false;
        }

        return CssColorOrShadowPattern().IsMatch(value.Trim());
    }

    public static string GetTokenValue(ColorSchemeTokens tokens, string tokenName)
    {
        return tokenName switch
        {
            nameof(ColorSchemeTokens.Background) => tokens.Background,
            nameof(ColorSchemeTokens.CardBackground) => tokens.CardBackground,
            nameof(ColorSchemeTokens.CardBorder) => tokens.CardBorder,
            nameof(ColorSchemeTokens.Ink) => tokens.Ink,
            nameof(ColorSchemeTokens.Muted) => tokens.Muted,
            nameof(ColorSchemeTokens.Primary) => tokens.Primary,
            nameof(ColorSchemeTokens.Accent) => tokens.Accent,
            nameof(ColorSchemeTokens.Danger) => tokens.Danger,
            nameof(ColorSchemeTokens.Success) => tokens.Success,
            nameof(ColorSchemeTokens.Focus) => tokens.Focus,
            nameof(ColorSchemeTokens.Shadow) => tokens.Shadow,
            nameof(ColorSchemeTokens.Surface) => tokens.Surface,
            nameof(ColorSchemeTokens.SurfaceStrong) => tokens.SurfaceStrong,
            nameof(ColorSchemeTokens.ChartPrimary) => tokens.ChartPrimary,
            nameof(ColorSchemeTokens.ChartSecondary) => tokens.ChartSecondary,
            nameof(ColorSchemeTokens.TaskNegative) => tokens.TaskNegative,
            nameof(ColorSchemeTokens.TaskNeutral) => tokens.TaskNeutral,
            nameof(ColorSchemeTokens.TaskPositive) => tokens.TaskPositive,
            _ => string.Empty
        };
    }

    public static ColorSchemeTokens WithTokenValue(ColorSchemeTokens tokens, string tokenName, string value)
    {
        return tokenName switch
        {
            nameof(ColorSchemeTokens.Background) => tokens with { Background = value },
            nameof(ColorSchemeTokens.CardBackground) => tokens with { CardBackground = value },
            nameof(ColorSchemeTokens.CardBorder) => tokens with { CardBorder = value },
            nameof(ColorSchemeTokens.Ink) => tokens with { Ink = value },
            nameof(ColorSchemeTokens.Muted) => tokens with { Muted = value },
            nameof(ColorSchemeTokens.Primary) => tokens with { Primary = value },
            nameof(ColorSchemeTokens.Accent) => tokens with { Accent = value },
            nameof(ColorSchemeTokens.Danger) => tokens with { Danger = value },
            nameof(ColorSchemeTokens.Success) => tokens with { Success = value },
            nameof(ColorSchemeTokens.Focus) => tokens with { Focus = value },
            nameof(ColorSchemeTokens.Shadow) => tokens with { Shadow = value },
            nameof(ColorSchemeTokens.Surface) => tokens with { Surface = value },
            nameof(ColorSchemeTokens.SurfaceStrong) => tokens with { SurfaceStrong = value },
            nameof(ColorSchemeTokens.ChartPrimary) => tokens with { ChartPrimary = value },
            nameof(ColorSchemeTokens.ChartSecondary) => tokens with { ChartSecondary = value },
            nameof(ColorSchemeTokens.TaskNegative) => tokens with { TaskNegative = value },
            nameof(ColorSchemeTokens.TaskNeutral) => tokens with { TaskNeutral = value },
            nameof(ColorSchemeTokens.TaskPositive) => tokens with { TaskPositive = value },
            _ => tokens
        };
    }

    public static IReadOnlyList<string> Validate(ColorSchemeDefinition scheme)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(scheme.Name))
        {
            errors.Add("Scheme name is required.");
        }

        foreach (var token in EditableTokens)
        {
            if (!IsValidTokenValue(GetTokenValue(scheme.Tokens, token.Name)))
            {
                errors.Add($"{token.Label} is not a supported color value.");
            }
        }

        return errors;
    }

    [GeneratedRegex("""^(#[0-9a-fA-F]{3,8}|rgba?\([^)]+\)|hsla?\([^)]+\)|color-mix\([^)]+\)|[a-zA-Z]+|(?:-?\d+(?:\.\d+)?(?:px|rem|em)?\s+){2,6}(?:#[0-9a-fA-F]{3,8}|rgba?\([^)]+\)|[a-zA-Z]+))$""", RegexOptions.CultureInvariant)]
    private static partial Regex CssColorOrShadowPattern();
}

public sealed record ColorSchemeDefinition(
    string Id,
    string Name,
    bool IsBuiltIn,
    ColorSchemeTokens Tokens);

public sealed record ColorSchemeTokens(
    string Background,
    string CardBackground,
    string CardBorder,
    string Ink,
    string Muted,
    string Primary,
    string Accent,
    string Danger,
    string Success,
    string Focus,
    string Shadow,
    string Surface,
    string SurfaceStrong,
    string ChartPrimary,
    string ChartSecondary,
    string TaskNegative,
    string TaskNeutral,
    string TaskPositive);

public sealed record ColorSchemeTokenDescriptor(string Name, string Label);

public sealed record ColorSchemePreferences(
    string SelectedSchemeId,
    IReadOnlyList<ColorSchemeDefinition> CustomSchemes);

public sealed record ColorSchemeState(
    ColorSchemeDefinition ActiveScheme,
    IReadOnlyList<ColorSchemeDefinition> BuiltInSchemes,
    IReadOnlyList<ColorSchemeDefinition> CustomSchemes);
