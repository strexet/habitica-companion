using System.Text.Json;
using System.Text.Json.Nodes;
using Habitica.Application.Sync;
using Habitica.Domain.Auth;
using Habitica.Domain.Party;
using Habitica.Domain.User;
using Habitica.Storage;

namespace Habitica.Application.Tests.Sync;

public sealed class LocalUserDataPortabilityServiceTests
{
    [Fact]
    public void CloudSyncSectionMapping_includes_preference_sections()
    {
        Assert.Contains(CloudSyncSection.TaskOrderPreferences, CloudSyncSectionMapping.AllSections);
        Assert.Equal(StorageKeys.TaskOrderPreferences, CloudSyncSectionMapping.StorageKeyFor(CloudSyncSection.TaskOrderPreferences));
        Assert.Equal(CloudSyncSection.TaskOrderPreferences, CloudSyncSectionMapping.SectionForStorageKey(StorageKeys.TaskOrderPreferences));
        Assert.Equal("task-order-preferences", CloudSyncSectionMapping.KvSuffix(CloudSyncSection.TaskOrderPreferences));
        Assert.Contains(CloudSyncSection.ColorSchemes, CloudSyncSectionMapping.AllSections);
        Assert.Equal(StorageKeys.ColorSchemePreferences, CloudSyncSectionMapping.StorageKeyFor(CloudSyncSection.ColorSchemes));
        Assert.Equal(CloudSyncSection.ColorSchemes, CloudSyncSectionMapping.SectionForStorageKey(StorageKeys.ColorSchemePreferences));
        Assert.Equal("color-schemes", CloudSyncSectionMapping.KvSuffix(CloudSyncSection.ColorSchemes));
    }

    [Fact]
    public async Task ExportAsync_excludes_persistent_credentials_and_includes_portable_records()
    {
        var storage = new InMemoryKeyValueStorage();
        var service = new LocalUserDataPortabilityService(storage, TimeProvider.System);
        await storage.SetAsync(StorageKeys.PersistentCredentials, new HabiticaCredentials("user-id", "api-token"), CancellationToken.None);
        await storage.SetAsync(
            StorageKeys.EquipmentPresets,
            new[]
            {
                new EquipmentPreset("preset-1", "user-id", EquipmentSetKind.Battle, "Casting", DateTimeOffset.Parse("2026-05-13T02:00:00Z"), new GearSlotsSnapshot(null, null, "weapon_wizard_5", null, null))
            },
            CancellationToken.None);
        await storage.SetAsync(
            StorageKeys.TaskOrderPreferences,
            new TaskOrderPreferences(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Todo"] = new[] { "todo-2", "todo-1" }
            }),
            CancellationToken.None);
        await storage.SetRawJsonAsync(StorageKeys.ColorSchemePreferences, """{"selectedSchemeId":"alpha","customSchemes":[]}""", CancellationToken.None);

        var bundle = await service.ExportAsync("user-id", CancellationToken.None);

