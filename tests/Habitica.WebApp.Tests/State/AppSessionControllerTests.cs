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
        Assert.True(remoteSync.SectionUploadCount >= 2);
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
    public async Task SaveEquipmentPresetAsync_keeps_battle_accessory_slots_in_preset()
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
        Assert.Equal("back_wizard_1", preset.Slots.Back);
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
            && entry.Metadata["healthBefore"] == "20"
            && entry.Metadata["goldBefore"] == "25");
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
    public async Task InvitePartyToQuestAsync_invites_owned_queued_quest_and_refreshes_party()
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

        Assert.True(result.Succeeded);
        Assert.Equal("dragon", Assert.Single(syncClient.InvitePartyQuestCalls));
        Assert.Equal("queue-1", Assert.Single(remotePartySync.InvitePartyCalls).QueueItemId);
        Assert.Contains(logStore.Entries, entry =>
            entry.FeatureArea == DiagnosticsFeatureArea.Party
            && entry.Operation == "party-quest-invite"
            && entry.Severity == DiagnosticsSeverity.Success);
    }

    [Fact]
    public async Task InvitePartyToQuestAsync_invites_owned_selected_quest_when_cached_quest_is_inactive()
    {
        var logStore = new FakeDiagnosticsLogStore(Array.Empty<DiagnosticsLogEntry>());
        var syncClient = new FakeHabiticaSyncClient(
            CreateUserSnapshot(),
            CreateTaskSnapshot(),
            CreatePartySnapshot() with
            {
                RetrievedAtUtc = DateTimeOffset.UtcNow,
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

        var result = await controller.InvitePartyToQuestAsync("queue-1", 1);

        Assert.True(result.Succeeded);
        Assert.Equal("dragon", Assert.Single(syncClient.InvitePartyQuestCalls));
        Assert.Equal("queue-1", Assert.Single(remotePartySync.InvitePartyCalls).QueueItemId);
    }

    [Fact]
    public async Task InvitePartyToQuestAsync_rejects_when_party_already_has_active_quest()
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
        Assert.Equal("The party already has an active quest.", result.Message);
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
        Assert.Equal("Refresh party data before inviting.", result.Message);
        Assert.Empty(syncClient.InvitePartyQuestCalls);
        Assert.Empty(remotePartySync.InvitePartyCalls);
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
        IRemotePartyDataSyncProvider? remotePartyDataSyncProvider = null)
    {
        var syncClient = new FakeHabiticaSyncClient(CreateUserSnapshot(), CreateTaskSnapshot(), CreatePartySnapshot());
        return CreateController(logStore, syncClient, remoteUserDataSyncProvider, remotePartyDataSyncProvider);
    }

    private static AppSessionController CreateController(
        FakeDiagnosticsLogStore logStore,
        FakeHabiticaSyncClient syncClient,
        IRemoteUserDataSyncProvider? remoteUserDataSyncProvider = null,
        IRemotePartyDataSyncProvider? remotePartyDataSyncProvider = null)
    {
        var credentialStore = new FakeCredentialStore();
        var keyValueStorage = new FakeKeyValueStorage();
        var taskSnapshotStore = new TaskSnapshotStore(keyValueStorage);
        var userSnapshotStore = new UserSnapshotStore(keyValueStorage);
        var partySnapshotStore = new PartySnapshotStore(keyValueStorage);
        var partyCronHistoryStore = new PartyCronHistoryStore(keyValueStorage);
        var gearCatalogStore = new GearCatalogStore(keyValueStorage);
        var equipmentPresetStore = new EquipmentPresetStore(keyValueStorage);
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
            localUserDataPortabilityService: new LocalUserDataPortabilityService(keyValueStorage, TimeProvider.System),
            refreshCoordinator: refreshCoordinator,
            remotePartyDataSyncProvider: remotePartyDataSyncProvider ?? new FakeRemotePartyDataSyncProvider(),
            remoteUserDataSyncProvider: remoteUserDataSyncProvider ?? new FakeRemoteUserDataSyncProvider(),
            taskSnapshotStore: taskSnapshotStore,
            userSnapshotStore: userSnapshotStore,
            diagnosticsLogStore: logStore,
            diagnosticsLogWriter: logWriter,
            snapshotFreshnessPolicy: freshnessPolicy,
            featureOptions: new AppFeatureOptions(),
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

        public int SectionUploadCount { get; private set; }

        public RemoteUserDataSnapshot? Snapshot { get; set; }

        public string? UploadedJson { get; private set; }

        public List<string> UploadedSectionKeys { get; } = new();

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
            return Task.FromResult<RemoteUserDataSnapshot?>(null);
        }

        public Task<SectionUploadResult> UploadSectionAsync(HabiticaCredentials credentials, string sectionKey, string plainTextJson, CancellationToken cancellationToken)
        {
            SectionUploadCount++;
            UploadedSectionKeys.Add(sectionKey);
            return Task.FromResult(new SectionUploadResult(true));
        }

        public Task<IReadOnlyList<RemoteUserDataSnapshot?>> DownloadAllSectionsAsync(HabiticaCredentials credentials, IReadOnlyList<string> sectionKeys, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<RemoteUserDataSnapshot?>>(Array.Empty<RemoteUserDataSnapshot?>());
        }

        public Task<IReadOnlyList<string>> ListSectionsAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
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

        public UserSnapshot UserSnapshot => _userSnapshot;

        public List<StatAllocation> StatAllocationCalls { get; } = new();

        public List<(string TaskId, TaskScoreDirection Direction)> ScoreTaskCalls { get; } = new();

        public int StartPartyQuestCalls { get; private set; }

        public List<string> InvitePartyQuestCalls { get; } = new();

        public int BuyHealthPotionCalls { get; private set; }

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

        public int RunCronCalls { get; private set; }

        public Task RunCronAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            RunCronCalls++;
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

        public Task<ArmoirePurchaseSnapshot> BuyArmoireAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ArmoirePurchaseSnapshot("food", "Fish", "Fish", null, "Found Fish."));
        }

        public Task BuyHealthPotionAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            BuyHealthPotionCalls++;
            return Task.CompletedTask;
        }

        public Task<GearCatalogSnapshot> GetContentCatalogAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
            => Task.FromResult(new GearCatalogSnapshot(DateTimeOffset.UtcNow, new Dictionary<string, GearCatalogItem>()));

        public PartySnapshot PartySnapshot { get; set; }

        public int GetPartySnapshotCalls { get; private set; }

        public Task<PartySnapshot> GetPartySnapshotAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            GetPartySnapshotCalls++;
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
