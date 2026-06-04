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
            currentProgressPoints: 5,
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
            currentProgressPoints: 5,
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

        Assert.Equal(30m, plan.CurrentProgressPercent);
        Assert.Equal(70m, plan.RemainingProgressPercent);
        var item = Assert.Single(plan.FeedPlan);
        Assert.Equal(5, item.Amount);
        Assert.Equal(20m, plan.MissingProgressAfterPlanPercent);
        Assert.False(plan.CanCompleteWithAvailableFood);
    }

    [Fact]
    public void Create_marks_already_complete_pet_without_feed_plan()
    {
        var plan = PetGrowthPlanFactory.Create(
            BaseWolf,
            currentProgressPoints: 50,
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
            currentProgressPoints: 5,
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
            currentProgressPoints: 5,
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
    public void Create_uses_highest_value_normal_food_first_without_mutating_owned_food()
    {
        var ownedFood = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Milk"] = 20,
            ["Meat"] = 2,
            ["Saddle"] = 1
        };

        var plan = PetGrowthPlanFactory.Create(
            BaseWolf,
            currentProgressPoints: 5,
            ownedFood,
            matchingMountOwned: false);

        Assert.Equal(new[] { "Meat", "Milk" }, plan.AvailableFood.Select(static item => item.Key));
        Assert.Equal(new[] { "Meat", "Milk" }, plan.FeedPlan.Select(static item => item.FoodKey));
        Assert.Equal(2, plan.FeedPlan[0].Amount);
        Assert.Equal(18, plan.FeedPlan[1].Amount);
        Assert.True(plan.CanCompleteWithAvailableFood);
        Assert.Equal(2, ownedFood["Meat"]);
        Assert.Equal(1, ownedFood["Saddle"]);
        Assert.Equal(20, ownedFood["Milk"]);
    }

    [Fact]
    public void AllocateQueue_reserves_earlier_pet_food_before_later_pet()
    {
        var allocations = PetGrowthPlanFactory.AllocateQueue(
            [
                new PetGrowthQueueRequest("Wolf-Base", "Meat"),
                new PetGrowthQueueRequest("TigerCub-Base", "Meat")
            ],
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["Wolf-Base"] = 5,
                ["TigerCub-Base"] = 5
            },
            new Dictionary<string, bool>(StringComparer.Ordinal),
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["Meat"] = 9
            });

        Assert.Equal(2, allocations.Count);
        Assert.Equal(9, allocations[0].PlannedAmount);
        Assert.Equal(100m, allocations[0].ExpectedProgressPercent);
        Assert.True(allocations[0].CanReachMount);
        Assert.Equal(0, allocations[1].PlannedAmount);
        Assert.Equal(0, allocations[1].SelectedFoodRemainingBeforeItem);
        Assert.True(allocations[1].SelectedFoodExhaustedByEarlierItems);
    }

    [Fact]
    public void AllocateQueue_marks_non_favorite_partial_food_plan()
    {
        var allocation = Assert.Single(PetGrowthPlanFactory.AllocateQueue(
            [new PetGrowthQueueRequest("Wolf-Base", "Milk")],
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["Wolf-Base"] = 5
            },
            new Dictionary<string, bool>(StringComparer.Ordinal),
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["Milk"] = 2
            }));

        Assert.Equal(PetFoodRecommendationPriority.Other, allocation.SelectedFoodPriority);
        Assert.True(allocation.UsesLessEfficientFood);
        Assert.True(allocation.HasPartialFoodPlan);
        Assert.Equal(2, allocation.PlannedAmount);
        Assert.Equal(18m, allocation.ExpectedProgressPercent);
    }

    private static PetCatalogItem BaseWolf => new("Wolf-Base", "Base Wolf", "Wolf", "Base", "base");
}
