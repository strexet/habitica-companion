namespace Habitica.Application.Sync;

public sealed record DomainRefreshState(
    RefreshDomain Domain,
    bool IsFetching,
    DateTimeOffset? LastRefreshedAtUtc = null,
    string? LastError = null,
    RefreshReason? Reason = null,
    RefreshPriority? Priority = null,
    TimeSpan? Duration = null,
    bool Deduplicated = false);
