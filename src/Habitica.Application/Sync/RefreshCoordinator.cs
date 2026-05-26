using System.Globalization;
using Habitica.Api;
using Habitica.Application.Diagnostics;
using Habitica.Domain.Auth;
using Habitica.Domain.Diagnostics;
using Habitica.Domain.Party;
using Habitica.Storage;

namespace Habitica.Application.Sync;

public sealed class RefreshCoordinator
{
    private readonly IHabiticaSyncClient _habiticaSyncClient;
    private readonly IUserSnapshotStore _userSnapshotStore;
    private readonly ITaskSnapshotStore _taskSnapshotStore;
    private readonly IPartySnapshotStore _partySnapshotStore;
    private readonly IPartyCronHistoryStore _partyCronHistoryStore;
    private readonly IGearCatalogStore _gearCatalogStore;
    private readonly DiagnosticsLogWriter _diagnosticsLogWriter;
    private readonly SnapshotFreshnessPolicy _snapshotFreshnessPolicy;
    private readonly TimeProvider _timeProvider;

    private readonly Dictionary<RefreshDomain, Task<DomainRefreshState>> _inflight = new();

    public RefreshCoordinator(
        IHabiticaSyncClient habiticaSyncClient,
        IUserSnapshotStore userSnapshotStore,
        ITaskSnapshotStore taskSnapshotStore,
        IPartySnapshotStore partySnapshotStore,
        IPartyCronHistoryStore partyCronHistoryStore,
        IGearCatalogStore gearCatalogStore,
        DiagnosticsLogWriter diagnosticsLogWriter,
        SnapshotFreshnessPolicy snapshotFreshnessPolicy,
        TimeProvider timeProvider)
    {
        _habiticaSyncClient = habiticaSyncClient;
        _userSnapshotStore = userSnapshotStore;
        _taskSnapshotStore = taskSnapshotStore;
        _partySnapshotStore = partySnapshotStore;
        _partyCronHistoryStore = partyCronHistoryStore;
        _gearCatalogStore = gearCatalogStore;
        _diagnosticsLogWriter = diagnosticsLogWriter;
        _snapshotFreshnessPolicy = snapshotFreshnessPolicy;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<DomainRefreshState>> RefreshDomainsAsync(
        HabiticaCredentials credentials,
        IReadOnlyList<(RefreshDomain Domain, RefreshPriority Priority)> requests,
        Action<DomainRefreshState> onDomainCompleted,
        CancellationToken cancellationToken,
        RefreshReason reason = RefreshReason.ManualRefresh)
    {
        var grouped = requests
            .GroupBy(static r => r.Priority)
            .OrderBy(static g => (int)g.Key);

        var results = new List<DomainRefreshState>();

        foreach (var priorityGroup in grouped)
        {
            var domains = priorityGroup
                .Select(static r => r.Domain)
                .Distinct()
                .ToArray();

            var tasks = new List<(RefreshDomain Domain, Task<DomainRefreshState> Task)>();

            foreach (var domain in domains)
            {
                if (domain is RefreshDomain.CloudSync or RefreshDomain.PartySync)
                {
                    continue;
                }

                var task = GetOrStartDomainRefreshAsync(domain, credentials, reason, priorityGroup.Key, cancellationToken);
                tasks.Add((domain, task));
            }

            foreach (var (domain, task) in tasks)
            {
                var state = await task;
                results.Add(state);
                onDomainCompleted(state);
            }
        }

        return results;
    }

    public async Task<DomainRefreshState> RefreshSingleDomainAsync(
        RefreshDomain domain,
        HabiticaCredentials credentials,
        CancellationToken cancellationToken)
    {
        return await GetOrStartDomainRefreshAsync(domain, credentials, RefreshReason.ManualRefresh, RefreshPriority.Visible, cancellationToken);
    }

    private Task<DomainRefreshState> GetOrStartDomainRefreshAsync(
        RefreshDomain domain,
        HabiticaCredentials credentials,
        RefreshReason reason,
        RefreshPriority priority,
        CancellationToken cancellationToken)
    {
        if (_inflight.TryGetValue(domain, out var existing) && !existing.IsCompleted)
        {
            return MarkDeduplicatedAsync(existing, cancellationToken);
        }

        var task = DispatchDomainAsync(domain, credentials, reason, priority, cancellationToken);
        _inflight[domain] = task;
        return task;
    }

    private async Task<DomainRefreshState> DispatchDomainAsync(
        RefreshDomain domain,
        HabiticaCredentials credentials,
        RefreshReason reason,
        RefreshPriority priority,
        CancellationToken cancellationToken)
    {
        var startedAtUtc = _timeProvider.GetUtcNow();
        try
        {
            var result = domain switch
            {
                RefreshDomain.UserProfile => await RefreshUserProfileAsync(credentials, cancellationToken),
                RefreshDomain.Tasks => await RefreshTasksAsync(credentials, cancellationToken),
                RefreshDomain.Party => await RefreshPartyAsync(credentials, cancellationToken),
                RefreshDomain.GearCatalog => await RefreshGearCatalogAsync(credentials, cancellationToken),
                _ => new DomainRefreshState(domain, false, null, $"Unsupported domain: {domain}")
            };

            var completed = result with
            {
                Reason = reason,
                Priority = priority,
                Duration = _timeProvider.GetUtcNow() - startedAtUtc
            };
            await WriteRefreshDiagnosticsAsync(completed, cancellationToken);
            return completed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failed = new DomainRefreshState(
                domain,
                false,
                LastError: exception.Message,
                Reason: reason,
                Priority: priority,
                Duration: _timeProvider.GetUtcNow() - startedAtUtc);
            await WriteRefreshDiagnosticsAsync(failed, cancellationToken);
            return failed;
        }
        finally
        {
            _inflight.Remove(domain);
        }
    }

    private async Task<DomainRefreshState> MarkDeduplicatedAsync(
        Task<DomainRefreshState> existing,
        CancellationToken cancellationToken)
    {
        var result = await existing;
        var deduplicated = result with { Deduplicated = true };
        await WriteRefreshDiagnosticsAsync(deduplicated, cancellationToken);
        return deduplicated;
    }

    private Task WriteRefreshDiagnosticsAsync(
        DomainRefreshState state,
        CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["domain"] = state.Domain.ToString(),
            ["reason"] = state.Reason?.ToString() ?? string.Empty,
            ["priority"] = state.Priority?.ToString() ?? string.Empty,
            ["durationMs"] = Math.Round(state.Duration?.TotalMilliseconds ?? 0d).ToString(CultureInfo.InvariantCulture),
            ["deduplicated"] = state.Deduplicated.ToString(CultureInfo.InvariantCulture)
        };

        if (!string.IsNullOrWhiteSpace(state.LastError))
        {
            metadata["error"] = state.LastError;
        }

        return _diagnosticsLogWriter.WriteAsync(
            DiagnosticsFeatureArea.Sync,
            $"refresh-{state.Domain.ToString().ToLowerInvariant()}",
            string.IsNullOrWhiteSpace(state.LastError) ? DiagnosticsSeverity.Success : DiagnosticsSeverity.Warning,
            string.IsNullOrWhiteSpace(state.LastError) ? DiagnosticsMode.LiveRead : DiagnosticsMode.Local,
            string.IsNullOrWhiteSpace(state.LastError)
                ? $"Refreshed {state.Domain}."
                : $"Refresh failed for {state.Domain}: {state.LastError}",
            metadata,
            cancellationToken);
    }

