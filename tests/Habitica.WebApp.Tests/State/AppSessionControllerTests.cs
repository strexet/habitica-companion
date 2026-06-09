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
        await WaitForConditionAsync(() => remoteSync.DownloadCount >= 2 && remoteSync.SectionUploadCount >= 2);

        Assert.True(remoteSync.DownloadCount >= 2);
        Assert.True(remoteSync.SectionUploadCount >= 2);
    }

    [Fact]
    public async Task RefreshForPageAsync_prioritizes_party_domains_for_quests_route()
    {
        var controller = CreateController(new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>()));
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });

        await controller.RefreshForPageAsync("https://localhost/quests");

        Assert.Equal(RefreshPriority.Visible, controller.State.DomainStates![RefreshDomain.Party].Priority);
        Assert.Equal(RefreshPriority.Visible, controller.State.DomainStates[RefreshDomain.UserProfile].Priority);
        Assert.Equal(RefreshPriority.Visible, controller.State.DomainStates[RefreshDomain.GearCatalog].Priority);
        Assert.Equal(RefreshPriority.Background, controller.State.DomainStates[RefreshDomain.Tasks].Priority);
    }

    [Fact]
    public void Habitica_min_request_spacing_configuration_fallback_is_350_without_appsettings_override()
    {
        var repositoryRoot = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Habitica.WebApp", "Program.cs"));
        var appsettings = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Habitica.WebApp", "wwwroot", "appsettings.json"));

        Assert.Contains("Habitica:MinRequestSpacingMilliseconds\", 350", program);
        Assert.DoesNotContain("MinRequestSpacingMilliseconds", appsettings);
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
        var uploadCountBeforePreset = remoteSync.SectionUploadCount;

        await controller.SaveEquipmentPresetAsync(EquipmentSetKind.Battle, "Casting");

        Assert.True(remoteSync.SectionUploadCount > uploadCountBeforePreset);
    }

    [Fact]
    public async Task PushCloudSyncAsync_redacts_pet_and_mount_maps_from_user_profile_section()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var remoteSync = new FakeRemoteUserDataSyncProvider();
        var userSnapshot = CreateUserSnapshot() with
        {
            Inventory = CreateUserSnapshot().Inventory with
            {
                OwnedPets = new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["Wolf-Base"] = 5,
                    ["Tiger-Base"] = -1
                },
                OwnedMounts = new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    ["Wolf-Base"] = true,
                    ["Tiger-Base"] = false
                }
            }
        };
        var syncClient = new FakeHabiticaSyncClient(userSnapshot, CreateTaskSnapshot(), CreatePartySnapshot());
        var controller = CreateController(logStore, syncClient, remoteUserDataSyncProvider: remoteSync);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });
        remoteSync.UploadedSections.Clear();

        await controller.PushCloudSyncAsync();

        var userProfileKey = CloudSyncSectionMapping.KvSuffix(CloudSyncSection.UserProfile);
        Assert.True(remoteSync.UploadedSections.TryGetValue(userProfileKey, out var uploadedJson));
        Assert.Contains("\"inventory\"", uploadedJson);
        Assert.DoesNotContain("ownedPets", uploadedJson);
        Assert.DoesNotContain("ownedMounts", uploadedJson);
    }

    [Fact]
    public async Task SyncAppDataSectionAsync_skips_upload_without_credentials()
    {
        var remoteSync = new FakeRemoteUserDataSyncProvider();
        var controller = CreateController(
            new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>()),
            remoteUserDataSyncProvider: remoteSync);

        var result = await controller.SyncAppDataSectionAsync(CloudSyncSection.TaskOrderPreferences);

        Assert.True(result.Succeeded);
        Assert.Equal(0, remoteSync.SectionUploadCount);
        Assert.Empty(controller.State.CloudSyncStatuses);
    }

    [Fact]
    public async Task SyncAppDataSectionAsync_uploads_task_order_section_only()
    {
        var storage = new FakeKeyValueStorage();
        await storage.SetAsync(
            StorageKeys.TaskOrderPreferences,
            new TaskOrderPreferences(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Todo"] = new[] { "todo-2", "todo-1" }
            }),
            CancellationToken.None);
        var remoteSync = new FakeRemoteUserDataSyncProvider();
        var controller = CreateController(
            new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>()),
            remoteUserDataSyncProvider: remoteSync,
            keyValueStorage: storage);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });
        await WaitForConditionAsync(() => !controller.State.IsCloudSyncing && remoteSync.SectionUploadCount > 0);
        remoteSync.ClearUploads();

        var result = await controller.SyncAppDataSectionAsync(CloudSyncSection.TaskOrderPreferences);

        Assert.True(result.Succeeded);
        var sectionKey = CloudSyncSectionMapping.KvSuffix(CloudSyncSection.TaskOrderPreferences);
        var metadataKey = CloudSyncSectionMapping.KvSuffix(CloudSyncSection.SyncMetadata);
        Assert.Equal(new[] { sectionKey, metadataKey }, remoteSync.UploadedSectionKeys);
        Assert.True(remoteSync.UploadedSections.TryGetValue(sectionKey, out var uploadedJson));
        Assert.Contains("todo-2", uploadedJson);
        Assert.DoesNotContain(CloudSyncSectionMapping.KvSuffix(CloudSyncSection.UserProfile), remoteSync.UploadedSectionKeys);
        Assert.Contains(controller.State.CloudSyncStatuses, status =>
            status.Section == CloudSyncSection.TaskOrderPreferences
            && status.Direction == CloudSyncDirection.Upload
            && status.Status == CloudSyncSectionStatusKind.Succeeded);
    }

    [Fact]
    public async Task SyncAppDataSectionAsync_records_failed_task_order_upload_without_throwing()
    {
        var storage = new FakeKeyValueStorage();
        await storage.SetAsync(
            StorageKeys.TaskOrderPreferences,
            new TaskOrderPreferences(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Todo"] = new[] { "todo-2", "todo-1" }
            }),
            CancellationToken.None);
        var remoteSync = new FakeRemoteUserDataSyncProvider();
        var sectionKey = CloudSyncSectionMapping.KvSuffix(CloudSyncSection.TaskOrderPreferences);
        remoteSync.FailedSectionKeys.Add(sectionKey);
        var controller = CreateController(
            new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>()),
            remoteUserDataSyncProvider: remoteSync,
            keyValueStorage: storage);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });
        await WaitForConditionAsync(() => !controller.State.IsCloudSyncing && remoteSync.SectionUploadCount > 0);
        remoteSync.ClearUploads();

        var result = await controller.SyncAppDataSectionAsync(CloudSyncSection.TaskOrderPreferences);

        Assert.False(result.Succeeded);
        Assert.Contains(controller.State.CloudSyncStatuses, status =>
            status.Section == CloudSyncSection.TaskOrderPreferences
            && status.Direction == CloudSyncDirection.Upload
            && status.Status == CloudSyncSectionStatusKind.Failed
            && status.Message == "Upload failed.");
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
        await WaitForConditionAsync(() => controller.State.Presets.Any(preset => preset.Id == "remote-preset"));

        Assert.Contains(controller.State.Presets, preset => preset.Id == "remote-preset" && preset.Name == "Remote Casting");
        Assert.True(remoteSync.DownloadCount >= 1);
        Assert.True(remoteSync.SectionUploadCount >= 1);
    }

    [Fact]
    public async Task SignInAsync_rebuilds_party_dashboard_after_merging_remote_history()
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
                      "key": "party/cronHistory",
                      "jsonText": "{\"events\":[{\"partyId\":\"party-123\",\"memberId\":\"user-id\",\"displayName\":\"Mage Tester\",\"lastCronUtc\":\"2026-04-26T09:00:00Z\",\"memberHabiticaDayKey\":\"2026-04-26\",\"observedAtUtc\":\"2026-04-26T12:00:00Z\",\"confidence\":0}]}"
                    }
                  ]
                }
                """,
                DateTimeOffset.Parse("2026-05-13T03:00:00Z"))
        };
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot(),
            CreateTaskSnapshot(),
            CreatePartySnapshotWithMember());
        var controller = CreateController(logStore, syncClient, remoteSync);

        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });
        await WaitForConditionAsync(() => controller.State.PartySnapshot?.CronDashboard?.HistoryDayCount == 2);

        Assert.Equal(2, controller.State.PartySnapshot!.CronDashboard!.HistoryDayCount);
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
    public async Task RemoveEquipmentPresetAsync_uploads_local_preset_list_without_resurrecting_remote_deleted_presets()
    {
        var remoteSync = new FakeRemoteUserDataSyncProvider();
        var controller = CreateController(
            new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>()),
            remoteUserDataSyncProvider: remoteSync);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });
        await controller.SaveEquipmentPresetAsync(EquipmentSetKind.Battle, "First");
        await controller.SaveEquipmentPresetAsync(EquipmentSetKind.Battle, "Second");
        var savedPresetsKey = CloudSyncSectionMapping.KvSuffix(CloudSyncSection.SavedPresets);
        var originalPresets = controller.State.Presets.OrderBy(preset => preset.Name).ToArray();
        var firstPreset = originalPresets[0];
        var secondPreset = originalPresets[1];
        remoteSync.SectionKeys = new[] { savedPresetsKey };
        remoteSync.SectionSnapshots[savedPresetsKey] = new RemoteUserDataSnapshot(
            JsonSerializer.Serialize(originalPresets, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            DateTimeOffset.Parse("2026-05-13T03:00:00Z"));

        var result = await controller.RemoveEquipmentPresetAsync(firstPreset.Id);

        Assert.True(result.Succeeded);
        Assert.DoesNotContain(controller.State.Presets, preset => preset.Id == firstPreset.Id);
        Assert.Contains(controller.State.Presets, preset => preset.Id == secondPreset.Id);
        Assert.True(remoteSync.UploadedSections.TryGetValue(savedPresetsKey, out var uploadedJson));
        Assert.DoesNotContain(firstPreset.Id, uploadedJson);
        Assert.Contains(secondPreset.Id, uploadedJson);
    }

    [Fact]
    public async Task SaveEquipmentPresetAsync_keeps_battle_accessory_slots_in_preset()
    {
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot() with
            {
                Equipment = CreateUserSnapshot().Equipment with
                {
                    Battle = CreateUserSnapshot().Equipment.Battle with
                    {
                        HeadAccessory = "headAccessory_special_1",
                        Eyewear = "eyewear_special_1",
                        Body = "body_special_1"
                    }
                }
            },
            CreateTaskSnapshot(),
            CreatePartySnapshot());
        var controller = CreateController(new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>()), syncClient);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });

        await controller.SaveEquipmentPresetAsync(EquipmentSetKind.Battle, "Casting");

        var preset = Assert.Single(controller.State.Presets);
        Assert.Equal("back_wizard_1", preset.Slots.Back);
        Assert.Equal("headAccessory_special_1", preset.Slots.HeadAccessory);
        Assert.Equal("eyewear_special_1", preset.Slots.Eyewear);
        Assert.Equal("body_special_1", preset.Slots.Body);
    }

    [Fact]
    public async Task EquipGearSlotsAsync_equips_accessory_slots()
    {
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot() with
            {
                RetrievedAtUtc = DateTimeOffset.UtcNow,
                Inventory = new InventorySnapshot(1, 5, 1, 1, 1, 1, new[] { "weapon_wizard_5", "weapon_warrior_6", "eyewear_special_1" })
            },
            CreateTaskSnapshot(),
            CreatePartySnapshot());
        var controller = CreateController(new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>()), syncClient);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });

        var result = await controller.EquipGearSlotsAsync(
            EquipmentSetKind.Battle,
            new GearSlotsSnapshot("head_wizard_3", "armor_wizard_4", "weapon_wizard_5", "shield_wizard_2", "back_wizard_1", Eyewear: "eyewear_special_1"),
            "inventory:accessory",
            "Equipping accessory");

        Assert.True(result.Succeeded);
        Assert.Contains((EquipmentSetKind.Battle, "eyewear_special_1"), syncClient.EquipCalls);
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
        var partySnapshotCallsBeforeCast = syncClient.GetPartySnapshotCalls;

        var result = await controller.CastSpellAsync(new SpellCastRequest("fireball", "todo-1", 2));

        Assert.True(result.Succeeded);
        Assert.Equal(2, syncClient.CastCalls.Count);
        Assert.Equal(partySnapshotCallsBeforeCast, syncClient.GetPartySnapshotCalls);
        Assert.All(syncClient.CastCalls, call =>
        {
            Assert.Equal("fireball", call.SpellId);
            Assert.Equal("todo-1", call.TargetTaskId);
        });
        Assert.Null(controller.State.ActiveSpellCastProgress);
        Assert.Contains(logStore.Entries, entry =>
            entry.FeatureArea == DiagnosticsFeatureArea.Skills
            && entry.Operation == "spell-cast"
            && entry.Metadata["completed"] == "2"
            && entry.Metadata["requestCount"] == "4");
    }

    [Fact]
    public async Task CastSpellAsync_can_cancel_during_preparation_before_requests()
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

        var castTask = controller.CastSpellAsync(new SpellCastRequest("fireball", "todo-1", 2));
        await WaitForConditionAsync(() => controller.State.ActiveSpellCastProgress?.Label == "Preparing...");

        await controller.CancelActiveSpellCastAsync();
        var result = await castTask;

        Assert.True(result.Succeeded);
        Assert.Equal("Casting cancelled before it started.", result.Message);
        Assert.Empty(syncClient.CastCalls);
        Assert.Null(controller.State.ActiveSpellCastProgress);
        Assert.False(controller.State.IsBusy);
        Assert.Contains(logStore.Entries, entry =>
            entry.FeatureArea == DiagnosticsFeatureArea.Skills
            && entry.Operation == "spell-cast"
            && entry.Severity == DiagnosticsSeverity.Info
            && entry.Metadata["completed"] == "0"
            && entry.Metadata["requested"] == "2"
            && entry.Metadata["stage"] == "preparation");
    }

    [Fact]
    public async Task CastSpellAsync_refreshes_party_snapshot_after_party_targeted_spell()
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
        var refreshedPartySnapshot = CreatePartySnapshotWithMember() with
        {
            RetrievedAtUtc = DateTimeOffset.Parse("2026-04-28T12:00:00Z")
        };
        syncClient.PartySnapshot = refreshedPartySnapshot;
        var partySnapshotCallsBeforeCast = syncClient.GetPartySnapshotCalls;

        var result = await controller.CastSpellAsync(new SpellCastRequest("mpheal", null, 1));

        Assert.True(result.Succeeded);
        Assert.Equal(partySnapshotCallsBeforeCast + 1, syncClient.GetPartySnapshotCalls);
        Assert.Equal(refreshedPartySnapshot.RetrievedAtUtc, controller.State.PartySnapshot?.RetrievedAtUtc);
        Assert.Single(controller.State.PartySnapshot?.Members ?? Array.Empty<PartyMemberSnapshot>());
        Assert.Contains(logStore.Entries, entry =>
            entry.FeatureArea == DiagnosticsFeatureArea.Skills
            && entry.Operation == "spell-cast"
            && entry.Metadata["requestCount"] == "4");
    }

    [Fact]
    public async Task CastSpellAsync_does_not_refresh_party_snapshot_after_self_targeted_spell()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot() with
            {
                ClassName = "warrior",
                RetrievedAtUtc = DateTimeOffset.UtcNow
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
        var partySnapshotCallsBeforeCast = syncClient.GetPartySnapshotCalls;

        var result = await controller.CastSpellAsync(new SpellCastRequest("defensiveStance", null, 1));

        Assert.True(result.Succeeded);
        Assert.Equal(partySnapshotCallsBeforeCast, syncClient.GetPartySnapshotCalls);
    }

    [Fact]
    public async Task CastSpellAsync_preserves_success_and_cached_party_snapshot_when_party_refresh_fails()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var originalPartySnapshot = CreatePartySnapshotWithMember();
        var syncClient = new FakeHabiticaSyncClient(CreateUserSnapshot() with { RetrievedAtUtc = DateTimeOffset.UtcNow }, CreateTaskSnapshot(), originalPartySnapshot);
        var controller = CreateController(logStore, syncClient);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });
        syncClient.GetPartySnapshotFailureMessage = "Party refresh unavailable.";
        var partySnapshotCallsBeforeCast = syncClient.GetPartySnapshotCalls;

        var result = await controller.CastSpellAsync(new SpellCastRequest("mpheal", null, 1));

        Assert.True(result.Succeeded);
        Assert.Contains("Party refresh needs retry", result.Message);
        Assert.Equal(partySnapshotCallsBeforeCast + 1, syncClient.GetPartySnapshotCalls);
        Assert.Equal(originalPartySnapshot.RetrievedAtUtc, controller.State.PartySnapshot?.RetrievedAtUtc);
        Assert.Contains(logStore.Entries, entry =>
            entry.FeatureArea == DiagnosticsFeatureArea.Sync
            && entry.Operation == "spell-cast-party-refresh"
            && entry.Severity == DiagnosticsSeverity.Warning
            && entry.Metadata["requestCount"] == "4");
        Assert.Contains(logStore.Entries, entry =>
            entry.FeatureArea == DiagnosticsFeatureArea.Skills
            && entry.Operation == "spell-cast"
            && entry.Severity == DiagnosticsSeverity.Success
            && entry.Metadata["requestCount"] == "4");
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
                    new GearSlotsSnapshot(
                        "head_old",
                        "armor_old",
                        "weapon_old",
                        "shield_old",
                        "back_old",
                        HeadAccessory: "headAccessory_old",
                        Eyewear: "eyewear_old",
                        Body: "body_old"),
                    new GearSlotsSnapshot(null, null, null, null, null)),
                Inventory = new InventorySnapshot(
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    new[]
                    {
                        "head_new",
                        "headAccessory_new",
                        "eyewear_new",
                        "armor_new",
                        "body_new",
                        "weapon_new",
                        "shield_new",
                        "back_new"
                    })
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
            AutoEquipGearSlots: new GearSlotsSnapshot(
                "head_new",
                "armor_new",
                "weapon_new",
                "shield_new",
                "back_new",
                HeadAccessory: "headAccessory_new",
                Eyewear: "eyewear_new",
                Body: "body_new")));

        Assert.True(result.Succeeded);
        Assert.Equal(
            new[]
            {
                (EquipmentSetKind.Battle, "head_new"),
                (EquipmentSetKind.Battle, "headAccessory_new"),
                (EquipmentSetKind.Battle, "eyewear_new"),
                (EquipmentSetKind.Battle, "armor_new"),
                (EquipmentSetKind.Battle, "body_new"),
                (EquipmentSetKind.Battle, "weapon_new"),
                (EquipmentSetKind.Battle, "shield_new"),
                (EquipmentSetKind.Battle, "back_new"),
                (EquipmentSetKind.Battle, "head_old"),
                (EquipmentSetKind.Battle, "headAccessory_old"),
                (EquipmentSetKind.Battle, "eyewear_old"),
                (EquipmentSetKind.Battle, "armor_old"),
                (EquipmentSetKind.Battle, "body_old"),
                (EquipmentSetKind.Battle, "weapon_old"),
                (EquipmentSetKind.Battle, "shield_old"),
                (EquipmentSetKind.Battle, "back_old")
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

    [Fact]
    public async Task AllocateStatsAsync_rejects_when_stat_allocation_is_locked()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot() with
            {
                RetrievedAtUtc = DateTimeOffset.UtcNow,
                Level = 9,
                UnallocatedStatPoints = 3
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

        var result = await controller.AllocateStatsAsync(new StatAllocation(0, 2, 0, 1));

        Assert.False(result.Succeeded);
        Assert.Equal("Stat allocation unlocks at level 10.", result.Message);
        Assert.Empty(syncClient.StatAllocationCalls);
    }

    [Fact]
    public async Task BuyHealthPotionAsync_buys_potion_refreshes_snapshot_and_writes_log()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot() with
            {
                RetrievedAtUtc = DateTimeOffset.UtcNow,
                Health = 20m,
                Gold = 25m
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

        var result = await controller.BuyHealthPotionAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(1, syncClient.BuyHealthPotionCalls);
        Assert.Contains(logStore.Entries, entry =>
            entry.FeatureArea == DiagnosticsFeatureArea.Inventory
            && entry.Operation == "health-potion-buy"
            && entry.Metadata["endpoint"] == "POST /user/buy/potion"
            && entry.Metadata["requestedPotionCount"] == "1"
            && entry.Metadata["completedPurchaseCount"] == "1"
            && entry.Metadata["healthBefore"] == "20"
            && entry.Metadata["goldBefore"] == "25");
    }

    [Fact]
    public async Task BuyGemsForGoldAsync_buys_sequentially_refreshes_snapshot_and_writes_log()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot() with
            {
                RetrievedAtUtc = DateTimeOffset.UtcNow,
                Gold = 80m,
                GemBalance = 5m,
                CanBuyGemsForGold = true,
                RemainingGemPurchases = 5
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

        var result = await controller.BuyGemsForGoldAsync(3);

        Assert.True(result.Succeeded);
        Assert.Equal(new[] { 1, 1, 1 }, syncClient.PurchaseGemsForGoldCalls);
        Assert.Contains(logStore.Entries, entry =>
            entry.FeatureArea == DiagnosticsFeatureArea.Inventory
            && entry.Operation == "gems-for-gold-buy"
            && entry.Metadata["requestedCount"] == "3"
            && entry.Metadata["completedCount"] == "3"
            && entry.Metadata["goldBefore"] == "80"
            && entry.Metadata["gemBalanceBefore"] == "5");
    }

    [Fact]
    public async Task BuyGemsForGoldAsync_allows_unknown_eligibility_when_gold_and_cap_allow()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot() with
            {
                RetrievedAtUtc = DateTimeOffset.UtcNow,
                Gold = 80m,
                GemBalance = 5m,
                CanBuyGemsForGold = null,
                RemainingGemPurchases = null
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

        var result = await controller.BuyGemsForGoldAsync(2);

        Assert.True(result.Succeeded);
        Assert.Equal(new[] { 1, 1 }, syncClient.PurchaseGemsForGoldCalls);
    }

    [Fact]
    public async Task BuyGemsForGoldAsync_blocks_explicit_ineligible_snapshot()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot() with
            {
                RetrievedAtUtc = DateTimeOffset.UtcNow,
                Gold = 80m,
                GemBalance = 5m,
                CanBuyGemsForGold = false,
                RemainingGemPurchases = 5
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

        var result = await controller.BuyGemsForGoldAsync(1);

        Assert.False(result.Succeeded);
        Assert.Equal("Subscribe in Habitica to buy gems with gold.", result.Message);
        Assert.Empty(syncClient.PurchaseGemsForGoldCalls);
    }

    [Fact]
    public async Task BuyGemsForGoldAsync_stops_on_partial_failure()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot() with
            {
                RetrievedAtUtc = DateTimeOffset.UtcNow,
                Gold = 100m,
                GemBalance = 5m,
                CanBuyGemsForGold = true,
                RemainingGemPurchases = 5
            },
            CreateTaskSnapshot(),
            CreatePartySnapshot())
        {
            PurchaseGemsForGoldFailureCall = 3
        };
        var controller = CreateController(logStore, syncClient);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });

        var result = await controller.BuyGemsForGoldAsync(4);

        Assert.False(result.Succeeded);
        Assert.Contains("Bought 2 of 4 requested gems before failure", result.Message);
        Assert.Equal(new[] { 1, 1, 1 }, syncClient.PurchaseGemsForGoldCalls);
        Assert.Contains(logStore.Entries, entry =>
            entry.FeatureArea == DiagnosticsFeatureArea.Inventory
            && entry.Operation == "gems-for-gold-buy"
            && entry.Severity == DiagnosticsSeverity.Error
            && entry.Metadata["requestedCount"] == "4"
            && entry.Metadata["completedCount"] == "2");
    }

    [Fact]
    public async Task SellInventoryItemAsync_sells_requested_count_refreshes_snapshot_and_writes_log()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot() with
            {
                RetrievedAtUtc = DateTimeOffset.UtcNow,
                Inventory = new InventorySnapshot(
                    4,
                    0,
                    0,
                    0,
                    0,
                    0,
                    Array.Empty<string>(),
                    OwnedEggs: new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        ["Wolf"] = 4
                    })
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
        var userRefreshCountBeforeSell = syncClient.GetUserSnapshotCalls;

        var result = await controller.SellInventoryItemAsync(InventorySellItemType.Egg, "Wolf", 3);

        Assert.True(result.Succeeded);
        Assert.Equal(3, syncClient.SellInventoryItemCalls.Count);
        Assert.Equal(userRefreshCountBeforeSell + 1, syncClient.GetUserSnapshotCalls);
        Assert.Contains(logStore.Entries, entry =>
            entry.FeatureArea == DiagnosticsFeatureArea.Inventory
            && entry.Operation == "inventory-bulk-sell"
            && entry.Metadata["itemType"] == "Egg"
            && entry.Metadata["itemKey"] == "Wolf"
            && entry.Metadata["completed"] == "3"
            && entry.Metadata["requestCount"] == "4");
    }

    [Fact]
    public async Task FeedPetAsync_runs_queue_sequentially_refreshes_snapshot_and_writes_log()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot() with
            {
                RetrievedAtUtc = DateTimeOffset.UtcNow,
                Inventory = new InventorySnapshot(
                    0,
                    3,
                    0,
                    0,
                    1,
                    0,
                    Array.Empty<string>(),
                    OwnedFood: new Dictionary<string, int>(StringComparer.Ordinal) { ["Meat"] = 2, ["Saddle"] = 1 },
                    OwnedPets: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf-Base"] = 0 })
            },
            CreateTaskSnapshot(),
            CreatePartySnapshot());
        var controller = CreateController(logStore, syncClient);
        await controller.SignInAsync(new SignInRequest { ApiToken = "api-token", UserId = "user-id" });
        var userRefreshCountBeforeFeed = syncClient.GetUserSnapshotCalls;
        var progressUpdates = new List<PetsMountsQueueProgress?>();
        controller.Changed += () => progressUpdates.Add(controller.State.ActivePetsMountsQueueProgress);

        var result = await controller.FeedPetAsync(
        [
            new("Wolf-Base", "Meat", 2),
            new("Wolf-Base", "Saddle", 1)
        ]);

        Assert.True(result.Succeeded);
        Assert.Equal(new[] { ("Wolf-Base", "Meat", 2), ("Wolf-Base", "Saddle", 1) }, syncClient.FeedPetCalls);
        Assert.Equal(userRefreshCountBeforeFeed + 1, syncClient.GetUserSnapshotCalls);
        Assert.Contains(logStore.Entries, entry =>
            entry.FeatureArea == DiagnosticsFeatureArea.Inventory
            && entry.Operation == "pets-feed"
            && entry.Metadata["completed"] == "2"
            && entry.Metadata["requestCount"] == "3");
        Assert.Contains(new PetsMountsQueueProgress(PetsMountsQueueOperation.Feed, 0, 2), progressUpdates);
        Assert.Contains(new PetsMountsQueueProgress(PetsMountsQueueOperation.Feed, 1, 2), progressUpdates);
        Assert.Contains(new PetsMountsQueueProgress(PetsMountsQueueOperation.Feed, 2, 2), progressUpdates);
        Assert.Null(controller.State.ActivePetsMountsQueueProgress);
    }

    [Fact]
    public async Task FeedPetAsync_stops_queue_after_first_failure()
    {
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot() with
            {
                RetrievedAtUtc = DateTimeOffset.UtcNow,
                Inventory = new InventorySnapshot(
                    0,
                    3,
                    0,
                    0,
                    1,
                    0,
                    Array.Empty<string>(),
                    OwnedFood: new Dictionary<string, int>(StringComparer.Ordinal) { ["Meat"] = 1, ["Milk"] = 1, ["Saddle"] = 1 },
                    OwnedPets: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf-Base"] = 0 })
            },
            CreateTaskSnapshot(),
            CreatePartySnapshot())
        {
            FeedPetFailureFoodKey = "Milk"
        };
        var controller = CreateController(new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>()), syncClient);
        await controller.SignInAsync(new SignInRequest { ApiToken = "api-token", UserId = "user-id" });
        var progressUpdates = new List<PetsMountsQueueProgress?>();
        controller.Changed += () => progressUpdates.Add(controller.State.ActivePetsMountsQueueProgress);

        var result = await controller.FeedPetAsync(
        [
            new("Wolf-Base", "Meat", 1),
            new("Wolf-Base", "Milk", 1),
            new("Wolf-Base", "Saddle", 1)
        ]);

        Assert.False(result.Succeeded);
        Assert.Equal("Meat", Assert.Single(syncClient.FeedPetCalls).FoodKey);
        Assert.Equal(new[] { "Meat", "Milk" }, syncClient.FeedPetAttemptedFoodKeys);
        Assert.Contains("Completed 1 of 3", result.Message);
        Assert.Contains(new PetsMountsQueueProgress(PetsMountsQueueOperation.Feed, 0, 3), progressUpdates);
        Assert.Contains(new PetsMountsQueueProgress(PetsMountsQueueOperation.Feed, 1, 3), progressUpdates);
        Assert.Null(controller.State.ActivePetsMountsQueueProgress);
    }

    [Fact]
    public async Task EquipPetAsync_and_EquipMountAsync_refresh_after_owned_companion_changes()
    {
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot() with
            {
                RetrievedAtUtc = DateTimeOffset.UtcNow,
                Inventory = new InventorySnapshot(
                    0,
                    0,
                    0,
                    0,
                    1,
                    1,
                    Array.Empty<string>(),
                    OwnedPets: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf-Base"] = 0 },
                    OwnedMounts: new Dictionary<string, bool>(StringComparer.Ordinal) { ["Wolf-Base"] = true })
            },
            CreateTaskSnapshot(),
            CreatePartySnapshot());
        var controller = CreateController(new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>()), syncClient);
        await controller.SignInAsync(new SignInRequest { ApiToken = "api-token", UserId = "user-id" });
        var refreshCalls = syncClient.GetUserSnapshotCalls;

        Assert.True((await controller.EquipPetAsync("Wolf-Base")).Succeeded);
        Assert.True((await controller.EquipMountAsync("Wolf-Base")).Succeeded);

        Assert.Equal("Wolf-Base", Assert.Single(syncClient.EquipPetCalls));
        Assert.Equal("Wolf-Base", Assert.Single(syncClient.EquipMountCalls));
        Assert.Equal(refreshCalls + 2, syncClient.GetUserSnapshotCalls);
    }

    [Fact]
    public async Task ScoreTaskAsync_scores_sequentially_refreshes_snapshots_and_writes_log()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot() with { RetrievedAtUtc = DateTimeOffset.UtcNow },
            new TaskCollectionSnapshot(
                DateTimeOffset.UtcNow,
                new[]
                {
                    new TaskSnapshot("habit-1", "Read docs", TaskType.Habit, false, 1m, null, null, 8m, SupportsPositiveScore: true, SupportsNegativeScore: true)
                }),
            CreatePartySnapshot());
        var controller = CreateController(logStore, syncClient);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });

        var result = await controller.ScoreTaskAsync(new TaskScoreRequest("habit-1", TaskScoreDirection.Up, 3));

        Assert.True(result.Succeeded);
        Assert.Equal(3, syncClient.ScoreTaskCalls.Count);
        Assert.All(syncClient.ScoreTaskCalls, call =>
        {
            Assert.Equal("habit-1", call.TaskId);
            Assert.Equal(TaskScoreDirection.Up, call.Direction);
        });
        Assert.Null(controller.State.ActiveTaskMutationProgress);
        Assert.Contains(logStore.Entries, entry =>
            entry.FeatureArea == DiagnosticsFeatureArea.Tasks
            && entry.Operation == "task-score"
            && entry.Metadata["completed"] == "3");
    }

    [Fact]
    public async Task StartNewDayAsync_runs_cron_refreshes_snapshots_and_writes_log()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot() with
            {
                RetrievedAtUtc = DateTimeOffset.UtcNow,
                CurrentHabiticaDayKey = "2026-04-27",
                NeedsCron = true
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

        var baselineUserSnapshotCalls = syncClient.GetUserSnapshotCalls;
        var baselineTasksCalls = syncClient.GetTasksCalls;
        var baselinePartyCalls = syncClient.GetPartySnapshotCalls;

        var result = await controller.StartNewDayAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(1, syncClient.RunCronCalls);
        Assert.Equal(baselineUserSnapshotCalls + 1, syncClient.GetUserSnapshotCalls);
        Assert.Equal(baselineTasksCalls + 1, syncClient.GetTasksCalls);
        Assert.Equal(baselinePartyCalls + 1, syncClient.GetPartySnapshotCalls);
        Assert.Contains(logStore.Entries, entry =>
            entry.FeatureArea == DiagnosticsFeatureArea.Sync
            && entry.Operation == "cron-start-new-day"
            && entry.Severity == DiagnosticsSeverity.Success);
    }

    [Fact]
    public async Task StartNewDayAsync_enriches_post_cron_party_snapshot_before_reloading_state()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot() with
            {
                RetrievedAtUtc = DateTimeOffset.UtcNow,
                CurrentHabiticaDayKey = "2026-04-27",
                NeedsCron = true
            },
            CreateTaskSnapshot(),
            CreatePartySnapshotWithCompletableAwaitingDamage());
        var controller = CreateController(logStore, syncClient);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });

        syncClient.PartySnapshot = CreatePartySnapshotWithCompletableAwaitingDamage();

        var result = await controller.StartNewDayAsync();

        Assert.True(result.Succeeded);
        Assert.NotNull(controller.State.PartySnapshot?.CronDashboard);
        Assert.Equal(600m, controller.State.PartySnapshot!.Quest?.PendingPartyProgress?.Value);
        Assert.True(controller.State.PartySnapshot.Quest?.CompletionEstimate?.WillCompleteAfterAwaitingCron == true);
        Assert.Equal("Marek50818", controller.State.PartySnapshot.Quest?.CompletionEstimate?.FinishingMemberDisplayName);
        Assert.NotEqual(PartyQuestEstimateConfidence.Unknown, controller.State.PartySnapshot.Quest?.CompletionEstimate?.Confidence);
    }

    [Fact]
    public async Task StartNewDayAsync_auto_equips_recommended_gear_before_cron()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot() with
            {
                RetrievedAtUtc = DateTimeOffset.UtcNow,
                CurrentHabiticaDayKey = "2026-04-27",
                NeedsCron = true,
                Equipment = new EquipmentSnapshot(
                    new GearSlotsSnapshot("head_old", null, null, null, null),
                    new GearSlotsSnapshot(null, null, null, null, null)),
                Inventory = new InventorySnapshot(0, 0, 0, 0, 0, 0, new[] { "head_new", "armor_new" })
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

        var result = await controller.StartNewDayAsync(new StartNewDayRequest(
            true,
            new GearSlotsSnapshot("head_new", "armor_new", null, null, null),
            "INT for mana"));

        Assert.True(result.Succeeded);
        Assert.Equal(
            new[]
            {
                "equip:head_new",
                "equip:armor_new",
                "cron",
                "equip:head_old",
                "equip:armor_new"
            },
            syncClient.OperationLog);
        Assert.Equal(1, syncClient.RunCronCalls);
        Assert.Contains(logStore.Entries, entry =>
            entry.FeatureArea == DiagnosticsFeatureArea.Sync
            && entry.Operation == "cron-start-new-day"
            && entry.Severity == DiagnosticsSeverity.Success
            && entry.Metadata["autoEquip"] == "True"
            && entry.Metadata["gearGoal"] == "INT for mana"
            && entry.Metadata["gearRequestCount"] == "2"
            && entry.Metadata["restoreGearRequestCount"] == "2");
    }

    [Fact]
    public async Task StartNewDayAsync_skips_cron_when_pre_cron_auto_equip_fails()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot() with
            {
                RetrievedAtUtc = DateTimeOffset.UtcNow,
                CurrentHabiticaDayKey = "2026-04-27",
                NeedsCron = true,
                Equipment = new EquipmentSnapshot(
                    new GearSlotsSnapshot("head_old", null, null, null, null),
                    new GearSlotsSnapshot(null, null, null, null, null)),
                Inventory = new InventorySnapshot(0, 0, 0, 0, 0, 0, new[] { "head_new", "armor_new" })
            },
            CreateTaskSnapshot(),
            CreatePartySnapshot())
        {
            EquipGearFailureKey = "armor_new"
        };
        var controller = CreateController(logStore, syncClient);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });

        var result = await controller.StartNewDayAsync(new StartNewDayRequest(
            true,
            new GearSlotsSnapshot("head_new", "armor_new", null, null, null),
            "INT for mana"));

        Assert.False(result.Succeeded);
        Assert.Contains("Start New Day skipped before CRON", result.Message);
        Assert.Contains("Previous battle gear was restored.", result.Message);
        Assert.Equal(0, syncClient.RunCronCalls);
        Assert.Equal(
            new[]
            {
                "equip:head_new",
                "equip:head_old"
            },
            syncClient.OperationLog);
    }

    [Fact]
    public async Task StartNewDayAsync_reports_post_cron_restore_failure()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot() with
            {
                RetrievedAtUtc = DateTimeOffset.UtcNow,
                CurrentHabiticaDayKey = "2026-04-27",
                NeedsCron = true,
                Equipment = new EquipmentSnapshot(
                    new GearSlotsSnapshot("head_old", null, null, null, null),
                    new GearSlotsSnapshot(null, null, null, null, null)),
                Inventory = new InventorySnapshot(0, 0, 0, 0, 0, 0, new[] { "head_new" })
            },
            CreateTaskSnapshot(),
            CreatePartySnapshot())
        {
            EquipGearFailureKey = "head_old"
        };
        var controller = CreateController(logStore, syncClient);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });

        var result = await controller.StartNewDayAsync(new StartNewDayRequest(
            true,
            new GearSlotsSnapshot("head_new", null, null, null, null),
            "INT for mana"));

        Assert.False(result.Succeeded);
        Assert.Contains("Start New Day completed, but restoring previous battle gear failed", result.Message);
        Assert.Equal(1, syncClient.RunCronCalls);
    }

    [Fact]
    public async Task StartNewDayAsync_restores_previous_gear_when_cron_fails()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot() with
            {
                RetrievedAtUtc = DateTimeOffset.UtcNow,
                CurrentHabiticaDayKey = "2026-04-27",
                NeedsCron = true,
                Equipment = new EquipmentSnapshot(
                    new GearSlotsSnapshot("head_old", null, null, null, null),
                    new GearSlotsSnapshot(null, null, null, null, null)),
                Inventory = new InventorySnapshot(0, 0, 0, 0, 0, 0, new[] { "head_new" })
            },
            CreateTaskSnapshot(),
            CreatePartySnapshot())
        {
            RunCronFailureMessage = "CRON failed."
        };
        var controller = CreateController(logStore, syncClient);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });

        var result = await controller.StartNewDayAsync(new StartNewDayRequest(
            true,
            new GearSlotsSnapshot("head_new", null, null, null, null),
            "INT for mana"));

        Assert.False(result.Succeeded);
        Assert.Contains("Start New Day failed while CRON was running", result.Message);
        Assert.Contains("Previous battle gear was restored.", result.Message);
        Assert.Equal(
            new[]
            {
                "equip:head_new",
                "cron",
                "equip:head_old"
            },
            syncClient.OperationLog);
    }

    [Fact]
    public async Task StartSelectedPartyQuestAsync_force_starts_selected_owned_quest_and_refreshes_party()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot(),
            CreateTaskSnapshot(),
            CreatePartySnapshot() with
            {
                Quest = new PartyQuestSnapshot("dragon", false, 0m, 0m, 2)
            });
        var remotePartySync = new FakeRemotePartyDataSyncProvider
        {
            Snapshot = new RemotePartyDataSnapshot(
                null,
                null,
                DateTimeOffset.UtcNow,
                QuestQueue: new[]
                {
                    new PartyQuestQueueEntry(
                        "queue-1",
                        "party-123",
                        "dragon",
                        "Dragon",
                        "user-id",
                        "Mage Tester",
                        PartyQuestQueueStatus.Selected,
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow,
                        1,
                        null,
                        true,
                        1,
                        Array.Empty<PartyQuestVote>())
                })
        };
        var controller = CreateController(logStore, syncClient, remotePartyDataSyncProvider: remotePartySync);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });
        await controller.RefreshForPageAsync("/party");
        await controller.RefreshPartyQuestStateAsync();
        syncClient.PartySnapshot = syncClient.PartySnapshot with
        {
            Quest = new PartyQuestSnapshot("dragon", true, 12.5m, 3m, 2)
        };

        var result = await controller.StartSelectedPartyQuestAsync("queue-1");

        Assert.True(result.Succeeded);
        Assert.Equal(1, syncClient.StartPartyQuestCalls);
        Assert.True(controller.State.PartySnapshot?.Quest?.IsActive);
        Assert.Contains(logStore.Entries, entry =>
            entry.FeatureArea == DiagnosticsFeatureArea.Party
            && entry.Operation == "party-quest-start"
            && entry.Severity == DiagnosticsSeverity.Success);
    }

    [Fact]
    public async Task StartSelectedPartyQuestAsync_allows_party_leader_to_force_start_selected_quest()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot(),
            CreateTaskSnapshot(),
            CreatePartySnapshot() with
            {
                Quest = new PartyQuestSnapshot("dragon", false, 0m, 0m, 2),
                LeaderId = "leader-id"
            });
        var remotePartySync = new FakeRemotePartyDataSyncProvider
        {
            Snapshot = new RemotePartyDataSnapshot(
                null,
                null,
                DateTimeOffset.UtcNow,
                QuestQueue: new[]
                {
                    new PartyQuestQueueEntry(
                        "queue-1",
                        "party-123",
                        "dragon",
                        "Dragon",
                        "user-id",
                        "Mage Tester",
                        PartyQuestQueueStatus.Selected,
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow,
                        1,
                        null,
                        true,
                        1,
                        Array.Empty<PartyQuestVote>())
                })
        };
        var controller = CreateController(logStore, syncClient, remotePartyDataSyncProvider: remotePartySync);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "leader-id"
        });
        await controller.RefreshForPageAsync("/party");
        await controller.RefreshPartyQuestStateAsync();
        syncClient.PartySnapshot = syncClient.PartySnapshot with
        {
            Quest = new PartyQuestSnapshot("dragon", true, 12.5m, 3m, 2),
            LeaderId = "leader-id"
        };

        var result = await controller.StartSelectedPartyQuestAsync("queue-1");

        Assert.True(result.Succeeded);
        Assert.Equal(1, syncClient.StartPartyQuestCalls);
    }

    [Fact]
    public async Task InvitePartyToQuestAsync_rejects_owned_queued_quest_until_it_is_selected_next()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot(),
            CreateTaskSnapshot(),
            CreatePartySnapshot() with
            {
                RetrievedAtUtc = DateTimeOffset.UtcNow,
                Quest = null
            });
        var remotePartySync = new FakeRemotePartyDataSyncProvider
        {
            Snapshot = new RemotePartyDataSnapshot(
                null,
                null,
                DateTimeOffset.UtcNow,
                QuestQueue: new[]
                {
                    new PartyQuestQueueEntry(
                        "queue-1",
                        "party-123",
                        "dragon",
                        "Dragon",
                        "user-id",
                        "Mage Tester",
                        PartyQuestQueueStatus.Queued,
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow,
                        1,
                        null,
                        false,
                        1,
                        Array.Empty<PartyQuestVote>())
                })
        };
        var controller = CreateController(logStore, syncClient, remotePartyDataSyncProvider: remotePartySync);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });
        await controller.RefreshForPageAsync("/party");
        await controller.RefreshPartyQuestStateAsync();

        var result = await controller.InvitePartyToQuestAsync("queue-1", 1);

        Assert.False(result.Succeeded);
        Assert.Equal("Select the quest as Next Quest before inviting the party.", result.Message);
        Assert.Empty(syncClient.InvitePartyQuestCalls);
        Assert.Empty(remotePartySync.InvitePartyCalls);
    }

    [Fact]
    public async Task InvitePartyToQuestAsync_invites_owned_selected_quest_when_party_has_no_quest()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot(),
            CreateTaskSnapshot(),
            CreatePartySnapshot() with
            {
                RetrievedAtUtc = DateTimeOffset.UtcNow,
                Quest = null
            });
        var remotePartySync = new FakeRemotePartyDataSyncProvider
        {
            Snapshot = new RemotePartyDataSnapshot(
                null,
                null,
                DateTimeOffset.UtcNow,
                QuestQueue: new[]
                {
                    new PartyQuestQueueEntry(
                        "queue-1",
                        "party-123",
                        "dragon",
                        "Dragon",
                        "user-id",
                        "Mage Tester",
                        PartyQuestQueueStatus.Selected,
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow,
                        1,
                        null,
                        true,
                        1,
                        Array.Empty<PartyQuestVote>())
                })
        };
        var controller = CreateController(logStore, syncClient, remotePartyDataSyncProvider: remotePartySync);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });
        await controller.RefreshForPageAsync("/party");
        await controller.RefreshPartyQuestStateAsync();

        var result = await controller.InvitePartyToQuestAsync("queue-1", 1);

        Assert.True(result.Succeeded);
        Assert.Equal("dragon", Assert.Single(syncClient.InvitePartyQuestCalls));
        Assert.Equal("queue-1", Assert.Single(remotePartySync.InvitePartyCalls).QueueItemId);
    }

    [Fact]
    public async Task InvitePartyToQuestAsync_rejects_when_party_already_has_quest()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot(),
            CreateTaskSnapshot(),
            CreatePartySnapshot() with
            {
                RetrievedAtUtc = DateTimeOffset.UtcNow,
                Quest = new PartyQuestSnapshot("dragon", true, 0m, 0m, 2)
            });
        var remotePartySync = new FakeRemotePartyDataSyncProvider
        {
            Snapshot = new RemotePartyDataSnapshot(
                null,
                null,
                DateTimeOffset.UtcNow,
                QuestQueue: new[]
                {
                    new PartyQuestQueueEntry(
                        "queue-1",
                        "party-123",
                        "dragon",
                        "Dragon",
                        "user-id",
                        "Mage Tester",
                        PartyQuestQueueStatus.Selected,
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow,
                        1,
                        null,
                        true,
                        1,
                        Array.Empty<PartyQuestVote>())
                })
        };
        var controller = CreateController(logStore, syncClient, remotePartyDataSyncProvider: remotePartySync);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });
        await controller.RefreshForPageAsync("/party");
        await controller.RefreshPartyQuestStateAsync();

        var result = await controller.InvitePartyToQuestAsync("queue-1", 1);

        Assert.False(result.Succeeded);
        Assert.Equal("The party already has a quest invitation or active quest.", result.Message);
        Assert.Empty(syncClient.InvitePartyQuestCalls);
        Assert.Empty(remotePartySync.InvitePartyCalls);
    }

    [Fact]
    public async Task InvitePartyToQuestAsync_rejects_when_party_data_is_stale()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot(),
            CreateTaskSnapshot(),
            CreatePartySnapshot() with
            {
                RetrievedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
                Quest = null
            });
        var remotePartySync = new FakeRemotePartyDataSyncProvider
        {
            Snapshot = new RemotePartyDataSnapshot(
                null,
                null,
                DateTimeOffset.UtcNow,
                QuestQueue: new[]
                {
                    new PartyQuestQueueEntry(
                        "queue-1",
                        "party-123",
                        "dragon",
                        "Dragon",
                        "user-id",
                        "Mage Tester",
                        PartyQuestQueueStatus.Selected,
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow,
                        1,
                        null,
                        true,
                        1,
                        Array.Empty<PartyQuestVote>())
                })
        };
        var controller = CreateController(logStore, syncClient, remotePartyDataSyncProvider: remotePartySync);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });
        await controller.RefreshForPageAsync("/party");
        await controller.RefreshPartyQuestStateAsync();

        var result = await controller.InvitePartyToQuestAsync("queue-1", 1);

        Assert.False(result.Succeeded);
        Assert.Equal("Refresh party data before inviting.", result.Message);
        Assert.Empty(syncClient.InvitePartyQuestCalls);
        Assert.Empty(remotePartySync.InvitePartyCalls);
    }

    [Fact]
    public async Task CreatePartySyncInviteProofAsync_returns_issued_browser_token()
    {
        var remotePartySync = new FakeRemotePartyDataSyncProvider();
        var controller = CreateController(
            new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>()),
            new FakeHabiticaSyncClient(CreateUserSnapshot(), CreateTaskSnapshot(), CreatePartySnapshot()),
            remotePartyDataSyncProvider: remotePartySync);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });
        await controller.RefreshForPageAsync("/party");

        var result = await controller.CreatePartySyncInviteProofAsync("Family devices");

        Assert.True(result.Succeeded);
        Assert.Equal("proof-id", result.IssuedInviteProof?.ProofId);
        Assert.Equal("proof-token", result.IssuedInviteProof?.Token);
        Assert.Equal("Family devices", result.IssuedInviteProof?.Label);
    }

    [Fact]
    public async Task Quest_invitation_response_calls_habitica_and_refreshes_party()
    {
        var pendingParty = CreatePartySnapshot() with
        {
            Quest = new PartyQuestSnapshot("dragon", false, 0m, 0m, 1),
            Members = new[]
            {
                new PartyMemberSnapshot("user-id", "Mage Tester", null, null, null, PartyCronState.Unknown, "Unknown.", null, null, ParticipationStatus: PartyQuestParticipationStatus.Pending)
            }
        };
        var acceptSyncClient = new FakeHabiticaSyncClient(CreateUserSnapshot(), CreateTaskSnapshot(), pendingParty);
        var acceptController = CreateController(
            new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>()),
            acceptSyncClient);
        await acceptController.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });
        await acceptController.RefreshForPageAsync("/party");

        var acceptResult = await acceptController.AcceptPartyQuestInvitationAsync();

        Assert.True(acceptResult.Succeeded);
        Assert.Equal(1, acceptSyncClient.AcceptPartyQuestCalls);
        Assert.True(acceptSyncClient.GetPartySnapshotCalls >= 1);

        var rejectSyncClient = new FakeHabiticaSyncClient(CreateUserSnapshot(), CreateTaskSnapshot(), pendingParty);
        var rejectController = CreateController(
            new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>()),
            rejectSyncClient);
        await rejectController.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });
        await rejectController.RefreshForPageAsync("/party");

        var rejectResult = await rejectController.RejectPartyQuestInvitationAsync();

        Assert.True(rejectResult.Succeeded);
        Assert.Equal(1, rejectSyncClient.RejectPartyQuestCalls);
        Assert.True(rejectSyncClient.GetPartySnapshotCalls >= 1);
    }

    [Fact]
    public async Task RemovePartyRecentlyCompletedQuestAsync_calls_remote_party_sync()
    {
        var remotePartySync = new FakeRemotePartyDataSyncProvider();
        var controller = CreateController(
            new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>()),
            new FakeHabiticaSyncClient(CreateUserSnapshot(), CreateTaskSnapshot(), CreatePartySnapshot()),
            remotePartyDataSyncProvider: remotePartySync);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });
        var completedAtUtc = DateTimeOffset.Parse("2026-04-27T12:00:00Z");

        var result = await controller.RemovePartyRecentlyCompletedQuestAsync("dragon", completedAtUtc);

        Assert.True(result.Succeeded);
        Assert.Equal(("dragon", completedAtUtc), Assert.Single(remotePartySync.RemoveRecentlyCompletedQuestCalls));
    }

    [Fact]
    public async Task Party_queue_control_actions_call_remote_provider()
    {
        var remotePartySync = new FakeRemotePartyDataSyncProvider();
        var controller = CreateController(
            new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>()),
            new FakeHabiticaSyncClient(CreateUserSnapshot(), CreateTaskSnapshot(), CreatePartySnapshot()),
            remotePartyDataSyncProvider: remotePartySync);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });
        await controller.RefreshForPageAsync("/party");

        var pinResult = await controller.PinPartyQuestQueueItemAsync("queue-1", 3, true);
        var selectResult = await controller.SelectPartyQuestQueueItemAsync("queue-1", 4);
        var skipResult = await controller.SkipPartyQuestQueueItemAsync("queue-1", 5);
        var expireResult = await controller.ExpirePartyQuestQueueItemAsync("queue-1", 6);
        var requeueResult = await controller.RequeuePartyQuestQueueItemAsync("queue-1", 7);

        Assert.True(pinResult.Succeeded);
        Assert.True(selectResult.Succeeded);
        Assert.True(skipResult.Succeeded);
        Assert.True(expireResult.Succeeded);
        Assert.True(requeueResult.Succeeded);
        Assert.Equal(("queue-1", 3, true), Assert.Single(remotePartySync.PinQueueItemCalls));
        Assert.Equal(("queue-1", 4), Assert.Single(remotePartySync.SelectQueueItemCalls));
        Assert.Equal(("queue-1", 5), Assert.Single(remotePartySync.SkipQueueItemCalls));
        Assert.Equal(("queue-1", 6), Assert.Single(remotePartySync.ExpireQueueItemCalls));
        Assert.Equal(("queue-1", 7), Assert.Single(remotePartySync.RequeueQueueItemCalls));
    }

    [Fact]
    public async Task PushCloudSyncAsync_auto_completes_active_queue_item_when_completion_chat_matches()
    {
        var completedAtUtc = DateTimeOffset.Parse("2026-04-27T12:05:00Z");
        var previousPartySnapshot = CreatePartySnapshot() with
        {
            RetrievedAtUtc = DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
            Quest = new PartyQuestSnapshot(
                "dragon",
                true,
                12.5m,
                3m,
                2,
                Name: "Dragon")
        };
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot(),
            CreateTaskSnapshot(),
            CreatePartySnapshot() with
            {
                RetrievedAtUtc = completedAtUtc,
                Quest = null,
                RecentChatMessages = new[]
                {
                    new PartyChatMessageSnapshot(
                        "chat-1",
                        completedAtUtc,
                        null,
                        new PartyChatMessageInfoSnapshot("boss_defeated", "dragon"))
                }
            });
        var remotePartySync = new FakeRemotePartyDataSyncProvider
        {
            Snapshot = new RemotePartyDataSnapshot(
                JsonSerializer.Serialize(previousPartySnapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                null,
                previousPartySnapshot.RetrievedAtUtc,
                QuestQueue: new[]
                {
                    new PartyQuestQueueEntry(
                        "queue-1",
                        "party-123",
                        "dragon",
                        "Dragon",
                        "user-id",
                        "Mage Tester",
                        PartyQuestQueueStatus.Active,
                        DateTimeOffset.Parse("2026-04-27T11:59:00Z"),
                        DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
                        1,
                        null,
                        true,
                        1,
                        Array.Empty<PartyQuestVote>(),
                        StartedAtUtc: DateTimeOffset.Parse("2026-04-27T12:00:00Z"))
                })
        };
        var controller = CreateController(
            new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>()),
            syncClient,
            remotePartyDataSyncProvider: remotePartySync);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });

        await controller.PushCloudSyncAsync();

        Assert.Contains(remotePartySync.ReconcileCalls, call =>
            call.QueueItemId == "queue-1"
            && call.Transition == "complete"
            && call.DetectionKey == "habitica-chat-boss:dragon:chat-1");
        Assert.Empty(remotePartySync.DetectedCompletionCalls);
    }

    [Fact]
    public async Task PushCloudSyncAsync_does_not_complete_active_queue_item_without_completion_chat()
    {
        var previousPartySnapshot = CreatePartySnapshot() with
        {
            RetrievedAtUtc = DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
            Quest = new PartyQuestSnapshot("dragon", true, 12.5m, 3m, 2)
        };
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot(),
            CreateTaskSnapshot(),
            CreatePartySnapshot() with
            {
                RetrievedAtUtc = DateTimeOffset.Parse("2026-04-27T12:05:00Z"),
                Quest = null,
                RecentChatMessages = Array.Empty<PartyChatMessageSnapshot>()
            });
        var remotePartySync = new FakeRemotePartyDataSyncProvider
        {
            Snapshot = new RemotePartyDataSnapshot(
                JsonSerializer.Serialize(previousPartySnapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                null,
                previousPartySnapshot.RetrievedAtUtc,
                QuestQueue: new[]
                {
                    new PartyQuestQueueEntry(
                        "queue-1",
                        "party-123",
                        "dragon",
                        "Dragon",
                        "user-id",
                        "Mage Tester",
                        PartyQuestQueueStatus.Active,
                        DateTimeOffset.Parse("2026-04-27T11:59:00Z"),
                        DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
                        1,
                        null,
                        true,
                        1,
                        Array.Empty<PartyQuestVote>(),
                        StartedAtUtc: DateTimeOffset.Parse("2026-04-27T12:00:00Z"))
                })
        };
        var controller = CreateController(
            new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>()),
            syncClient,
            remotePartyDataSyncProvider: remotePartySync);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });

        await controller.PushCloudSyncAsync();

        Assert.DoesNotContain(remotePartySync.ReconcileCalls, call => call.Transition == "complete");
        Assert.Empty(remotePartySync.DetectedCompletionCalls);
    }

    [Fact]
    public async Task PushCloudSyncAsync_records_unqueued_collection_completion_from_chat_signal()
    {
        var completedAtUtc = DateTimeOffset.Parse("2026-04-27T12:06:00Z");
        var previousPartySnapshot = CreatePartySnapshot() with
        {
            RetrievedAtUtc = DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
            Quest = new PartyQuestSnapshot(
                "evilsanta",
                true,
                0m,
                0m,
                3,
                QuestType: PartyQuestType.Collection,
                Name: "Trapper Santa",
                RewardSummary: new[] { "100 XP" })
        };
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot(),
            CreateTaskSnapshot(),
            CreatePartySnapshot() with
            {
                RetrievedAtUtc = completedAtUtc,
                Quest = null,
                RecentChatMessages = new[]
                {
                    new PartyChatMessageSnapshot(
                        "chat-collection",
                        completedAtUtc,
                        null,
                        new PartyChatMessageInfoSnapshot("all_items_found", null))
                }
            });
        var remotePartySync = new FakeRemotePartyDataSyncProvider
        {
            Snapshot = new RemotePartyDataSnapshot(
                JsonSerializer.Serialize(previousPartySnapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                null,
                previousPartySnapshot.RetrievedAtUtc)
        };
        var controller = CreateController(
            new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>()),
            syncClient,
            remotePartyDataSyncProvider: remotePartySync);
        await controller.SignInAsync(new SignInRequest
        {
            ApiToken = "api-token",
            PersistLocally = false,
            UserId = "user-id"
        });

        await controller.PushCloudSyncAsync();

        var completion = Assert.Single(remotePartySync.DetectedCompletionCalls
            .Where(entry => entry.DetectionKey == "habitica-chat-collection:evilsanta:chat-collection")
            .DistinctBy(entry => entry.DetectionKey));
        Assert.Equal("evilsanta", completion.QuestKey);
        Assert.Equal("Trapper Santa", completion.QuestName);
        Assert.Equal("habitica-chat-collection:evilsanta:chat-collection", completion.DetectionKey);
        Assert.Equal(new[] { "100 XP" }, completion.RewardSummary);
    }

    private static AppSessionController CreateController(
        FakeDiagnosticsLogStore logStore,
        IRemoteUserDataSyncProvider? remoteUserDataSyncProvider = null,
        IRemotePartyDataSyncProvider? remotePartyDataSyncProvider = null,
        FakeKeyValueStorage? keyValueStorage = null)
    {
        var syncClient = new FakeHabiticaSyncClient(CreateUserSnapshot(), CreateTaskSnapshot(), CreatePartySnapshot());
        return CreateController(logStore, syncClient, remoteUserDataSyncProvider, remotePartyDataSyncProvider, keyValueStorage);
    }

    private static AppSessionController CreateController(
        FakeDiagnosticsLogStore logStore,
        FakeHabiticaSyncClient syncClient,
        IRemoteUserDataSyncProvider? remoteUserDataSyncProvider = null,
        IRemotePartyDataSyncProvider? remotePartyDataSyncProvider = null,
        FakeKeyValueStorage? keyValueStorage = null)
    {
        var credentialStore = new FakeCredentialStore();
        var storage = keyValueStorage ?? new FakeKeyValueStorage();
        var taskSnapshotStore = new TaskSnapshotStore(storage);
        var userSnapshotStore = new UserSnapshotStore(storage);
        var partySnapshotStore = new PartySnapshotStore(storage);
        var partyCronHistoryStore = new PartyCronHistoryStore(storage);
        var gearCatalogStore = new GearCatalogStore(storage);
        var equipmentPresetStore = new EquipmentPresetStore(storage);
        var logWriter = new DiagnosticsLogWriter(logStore, TimeProvider.System);

        var freshnessPolicy = new SnapshotFreshnessPolicy();
        var refreshCoordinator = new RefreshCoordinator(
            syncClient, userSnapshotStore, taskSnapshotStore, partySnapshotStore,
            partyCronHistoryStore, gearCatalogStore, logWriter, freshnessPolicy, TimeProvider.System);

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
            localUserDataPortabilityService: new LocalUserDataPortabilityService(storage, TimeProvider.System),
            refreshCoordinator: refreshCoordinator,
            remotePartyDataSyncProvider: remotePartyDataSyncProvider ?? new FakeRemotePartyDataSyncProvider(),
            remoteUserDataSyncProvider: remoteUserDataSyncProvider ?? new FakeRemoteUserDataSyncProvider(),
            taskSnapshotStore: taskSnapshotStore,
            userSnapshotStore: userSnapshotStore,
            diagnosticsLogStore: logStore,
            diagnosticsLogWriter: logWriter,
            snapshotFreshnessPolicy: freshnessPolicy,
            featureOptions: new AppFeatureOptions { HabiticaRequestDelayMilliseconds = 0 },
            timeProvider: TimeProvider.System);
    }

    private static async Task WaitForConditionAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var solutionPath = Path.Combine(directory.FullName, "Habitica.sln");
            if (File.Exists(solutionPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
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

        public int SectionUploadCount { get; private set; }

        public RemoteUserDataSnapshot? Snapshot { get; set; }

        public string? UploadedJson { get; private set; }

        public IReadOnlyList<string> SectionKeys { get; set; } = Array.Empty<string>();

        public Dictionary<string, RemoteUserDataSnapshot?> SectionSnapshots { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> UploadedSections { get; } = new(StringComparer.Ordinal);

        public List<string> UploadedSectionKeys { get; } = new();

        public HashSet<string> FailedSectionKeys { get; } = new(StringComparer.Ordinal);

        public void ClearUploads()
        {
            UploadedSections.Clear();
            UploadedSectionKeys.Clear();
        }

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

        public Task<RemoteUserDataSnapshot?> DownloadSectionAsync(HabiticaCredentials credentials, string sectionKey, CancellationToken cancellationToken)
        {
            return Task.FromResult(SectionSnapshots.GetValueOrDefault(sectionKey));
        }

        public Task<SectionUploadResult> UploadSectionAsync(HabiticaCredentials credentials, string sectionKey, string plainTextJson, CancellationToken cancellationToken)
        {
            SectionUploadCount++;
            UploadedSectionKeys.Add(sectionKey);
            if (FailedSectionKeys.Contains(sectionKey))
            {
                return Task.FromResult(new SectionUploadResult(false, "Upload failed."));
            }

            UploadedSections[sectionKey] = plainTextJson;
            return Task.FromResult(new SectionUploadResult(true));
        }

        public Task<IReadOnlyList<RemoteUserDataSnapshot?>> DownloadAllSectionsAsync(HabiticaCredentials credentials, IReadOnlyList<string> sectionKeys, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<RemoteUserDataSnapshot?>>(
                sectionKeys.Select(key => SectionSnapshots.GetValueOrDefault(key)).ToArray());
        }

        public Task<IReadOnlyList<string>> ListSectionsAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            return Task.FromResult(SectionKeys);
        }
    }

    private sealed class FakeRemotePartyDataSyncProvider : IRemotePartyDataSyncProvider
    {
        public int DownloadCount { get; private set; }

        public int UploadCount { get; private set; }

        public RemotePartyDataSnapshot? Snapshot { get; set; }

        public string? UploadedPartySnapshotJson { get; private set; }

        public string? UploadedCronHistoryJson { get; private set; }

        public PartySyncClaim? LastClaim { get; private set; }

        public List<(string QueueItemId, int Version)> InvitePartyCalls { get; } = new();

        public List<(string QueueItemId, int Version, bool Pinned)> PinQueueItemCalls { get; } = new();

        public List<(string QueueItemId, int Version)> SelectQueueItemCalls { get; } = new();

        public List<(string QueueItemId, int Version)> SkipQueueItemCalls { get; } = new();

        public List<(string QueueItemId, int Version)> ExpireQueueItemCalls { get; } = new();

        public List<(string QueueItemId, int Version)> RequeueQueueItemCalls { get; } = new();

        public List<(string QuestKey, DateTimeOffset CompletedAtUtc)> RemoveRecentlyCompletedQuestCalls { get; } = new();

        public List<(string QueueItemId, string QuestKey, string Transition, int? ParticipantsCount, string? CompletedByDisplayName, string? DetectionKey)> ReconcileCalls { get; } = new();

        public List<PartyDetectedQuestCompletion> DetectedCompletionCalls { get; } = new();

        public Task<RemotePartyDataSnapshot?> DownloadAsync(
            PartySyncClaim claim,
            CancellationToken cancellationToken)
        {
            LastClaim = claim;
            DownloadCount++;
            return Task.FromResult(Snapshot);
        }

        public Task UploadAsync(
            PartySyncClaim claim,
            string partySnapshotJson,
            string cronHistoryJson,
            CancellationToken cancellationToken)
        {
            LastClaim = claim;
            UploadCount++;
            UploadedPartySnapshotJson = partySnapshotJson;
            UploadedCronHistoryJson = cronHistoryJson;
            return Task.CompletedTask;
        }

        public Task<RemotePartyQuestState> PublishQuestPoolAsync(
            PartySyncClaim claim,
            IReadOnlyList<PartyQuestPoolEntry> entries,
            CancellationToken cancellationToken)
        {
            LastClaim = claim;
            return Task.FromResult(new RemotePartyQuestState(
                DateTimeOffset.UtcNow,
                Snapshot?.QuestQueue,
                entries,
                Snapshot?.RecentlyCompleted,
                Snapshot?.Management));
        }

        public Task<RemotePartyQuestState> AddQuestQueueItemAsync(
            PartySyncClaim claim,
            PartyQuestPoolEntry entry,
            CancellationToken cancellationToken)
        {
            LastClaim = claim;
            var queueEntry = new PartyQuestQueueEntry(
                Guid.NewGuid().ToString("N"),
                claim.PartyId,
                entry.QuestKey,
                entry.QuestName,
                claim.UserId,
                entry.OwnerDisplayName,
                PartyQuestQueueStatus.Queued,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                1,
                null,
                false,
                1,
                Array.Empty<PartyQuestVote>(),
                entry.Rewards);
            return Task.FromResult(new RemotePartyQuestState(DateTimeOffset.UtcNow, QuestQueue: new[] { queueEntry }, QuestPool: new[] { entry }));
        }

        public Task<RemotePartyQuestState> ToggleQuestVoteAsync(
            PartySyncClaim claim,
            string queueItemId,
            string voterDisplayName,
            CancellationToken cancellationToken)
        {
            LastClaim = claim;
            return Task.FromResult(new RemotePartyQuestState(DateTimeOffset.UtcNow));
        }

        public Task<RemotePartyQuestState> RemoveQuestQueueItemAsync(
            PartySyncClaim claim,
            string queueItemId,
            int version,
            CancellationToken cancellationToken)
        {
            LastClaim = claim;
            return Task.FromResult(new RemotePartyQuestState(DateTimeOffset.UtcNow));
        }

        public Task<RemotePartyQuestState> PinQuestQueueItemAsync(
            PartySyncClaim claim,
            string queueItemId,
            int version,
            bool pinned,
            CancellationToken cancellationToken)
        {
            LastClaim = claim;
            PinQueueItemCalls.Add((queueItemId, version, pinned));
            return Task.FromResult(new RemotePartyQuestState(DateTimeOffset.UtcNow));
        }

        public Task<RemotePartyQuestState> SelectQuestQueueItemAsync(
            PartySyncClaim claim,
            string queueItemId,
            int version,
            CancellationToken cancellationToken)
        {
            LastClaim = claim;
            SelectQueueItemCalls.Add((queueItemId, version));
            return Task.FromResult(new RemotePartyQuestState(DateTimeOffset.UtcNow));
        }

        public Task<RemotePartyQuestState> SkipQuestQueueItemAsync(
            PartySyncClaim claim,
            string queueItemId,
            int version,
            CancellationToken cancellationToken)
        {
            LastClaim = claim;
            SkipQueueItemCalls.Add((queueItemId, version));
            return Task.FromResult(new RemotePartyQuestState(DateTimeOffset.UtcNow));
        }

        public Task<RemotePartyQuestState> ExpireQuestQueueItemAsync(
            PartySyncClaim claim,
            string queueItemId,
            int version,
            CancellationToken cancellationToken)
        {
            LastClaim = claim;
            ExpireQueueItemCalls.Add((queueItemId, version));
            return Task.FromResult(new RemotePartyQuestState(DateTimeOffset.UtcNow));
        }

        public Task<RemotePartyQuestState> RequeueQuestQueueItemAsync(
            PartySyncClaim claim,
            string queueItemId,
            int version,
            CancellationToken cancellationToken)
        {
            LastClaim = claim;
            RequeueQueueItemCalls.Add((queueItemId, version));
            return Task.FromResult(new RemotePartyQuestState(DateTimeOffset.UtcNow));
        }

        public Task<RemotePartyQuestState> MarkQuestCompletedAsync(
            PartySyncClaim claim,
            string queueItemId,
            int version,
            int? participantsCount,
            CancellationToken cancellationToken)
        {
            LastClaim = claim;
            return Task.FromResult(new RemotePartyQuestState(DateTimeOffset.UtcNow));
        }

        public Task<RemotePartyQuestState> RemoveRecentlyCompletedQuestAsync(
            PartySyncClaim claim,
            string questKey,
            DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken)
        {
            LastClaim = claim;
            RemoveRecentlyCompletedQuestCalls.Add((questKey, completedAtUtc));
            return Task.FromResult(new RemotePartyQuestState(DateTimeOffset.UtcNow));
        }

        public Task<RemotePartyQuestState> InvitePartyAsync(
            PartySyncClaim claim,
            string queueItemId,
            int version,
            CancellationToken cancellationToken)
        {
            LastClaim = claim;
            InvitePartyCalls.Add((queueItemId, version));
            return Task.FromResult(new RemotePartyQuestState(DateTimeOffset.UtcNow));
        }

        public Task<RemotePartyQuestState> ReconcileQuestLifecycleAsync(
            PartySyncClaim claim,
            string queueItemId,
            string questKey,
            string transition,
            int? participantsCount,
            string? completedByDisplayName,
            string? detectionKey,
            CancellationToken cancellationToken)
        {
            LastClaim = claim;
            ReconcileCalls.Add((queueItemId, questKey, transition, participantsCount, completedByDisplayName, detectionKey));
            return Task.FromResult(new RemotePartyQuestState(DateTimeOffset.UtcNow));
        }

        public Task<RemotePartyQuestState> RecordDetectedQuestCompletionAsync(
            PartySyncClaim claim,
            PartyDetectedQuestCompletion completion,
            CancellationToken cancellationToken)
        {
            LastClaim = claim;
            DetectedCompletionCalls.Add(completion);
            return Task.FromResult(new RemotePartyQuestState(DateTimeOffset.UtcNow));
        }

        public Task<RemotePartyQuestState> AssignOfficerAsync(
            PartySyncClaim claim,
            string userId,
            string displayName,
            CancellationToken cancellationToken)
        {
            LastClaim = claim;
            return Task.FromResult(new RemotePartyQuestState(DateTimeOffset.UtcNow));
        }

        public Task<RemotePartyQuestState> AssignPartyOwnerAsync(
            PartySyncClaim claim,
            string userId,
            string displayName,
            CancellationToken cancellationToken)
        {
            LastClaim = claim;
            return Task.FromResult(new RemotePartyQuestState(DateTimeOffset.UtcNow));
        }

        public Task<RemotePartyQuestState> RemoveOfficerAsync(
            PartySyncClaim claim,
            string userId,
            CancellationToken cancellationToken)
        {
            LastClaim = claim;
            return Task.FromResult(new RemotePartyQuestState(DateTimeOffset.UtcNow));
        }

        public Task<RemotePartyQuestState> KickMemberAsync(
            PartySyncClaim claim,
            string userId,
            string displayName,
            string? reason,
            CancellationToken cancellationToken)
        {
            LastClaim = claim;
            return Task.FromResult(new RemotePartyQuestState(DateTimeOffset.UtcNow));
        }

        public Task<RemotePartyQuestState> UnkickMemberAsync(
            PartySyncClaim claim,
            string userId,
            CancellationToken cancellationToken)
        {
            LastClaim = claim;
            return Task.FromResult(new RemotePartyQuestState(DateTimeOffset.UtcNow));
        }

        public Task<RemotePartyQuestState> UpdateSettingsAsync(
            PartySyncClaim claim,
            PartySyncSettings settings,
            CancellationToken cancellationToken)
        {
            LastClaim = claim;
            return Task.FromResult(new RemotePartyQuestState(DateTimeOffset.UtcNow));
        }

        public Task<RemotePartyInviteProofActionResult> CreateInviteProofAsync(
            PartySyncClaim claim,
            string label,
            DateTimeOffset? expiresAtUtc,
            CancellationToken cancellationToken)
        {
            LastClaim = claim;
            return Task.FromResult(new RemotePartyInviteProofActionResult(
                DateTimeOffset.UtcNow,
                IssuedInviteProof: new PartySyncIssuedInviteProof("proof-id", "proof-token", label, DateTimeOffset.UtcNow, expiresAtUtc)));
        }

        public Task<RemotePartyInviteProofActionResult> RevokeInviteProofAsync(
            PartySyncClaim claim,
            string proofId,
            CancellationToken cancellationToken)
        {
            LastClaim = claim;
            return Task.FromResult(new RemotePartyInviteProofActionResult(DateTimeOffset.UtcNow));
        }

        public Task<RemotePartyInviteProofActionResult> RotateInviteProofAsync(
            PartySyncClaim claim,
            string proofId,
            CancellationToken cancellationToken)
        {
            LastClaim = claim;
            return Task.FromResult(new RemotePartyInviteProofActionResult(DateTimeOffset.UtcNow));
        }

        public Task<RemotePartyInviteProofActionResult> RemoveInviteProofAsync(
            PartySyncClaim claim,
            string proofId,
            CancellationToken cancellationToken)
        {
            LastClaim = claim;
            return Task.FromResult(new RemotePartyInviteProofActionResult(DateTimeOffset.UtcNow));
        }

        public Task<RemotePartyInviteProofActionResult> SetInviteProofModeAsync(
            PartySyncClaim claim,
            bool enabled,
            CancellationToken cancellationToken)
        {
            LastClaim = claim;
            return Task.FromResult(new RemotePartyInviteProofActionResult(DateTimeOffset.UtcNow));
        }

        public Task ActivateInviteProofAsync(
            string partyId,
            string proofId,
            string token,
            string? label,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task ClearInviteProofAsync(
            string partyId,
            CancellationToken cancellationToken)
        {
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

    private sealed class FakeHabiticaSyncClient : IHabiticaSyncClient
    {
        private readonly UserSnapshot _userSnapshot;
        private readonly TaskCollectionSnapshot _taskSnapshot;

        public FakeHabiticaSyncClient(UserSnapshot userSnapshot, TaskCollectionSnapshot taskSnapshot, PartySnapshot partySnapshot)
        {
            _userSnapshot = userSnapshot;
            _taskSnapshot = taskSnapshot;
            PartySnapshot = partySnapshot;
        }

        public List<(string SpellId, string? TargetTaskId)> CastCalls { get; } = new();

        public List<(EquipmentSetKind Kind, string Key)> EquipCalls { get; } = new();

        public List<string> OperationLog { get; } = new();

        public UserSnapshot UserSnapshot => _userSnapshot;

        public List<StatAllocation> StatAllocationCalls { get; } = new();

        public List<(string TaskId, TaskScoreDirection Direction)> ScoreTaskCalls { get; } = new();

        public int StartPartyQuestCalls { get; private set; }

        public List<string> InvitePartyQuestCalls { get; } = new();

        public int AcceptPartyQuestCalls { get; private set; }

        public int RejectPartyQuestCalls { get; private set; }

        public int BuyHealthPotionCalls { get; private set; }

        public List<int> PurchaseGemsForGoldCalls { get; } = new();

        public int? PurchaseGemsForGoldFailureCall { get; init; }

        public List<(InventorySellItemType Type, string Key)> SellInventoryItemCalls { get; } = new();

        public string? EquipGearFailureKey { get; init; }

        public Task EquipGearAsync(HabiticaCredentials credentials, string key, CancellationToken cancellationToken)
        {
            ThrowIfEquipGearFails(key);
            EquipCalls.Add((EquipmentSetKind.Battle, key));
            OperationLog.Add($"equip:{key}");
            return Task.CompletedTask;
        }

        public Task EquipGearAsync(HabiticaCredentials credentials, EquipmentSetKind kind, string key, CancellationToken cancellationToken)
        {
            ThrowIfEquipGearFails(key);
            EquipCalls.Add((kind, key));
            OperationLog.Add($"equip:{key}");
            return Task.CompletedTask;
        }

        private void ThrowIfEquipGearFails(string key)
        {
            if (string.Equals(key, EquipGearFailureKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Equip failed for {key}.");
            }
        }

        public Task CastSpellAsync(HabiticaCredentials credentials, string spellId, string? targetId, CancellationToken cancellationToken)
        {
            CastCalls.Add((spellId, targetId));
            return Task.CompletedTask;
        }

        public int RunCronCalls { get; private set; }

        public string? RunCronFailureMessage { get; init; }

        public Task RunCronAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            RunCronCalls++;
            OperationLog.Add("cron");
            if (!string.IsNullOrWhiteSpace(RunCronFailureMessage))
            {
                throw new InvalidOperationException(RunCronFailureMessage);
            }

            return Task.CompletedTask;
        }

        public Task AllocateStatsAsync(HabiticaCredentials credentials, StatAllocation allocation, CancellationToken cancellationToken)
        {
            StatAllocationCalls.Add(allocation);
            return Task.CompletedTask;
        }

        public Task ScoreTaskAsync(HabiticaCredentials credentials, string taskId, TaskScoreDirection direction, CancellationToken cancellationToken)
        {
            ScoreTaskCalls.Add((taskId, direction));
            return Task.CompletedTask;
        }

        public Task StartPartyQuestAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            StartPartyQuestCalls++;
            return Task.CompletedTask;
        }

        public Task InvitePartyToQuestAsync(HabiticaCredentials credentials, string questKey, CancellationToken cancellationToken)
        {
            InvitePartyQuestCalls.Add(questKey);
            return Task.CompletedTask;
        }

        public Task AcceptPartyQuestAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            AcceptPartyQuestCalls++;
            return Task.CompletedTask;
        }

        public Task RejectPartyQuestAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            RejectPartyQuestCalls++;
            return Task.CompletedTask;
        }

        public Task<ArmoirePurchaseSnapshot> BuyArmoireAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ArmoirePurchaseSnapshot("food", "Fish", "Fish", null, "Found Fish."));
        }

        public Task BuyHealthPotionAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            BuyHealthPotionCalls++;
            return Task.CompletedTask;
        }

        public Task PurchaseGemsForGoldAsync(HabiticaCredentials credentials, int quantity, CancellationToken cancellationToken)
        {
            PurchaseGemsForGoldCalls.Add(quantity);
            if (PurchaseGemsForGoldFailureCall == PurchaseGemsForGoldCalls.Count)
            {
                throw new InvalidOperationException("Gem purchase failed.");
            }

            return Task.CompletedTask;
        }

        public List<(string PetKey, string FoodKey, int Amount)> FeedPetCalls { get; } = new();

        public List<string> FeedPetAttemptedFoodKeys { get; } = new();

        public List<string> EquipPetCalls { get; } = new();

        public List<string> EquipMountCalls { get; } = new();

        public List<(string EggKey, string HatchingPotionKey)> HatchPetCalls { get; } = new();

        public string? FeedPetFailureFoodKey { get; init; }

        public Task FeedPetAsync(HabiticaCredentials credentials, string petKey, string foodKey, int amount, CancellationToken cancellationToken)
        {
            FeedPetAttemptedFoodKeys.Add(foodKey);
            if (string.Equals(foodKey, FeedPetFailureFoodKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Feed failed for {foodKey}.");
            }

            FeedPetCalls.Add((petKey, foodKey, amount));
            return Task.CompletedTask;
        }

        public Task EquipPetAsync(HabiticaCredentials credentials, string key, CancellationToken cancellationToken)
        {
            EquipPetCalls.Add(key);
            return Task.CompletedTask;
        }

        public Task EquipMountAsync(HabiticaCredentials credentials, string key, CancellationToken cancellationToken)
        {
            EquipMountCalls.Add(key);
            return Task.CompletedTask;
        }

        public Task HatchPetAsync(HabiticaCredentials credentials, string eggKey, string hatchingPotionKey, CancellationToken cancellationToken)
        {
            HatchPetCalls.Add((eggKey, hatchingPotionKey));
            return Task.CompletedTask;
        }

        public Task SellInventoryItemAsync(
            HabiticaCredentials credentials,
            InventorySellItemType type,
            string key,
            CancellationToken cancellationToken)
        {
            SellInventoryItemCalls.Add((type, key));
            return Task.CompletedTask;
        }

        public Task<GearCatalogSnapshot> GetContentCatalogAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
            => Task.FromResult(new GearCatalogSnapshot(DateTimeOffset.UtcNow, new Dictionary<string, GearCatalogItem>()));

        public PartySnapshot PartySnapshot { get; set; }

        public int GetPartySnapshotCalls { get; private set; }

        public string? GetPartySnapshotFailureMessage { get; set; }

        public Task<PartySnapshot> GetPartySnapshotAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            GetPartySnapshotCalls++;
            if (!string.IsNullOrWhiteSpace(GetPartySnapshotFailureMessage))
            {
                throw new InvalidOperationException(GetPartySnapshotFailureMessage);
            }

            return Task.FromResult(PartySnapshot);
        }

        public int GetTasksCalls { get; private set; }

        public Task<TaskCollectionSnapshot> GetTasksAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            GetTasksCalls++;
            return Task.FromResult(_taskSnapshot);
        }

        public Task<UserSummary> GetUserAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
            => Task.FromResult(new UserSummary(_userSnapshot.DisplayName, _userSnapshot.ClassName, _userSnapshot.Level));

        public int GetUserSnapshotCalls { get; private set; }

        public Task<UserSnapshot> GetUserSnapshotAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            GetUserSnapshotCalls++;
            return Task.FromResult(_userSnapshot);
        }
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

    private static PartySnapshot CreatePartySnapshotWithCompletableAwaitingDamage()
    {
        return new PartySnapshot(
            DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
            "party-123",
            "Night Owls",
            "Quest-focused party",
            2,
            new PartyQuestSnapshot(
                "dragon",
                true,
                0m,
                0m,
                2,
                BossHealthRemaining: 500m,
                BossHealthTotal: 500m),
            new[]
            {
                new PartyMemberSnapshot(
                    "user-id",
                    "Mage Tester",
                    DateTimeOffset.Parse("2026-04-27T09:00:00Z"),
                    0,
                    0,
                    PartyCronState.CronedToday,
                    "Croned today.",
                    "2026-04-27",
                    DateTimeOffset.Parse("2026-04-27T00:00:00Z"),
                    PendingQuestDamage: 0m,
                    ParticipationStatus: PartyQuestParticipationStatus.Accepted),
                new PartyMemberSnapshot(
                    "member-2",
                    "Marek50818",
                    DateTimeOffset.Parse("2026-04-26T10:10:00Z"),
                    0,
                    0,
                    PartyCronState.NotCronedYet,
                    "Not croned yet.",
                    "2026-04-27",
                    DateTimeOffset.Parse("2026-04-27T00:00:00Z"),
                    PendingQuestDamage: 600m,
                    ParticipationStatus: PartyQuestParticipationStatus.Accepted)
            });
    }

    private static PartySnapshot CreatePartySnapshotWithMember()
    {
        return new PartySnapshot(
            DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
            "party-123",
            "Night Owls",
            "Quest-focused party",
            1,
            new PartyQuestSnapshot("dragon", true, 12.5m, 3m, 1),
            new[]
            {
                new PartyMemberSnapshot(
                    "user-id",
                    "Mage Tester",
                    DateTimeOffset.Parse("2026-04-27T09:00:00Z"),
                    0,
                    0,
                    PartyCronState.CronedToday,
                    "Croned today.",
                    "2026-04-27",
                    DateTimeOffset.Parse("2026-04-27T00:00:00Z"))
            });
    }
}
