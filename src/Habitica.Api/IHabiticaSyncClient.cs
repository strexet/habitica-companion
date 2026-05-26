using Habitica.Domain.Auth;
using Habitica.Domain.Party;
using Habitica.Domain.Tasks;
using Habitica.Domain.User;

namespace Habitica.Api;

public interface IHabiticaSyncClient
{
    Task<UserSummary> GetUserAsync(HabiticaCredentials credentials, CancellationToken cancellationToken);

    Task<UserSnapshot> GetUserSnapshotAsync(HabiticaCredentials credentials, CancellationToken cancellationToken);

    Task<PartySnapshot> GetPartySnapshotAsync(HabiticaCredentials credentials, CancellationToken cancellationToken);

    Task EquipGearAsync(HabiticaCredentials credentials, string key, CancellationToken cancellationToken);

    Task EquipGearAsync(HabiticaCredentials credentials, EquipmentSetKind kind, string key, CancellationToken cancellationToken);

    Task CastSpellAsync(HabiticaCredentials credentials, string spellId, string? targetId, CancellationToken cancellationToken);

    Task RunCronAsync(HabiticaCredentials credentials, CancellationToken cancellationToken);

    Task AllocateStatsAsync(HabiticaCredentials credentials, StatAllocation allocation, CancellationToken cancellationToken);

    Task<ArmoirePurchaseSnapshot> BuyArmoireAsync(HabiticaCredentials credentials, CancellationToken cancellationToken);

    Task BuyHealthPotionAsync(HabiticaCredentials credentials, CancellationToken cancellationToken);

    Task SellInventoryItemAsync(
        HabiticaCredentials credentials,
        InventorySellItemType type,
        string key,
        CancellationToken cancellationToken);

    Task<GearCatalogSnapshot> GetContentCatalogAsync(HabiticaCredentials credentials, CancellationToken cancellationToken);

    Task<TaskCollectionSnapshot> GetTasksAsync(HabiticaCredentials credentials, CancellationToken cancellationToken);

    Task ScoreTaskAsync(HabiticaCredentials credentials, string taskId, TaskScoreDirection direction, CancellationToken cancellationToken);

    Task InvitePartyToQuestAsync(HabiticaCredentials credentials, string questKey, CancellationToken cancellationToken);

    Task AcceptPartyQuestAsync(HabiticaCredentials credentials, CancellationToken cancellationToken);

    Task RejectPartyQuestAsync(HabiticaCredentials credentials, CancellationToken cancellationToken);

    Task StartPartyQuestAsync(HabiticaCredentials credentials, CancellationToken cancellationToken);
}
