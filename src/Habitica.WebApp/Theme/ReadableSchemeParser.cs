using System.Text.Json;

namespace Habitica.WebApp.Theme;

public static class ReadableSchemeParser
{
    public const string Schema = "habitica-tool.color-scheme.v2";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly (string Readable, string Token, string? Alias)[] ColorFields =
    {
        ("PageBackground", nameof(ColorSchemeTokens.Background), "Background"),
        ("CardBackground", nameof(ColorSchemeTokens.CardBackground), null),
        ("CardBorder", nameof(ColorSchemeTokens.CardBorder), null),
        ("BodyText", nameof(ColorSchemeTokens.Ink), "Ink"),
        ("SecondaryText", nameof(ColorSchemeTokens.Muted), "Muted"),
        ("Primary", nameof(ColorSchemeTokens.Primary), null),
        ("Accent", nameof(ColorSchemeTokens.Accent), null),
        ("Danger", nameof(ColorSchemeTokens.Danger), null),
        ("Success", nameof(ColorSchemeTokens.Success), null),
        ("FocusOutline", nameof(ColorSchemeTokens.Focus), "Focus"),
        ("CardShadow", nameof(ColorSchemeTokens.Shadow), "Shadow"),
        ("SurfaceTint", nameof(ColorSchemeTokens.Surface), "Surface"),
        ("SurfaceStrongTint", nameof(ColorSchemeTokens.SurfaceStrong), "SurfaceStrong"),
        ("ChartPrimary", nameof(ColorSchemeTokens.ChartPrimary), null),
        ("ChartSecondary", nameof(ColorSchemeTokens.ChartSecondary), null),
        ("TaskNegativeTint", nameof(ColorSchemeTokens.TaskNegative), "TaskNegative"),
        ("TaskNeutralTint", nameof(ColorSchemeTokens.TaskNeutral), "TaskNeutral"),
        ("TaskPositiveTint", nameof(ColorSchemeTokens.TaskPositive), "TaskPositive"),
        ("AppBarBackground", nameof(ColorSchemeTokens.AppBarBackground), null),
        ("AppBarText", nameof(ColorSchemeTokens.AppBarText), null),
        ("DrawerBackground", nameof(ColorSchemeTokens.DrawerBackground), null),
        ("DrawerText", nameof(ColorSchemeTokens.DrawerText), null),
        ("ButtonText", nameof(ColorSchemeTokens.ButtonText), null),
        ("DisabledBackground", nameof(ColorSchemeTokens.DisabledBackground), null),
        ("DisabledText", nameof(ColorSchemeTokens.DisabledText), null),
        ("DisabledBorder", nameof(ColorSchemeTokens.DisabledBorder), null),
        ("InputBackground", nameof(ColorSchemeTokens.InputBackground), null),
        ("InputBorder", nameof(ColorSchemeTokens.InputBorder), null)
    };

    public static string Serialize(ColorSchemeDefinition scheme)
    {
        var gradients = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        AddGradient(gradients, "PageBackground", scheme.Tokens.BackgroundGradient);
        AddGradient(gradients, "Card", scheme.Tokens.CardGradient);
        AddGradient(gradients, "AppBar", scheme.Tokens.AppBarGradient);
        AddGradient(gradients, "Drawer", scheme.Tokens.DrawerGradient);
        AddGradient(gradients, "PrimaryButton", scheme.Tokens.PrimaryButtonGradient);
        AddGradient(gradients, "SecondaryButton", scheme.Tokens.SecondaryButtonGradient);
        AddGradient(gradients, "AccentChip", scheme.Tokens.AccentChipGradient);

        var shadows = new Dictionary<string, string?>(StringComparer.Ordinal);
        AddOptional(shadows, "Headings", scheme.Tokens.HeadingTextShadow);
        AddOptional(shadows, "AppBar", scheme.Tokens.AppBarTextShadow);
        AddOptional(shadows, "Drawer", scheme.Tokens.DrawerTextShadow);

        var model = new ReadableSchemeClipboardModel(
            Schema,
            scheme.Name,
            scheme.IsDark ? "Dark" : "Light",
            string.Empty,
            ColorFields.ToDictionary(
                field => field.Readable,
                field => ColorSchemeCatalog.GetTokenValue(scheme.Tokens, field.Token),
                StringComparer.Ordinal),
            gradients.Count == 0 ? null : gradients,
            shadows.Count == 0 ? null : shadows);
        return JsonSerializer.Serialize(model, JsonOptions);
    }

