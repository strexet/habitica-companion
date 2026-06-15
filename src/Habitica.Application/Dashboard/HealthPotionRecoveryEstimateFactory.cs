using Habitica.Domain.Dashboard;
using Habitica.Domain.User;

namespace Habitica.Application.Dashboard;

public sealed class HealthPotionRecoveryEstimateFactory
{
    public const decimal HealthPotionGoldCost = 25m;
    public const decimal HealthPotionHealing = 15m;

    private const decimal LowPostCronHealthThreshold = 15m;

    public HealthPotionRecoveryEstimate Create(UserSnapshot user, PendingDamageEstimate damageEstimate)
    {
        var maximumHealth = user.MaxHealth;
        var healthMissing = maximumHealth > 0m
            ? Math.Max(0m, maximumHealth - user.Health)
            : 0m;
        var maximumUsefulPotionCount = healthMissing <= 0m
            ? 0
            : (int)Math.Ceiling(healthMissing / HealthPotionHealing);
        var affordablePotionCount = user.Gold < HealthPotionGoldCost
            ? 0
            : (int)Math.Floor(user.Gold / HealthPotionGoldCost);
        var effectiveOnePotionHealing = maximumHealth > 0m
            ? Math.Min(HealthPotionHealing, healthMissing)
            : 0m;
        var expectedHealthAfterOnePotion = maximumHealth > 0m
            ? Math.Min(maximumHealth, user.Health + HealthPotionHealing)
            : user.Health;
        var expectedHealthAfterOnePotionAndCron = Math.Max(0m, expectedHealthAfterOnePotion - damageEstimate.TotalDamage);
        var uncappedHealthAfterCron = user.Health - damageEstimate.TotalDamage;
        var minimumSurvivalPotionCount = GetMinimumSurvivalPotionCount(user.Health, damageEstimate.TotalDamage);
        var recommendedPotionCount = ResolveRecommendedPotionCount(
            damageEstimate,
            uncappedHealthAfterCron,
            maximumUsefulPotionCount,
            affordablePotionCount,
            minimumSurvivalPotionCount);
        var recommendedCountHealthAfterCron = Math.Min(maximumHealth, user.Health + recommendedPotionCount * HealthPotionHealing)
            - damageEstimate.TotalDamage;
        var recommendedCountRemovesKnockoutRisk = recommendedPotionCount > 0
            && recommendedCountHealthAfterCron > 0m
            && damageEstimate.Readiness != PendingDamageReadiness.Incomplete;
        var shouldShow = maximumUsefulPotionCount > 0
            && !damageEstimate.IsDamagePausedByInn
            && (damageEstimate.TotalDamage > 0m
                || damageEstimate.Risk is PendingDamageRisk.Danger or PendingDamageRisk.Warning
                || damageEstimate.EstimatedHealthAfterCron <= LowPostCronHealthThreshold);
        var canBuySinglePotion = maximumUsefulPotionCount > 0 && affordablePotionCount > 0;

        return new HealthPotionRecoveryEstimate(
            user.Health,
            maximumHealth,
            user.Gold,
            HealthPotionHealing,
            HealthPotionGoldCost,
            effectiveOnePotionHealing,
            damageEstimate.TotalDamage,
            damageEstimate.EstimatedHealthAfterCron,
            expectedHealthAfterOnePotion,
            expectedHealthAfterOnePotionAndCron,
            maximumUsefulPotionCount,
            affordablePotionCount,
            recommendedPotionCount,
            shouldShow,
            canBuySinglePotion,
            damageEstimate.Readiness == PendingDamageReadiness.Incomplete,
            recommendedCountRemovesKnockoutRisk,
            BuildRecommendationText(
                damageEstimate,
                minimumSurvivalPotionCount,
                recommendedPotionCount,
                maximumUsefulPotionCount,
                affordablePotionCount,
                recommendedCountRemovesKnockoutRisk),
            BuildAvailabilityText(maximumUsefulPotionCount, affordablePotionCount));
    }

    private static int GetMinimumSurvivalPotionCount(decimal currentHealth, decimal estimatedDamage)
    {
        if (estimatedDamage < currentHealth)
        {
            return 0;
        }

        return (int)Math.Floor((estimatedDamage - currentHealth) / HealthPotionHealing) + 1;
    }

    private static int ResolveRecommendedPotionCount(
        PendingDamageEstimate damageEstimate,
        decimal uncappedHealthAfterCron,
        int maximumUsefulPotionCount,
        int affordablePotionCount,
        int minimumSurvivalPotionCount)
    {
        if (maximumUsefulPotionCount <= 0 || affordablePotionCount <= 0)
        {
            return 0;
        }

        if (damageEstimate.Risk == PendingDamageRisk.Danger && minimumSurvivalPotionCount > 0)
        {
            return Math.Min(minimumSurvivalPotionCount, Math.Min(maximumUsefulPotionCount, affordablePotionCount));
        }

        if (damageEstimate.TotalDamage > 0m && uncappedHealthAfterCron < LowPostCronHealthThreshold)
        {
            return Math.Min(1, Math.Min(maximumUsefulPotionCount, affordablePotionCount));
        }

        return 0;
    }

    private static string BuildRecommendationText(
        PendingDamageEstimate damageEstimate,
        int minimumSurvivalPotionCount,
        int recommendedPotionCount,
        int maximumUsefulPotionCount,
        int affordablePotionCount,
        bool recommendedCountRemovesKnockoutRisk)
    {
        if (damageEstimate.Risk == PendingDamageRisk.Danger)
        {
            if (damageEstimate.Readiness == PendingDamageReadiness.Incomplete && recommendedPotionCount > 0)
            {
                return recommendedPotionCount == 1
                    ? "1 Health Potion improves your post-CRON HP for this incomplete estimate."
                    : $"{recommendedPotionCount} Health Potions improve your post-CRON HP for this incomplete estimate.";
            }

            if (recommendedCountRemovesKnockoutRisk)
            {
                return recommendedPotionCount == 1
                    ? "1 Health Potion removes the current knockout risk."
                    : $"{recommendedPotionCount} Health Potions remove the current knockout risk.";
            }

            if (affordablePotionCount <= 0 || affordablePotionCount < minimumSurvivalPotionCount)
            {
                return "Not enough gold for the recommended recovery.";
            }

            if (minimumSurvivalPotionCount > maximumUsefulPotionCount)
            {
                return "Health Potion recovery cannot fully remove the current knockout risk.";
            }
        }

        if (recommendedPotionCount > 0)
        {
            return recommendedPotionCount == 1
                ? "1 Health Potion improves your post-CRON HP."
                : $"{recommendedPotionCount} Health Potions improve your post-CRON HP.";
        }

        return damageEstimate.Readiness == PendingDamageReadiness.Incomplete
            ? "Damage estimate incomplete - recovery cannot guarantee survival."
            : "Potion recovery is optional for this CRON estimate.";
    }

    private static string BuildAvailabilityText(int maximumUsefulPotionCount, int affordablePotionCount)
    {
        if (maximumUsefulPotionCount <= 0)
        {
            return "Health is already full.";
        }

        return affordablePotionCount <= 0
            ? "Needs 25 GP."
            : "Ready to buy one Health Potion.";
    }
}
