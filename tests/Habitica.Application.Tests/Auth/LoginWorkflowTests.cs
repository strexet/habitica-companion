using Habitica.Application.Auth;
using Habitica.Application.Sync;
using Habitica.Api;
using Habitica.Domain.Auth;
using Habitica.Domain.Party;
using Habitica.Domain.Tasks;
using Habitica.Domain.User;
using Habitica.Storage;

namespace Habitica.Application.Tests.Auth;

public sealed class LoginWorkflowTests
{
    [Fact]
    public async Task AuthenticateAndSyncAsync_clears_persistent_credentials_when_persistence_is_not_requested()
    {
        var apiClient = new FakeHabiticaSyncClient();
        var credentialStore = new FakeCredentialStore();
        var taskStore = new FakeTaskSnapshotStore();
        var userStore = new FakeUserSnapshotStore();
        var partyStore = new FakePartySnapshotStore();
        var workflow = new LoginWorkflow(apiClient, credentialStore, taskStore, userStore, partyStore);

        var result = await workflow.AuthenticateAndSyncAsync(
            new LoginCommand("user-id", "api-token", false),
            CancellationToken.None);

        Assert.Equal("Mage Tester", result.DisplayName);
        Assert.Equal(2, result.TaskCount);
        Assert.True(credentialStore.ClearedPersistentCredentials);
        Assert.Null(credentialStore.SavedCredentials);
        Assert.NotNull(taskStore.LastSavedSnapshot);
        Assert.NotNull(userStore.LastSavedSnapshot);
        Assert.NotNull(partyStore.LastSavedSnapshot);
    }

    [Fact]
    public async Task AuthenticateAndSyncAsync_persists_credentials_when_opted_in()
    {
        var apiClient = new FakeHabiticaSyncClient();
        var credentialStore = new FakeCredentialStore();
        var taskStore = new FakeTaskSnapshotStore();
        var userStore = new FakeUserSnapshotStore();
        var partyStore = new FakePartySnapshotStore();
        var workflow = new LoginWorkflow(apiClient, credentialStore, taskStore, userStore, partyStore);

        await workflow.AuthenticateAndSyncAsync(
            new LoginCommand("user-id", "api-token", true),
            CancellationToken.None);

        Assert.Equal(new HabiticaCredentials("user-id", "api-token"), credentialStore.SavedCredentials);
    }

    private sealed class FakeHabiticaSyncClient : IHabiticaSyncClient
    {
        public Task<UserSummary> GetUserAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            return Task.FromResult(new UserSummary("Mage Tester", "wizard", 15));
        }

        public Task<UserSnapshot> GetUserSnapshotAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            return Task.FromResult(new UserSnapshot(
                DateTimeOffset.Parse("2026-04-24T12:00:00Z"),
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
                new InventorySnapshot(1, 1, 1, 1, 1, 1, new[] { "armor_wizard_4", "head_wizard_3" })));
        }

        public Task<PartySnapshot> GetPartySnapshotAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            return Task.FromResult(new PartySnapshot(
                DateTimeOffset.Parse("2026-04-24T12:00:00Z"),
                "party-123",
                "Night Owls",
                "Quest-focused party",
                4,
                new PartyQuestSnapshot("dragon", true, 12.5m, 3m, 2)));
        }

        public Task<TaskCollectionSnapshot> GetTasksAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            var snapshot = new TaskCollectionSnapshot(
                DateTimeOffset.Parse("2026-04-24T12:00:00Z"),
                new[]
                {
                    new TaskSnapshot("todo-open", "Buy milk", TaskType.Todo, false, 2, null, null),
                    new TaskSnapshot("daily-open", "Exercise", TaskType.Daily, false, 1, null, null)
                });

            return Task.FromResult(snapshot);
        }
    }

    private sealed class FakeCredentialStore : ICredentialStore
    {
        public bool ClearedPersistentCredentials { get; private set; }

        public HabiticaCredentials? SavedCredentials { get; private set; }

        public Task<HabiticaCredentials?> GetPersistentCredentialsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(SavedCredentials);
        }

        public Task ClearPersistentCredentialsAsync(CancellationToken cancellationToken)
        {
            ClearedPersistentCredentials = true;
            return Task.CompletedTask;
        }

        public Task SavePersistentCredentialsAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            SavedCredentials = credentials;
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

    private sealed class FakePartySnapshotStore : IPartySnapshotStore
    {
        public PartySnapshot? LastSavedSnapshot { get; private set; }

        public bool Cleared { get; private set; }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            Cleared = true;
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
