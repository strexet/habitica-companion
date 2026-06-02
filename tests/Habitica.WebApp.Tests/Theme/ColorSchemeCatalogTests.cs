using Habitica.WebApp.Theme;

namespace Habitica.WebApp.Tests.Theme;

public sealed class ColorSchemeCatalogTests
{
    [Fact]
    public void Built_in_schemes_include_required_palettes()
    {
        var schemes = ColorSchemeCatalog.BuiltInSchemes;
        var ids = schemes.Select(scheme => scheme.Id).ToArray();

        Assert.Equal(ColorSchemeCatalog.DefaultLightSchemeId, ids[0]);
        Assert.Equal(ColorSchemeCatalog.DefaultDarkSchemeId, ids[1]);
        Assert.Equal(ColorSchemeCatalog.ForestLegacyId, ids[2]);
        Assert.Equal("frosted-cake", ids[3]);
        Assert.Contains("arcane-wraith", ids);
        Assert.Contains("phantom-fair", ids);
        Assert.Contains("toxic-swamp", ids);
        Assert.Contains("green-menace", ids);
        Assert.Contains("abyssal-blackwater", ids);
        Assert.Contains("obsidian-glow", ids);
        Assert.Contains("blessed-skyhaven", ids);
        Assert.Contains("infernal-covenant", ids);
        Assert.Equal(schemes.Count(scheme => !scheme.IsDark), schemes.Count(scheme => scheme.IsDark));
        Assert.DoesNotContain(ids, id => id is "habitica" or "mana-mirage" or "mushroom-meadow" or "mushroom-trip" or "sugar-crash" or "neon-rogue" or "neon-abyss-carnival");
    }

    [Fact]
    public void Gryphy_light_scheme_matches_restored_default_palette()
    {
        var alpha = ColorSchemeCatalog.Alpha;

        Assert.Equal(ColorSchemeCatalog.DefaultLightSchemeId, alpha.Id);
        Assert.Equal("#f7f1ff", alpha.Tokens.Background);
        Assert.Equal("rgba(255, 252, 255, 0.94)", alpha.Tokens.CardBackground);
        Assert.Equal("#2d2040", alpha.Tokens.Ink);
        Assert.Equal("#7334bd", alpha.Tokens.Primary);
        Assert.Equal("#d99416", alpha.Tokens.Accent);
        Assert.Equal("#684095", alpha.Tokens.AppBarBackground);
        Assert.Equal("#3b2356", alpha.Tokens.DrawerBackground);
    }

    [Theory]
    [InlineData("alpha", "forest-legacy")]
    [InlineData("neon-rogue", "arcane-wraith")]
    [InlineData("neon-abyss-carnival", "phantom-fair")]
    [InlineData("habitica", "gryphy-light")]
    [InlineData("sugar-crash", "gryphy-dark")]
    public void Legacy_scheme_ids_map_to_supported_palettes(string legacyId, string expectedId)
    {
        Assert.Equal(expectedId, ColorSchemeCatalog.MigrateLegacySchemeId(legacyId));
        Assert.Equal(expectedId, ColorSchemeCatalog.Resolve(legacyId, Array.Empty<ColorSchemeDefinition>()).Id);
    }

    [Fact]
    public void Built_in_schemes_define_shell_and_disabled_tokens()
    {
        foreach (var scheme in ColorSchemeCatalog.BuiltInSchemes)
        {
            Assert.True(ColorSchemeCatalog.IsValidTokenValue(scheme.Tokens.AppBarBackground), scheme.Name);
            Assert.True(ColorSchemeCatalog.IsValidTokenValue(scheme.Tokens.DrawerBackground), scheme.Name);
            Assert.True(ColorSchemeCatalog.IsValidTokenValue(scheme.Tokens.PrimaryButtonText), scheme.Name);
            Assert.True(ColorSchemeCatalog.IsValidTokenValue(
                ColorSchemeCatalog.GetTokenValue(scheme.Tokens, nameof(ColorSchemeTokens.SecondaryButtonText))), scheme.Name);
            Assert.True(ColorSchemeCatalog.IsValidTokenValue(scheme.Tokens.DisabledBackground), scheme.Name);
            Assert.True(ColorSchemeCatalog.IsValidTokenValue(scheme.Tokens.DisabledText), scheme.Name);
            Assert.True(ColorSchemeCatalog.IsValidTokenValue(scheme.Tokens.InputBackground), scheme.Name);
            Assert.True(ColorSchemeCatalog.IsValidTokenValue(scheme.Tokens.TaskNegative), scheme.Name);
            Assert.True(ColorSchemeCatalog.IsValidTokenValue(scheme.Tokens.TaskNeutral), scheme.Name);
            Assert.True(ColorSchemeCatalog.IsValidTokenValue(scheme.Tokens.TaskPositive), scheme.Name);
        }
    }

