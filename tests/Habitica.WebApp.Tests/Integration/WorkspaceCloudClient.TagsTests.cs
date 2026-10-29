using System.Net;
using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Integration;

namespace Habitica.WebApp.Tests.Integration;

public sealed partial class WorkspaceCloudClientTests
{
    [Fact]
    public async Task Tag_wire_reviews_keep_original_identity_and_separate_metadata_from_game_effects()
    {
        var vault=Vault(out var vaultHttp);using var owned=vaultHttp;var requestId=Guid.NewGuid();var group=Guid.NewGuid();var commandId=Guid.NewGuid();var calls=0;
        var review=new WorkspaceTagReview(commandId,requestId,_id,"reconcile",1,DateTimeOffset.UtcNow.AddMinutes(-1),true,0,
            new(group,WorkspaceOrganizationKind.Project,"Same name",null,[],"No scoring. Exact metadata only."),Expired:true,ReviewOutcome:"prepared");
        using var http=new HttpClient(new Handler(async(request,token)=>
        {
            if(request.RequestUri!.AbsolutePath=="/connect")return Connection();Assert.Equal("/tag-api",request.RequestUri.AbsolutePath);Assert.Equal(Session,request.Headers.Authorization!.Parameter);
            var text=await request.Content!.ReadAsStringAsync(token);Assert.DoesNotContain(Credentials.ApiToken,text);using var body=JsonDocument.Parse(text);var tool=body.RootElement.GetProperty("tool").GetString();var args=body.RootElement.GetProperty("arguments");calls++;
            if(tool=="prepare_group_tag_command"){Assert.Equal(requestId,args.GetProperty("requestId").GetGuid());Assert.Equal("project",args.GetProperty("kind").GetString());return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(WorkspaceTaskApiJson.Serialize(review))};}
            Assert.Equal(commandId,args.GetProperty("commandId").GetGuid());
            if(calls==2)throw new HttpRequestException("Synthetic secret must not escape");
            return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(WorkspaceTaskApiJson.Serialize(new WorkspaceTagReceipt(commandId,requestId,_id,"reconcile",true,0,false,[])))};
        }));
        var client=new WorkspaceCloudClient(http,vault,"https://workspace.invalid/");await client.ConnectAsync(Credentials,default);
        var prepared=await client.PrepareTagReviewAsync(new(requestId,"reconcile",WorkspaceOrganizationKind.Project,group),default);Assert.True(prepared.Succeeded);
        var unknown=await client.AcceptTagReviewAsync(prepared.Value!,default);Assert.False(unknown.Succeeded);Assert.True(unknown.OutcomeUnknown);Assert.DoesNotContain("secret",unknown.Message);
        // Original acceptance may already have succeeded before expiry; server owns receipt-first recovery.
        var recovered=await client.AcceptTagReviewAsync(prepared.Value!,default);Assert.True(recovered.Succeeded);Assert.Equal(commandId,recovered.Value!.CommandId);Assert.Equal(0,recovered.Value.GameEffects);Assert.Equal(3,calls);
    }
    [Fact]
    public async Task Tag_terminal_receipts_require_exact_identity_and_explicit_durable_proof()
    {
        var vault=Vault(out var vaultHttp);using var owned=vaultHttp;var command=Guid.NewGuid();var request=Guid.NewGuid();
        var receipt=new WorkspaceTagReceipt(command,request,_id,"reconcile",false,0,false,[],ReviewOutcome:"terminal-unaccepted",Reason:"tag_preview_changed",ClosedAtUtc:DateTimeOffset.UtcNow);
        using var http=new HttpClient(new Handler((r,_)=>Task.FromResult(r.RequestUri!.AbsolutePath=="/connect"?Connection():Json(receipt))));
        var client=new WorkspaceCloudClient(http,vault,"https://workspace.invalid/");await client.ConnectAsync(Credentials,default);
        Assert.True((await client.ReadTagReviewAsync(command,default)).Succeeded);
        var original=receipt;
        foreach(var invalid in new[]{original with {ClosedAtUtc=null},original with {ReviewOutcome=null},original with {OutcomeUnknown=true},original with {WorkIds=[Guid.NewGuid()]},original with {WorkspaceId=Guid.NewGuid()},original with {CommandId=Guid.NewGuid()},original with {SavedInCompanion=true}})
        {receipt=invalid;var result=await client.ReadTagReviewAsync(command,default);Assert.False(result.Succeeded);Assert.True(result.OutcomeUnknown);}
        receipt=original with {SavedInCompanion=null,OutcomeUnknown=true,ReviewOutcome="prepared",Prepared=true,Reason=null,ClosedAtUtc=null};
        var prepared=await client.ReadTagReviewAsync(command,default);Assert.True(prepared.Succeeded);Assert.Null(prepared.Value!.SavedInCompanion);Assert.True(prepared.Value.OutcomeUnknown);
    }
    [Fact]
    public async Task Tag_status_rejects_wrong_account_inconsistent_counts_and_late_session_response()
    {
        var vault=Vault(out var vaultHttp);using var owned=vaultHttp;var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var delay=false;
        var page=new WorkspaceTagStatusPage("stored-canonical-evidence",_id,1,true,false,false,false,new(2,1,0,1,0,1),new(0,0,0,0,0),[],0,null);
        using var http=new HttpClient(new Handler(async(request,_)=>{if(request.RequestUri!.AbsolutePath=="/connect")return Connection();if(delay){entered.SetResult();await release.Task;}return Json(page);}));
        var client=new WorkspaceCloudClient(http,vault,"https://workspace.invalid/");await client.ConnectAsync(Credentials,default);
        var valid=await client.ReadGroupTagsAsync(0,null,default);Assert.True(valid.Succeeded);Assert.False(valid.Value!.IntegrationComplete);Assert.Equal(1,valid.Value.TaskCoverage.Archived);
        page=page with {IntegrationComplete=true};Assert.False((await client.ReadGroupTagsAsync(0,null,default)).Succeeded);page=page with {IntegrationComplete=false,WorkspaceId=Guid.NewGuid()};Assert.False((await client.ReadGroupTagsAsync(0,null,default)).Succeeded);
        page=page with {WorkspaceId=_id,TaskCoverage=new(2,2,0,1,0,1)};Assert.False((await client.ReadGroupTagsAsync(0,null,default)).Succeeded);
        delay=true;var pending=client.ReadGroupTagsAsync(0,null,default);await entered.Task;client.ForgetSession();release.SetResult();Assert.False((await pending).Succeeded);Assert.Null(client.WorkspaceId);
    }
    [Fact]
    public async Task Tag_consent_uses_management_identity_and_explicit_version_without_changing_agent_grants()
    {
        var calls=0;using var http=new HttpClient(new Handler(async(request,token)=>
        {
            if(request.RequestUri!.AbsolutePath=="/enroll")return Json(new {profileId="prf_"+new string('a',43),managementToken=Management},HttpStatusCode.Created);
            Assert.EndsWith("/tag-consent",request.RequestUri.AbsolutePath);Assert.Equal(Management,request.Headers.Authorization!.Parameter);calls++;
            if(request.Method==HttpMethod.Get)return Json(new {enabled=false,consentVersion="habitica-tags-v1",grantedAtUtc=(string?)null});
            using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));Assert.True(body.RootElement.GetProperty("enabled").GetBoolean());Assert.Equal("habitica-tags-v1",body.RootElement.GetProperty("consentVersion").GetString());
            return Json(new {enabled=true,consentVersion="habitica-tags-v1",remoteTagsRetained=true,unknownAttemptsRetained=true});
        }));
        var vault=new ChatGptIntegrationClient(http,"https://auth.invalid/",false);await vault.RevalidateAsync(Credentials,default);
        Assert.False((await vault.ReadTagConsentAsync(default)).Enabled);Assert.True((await vault.SetTagConsentAsync(true,default)).Enabled);Assert.Equal(2,calls);
    }
}
