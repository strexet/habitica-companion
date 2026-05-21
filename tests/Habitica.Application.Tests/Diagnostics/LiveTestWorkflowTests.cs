using Habitica.Api;
using Habitica.Application.Diagnostics;
using Habitica.Domain.Auth;
using Habitica.Domain.Diagnostics;
using Habitica.Domain.Party;
using Habitica.Domain.Tasks;
using Habitica.Domain.User;
using Habitica.Storage;

namespace Habitica.Application.Tests.Diagnostics;

public sealed class LiveTestWorkflowTests
{
    [Fact]
    public async Task RunSafeLiveTestsAsync_saves_snapshots_and_returns_passed_results()
    {
        var client = new FakeHabiticaSyncClient(CreateUserSnapshot(), CreateTaskSnapshot(), CreatePartySnapshot());
        var userStore = new FakeUserSnapshotStore();
        var taskStore = new FakeTaskSnapshotStore();
        var partyStore = new FakePartySnapshotStore();
        var logStore = new FakeDiagnosticsLogStore();
        var workflow = new LiveTestWorkflow(client, userStore, taskStore, partyStore, new DiagnosticsLogWriter(logStore, TimeProvider.System), TimeProvider.System);

        var result = await workflow.RunSafeLiveTestsAsync(new HabiticaCredentials("user-id", "api-token"), CancellationToken.None);

        Assert.Equal(3, result.TotalRequests);
        Assert.Equal(4, result.Results.Count);
        Assert.All(result.Results.Where(test => test.Status != LiveTestStatus.Skipped), test => Assert.Equal(LiveTestStatus.Passed, test.Status));
        Assert.NotNull(userStore.LastSavedSnapshot);
        Assert.NotNull(taskStore.LastSavedSnapshot);
        Assert.NotNull(partyStore.LastSavedSnapshot);
        Assert.Contains(logStore.Entries, entry =>
            entry.Operation == "safe-live-tests"
            && entry.Severity == DiagnosticsSeverity.Success);
    }

    [Fact]
    public async Task RunReversibleGearTestAsync_skips_when_no_alternate_gear_exists()
    {
        var snapshot = CreateUserSnapshot() with
        {
            Inventory = new InventorySnapshot(1, 5, 1, 1, 1, 1, new[] { "weapon_wizard_5" })
        };
        var client = new FakeHabiticaSyncClient(snapshot, CreateTaskSnapshot(), CreatePartySnapshot());
        var workflow = new LiveTestWorkflow(client, new FakeUserSnapshotStore(), new FakeTaskSnapshotStore(), new FakePartySnapshotStore(), new DiagnosticsLogWriter(new FakeDiagnosticsLogStore(), TimeProvider.System), TimeProvider.System);

        var result = await workflow.RunReversibleGearTestAsync(new HabiticaCredentials("user-id", "api-token"), CancellationToken.None);

        var test = Assert.Single(result.Results);
        Assert.Equal(LiveTestStatus.Skipped, test.Status);
        Assert.Empty(client.EquipCalls);
    }

    [Fact]
    public async Task RunReversibleGearTestAsync_restores_original_gear_after_roundtrip()
    {
        var client = new FakeHabiticaSyncClient(CreateUserSnapshot(), CreateTaskSnapshot(), CreatePartySnapshot());
        var userStore = new FakeUserSnapshotStore();
        var workflow = new LiveTestWorkflow(client, userStore, new FakeTaskSnapshotStore(), new FakePartySnapshotStore(), new DiagnosticsLogWriter(new FakeDiagnosticsLogStore(), TimeProvider.System), TimeProvider.System);

        var result = await workflow.RunReversibleGearTestAsync(new HabiticaCredentials("user-id", "api-token"), CancellationToken.None);

        var test = Assert.Single(result.Results);
        Assert.Equal(LiveTestStatus.Passed, test.Status);
        Assert.Equal(new[] { "weapon_warrior_6", "weapon_wizard_5" }, client.EquipCalls);
        Assert.Equal("weapon_wizard_5", userStore.LastSavedSnapshot!.Equipment.Battle.Weapon);
    }

    private sealed class FakeDiagnosticsLogStore : IDiagnosticsLogStore
    {
        public List<DiagnosticsLogEntry> Entries { get; } = new();

        public Task<IReadOnlyList<DiagnosticsLogEntry>> GetRecentAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<DiagnosticsLogEntry>>(Entries);
        }

