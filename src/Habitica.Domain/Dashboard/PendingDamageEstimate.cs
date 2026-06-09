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
