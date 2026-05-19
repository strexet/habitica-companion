namespace Habitica.Application.Sync;

public enum RefreshReason
{
    AppBoot,
    ManualRefresh,
    PageEntered,
    MutationCompleted,
    SnapshotStale
}
