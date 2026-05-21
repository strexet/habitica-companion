using Habitica.Domain.Party;

namespace Habitica.WebApp.Sync;

public interface IRemotePartyDataSyncProvider
{
    Task<RemotePartyDataSnapshot?> DownloadAsync(
        PartySyncClaim claim,
        CancellationToken cancellationToken);

    Task UploadAsync(
        PartySyncClaim claim,
        string partySnapshotJson,
        string cronHistoryJson,
        CancellationToken cancellationToken);

    Task<RemotePartyQuestState> PublishQuestPoolAsync(
        PartySyncClaim claim,
        IReadOnlyList<PartyQuestPoolEntry> entries,
        CancellationToken cancellationToken);

    Task<RemotePartyQuestState> AddQuestQueueItemAsync(
        PartySyncClaim claim,
        PartyQuestPoolEntry entry,
        CancellationToken cancellationToken);

    Task<RemotePartyQuestState> ToggleQuestVoteAsync(
        PartySyncClaim claim,
        string queueItemId,
        string voterDisplayName,
        CancellationToken cancellationToken);

    Task<RemotePartyQuestState> RemoveQuestQueueItemAsync(
        PartySyncClaim claim,
        string queueItemId,
        int version,
        CancellationToken cancellationToken);

    Task<RemotePartyQuestState> MarkQuestCompletedAsync(
        PartySyncClaim claim,
        string queueItemId,
        int version,
        int? participantsCount,
        CancellationToken cancellationToken);

    Task<RemotePartyQuestState> ReconcileQuestLifecycleAsync(
        PartySyncClaim claim,
        string queueItemId,
        string questKey,
        string transition,
        int? participantsCount,
        string? completedByDisplayName,
        CancellationToken cancellationToken);

    Task<RemotePartyQuestState> AssignOfficerAsync(
        PartySyncClaim claim,
        string userId,
        string displayName,
        CancellationToken cancellationToken);

    Task<RemotePartyQuestState> AssignPartyOwnerAsync(
        PartySyncClaim claim,
        string userId,
        string displayName,
        CancellationToken cancellationToken);

    Task<RemotePartyQuestState> RemoveOfficerAsync(
        PartySyncClaim claim,
        string userId,
        CancellationToken cancellationToken);

    Task<RemotePartyQuestState> KickMemberAsync(
        PartySyncClaim claim,
        string userId,
        string displayName,
        string? reason,
        CancellationToken cancellationToken);

    Task<RemotePartyQuestState> UnkickMemberAsync(
        PartySyncClaim claim,
        string userId,
        CancellationToken cancellationToken);

    Task<RemotePartyQuestState> UpdateSettingsAsync(
        PartySyncClaim claim,
        PartySyncSettings settings,
        CancellationToken cancellationToken);
}

public sealed record PartySyncClaim(
    string PartyId,
    string UserId,
    string DisplayName,
    string? LeaderId,
    string ProofVersion = "local-claim-v1");

public sealed record RemotePartyDataSnapshot(
    string? PartySnapshotJson,
    string? CronHistoryJson,
    DateTimeOffset? UpdatedAtUtc,
    IReadOnlyList<PartyQuestQueueEntry>? QuestQueue = null,
    IReadOnlyList<PartyQuestPoolEntry>? QuestPool = null,
    IReadOnlyList<PartyRecentlyCompletedQuest>? RecentlyCompleted = null,
    PartySyncManagementState? Management = null);

public sealed record RemotePartyQuestState(
    DateTimeOffset? UpdatedAtUtc,
    IReadOnlyList<PartyQuestQueueEntry>? QuestQueue = null,
    IReadOnlyList<PartyQuestPoolEntry>? QuestPool = null,
    IReadOnlyList<PartyRecentlyCompletedQuest>? RecentlyCompleted = null,
    PartySyncManagementState? Management = null);
