namespace Habitica.Domain.Dashboard;

public sealed record PendingDamageEstimate(
    decimal TotalDamage,
    IReadOnlyList<PendingDamageSource> IncludedSources,
    IReadOnlyList<string> ExcludedSources,
    PendingDamageRisk Risk)
{
    public bool HasDamage => TotalDamage > 0m;
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
