using Habitica.Domain.User;

namespace Habitica.Rules.Pets;

public static class PetFeedRecommendationFactory
{
    public const decimal FavoriteFoodProgressPercent = 10m;
    public const decimal OtherFoodProgressPercent = 4m;
    public const decimal GenericFoodProgressPercent = 100m;

    public static IReadOnlyList<PetFoodRecommendation> OrderAvailableFood(
        PetCatalogItem pet,
        IReadOnlyDictionary<string, int> ownedFood)
    {
        return ownedFood
            .Where(static item => item.Value > 0)
            .Select(item => BuildRecommendation(pet, item.Key, item.Value))
            .OrderByDescending(static item => item.ProgressPercent)
            .ThenBy(static item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.Key, StringComparer.Ordinal)
            .ToArray();
    }

    private static PetFoodRecommendation BuildRecommendation(PetCatalogItem pet, string foodKey, int ownedCount)
    {
        var catalogItem = PetsMountsCatalog.Food.FirstOrDefault(item =>
            string.Equals(item.Key, foodKey, StringComparison.Ordinal));
        var priority = catalogItem switch
        {
            { TargetPotionKey: not null } when string.Equals(
                catalogItem.TargetPotionKey,
                pet.HatchingPotionKey,
                StringComparison.Ordinal) => PetFoodRecommendationPriority.Favorite,
            { IsGeneric: true } => PetFoodRecommendationPriority.Generic,
            _ => PetFoodRecommendationPriority.Other
        };

        return new PetFoodRecommendation(
            foodKey,
            catalogItem?.DisplayName ?? PetsMountsCatalog.ToReadableName(foodKey),
            ownedCount,
            priority,
            GetProgressPercent(priority));
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
}

public sealed record PetFoodRecommendation(
    string Key,
    string DisplayName,
    int OwnedCount,
    PetFoodRecommendationPriority Priority,
    decimal ProgressPercent);

public enum PetFoodRecommendationPriority
{
    Favorite,
    Generic,
    Other
}
