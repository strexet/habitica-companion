using Habitica.Domain.Auth;
using Habitica.Domain.Party;
using Habitica.Domain.Tasks;
using Habitica.Domain.User;

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

    [Fact]
    public async Task UserSnapshotStore_round_trips_latest_snapshot()
    {
        var adapter = new InMemoryKeyValueStorage();
        var store = new UserSnapshotStore(adapter);
        var snapshot = new UserSnapshot(
            DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
            "Mage Tester",
            "wizard",
            15,
            42.5m,
            50m,
            33.5m,
            40m,
            125.1m,
            74.9m,
            88.25m,
            "party-123",
            "Wolf-Base",
            "Wolf-Base",
            new EquipmentSnapshot(
                new GearSlotsSnapshot("head_wizard_3", "armor_wizard_4", "weapon_wizard_5", "shield_wizard_2", "back_wizard_1"),
                new GearSlotsSnapshot("head_special_2", "armor_special_2", "weapon_special_2", "shield_special_2", "back_special_2")),
            new InventorySnapshot(
                1,
                1,
                1,
                1,
                1,
                1,
                new[] { "armor_wizard_4", "head_wizard_3" }));

        await store.SaveAsync(snapshot, CancellationToken.None);
        var loadedSnapshot = await store.GetLatestAsync(CancellationToken.None);

        Assert.NotNull(loadedSnapshot);
        Assert.Equal(snapshot.DisplayName, loadedSnapshot!.DisplayName);
        Assert.Equal(snapshot.Health, loadedSnapshot.Health);
        Assert.Equal(snapshot.Equipment.Battle.Head, loadedSnapshot.Equipment.Battle.Head);
        Assert.Equal(snapshot.Inventory.OwnedGearKeys, loadedSnapshot.Inventory.OwnedGearKeys);
    }

    [Fact]
    public async Task UserSnapshotStore_clears_the_latest_snapshot()
    {
        var adapter = new InMemoryKeyValueStorage();
        var store = new UserSnapshotStore(adapter);
        var snapshot = new UserSnapshot(
            DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
            "Mage Tester",
            "wizard",
            15,
            42.5m,
            50m,
            33.5m,
            40m,
            125.1m,
            74.9m,
            88.25m,
            null,
            null,
            null,
            new EquipmentSnapshot(
                new GearSlotsSnapshot(null, null, null, null, null),
                new GearSlotsSnapshot(null, null, null, null, null)),
            new InventorySnapshot(0, 0, 0, 0, 0, 0, Array.Empty<string>()));

        await store.SaveAsync(snapshot, CancellationToken.None);
        await store.ClearAsync(CancellationToken.None);
        var loadedSnapshot = await store.GetLatestAsync(CancellationToken.None);

        Assert.Null(loadedSnapshot);
    }

    [Fact]
    public async Task PartySnapshotStore_round_trips_latest_snapshot()
    {
        var adapter = new InMemoryKeyValueStorage();
        var store = new PartySnapshotStore(adapter);
        var snapshot = new PartySnapshot(
            DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
            "party-123",
            "Night Owls",
            "Quest-focused party",
            4,
            new PartyQuestSnapshot("dragon", true, 12.5m, 3m, 2));

        await store.SaveAsync(snapshot, CancellationToken.None);
        var loadedSnapshot = await store.GetLatestAsync(CancellationToken.None);

        Assert.NotNull(loadedSnapshot);
        Assert.Equal(snapshot, loadedSnapshot);
    }

    [Fact]
    public async Task PartySnapshotStore_clears_the_latest_snapshot()
    {
        var adapter = new InMemoryKeyValueStorage();
        var store = new PartySnapshotStore(adapter);
        var snapshot = new PartySnapshot(
            DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
            "party-123",
            "Night Owls",
            null,
            4,
            null);

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
