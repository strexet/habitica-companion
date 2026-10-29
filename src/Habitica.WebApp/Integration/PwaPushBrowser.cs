using Habitica.Application.Notifications;
using Microsoft.JSInterop;

namespace Habitica.WebApp.Integration;

public sealed class PwaPushBrowser(IJSRuntime javascript) : IPushBrowser, IAsyncDisposable
{
    private IJSObjectReference? _module;
    public async Task<BrowserPushCapability> PrepareAsync(CancellationToken cancellationToken)
    {
        try
        {
            _module ??= await javascript.InvokeAsync<IJSObjectReference>("import", cancellationToken, "./js/pwaPush.js");
            return await _module.InvokeAsync<BrowserPushCapability>("capability", cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (JSException) { return new("unsupported", "unsupported"); }
    }
    public Task<BrowserPushCurrent> ReadCurrentAsync(CancellationToken cancellationToken) => InvokeAsync("current", new BrowserPushCurrent("unprepared", "unsupported", false, null), cancellationToken);
    // Prepare before rendering an enabled action. Never await a module import in the click path.
    public Task<BrowserPushResult> SubscribeAsync(string applicationServerKey, bool reminderConsent, CancellationToken cancellationToken) => InvokeAsync("subscribe", new BrowserPushResult("unprepared"), cancellationToken, applicationServerKey, reminderConsent);
    public Task<BrowserPushResult> UnsubscribeAsync(string expectedFingerprint, CancellationToken cancellationToken) => InvokeAsync("unsubscribe", new BrowserPushResult("unprepared"), cancellationToken, expectedFingerprint);
    private async Task<T> InvokeAsync<T>(string name, T unavailable, CancellationToken cancellationToken, params object?[] args)
    {
        if (_module is null) return unavailable;
        try { return await _module.InvokeAsync<T>(name, cancellationToken, args); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (JSException) { return unavailable; }
    }
    public async ValueTask DisposeAsync()
    {
        if (_module is not null) { try { await _module.DisposeAsync(); } catch (JSDisconnectedException) { } _module = null; }
    }
}
