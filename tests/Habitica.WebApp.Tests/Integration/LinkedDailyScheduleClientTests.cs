using System.Net;
using Habitica.Application.Workspace;
using Habitica.Domain.Auth;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Integration;
namespace Habitica.WebApp.Tests.Integration;
public sealed class LinkedDailyScheduleClientTests
{
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> respond):HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromResult(respond(request));}
    private static HttpResponseMessage Json(object value)=>new(HttpStatusCode.OK){Content=new StringContent(WorkspaceTaskApiJson.Serialize(value),System.Text.Encoding.UTF8,"application/json")};
    private static RecurrenceRule Rule=>new(RecurrenceFrequency.Daily,3,new(2026,11,2),"UTC",[],[]);
    private static HabiticaDailySchedule Before=>new("daily",1,DateTimeOffset.Parse("2026-10-27T00:00:00Z"),new Dictionary<string,bool>{{"su",true},{"m",true},{"t",true},{"w",true},{"th",true},{"f",true},{"s",true}},[],[]);
    private static WorkspaceCommand Command(Guid workspace)=>new(Guid.NewGuid(),workspace,10,WorkspaceOperation.EditLinkedDailySchedule,Guid.NewGuid(),new(Recurrence:Rule),ProjectToHabitica:true);
    private static WorkspaceCommand Prepared(WorkspaceCommand intent){var context=new LinkedDailyScheduleContext(Guid.NewGuid(),new string('a',64),DateTimeOffset.UtcNow,new(0,0),Before,LinkedDailySchedulePolicy.Review(Before,intent.Patch!.Recurrence!,new(0,0)).ExpectedAfter!);return intent with{Id=Guid.NewGuid(),Patch=intent.Patch! with{LinkedSchedule=context}};}
    [Fact]public async Task LinkedDailySchedule_transport_refuses_a_self_consistent_but_unrequested_rule()
    {
        var workspace=Guid.NewGuid();var intent=Command(workspace);bool tamper=false;
        using var vaultHttp=new HttpClient(new Handler(_=>Json(new{profileId="prf_"+new string('a',43),managementToken=new string('m',43)})));var vault=new ChatGptIntegrationClient(vaultHttp,"https://auth.invalid/");
        using var http=new HttpClient(new Handler(request=>{if(request.RequestUri!.AbsolutePath=="/connect")return Json(new{workspaceId=workspace,sessionToken=new string('s',43),consentVersion="workspace-v1",expiresAtUtc=DateTimeOffset.UtcNow.AddHours(1)});var prepared=Prepared(tamper?intent with{Patch=new(Recurrence:Rule with{Interval=99})}:intent);return Json(new WorkspaceCommandPreview(prepared.Id,Guid.NewGuid(),DateTimeOffset.UtcNow.AddSeconds(90),false,prepared,true,false));}));
        var client=new WorkspaceCloudClient(http,vault,"https://workspace.invalid/");Assert.True((await client.ConnectAsync(new(Guid.NewGuid().ToString("D"),"synthetic-only"),default)).Succeeded);
        Assert.True((await client.PrepareCommandAsync(Guid.NewGuid(),intent,default)).Succeeded);tamper=true;var refused=await client.PrepareCommandAsync(Guid.NewGuid(),intent,default);Assert.False(refused.Succeeded);Assert.True(refused.OutcomeUnknown);
    }
    [Fact]public async Task LinkedDailySchedule_original_epoch_and_normalized_receipt_are_validated_without_current_state_inference()
    {
        var workspace=Guid.NewGuid();var prepared=Prepared(Command(workspace));var context=prepared.Patch!.LinkedSchedule!;var effective=context.ObservedAtUtc.AddSeconds(1);var outcome=new LinkedDailyScheduleResult(effective.AddSeconds(1),context.ExpectedAfter,true,Guid.NewGuid());var epoch=new WorkspaceDailyScheduleEpoch(effective,prepared.Patch.Recurrence!,context,outcome);
        var receipt=new WorkspaceMutationReceipt("editLinkedDailySchedule",prepared.TaskId,prepared.Id,Guid.NewGuid(),"Synthetic",true,"confirmed",0,1,1,null,"none",null,true,false,epoch);bool corrupt=false;var tools=new List<string>();
        using var vaultHttp=new HttpClient(new Handler(_=>Json(new{profileId="prf_"+new string('a',43),managementToken=new string('m',43)})));var vault=new ChatGptIntegrationClient(vaultHttp,"https://auth.invalid/");
        using var http=new HttpClient(new AsyncHandler(async request=>{if(request.RequestUri!.AbsolutePath=="/connect")return Json(new{workspaceId=workspace,sessionToken=new string('s',43),consentVersion="workspace-v1",expiresAtUtc=DateTimeOffset.UtcNow.AddHours(1)});using var input=System.Text.Json.JsonDocument.Parse(await request.Content!.ReadAsStringAsync());tools.Add(input.RootElement.GetProperty("tool").GetString()!);return Json(corrupt?receipt with{ScheduleEpoch=epoch with{Result=outcome with{MatchesExpected=false}}}:receipt);}));
        var client=new WorkspaceCloudClient(http,vault,"https://workspace.invalid/");await client.ConnectAsync(new(Guid.NewGuid().ToString("D"),"synthetic-only"),default);var preview=new WorkspaceCommandPreview(prepared.Id,receipt.CorrelationId,effective.AddSeconds(60),false,prepared,true,false);
        Assert.True((await client.AcceptCommandAsync(preview,default)).Succeeded);Assert.Equal("edit_linked_daily_schedule",tools.Single());Assert.Equal(outcome.AttemptId,(await client.ReadCommandAsync(prepared.Id,default)).Value!.ScheduleEpoch!.Result!.AttemptId);corrupt=true;var bad=await client.ReadCommandAsync(prepared.Id,default);Assert.False(bad.Succeeded);Assert.True(bad.OutcomeUnknown);
    }
    private sealed class AsyncHandler(Func<HttpRequestMessage,Task<HttpResponseMessage>> respond):HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>respond(request);}
}