    private async Task<DomainRefreshState> RefreshUserProfileAsync(
        HabiticaCredentials credentials,
        CancellationToken cancellationToken)
    {
        var user = await _habiticaSyncClient.GetUserSnapshotAsync(credentials, cancellationToken);
        await _userSnapshotStore.SaveAsync(user, cancellationToken);
        return new DomainRefreshState(RefreshDomain.UserProfile, false, user.RetrievedAtUtc, null);
    }

    private async Task<DomainRefreshState> RefreshTasksAsync(
        HabiticaCredentials credentials,
        CancellationToken cancellationToken)
    {
        var tasks = await _habiticaSyncClient.GetTasksAsync(credentials, cancellationToken);
        await _taskSnapshotStore.SaveAsync(tasks, cancellationToken);
        return new DomainRefreshState(RefreshDomain.Tasks, false, tasks.RetrievedAtUtc, null);
    }

    private async Task<DomainRefreshState> RefreshPartyAsync(
        HabiticaCredentials credentials,
        CancellationToken cancellationToken)
    {
        var user = await _userSnapshotStore.GetLatestAsync(cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(user.PartyId))
        {
            await _partySnapshotStore.ClearAsync(cancellationToken);
            return new DomainRefreshState(RefreshDomain.Party, false, _timeProvider.GetUtcNow(), null);
        }

        var party = await _habiticaSyncClient.GetPartySnapshotAsync(credentials, cancellationToken);
        var cronEvents = PartyCronCalculator.CreateHistoryEvents(party);
        var cronHistory = await _partyCronHistoryStore.UpsertAsync(cronEvents, party.RetrievedAtUtc, cancellationToken);
        var cronDashboard = PartyCronCalculator.BuildDashboard(
            party,
            cronHistory,
            credentials.UserId,
            party.RetrievedAtUtc,
            TimeZoneInfo.Local);

        var enriched = party with
        {
            Members = cronDashboard.Members,
            CronDashboard = cronDashboard
        };

        if (enriched.Quest is not null)
        {
            enriched = enriched with
            {
                Quest = PartyQuestProgressCalculator.Enrich(
                    enriched,
                    enriched.Quest,
                    credentials.UserId,
                    party.RetrievedAtUtc,
                    TimeZoneInfo.Local,
                    includeStaleMembers: false)
            };
        }

        await _partySnapshotStore.SaveAsync(enriched, cancellationToken);
        return new DomainRefreshState(RefreshDomain.Party, false, party.RetrievedAtUtc, null);
    }

    private async Task<DomainRefreshState> RefreshGearCatalogAsync(
        HabiticaCredentials credentials,
        CancellationToken cancellationToken)
    {
        var catalog = await _habiticaSyncClient.GetContentCatalogAsync(credentials, cancellationToken);
        await _gearCatalogStore.SaveAsync(catalog, cancellationToken);

        await _diagnosticsLogWriter.WriteAsync(
            DiagnosticsFeatureArea.Inventory,
            "inventory-refresh-catalog",
            DiagnosticsSeverity.Success,
            DiagnosticsMode.LiveRead,
            "Refreshed gear content catalog.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["itemCount"] = catalog.Items.Count.ToString(CultureInfo.InvariantCulture)
            },
            cancellationToken);

        return new DomainRefreshState(RefreshDomain.GearCatalog, false, catalog.RetrievedAtUtc, null);
    }
}
