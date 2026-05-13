using Habitica.Api;
using Habitica.Application.Auth;
using Habitica.Application.Diagnostics;
using Habitica.Application.Sync;
using Habitica.Domain.Auth;
using Habitica.Domain.Diagnostics;
using Habitica.Domain.Party;
using Habitica.Domain.Tasks;
using Habitica.Domain.User;
using Habitica.Storage;
using Habitica.WebApp.Sync;
using Habitica.WebApp.State;
using System.Text.Json;

namespace Habitica.WebApp.Tests.State;

public sealed class AppSessionControllerTests
{
    [Fact]
    public async Task InitializeAsync_loads_cached_diagnostics_entries_into_state()
    {
        var logStore = new FakeDiagnosticsLogStore(new[]
        {
            new DiagnosticsLogEntry(
                "entry-1",
                DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
                DiagnosticsFeatureArea.Diagnostics,
                "safe-live-tests",
                DiagnosticsSeverity.Warning,
                DiagnosticsMode.LiveRead,
                "warning",
                new Dictionary<string, string>())
        });

        var controller = CreateController(logStore);

        await controller.InitializeAsync();

        Assert.Single(controller.State.DiagnosticsLogEntries!);
        Assert.Equal(1, controller.State.DiagnosticsWarningCount);
        Assert.True(controller.State.HasDiagnosticsHistory);
    }

    [Fact]
    public async Task ClearDiagnosticsLogsAsync_refreshes_state_after_clearing_store()
    {
        var logStore = new FakeDiagnosticsLogStore(new[]
        {
            new DiagnosticsLogEntry(
                "entry-1",
                DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
                DiagnosticsFeatureArea.Diagnostics,
                "safe-live-tests",
                DiagnosticsSeverity.Info,
                DiagnosticsMode.LiveRead,
                "info",
                new Dictionary<string, string>())
        });

        var controller = CreateController(logStore);
        await controller.InitializeAsync();

        await controller.ClearDiagnosticsLogsAsync();

        Assert.Empty(controller.State.DiagnosticsLogEntries!);
        Assert.Equal(0, controller.State.DiagnosticsWarningCount);
    }

    [Fact]
    public async Task ClearLocalDataAsync_clears_diagnostics_history_with_other_local_state()
    {
        var logStore = new FakeDiagnosticsLogStore(new[]
        {
            new DiagnosticsLogEntry(
                "entry-1",
                DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
                DiagnosticsFeatureArea.Auth,
                "sign-in",
                DiagnosticsSeverity.Success,
                DiagnosticsMode.LiveRead,
                "signed in",
                new Dictionary<string, string>())
        });

        var controller = CreateController(logStore);
        await controller.InitializeAsync();

        await controller.ClearLocalDataAsync();

        Assert.Empty(controller.State.DiagnosticsLogEntries!);
        Assert.False(controller.State.HasDiagnosticsHistory);
    }

