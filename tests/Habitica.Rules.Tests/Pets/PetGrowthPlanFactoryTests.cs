using Habitica.Domain.User;
using Habitica.Rules.Pets;

namespace Habitica.Rules.Tests.Pets;

public sealed class PetGrowthPlanFactoryTests
{
    [Fact]
    public void Create_plans_nine_favorite_food_for_newly_hatched_pet()
    {
        var plan = PetGrowthPlanFactory.Create(
            BaseWolf,
            currentProgressPoints: 0,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["Meat"] = 9
            },
            matchingMountOwned: false);

        Assert.Equal(10m, plan.CurrentProgressPercent);
        Assert.Equal(90m, plan.RemainingProgressPercent);
        Assert.True(plan.CanGrowIntoMount);
        Assert.True(plan.CanCompleteWithAvailableFood);
        var item = Assert.Single(plan.FeedPlan);
        Assert.Equal("Meat", item.FoodKey);
        Assert.Equal(9, item.Amount);
        Assert.Equal(PetFoodRecommendationPriority.Favorite, item.Priority);
        Assert.Equal(10m, item.ProgressPercent);
    }

    [Fact]
    public void Create_plans_twenty_three_non_favorite_food_when_no_favorite_or_generic_food_exists()
    {
        var plan = PetGrowthPlanFactory.Create(
            BaseWolf,
            currentProgressPoints: 0,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["Milk"] = 23
            },
            matchingMountOwned: false);

        var item = Assert.Single(plan.FeedPlan);
        Assert.Equal("Milk", item.FoodKey);
        Assert.Equal(23, item.Amount);
        Assert.Equal(PetFoodRecommendationPriority.Other, item.Priority);
        Assert.Equal(4m, item.ProgressPercent);
        Assert.True(plan.CanCompleteWithAvailableFood);
    }

    [Fact]
    public void Create_accounts_for_partial_progress()
    {
        var plan = PetGrowthPlanFactory.Create(
            BaseWolf,
            currentProgressPoints: 15,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["Meat"] = 5
            },
            matchingMountOwned: false);

        Assert.Equal(40m, plan.CurrentProgressPercent);
        Assert.Equal(60m, plan.RemainingProgressPercent);
        var item = Assert.Single(plan.FeedPlan);
        Assert.Equal(5, item.Amount);
        Assert.Equal(10m, plan.MissingProgressAfterPlanPercent);
        Assert.False(plan.CanCompleteWithAvailableFood);
    }

    [Fact]
    public void Create_marks_already_complete_pet_without_feed_plan()
    {
        var plan = PetGrowthPlanFactory.Create(
            BaseWolf,
            currentProgressPoints: 45,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["Meat"] = 9
            },
            matchingMountOwned: false);

        Assert.Equal(100m, plan.CurrentProgressPercent);
        Assert.Equal(0m, plan.RemainingProgressPercent);
        Assert.False(plan.CanGrowIntoMount);
        Assert.Equal(PetGrowthUnavailableReason.AlreadyMountReady, plan.UnavailableReason);
        Assert.Empty(plan.FeedPlan);
    }

    [Fact]
    public void Create_rejects_unknown_pet_key()
    {
        var plan = PetGrowthPlanFactory.Create(
            "Unknown-Base",
            new Dictionary<string, int>(StringComparer.Ordinal),
            new Dictionary<string, bool>(StringComparer.Ordinal),
            new Dictionary<string, int>(StringComparer.Ordinal));

        Assert.Equal(PetGrowthUnavailableReason.UnknownPet, plan.UnavailableReason);
        Assert.False(plan.CanGrowIntoMount);
        Assert.Null(plan.MountKey);
    }

    [Fact]
    public void Create_rejects_missing_owned_pet_key()
    {
        var plan = PetGrowthPlanFactory.Create(
            "Wolf-Base",
            new Dictionary<string, int>(StringComparer.Ordinal),
            new Dictionary<string, bool>(StringComparer.Ordinal),
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["Meat"] = 9
            });

        Assert.Equal(PetGrowthUnavailableReason.PetNotOwned, plan.UnavailableReason);
        Assert.Equal("Wolf-Base", plan.MountKey);
        Assert.False(plan.CanGrowIntoMount);
        Assert.Empty(plan.FeedPlan);
    }

    [Fact]
    public void Create_rejects_wacky_pet_without_mount()
    {
        var plan = PetGrowthPlanFactory.Create(
            new PetCatalogItem("Wolf-Veggie", "Veggie Wolf", "Wolf", "Veggie", "wacky"),
            currentProgressPoints: 0,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["Meat"] = 9
            },
            matchingMountOwned: false);

        Assert.Equal(PetGrowthUnavailableReason.NonGrowablePet, plan.UnavailableReason);
        Assert.False(plan.CanGrowIntoMount);
        Assert.Null(plan.MountKey);
        Assert.Empty(plan.FeedPlan);
    }

    [Fact]
    public void Create_rejects_pet_when_matching_mount_is_owned()
    {
        var plan = PetGrowthPlanFactory.Create(
            BaseWolf,
            currentProgressPoints: 0,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["Meat"] = 9
            },
            matchingMountOwned: true);

        Assert.Equal(PetGrowthUnavailableReason.MountAlreadyOwned, plan.UnavailableReason);
        Assert.Equal("Wolf-Base", plan.MountKey);
        Assert.Equal(100m, plan.CurrentProgressPercent);
        Assert.False(plan.CanGrowIntoMount);
        Assert.True(plan.MatchingMountOwned);
        Assert.False(plan.CanCompleteWithAvailableFood);
        Assert.Empty(plan.FeedPlan);
    }

    [Fact]
    public void Create_preserves_recommendation_order_and_builds_mixed_plan_without_mutating_owned_food()
    {
        var ownedFood = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Milk"] = 20,
            ["Meat"] = 2,
            ["Saddle"] = 1
        };

        var plan = PetGrowthPlanFactory.Create(
            BaseWolf,
            currentProgressPoints: 0,
            ownedFood,
            matchingMountOwned: false);

        Assert.Equal(new[] { "Meat", "Saddle", "Milk" }, plan.AvailableFood.Select(static item => item.Key));
        Assert.Equal(new[] { "Meat", "Saddle" }, plan.FeedPlan.Select(static item => item.FoodKey));
        Assert.Equal(new[] { 2, 1 }, plan.FeedPlan.Select(static item => item.Amount));
        Assert.True(plan.CanCompleteWithAvailableFood);
        Assert.Equal(2, ownedFood["Meat"]);
        Assert.Equal(1, ownedFood["Saddle"]);
        Assert.Equal(20, ownedFood["Milk"]);
    }

    private static PetCatalogItem BaseWolf => new("Wolf-Base", "Base Wolf", "Wolf", "Base", "base");
}
