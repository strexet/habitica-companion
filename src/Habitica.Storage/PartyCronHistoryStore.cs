using Habitica.Domain.Party;

namespace Habitica.Storage;

public sealed class PartyCronHistoryStore : IPartyCronHistoryStore
{
    private readonly IKeyValueStorage _keyValueStorage;

    public PartyCronHistoryStore(IKeyValueStorage keyValueStorage)
    {
        _keyValueStorage = keyValueStorage;
    }

    public async Task<PartyCronHistorySnapshot> GetAsync(CancellationToken cancellationToken)
    {
        return await _keyValueStorage.GetAsync<PartyCronHistorySnapshot>(StorageKeys.PartyCronHistory, cancellationToken)
            ?? new PartyCronHistorySnapshot(Array.Empty<PartyCronHistoryEvent>());
    }

    public async Task<PartyCronHistorySnapshot> UpsertAsync(
        IEnumerable<PartyCronHistoryEvent> events,
        DateTimeOffset pruneReferenceUtc,
        CancellationToken cancellationToken)
    {
        var current = await GetAsync(cancellationToken);
        var cutoffUtc = pruneReferenceUtc.ToUniversalTime().AddDays(-PartyCronCalculator.StoredHistoryDays);
        var merged = current.Events
            .Where(eventEntry => eventEntry.LastCronUtc >= cutoffUtc)
            .ToDictionary(BuildKey, StringComparer.Ordinal);

        foreach (var eventEntry in events.Where(static eventEntry => eventEntry.LastCronUtc != default))
        {
            var normalized = eventEntry with
            {
                LastCronUtc = eventEntry.LastCronUtc.ToUniversalTime(),
                ObservedAtUtc = eventEntry.ObservedAtUtc.ToUniversalTime()
            };

            merged[BuildKey(normalized)] = normalized;
        }

        var snapshot = new PartyCronHistorySnapshot(
            merged.Values
                .Where(eventEntry => eventEntry.LastCronUtc >= cutoffUtc)
                .OrderBy(static eventEntry => eventEntry.LastCronUtc)
                .ThenBy(static eventEntry => eventEntry.MemberId, StringComparer.Ordinal)
                .ToArray());
        await _keyValueStorage.SetAsync(StorageKeys.PartyCronHistory, snapshot, cancellationToken);

        return snapshot;
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        return _keyValueStorage.RemoveAsync(StorageKeys.PartyCronHistory, cancellationToken);
    }

    private static string BuildKey(PartyCronHistoryEvent eventEntry)
    {
        return string.Join(
            '|',
            eventEntry.PartyId,
            eventEntry.MemberId,
            eventEntry.LastCronUtc.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture));
    }
}
