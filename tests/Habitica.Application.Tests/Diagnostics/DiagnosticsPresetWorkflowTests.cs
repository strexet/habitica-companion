using Habitica.Api;
using Habitica.Application.Diagnostics;
using Habitica.Domain.Auth;
using Habitica.Domain.Diagnostics;
using Habitica.Domain.Party;
using Habitica.Domain.Tasks;
using Habitica.Domain.User;
using Habitica.Storage;

namespace Habitica.Application.Tests.Diagnostics;

public sealed class DiagnosticsPresetWorkflowTests
{
    [Fact]
    public async Task RunAsync_returns_preview_and_writes_success_log()
    {
        var client = new FakeHabiticaSyncClient(CreateUserSnapshot(), CreateTaskSnapshot(), CreatePartySnapshot());
        var logStore = new FakeDiagnosticsLogStore();
        var workflow = new DiagnosticsPresetWorkflow(
            client,
            new DiagnosticsLogWriter(logStore, TimeProvider.System));

        var result = await workflow.RunAsync(
            new HabiticaCredentials("user-id", "api-token"),
            DiagnosticsPreset.UserAccount,
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.RequestCount);
        Assert.Contains("\"displayName\"", result.ResponsePreview, StringComparison.Ordinal);
        Assert.Contains(logStore.Entries, entry =>
            entry.Operation == "preset-user-account"
            && entry.FeatureArea == DiagnosticsFeatureArea.Diagnostics
            && entry.Severity == DiagnosticsSeverity.Success);
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
        {
            return Task.CompletedTask;
        }

        public Task EquipGearAsync(HabiticaCredentials credentials, EquipmentSetKind kind, string key, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
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

        public Task StartPartyQuestAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task InvitePartyToQuestAsync(HabiticaCredentials credentials, string questKey, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task AcceptPartyQuestAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task RejectPartyQuestAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<ArmoirePurchaseSnapshot> BuyArmoireAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ArmoirePurchaseSnapshot("food", "Fish", "Fish", null, "Found Fish."));
        }

        public Task BuyHealthPotionAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task FeedPetAsync(HabiticaCredentials credentials, string petKey, string foodKey, int amount, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task EquipPetAsync(HabiticaCredentials credentials, string key, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task EquipMountAsync(HabiticaCredentials credentials, string key, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task HatchPetAsync(HabiticaCredentials credentials, string eggKey, string hatchingPotionKey, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SellInventoryItemAsync(
            HabiticaCredentials credentials,
            InventorySellItemType type,
            string key,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
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
}
