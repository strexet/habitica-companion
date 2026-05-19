using Habitica.Domain.Auth;
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
        HabiticaCredentials credentials,
        string partyId,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyDataSnapshot?>(
            "downloadPartyData",
            cancellationToken,
            credentials.UserId,
            credentials.ApiToken,
            partyId);
    }

    public async Task UploadAsync(
        HabiticaCredentials credentials,
        string partyId,
        string partySnapshotJson,
        string cronHistoryJson,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync(
            "uploadPartyData",
            cancellationToken,
            credentials.UserId,
            credentials.ApiToken,
            partyId,
            partySnapshotJson,
            cronHistoryJson);
    }

    public async Task<RemotePartyQuestState> PublishQuestPoolAsync(
        HabiticaCredentials credentials,
        string partyId,
        IReadOnlyList<PartyQuestPoolEntry> entries,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyQuestState>(
            "publishQuestPool",
            cancellationToken,
            credentials.UserId,
            credentials.ApiToken,
            partyId,
            entries);
    }

    public async Task<RemotePartyQuestState> AddQuestQueueItemAsync(
        HabiticaCredentials credentials,
        string partyId,
        PartyQuestPoolEntry entry,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyQuestState>(
            "addQuestQueueItem",
            cancellationToken,
            credentials.UserId,
            credentials.ApiToken,
            partyId,
            entry);
    }

    public async Task<RemotePartyQuestState> ToggleQuestVoteAsync(
        HabiticaCredentials credentials,
        string partyId,
        string queueItemId,
        string voterDisplayName,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyQuestState>(
            "toggleQuestVote",
            cancellationToken,
            credentials.UserId,
            credentials.ApiToken,
            partyId,
            queueItemId,
            voterDisplayName);
    }

    public async Task<RemotePartyQuestState> RemoveQuestQueueItemAsync(
        HabiticaCredentials credentials,
        string partyId,
        string queueItemId,
        int version,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemotePartyQuestState>(
            "removeQuestQueueItem",
            cancellationToken,
            credentials.UserId,
            credentials.ApiToken,
            partyId,
            queueItemId,
            version);
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
