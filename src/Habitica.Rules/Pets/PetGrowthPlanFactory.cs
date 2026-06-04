using Habitica.Domain.User;

namespace Habitica.Rules.Pets;

public static class PetGrowthPlanFactory
{
    public const decimal HatchedProgressPercent = 10m;
    public const decimal MountProgressPercent = 100m;
    public const decimal ProgressPointPercent = 2m;
    private const int HatchedProgressPoints = 5;

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
            .OrderAvailableFood(pet, ownedFood, includeGeneric: false)
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

    public static IReadOnlyList<PetGrowthQueueAllocation> AllocateQueue(
        IReadOnlyList<PetGrowthQueueRequest> queue,
        IReadOnlyDictionary<string, int> ownedPets,
        IReadOnlyDictionary<string, bool> ownedMounts,
        IReadOnlyDictionary<string, int> ownedFood)
    {
        var reservedFood = new Dictionary<string, int>(StringComparer.Ordinal);
        var allocations = new List<PetGrowthQueueAllocation>(queue.Count);

        foreach (var request in queue)
        {
            var plan = Create(request.PetKey, ownedPets, ownedMounts, ownedFood);
            var pet = PetsMountsCatalog.FindPet(request.PetKey);
            var selectedFood = ResolveSelectedFood(pet, plan, request.SelectedFoodKey, ownedFood);
            var selectedFoodKey = selectedFood?.Key;
            var selectedAvailable = selectedFoodKey is null ? 0 : ownedFood.GetValueOrDefault(selectedFoodKey);
            var alreadyReserved = selectedFoodKey is null ? 0 : reservedFood.GetValueOrDefault(selectedFoodKey);
            var remainingAvailable = Math.Max(0, selectedAvailable - alreadyReserved);
            var requiredAmount = selectedFood is null || !plan.CanGrowIntoMount
                ? 0
                : (int)Math.Ceiling(plan.RemainingProgressPercent / selectedFood.ProgressPercent);
            var plannedAmount = Math.Min(remainingAvailable, requiredAmount);
            var expectedProgress = selectedFood is null
                ? plan.CurrentProgressPercent
                : Math.Min(MountProgressPercent, plan.CurrentProgressPercent + plannedAmount * selectedFood.ProgressPercent);

            if (selectedFoodKey is not null && plannedAmount > 0)
            {
                reservedFood[selectedFoodKey] = alreadyReserved + plannedAmount;
            }

            allocations.Add(new PetGrowthQueueAllocation(
                request.PetKey,
                plan.MountKey,
                plan.CurrentProgressPercent,
                plan.RemainingProgressPercent,
                selectedFoodKey,
                selectedFood?.DisplayName,
                selectedFood?.Priority,
                selectedFood?.ProgressPercent ?? 0m,
                selectedAvailable,
                remainingAvailable,
                requiredAmount,
                plannedAmount,
                expectedProgress,
                plan.CanGrowIntoMount,
                expectedProgress >= MountProgressPercent,
                selectedFood is null,
                selectedFood?.Priority == PetFoodRecommendationPriority.Other,
                selectedFoodKey is not null && selectedAvailable > 0 && remainingAvailable <= 0,
                selectedFoodKey is not null && plannedAmount < requiredAmount));
        }

        return allocations;
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

    private static PetGrowthFoodOption? ResolveSelectedFood(
        PetCatalogItem? pet,
        PetGrowthPlan plan,
        string? selectedFoodKey,
        IReadOnlyDictionary<string, int> ownedFood)
    {
        if (pet is null || string.IsNullOrWhiteSpace(selectedFoodKey))
        {
            return plan.AvailableFood.FirstOrDefault();
        }

        var existing = plan.AvailableFood.FirstOrDefault(item =>
            string.Equals(item.Key, selectedFoodKey, StringComparison.Ordinal));
        if (existing is not null)
        {
            return existing;
        }

        var recommendation = PetFeedRecommendationFactory.BuildRecommendation(
            pet,
            selectedFoodKey,
            ownedFood.GetValueOrDefault(selectedFoodKey));
        return recommendation.Priority == PetFoodRecommendationPriority.Generic
            ? null
            : ToFoodOption(recommendation);
    }

    private static PetGrowthFoodOption ToFoodOption(PetFoodRecommendation recommendation)
    {
        return new PetGrowthFoodOption(
            recommendation.Key,
            recommendation.DisplayName,
            recommendation.OwnedCount,
            recommendation.Priority,
            recommendation.ProgressPercent);
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

    private static decimal NormalizeProgress(int currentProgressPoints)
    {
        var progress = Math.Max(HatchedProgressPoints, currentProgressPoints) * ProgressPointPercent;
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

public sealed record PetGrowthQueueRequest(
    string PetKey,
    string? SelectedFoodKey);

public sealed record PetGrowthQueueAllocation(
    string PetKey,
    string? MountKey,
    decimal CurrentProgressPercent,
    decimal RemainingProgressPercent,
    string? SelectedFoodKey,
    string? SelectedFoodDisplayName,
    PetFoodRecommendationPriority? SelectedFoodPriority,
    decimal SelectedFoodProgressPercent,
    int SelectedFoodAvailableCount,
    int SelectedFoodRemainingBeforeItem,
    int RequiredAmount,
    int PlannedAmount,
    decimal ExpectedProgressPercent,
    bool CanGrowIntoMount,
    bool CanReachMount,
    bool HasNoSelectedFood,
    bool UsesLessEfficientFood,
    bool SelectedFoodExhaustedByEarlierItems,
    bool HasPartialFoodPlan)
{
    public bool CanExecuteFeed => !HasNoSelectedFood && PlannedAmount > 0;
}

public enum PetGrowthUnavailableReason
{
    UnknownPet,
    PetNotOwned,
    NonGrowablePet,
    MountAlreadyOwned,
    AlreadyMountReady
}