    [Fact]
    public async Task SaveEquipmentPresetAsync_persists_preset_and_writes_inventory_log()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var controller = CreateController(logStore);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });

        var result = await controller.SaveEquipmentPresetAsync(EquipmentSetKind.Battle, "Casting");

        Assert.True(result.Succeeded);
        Assert.Contains(controller.State.Presets, preset => preset.Name == "Casting" && preset.Kind == EquipmentSetKind.Battle);
        Assert.Contains(logStore.Entries, entry =>
            entry.FeatureArea == DiagnosticsFeatureArea.Inventory
            && entry.Operation == "inventory-save-preset"
            && entry.Metadata["presetName"] == "Casting");
    }

    [Fact]
    public async Task RefreshAsync_merges_cloud_data_before_uploading_local_sync_bundle()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var remoteSync = new FakeRemoteUserDataSyncProvider
        {
            Snapshot = new RemoteUserDataSnapshot(
                """
                {
                  "schemaVersion": 1,
                  "exportedAtUtc": "2026-05-13T03:00:00Z",
                  "userId": "user-id",
                  "records": []
                }
                """,
                DateTimeOffset.Parse("2026-05-13T03:00:00Z"))
        };
        var controller = CreateController(logStore, remoteUserDataSyncProvider: remoteSync);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });

        await controller.RefreshAsync();

        Assert.True(remoteSync.DownloadCount >= 2);
        Assert.True(remoteSync.UploadCount >= 2);
        Assert.NotNull(remoteSync.UploadedJson);
    }

    [Fact]
    public async Task SaveEquipmentPresetAsync_uploads_encrypted_cloud_sync_bundle_after_local_change()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var remoteSync = new FakeRemoteUserDataSyncProvider();
        var controller = CreateController(logStore, remoteUserDataSyncProvider: remoteSync);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });
        var uploadCountBeforePreset = remoteSync.UploadCount;

        await controller.SaveEquipmentPresetAsync(EquipmentSetKind.Battle, "Casting");

        Assert.True(remoteSync.UploadCount > uploadCountBeforePreset);
        Assert.NotNull(remoteSync.UploadedJson);
    }

    [Fact]
    public async Task SignInAsync_merges_remote_cloud_data_into_visible_state()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var remoteSync = new FakeRemoteUserDataSyncProvider
        {
            Snapshot = new RemoteUserDataSnapshot(
                """
                {
                  "schemaVersion": 1,
                  "exportedAtUtc": "2026-05-13T03:00:00Z",
                  "userId": "user-id",
                  "records": [
                    {
                      "key": "inventory/equipmentPresets",
                      "jsonText": "[{\"id\":\"remote-preset\",\"userId\":\"user-id\",\"kind\":0,\"name\":\"Remote Casting\",\"savedAtUtc\":\"2026-05-13T02:00:00Z\",\"slots\":{\"head\":\"head_wizard_3\",\"armor\":null,\"weapon\":\"weapon_wizard_5\",\"shield\":null,\"back\":null}}]"
                    }
                  ]
                }
                """,
                DateTimeOffset.Parse("2026-05-13T03:00:00Z"))
        };
        var controller = CreateController(logStore, remoteUserDataSyncProvider: remoteSync);

        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });

        Assert.Contains(controller.State.Presets, preset => preset.Id == "remote-preset" && preset.Name == "Remote Casting");
        Assert.True(remoteSync.DownloadCount >= 1);
        Assert.True(remoteSync.UploadCount >= 1);
    }

    [Fact]
    public async Task RenameEquipmentPresetAsync_updates_local_preset_and_writes_inventory_log()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var controller = CreateController(logStore);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });
        await controller.SaveEquipmentPresetAsync(EquipmentSetKind.Battle, "Casting");
        var preset = Assert.Single(controller.State.Presets);

        var result = await controller.RenameEquipmentPresetAsync(preset.Id, "Focused Casting");

        Assert.True(result.Succeeded);
        var renamed = Assert.Single(controller.State.Presets);
        Assert.Equal(preset.Id, renamed.Id);
        Assert.Equal("Focused Casting", renamed.Name);
        Assert.Contains(logStore.Entries, entry =>
            entry.FeatureArea == DiagnosticsFeatureArea.Inventory
            && entry.Operation == "inventory-rename-preset"
            && entry.Metadata["presetId"] == preset.Id
            && entry.Metadata["presetName"] == "Focused Casting");
    }

    [Fact]
    public async Task SaveEquipmentPresetAsync_removes_battle_back_slot_from_preset()
    {
        var controller = CreateController(new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>()));
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });

        await controller.SaveEquipmentPresetAsync(EquipmentSetKind.Battle, "Casting");

        var preset = Assert.Single(controller.State.Presets);
        Assert.Null(preset.Slots.Back);
    }

    [Fact]
    public async Task CastSpellAsync_casts_sequentially_refreshes_snapshots_and_writes_log()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var syncClient = new FakeHabiticaSyncClient(CreateUserSnapshot() with { RetrievedAtUtc = DateTimeOffset.UtcNow }, CreateTaskSnapshot(), CreatePartySnapshot());
        var controller = CreateController(logStore, syncClient);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });

        var result = await controller.CastSpellAsync(new SpellCastRequest("fireball", "todo-1", 2));

        Assert.True(result.Succeeded);
        Assert.Equal(2, syncClient.CastCalls.Count);
        Assert.All(syncClient.CastCalls, call =>
        {
            Assert.Equal("fireball", call.SpellId);
            Assert.Equal("todo-1", call.TargetTaskId);
        });
        Assert.Null(controller.State.ActiveSpellCastProgress);
        Assert.Contains(logStore.Entries, entry =>
            entry.FeatureArea == DiagnosticsFeatureArea.Skills
            && entry.Operation == "spell-cast"
            && entry.Metadata["completed"] == "2");
    }

    [Fact]
    public async Task CastSpellAsync_auto_equips_recommended_gear_then_restores_original_gear()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot() with
            {
                RetrievedAtUtc = DateTimeOffset.UtcNow,
                Equipment = new EquipmentSnapshot(
                    new GearSlotsSnapshot("head_old", "armor_old", "weapon_old", "shield_old", null),
                    new GearSlotsSnapshot(null, null, null, null, null)),
                Inventory = new InventorySnapshot(0, 0, 0, 0, 0, 0, new[] { "head_new", "armor_new", "weapon_new", "shield_new" })
            },
            CreateTaskSnapshot(),
            CreatePartySnapshot());
        var controller = CreateController(logStore, syncClient);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });

        var result = await controller.CastSpellAsync(new SpellCastRequest(
            "fireball",
            "todo-1",
            1,
            AutoEquipRecommendedGear: true,
            AutoEquipGearSlots: new GearSlotsSnapshot("head_new", "armor_new", "weapon_new", "shield_new", null)));

        Assert.True(result.Succeeded);
        Assert.Equal(
            new[]
            {
                (EquipmentSetKind.Battle, "head_new"),
                (EquipmentSetKind.Battle, "armor_new"),
                (EquipmentSetKind.Battle, "weapon_new"),
                (EquipmentSetKind.Battle, "shield_new"),
                (EquipmentSetKind.Battle, "head_old"),
                (EquipmentSetKind.Battle, "armor_old"),
                (EquipmentSetKind.Battle, "weapon_old"),
                (EquipmentSetKind.Battle, "shield_old")
            },
            syncClient.EquipCalls);
        Assert.Single(syncClient.CastCalls);
        Assert.Null(controller.State.ActiveEquipmentProgress);
    }

    [Fact]
    public async Task AllocateStatsAsync_sends_bulk_allocation_refreshes_snapshot_and_writes_log()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var syncClient = new FakeHabiticaSyncClient(CreateUserSnapshot() with { RetrievedAtUtc = DateTimeOffset.UtcNow, UnallocatedStatPoints = 3 }, CreateTaskSnapshot(), CreatePartySnapshot());
        var controller = CreateController(logStore, syncClient);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });

        var result = await controller.AllocateStatsAsync(new StatAllocation(0, 2, 0, 1));

        Assert.True(result.Succeeded);
        Assert.Equal(new StatAllocation(0, 2, 0, 1), Assert.Single(syncClient.StatAllocationCalls));
        Assert.Contains(logStore.Entries, entry =>
            entry.FeatureArea == DiagnosticsFeatureArea.Skills
            && entry.Operation == "stats-allocate"
            && entry.Metadata["int"] == "2"
            && entry.Metadata["per"] == "1");
    }

    private static AppSessionController CreateController(
        FakeDiagnosticsLogStore logStore,
        IRemoteUserDataSyncProvider? remoteUserDataSyncProvider = null)
    {
        var syncClient = new FakeHabiticaSyncClient(CreateUserSnapshot(), CreateTaskSnapshot(), CreatePartySnapshot());
        return CreateController(logStore, syncClient, remoteUserDataSyncProvider);
    }

    private static AppSessionController CreateController(
        FakeDiagnosticsLogStore logStore,
        FakeHabiticaSyncClient syncClient,
        IRemoteUserDataSyncProvider? remoteUserDataSyncProvider = null)
    {
        var credentialStore = new FakeCredentialStore();
        var taskSnapshotStore = new FakeTaskSnapshotStore();
        var userSnapshotStore = new FakeUserSnapshotStore();
        var partySnapshotStore = new FakePartySnapshotStore();
        var partyCronHistoryStore = new FakePartyCronHistoryStore();
        var gearCatalogStore = new FakeGearCatalogStore();
        var logWriter = new DiagnosticsLogWriter(logStore, TimeProvider.System);
        var keyValueStorage = new FakeKeyValueStorage();
        var equipmentPresetStore = new EquipmentPresetStore(keyValueStorage);

        return new AppSessionController(
            loginWorkflow: new LoginWorkflow(syncClient, credentialStore, taskSnapshotStore, userSnapshotStore, partySnapshotStore, partyCronHistoryStore, logWriter),
            habiticaSyncClient: syncClient,
            liveTestWorkflow: new LiveTestWorkflow(syncClient, userSnapshotStore, taskSnapshotStore, partySnapshotStore, logWriter, TimeProvider.System),
            diagnosticsPresetWorkflow: new DiagnosticsPresetWorkflow(syncClient, logWriter),
            credentialStore: credentialStore,
            equipmentPresetStore: equipmentPresetStore,
            gearCatalogStore: gearCatalogStore,
            partyCronHistoryStore: partyCronHistoryStore,
            partySnapshotStore: partySnapshotStore,
            localUserDataPortabilityService: new LocalUserDataPortabilityService(keyValueStorage, TimeProvider.System),
            remoteUserDataSyncProvider: remoteUserDataSyncProvider ?? new FakeRemoteUserDataSyncProvider(),
            taskSnapshotStore: taskSnapshotStore,
            userSnapshotStore: userSnapshotStore,
            diagnosticsLogStore: logStore,
            diagnosticsLogWriter: logWriter,
            snapshotFreshnessPolicy: new SnapshotFreshnessPolicy(),
            timeProvider: TimeProvider.System);
    }

    private sealed class FakeKeyValueStorage : IKeyValueStorage
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public Task<TValue?> GetAsync<TValue>(string key, CancellationToken cancellationToken)
        {
            return Task.FromResult(_values.TryGetValue(key, out var json)
                ? JsonSerializer.Deserialize<TValue>(json, JsonOptions)
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

    private sealed class FakeRemoteUserDataSyncProvider : IRemoteUserDataSyncProvider
    {
        public int DownloadCount { get; private set; }

        public int UploadCount { get; private set; }

        public RemoteUserDataSnapshot? Snapshot { get; set; }

        public string? UploadedJson { get; private set; }

        public Task<RemoteUserDataSnapshot?> DownloadAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            DownloadCount++;
            return Task.FromResult(Snapshot);
        }

        public Task UploadAsync(HabiticaCredentials credentials, string plainTextJson, CancellationToken cancellationToken)
        {
            UploadCount++;
            UploadedJson = plainTextJson;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDiagnosticsLogStore : IDiagnosticsLogStore
    {
        private readonly List<DiagnosticsLogEntry> _entries;

        public FakeDiagnosticsLogStore(IEnumerable<DiagnosticsLogEntry> entries)
        {
            _entries = entries.ToList();
        }

        public IReadOnlyList<DiagnosticsLogEntry> Entries => _entries;

        public Task<IReadOnlyList<DiagnosticsLogEntry>> GetRecentAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<DiagnosticsLogEntry>>(_entries);
        }

        public Task AppendAsync(DiagnosticsLogEntry entry, CancellationToken cancellationToken)
        {
            _entries.Insert(0, entry);
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            _entries.Clear();
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCredentialStore : ICredentialStore
    {
        public Task ClearPersistentCredentialsAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<HabiticaCredentials?> GetPersistentCredentialsAsync(CancellationToken cancellationToken)
            => Task.FromResult<HabiticaCredentials?>(null);

        public Task SavePersistentCredentialsAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class FakeTaskSnapshotStore : ITaskSnapshotStore
    {
        public TaskCollectionSnapshot? Snapshot { get; set; }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            Snapshot = null;
            return Task.CompletedTask;
        }

        public Task<TaskCollectionSnapshot?> GetLatestAsync(CancellationToken cancellationToken)
            => Task.FromResult(Snapshot);

        public Task SaveAsync(TaskCollectionSnapshot snapshot, CancellationToken cancellationToken)
        {
            Snapshot = snapshot;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUserSnapshotStore : IUserSnapshotStore
    {
        public UserSnapshot? Snapshot { get; set; }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            Snapshot = null;
            return Task.CompletedTask;
        }

        public Task<UserSnapshot?> GetLatestAsync(CancellationToken cancellationToken)
            => Task.FromResult(Snapshot);

        public Task SaveAsync(UserSnapshot snapshot, CancellationToken cancellationToken)
        {
            Snapshot = snapshot;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePartySnapshotStore : IPartySnapshotStore
    {
        public PartySnapshot? Snapshot { get; set; }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            Snapshot = null;
            return Task.CompletedTask;
        }

        public Task<PartySnapshot?> GetLatestAsync(CancellationToken cancellationToken)
            => Task.FromResult(Snapshot);

        public Task SaveAsync(PartySnapshot snapshot, CancellationToken cancellationToken)
        {
            Snapshot = snapshot;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePartyCronHistoryStore : IPartyCronHistoryStore
    {
        public bool Cleared { get; private set; }

        public PartyCronHistorySnapshot Snapshot { get; private set; } = new(Array.Empty<PartyCronHistoryEvent>());

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            Cleared = true;
            Snapshot = new PartyCronHistorySnapshot(Array.Empty<PartyCronHistoryEvent>());
            return Task.CompletedTask;
        }

        public Task<PartyCronHistorySnapshot> GetAsync(CancellationToken cancellationToken)
            => Task.FromResult(Snapshot);

        public Task<PartyCronHistorySnapshot> UpsertAsync(
            IEnumerable<PartyCronHistoryEvent> events,
            DateTimeOffset pruneReferenceUtc,
            CancellationToken cancellationToken)
        {
            Snapshot = new PartyCronHistorySnapshot(Snapshot.Events.Concat(events).ToArray());
            return Task.FromResult(Snapshot);
        }
    }

    private sealed class FakeGearCatalogStore : IGearCatalogStore
    {
        public GearCatalogSnapshot? Snapshot { get; private set; }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            Snapshot = null;
            return Task.CompletedTask;
        }

        public Task<GearCatalogSnapshot?> GetLatestAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Snapshot);
        }

        public Task SaveAsync(GearCatalogSnapshot catalog, CancellationToken cancellationToken)
        {
            Snapshot = catalog;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHabiticaSyncClient : IHabiticaSyncClient
    {
        private readonly UserSnapshot _userSnapshot;
        private readonly TaskCollectionSnapshot _taskSnapshot;
        private readonly PartySnapshot _partySnapshot;

        public FakeHabiticaSyncClient(UserSnapshot userSnapshot, TaskCollectionSnapshot taskSnapshot, PartySnapshot partySnapshot)
        {
            _userSnapshot = userSnapshot;
            _taskSnapshot = taskSnapshot;
            _partySnapshot = partySnapshot;
        }

        public List<(string SpellId, string? TargetTaskId)> CastCalls { get; } = new();

        public List<(EquipmentSetKind Kind, string Key)> EquipCalls { get; } = new();

        public UserSnapshot UserSnapshot => _userSnapshot;

        public List<StatAllocation> StatAllocationCalls { get; } = new();

        public Task EquipGearAsync(HabiticaCredentials credentials, string key, CancellationToken cancellationToken)
        {
            EquipCalls.Add((EquipmentSetKind.Battle, key));
            return Task.CompletedTask;
        }

        public Task EquipGearAsync(HabiticaCredentials credentials, EquipmentSetKind kind, string key, CancellationToken cancellationToken)
        {
            EquipCalls.Add((kind, key));
            return Task.CompletedTask;
        }

        public Task CastSpellAsync(HabiticaCredentials credentials, string spellId, string? targetId, CancellationToken cancellationToken)
        {
            CastCalls.Add((spellId, targetId));
            return Task.CompletedTask;
        }

        public Task AllocateStatsAsync(HabiticaCredentials credentials, StatAllocation allocation, CancellationToken cancellationToken)
        {
            StatAllocationCalls.Add(allocation);
            return Task.CompletedTask;
        }

        public Task<GearCatalogSnapshot> GetContentCatalogAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
            => Task.FromResult(new GearCatalogSnapshot(DateTimeOffset.UtcNow, new Dictionary<string, GearCatalogItem>()));

        public Task<PartySnapshot> GetPartySnapshotAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
            => Task.FromResult(_partySnapshot);

        public Task<TaskCollectionSnapshot> GetTasksAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
            => Task.FromResult(_taskSnapshot);

        public Task<UserSummary> GetUserAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
            => Task.FromResult(new UserSummary(_userSnapshot.DisplayName, _userSnapshot.ClassName, _userSnapshot.Level));

        public Task<UserSnapshot> GetUserSnapshotAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
            => Task.FromResult(_userSnapshot);
    }

    private static UserSnapshot CreateUserSnapshot()
    {
        return new UserSnapshot(
            DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
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
            new InventorySnapshot(1, 5, 1, 1, 1, 1, new[] { "weapon_wizard_5", "weapon_warrior_6" }));
    }

    private static TaskCollectionSnapshot CreateTaskSnapshot()
    {
        return new TaskCollectionSnapshot(
            DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
            new[]
            {
                new TaskSnapshot("todo-1", "Buy milk", TaskType.Todo, false, 1.5m, null, null)
            });
    }

    private static PartySnapshot CreatePartySnapshot()
    {
        return new PartySnapshot(
            DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
            "party-123",
            "Night Owls",
            "Quest-focused party",
            4,
            new PartyQuestSnapshot("dragon", true, 12.5m, 3m, 2));
    }
}