        Assert.Equal("user-id", bundle.UserId);
        Assert.Contains(bundle.Records, record => record.Key == StorageKeys.EquipmentPresets);
        Assert.Contains(bundle.Records, record => record.Key == StorageKeys.TaskOrderPreferences);
        Assert.Contains(bundle.Records, record => record.Key == StorageKeys.ColorSchemePreferences);
        Assert.DoesNotContain(bundle.Records, record => record.Key == StorageKeys.PersistentCredentials);
        Assert.DoesNotContain(service.Serialize(bundle), "api-token", StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewImportAsync_reports_conflicting_local_data()
    {
        var storage = new InMemoryKeyValueStorage();
        var service = new LocalUserDataPortabilityService(storage, TimeProvider.System);
        await storage.SetRawJsonAsync(StorageKeys.LatestPartySnapshot, """{"retrievedAtUtc":"2026-05-13T01:00:00Z"}""", CancellationToken.None);
        var bundle = new LocalUserDataBundle(
            1,
            DateTimeOffset.Parse("2026-05-13T02:00:00Z"),
            "user-id",
            new[]
            {
                new LocalUserDataRecord(StorageKeys.LatestPartySnapshot, """{"retrievedAtUtc":"2026-05-13T02:00:00Z"}""")
            });

        var preview = await service.PreviewImportAsync(bundle, CancellationToken.None);

        Assert.True(preview.HasLocalData);
        Assert.Equal(1, preview.IncomingRecordCount);
        Assert.Equal(new[] { StorageKeys.LatestPartySnapshot }, preview.ConflictingKeys);
    }

    [Fact]
    public async Task ImportAsync_merges_equipment_presets_and_party_cron_history()
    {
        var storage = new InMemoryKeyValueStorage();
        var service = new LocalUserDataPortabilityService(storage, TimeProvider.System);
        await storage.SetAsync(
            StorageKeys.EquipmentPresets,
            new[]
            {
                new EquipmentPreset("local", "user-id", EquipmentSetKind.Battle, "Local", DateTimeOffset.Parse("2026-05-13T01:00:00Z"), new GearSlotsSnapshot("head_local", null, null, null, null))
            },
            CancellationToken.None);
        await storage.SetAsync(
            StorageKeys.PartyCronHistory,
            new PartyCronHistorySnapshot(new[]
            {
                CreateCronEvent("member-1", "2026-05-12T06:00:00Z")
            }),
            CancellationToken.None);
        var incomingPresets = JsonSerializer.Serialize(
            new[]
            {
                new EquipmentPreset("remote", "user-id", EquipmentSetKind.Battle, "Remote", DateTimeOffset.Parse("2026-05-13T02:00:00Z"), new GearSlotsSnapshot("head_remote", null, null, null, null))
            },
            InMemoryKeyValueStorage.JsonOptions);
        var incomingHistory = JsonSerializer.Serialize(
            new PartyCronHistorySnapshot(new[]
            {
                CreateCronEvent("member-2", "2026-05-13T06:00:00Z")
            }),
            InMemoryKeyValueStorage.JsonOptions);
        var bundle = new LocalUserDataBundle(
            1,
            DateTimeOffset.Parse("2026-05-13T03:00:00Z"),
            "user-id",
            new[]
            {
                new LocalUserDataRecord(StorageKeys.EquipmentPresets, incomingPresets),
                new LocalUserDataRecord(StorageKeys.PartyCronHistory, incomingHistory)
            });

        await service.ImportAsync(bundle, LocalDataImportMode.Merge, CancellationToken.None);

        var mergedPresets = await storage.GetAsync<EquipmentPreset[]>(StorageKeys.EquipmentPresets, CancellationToken.None);
        var mergedHistory = await storage.GetAsync<PartyCronHistorySnapshot>(StorageKeys.PartyCronHistory, CancellationToken.None);

        Assert.NotNull(mergedPresets);
        Assert.Contains(mergedPresets!, preset => preset.Id == "local");
        Assert.Contains(mergedPresets!, preset => preset.Id == "remote");
        Assert.NotNull(mergedHistory);
        Assert.Contains(mergedHistory!.Events, entry => entry.MemberId == "member-1");
        Assert.Contains(mergedHistory.Events, entry => entry.MemberId == "member-2");
    }

    [Fact]
    public async Task ImportAsync_merges_task_order_preferences_with_imported_shared_ids_first()
    {
        var storage = new InMemoryKeyValueStorage();
        var service = new LocalUserDataPortabilityService(storage, TimeProvider.System);
        await storage.SetAsync(
            StorageKeys.TaskOrderPreferences,
            new TaskOrderPreferences(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Todo"] = new[] { "local-only", "shared-1", "shared-2" },
                ["Habit"] = new[] { "habit-local" }
            }),
            CancellationToken.None);
        var incomingOrder = JsonSerializer.Serialize(
            new TaskOrderPreferences(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Todo"] = new[] { "shared-2", "shared-1", "incoming-only" },
                ["Daily"] = new[] { "daily-incoming" }
            }),
            InMemoryKeyValueStorage.JsonOptions);
        var bundle = new LocalUserDataBundle(
            1,
            DateTimeOffset.Parse("2026-05-13T03:00:00Z"),
            "user-id",
            new[]
            {
                new LocalUserDataRecord(StorageKeys.TaskOrderPreferences, incomingOrder)
            });

        await service.ImportAsync(bundle, LocalDataImportMode.Merge, CancellationToken.None);

        var merged = await storage.GetAsync<TaskOrderPreferences>(StorageKeys.TaskOrderPreferences, CancellationToken.None);

