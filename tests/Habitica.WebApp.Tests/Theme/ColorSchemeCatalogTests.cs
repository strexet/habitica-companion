using Habitica.WebApp.Theme;

namespace Habitica.WebApp.Tests.Theme;

public sealed class ColorSchemeCatalogTests
{
    [Fact]
    public void Built_in_schemes_include_required_palettes()
    {
        var names = ColorSchemeCatalog.BuiltInSchemes.Select(scheme => scheme.Name).ToArray();

        Assert.Contains("Alpha", names);
        Assert.Contains("Habitica", names);
        Assert.Contains("Gryphy Light", names);
        Assert.Contains("Gryphy Dark", names);
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
