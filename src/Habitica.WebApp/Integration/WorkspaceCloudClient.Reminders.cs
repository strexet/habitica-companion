using System.Net.Http.Json;
using System.Text.Json;
using Habitica.Application.Notifications;
using Habitica.Domain.Workspace;

namespace Habitica.WebApp.Integration;

public sealed partial class WorkspaceCloudClient : ITaskReminderClient
{
    long ITaskReminderClient.ContextVersion => ((long)_vault.SessionVersion << 32) | (uint)_generation;
    private static PushResult<T> ReminderUnavailable<T>() => new(false,"Reminder result is unavailable. Recover the original request/command before another change.");
    private static bool ReminderReceiptValid(ReminderReceipt receipt,Guid workspaceId) => receipt.WorkspaceId==workspaceId&&receipt.CommandId!=Guid.Empty&&receipt.RequestId!=Guid.Empty&&receipt.ScheduleId!=Guid.Empty
        &&receipt.Saved&&!receipt.Dispatched&&!receipt.AcceptedByPushService&&receipt.Shown is null&&receipt.Read is null&&receipt.Intent is not null&&ReminderIdentity.Valid(receipt.Intent)
        &&receipt.TaskId==receipt.Intent.TaskId&&receipt.Operation==receipt.Intent.Operation&&receipt.Revision==receipt.Intent.ExpectedRevision+1
        &&(receipt.Intent.ScheduleId is null||receipt.Intent.ScheduleId==receipt.ScheduleId)&&receipt.State==(receipt.Operation=="set"?"active":"cancelled")&&receipt.Scheduled==(receipt.Operation=="set");
    private static bool ReminderStatusValid(ReminderCommandStatus value,Guid workspaceId,Guid commandId) => value.CommandId==commandId
        &&(value.Accepted==true?value.Receipt is {} receipt&&receipt.CommandId==commandId&&ReminderReceiptValid(receipt,workspaceId):value.Receipt is null);
    public Task<PushResult<TaskReminderState>> ReadTaskRemindersAsync(Guid taskId,int offset,CancellationToken token)
    {
        if(taskId==Guid.Empty||offset is <0 or >8192)return Task.FromResult(ReminderUnavailable<TaskReminderState>());
        return ReminderRequestAsync<TaskReminderState>("reminders?taskId="+taskId.ToString("D")+"&offset="+offset,null,value=>ValidReminderRead(value,taskId,offset),token);
    }
    private bool ValidReminderRead(TaskReminderState value,Guid taskId,int offset)
    {
        if(value.TaskId!=taskId||value.Offset!=offset||value.WorkspaceRevision is <1 or >WorkspaceDocument.MaxRevision||value.Shown is not null||value.Read is not null
            ||value.NextOffset is {} next&&next!=offset+50||value.Schedules is not{Length:<=64}||value.Deliveries is not{Length:<=50})return false;
        if(value.Schedules.Select(s=>s.ScheduleId).Distinct().Count()!=value.Schedules.Length||value.Schedules.Any(s=>s.ScheduleId==Guid.Empty||s.TaskId!=taskId||s.Revision is <1 or >WorkspaceDocument.MaxRevision
            ||s.State is not("active" or "cancelled" or "paused" or "finished")||s.Intent is null||!ReminderIdentity.Valid(s.Intent)||s.Intent.TaskId!=taskId||s.Intent.Operation!="set"||s.Reason?.Length is not(>0 and <=128)))return false;
        if(value.Deliveries.Any(d=>d.OccurrenceId==Guid.Empty||d.ScheduleId==Guid.Empty||d.DeviceId==Guid.Empty||d.ScheduleRevision is <1 or >WorkspaceDocument.MaxRevision||d.AtUtc==default||d.AtUtc.Offset!=TimeSpan.Zero
            ||d.CivilDate==default||d.State is not("scheduled" or "dispatched" or "accepted" or "refused" or "unknown" or "expired" or "cancelled" or "retryable")||d.Code?.Length is not(>0 and <=128)||d.Attempt is <0 or >3
            ||d.Attempts is not{Length:<=3}||d.Attempts.Length!=d.Attempt||d.Attempts.Any(a=>a.RequestId==Guid.Empty||a.Attempt is <1 or >3||a.State is not("dispatched" or "accepted" or "refused" or "unknown" or "retryable")||a.Shown is not null||a.Read is not null)))return false;
        return value.Push is null?!value.Available:value.Push.ProfileId==_vault.ProfileId&&value.Push.ConnectionId!=Guid.Empty&&value.Push.ConsentVersion=="web-push-v1"
            &&value.Push.Devices is{Length:<=64}&&value.Push.Devices.All(d=>d.DeviceId!=Guid.Empty&&d.SubscriptionId!=Guid.Empty&&d.Revision is >0 and <=WorkspaceDocument.MaxRevision&&d.State is "active" or "unsubscribed" or "revoked" or "expired")
            &&(!value.Available||value.Push.DispatchAvailable);
    }
    public Task<PushResult<ReminderPreview>> PrepareReminderAsync(ReminderPreparation preparation,CancellationToken token)
    {
        if(preparation.RequestId==Guid.Empty||preparation.ExpectedWorkspaceRevision is <1 or >WorkspaceDocument.MaxRevision||!ReminderIdentity.Valid(preparation.Intent))return Task.FromResult(ReminderUnavailable<ReminderPreview>());
        return ReminderRequestAsync<ReminderPreview>("reminder-api",new{tool="prepare_reminder_command",arguments=new{preparation.RequestId,preparation.ExpectedWorkspaceRevision,Intent=ReminderIdentity.Wire(preparation.Intent)}},value=>
            value.CommandId!=Guid.Empty&&value.RequestId==preparation.RequestId&&(value.Accepted?value.Receipt is {} receipt&&ReminderReceiptValid(receipt,WorkspaceId!.Value)&&ReminderIdentity.Same(receipt.Intent,preparation.Intent)
                :value.WorkspaceId==WorkspaceId&&value.ExpectedWorkspaceRevision==preparation.ExpectedWorkspaceRevision&&value.ScheduleId is {} id&&id!=Guid.Empty
                    &&(preparation.Intent.ScheduleId is null||preparation.Intent.ScheduleId==id)&&value.InputHash==ReminderIdentity.Hash(preparation)&&value.AcceptToken is {} accept&&System.Text.RegularExpressions.Regex.IsMatch(accept,"^[A-Za-z0-9_-]{43}$")
                    &&value.ExpiresAtUtc>DateTimeOffset.UtcNow&&value.ExpiresAtUtc<=DateTimeOffset.UtcNow.AddMinutes(11)&&ReminderIdentity.Same(value.Intent,preparation.Intent)&&value.Receipt is null),token);
    }
    public Task<PushResult<ReminderCommandStatus>> AcceptReminderAsync(ReminderPreview preview,CancellationToken token)
    {
        if(preview.Accepted||preview.Intent is null||preview.CommandId==Guid.Empty||preview.WorkspaceId!=WorkspaceId||preview.ExpiresAtUtc<=DateTimeOffset.UtcNow||preview.AcceptToken is null||preview.InputHash is null)return Task.FromResult(ReminderUnavailable<ReminderCommandStatus>());
        return ReminderRequestAsync<ReminderCommandStatus>("reminder-api",new{tool="accept_reminder_command",arguments=new{preview.CommandId,preview.AcceptToken,expectedHash=preview.InputHash}},value=>ReminderStatusValid(value,WorkspaceId!.Value,preview.CommandId)
            &&value.Accepted==true&&value.Receipt?.RequestId==preview.RequestId&&value.Receipt.ScheduleId==preview.ScheduleId&&ReminderIdentity.Same(value.Receipt.Intent,preview.Intent),token);
    }
    public Task<PushResult<ReminderCommandStatus>> RecoverReminderAsync(Guid commandId,CancellationToken token) => commandId==Guid.Empty?Task.FromResult(ReminderUnavailable<ReminderCommandStatus>())
        :ReminderRequestAsync<ReminderCommandStatus>("reminder-api",new{tool="get_reminder_command",arguments=new{commandId}},value=>ReminderStatusValid(value,WorkspaceId!.Value,commandId),token);
    private async Task<PushResult<T>> ReminderRequestAsync<T>(string path,object? body,Func<T,bool> validate,CancellationToken token) where T:class
    {
        if(_baseUri is null||_token is null||WorkspaceId is null)return ReminderUnavailable<T>();
        var generation=_generation;var vaultVersion=_vault.SessionVersion;var workspaceId=WorkspaceId;
        try
        {
            using var request=Request(body is null?HttpMethod.Get:HttpMethod.Post,path);if(body is not null)request.Content=JsonContent.Create(body,options:ReminderIdentity.Json);
            using var response=await _http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);if(!response.IsSuccessStatusCode)return ReminderUnavailable<T>();
            await using var stream=await response.Content.ReadAsStreamAsync(token);var buffer=new byte[512*1024+1];var count=await stream.ReadAtLeastAsync(buffer,buffer.Length,false,token);if(count>=buffer.Length)return ReminderUnavailable<T>();
            var value=JsonSerializer.Deserialize<T>(buffer.AsSpan(0,count),ReminderIdentity.Json);
            if(generation!=_generation||vaultVersion!=_vault.SessionVersion||workspaceId!=WorkspaceId||value is null||!validate(value)
                ||value is TaskReminderState state&&state.WorkspaceId!=workspaceId)return ReminderUnavailable<T>();
            return new(true,"Reminder status or original result checked.",value);
        }
        catch(OperationCanceledException)when(token.IsCancellationRequested){throw;}
        catch{return ReminderUnavailable<T>();}
    }
}
