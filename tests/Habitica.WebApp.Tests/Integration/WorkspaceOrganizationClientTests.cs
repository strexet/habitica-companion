using System.Net;
using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Auth;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Integration;

namespace Habitica.WebApp.Tests.Integration;

public sealed class WorkspaceOrganizationClientTests
{
    private const string Session="sssssssssssssssssssssssssssssssssssssssssss";
    private static readonly HabiticaCredentials Credentials=new("00000000-0000-4000-8000-000000000011","synthetic-token");
    private static HttpResponseMessage Json(object value)=>new(HttpStatusCode.OK){Content=new StringContent(WorkspaceTaskApiJson.Serialize(value),System.Text.Encoding.UTF8,"application/json")};
    private sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> send):HttpMessageHandler {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>send(request);}
    private static HttpClient Vault()=>new(new Handler(_=>Task.FromResult(Json(new {profileId="prf_"+new string('p',43),managementToken=new string('m',43)}))));
    private static HttpResponseMessage Connection(Guid id)=>Json(new {workspaceId=id,sessionToken=Session,expiresAtUtc=DateTimeOffset.UtcNow.AddHours(1),consentVersion="workspace-v1"});
    private static WorkspaceOrganizationCommand Intent(Guid id)=>new(Guid.NewGuid(),id,1,WorkspaceOrganizationKind.Category,WorkspaceOrganizationOperation.Create,Guid.NewGuid(),new(Name:"Reviewed area"));
    private static WorkspaceOrganizationPreview Preview(WorkspaceOrganizationCommand command)=>new(command.Id,Guid.NewGuid(),DateTimeOffset.UtcNow.AddMinutes(10),false,command,WorkspaceOrganizationCommands.Apply(command,null),false,false);
    private static WorkspaceOrganizationReceipt Receipt(WorkspaceOrganizationPreview p)=>new(p.Command.Id,p.CorrelationId,p.Command.WorkspaceId,p.Command.Kind,p.Command.Operation,p.Command.EntityId,new(p.Command,WorkspaceOrganizationCommands.Hash(p.Command),DateTimeOffset.UtcNow,p.Snapshot),true,false,0,0,true,false);
    [Fact]
    public async Task Creation_binds_all_intent_fields_and_recovers_original_native_receipt_after_lost_acknowledgement()
    {
        var id=Guid.NewGuid();var intent=Intent(id);var preview=Preview(intent with {Id=Guid.NewGuid(),EntityId=Guid.NewGuid()});var requestId=Guid.NewGuid();var calls=0;
        using var vault=Vault();using var http=new HttpClient(new Handler(async request=>{
            if(request.RequestUri!.AbsolutePath=="/connect")return Connection(id);
            Assert.Equal("/organization-api",request.RequestUri.AbsolutePath);Assert.Equal(Session,request.Headers.Authorization!.Parameter);var text=await request.Content!.ReadAsStringAsync();Assert.DoesNotContain(Credentials.ApiToken,text);using var body=JsonDocument.Parse(text);var tool=body.RootElement.GetProperty("tool").GetString();var args=body.RootElement.GetProperty("arguments");calls++;
            if(tool=="prepare_organization_command"){Assert.Equal(requestId,args.GetProperty("requestId").GetGuid());Assert.False(args.TryGetProperty("entityId",out _));Assert.Equal("Reviewed area",args.GetProperty("patch").GetProperty("name").GetString());return Json(preview);}
            Assert.Equal(preview.PreparedCommandId,args.GetProperty("commandId").GetGuid());if(tool=="accept_organization_command")throw new HttpRequestException("Synthetic secret-bearing failure");Assert.Equal("get_organization_command",tool);return Json(Receipt(preview));
        }));
        var client=new WorkspaceCloudClient(http,new ChatGptIntegrationClient(vault,"https://auth.invalid/",false),"https://workspace.invalid/");Assert.True((await client.ConnectAsync(Credentials,default)).Succeeded);
        var prepared=await client.PrepareOrganizationAsync(requestId,intent,default);Assert.True(prepared.Succeeded);Assert.Equal(preview.PreparedCommandId,prepared.Value!.PreparedCommandId);
        var uncertain=await client.AcceptOrganizationAsync(prepared.Value,default);Assert.False(uncertain.Succeeded);Assert.True(uncertain.OutcomeUnknown);Assert.DoesNotContain("secret-bearing",uncertain.Message);
        var recovered=await client.ReadOrganizationAsync(preview.PreparedCommandId,default);Assert.True(recovered.Succeeded);Assert.True(recovered.Value!.SavedInCompanion);Assert.Equal(0,recovered.Value.ConfirmedEffectCount);Assert.Equal(3,calls);
    }
    [Theory]
    [InlineData("command")][InlineData("snapshot")][InlineData("effects")][InlineData("revision")][InlineData("clear")][InlineData("target")][InlineData("receipt")]
    public async Task Altered_native_previews_cannot_replace_reviewed_partial_intent(string mutation)
    {
        var id=Guid.NewGuid();var current=new WorkspaceOrganizationSnapshot(WorkspaceOrganizationKind.Project,Guid.NewGuid(),1,"Old",CategoryId:Guid.NewGuid());
        var intent=new WorkspaceOrganizationCommand(Guid.NewGuid(),id,1,WorkspaceOrganizationKind.Project,WorkspaceOrganizationOperation.Update,current.Id,new(Name:"Reviewed",ClearCategory:true));
        var command=intent with {Id=Guid.NewGuid()};var preview=new WorkspaceOrganizationPreview(command.Id,Guid.NewGuid(),DateTimeOffset.UtcNow.AddMinutes(10),false,command,WorkspaceOrganizationCommands.Apply(command,current),false,false);
        preview=mutation switch {"command"=>preview with {Command=command with {Patch=new(Name:"Changed",ClearCategory:true)}},"snapshot"=>preview with {Snapshot=preview.Snapshot with {Name="Changed"}},"effects"=>preview with {MayAffectHabitica=true},"revision"=>preview with {Command=command with {ExpectedRevision=2}},"clear"=>preview with {Command=command with {Patch=new(Name:"Reviewed")}},"target"=>preview with {Command=command with {EntityId=Guid.NewGuid()}},"receipt"=>preview with {SavedInCompanion=true},_=>preview};
        using var vault=Vault();using var http=new HttpClient(new Handler(request=>Task.FromResult(request.RequestUri!.AbsolutePath=="/connect"?Connection(id):Json(preview))));var client=new WorkspaceCloudClient(http,new ChatGptIntegrationClient(vault,"https://auth.invalid/",false),"https://workspace.invalid/");await client.ConnectAsync(Credentials,default);
        var result=await client.PrepareOrganizationAsync(Guid.NewGuid(),intent,default);Assert.False(result.Succeeded);Assert.True(result.OutcomeUnknown);Assert.Null(result.Value);
    }
    [Theory]
    [InlineData("effects")][InlineData("event")][InlineData("identity")][InlineData("hash")][InlineData("snapshot")][InlineData("unknown")]
    public async Task Forged_game_effects_or_mismatched_native_receipts_cannot_report_a_save(string mutation)
    {
        var id=Guid.NewGuid();var preview=Preview(Intent(id));var receipt=Receipt(preview);
        receipt=mutation switch {"effects"=>receipt with {MayAffectHabitica=true,ConfirmedEffectCount=1},"event"=>receipt with {AcceptedEventCount=1},"identity"=>receipt with {EntityId=Guid.NewGuid()},"hash"=>receipt with {Entry=receipt.Entry with {PayloadHash=new string('0',64)}},"snapshot"=>receipt with {Entry=receipt.Entry with {Snapshot=receipt.Entry.Snapshot with {Name="Wrong"}}},"unknown"=>receipt with {OutcomeUnknown=true},_=>receipt};
        using var vault=Vault();using var http=new HttpClient(new Handler(request=>Task.FromResult(request.RequestUri!.AbsolutePath=="/connect"?Connection(id):Json(receipt))));var client=new WorkspaceCloudClient(http,new ChatGptIntegrationClient(vault,"https://auth.invalid/",false),"https://workspace.invalid/");await client.ConnectAsync(Credentials,default);
        var result=await client.AcceptOrganizationAsync(preview,default);Assert.False(result.Succeeded);Assert.True(result.OutcomeUnknown);Assert.Null(result.Value);
    }
    [Fact]
    public async Task Expired_preview_and_changed_sessions_do_not_authorize_another_save()
    {
        var id=Guid.NewGuid();var preview=Preview(Intent(id));var count=0;var entered=new TaskCompletionSource();var gate=new TaskCompletionSource();
        using var vault=Vault();using var http=new HttpClient(new Handler(async request=>{if(request.RequestUri!.AbsolutePath=="/connect")return Connection(id);count++;entered.SetResult();await gate.Task;return Json(Receipt(preview));}));var client=new WorkspaceCloudClient(http,new ChatGptIntegrationClient(vault,"https://auth.invalid/",false),"https://workspace.invalid/");await client.ConnectAsync(Credentials,default);
        Assert.False((await client.AcceptOrganizationAsync(preview with {Expired=true},default)).Succeeded);Assert.Equal(0,count);
        var pending=client.ReadOrganizationAsync(preview.PreparedCommandId,default);await entered.Task;client.ForgetSession();gate.SetResult();var result=await pending;Assert.False(result.Succeeded);Assert.False(result.OutcomeUnknown);Assert.Null(result.Value);
    }
}
