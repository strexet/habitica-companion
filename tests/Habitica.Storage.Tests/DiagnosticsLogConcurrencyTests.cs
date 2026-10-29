using Habitica.Domain.Diagnostics;

namespace Habitica.Storage.Tests;

public sealed class DiagnosticsLogConcurrencyTests
{
    [Fact]
    public async Task Overlapping_appends_preserve_committed_entries_and_retention()
    {
        var storage = new GatedStorage("get");
        var store = new DiagnosticsLogStore(storage, maxEntries: 2);
        var first = store.AppendAsync(Entry("first"), CancellationToken.None);
        var second = store.AppendAsync(Entry("second"), CancellationToken.None);
        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);
        Assert.Equal(new[] { "get" }, storage.Calls);
        storage.Release.TrySetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { "second", "first" }, storage.Entries.Select(e => e.Id));
        await store.AppendAsync(Entry("third"), CancellationToken.None);
        Assert.Equal(new[] { "third", "second" }, storage.Entries.Select(e => e.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Append_and_clear_respect_the_inflight_mutation(bool clearFirst)
    {
        var storage = new GatedStorage(clearFirst ? "remove" : "set");
        var store = new DiagnosticsLogStore(storage);
        var first = clearFirst ? store.ClearAsync(CancellationToken.None) : store.AppendAsync(Entry("before"), CancellationToken.None);
        var callsBefore = storage.Calls.ToArray();
        var second = clearFirst ? store.AppendAsync(Entry("after"), CancellationToken.None) : store.ClearAsync(CancellationToken.None);
        Assert.False(second.IsCompleted);
        Assert.Equal(callsBefore, storage.Calls);
        storage.Release.TrySetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(clearFirst ? new[] { "after" } : Array.Empty<string>(), storage.Entries.Select(e => e.Id));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Cancellation_before_acquisition_never_enters_storage_or_releases_another_owner(bool clear, bool alreadyCancelled)
    {
        var storage = new GatedStorage("get");
        var store = new DiagnosticsLogStore(storage);
        var owner = store.AppendAsync(Entry("owner"), CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        if (alreadyCancelled) cancellation.Cancel();
        var waiting = clear ? store.ClearAsync(cancellation.Token) : store.AppendAsync(Entry("cancelled"), cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(5)));
        var follower = store.AppendAsync(Entry("follower"), CancellationToken.None);
        Assert.False(follower.IsCompleted);
        Assert.Equal(new[] { "get" }, storage.Calls);
        storage.Release.TrySetResult();
        await Task.WhenAll(owner, follower).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { "follower", "owner" }, storage.Entries.Select(e => e.Id));
    }

    [Theory]
    [InlineData("get", false)]
    [InlineData("set", false)]
    [InlineData("remove", false)]
    [InlineData("get", true)]
    [InlineData("set", true)]
    [InlineData("remove", true)]
    public async Task Storage_cancellation_or_failure_releases_the_gate(string operation, bool fail)
    {
        var storage = new GatedStorage(operation) { Fail = fail };
        var store = new DiagnosticsLogStore(storage);
        using var cancellation = new CancellationTokenSource();
        var interrupted = operation == "remove" ? store.ClearAsync(cancellation.Token) : store.AppendAsync(Entry("interrupted"), cancellation.Token);
        var follower = store.AppendAsync(Entry("survivor"), CancellationToken.None);
        Assert.False(follower.IsCompleted);
        if (fail)
        {
            storage.Release.TrySetResult();
            await Assert.ThrowsAsync<InvalidOperationException>(() => interrupted.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        else
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => interrupted.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        await follower.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { "survivor" }, storage.Entries.Select(e => e.Id));
    }

    [Fact]
    public async Task Default_capacity_remains_250_newest_first()
    {
        var storage = new GatedStorage("unused");
        var store = new DiagnosticsLogStore(storage);
        for (var index = 0; index < 251; index++) await store.AppendAsync(Entry(index.ToString()), CancellationToken.None);
        Assert.Equal(Enumerable.Range(1, 250).Reverse().Select(i => i.ToString()), storage.Entries.Select(e => e.Id));
    }

    private static DiagnosticsLogEntry Entry(string id) => new(id, DateTimeOffset.UnixEpoch,
        DiagnosticsFeatureArea.Diagnostics, "test", DiagnosticsSeverity.Info, DiagnosticsMode.Local,
        "synthetic", new Dictionary<string, string>());

    private sealed class GatedStorage(string blockedOperation) : IKeyValueStorage
    {
        private bool _blocked;
        public bool Fail { get; init; }
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<string> Calls { get; } = [];
        public DiagnosticsLogEntry[] Entries { get; private set; } = [];

        private async Task EnterAsync(string operation, CancellationToken token)
        {
            Calls.Add(operation);
            token.ThrowIfCancellationRequested();
            if (operation != blockedOperation || _blocked) return;
            _blocked = true;
            await Release.Task.WaitAsync(token);
            if (Fail) throw new InvalidOperationException("Synthetic storage failure");
        }

        public async Task<TValue?> GetAsync<TValue>(string key, CancellationToken cancellationToken)
        {
            await EnterAsync("get", cancellationToken);
            return (TValue)(object)Entries;
        }
        public async Task SetAsync<TValue>(string key, TValue value, CancellationToken cancellationToken)
        {
            await EnterAsync("set", cancellationToken);
            Entries = (DiagnosticsLogEntry[])(object)value!;
        }
        public async Task RemoveAsync(string key, CancellationToken cancellationToken)
        {
            await EnterAsync("remove", cancellationToken);
            Entries = [];
        }
        public Task<string?> GetRawJsonAsync(string key, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SetRawJsonAsync(string key, string jsonText, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
