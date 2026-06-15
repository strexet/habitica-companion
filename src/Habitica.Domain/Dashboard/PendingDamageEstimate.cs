namespace Habitica.Domain.Dashboard;

public sealed record PendingDamageEstimate(
    decimal TotalDamage,
    decimal EstimatedHealthAfterCron,
    IReadOnlyList<PendingDamageSource> IncludedSources,
    IReadOnlyList<string> ExcludedSources,
    PendingDamageRisk Risk,
    PendingDamageReadiness Readiness,
    decimal EstimatedDailyDamage = 0m,
    decimal EstimatedBossDamage = 0m,
    int IncludedDailyCount = 0,
    int UnknownDueDailyCount = 0,
    int MissingTaskValueCount = 0,
    bool UsesComputedConstitution = false,
    decimal? EffectiveConstitution = null,
    bool MissingComputedStatInputs = false,
    bool MissingChecklistData = false,
    bool BossDamageUnavailable = false,
    bool IsDamagePausedByInn = false,
    IReadOnlyList<PendingDamageDiagnostic>? Diagnostics = null)
{
    public bool HasDamage => TotalDamage > 0m;

    public bool HasUnknownSources => Readiness is PendingDamageReadiness.Incomplete;

    public IReadOnlyList<PendingDamageDiagnostic> DiagnosticItems => Diagnostics ?? Array.Empty<PendingDamageDiagnostic>();
}

public sealed record PendingDamageSource(
    string Label,
    decimal Damage,
    string Detail);

public sealed record PendingDamageDiagnostic(
    string SourceKind,
    string SourceId,
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
