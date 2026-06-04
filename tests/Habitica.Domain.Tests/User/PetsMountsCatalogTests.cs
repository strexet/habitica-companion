using Habitica.Domain.User;

namespace Habitica.Domain.Tests.User;

public sealed class PetsMountsCatalogTests
{
    [Fact]
    public void TryGetCreatureTypeKey_returns_egg_key_for_pet()
    {
        var pet = PetsMountsCatalog.FindPet("TigerCub-Base");

        Assert.NotNull(pet);
        Assert.True(PetsMountsCatalog.TryGetCreatureTypeKey(pet!, out var key));
        Assert.Equal("TigerCub", key);
        Assert.Equal("Tiger Cub", PetsMountsCatalog.ToCreatureTypeDisplayName(key));
    }

    [Fact]
    public void TryGetCreatureTypeKey_maps_mount_to_matching_pet_type()
    {
        var mount = PetsMountsCatalog.FindMount("FlyingPig-Base");

        Assert.NotNull(mount);
        Assert.True(PetsMountsCatalog.TryGetCreatureTypeKey(mount!, out var key));
        Assert.Equal("FlyingPig", key);
    }

    [Fact]
    public void TryGetPetKeyForMount_returns_same_key_for_growable_mount()
    {
        Assert.True(PetsMountsCatalog.TryGetPetKeyForMount("Wolf-Base", out var petKey));
        Assert.Equal("Wolf-Base", petKey);
    }

    [Fact]
    public void TryGetCreatureTypeKey_safely_derives_unknown_companion_prefix()
    {
        Assert.True(PetsMountsCatalog.TryGetCreatureTypeKey("Jackalope-RoyalPurple", out var key));
        Assert.Equal("Jackalope", key);
    }
}
