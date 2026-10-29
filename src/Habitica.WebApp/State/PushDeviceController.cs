using Habitica.Application.Notifications;

namespace Habitica.WebApp.State;

public sealed class PushDeviceController : IDisposable
{
    private readonly IAppSessionController _session;
    private readonly IPushSubscriptionClient _client;
    private readonly IPushBrowser _browser;
    private readonly INotificationLocalStore _store;
    private string? _account;
    private long _clientVersion,_generation;
    private PushBrowserLocalState? _local;
    public PushDeviceController(IAppSessionController session,IPushSubscriptionClient client,IPushBrowser browser,INotificationLocalStore store)
    { _session=session;_client=client;_browser=browser;_store=store;_account=Account;_clientVersion=client.ContextVersion;session.Changed+=SessionChanged; }
    public event Action? Changed;
    public PushStatus? Status { get; private set; }
    public BrowserPushCapability Capability { get; private set; }=new("unprepared","unsupported");
    public BrowserPushCurrent? Current { get; private set; }
    public bool IsBusy { get; private set; }
    public string? Message { get; private set; }
    public PushBrowserBinding? Binding=>_local?.Binding;
    public PendingPushAction? Pending=>_local?.Pending;
    public bool StorageAvailable=>_local is not null;
    public bool CanChange=>Account is not null&&_session.HasChatGptConnection&&Status is not null&&_local is not null&&!IsBusy&&Pending is null;
    public bool CanRegister=>CanChange&&Status!.Available&&Status.Enabled&&Status.ConsentEpoch is not null&&Capability.State=="available"&&Capability.Permission!="denied";
    public bool CanRemoveLocal=>CanChange&&Binding is {} binding&&Current is{Exists:true,Fingerprint:{} fingerprint}&&binding.Fingerprint==fingerprint;
    public bool ThisDevice(PushDevice device)=>Binding is {} binding&&binding.DeviceId==device.DeviceId&&binding.SubscriptionId==device.SubscriptionId&&Current?.Fingerprint==binding.Fingerprint;
    private string? Account=>_session.State.IsAuthenticated&&Guid.TryParse(_session.State.UserId,out var id)&&id!=Guid.Empty?id.ToString("D"):null;
    private PushContext Context=>Status is {} status?new(status.ProfileId,status.ConnectionId):throw new InvalidOperationException("Load the current push context first.");
    private bool CurrentScope(string account,long generation,long version)=>Account==account&&_generation==generation&&_client.ContextVersion==version;
    private void SessionChanged()
    {
        if(Account==_account&&_clientVersion==_client.ContextVersion&&_session.HasChatGptConnection)return;
        _account=Account;_clientVersion=_client.ContextVersion;_generation++;Status=null;Current=null;_local=null;IsBusy=false;Message=null;Changed?.Invoke();
    }
    public async Task RefreshAsync()
    {
        if(IsBusy)return;var account=Account;var generation=_generation;var version=_client.ContextVersion;
        if(account is null||!_session.HasChatGptConnection){Status=null;_local=null;Message="Sign in and connect the integration in Todo Sync before managing account push.";Changed?.Invoke();return;}
        IsBusy=true;Message=null;Changed?.Invoke();
        try
        {
            var capability=await _browser.PrepareAsync(default);var current=await _browser.ReadCurrentAsync(default);var status=await _client.ReadPushStatusAsync(default);
            if(!CurrentScope(account,generation,version))return;Capability=capability;Current=current;
            if(!status.Succeeded||status.Value is null){Status=null;_local=null;Message=status.Message;return;}
            Status=status.Value;
            try{var local=await _store.ReadDeviceAsync(account,Context);if(CurrentScope(account,generation,version))_local=local;}
            catch{if(CurrentScope(account,generation,version)){_local=null;Message="Local device receipt storage is unavailable. No subscription or server preference was changed.";}}
        }
        catch{if(CurrentScope(account,generation,version)){Status=null;_local=null;Message="Push setup is unavailable. Reload status; no permission is requested on load.";}}
        finally{if(CurrentScope(account,generation,version)){IsBusy=false;Changed?.Invoke();}}
    }
    private async Task<PushBrowserLocalState> PersistAsync(string account,PushContext context,PushBrowserLocalState state)
    {
        var old=_local??throw new InvalidOperationException("Reload local receipt ownership first.");
        var generation=_generation;var version=_client.ContextVersion;
        if(!await _store.WriteDeviceAsync(account,context,old.Revision,state)){if(CurrentScope(account,generation,version))_local=null;throw new InvalidOperationException("Another tab changed device receipt ownership. Reload before another action.");}
        if(CurrentScope(account,generation,version)&&Status?.ProfileId==context.ProfileId&&Status.ConnectionId==context.ConnectionId)_local=state;
        return state;
    }
    private async Task DiscardUnsentAsync(string account,PushContext context,PushBrowserLocalState state)
    {
        try{await _store.WriteDeviceAsync(account,context,state.Revision,state with{Revision=state.Revision+1,Pending=null});}catch{/* Keep an unavailable original reference rather than overwrite another tab. */}
    }
    public Task SetConsentAsync(bool enabled,bool acknowledged)
    {
        if(!CanChange||enabled&&(!acknowledged||Status?.Available!=true))return Task.CompletedTask;
        return RunAsync(async(account,context,generation,version)=>
    {
        var pending=new PendingPushAction(Guid.NewGuid(),"consent",Status!.ConsentEpoch,enabled);
        var state=await PersistAsync(account,context,_local! with{Revision=_local!.Revision+1,Pending=pending});
        if(!CurrentScope(account,generation,version)){await DiscardUnsentAsync(account,context,state);return;}
        var result=await _client.SetPushConsentAsync(new(context,pending.RequestId,pending.ExpectedConsentEpoch,enabled),default);
        if(CurrentScope(account,generation,version))await ApplyAsync(account,context,pending,result);
    });}
    public async Task RegisterAsync(bool acknowledged)
    {
        if(!CanRegister||!acknowledged)return;
        var account=Account!;var context=Context;var generation=_generation;var version=_client.ContextVersion;var key=Status!.ApplicationServerKey!;var epoch=Status.ConsentEpoch!.Value;
        IsBusy=true;Message=null;Changed?.Invoke();
        // Call prepared interop immediately in the user click stack, before any storage await.
        var browserRequest=_browser.SubscribeAsync(key,true,default);
        try
        {
            var result=await browserRequest;if(!CurrentScope(account,generation,version))return;
            if(result.State!="subscribed"||result.Subscription is null||result.Fingerprint is null){Message="Browser setup: "+result.State+". No server subscription was saved.";return;}
            var own=_local?.Binding is {} binding&&binding.Fingerprint==result.Fingerprint?Status.Devices.SingleOrDefault(d=>d.DeviceId==binding.DeviceId):null;
            var pending=new PendingPushAction(Guid.NewGuid(),"subscribe",null,null,own?.DeviceId??Guid.NewGuid(),own?.Revision??0,epoch,Fingerprint:result.Fingerprint);
            var state=await PersistAsync(account,context,_local! with{Revision=_local!.Revision+1,Pending=pending});
            if(!CurrentScope(account,generation,version)){await DiscardUnsentAsync(account,context,state);return;}
            var saved=await _client.SubscribePushAsync(new(context,pending.RequestId,pending.DeviceId!.Value,pending.ExpectedRevision!.Value,epoch,result.Subscription),default);
            if(CurrentScope(account,generation,version))await ApplyAsync(account,context,pending,saved);
        }
        catch(InvalidOperationException error){if(CurrentScope(account,generation,version))Message=error.Message+" A browser subscription can exist without an acknowledged server binding.";}
        catch{if(CurrentScope(account,generation,version))Message="Device setup is unconfirmed. Check the original request before another upload.";}
        finally{if(CurrentScope(account,generation,version)){IsBusy=false;Changed?.Invoke();}}
    }
    public Task RevokeDeviceAsync(Guid deviceId)
    {
        if(!CanChange)return Task.CompletedTask;
        return RunAsync(async(account,context,generation,version)=>
    {
        if(Status?.ConsentEpoch is not {} epoch)return;
        var device=Status.Devices.SingleOrDefault(d=>d.DeviceId==deviceId&&d.State=="active");if(device is null)return;
        var pending=new PendingPushAction(Guid.NewGuid(),"unsubscribe",null,null,device.DeviceId,device.Revision,epoch,device.SubscriptionId);
        var state=await PersistAsync(account,context,_local! with{Revision=_local!.Revision+1,Pending=pending});
        if(!CurrentScope(account,generation,version)){await DiscardUnsentAsync(account,context,state);return;}
        var result=await _client.UnsubscribePushAsync(new(context,pending.RequestId,device.DeviceId,device.Revision,epoch,device.SubscriptionId),default);
        if(CurrentScope(account,generation,version))await ApplyAsync(account,context,pending,result);
    });}
    public async Task RemoveLocalAsync()
    {
        if(!CanRemoveLocal)return;var account=Account!;var generation=_generation;var version=_client.ContextVersion;var fingerprint=Binding!.Fingerprint;
        IsBusy=true;Changed?.Invoke();var removal=_browser.UnsubscribeAsync(fingerprint,default);
        try{var result=await removal;if(CurrentScope(account,generation,version)){var current=await _browser.ReadCurrentAsync(default);if(CurrentScope(account,generation,version)){Current=current;Message="Local browser removal: "+result.State+". Server device consent and receipts have separate controls above.";}}}
        catch{if(CurrentScope(account,generation,version))Message="Local subscription removal is unconfirmed. Reload browser state before trying again.";}
        finally{if(CurrentScope(account,generation,version)){IsBusy=false;Changed?.Invoke();}}
    }
    public async Task RetryOriginalAsync(bool acknowledged)
    {
        if(IsBusy||Pending is not {} pending||Status is null||_local is null||Account is not {} account)return;
        var context=Context;var generation=_generation;var version=_client.ContextVersion;
        if(pending.Operation=="subscribe"&&(!acknowledged||Status.ApplicationServerKey is null))return;
        IsBusy=true;Message=null;Changed?.Invoke();
        // Retrying a subscription still needs a fresh, direct browser gesture; material is never stored.
        var browser=pending.Operation=="subscribe"?_browser.SubscribeAsync(Status.ApplicationServerKey!,true,default):null;
        try
        {
            PushResult<PushReceipt> result;
            if(browser is not null)
            {
                var material=await browser;if(!CurrentScope(account,generation,version))return;
                if(material.State!="subscribed"||material.Subscription is null||material.Fingerprint!=pending.Fingerprint){Message="The browser subscription changed or permission is unavailable. Keep the original request and recover its receipt.";return;}
                result=await _client.SubscribePushAsync(new(context,pending.RequestId,pending.DeviceId!.Value,pending.ExpectedRevision!.Value,pending.ConsentEpoch!.Value,material.Subscription),default);
            }
            else if(pending.Operation=="consent")result=await _client.SetPushConsentAsync(new(context,pending.RequestId,pending.ExpectedConsentEpoch,pending.Enabled!.Value),default);
            else result=await _client.UnsubscribePushAsync(new(context,pending.RequestId,pending.DeviceId!.Value,pending.ExpectedRevision!.Value,pending.ConsentEpoch!.Value,pending.SubscriptionId!.Value),default);
            if(CurrentScope(account,generation,version))await ApplyAsync(account,context,pending,result);
        }
        catch{if(CurrentScope(account,generation,version))Message="The original device request remains unconfirmed. No replacement ID was created.";}
        finally{if(CurrentScope(account,generation,version)){IsBusy=false;Changed?.Invoke();}}
    }
    public Task RecoverAsync()=>RunAsync(async(account,context,generation,version)=>
    {
        if(Pending is not {} pending)return;var result=await _client.RecoverPushReceiptAsync(pending.RequestId,default);if(CurrentScope(account,generation,version))await ApplyAsync(account,context,pending,result);
    },allowPending:true);
    private async Task ApplyAsync(string account,PushContext context,PendingPushAction pending,PushResult<PushReceipt> result)
    {
        if(!result.Succeeded||result.Value is not {} receipt){Message=result.Message+" Keep original request "+pending.RequestId.ToString("D")+"; absence does not prove an uncertain write failed.";return;}
        if(receipt.ProfileId!=context.ProfileId||receipt.ConnectionId!=context.ConnectionId||receipt.RequestId!=pending.RequestId||receipt.Operation!=pending.Operation||!receipt.Saved
            ||pending.Operation=="consent"&&receipt.Enabled!=pending.Enabled||pending.Operation!="consent"&&(receipt.DeviceId!=pending.DeviceId||receipt.Revision!=pending.ExpectedRevision+1||pending.SubscriptionId is {} subscription&&receipt.SubscriptionId!=subscription))
        {Message="Original device receipt did not match the exact pending action. No replacement action was created.";return;}
        var generation=_generation;var version=_client.ContextVersion;
        var binding=_local!.Binding;
        if(pending.Operation=="subscribe"&&receipt.SubscriptionId is {} sid&&receipt.Revision is {} revision&&pending.Fingerprint is {} fingerprint)binding=new(receipt.DeviceId!.Value,sid,revision,fingerprint);
        await PersistAsync(account,context,_local with{Revision=_local.Revision+1,Binding=binding,Pending=null});
        if(!CurrentScope(account,generation,version))return;
        Message="Original "+pending.Operation+" receipt confirmed. Browser permission remains controlled by the browser/system.";
        var status=await _client.ReadPushStatusAsync(default);if(CurrentScope(account,generation,version)&&status.Succeeded&&status.Value?.ProfileId==context.ProfileId&&status.Value.ConnectionId==context.ConnectionId)Status=status.Value;
    }
    private async Task RunAsync(Func<string,PushContext,long,long,Task> action,bool allowPending=false)
    {
        if(IsBusy||Account is not {} account||Status is null||_local is null||!allowPending&&Pending is not null)return;
        var context=Context;var generation=_generation;var version=_client.ContextVersion;
        IsBusy=true;Message=null;Changed?.Invoke();
        try{await action(account,context,generation,version);}
        catch(InvalidOperationException error){if(CurrentScope(account,generation,version))Message=error.Message;}
        catch{if(CurrentScope(account,generation,version))Message="The original device request is unconfirmed. Recover its receipt before another change.";}
        finally{if(CurrentScope(account,generation,version)){IsBusy=false;Changed?.Invoke();}}
    }
    public void Dispose()=>_session.Changed-=SessionChanged;
}
