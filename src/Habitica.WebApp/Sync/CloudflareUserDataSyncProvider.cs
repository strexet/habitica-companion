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

    public async Task<RemoteUserDataSnapshot?> DownloadSectionAsync(
        HabiticaCredentials credentials,
        string sectionKey,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        return await module.InvokeAsync<RemoteUserDataSnapshot?>(
            "downloadSection",
            cancellationToken,
            credentials.UserId,
            credentials.ApiToken,
            sectionKey);
    }

    public async Task<SectionUploadResult> UploadSectionAsync(
        HabiticaCredentials credentials,
        string sectionKey,
        string plainTextJson,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        var result = await module.InvokeAsync<SectionUploadJsResult>(
            "uploadSection",
            cancellationToken,
            credentials.UserId,
            credentials.ApiToken,
            sectionKey,
            plainTextJson);

        return new SectionUploadResult(
            result.Ok,
            result.Ok ? null : (result.Error ?? "Upload failed"),
            result.PayloadBytes);
    }

    public async Task<IReadOnlyList<RemoteUserDataSnapshot?>> DownloadAllSectionsAsync(
        HabiticaCredentials credentials,
        IReadOnlyList<string> sectionKeys,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        var results = await module.InvokeAsync<DownloadSectionJsResult?[]>(
            "downloadAllSections",
            cancellationToken,
            credentials.UserId,
            credentials.ApiToken,
            sectionKeys);

        return results
            .Select(static result => result is { Ok: true, PlainTextJson: not null }
                ? new RemoteUserDataSnapshot(result.PlainTextJson, result.UpdatedAtUtc)
                : null)
            .ToArray();
    }

    public async Task<IReadOnlyList<string>> ListSectionsAsync(
        HabiticaCredentials credentials,
        CancellationToken cancellationToken)
    {
        var module = await _moduleTask.Value;
        var sections = await module.InvokeAsync<string[]>(
            "listSections",
            cancellationToken,
            credentials.UserId,
            credentials.ApiToken);

        return sections ?? Array.Empty<string>();
    }

    public async ValueTask DisposeAsync()
    {
        if (_moduleTask.IsValueCreated)
        {
            var module = await _moduleTask.Value;
            await module.DisposeAsync();
        }
    }

    private sealed record SectionUploadJsResult(
        bool Ok,
        string? Error,
        int? PayloadBytes);

    private sealed record DownloadSectionJsResult(
        bool Ok,
        string? PlainTextJson,
        DateTimeOffset? UpdatedAtUtc);
}

