using Habitica.Application.Notifications;

namespace Habitica.WebApp.State;

public sealed class TaskReminderController : IDisposable
{
    private readonly IAppSessionController _session;
    private readonly TodoWorkspacePresentation _todo;
    private readonly ITaskReminderClient _client;
    private readonly INotificationLocalStore _store;
    private string? _account;
    private long _version, _generation;
    private TodoWorkspaceSelection? _source;
    private PushContext? _context;
    private ReminderLocalState? _local;
    public TaskReminderController(IAppSessionController session, TodoWorkspacePresentation todo, ITaskReminderClient client, INotificationLocalStore store)
    { _session=session;_todo=todo;_client=client;_store=store;_account=Account;_version=client.ContextVersion;session.Changed+=Invalidate;todo.Changed+=Invalidate; }
    public event Action? Changed;
    public TaskReminderState? State { get; private set; }
    public ReminderPreview? Preview { get; private set; }
    public ReminderReceipt? Receipt { get; private set; }
    public PendingReminderAction? Pending=>_local?.Pending;
    public string? Message { get; private set; }
    public bool IsBusy { get; private set; }
    public bool CanPrepare=>CanChange&&State is{Available:true,Push.Enabled:true}&&State.Push.Devices.Any(device=>device.State=="active");
    public bool CanChange=>!IsBusy&&Pending is null&&State is not null&&_local is not null&&_context is not null&&_source==_todo.Selection&&State.WorkspaceRevision==_todo.Revision&&!_todo.IsReadOnly&&!_todo.Commands.HasPendingIntent;
    public bool CanAccept=>!IsBusy&&Preview is {Accepted:false} preview&&preview.ExpiresAtUtc>DateTimeOffset.UtcNow&&Pending?.CommandId==preview.CommandId&&_source==_todo.Selection&&State?.WorkspaceRevision==_todo.Revision&&!_todo.IsReadOnly;
    private string? Account=>_session.State.IsAuthenticated&&Guid.TryParse(_session.State.UserId,out var id)&&id!=Guid.Empty?id.ToString("D"):null;
    private bool Current(long generation)=>generation==_generation&&Account==_account&&_client.ContextVersion==_version&&_todo.Selection==_source;
    private void Invalidate()
    {
        if(Account==_account&&_version==_client.ContextVersion&&_source==_todo.Selection)return;
        _account=Account;_version=_client.ContextVersion;_source=_todo.Selection;_generation++;
        State=null;Preview=null;Receipt=null;_local=null;_context=null;IsBusy=false;Message=null;Changed?.Invoke();
    }
    public async Task LoadAsync(Guid taskId,int offset=0)
    {
        if(IsBusy)return;Invalidate();var generation=_generation;
        if(_account is null||_source is not{Source:TodoWorkspaceSource.Cloud}||_todo.Snapshot is null){Message="Select and load the cloud workspace in Todo Sync. Browser cues remain available separately.";Changed?.Invoke();return;}
        IsBusy=true;Preview=null;Receipt=null;Message=null;Changed?.Invoke();
        try
        {
            var result=await _client.ReadTaskRemindersAsync(taskId,offset,default);if(!Current(generation))return;
            if(!result.Succeeded||result.Value is not {} state||state.WorkspaceId!=_source.WorkspaceId){State=null;_local=null;Message=result.Message;return;}
            State=state;
            if(state.Push is {} push){var context=new PushContext(push.ProfileId,push.ConnectionId);var local=await _store.ReadReminderAsync(_account,context,state.WorkspaceId);if(!Current(generation))return;_context=context;_local=local;}
            else{_context=null;_local=null;}
        }
        catch{if(Current(generation)){State=null;_local=null;Message="Reminder status or local original-command storage is unavailable. Reload before changing schedules.";}}
        finally{if(Current(generation)){IsBusy=false;Changed?.Invoke();}}
    }
    private async Task PersistAsync(ReminderLocalState next,long generation)
    {
        var old=_local??throw new InvalidOperationException("Load original-command storage first.");
        if(!Current(generation))throw new InvalidOperationException("Reminder context changed.");
        var saved=await _store.WriteReminderAsync(_account!,_context!,_source!.WorkspaceId,old.Revision,next);
        if(!Current(generation))return;
        if(!saved){_local=null;Preview=null;throw new InvalidOperationException("Another tab changed the original command. Reload before continuing.");}
        _local=next;
    }
    public Task PrepareAsync(ReminderIntent intent)
    {
        if(!(intent.Operation=="cancel"?CanChange:CanPrepare)||State?.TaskId!=intent.TaskId||!ReminderIdentity.Valid(intent))return Task.CompletedTask;
        return RunAsync(async generation=>
        {
            var pending=new PendingReminderAction(State.WorkspaceId,new(Guid.NewGuid(),State.WorkspaceRevision,intent),null);
            await PersistAsync(new(_local!.Revision+1,pending),generation);if(Current(generation))await PrepareOriginalAsync(generation);
        });
    }
    public Task RetryPreviewAsync()=>RunAsync(PrepareOriginalAsync);
    private async Task PrepareOriginalAsync(long generation)
    {
        if(Pending is not {} pending)return;
        var result=await _client.PrepareReminderAsync(pending.Preparation,default);if(!Current(generation))return;
        if(!result.Succeeded||result.Value is not {} preview){Message=result.Message+" Original request retained; retry its preview or recover its command.";return;}
        if(preview.RequestId!=pending.Preparation.RequestId||pending.CommandId is {} command&&command!=preview.CommandId)throw new InvalidOperationException("Original preview identity changed.");
        await PersistAsync(new(_local!.Revision+1,pending with{CommandId=preview.CommandId}),generation);if(!Current(generation))return;
        if(preview.Accepted&&preview.Receipt is {} receipt)await CompleteAsync(receipt,generation);
        else{Preview=preview;Message="Review the exact schedule below. Acceptance saves configuration; notification delivery remains best effort.";}
    }
    public Task AcceptAsync()
    {
        if(!CanAccept)return Task.CompletedTask;
        var preview=Preview!;
        return RunAsync(async generation=>{await PersistAsync(new(_local!.Revision+1,Pending! with{AcceptanceAttempted=true}),generation);if(!Current(generation))return;var result=await _client.AcceptReminderAsync(preview,default);if(!Current(generation))return;Preview=null;if(result.Succeeded&&result.Value?.Receipt is {} receipt)await CompleteAsync(receipt,generation);else Message=result.Message+" Recover the original command before another schedule change.";});
    }
    public Task DiscardUnsubmittedAsync()=>RunAsync(async generation=>
    {
        if(Pending is not{AcceptanceAttempted:false})return;
        await PersistAsync(new(_local!.Revision+1,null),generation);if(Current(generation)){Preview=null;Message="Unsubmitted local preview discarded. No acceptance was attempted; tasks and schedules are unchanged.";}
    });
    public Task RecoverAsync()=>RunAsync(async generation=>
    {
        if(Pending?.CommandId is not {} command){Message="Retry the original preview to recover its command identity.";return;}
        var result=await _client.RecoverReminderAsync(command,default);if(!Current(generation))return;
        Preview=null;
        if(result.Succeeded&&result.Value?.Receipt is {} receipt)await CompleteAsync(receipt,generation);
        else Message=result.Succeeded&&result.Value?.Accepted==false?"Original command is not accepted. Retry the original preview; an expired preview cannot be accepted.":result.Message+" Absence is not proof an uncertain write failed.";
    });
    private async Task CompleteAsync(ReminderReceipt receipt,long generation)
    {
        if(Pending is not {} p||receipt.WorkspaceId!=p.WorkspaceId||receipt.RequestId!=p.Preparation.RequestId||receipt.CommandId!=p.CommandId||!ReminderIdentity.Same(receipt.Intent,p.Preparation.Intent))throw new InvalidOperationException("Receipt does not match the original intent.");
        await PersistAsync(new(_local!.Revision+1,null),generation);if(!Current(generation))return;
        Receipt=receipt;Preview=null;State=null;Message="Original configuration receipt confirmed. Reload schedule status. Provider acceptance never proves shown or read.";
    }
    private async Task RunAsync(Func<long,Task> action)
    {
        if(IsBusy||_local is null||_context is null||!Current(_generation))return;var generation=_generation;IsBusy=true;Message=null;Changed?.Invoke();
        try{await action(generation);}catch(InvalidOperationException error){if(Current(generation))Message=error.Message;}catch{if(Current(generation))Message="Original reminder request is unconfirmed. Recover before creating another request.";}
        finally{if(Current(generation)){IsBusy=false;Changed?.Invoke();}}
    }
    public void Dispose(){_session.Changed-=Invalidate;_todo.Changed-=Invalidate;}
}
