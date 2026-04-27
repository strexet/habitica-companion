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
using Habitica.WebApp.State;

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

    private static AppSessionController CreateController(FakeDiagnosticsLogStore logStore)
    {
        var syncClient = new FakeHabiticaSyncClient(CreateUserSnapshot(), CreateTaskSnapshot(), CreatePartySnapshot());
        var credentialStore = new FakeCredentialStore();
        var taskSnapshotStore = new FakeTaskSnapshotStore();
        var userSnapshotStore = new FakeUserSnapshotStore();
        var partySnapshotStore = new FakePartySnapshotStore();
        var logWriter = new DiagnosticsLogWriter(logStore, TimeProvider.System);

        return new AppSessionController(
            loginWorkflow: new LoginWorkflow(syncClient, credentialStore, taskSnapshotStore, userSnapshotStore, partySnapshotStore, logWriter),
            liveTestWorkflow: new LiveTestWorkflow(syncClient, userSnapshotStore, taskSnapshotStore, partySnapshotStore, logWriter, TimeProvider.System),
            diagnosticsPresetWorkflow: new DiagnosticsPresetWorkflow(syncClient, logWriter),
            credentialStore: credentialStore,
            partySnapshotStore: partySnapshotStore,
            taskSnapshotStore: taskSnapshotStore,
            userSnapshotStore: userSnapshotStore,
            diagnosticsLogStore: logStore,
            diagnosticsLogWriter: logWriter,
            snapshotFreshnessPolicy: new SnapshotFreshnessPolicy(),
            timeProvider: TimeProvider.System);
    }

    private sealed class FakeDiagnosticsLogStore : IDiagnosticsLogStore
    {
        private readonly List<DiagnosticsLogEntry> _entries;

        public FakeDiagnosticsLogStore(IEnumerable<DiagnosticsLogEntry> entries)
        {
            _entries = entries.ToList();
        }

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

        public Task EquipGearAsync(HabiticaCredentials credentials, string key, CancellationToken cancellationToken)
            => Task.CompletedTask;

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
