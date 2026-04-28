using Habitica.Domain.Party;

namespace Habitica.Storage.Tests;

public sealed class PartyCronHistoryStoreTests
{
    [Fact]
    public async Task UpsertAsync_deduplicates_same_member_last_cron_across_multiple_refreshes()
    {
        var adapter = new InMemoryKeyValueStorage();
        var store = new PartyCronHistoryStore(adapter);
        var first = CreateEvent("member-1", "2026-04-27T08:15:00Z", "2026-04-27T08:20:00Z");
        var second = CreateEvent("member-1", "2026-04-27T08:15:00Z", "2026-04-27T09:20:00Z");

        await store.UpsertAsync(new[] { first }, DateTimeOffset.Parse("2026-04-27T08:20:00Z"), CancellationToken.None);
        var snapshot = await store.UpsertAsync(new[] { second }, DateTimeOffset.Parse("2026-04-27T09:20:00Z"), CancellationToken.None);

        Assert.Single(snapshot.Events);
        Assert.Equal(DateTimeOffset.Parse("2026-04-27T09:20:00Z"), snapshot.Events.Single().ObservedAtUtc);
    }

    [Fact]
    public async Task UpsertAsync_keeps_newly_observed_same_day_cron_event_and_prunes_after_ninety_days()
    {
        var adapter = new InMemoryKeyValueStorage();
        var store = new PartyCronHistoryStore(adapter);
        var oldEvent = CreateEvent("member-1", "2026-01-20T08:00:00Z", "2026-01-20T08:10:00Z");
        var morningEvent = CreateEvent("member-1", "2026-04-27T08:00:00Z", "2026-04-27T08:10:00Z");
        var laterEvent = CreateEvent("member-1", "2026-04-27T10:00:00Z", "2026-04-27T10:10:00Z");

        await store.UpsertAsync(new[] { oldEvent, morningEvent }, DateTimeOffset.Parse("2026-04-27T08:10:00Z"), CancellationToken.None);
        var snapshot = await store.UpsertAsync(new[] { laterEvent }, DateTimeOffset.Parse("2026-04-27T10:10:00Z"), CancellationToken.None);

        Assert.Equal(2, snapshot.Events.Count);
        Assert.DoesNotContain(snapshot.Events, entry => entry.LastCronUtc == oldEvent.LastCronUtc);
        Assert.Contains(snapshot.Events, entry => entry.LastCronUtc == morningEvent.LastCronUtc);
        Assert.Contains(snapshot.Events, entry => entry.LastCronUtc == laterEvent.LastCronUtc);
    }

    private static PartyCronHistoryEvent CreateEvent(string memberId, string lastCronUtc, string observedAtUtc)
    {
        return new PartyCronHistoryEvent(
            "party-123",
            memberId,
            "Alpha",
            DateTimeOffset.Parse(lastCronUtc),
            "2026-04-27",
            DateTimeOffset.Parse(observedAtUtc),
            PartyCronEventConfidence.High);
    }

    private sealed class InMemoryKeyValueStorage : IKeyValueStorage
    {
        private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

        public Task<TValue?> GetAsync<TValue>(string key, CancellationToken cancellationToken)
        {
            return Task.FromResult(_values.TryGetValue(key, out var value) ? (TValue?)value : default);
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }

        public Task SetAsync<TValue>(string key, TValue value, CancellationToken cancellationToken)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }
    }
}