    [Fact]
    public void Complete_backfills_missing_custom_scheme_tokens()
    {
        var scheme = ColorSchemeCatalog.CreateCustomCopy(ColorSchemeCatalog.Alpha, "Legacy")
            with
            {
                Tokens = ColorSchemeCatalog.Alpha.Tokens with
                {
                    AppBarBackground = "",
                    DisabledText = "",
                    InputBackground = ""
                }
            };

        var completed = ColorSchemeCatalog.Complete(scheme);

        Assert.Equal(ColorSchemeCatalog.Alpha.Tokens.AppBarBackground, completed.Tokens.AppBarBackground);
        Assert.Equal(ColorSchemeCatalog.Alpha.Tokens.DisabledText, completed.Tokens.DisabledText);
        Assert.Equal(ColorSchemeCatalog.Alpha.Tokens.InputBackground, completed.Tokens.InputBackground);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Generated_random_theme_passes_validation_across_chaos_levels(double chaos)
    {
        for (var seed = 0; seed < 200; seed++)
        {
            var scheme = ColorSchemeCatalog.GenerateRandomTheme(new Random(seed), chaos);

            Assert.Equal(ColorSchemeCatalog.RandomSchemeId, scheme.Id);
            var errors = ColorSchemeCatalog.Validate(scheme);
            Assert.True(errors.Count == 0, $"chaos={chaos} seed {seed}: {string.Join(", ", errors)}");
        }
    }

    [Fact]
    public void Same_seed_reproduces_the_same_theme_so_chaos_slider_is_reversible()
    {
        // The chaos slider re-renders the pending random with a fixed seed. Re-rendering the same
        // seed at the same chaos must reproduce the exact palette, so dragging the slider back and
        // forth is predictable and reversible.
        const int seed = 4242;
        var first = ColorSchemeCatalog.GenerateRandomTheme(new Random(seed), 0.7);
        var afterRoundTrip = ColorSchemeCatalog.GenerateRandomTheme(new Random(seed), 0.7);
        Assert.Equal(first.Tokens, afterRoundTrip.Tokens);

        // A different chaos on the same seed is a different but still deterministic palette.
        var calmer = ColorSchemeCatalog.GenerateRandomTheme(new Random(seed), 0.2);
        Assert.NotEqual(first.Tokens, calmer.Tokens);
        Assert.Equal(calmer.Tokens, ColorSchemeCatalog.GenerateRandomTheme(new Random(seed), 0.2).Tokens);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Generated_random_theme_defines_every_gradient_surface(double chaos)
    {
        var tokens = ColorSchemeCatalog.GenerateRandomTheme(new Random(42), chaos).Tokens;

        Assert.NotNull(tokens.BackgroundGradient);
        Assert.NotNull(tokens.CardGradient);
        Assert.NotNull(tokens.AppBarGradient);
        Assert.NotNull(tokens.DrawerGradient);
        Assert.NotNull(tokens.PrimaryButtonGradient);
        Assert.NotNull(tokens.SecondaryButtonGradient);
        Assert.NotNull(tokens.AccentChipGradient);
    }

    [Fact]
    public void Readable_clipboard_round_trip_preserves_gradients_variant_and_text_shadows()
    {
        var source = ColorSchemeCatalog.GenerateRandomTheme(new Random(42), 0.9) with { Name = "Shared theme" };

        var outcome = ReadableSchemeParser.Parse(ReadableSchemeParser.Serialize(source), ColorSchemeCatalog.CreateCustomCopy(ColorSchemeCatalog.Alpha, "Draft"));

        Assert.Equal(SchemeParseResult.Success, outcome.Result);
        Assert.NotNull(outcome.Scheme);
        Assert.Equal(source.Name, outcome.Scheme!.Name);
        Assert.Equal(source.IsDark, outcome.Scheme.IsDark);
        Assert.Equal(source.Tokens, outcome.Scheme.Tokens);
    }

    [Fact]
    public void Readable_clipboard_copy_uses_v2_names_and_omits_empty_optional_groups()
    {
        var json = ReadableSchemeParser.Serialize(ColorSchemeCatalog.DefaultLight);
        var baseline = ColorSchemeCatalog.GenerateRandomTheme(new Random(42), 0.9);

        Assert.Contains("\"$schema\": \"habitica-tool.color-scheme.v2\"", json);
        Assert.Contains("\"PageBackground\"", json);
        Assert.Contains("\"BodyText\"", json);
        Assert.Contains("\"FocusOutline\"", json);
        Assert.DoesNotContain("\"Gradients\"", json);
        Assert.DoesNotContain("\"TextShadows\"", json);
        Assert.Equal(ColorSchemeCatalog.DefaultLight.Tokens, ReadableSchemeParser.Parse(json, baseline).Scheme!.Tokens);
    }

    [Fact]
    public void Readable_clipboard_parser_accepts_v1_and_rejects_partial_or_conflicting_v2_values()
    {
        var baseline = ColorSchemeCatalog.CreateCustomCopy(ColorSchemeCatalog.Alpha, "Draft");

        var legacy = ReadableSchemeParser.Parse("""{"Name":"Old","Tokens":{"Primary":"#112233"}}""", baseline);
        var partial = ReadableSchemeParser.Parse("""{"Name":"Bad","Colors":{"Primary":"#112233"},"Gradients":{"Card":{"TopLeft":"#fff"}}}""", baseline);
        var conflicting = ReadableSchemeParser.Parse("""{"Name":"Bad","Colors":{"PageBackground":"#fff","Background":"#000"}}""", baseline);

        Assert.Equal(SchemeParseResult.Success, legacy.Result);
        Assert.Equal("#112233", legacy.Scheme!.Tokens.Primary);
        Assert.Equal(SchemeParseResult.PartialGradient, partial.Result);
        Assert.Contains("Card", partial.Detail);
        Assert.Equal(SchemeParseResult.ConflictingAliases, conflicting.Result);
        Assert.Contains("PageBackground", conflicting.Detail);
    }

    [Fact]
    public void Readable_clipboard_parser_accepts_multi_layer_card_and_text_shadows()
    {
        var baseline = ColorSchemeCatalog.CreateCustomCopy(ColorSchemeCatalog.Alpha, "Draft");
        const string cardShadow = "0 0 0 1px rgba(172, 202, 255, 0.10), 0 22px 76px rgba(127, 168, 255, 0.27), 0 0 68px rgba(203, 161, 255, 0.16), 0 0 28px rgba(145, 183, 255, 0.10)";
        const string headingShadow = "0 0 18px rgba(145, 183, 255, 0.50), 0 0 40px rgba(203, 161, 255, 0.27), 0 0 64px rgba(145, 183, 255, 0.12)";
        var json = "{\"Name\":\"Obsidian\",\"Variant\":\"Dark\",\"Colors\":{\"Primary\":\"#91b7ff\",\"CardShadow\":\""
            + cardShadow + "\"},\"TextShadows\":{\"Headings\":\"" + headingShadow + "\"}}";

        var outcome = ReadableSchemeParser.Parse(json, baseline);

        Assert.Equal(SchemeParseResult.Success, outcome.Result);
        Assert.Equal(cardShadow, outcome.Scheme!.Tokens.Shadow);
        Assert.Equal(headingShadow, outcome.Scheme.Tokens.HeadingTextShadow);
    }

    [Fact]
    public void Multi_layer_shadow_is_valid_but_not_a_plain_color()
    {
        const string multiLayer = "0 0 0 1px rgba(172, 202, 255, 0.10), 0 22px 76px rgba(127, 168, 255, 0.27)";

        Assert.True(ColorSchemeCatalog.IsValidShadowValue(multiLayer));
        Assert.False(ColorSchemeCatalog.IsValidTokenValue(multiLayer));
        Assert.True(ColorSchemeCatalog.IsValidShadowValue("none"));
        Assert.False(ColorSchemeCatalog.IsValidShadowValue("0 0 0 1px not a color, garbage"));
    }

    [Fact]
    public void Pick_random_preset_excludes_active_and_random_ids()
    {
        var random = new Random(7);
        var customs = new[]
        {
            ColorSchemeCatalog.CreateCustomCopy(ColorSchemeCatalog.Alpha, "Mine")
        };

        for (var i = 0; i < 50; i++)
        {
            var active = ColorSchemeCatalog.Alpha.Id;
            var picked = ColorSchemeCatalog.PickRandomPreset(customs, active, random);

            Assert.NotEqual(active, picked.Id);
            Assert.NotEqual(ColorSchemeCatalog.RandomSchemeId, picked.Id);
        }
    }

    [Fact]
    public void Pick_random_preset_includes_custom_schemes_in_pool()
    {
        var custom = ColorSchemeCatalog.CreateCustomCopy(ColorSchemeCatalog.Alpha, "Mine");
        var allowed = ColorSchemeCatalog.BuiltInSchemes.Select(s => s.Id).Append(custom.Id).ToHashSet();

        for (var seed = 0; seed < 50; seed++)
        {
            var picked = ColorSchemeCatalog.PickRandomPreset(new[] { custom }, excludeId: null, new Random(seed));
            Assert.Contains(picked.Id, allowed);
        }
    }

    [Fact]
    public void Validation_rejects_invalid_custom_color_values()
    {
        var scheme = ColorSchemeCatalog.CreateCustomCopy(ColorSchemeCatalog.Alpha, "Bad")
            with
            {
                Tokens = ColorSchemeCatalog.Alpha.Tokens with { Primary = "not a color?" }
            };

        var errors = ColorSchemeCatalog.Validate(scheme);

        Assert.Contains(errors, error => error.Contains("Primary", StringComparison.Ordinal));
    }
}
