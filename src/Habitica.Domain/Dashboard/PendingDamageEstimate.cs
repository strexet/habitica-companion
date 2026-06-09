namespace Habitica.Domain.Dashboard;

public sealed record PendingDamageEstimate(
    decimal TotalDamage,
    decimal EstimatedHealthAfterCron,
    IReadOnlyList<PendingDamageSource> IncludedSources,
    IReadOnlyList<string> ExcludedSources,
    PendingDamageRisk Risk,
    PendingDamageReadiness Readiness)
{
    public bool HasDamage => TotalDamage > 0m;

    public bool HasUnknownSources => Readiness is PendingDamageReadiness.Incomplete;
}

public sealed record PendingDamageSource(
    string Label,
    decimal Damage,
    string Detail);

public sealed record HealthPotionRecoveryEstimate(
    decimal CurrentHealth,
    decimal MaximumHealth,
    decimal CurrentGold,
    decimal PotionHealing,
    decimal PotionGoldCost,
    decimal EffectiveHealingFromOnePotion,
    decimal EstimatedDamage,
    decimal EstimatedHealthAfterCron,
    decimal ExpectedHealthAfterOnePotion,
    decimal ExpectedHealthAfterOnePotionAndCron,
    int MaximumUsefulPotionCount,
    int AffordablePotionCount,
    int RecommendedPotionCount,
    bool ShouldShow,
    bool CanBuySinglePotion,
    bool IsBasedOnIncompleteDamageEstimate,
    bool RecommendedCountRemovesKnockoutRisk,
    string RecommendationText,
    string AvailabilityText);

public enum PendingDamageRisk
{
    None,
    Info,
    Warning,
    Danger
}

public enum PendingDamageReadiness
{
    High,
    Estimated,
    Incomplete
}
