using Habitica.Domain.Auth;
using Habitica.Domain.Diagnostics;
using Habitica.Domain.Party;
using Habitica.Domain.Tasks;
using Habitica.Domain.User;
using System.Text.Json;

namespace Habitica.Storage.Tests;

public sealed class StorageStoreTests
{
    [Fact]
    public void StorageKeys_include_preferences_as_portable_data()
    {
        Assert.Equal("preferences/taskOrder", StorageKeys.TaskOrderPreferences);
        Assert.Contains(StorageKeys.TaskOrderPreferences, StorageKeys.PortableDataKeys);
        Assert.Equal("preferences/colorSchemes", StorageKeys.ColorSchemePreferences);
        Assert.Contains(StorageKeys.ColorSchemePreferences, StorageKeys.PortableDataKeys);
    }

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

    [Fact]
    public async Task DiagnosticsLogStore_prepends_new_entries_and_caps_history()
    {
        var adapter = new InMemoryKeyValueStorage();
        var store = new DiagnosticsLogStore(adapter, maxEntries: 2);

        await store.AppendAsync(CreateLogEntry("one", DiagnosticsSeverity.Info), CancellationToken.None);
        await store.AppendAsync(CreateLogEntry("two", DiagnosticsSeverity.Warning), CancellationToken.None);
        await store.AppendAsync(CreateLogEntry("three", DiagnosticsSeverity.Error), CancellationToken.None);

        var entries = await store.GetRecentAsync(CancellationToken.None);

        Assert.Equal(new[] { "three", "two" }, entries.Select(entry => entry.Id));
    }

    [Fact]
    public async Task DiagnosticsLogStore_clears_history()
    {
        var adapter = new InMemoryKeyValueStorage();
        var store = new DiagnosticsLogStore(adapter);

        await store.AppendAsync(CreateLogEntry("one", DiagnosticsSeverity.Info), CancellationToken.None);
        await store.ClearAsync(CancellationToken.None);

        var entries = await store.GetRecentAsync(CancellationToken.None);

        Assert.Empty(entries);
    }

    [Fact]
    public async Task GearCatalogStore_round_trips_latest_catalog()
    {
        var adapter = new InMemoryKeyValueStorage();
        var store = new GearCatalogStore(adapter);
        var catalog = new GearCatalogSnapshot(
            DateTimeOffset.Parse("2026-04-28T09:00:00Z"),
            new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
            {
                ["weapon_wizard_5"] = new(
                    "weapon_wizard_5",
                    "Wizard Wand",
                    "Weapon",
                    "wizard",
                    "A focused casting weapon.",
                    new GearStatBlock(0m, 12m, 0m, 2m))
            });

        await store.SaveAsync(catalog, CancellationToken.None);
        var loaded = await store.GetLatestAsync(CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("Wizard Wand", loaded!.Items["weapon_wizard_5"].Text);
    }

    [Fact]
    public async Task EquipmentPresetStore_keeps_presets_per_user_and_removes_selected_preset()
    {
        var adapter = new InMemoryKeyValueStorage();
        var store = new EquipmentPresetStore(adapter);
        var userOnePreset = new EquipmentPreset(
            "preset-1",
            "user-1",
            EquipmentSetKind.Battle,
            "Casting",
            DateTimeOffset.Parse("2026-04-28T09:00:00Z"),
            new GearSlotsSnapshot(null, null, "weapon_wizard_5", null, null));
        var userTwoPreset = new EquipmentPreset(
            "preset-2",
            "user-2",
            EquipmentSetKind.Battle,
            "Casting",
            DateTimeOffset.Parse("2026-04-28T09:05:00Z"),
            new GearSlotsSnapshot(null, null, "weapon_warrior_6", null, null));

        await store.SaveAsync(userOnePreset, CancellationToken.None);
        await store.SaveAsync(userTwoPreset, CancellationToken.None);
        await store.RemoveAsync("user-1", "preset-1", CancellationToken.None);

        Assert.Empty(await store.GetForUserAsync("user-1", CancellationToken.None));
        var remaining = Assert.Single(await store.GetForUserAsync("user-2", CancellationToken.None));
        Assert.Equal("preset-2", remaining.Id);
    }

    [Fact]
    public async Task EquipmentPresetStore_rejects_duplicate_names_for_same_user_and_kind()
    {
        var adapter = new InMemoryKeyValueStorage();
        var store = new EquipmentPresetStore(adapter);
        var first = new EquipmentPreset(
            "preset-1",
            "user-1",
            EquipmentSetKind.Battle,
            "Casting",
            DateTimeOffset.Parse("2026-04-28T09:00:00Z"),
            new GearSlotsSnapshot(null, null, "weapon_wizard_5", null, null));
        var duplicate = first with
        {
            Id = "preset-2",
            CreatedAtUtc = DateTimeOffset.Parse("2026-04-28T09:05:00Z")
        };

        await store.SaveAsync(first, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(duplicate, CancellationToken.None));
    }

    [Fact]
    public async Task EquipmentPresetStore_renames_selected_preset_and_preserves_id()
    {
        var adapter = new InMemoryKeyValueStorage();
        var store = new EquipmentPresetStore(adapter);
        var preset = new EquipmentPreset(
            "preset-1",
            "user-1",
            EquipmentSetKind.Battle,
            "Casting",
            DateTimeOffset.Parse("2026-04-28T09:00:00Z"),
            new GearSlotsSnapshot(null, null, "weapon_wizard_5", null, null));

        await store.SaveAsync(preset, CancellationToken.None);
        await store.SaveAsync(preset with { Name = "Focused Casting" }, CancellationToken.None);

        var renamed = Assert.Single(await store.GetForUserAsync("user-1", CancellationToken.None));
        Assert.Equal("preset-1", renamed.Id);
        Assert.Equal("Focused Casting", renamed.Name);
    }

    private static DiagnosticsLogEntry CreateLogEntry(string id, DiagnosticsSeverity severity)
    {
        return new DiagnosticsLogEntry(
            Id: id,
            OccurredAtUtc: DateTimeOffset.Parse("2026-04-27T10:00:00Z"),
            FeatureArea: DiagnosticsFeatureArea.Diagnostics,
            Operation: "test",
            Severity: severity,
            Mode: DiagnosticsMode.Local,
            Message: $"entry-{id}",
            Metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestCount"] = "1"
            });
    }

    private sealed class InMemoryKeyValueStorage : IKeyValueStorage
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
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

        public Task<string?> GetRawJsonAsync(string key, CancellationToken cancellationToken)
        {
            return Task.FromResult(_values.TryGetValue(key, out var value) && value is not null
                ? JsonSerializer.Serialize(value, JsonOptions)
                : null);
        }

        public Task SetAsync<TValue>(string key, TValue value, CancellationToken cancellationToken)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task SetRawJsonAsync(string key, string jsonText, CancellationToken cancellationToken)
        {
            _values[key] = JsonSerializer.Deserialize<object>(jsonText, JsonOptions);
            return Task.CompletedTask;
        }
    }
}
