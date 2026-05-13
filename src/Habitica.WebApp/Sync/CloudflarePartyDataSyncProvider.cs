using Habitica.Domain.Auth;
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

    public async ValueTask DisposeAsync()
    {
        if (_moduleTask.IsValueCreated)
        {
            var module = await _moduleTask.Value;
            await module.DisposeAsync();
        }
    }
}
