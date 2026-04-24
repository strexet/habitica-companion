using Habitica.Domain.Sync;

namespace Habitica.Application.Sync;

public sealed class SnapshotFreshnessPolicy
{
    public SnapshotFreshnessState Classify(
        SnapshotCategory category,
        DateTimeOffset? retrievedAtUtc,
        DateTimeOffset nowUtc)
    {
        if (retrievedAtUtc is null)
        {
            return SnapshotFreshnessState.Missing;
        }

        var age = nowUtc - retrievedAtUtc.Value;

        return category switch
        {
            SnapshotCategory.VolatileGameplayState => ClassifyWindow(age, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(60)),
            SnapshotCategory.PartyActivityTimingInputs => ClassifyWindow(age, TimeSpan.FromHours(6), TimeSpan.FromHours(72)),
            SnapshotCategory.ReferenceMetadata => SnapshotFreshnessState.Fresh,
            _ => SnapshotFreshnessState.Expired
        };
    }

    private static SnapshotFreshnessState ClassifyWindow(TimeSpan age, TimeSpan freshWindow, TimeSpan staleWindow)
    {
        if (age <= freshWindow)
        {
            return SnapshotFreshnessState.Fresh;
        }

        if (age <= staleWindow)
        {
            return SnapshotFreshnessState.Stale;
        }

        return SnapshotFreshnessState.Expired;
    }
}
