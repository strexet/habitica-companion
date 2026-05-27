using Habitica.WebApp.Theme;

namespace Habitica.WebApp.Tests.Theme;

public sealed class ColorSchemeCatalogTests
{
    [Fact]
    public void Built_in_schemes_include_required_palettes()
    {
        var names = ColorSchemeCatalog.BuiltInSchemes.Select(scheme => scheme.Name).ToArray();

        Assert.Equal(15, names.Length);
        Assert.Contains("Alpha", names);
        Assert.Contains("Habitica", names);
        Assert.Contains("Gryphy Light", names);
        Assert.Contains("Gryphy Dark", names);
        Assert.Contains("Midnight Tavern", names);
        Assert.Contains("Dragonfire Keep", names);
        Assert.Contains("Neon Rogue", names);
        Assert.Contains("Frost Healer", names);
        Assert.Contains("Sunlit Stable", names);
        Assert.Contains("Mosswood Quest", names);
        Assert.Contains("Potion Shop", names);
        Assert.Contains("Boss Battle", names);
        Assert.Contains("Quiet Ledger", names);
        Assert.Contains("Celestial Inn", names);
        Assert.Contains("Mana Mirage", names);
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