        Assert.NotNull(merged);
        Assert.Equal(new[] { "shared-2", "shared-1", "local-only", "incoming-only" }, merged!.OrdersByType["Todo"]);
        Assert.Equal(new[] { "habit-local" }, merged.OrdersByType["Habit"]);
        Assert.Equal(new[] { "daily-incoming" }, merged.OrdersByType["Daily"]);
    }

    [Fact]
    public async Task ImportAsync_merges_color_schemes_by_timestamp_per_id_and_selection()
    {
        // Cross-device merge: custom schemes union by id with newer updatedAtUtc winning, and
        // the selectedSchemeId follows whichever side has the newer selectedAtUtc. A built-in
        // selection rides as just its id; custom schemes ship their full token bundles.
        var storage = new InMemoryKeyValueStorage();
        var service = new LocalUserDataPortabilityService(storage, TimeProvider.System);

        const string localJson = """
            {
              "selectedSchemeId":"alpha",
              "schemaVersion":2,
              "selectedAtUtc":"2026-05-13T01:00:00Z",
              "customSchemes":[
                {"id":"c1","name":"Local Old","isBuiltIn":false,"isDark":false,"updatedAtUtc":"2026-05-13T01:00:00Z","tokens":{}}
              ]
            }
            """;
        const string incomingJson = """
            {
              "selectedSchemeId":"c2",
              "schemaVersion":2,
              "selectedAtUtc":"2026-05-13T02:00:00Z",
              "customSchemes":[
                {"id":"c1","name":"Local Updated","isBuiltIn":false,"updatedAtUtc":"2026-05-13T03:00:00Z","tokens":{}},
                {"id":"c2","name":"Remote","isBuiltIn":false,"isDark":true,"updatedAtUtc":"2026-05-13T02:00:00Z","tokens":{}}
              ]
            }
            """;
        await storage.SetRawJsonAsync(StorageKeys.ColorSchemePreferences, localJson, CancellationToken.None);

        var bundle = new LocalUserDataBundle(
            1,
            DateTimeOffset.Parse("2026-05-13T04:00:00Z"),
            "user-id",
            new[] { new LocalUserDataRecord(StorageKeys.ColorSchemePreferences, incomingJson) });

        await service.ImportAsync(bundle, LocalDataImportMode.Merge, CancellationToken.None);

        var mergedJson = await storage.GetRawJsonAsync(StorageKeys.ColorSchemePreferences, CancellationToken.None);
        Assert.NotNull(mergedJson);
        var merged = JsonNode.Parse(mergedJson!)!.AsObject();

        Assert.Equal("c2", merged["selectedSchemeId"]!.GetValue<string>());
        Assert.Equal(2, merged["schemaVersion"]!.GetValue<int>());
        var customs = merged["customSchemes"]!.AsArray();
        var byId = customs.ToDictionary(node => node!["id"]!.GetValue<string>(), node => node!["name"]!.GetValue<string>());
        Assert.Equal(2, byId.Count);
        Assert.Equal("Local Updated", byId["c1"]);
        Assert.Equal("Remote", byId["c2"]);
        Assert.True(customs.Single(node => node!["id"]!.GetValue<string>() == "c2")!["isDark"]!.GetValue<bool>());
    }

    [Fact]
    public async Task ImportAsync_keeps_local_selection_when_neither_side_stamped_selected_at()
    {
        var storage = new InMemoryKeyValueStorage();
        var service = new LocalUserDataPortabilityService(storage, TimeProvider.System);

        const string localJson = """{"selectedSchemeId":"habitica","customSchemes":[]}""";
        const string incomingJson = """{"selectedSchemeId":"gryphy-dark","customSchemes":[]}""";
        await storage.SetRawJsonAsync(StorageKeys.ColorSchemePreferences, localJson, CancellationToken.None);

        var bundle = new LocalUserDataBundle(
            1,
            DateTimeOffset.Parse("2026-05-13T04:00:00Z"),
            "user-id",
            new[] { new LocalUserDataRecord(StorageKeys.ColorSchemePreferences, incomingJson) });

        await service.ImportAsync(bundle, LocalDataImportMode.Merge, CancellationToken.None);

        var mergedJson = await storage.GetRawJsonAsync(StorageKeys.ColorSchemePreferences, CancellationToken.None);
        var merged = JsonNode.Parse(mergedJson!)!.AsObject();
        Assert.Equal("habitica", merged["selectedSchemeId"]!.GetValue<string>());
    }

    [Fact]
    public async Task ClearSectionAsync_removes_task_order_preferences()
    {
        var storage = new InMemoryKeyValueStorage();
        var service = new LocalUserDataPortabilityService(storage, TimeProvider.System);
        await storage.SetAsync(
            StorageKeys.TaskOrderPreferences,
            new TaskOrderPreferences(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Todo"] = new[] { "todo-1" }
            }),
            CancellationToken.None);

        await service.ClearSectionAsync(StorageKeys.TaskOrderPreferences, CancellationToken.None);

        Assert.Null(await storage.GetRawJsonAsync(StorageKeys.TaskOrderPreferences, CancellationToken.None));
    }

    private static PartyCronHistoryEvent CreateCronEvent(string memberId, string lastCronUtc)
    {
        return new PartyCronHistoryEvent(
            "party-id",
            memberId,
            "Member",
            DateTimeOffset.Parse(lastCronUtc),
            "2026-05-13",
            DateTimeOffset.Parse(lastCronUtc).AddMinutes(5),
            PartyCronEventConfidence.High);
    }

    private sealed class InMemoryKeyValueStorage : IKeyValueStorage
    {
        public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public Task<TValue?> GetAsync<TValue>(string key, CancellationToken cancellationToken)
        {
            return Task.FromResult(_values.TryGetValue(key, out var value)
                ? JsonSerializer.Deserialize<TValue>(value, JsonOptions)
                : default);
        }

        public Task<string?> GetRawJsonAsync(string key, CancellationToken cancellationToken)
        {
            return Task.FromResult(_values.GetValueOrDefault(key));
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }

        public Task SetAsync<TValue>(string key, TValue value, CancellationToken cancellationToken)
        {
            _values[key] = JsonSerializer.Serialize(value, JsonOptions);
            return Task.CompletedTask;
        }

        public Task SetRawJsonAsync(string key, string jsonText, CancellationToken cancellationToken)
        {
            _values[key] = jsonText;
            return Task.CompletedTask;
        }
    }
}
