using Habitica.WebApp.Theme;

namespace Habitica.WebApp.Tests.Theme;

public sealed class ColorSchemeCatalogTests
{
    [Fact]
    public void Built_in_schemes_include_required_palettes()
    {
        var names = ColorSchemeCatalog.BuiltInSchemes.Select(scheme => scheme.Name).ToArray();

        Assert.Equal(20, names.Length);
        Assert.Contains("Alpha (Light)", names);
        Assert.Contains("Habitica (Light)", names);
        Assert.Contains("Gryphy (Light)", names);
        Assert.Contains("Gryphy (Dark)", names);
        Assert.Contains("Midnight Tavern (Dark)", names);
        Assert.Contains("Dragonfire Keep (Dark)", names);
        Assert.Contains("Neon Rogue (Dark)", names);
        Assert.Contains("Frost Healer (Light)", names);
        Assert.Contains("Sunlit Stable (Light)", names);
        Assert.Contains("Mosswood Quest (Light)", names);
        Assert.Contains("Potion Shop (Light)", names);
        Assert.Contains("Boss Battle (Dark)", names);
        Assert.Contains("Quiet Ledger (Light)", names);
        Assert.Contains("Celestial Inn (Dark)", names);
        Assert.Contains("Mana Mirage (Dark)", names);
        Assert.Contains("Mushroom Meadow (Light)", names);
        Assert.Contains("Mushroom Trip (Dark)", names);
        Assert.Contains("Frosted Cake (Light)", names);
        Assert.Contains("Sugar Crash (Dark)", names);
        Assert.Contains("Neon Abyss Carnival (Dark)", names);
    }

    [Fact]
    public void Alpha_scheme_preserves_current_root_palette()
    {
        var alpha = ColorSchemeCatalog.Alpha;

        Assert.Equal("#f5efe2", alpha.Tokens.Background);
        Assert.Equal("rgba(255, 250, 241, 0.92)", alpha.Tokens.CardBackground);
        Assert.Equal("#162423", alpha.Tokens.Ink);
        Assert.Equal("#2d746e", alpha.Tokens.Primary);
        Assert.Equal("#c5772b", alpha.Tokens.Accent);
        Assert.Equal("#a13f35", alpha.Tokens.Danger);
        Assert.Equal("#173f3b", alpha.Tokens.AppBarBackground);
        Assert.Equal("#163431", alpha.Tokens.DrawerBackground);
    }

    [Fact]
    public void Built_in_schemes_define_shell_and_disabled_tokens()
    {
        foreach (var scheme in ColorSchemeCatalog.BuiltInSchemes)
        {
            Assert.True(ColorSchemeCatalog.IsValidTokenValue(scheme.Tokens.AppBarBackground), scheme.Name);
            Assert.True(ColorSchemeCatalog.IsValidTokenValue(scheme.Tokens.DrawerBackground), scheme.Name);
            Assert.True(ColorSchemeCatalog.IsValidTokenValue(scheme.Tokens.ButtonText), scheme.Name);
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
