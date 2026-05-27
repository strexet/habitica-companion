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
                "#8ccfbd",
                "#173f3b",
                "#f5efe2",
                "#163431",
                "#f5efe2",
                "#ffffff",
                "rgba(22, 36, 35, 0.08)",
                "rgba(95, 109, 103, 0.58)",
                "rgba(95, 109, 103, 0.28)",
                "rgba(255, 255, 255, 0.9)",
                "rgba(22, 36, 35, 0.12)")),
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
                "#24cc8f",
                "#4f2a93",
                "#fff7ff",
                "#2f1d52",
                "#fff7ff",
                "#ffffff",
                "rgba(79, 42, 147, 0.08)",
                "rgba(111, 99, 132, 0.62)",
                "rgba(111, 99, 132, 0.28)",
                "rgba(255, 255, 255, 0.94)",
                "rgba(79, 42, 147, 0.16)")),
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
                "#ffcf26",
                "#4f2380",
                "#fff7ff",
                "#2d1945",
                "#fff7ff",
                "#ffffff",
                "rgba(103, 49, 184, 0.08)",
                "rgba(106, 92, 119, 0.62)",
                "rgba(106, 92, 119, 0.28)",
                "rgba(255, 252, 255, 0.94)",
                "rgba(103, 49, 184, 0.16)")),
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
                "#ffcf26",
                "#241336",
                "#fff7ff",
                "#1d102b",
                "#fff7ff",
                "#12081f",
                "rgba(202, 140, 255, 0.1)",
                "rgba(205, 189, 224, 0.54)",
                "rgba(202, 140, 255, 0.26)",
                "#21122f",
                "rgba(202, 140, 255, 0.3)"))
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
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.TaskPositive), "Task positive"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.AppBarBackground), "Header background"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.AppBarText), "Header text"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.DrawerBackground), "Navigation background"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.DrawerText), "Navigation text"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.ButtonText), "Filled button text"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.DisabledBackground), "Disabled background"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.DisabledText), "Disabled text"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.DisabledBorder), "Disabled border"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.InputBackground), "Input background"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.InputBorder), "Input border")
    };

    public static ColorSchemeDefinition Alpha => BuiltInSchemes[0];

    public static ColorSchemeDefinition Resolve(string? schemeId, IReadOnlyList<ColorSchemeDefinition> customSchemes)
    {
        return BuiltInSchemes.Concat(customSchemes)
            .Select(Complete)
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

    public static ColorSchemeDefinition Complete(ColorSchemeDefinition scheme)
    {
        var fallback = Alpha.Tokens;
        var tokens = scheme.Tokens ?? fallback;
        return scheme with
        {
            Id = string.IsNullOrWhiteSpace(scheme.Id) ? $"custom-{Guid.NewGuid():N}" : scheme.Id,
            Name = NormalizeName(scheme.Name, scheme.IsBuiltIn ? "Alpha" : "Custom scheme"),
            Tokens = new ColorSchemeTokens(
                NormalizeToken(tokens.Background, fallback.Background),
                NormalizeToken(tokens.CardBackground, fallback.CardBackground),
                NormalizeToken(tokens.CardBorder, fallback.CardBorder),
                NormalizeToken(tokens.Ink, fallback.Ink),
                NormalizeToken(tokens.Muted, fallback.Muted),
                NormalizeToken(tokens.Primary, fallback.Primary),
                NormalizeToken(tokens.Accent, fallback.Accent),
                NormalizeToken(tokens.Danger, fallback.Danger),
                NormalizeToken(tokens.Success, fallback.Success),
                NormalizeToken(tokens.Focus, fallback.Focus),
                NormalizeToken(tokens.Shadow, fallback.Shadow),
                NormalizeToken(tokens.Surface, fallback.Surface),
                NormalizeToken(tokens.SurfaceStrong, fallback.SurfaceStrong),
                NormalizeToken(tokens.ChartPrimary, fallback.ChartPrimary),
                NormalizeToken(tokens.ChartSecondary, fallback.ChartSecondary),
                NormalizeToken(tokens.TaskNegative, fallback.TaskNegative),
                NormalizeToken(tokens.TaskNeutral, fallback.TaskNeutral),
                NormalizeToken(tokens.TaskPositive, fallback.TaskPositive),
                NormalizeToken(tokens.AppBarBackground, fallback.AppBarBackground),
                NormalizeToken(tokens.AppBarText, fallback.AppBarText),
                NormalizeToken(tokens.DrawerBackground, fallback.DrawerBackground),
                NormalizeToken(tokens.DrawerText, fallback.DrawerText),
                NormalizeToken(tokens.ButtonText, fallback.ButtonText),
                NormalizeToken(tokens.DisabledBackground, fallback.DisabledBackground),
                NormalizeToken(tokens.DisabledText, fallback.DisabledText),
                NormalizeToken(tokens.DisabledBorder, fallback.DisabledBorder),
                NormalizeToken(tokens.InputBackground, fallback.InputBackground),
                NormalizeToken(tokens.InputBorder, fallback.InputBorder))
        };
    }

    private static string NormalizeToken(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
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
            nameof(ColorSchemeTokens.AppBarBackground) => tokens.AppBarBackground,
            nameof(ColorSchemeTokens.AppBarText) => tokens.AppBarText,
            nameof(ColorSchemeTokens.DrawerBackground) => tokens.DrawerBackground,
            nameof(ColorSchemeTokens.DrawerText) => tokens.DrawerText,
            nameof(ColorSchemeTokens.ButtonText) => tokens.ButtonText,
            nameof(ColorSchemeTokens.DisabledBackground) => tokens.DisabledBackground,
            nameof(ColorSchemeTokens.DisabledText) => tokens.DisabledText,
            nameof(ColorSchemeTokens.DisabledBorder) => tokens.DisabledBorder,
            nameof(ColorSchemeTokens.InputBackground) => tokens.InputBackground,
            nameof(ColorSchemeTokens.InputBorder) => tokens.InputBorder,
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
            nameof(ColorSchemeTokens.AppBarBackground) => tokens with { AppBarBackground = value },
            nameof(ColorSchemeTokens.AppBarText) => tokens with { AppBarText = value },
            nameof(ColorSchemeTokens.DrawerBackground) => tokens with { DrawerBackground = value },
            nameof(ColorSchemeTokens.DrawerText) => tokens with { DrawerText = value },
            nameof(ColorSchemeTokens.ButtonText) => tokens with { ButtonText = value },
            nameof(ColorSchemeTokens.DisabledBackground) => tokens with { DisabledBackground = value },
            nameof(ColorSchemeTokens.DisabledText) => tokens with { DisabledText = value },
            nameof(ColorSchemeTokens.DisabledBorder) => tokens with { DisabledBorder = value },
            nameof(ColorSchemeTokens.InputBackground) => tokens with { InputBackground = value },
            nameof(ColorSchemeTokens.InputBorder) => tokens with { InputBorder = value },
            _ => tokens
        };
    }

    public static IReadOnlyList<string> Validate(ColorSchemeDefinition scheme)
    {
        scheme = Complete(scheme);
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
    string TaskPositive,
    string AppBarBackground,
    string AppBarText,
    string DrawerBackground,
    string DrawerText,
    string ButtonText,
    string DisabledBackground,
    string DisabledText,
    string DisabledBorder,
    string InputBackground,
    string InputBorder);

public sealed record ColorSchemeTokenDescriptor(string Name, string Label);

public sealed record ColorSchemePreferences(
    string SelectedSchemeId,
    IReadOnlyList<ColorSchemeDefinition> CustomSchemes);

public sealed record ColorSchemeState(
    ColorSchemeDefinition ActiveScheme,
    IReadOnlyList<ColorSchemeDefinition> BuiltInSchemes,
    IReadOnlyList<ColorSchemeDefinition> CustomSchemes);
