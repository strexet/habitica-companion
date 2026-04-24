using Habitica.Application.Sync;
using Habitica.Domain.Sync;

namespace Habitica.Application.Tests.Sync;

public sealed class SnapshotFreshnessPolicyTests
{
    private readonly SnapshotFreshnessPolicy _policy = new();

    [Fact]
    public void Classify_returns_fresh_for_recent_volatile_gameplay_snapshots()
    {
        var freshness = _policy.Classify(
            SnapshotCategory.VolatileGameplayState,
            DateTimeOffset.Parse("2026-04-24T11:57:00Z"),
            DateTimeOffset.Parse("2026-04-24T12:00:00Z"));

        Assert.Equal(SnapshotFreshnessState.Fresh, freshness);
    }

    [Fact]
    public void Classify_returns_stale_for_mid_age_volatile_gameplay_snapshots()
    {
        var freshness = _policy.Classify(
            SnapshotCategory.VolatileGameplayState,
            DateTimeOffset.Parse("2026-04-24T11:40:00Z"),
            DateTimeOffset.Parse("2026-04-24T12:00:00Z"));

        Assert.Equal(SnapshotFreshnessState.Stale, freshness);
    }

    [Fact]
    public void Classify_returns_expired_for_old_volatile_gameplay_snapshots()
    {
        var freshness = _policy.Classify(
            SnapshotCategory.VolatileGameplayState,
            DateTimeOffset.Parse("2026-04-24T10:30:00Z"),
            DateTimeOffset.Parse("2026-04-24T12:00:00Z"));

        Assert.Equal(SnapshotFreshnessState.Expired, freshness);
    }
}
