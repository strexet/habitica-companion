using Habitica.Domain.User;

namespace Habitica.Rules.Pets;

public static class PetGrowthPlanFactory
{
    public const decimal HatchedProgressPercent = 10m;
    public const decimal FavoriteFoodProgressPercent = 10m;
    public const decimal OtherFoodProgressPercent = 4m;
    public const decimal GenericFoodProgressPercent = 100m;
    public const decimal MountProgressPercent = 100m;
    public const decimal ProgressPointPercent = 2m;

    public static PetGrowthPlan Create(
        PetCatalogItem pet,
        int currentProgressPoints,
        IReadOnlyDictionary<string, int> ownedFood,
        bool matchingMountOwned)
    {
        if (!PetsMountsCatalog.TryGetGrowableMountKey(pet, out var mountKey))
        {
            return Unavailable(
                pet.Key,
                null,
                PetGrowthUnavailableReason.NonGrowablePet,
                NormalizeProgress(currentProgressPoints));
        }

        var currentProgress = matchingMountOwned
            ? MountProgressPercent
            : NormalizeProgress(currentProgressPoints);
        var remainingProgress = Math.Max(0m, MountProgressPercent - currentProgress);

        if (matchingMountOwned)
        {
            return new PetGrowthPlan(
                pet.Key,
                mountKey,
                currentProgress,
                remainingProgress,
                false,
                true,
                PetGrowthUnavailableReason.MountAlreadyOwned,
                Array.Empty<PetGrowthFoodOption>(),
                Array.Empty<PetGrowthFeedPlanItem>(),
                0m,
                false);
        }

        var foodOptions = PetFeedRecommendationFactory
            .OrderAvailableFood(pet, ownedFood)
            .Select(ToFoodOption)
            .ToArray();
        var feedPlan = BuildFeedPlan(foodOptions, remainingProgress, out var missingProgress);

        return new PetGrowthPlan(
            pet.Key,
            mountKey,
            currentProgress,
            remainingProgress,
            remainingProgress > 0m,
            false,
            remainingProgress <= 0m ? PetGrowthUnavailableReason.AlreadyMountReady : null,
            foodOptions,
            feedPlan,
            missingProgress,
            missingProgress <= 0m);
    }

    public static PetGrowthPlan Create(
        string petKey,
        IReadOnlyDictionary<string, int> ownedPets,
        IReadOnlyDictionary<string, bool> ownedMounts,
        IReadOnlyDictionary<string, int> ownedFood)
    {
        var pet = PetsMountsCatalog.FindPet(petKey);
        if (pet is null)
        {
            return Unavailable(petKey, null, PetGrowthUnavailableReason.UnknownPet, 0m);
        }

        if (!ownedPets.TryGetValue(petKey, out var currentProgressPoints) || currentProgressPoints < 0)
        {
            PetsMountsCatalog.TryGetGrowableMountKey(pet, out var unavailableMountKey);
            return Unavailable(
                petKey,
                string.IsNullOrEmpty(unavailableMountKey) ? null : unavailableMountKey,
                PetGrowthUnavailableReason.PetNotOwned,
                0m);
        }

        var matchingMountOwned = PetsMountsCatalog.TryGetGrowableMountKey(pet, out var mountKey)
            && ownedMounts.GetValueOrDefault(mountKey);

        return Create(pet, currentProgressPoints, ownedFood, matchingMountOwned);
    }

    private static PetGrowthPlan Unavailable(
        string petKey,
        string? mountKey,
        PetGrowthUnavailableReason reason,
        decimal currentProgressPercent)
    {
        return new PetGrowthPlan(
            petKey,
            mountKey,
            currentProgressPercent,
            Math.Max(0m, MountProgressPercent - currentProgressPercent),
            false,
            false,
            reason,
            Array.Empty<PetGrowthFoodOption>(),
            Array.Empty<PetGrowthFeedPlanItem>(),
            Math.Max(0m, MountProgressPercent - currentProgressPercent),
            false);
    }

    private static PetGrowthFoodOption ToFoodOption(PetFoodRecommendation recommendation)
    {
        return new PetGrowthFoodOption(
            recommendation.Key,
            recommendation.DisplayName,
            recommendation.OwnedCount,
            recommendation.Priority,
            GetProgressPercent(recommendation.Priority));
    }

    private static PetGrowthFeedPlanItem[] BuildFeedPlan(
        IReadOnlyList<PetGrowthFoodOption> foodOptions,
        decimal remainingProgress,
        out decimal missingProgress)
    {
        var plan = new List<PetGrowthFeedPlanItem>();
        var remaining = remainingProgress;

        foreach (var food in foodOptions)
        {
            if (remaining <= 0m)
            {
                break;
            }

            var needed = (int)Math.Ceiling(remaining / food.ProgressPercent);
            var amount = Math.Min(food.OwnedCount, needed);
            if (amount <= 0)
            {
                continue;
            }

            plan.Add(new PetGrowthFeedPlanItem(
                food.Key,
                food.DisplayName,
                amount,
                food.OwnedCount,
                food.Priority,
                food.ProgressPercent));
            remaining -= amount * food.ProgressPercent;
        }

        missingProgress = Math.Max(0m, remaining);
        return plan.ToArray();
    }

    private static decimal GetProgressPercent(PetFoodRecommendationPriority priority)
    {
        return priority switch
        {
            PetFoodRecommendationPriority.Favorite => FavoriteFoodProgressPercent,
            PetFoodRecommendationPriority.Generic => GenericFoodProgressPercent,
            _ => OtherFoodProgressPercent
        };
    }

    private static decimal NormalizeProgress(int currentProgressPoints)
    {
        var progress = HatchedProgressPercent + Math.Max(0, currentProgressPoints) * ProgressPointPercent;
        return Math.Clamp(progress, HatchedProgressPercent, MountProgressPercent);
    }
}

public sealed record PetGrowthPlan(
    string PetKey,
    string? MountKey,
    decimal CurrentProgressPercent,
    decimal RemainingProgressPercent,
    bool CanGrowIntoMount,
    bool MatchingMountOwned,
    PetGrowthUnavailableReason? UnavailableReason,
    IReadOnlyList<PetGrowthFoodOption> AvailableFood,
    IReadOnlyList<PetGrowthFeedPlanItem> FeedPlan,
    decimal MissingProgressAfterPlanPercent,
    bool CanCompleteWithAvailableFood);

public sealed record PetGrowthFoodOption(
    string Key,
    string DisplayName,
    int OwnedCount,
    PetFoodRecommendationPriority Priority,
    decimal ProgressPercent);

public sealed record PetGrowthFeedPlanItem(
    string FoodKey,
    string DisplayName,
    int Amount,
    int OwnedCount,
    PetFoodRecommendationPriority Priority,
    decimal ProgressPercent);

public enum PetGrowthUnavailableReason
{
    UnknownPet,
    PetNotOwned,
    NonGrowablePet,
    MountAlreadyOwned,
    AlreadyMountReady
}
