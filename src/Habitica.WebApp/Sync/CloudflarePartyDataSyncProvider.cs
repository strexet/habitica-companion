using Habitica.Domain.Party;
using Microsoft.JSInterop;

namespace Habitica.WebApp.Sync;

public sealed class CloudflarePartyDataSyncProvider : IRemotePartyDataSyncProvider, IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask;

    public CloudflarePartyDataSyncProvider(IJSRuntime jsRuntime)
    {
        _moduleTask = new Lazy<Task<IJSObjectReference>>(
            () => jsRuntime.InvokeAsync<IJSObjectReference>(
                "import",
                "./js/sync/cloudflarePartySync.js").AsTask());
    }

    public async Task<RemotePartyDataSnapshot?> DownloadAsync(
        PartySyncClaim claim,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyDataSnapshot?>(
            "downloadPartyData",
            cancellationToken,
            claim);
    }

    public async Task UploadAsync(
        PartySyncClaim claim,
        string partySnapshotJson,
        string cronHistoryJson,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync(
            "uploadPartyData",
            cancellationToken,
            claim,
            partySnapshotJson,
            cronHistoryJson);
    }

    public async Task<RemotePartyQuestState> PublishQuestPoolAsync(
        PartySyncClaim claim,
        IReadOnlyList<PartyQuestPoolEntry> entries,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyQuestState>(
            "publishQuestPool",
            cancellationToken,
            claim,
            entries);
    }

    public async Task<RemotePartyQuestState> AddQuestQueueItemAsync(
        PartySyncClaim claim,
        PartyQuestPoolEntry entry,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyQuestState>(
            "addQuestQueueItem",
            cancellationToken,
            claim,
            entry);
    }

    public async Task<RemotePartyQuestState> ToggleQuestVoteAsync(
        PartySyncClaim claim,
        string queueItemId,
        string voterDisplayName,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyQuestState>(
            "toggleQuestVote",
            cancellationToken,
            claim,
            queueItemId,
            voterDisplayName);
    }

    public async Task<RemotePartyQuestState> RemoveQuestQueueItemAsync(
        PartySyncClaim claim,
        string queueItemId,
        int version,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyQuestState>(
            "removeQuestQueueItem",
            cancellationToken,
            claim,
            queueItemId,
            version);
    }

    public async Task<RemotePartyQuestState> PinQuestQueueItemAsync(
        PartySyncClaim claim,
        string queueItemId,
        int version,
        bool pinned,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyQuestState>(
            "pinQuestQueueItem",
            cancellationToken,
            claim,
            queueItemId,
            version,
            pinned);
    }

    public async Task<RemotePartyQuestState> SelectQuestQueueItemAsync(
        PartySyncClaim claim,
        string queueItemId,
        int version,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyQuestState>(
            "selectQuestQueueItem",
            cancellationToken,
            claim,
            queueItemId,
            version);
    }

    public async Task<RemotePartyQuestState> SkipQuestQueueItemAsync(
        PartySyncClaim claim,
        string queueItemId,
        int version,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyQuestState>(
            "skipQuestQueueItem",
            cancellationToken,
            claim,
            queueItemId,
            version);
    }

    public async Task<RemotePartyQuestState> ExpireQuestQueueItemAsync(
        PartySyncClaim claim,
        string queueItemId,
        int version,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyQuestState>(
            "expireQuestQueueItem",
            cancellationToken,
            claim,
            queueItemId,
            version);
    }

    public async Task<RemotePartyQuestState> RequeueQuestQueueItemAsync(
        PartySyncClaim claim,
        string queueItemId,
        int version,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyQuestState>(
            "requeueQuestQueueItem",
            cancellationToken,
            claim,
            queueItemId,
            version);
    }

    public async Task<RemotePartyQuestState> MarkQuestCompletedAsync(
        PartySyncClaim claim,
        string queueItemId,
        int version,
        int? participantsCount,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyQuestState>(
            "markQuestCompleted",
            cancellationToken,
            claim,
            queueItemId,
            version,
            participantsCount);
    }

    public async Task<RemotePartyQuestState> RemoveRecentlyCompletedQuestAsync(
        PartySyncClaim claim,
        string questKey,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyQuestState>(
            "removeRecentlyCompletedQuest",
            cancellationToken,
            claim,
            questKey,
            completedAtUtc);
    }

    public async Task<RemotePartyQuestState> InvitePartyAsync(
        PartySyncClaim claim,
        string queueItemId,
        int version,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyQuestState>(
            "invitePartyToQuest",
            cancellationToken,
            claim,
            queueItemId,
            version);
    }

    public async Task<RemotePartyQuestState> ReconcileQuestLifecycleAsync(
        PartySyncClaim claim,
        string queueItemId,
        string questKey,
        string transition,
        int? participantsCount,
        string? completedByDisplayName,
        string? detectionKey,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyQuestState>(
            "reconcileQuestLifecycle",
            cancellationToken,
            claim,
            queueItemId,
            questKey,
            transition,
            participantsCount,
            completedByDisplayName,
            detectionKey);
    }

    public async Task<RemotePartyQuestState> RecordDetectedQuestCompletionAsync(
        PartySyncClaim claim,
        PartyDetectedQuestCompletion completion,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyQuestState>(
            "recordDetectedQuestCompletion",
            cancellationToken,
            claim,
            completion);
    }

    public async Task<RemotePartyQuestState> AssignOfficerAsync(
        PartySyncClaim claim,
        string userId,
        string displayName,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyQuestState>(
            "assignOfficer",
            cancellationToken,
            claim,
            userId,
            displayName);
    }

    public async Task<RemotePartyQuestState> AssignPartyOwnerAsync(
        PartySyncClaim claim,
        string userId,
        string displayName,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyQuestState>(
            "assignPartyOwner",
            cancellationToken,
            claim,
            userId,
            displayName);
    }

    public async Task<RemotePartyQuestState> RemoveOfficerAsync(
        PartySyncClaim claim,
        string userId,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyQuestState>(
            "removeOfficer",
            cancellationToken,
            claim,
            userId);
    }

    public async Task<RemotePartyQuestState> KickMemberAsync(
        PartySyncClaim claim,
        string userId,
        string displayName,
        string? reason,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyQuestState>(
            "kickMember",
            cancellationToken,
            claim,
            userId,
            displayName,
            reason);
    }

    public async Task<RemotePartyQuestState> UnkickMemberAsync(
        PartySyncClaim claim,
        string userId,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyQuestState>(
            "unkickMember",
            cancellationToken,
            claim,
            userId);
    }

    public async Task<RemotePartyQuestState> UpdateSettingsAsync(
        PartySyncClaim claim,
        PartySyncSettings settings,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyQuestState>(
            "updatePartySyncSettings",
            cancellationToken,
            claim,
            settings);
    }

    public async Task<RemotePartyInviteProofActionResult> CreateInviteProofAsync(
        PartySyncClaim claim,
        string label,
        DateTimeOffset? expiresAtUtc,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyInviteProofActionResult>(
            "createPartySyncInviteProof",
            cancellationToken,
            claim,
            label,
            expiresAtUtc);
    }

    public async Task<RemotePartyInviteProofActionResult> RevokeInviteProofAsync(
        PartySyncClaim claim,
        string proofId,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyInviteProofActionResult>(
            "revokePartySyncInviteProof",
            cancellationToken,
            claim,
            proofId);
    }

    public async Task<RemotePartyInviteProofActionResult> RotateInviteProofAsync(
        PartySyncClaim claim,
        string proofId,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyInviteProofActionResult>(
            "rotatePartySyncInviteProof",
            cancellationToken,
            claim,
            proofId);
    }

    public async Task<RemotePartyInviteProofActionResult> RemoveInviteProofAsync(
        PartySyncClaim claim,
        string proofId,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyInviteProofActionResult>(
            "removePartySyncInviteProof",
            cancellationToken,
            claim,
            proofId);
    }

    public async Task<RemotePartyInviteProofActionResult> SetInviteProofModeAsync(
        PartySyncClaim claim,
        bool enabled,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyInviteProofActionResult>(
            "setPartySyncInviteProofMode",
            cancellationToken,
            claim,
            enabled);
    }

    public async Task ActivateInviteProofAsync(
        string partyId,
        string proofId,
        string token,
        string? label,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync(
            "activatePartySyncInviteProof",
            cancellationToken,
            partyId,
            proofId,
            token,
            label);
    }

    public async Task ClearInviteProofAsync(
        string partyId,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync(
            "clearPartySyncInviteProof",
            cancellationToken,
            partyId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_moduleTask.IsValueCreated)
        {
            var module = await _moduleTask.Value;
            await module.DisposeAsync();
        }
    }
}