    public static ReadableSchemeParseOutcome Parse(string json, ColorSchemeDefinition baseline)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new(SchemeParseResult.Empty);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return new(SchemeParseResult.InvalidJson);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new(SchemeParseResult.NotAnObject);
            }

            var isV2 = TryGet(root, "$schema", out var schema)
                && schema.ValueKind == JsonValueKind.String
                && string.Equals(schema.GetString(), Schema, StringComparison.Ordinal)
                || TryGet(root, "Colors", out _);
            return isV2 ? ParseV2(root, baseline) : ParseV1(root, baseline);
        }
    }

    private static ReadableSchemeParseOutcome ParseV2(JsonElement root, ColorSchemeDefinition baseline)
    {
        if (!TryGetString(root, "Name", out var name) || string.IsNullOrWhiteSpace(name))
        {
            return new(SchemeParseResult.MissingName);
        }

        if (!TryGet(root, "Colors", out var colors) || colors.ValueKind != JsonValueKind.Object)
        {
            return new(SchemeParseResult.NoTokens);
        }

        var tokens = baseline.Tokens with
        {
            BackgroundGradient = null,
            CardGradient = null,
            AppBarGradient = null,
            DrawerGradient = null,
            PrimaryButtonGradient = null,
            SecondaryButtonGradient = null,
            AccentChipGradient = null,
            HeadingTextShadow = null,
            AppBarTextShadow = null,
            DrawerTextShadow = null
        };
        var recognized = 0;
        foreach (var field in ColorFields)
        {
            var result = TryReadAliasedValue(colors, field.Readable, field.Alias, out var value);
            if (result is not null)
            {
                return result;
            }

            if (value is null)
            {
                continue;
            }

            var isShadow = field.Token == nameof(ColorSchemeTokens.Shadow);
            if (!(isShadow ? ColorSchemeCatalog.IsValidShadowValue(value) : ColorSchemeCatalog.IsValidTokenValue(value)))
            {
                return new(SchemeParseResult.InvalidValue, Detail: $"Colors.{field.Readable} is not a valid CSS {(isShadow ? "shadow" : "color")} value.");
            }

            tokens = ColorSchemeCatalog.WithTokenValue(tokens, field.Token, value);
            recognized++;
        }

        if (recognized == 0)
        {
            return new(SchemeParseResult.NoTokens);
        }

        var gradientsResult = ParseGradients(root, tokens);
        if (gradientsResult.Outcome is not null)
        {
            return gradientsResult.Outcome;
        }

        tokens = gradientsResult.Tokens;
        var shadowsResult = ParseTextShadows(root, tokens);
        if (shadowsResult.Outcome is not null)
        {
            return shadowsResult.Outcome;
        }

        var isDark = baseline.IsDark;
        if (TryGetString(root, "Variant", out var variant))
        {
            if (string.Equals(variant, "dark", StringComparison.OrdinalIgnoreCase))
            {
                isDark = true;
            }
            else if (string.Equals(variant, "light", StringComparison.OrdinalIgnoreCase))
            {
                isDark = false;
            }
        }

        return new(
            SchemeParseResult.Success,
            baseline with
            {
                Name = ColorSchemeCatalog.NormalizeName(name, "Custom scheme"),
                IsBuiltIn = false,
                IsDark = isDark,
                Tokens = shadowsResult.Tokens
            });
    }

    private static ReadableSchemeParseOutcome ParseV1(JsonElement root, ColorSchemeDefinition baseline)
    {
        var name = TryGetString(root, "Name", out var parsedName) ? parsedName : baseline.Name;
        var tokenRoot = TryGet(root, "Tokens", out var nested) ? nested : root;
        if (tokenRoot.ValueKind != JsonValueKind.Object)
        {
            return new(SchemeParseResult.NotAnObject);
        }

        var tokens = baseline.Tokens;
        var recognized = 0;
        foreach (var field in ColorFields)
        {
            if (!TryGetString(tokenRoot, field.Token, out var value))
            {
                continue;
            }

            var isShadow = field.Token == nameof(ColorSchemeTokens.Shadow);
            if (!(isShadow ? ColorSchemeCatalog.IsValidShadowValue(value) : ColorSchemeCatalog.IsValidTokenValue(value)))
            {
                return new(SchemeParseResult.InvalidValue, Detail: $"{field.Token} is not a valid CSS {(isShadow ? "shadow" : "color")} value.");
            }

            tokens = ColorSchemeCatalog.WithTokenValue(tokens, field.Token, value);
            recognized++;
        }

        return recognized == 0
            ? new(SchemeParseResult.NoTokens)
            : new(
                SchemeParseResult.Success,
                baseline with
                {
                    Name = ColorSchemeCatalog.NormalizeName(name, "Custom scheme"),
                    IsBuiltIn = false,
                    Tokens = tokens
                });
    }

    private static (ColorSchemeTokens Tokens, ReadableSchemeParseOutcome? Outcome) ParseGradients(JsonElement root, ColorSchemeTokens tokens)
    {
        if (!TryGet(root, "Gradients", out var gradients) || gradients.ValueKind == JsonValueKind.Null)
        {
            return (tokens, null);
        }

        if (gradients.ValueKind != JsonValueKind.Object)
        {
            return (tokens, new(SchemeParseResult.InvalidValue, Detail: "Gradients must be an object."));
        }

        var current = tokens;
        var parsed = ParseGradient("PageBackground", current, names => current with { BackgroundGradient = new GradientStops9(names[0], names[1], names[2], names[3], names[4], names[5], names[6], names[7], names[8]) },
            "TopLeft", "Top", "TopRight", "MiddleLeft", "Middle", "MiddleRight", "BottomLeft", "Bottom", "BottomRight");
        if (parsed.Outcome is not null) return parsed;
        current = parsed.Tokens;
        parsed = ParseGradient("Card", current, names => current with { CardGradient = new GradientStops8(names[0], names[1], names[2], names[3], names[4], names[5], names[6], names[7]) },
            "TopLeft", "Top", "TopRight", "MiddleLeft", "MiddleRight", "BottomLeft", "Bottom", "BottomRight");
        if (parsed.Outcome is not null) return parsed;
        current = parsed.Tokens;
        parsed = ParseGradient("AppBar", current, names => current with { AppBarGradient = new GradientStops6(names[0], names[1], names[2], names[3], names[4], names[5]) },
            "TopLeft", "Top", "TopRight", "BottomLeft", "Bottom", "BottomRight");
        if (parsed.Outcome is not null) return parsed;
        current = parsed.Tokens;
        parsed = ParseGradient("Drawer", current, names => current with { DrawerGradient = new GradientStops6(names[0], names[1], names[2], names[3], names[4], names[5]) },
            "TopLeft", "Top", "TopRight", "BottomLeft", "Bottom", "BottomRight");
        if (parsed.Outcome is not null) return parsed;
        current = parsed.Tokens;
        parsed = ParseGradient("PrimaryButton", current, names => current with { PrimaryButtonGradient = new GradientStops4(names[0], names[1], names[2], names[3]) },
            "TopLeft", "TopRight", "BottomLeft", "BottomRight");
        if (parsed.Outcome is not null) return parsed;
        current = parsed.Tokens;
        parsed = ParseGradient("SecondaryButton", current, names => current with { SecondaryButtonGradient = new GradientStops2(names[0], names[1]) }, "Start", "End");
        if (parsed.Outcome is not null) return parsed;
        current = parsed.Tokens;
        return ParseGradient("AccentChip", current, names => current with { AccentChipGradient = new GradientStops2(names[0], names[1]) }, "Start", "End");

        (ColorSchemeTokens Tokens, ReadableSchemeParseOutcome? Outcome) ParseGradient(
            string name,
            ColorSchemeTokens current,
            Func<string[], ColorSchemeTokens> apply,
            params string[] stopNames)
        {
            if (!TryGet(gradients, name, out var gradient) || gradient.ValueKind == JsonValueKind.Null)
            {
                return (current, null);
            }

            if (gradient.ValueKind != JsonValueKind.Object)
            {
                return (current, new(SchemeParseResult.InvalidValue, Detail: $"Gradients.{name} must be an object."));
            }

            var missing = stopNames.Where(stop => !TryGetString(gradient, stop, out _)).ToArray();
            if (missing.Length > 0)
            {
                return (current, new(SchemeParseResult.PartialGradient, Detail: $"Gradients.{name} is missing: {string.Join(", ", missing)}."));
            }

            var values = stopNames.Select(stop => gradient.GetPropertyIgnoreCase(stop).GetString()!).ToArray();
            var invalid = stopNames.Zip(values).FirstOrDefault(pair => !ColorSchemeCatalog.IsValidTokenValue(pair.Second));
            return invalid.First is not null
                ? (current, new(SchemeParseResult.InvalidValue, Detail: $"Gradients.{name}.{invalid.First} is not a valid CSS color."))
                : (apply(values), null);
        }
    }

    private static (ColorSchemeTokens Tokens, ReadableSchemeParseOutcome? Outcome) ParseTextShadows(JsonElement root, ColorSchemeTokens tokens)
    {
        if (!TryGet(root, "TextShadows", out var shadows) || shadows.ValueKind == JsonValueKind.Null)
        {
            return (tokens, null);
        }

        if (shadows.ValueKind != JsonValueKind.Object)
        {
            return (tokens, new(SchemeParseResult.InvalidValue, Detail: "TextShadows must be an object."));
        }

        var current = tokens;
        var result = ReadOptionalShadow(shadows, "Headings", current, value => current with { HeadingTextShadow = value });
        if (result.Outcome is not null) return result;
        current = result.Tokens;
        result = ReadOptionalShadow(shadows, "AppBar", current, value => current with { AppBarTextShadow = value });
        if (result.Outcome is not null) return result;
        current = result.Tokens;
        return ReadOptionalShadow(shadows, "Drawer", current, value => current with { DrawerTextShadow = value });
    }

    private static (ColorSchemeTokens Tokens, ReadableSchemeParseOutcome? Outcome) ReadOptionalShadow(
        JsonElement parent,
        string name,
        ColorSchemeTokens tokens,
        Func<string?, ColorSchemeTokens> apply,
        ReadableSchemeParseOutcome? previous = null)
    {
        if (previous is not null || !TryGet(parent, name, out var value))
        {
            return (tokens, previous);
        }

        if (value.ValueKind == JsonValueKind.Null)
        {
            return (apply(null), null);
        }

        if (value.ValueKind != JsonValueKind.String || !ColorSchemeCatalog.IsValidShadowValue(value.GetString()))
        {
            return (tokens, new(SchemeParseResult.InvalidValue, Detail: $"TextShadows.{name} is not a valid CSS text-shadow value."));
        }

        return (apply(value.GetString()), null);
    }

    private static ReadableSchemeParseOutcome? TryReadAliasedValue(JsonElement element, string readable, string? alias, out string? value)
    {
        value = null;
        var hasReadable = TryGetString(element, readable, out var readableValue);
        var aliasValue = string.Empty;
        var hasAlias = alias is not null && TryGetString(element, alias, out aliasValue);
        if (hasReadable && hasAlias && !string.Equals(readableValue, aliasValue, StringComparison.Ordinal))
        {
            return new(SchemeParseResult.ConflictingAliases, Detail: $"Colors.{readable} conflicts with Colors.{alias}.");
        }

        value = hasReadable ? readableValue : hasAlias ? aliasValue : null;
        return null;
    }

    private static void AddGradient(Dictionary<string, IReadOnlyDictionary<string, string>> gradients, string name, object? gradient)
    {
        if (gradient is null)
        {
            return;
        }

        gradients[name] = gradient.GetType()
            .GetProperties()
            .ToDictionary(property => property.Name, property => (string)property.GetValue(gradient)!, StringComparer.Ordinal);
    }

    private static void AddOptional(Dictionary<string, string?> values, string name, string? value)
    {
        if (value is not null)
        {
            values[name] = value;
        }
    }

    private static bool TryGet(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetString(JsonElement element, string name, out string value)
    {
        if (TryGet(element, name, out var property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString()!;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static JsonElement GetPropertyIgnoreCase(this JsonElement element, string name)
    {
        return TryGet(element, name, out var value) ? value : default;
    }
}