        public Task AppendAsync(DiagnosticsLogEntry entry, CancellationToken cancellationToken)
        {
            Entries.Insert(0, entry);
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            Entries.Clear();
            return Task.CompletedTask;
        }
    }

    private static UserSnapshot CreateUserSnapshot()
    {
        return new UserSnapshot(
            DateTimeOffset.Parse("2026-04-26T10:00:00Z"),
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
                5,
                1,
                1,
                1,
                1,
                new[]
                {
                    "head_wizard_3",
                    "weapon_warrior_6",
                    "weapon_wizard_5"
                }));
    }

    private static TaskCollectionSnapshot CreateTaskSnapshot()
    {
        return new TaskCollectionSnapshot(
            DateTimeOffset.Parse("2026-04-26T10:00:00Z"),
            new[]
            {
                new TaskSnapshot("todo-1", "Buy milk", TaskType.Todo, false, 1.5m, null, null),
                new TaskSnapshot("daily-1", "Exercise", TaskType.Daily, false, 1m, null, null)
            });
    }

    private static PartySnapshot CreatePartySnapshot()
    {
        return new PartySnapshot(
            DateTimeOffset.Parse("2026-04-26T10:00:00Z"),
            "party-123",
            "Night Owls",
            "Quest-focused party",
            4,
            new PartyQuestSnapshot("dragon", true, 12.5m, 3m, 2));
    }

    private sealed class FakeHabiticaSyncClient : IHabiticaSyncClient
    {
        private UserSnapshot _userSnapshot;
        private readonly TaskCollectionSnapshot _taskSnapshot;
        private readonly PartySnapshot _partySnapshot;

        public FakeHabiticaSyncClient(UserSnapshot userSnapshot, TaskCollectionSnapshot taskSnapshot, PartySnapshot partySnapshot)
        {
            _userSnapshot = userSnapshot;
            _taskSnapshot = taskSnapshot;
            _partySnapshot = partySnapshot;
        }

        public List<string> EquipCalls { get; } = new();

        public Task EquipGearAsync(HabiticaCredentials credentials, string key, CancellationToken cancellationToken)
        {
            EquipCalls.Add(key);

            _userSnapshot = _userSnapshot with
            {
                Equipment = _userSnapshot.Equipment with
                {
                    Battle = _userSnapshot.Equipment.Battle with
                    {
                        Weapon = key.StartsWith("weapon_", StringComparison.Ordinal) ? key : _userSnapshot.Equipment.Battle.Weapon
                    }
                }
            };

            return Task.CompletedTask;
        }

        public Task EquipGearAsync(HabiticaCredentials credentials, EquipmentSetKind kind, string key, CancellationToken cancellationToken)
        {
            return EquipGearAsync(credentials, key, cancellationToken);
        }

        public Task CastSpellAsync(HabiticaCredentials credentials, string spellId, string? targetId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task RunCronAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task AllocateStatsAsync(HabiticaCredentials credentials, StatAllocation allocation, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task ScoreTaskAsync(HabiticaCredentials credentials, string taskId, TaskScoreDirection direction, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<ArmoirePurchaseSnapshot> BuyArmoireAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ArmoirePurchaseSnapshot("food", "Fish", "Fish", null, "Found Fish."));
        }

        public Task<GearCatalogSnapshot> GetContentCatalogAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            return Task.FromResult(new GearCatalogSnapshot(DateTimeOffset.UtcNow, new Dictionary<string, GearCatalogItem>()));
        }

        public Task<PartySnapshot> GetPartySnapshotAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            return Task.FromResult(_partySnapshot);
        }

        public Task<TaskCollectionSnapshot> GetTasksAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            return Task.FromResult(_taskSnapshot);
        }

        public Task<UserSummary> GetUserAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            return Task.FromResult(new UserSummary(_userSnapshot.DisplayName, _userSnapshot.ClassName, _userSnapshot.Level));
        }

        public Task<UserSnapshot> GetUserSnapshotAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            return Task.FromResult(_userSnapshot);
        }
    }

    private sealed class FakeUserSnapshotStore : IUserSnapshotStore
    {
        public UserSnapshot? LastSavedSnapshot { get; private set; }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            LastSavedSnapshot = null;
            return Task.CompletedTask;
        }

        public Task<UserSnapshot?> GetLatestAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(LastSavedSnapshot);
        }

        public Task SaveAsync(UserSnapshot snapshot, CancellationToken cancellationToken)
        {
            LastSavedSnapshot = snapshot;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTaskSnapshotStore : ITaskSnapshotStore
    {
        public TaskCollectionSnapshot? LastSavedSnapshot { get; private set; }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            LastSavedSnapshot = null;
            return Task.CompletedTask;
        }

        public Task<TaskCollectionSnapshot?> GetLatestAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(LastSavedSnapshot);
        }

        public Task SaveAsync(TaskCollectionSnapshot snapshot, CancellationToken cancellationToken)
        {
            LastSavedSnapshot = snapshot;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePartySnapshotStore : IPartySnapshotStore
    {
        public PartySnapshot? LastSavedSnapshot { get; private set; }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            LastSavedSnapshot = null;
            return Task.CompletedTask;
        }

        public Task<PartySnapshot?> GetLatestAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(LastSavedSnapshot);
        }

        public Task SaveAsync(PartySnapshot snapshot, CancellationToken cancellationToken)
        {
            LastSavedSnapshot = snapshot;
            return Task.CompletedTask;
        }
    }
}
