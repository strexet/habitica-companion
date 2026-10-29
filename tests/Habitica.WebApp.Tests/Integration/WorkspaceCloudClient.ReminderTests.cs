using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Habitica.Application.Notifications;
using Habitica.WebApp.Integration;
namespace Habitica.WebApp.Tests.Integration;
public sealed partial class WorkspaceCloudClientTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public async Task Reminder_preview_and_acceptance_validate_exact_hash_identity_and_non_delivery_receipt(bool tamper)
    {
        var vault=Vault(out var vh);using var owned=vh;var task=Guid.NewGuid();var device=Guid.NewGuid();var command=Guid.NewGuid();var schedule=Guid.NewGuid();
        var intent=new ReminderIntent("set",null,0,task,DateTimeOffset.Parse("2099-11-01T09:00:00Z"),"UTC",null,[new(device,1,Guid.NewGuid())]);var prep=new ReminderPreparation(Guid.NewGuid(),10,intent);var writes=0;
        var receipt=new ReminderReceipt(command,prep.RequestId,_id,schedule,1,task,"set","active",true,true,false,false,null,null,intent);
        using var http=new HttpClient(new Handler(async(request,ct)=>
        {
            if(request.RequestUri!.AbsolutePath=="/connect")return Connection();Assert.Equal(Session,request.Headers.Authorization!.Parameter);
            Assert.Equal("/reminder-api",request.RequestUri.AbsolutePath);using var json=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));var args=json.RootElement.GetProperty("arguments");
            if(json.RootElement.GetProperty("tool").GetString()=="prepare_reminder_command")
            {
                Assert.Equal(prep.RequestId,args.GetProperty("requestId").GetGuid());Assert.Equal("2099-11-01T09:00:00.0000000Z",args.GetProperty("intent").GetProperty("reviewAfterUtc").GetString());
                var response=new ReminderPreview(command,prep.RequestId,false,ReminderIdentity.Hash(prep),new string('t',43),DateTimeOffset.UtcNow.AddMinutes(5),_id,10,tamper?intent with{TaskId=Guid.NewGuid()}:intent,schedule,"generic");return new(HttpStatusCode.OK){Content=JsonContent.Create(response,options:ReminderIdentity.Json)};
            }
            writes++;Assert.Equal(command,args.GetProperty("commandId").GetGuid());Assert.Equal(ReminderIdentity.Hash(prep),args.GetProperty("expectedHash").GetString());
            return new(HttpStatusCode.OK){Content=JsonContent.Create(new ReminderCommandStatus(command,true,Receipt:receipt),options:ReminderIdentity.Json)};
        }));
        var cloud=new WorkspaceCloudClient(http,vault,"https://workspace.invalid/");await cloud.ConnectAsync(Credentials,default);ITaskReminderClient client=cloud;var preview=await client.PrepareReminderAsync(prep,default);Assert.Equal(!tamper,preview.Succeeded);
        if(!tamper){var result=await client.AcceptReminderAsync(preview.Value!,default);Assert.True(result.Succeeded);Assert.True(result.Value!.Receipt!.Scheduled);Assert.False(result.Value.Receipt.AcceptedByPushService);Assert.Null(result.Value.Receipt.Shown);}Assert.Equal(tamper?0:1,writes);
    }
    [Theory][InlineData("secret")][InlineData("delivery")][InlineData("missing")][InlineData("workspace")]
    public async Task Reminder_reads_refuse_secret_bearing_missing_or_unverifiable_state(string invalid)
    {
        var vault=Vault(out var vh);using var owned=vh;var task=Guid.NewGuid();object response=new{workspaceId=_id,workspaceRevision=10,taskId=task,available=false,schedules=Array.Empty<object>(),deliveries=Array.Empty<object>(),offset=0,nextOffset=(int?)null,push=(object?)null,shown=(bool?)null,read=(bool?)null};
        var node=JsonSerializer.SerializeToNode(response)!.AsObject();if(invalid=="secret")node["endpoint"]="https://private.invalid/";if(invalid=="delivery")node["shown"]=true;if(invalid=="missing")node.Remove("shown");if(invalid=="workspace")node["workspaceId"]=Guid.NewGuid();
        using var http=new HttpClient(new Handler((request,ct)=>System.Threading.Tasks.Task.FromResult(request.RequestUri!.AbsolutePath=="/connect"?Connection():Json(node))));var cloud=new WorkspaceCloudClient(http,vault,"https://workspace.invalid/");await cloud.ConnectAsync(Credentials,default);Assert.False((await ((ITaskReminderClient)cloud).ReadTaskRemindersAsync(task,0,default)).Succeeded);
    }
    [Fact] public async Task Late_reminder_response_is_discarded_and_transport_failure_never_replays()
    {
        var vault=Vault(out var vh);using var owned=vh;var task=Guid.NewGuid();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var calls=0;
        using var http=new HttpClient(new Handler(async(request,ct)=>{if(request.RequestUri!.AbsolutePath=="/connect")return Connection();calls++;entered.TrySetResult();await gate.Task;return Json(new TaskReminderState(_id,10,task,false,[],[],0,null,null,null,null));}));var cloud=new WorkspaceCloudClient(http,vault,"https://workspace.invalid/");await cloud.ConnectAsync(Credentials,default);var pending=((ITaskReminderClient)cloud).ReadTaskRemindersAsync(task,0,default);await entered.Task;cloud.ForgetSession();gate.SetResult();Assert.False((await pending).Succeeded);Assert.Equal(1,calls);
    }
}
