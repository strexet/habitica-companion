using Habitica.Domain.Auth;
using Habitica.Domain.Party;

namespace Habitica.WebApp.Sync;

public interface IRemotePartyDataSyncProvider
{
    Task<RemotePartyDataSnapshot?> DownloadAsync(
        HabiticaCredentials credentials,
        string partyId,
        CancellationToken cancellationToken);

    Task UploadAsync(
        HabiticaCredentials credentials,
        string partyId,
        string partySnapshotJson,
        string cronHistoryJson,
        CancellationToken cancellationToken);

    Task<RemotePartyQuestState> PublishQuestPoolAsync(
        HabiticaCredentials credentials,
        string partyId,
        IReadOnlyList<PartyQuestPoolEntry> entries,
        CancellationToken cancellationToken);

    Task<RemotePartyQuestState> AddQuestQueueItemAsync(
        HabiticaCredentials credentials,
        string partyId,
        PartyQuestPoolEntry entry,
        CancellationToken cancellationToken);

    Task<RemotePartyQuestState> ToggleQuestVoteAsync(
        HabiticaCredentials credentials,
        string partyId,
        string queueItemId,
        string voterDisplayName,
        CancellationToken cancellationToken);

    Task<RemotePartyQuestState> RemoveQuestQueueItemAsync(
        HabiticaCredentials credentials,
        string partyId,
        string queueItemId,
        int version,
        CancellationToken cancellationToken);

    Task<RemotePartyQuestState> ReconcileQuestLifecycleAsync(
        HabiticaCredentials credentials,
        string partyId,
        string queueItemId,
        string questKey,
        string transition,
        int? participantsCount,
        string? completedByDisplayName,
        CancellationToken cancellationToken);
}

public sealed record RemotePartyDataSnapshot(
    string? PartySnapshotJson,
    string? CronHistoryJson,
    DateTimeOffset? UpdatedAtUtc,
    IReadOnlyList<PartyQuestQueueEntry>? QuestQueue = null,
    IReadOnlyList<PartyQuestPoolEntry>? QuestPool = null,
    IReadOnlyList<PartyRecentlyCompletedQuest>? RecentlyCompleted = null);

public sealed record RemotePartyQuestState(
    DateTimeOffset? UpdatedAtUtc,
    IReadOnlyList<PartyQuestQueueEntry>? QuestQueue = null,
    IReadOnlyList<PartyQuestPoolEntry>? QuestPool = null,
    IReadOnlyList<PartyRecentlyCompletedQuest>? RecentlyCompleted = null);
