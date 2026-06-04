using Habitica.Domain.User;
using Habitica.Rules.Pets;

namespace Habitica.Rules.Tests.Pets;

public sealed class PetFeedRecommendationFactoryTests
{
    [Fact]
    public void OrderAvailableFood_puts_highest_pet_growth_value_first()
    {
        var recommendations = PetFeedRecommendationFactory.OrderAvailableFood(
            new PetCatalogItem("Wolf-Base", "Base Wolf", "Wolf", "Base", "base"),
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["Milk"] = 2,
                ["Saddle"] = 1,
                ["Meat"] = 3
            });

        Assert.Equal(new[] { "Saddle", "Meat", "Milk" }, recommendations.Select(static item => item.Key));
        Assert.Equal(
            new[]
            {
                PetFoodRecommendationPriority.Generic,
                PetFoodRecommendationPriority.Favorite,
                PetFoodRecommendationPriority.Other
            },
            recommendations.Select(static item => item.Priority));
        Assert.Equal(new[] { 100m, 10m, 4m }, recommendations.Select(static item => item.ProgressPercent));
    }

    [Fact]
    public void OrderAvailableFood_omits_food_without_cached_inventory()
    {
        var recommendations = PetFeedRecommendationFactory.OrderAvailableFood(
            new PetCatalogItem("Wolf-Base", "Base Wolf", "Wolf", "Base", "base"),
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["Meat"] = 0,
                ["Milk"] = 1
            });

        Assert.Equal("Milk", Assert.Single(recommendations).Key);
    }

    [Fact]
    public void OrderAvailableFood_can_exclude_generic_saddle_for_normal_food_flow()
    {
        var recommendations = PetFeedRecommendationFactory.OrderAvailableFood(
            new PetCatalogItem("Wolf-Base", "Base Wolf", "Wolf", "Base", "base"),
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["Saddle"] = 1,
                ["Meat"] = 3
            },
            includeGeneric: false);

        var item = Assert.Single(recommendations);
        Assert.Equal("Meat", item.Key);
        Assert.Equal(PetFoodRecommendationPriority.Favorite, item.Priority);
    }

    [Fact]
    public void OrderAvailableFood_treats_premium_pet_food_as_full_growth()
    {
        var recommendations = PetFeedRecommendationFactory.OrderAvailableFood(
            new PetCatalogItem("Wolf-RoyalPurple", "Royal Purple Wolf", "Wolf", "RoyalPurple", "premium"),
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["Milk"] = 1
            });

        var item = Assert.Single(recommendations);
        Assert.Equal("Milk", item.Key);
        Assert.Equal(PetFoodRecommendationPriority.Favorite, item.Priority);
        Assert.Equal(10m, item.ProgressPercent);
    }
}
