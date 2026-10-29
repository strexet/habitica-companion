using Habitica.Application.Notifications;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Microsoft.JSInterop;

namespace Habitica.WebApp.State;

public sealed class ForegroundReminderController : IDisposable
{
    private readonly IAppSessionController _session;
    private readonly TodoWorkspacePresentation _todo;
    private readonly IForegroundReminderBrowser _browser;
    private long _version;
    private bool _ready, _disposed;
    private WorkspaceDocument? _document;
    private string? _key;
    public event Action? Changed;
    public ForegroundReminderState? State { get; private set; }
    public IReadOnlyList<WorkspaceReminderCue> Cues { get; private set; } = [];
    public bool Ready => _ready && _key is not null && State?.ContextKey == _key && State.BindingVersion == _version;
    public string? Error { get; private set; }
    public IReadOnlyList<WorkspaceReminderCue> Due => Ready && State!.InApp ? Cues.Where(c => c.ReviewAfterUtc.ToUnixTimeMilliseconds() <= State.NowMs).ToArray() : [];
    public ForegroundReminderController(IAppSessionController session, TodoWorkspacePresentation todo, IForegroundReminderBrowser browser)
    {
        _session=session;_todo=todo;_browser=browser;
        _session.Changed+=SourceChanged;_todo.Changed+=SourceChanged;_browser.Changed+=Receive;
    }
    public async Task InitializeAsync()
    {
        if(_ready||_disposed)return;
        try {await _browser.PrepareAsync();if(_disposed)return;_ready=true;await RebindAsync();}
        catch(JSException){Error="Browser reminders could not start. Saved review cues remain in Tasks.";Changed?.Invoke();}
    }
    private void SourceChanged()
    {
        if(!_ready||_disposed)return;
        var document=_todo.Snapshot?.Document;
        if(ReferenceEquals(document,_document)&&Key()==_key)return;
        // Rebind starts synchronously, clearing displayed private state before asynchronous storage reads.
        _=RebindSafelyAsync();
    }
    private async Task RebindSafelyAsync()
    {
        try {await RebindAsync();}
        catch(JSException){Error="Browser reminder state is unavailable. Reload Today; no delivery is confirmed.";Changed?.Invoke();}
        catch(InvalidOperationException){Error="Review cue data is unavailable. Reload the selected workspace.";Changed?.Invoke();}
    }
    private string? Key() => _session.State.IsAuthenticated && Guid.TryParse(_session.State.UserId,out var account) && _todo.Snapshot is not null && _todo.Selection is {} source
        ? $"{account:D}:{source.Source.ToString().ToLowerInvariant()}:{source.WorkspaceId:D}" : null;
    private async Task RebindAsync()
    {
        var version=++_version;State=null;Error=null;Cues=[];_key=Key();_document=_todo.Snapshot?.Document;
        ForegroundReminderContext? context=null;
        if(_key is not null&&_document is not null&&_todo.Selection is {} source)
        {
            Cues=WorkspaceReminderCues.Read(_document);
            context=new(Guid.Parse(_session.State.UserId!).ToString("D"),source.WorkspaceId,source.Source.ToString().ToLowerInvariant());
        }
        Changed?.Invoke();
        var state=await _browser.BindAsync(context,Cues.Select(c=>new ForegroundReminderSchedule(c.TaskId,WorkspaceTaskReview.CanonicalUtc(c.ReviewAfterUtc)!,c.ScheduleRevision)).ToArray(),version);
        Receive(state);
    }
    private void Receive(ForegroundReminderState state)
    {
        if(_disposed||state.BindingVersion!=_version||state.ContextKey!=_key)return;
        State=state;Changed?.Invoke();
    }
    public Task SetInAppAsync(bool enabled)=>Act(()=>_browser.ConfigureAsync("inApp",enabled,_version));
    public Task DisableBrowserAsync()=>Act(()=>_browser.ConfigureAsync("browser",false,_version));
    public Task EnableBrowserAsync()=>Act(()=>_browser.RequestPermissionAsync(_version));
    private async Task Act(Func<Task<ForegroundReminderState>> action)
    {
        if(!Ready)return;
        try {Receive(await action());}
        catch(JSException){Error="Reminder preference is unconfirmed. Reload Today before trying again.";Changed?.Invoke();}
    }
    public void Dispose(){_disposed=true;_session.Changed-=SourceChanged;_todo.Changed-=SourceChanged;_browser.Changed-=Receive;}
}
