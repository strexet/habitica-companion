using System.Text.Json;
using Habitica.Application.Notifications;
using Habitica.Domain.Workspace;
using Microsoft.JSInterop;

namespace Habitica.WebApp.Integration;

public sealed class NotificationLocalStore(IJSRuntime javascript):INotificationLocalStore,IAsyncDisposable
{
    private IJSObjectReference? _module;
    private async Task<IJSObjectReference> Module(CancellationToken token)=>_module??=await javascript.InvokeAsync<IJSObjectReference>("import",token,"./js/storage/indexedDbStorage.js");
    private static string Key(string account,PushContext context,Guid? workspace=null)
    {
        if(!Guid.TryParse(account,out var id)||id==Guid.Empty||context.ConnectionId==Guid.Empty||!System.Text.RegularExpressions.Regex.IsMatch(context.ProfileId,"^prf_[A-Za-z0-9_-]{43}$")||workspace==Guid.Empty)throw new InvalidOperationException("Invalid notification context.");
        return $"notification-state-v1:{id:D}:{context.ProfileId}:{context.ConnectionId:D}:"+(workspace?.ToString("D")??"device");
    }
    private static bool Fingerprint(string? value)=>value is not null&&System.Text.RegularExpressions.Regex.IsMatch(value,"^[A-Za-z0-9_-]{43}$");
    private static bool Valid(PushBrowserLocalState value)=>value.Revision is >0 and <=WorkspaceDocument.MaxRevision
        &&(value.Binding is null||value.Binding.DeviceId!=Guid.Empty&&value.Binding.SubscriptionId!=Guid.Empty&&value.Binding.Revision is >0 and <=WorkspaceDocument.MaxRevision&&Fingerprint(value.Binding.Fingerprint))
        &&(value.Pending is not {} pending||pending.RequestId!=Guid.Empty&&(pending.Operation=="consent"?pending.Enabled is not null&&pending.DeviceId is null&&pending.ExpectedRevision is null&&pending.ConsentEpoch is null&&pending.SubscriptionId is null&&pending.Fingerprint is null&&pending.ExpectedConsentEpoch!=Guid.Empty
            :pending.Operation is "subscribe" or "unsubscribe"&&pending.Enabled is null&&pending.ExpectedConsentEpoch is null&&pending.DeviceId is {} device&&device!=Guid.Empty&&pending.ExpectedRevision is >=0 and <WorkspaceDocument.MaxRevision
                &&pending.ConsentEpoch is {} epoch&&epoch!=Guid.Empty&&(pending.Fingerprint is null||Fingerprint(pending.Fingerprint))&&(pending.Operation=="subscribe"?pending.SubscriptionId is null:pending.SubscriptionId is {} subscription&&subscription!=Guid.Empty)));
    private static bool Valid(ReminderLocalState value)=>value.Revision is >0 and <=WorkspaceDocument.MaxRevision&&(value.Pending is not {} p||p.WorkspaceId!=Guid.Empty&&p.Preparation.RequestId!=Guid.Empty&&p.Preparation.ExpectedWorkspaceRevision is >0 and <=WorkspaceDocument.MaxRevision&&ReminderIdentity.Valid(p.Preparation.Intent)&&p.CommandId!=Guid.Empty);
    public async Task<PushBrowserLocalState> ReadDeviceAsync(string accountId,PushContext context,CancellationToken token=default)
    {
        var module=await Module(token);var json=await module.InvokeAsync<string?>("getNotificationState",token,Key(accountId,context));
        if(json is null)return new(0,null,null);var state=JsonSerializer.Deserialize<PushBrowserLocalState>(json,ReminderIdentity.Json);if(state is null||!Valid(state))throw new InvalidOperationException("Device receipt state is unavailable.");return state;
    }
    public async Task<bool> WriteDeviceAsync(string accountId,PushContext context,long expectedRevision,PushBrowserLocalState state,CancellationToken token=default)
    {
        if(!Valid(state)||state.Revision!=expectedRevision+1)return false;return await(await Module(token)).InvokeAsync<bool>("commitNotificationState",token,Key(accountId,context),expectedRevision,JsonSerializer.Serialize(state,ReminderIdentity.Json));
    }
    public async Task<ReminderLocalState> ReadReminderAsync(string accountId,PushContext context,Guid workspaceId,CancellationToken token=default)
    {
        var json=await(await Module(token)).InvokeAsync<string?>("getNotificationState",token,Key(accountId,context,workspaceId));if(json is null)return new(0,null);
        var state=JsonSerializer.Deserialize<ReminderLocalState>(json,ReminderIdentity.Json);if(state is null||!Valid(state)||state.Pending is {} p&&p.WorkspaceId!=workspaceId)throw new InvalidOperationException("Reminder receipt state is unavailable.");return state;
    }
    public async Task<bool> WriteReminderAsync(string accountId,PushContext context,Guid workspaceId,long expectedRevision,ReminderLocalState state,CancellationToken token=default)
    {
        if(!Valid(state)||state.Revision!=expectedRevision+1||state.Pending is {} p&&p.WorkspaceId!=workspaceId)return false;
        return await(await Module(token)).InvokeAsync<bool>("commitNotificationState",token,Key(accountId,context,workspaceId),expectedRevision,JsonSerializer.Serialize(state,ReminderIdentity.Json));
    }
    public async Task ClearAsync(CancellationToken token=default)=>await(await Module(token)).InvokeVoidAsync("clearNotificationStates",token);
    public async ValueTask DisposeAsync(){if(_module is not null){try{await _module.DisposeAsync();}catch(JSDisconnectedException){} _module=null;}}
}
