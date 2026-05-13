using Habitica.Domain.Auth;
using Microsoft.JSInterop;

namespace Habitica.WebApp.Sync;

public sealed class CloudflareUserDataSyncProvider : IRemoteUserDataSyncProvider, IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask;

    public CloudflareUserDataSyncProvider(IJSRuntime jsRuntime)
    {
        _moduleTask = new Lazy<Task<IJSObjectReference>>(
            () => jsRuntime.InvokeAsync<IJSObjectReference>(
                "import",
                "./js/sync/cloudflareSync.js").AsTask());
    }

    public async Task<RemoteUserDataSnapshot?> DownloadAsync(
        HabiticaCredentials credentials,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemoteUserDataSnapshot?>(
            "downloadData",
            cancellationToken,
            credentials.UserId,
            credentials.ApiToken);
    }

    public async Task UploadAsync(
        HabiticaCredentials credentials,
        string plainTextJson,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync(
            "uploadData",
            cancellationToken,
            credentials.UserId,
            credentials.ApiToken,
            plainTextJson);
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

