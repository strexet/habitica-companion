using Bunit;
using Habitica.WebApp.Assets;
using Habitica.WebApp.Components;

namespace Habitica.WebApp.Tests.Components;

public sealed class HabiticaImageTests : BunitContext
{
    [Fact]
    public void Renders_image_with_reserved_fallback_for_known_asset()
    {
        var asset = HabiticaImageAssetResolver.Skill("fireball", "Burst of Flames");

        var cut = Render<HabiticaImage>(parameters => parameters
            .Add(component => component.Asset, asset));

        var image = cut.Find("img");
        Assert.Equal("Burst of Flames", image.GetAttribute("alt"));
        Assert.Contains("shop_fireball.png", image.GetAttribute("src"));
        Assert.Contains("habitica-image-fallback hidden", cut.Find(".habitica-image-fallback").GetAttribute("class"));
    }

    [Fact]
    public void Renders_fixed_fallback_when_source_is_missing()
    {
        var asset = HabiticaImageAssetResolver.Missing(HabiticaImageKind.Gear, "unknown", "Unknown Gear");

        var cut = Render<HabiticaImage>(parameters => parameters
            .Add(component => component.Asset, asset));

        Assert.Empty(cut.FindAll("img"));
        Assert.Equal("UG", cut.Find(".habitica-image-fallback").TextContent);
        Assert.Contains("habitica-image-frame--medium", cut.Find(".habitica-image-frame").GetAttribute("class"));
    }
}
