namespace Habitica.Domain.Sync;

public enum SnapshotFreshnessState
{
    Missing,
    Fresh,
    Stale,
    Expired
}
