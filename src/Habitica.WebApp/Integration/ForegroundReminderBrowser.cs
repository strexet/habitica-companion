using Habitica.Application.Notifications;
using Microsoft.JSInterop;

namespace Habitica.WebApp.Integration;

public sealed class ForegroundReminderBrowser(IJSRuntime javascript) : IForegroundReminderBrowser, IAsyncDisposable
{
    private IJSObjectReference? _module;
    private DotNetObjectReference<ForegroundReminderBrowser>? _callback;
    public event Action<ForegroundReminderState>? Changed;
    public async Task PrepareAsync(CancellationToken token = default)
    {
        if (_module is not null) return;
        _module = await javascript.InvokeAsync<IJSObjectReference>("import", token, "./js/foregroundReminders.js");
        _callback = DotNetObjectReference.Create(this);
        try { await _module.InvokeVoidAsync("prepare", token, _callback); }
        catch { _callback.Dispose();_callback=null;_module=null;throw; }
    }
    [JSInvokable] public void Publish(ForegroundReminderState state) => Changed?.Invoke(state);
    public Task<ForegroundReminderState> BindAsync(ForegroundReminderContext? context, IReadOnlyList<ForegroundReminderSchedule> cues, long version, CancellationToken token = default) => Invoke("bind", token, context, cues, version);
    public Task<ForegroundReminderState> ConfigureAsync(string kind, bool enabled, long version, CancellationToken token = default) => Invoke("configure", token, kind, enabled, version);
    // The module and IndexedDB prerequisite are prepared before enabling this click action.
    public Task<ForegroundReminderState> RequestPermissionAsync(long version, CancellationToken token = default) => Invoke("requestPermission", token, version);
    private async Task<ForegroundReminderState> Invoke(string method, CancellationToken token, params object?[] args)
    {
        if (_module is null) throw new InvalidOperationException("Reminder browser is not prepared.");
        return await _module.InvokeAsync<ForegroundReminderState>(method, token, args);
    }
    public async Task ClearAsync(CancellationToken token = default)
    {
        await PrepareAsync(token);
        await _module!.InvokeVoidAsync("clear", token);
    }
    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try { await _module.InvokeVoidAsync("stop"); await _module.DisposeAsync(); }
            catch (JSException) { }
        }
        _callback?.Dispose(); _module = null; _callback = null;
    }
}
