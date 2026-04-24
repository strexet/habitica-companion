using Habitica.Domain.Auth;
using Habitica.Domain.Tasks;

namespace Habitica.Storage.Tests;

public sealed class StorageStoreTests
{
    [Fact]
    public async Task CredentialStore_round_trips_persistent_credentials()
    {
        var adapter = new InMemoryKeyValueStorage();
        var store = new CredentialStore(adapter);

        await store.SavePersistentCredentialsAsync(new HabiticaCredentials("user-id", "api-token"), CancellationToken.None);
        var loadedCredentials = await store.GetPersistentCredentialsAsync(CancellationToken.None);

        Assert.Equal(new HabiticaCredentials("user-id", "api-token"), loadedCredentials);
    }

    [Fact]
    public async Task CredentialStore_clears_persistent_credentials()
    {
        var adapter = new InMemoryKeyValueStorage();
        var store = new CredentialStore(adapter);

        await store.SavePersistentCredentialsAsync(new HabiticaCredentials("user-id", "api-token"), CancellationToken.None);
        await store.ClearPersistentCredentialsAsync(CancellationToken.None);
        var loadedCredentials = await store.GetPersistentCredentialsAsync(CancellationToken.None);

        Assert.Null(loadedCredentials);
    }

    [Fact]
    public async Task TaskSnapshotStore_round_trips_latest_snapshot()
    {
        var adapter = new InMemoryKeyValueStorage();
        var store = new TaskSnapshotStore(adapter);
        var snapshot = new TaskCollectionSnapshot(
            DateTimeOffset.Parse("2026-04-24T12:00:00Z"),
            new[]
            {
                new TaskSnapshot("todo-1", "Buy milk", TaskType.Todo, false, 1.5m, "2 liters", null)
            });

        await store.SaveAsync(snapshot, CancellationToken.None);
        var loadedSnapshot = await store.GetLatestAsync(CancellationToken.None);

        Assert.NotNull(loadedSnapshot);
        Assert.Equal(snapshot, loadedSnapshot);
    }

    [Fact]
    public async Task TaskSnapshotStore_clears_the_latest_snapshot()
    {
        var adapter = new InMemoryKeyValueStorage();
        var store = new TaskSnapshotStore(adapter);
        var snapshot = new TaskCollectionSnapshot(
            DateTimeOffset.Parse("2026-04-24T12:00:00Z"),
            new[]
            {
                new TaskSnapshot("todo-1", "Buy milk", TaskType.Todo, false, 1.5m, "2 liters", null)
            });

        await store.SaveAsync(snapshot, CancellationToken.None);
        await store.ClearAsync(CancellationToken.None);
        var loadedSnapshot = await store.GetLatestAsync(CancellationToken.None);

        Assert.Null(loadedSnapshot);
    }

    private sealed class InMemoryKeyValueStorage : IKeyValueStorage
    {
        private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

        public Task<TValue?> GetAsync<TValue>(string key, CancellationToken cancellationToken)
        {
            if (_values.TryGetValue(key, out var value))
            {
                return Task.FromResult((TValue?)value);
            }

            return Task.FromResult(default(TValue));
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
