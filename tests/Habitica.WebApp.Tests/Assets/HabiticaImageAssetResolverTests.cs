using Habitica.WebApp.Assets;

namespace Habitica.WebApp.Tests.Assets;

public sealed class HabiticaImageAssetResolverTests
{
    [Fact]
    public void Gear_uses_official_static_image_key_and_display_name()
    {
        var asset = HabiticaImageAssetResolver.Gear("weapon_wizard_5", "Wizard Wand");

        Assert.Equal(HabiticaImageKind.Gear, asset.Kind);
        Assert.Equal("Wizard Wand", asset.AltText);
        Assert.Equal("WW", asset.FallbackText);
        Assert.Equal(HabiticaImageSize.Medium, asset.Size);
        Assert.Equal(
            "https://habitica-assets.s3.amazonaws.com/mobileApp/images/shop_weapon_wizard_5.png",
            asset.SourceUrl);
    }

    [Theory]
    [InlineData("fireball", "Burst of Flames", "https://habitica-assets.s3.amazonaws.com/mobileApp/images/shop_fireball.png")]
    [InlineData("protectAura", "Protective Aura", "https://habitica-assets.s3.amazonaws.com/mobileApp/images/shop_protectAura.png")]
    public void Skill_uses_official_skill_icon(string key, string label, string expectedUrl)
    {
        var asset = HabiticaImageAssetResolver.Skill(key, label);

        Assert.Equal(HabiticaImageKind.Skill, asset.Kind);
        Assert.Equal(label, asset.AltText);
        Assert.Equal(expectedUrl, asset.SourceUrl);
    }

    [Fact]
    public void Companion_assets_use_stable_pet_and_mount_keys()
    {
        var pet = HabiticaImageAssetResolver.Pet("Wolf-Base", "Wolf Base");
        var mount = HabiticaImageAssetResolver.Mount("Wolf-Base", "Wolf Base");

        Assert.Equal("https://habitica-assets.s3.amazonaws.com/mobileApp/images/Pet-Wolf-Base.png", pet.SourceUrl);
        Assert.Equal("https://habitica-assets.s3.amazonaws.com/mobileApp/images/Mount_Icon_Wolf-Base.png", mount.SourceUrl);
    }

    [Fact]
    public void Summary_inventory_assets_use_official_category_icons()
    {
        Assert.Equal("https://habitica-assets.s3.amazonaws.com/mobileApp/images/Pet_Egg_Wolf.png", HabiticaImageAssetResolver.EggSummary().SourceUrl);
        Assert.Equal("https://habitica-assets.s3.amazonaws.com/mobileApp/images/Pet_Food_Meat.png", HabiticaImageAssetResolver.FoodSummary().SourceUrl);
        Assert.Equal("https://habitica-assets.s3.amazonaws.com/mobileApp/images/Pet_HatchingPotion_Base.png", HabiticaImageAssetResolver.HatchingPotionSummary().SourceUrl);
        Assert.Equal("https://habitica-assets.s3.amazonaws.com/mobileApp/images/inventory_quest_scroll.png", HabiticaImageAssetResolver.QuestScrollSummary().SourceUrl);
    }

    [Fact]
    public void Missing_asset_preserves_fallback_without_source_url()
    {
        var asset = HabiticaImageAssetResolver.Missing(HabiticaImageKind.Quest, "unknown-quest", "Unknown Quest");

        Assert.Null(asset.SourceUrl);
        Assert.Equal("UQ", asset.FallbackText);
        Assert.Equal(HabiticaImageKind.Quest, asset.Kind);
    }
}
