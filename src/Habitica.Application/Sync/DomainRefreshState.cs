namespace Habitica.Application.Sync;

public sealed record DomainRefreshState(
    RefreshDomain Domain,
    bool IsFetching,
    DateTimeOffset? LastRefreshedAtUtc = null,
    string? LastError = null);
