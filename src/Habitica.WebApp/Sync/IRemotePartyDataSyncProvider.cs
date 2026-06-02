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

    Task<RemotePartyQuestState> PinQuestQueueItemAsync(
        PartySyncClaim claim,
        string queueItemId,
        int version,
        bool pinned,
        CancellationToken cancellationToken);

    Task<RemotePartyQuestState> SelectQuestQueueItemAsync(
        PartySyncClaim claim,
        string queueItemId,
        int version,
        CancellationToken cancellationToken);

    Task<RemotePartyQuestState> SkipQuestQueueItemAsync(
        PartySyncClaim claim,
        string queueItemId,
        int version,
        CancellationToken cancellationToken);

    Task<RemotePartyQuestState> ExpireQuestQueueItemAsync(
        PartySyncClaim claim,
        string queueItemId,
        int version,
        CancellationToken cancellationToken);

    Task<RemotePartyQuestState> RequeueQuestQueueItemAsync(
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

    Task<RemotePartyQuestState> RemoveRecentlyCompletedQuestAsync(
        PartySyncClaim claim,
        string questKey,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken);

    Task<RemotePartyQuestState> InvitePartyAsync(
        PartySyncClaim claim,
        string queueItemId,
        int version,
        CancellationToken cancellationToken);

    Task<RemotePartyQuestState> ReconcileQuestLifecycleAsync(
        PartySyncClaim claim,
        string queueItemId,
        string questKey,
        string transition,
        int? participantsCount,
        string? completedByDisplayName,
        string? detectionKey,
        CancellationToken cancellationToken);

    Task<RemotePartyQuestState> RecordDetectedQuestCompletionAsync(
        PartySyncClaim claim,
        PartyDetectedQuestCompletion completion,
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

    Task<RemotePartyInviteProofActionResult> CreateInviteProofAsync(
        PartySyncClaim claim,
        string label,
        DateTimeOffset? expiresAtUtc,
        CancellationToken cancellationToken);

    Task<RemotePartyInviteProofActionResult> RevokeInviteProofAsync(
        PartySyncClaim claim,
        string proofId,
        CancellationToken cancellationToken);

    Task<RemotePartyInviteProofActionResult> RotateInviteProofAsync(
        PartySyncClaim claim,
        string proofId,
        CancellationToken cancellationToken);

    Task<RemotePartyInviteProofActionResult> RemoveInviteProofAsync(
        PartySyncClaim claim,
        string proofId,
        CancellationToken cancellationToken);

    Task<RemotePartyInviteProofActionResult> SetInviteProofModeAsync(
        PartySyncClaim claim,
        bool enabled,
        CancellationToken cancellationToken);

    Task ActivateInviteProofAsync(
        string partyId,
        string proofId,
        string token,
        string? label,
        CancellationToken cancellationToken);

    Task ClearInviteProofAsync(
        string partyId,
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

public sealed record RemotePartyInviteProofActionResult(
    DateTimeOffset? UpdatedAtUtc,
    IReadOnlyList<PartyQuestQueueEntry>? QuestQueue = null,
    IReadOnlyList<PartyQuestPoolEntry>? QuestPool = null,
    IReadOnlyList<PartyRecentlyCompletedQuest>? RecentlyCompleted = null,
    PartySyncManagementState? Management = null,
    PartySyncIssuedInviteProof? IssuedInviteProof = null);

public sealed record PartySyncIssuedInviteProof(
    string ProofId,
    string Token,
    string Label,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset? ExpiresAtUtc);

public sealed record PartyDetectedQuestCompletion(
    string QuestKey,
    string QuestName,
    DateTimeOffset? StartedAtUtc,
    int? ParticipantsCount,
    IReadOnlyList<string> RewardSummary,
    string DetectionKey,
    DateTimeOffset CompletedAtUtc);
