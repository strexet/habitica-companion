using System.Net;
using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Diagnostics;
using Habitica.WebApp.Integration;
using Habitica.WebApp.Tests.Workspace;
namespace Habitica.WebApp.Tests.Integration;

public sealed partial class WorkspaceCloudClientTests
{
    [Fact]
    public async Task Exact_retirement_wire_and_hash_validation_preserve_original_unknown_receipt_and_redacted_logs()
    {
        var f=new SourceRetirementFixture();var request=new WorkspaceSourceRetirementRequest(Guid.NewGuid(),f.Plan.ConversionId,f.Plan.Revision);await f.PrepareSourceRetirementAsync(request,default);for(var i=0;i<f.View!.VerificationSteps;i++)await f.VerifySourceRetirementAsync(f.View.CommandId,default);object reply=f.View;
        var vault=Vault(out var vaultHttp);using var owned=vaultHttp;var calls=new List<string>();var logs=new Habitica.Storage.DiagnosticsLogStore(new InMemoryKeyValueStorage());var writer=new Habitica.Application.Diagnostics.DiagnosticsLogWriter(logs,TimeProvider.System);
        using var http=new HttpClient(new Handler(async(message,token)=>
        {
            if(message.RequestUri!.AbsolutePath=="/connect")return Json(new {workspaceId=f.State.Document.Id,sessionToken=Session,expiresAtUtc=DateTimeOffset.UtcNow.AddHours(1),consentVersion="workspace-v1"},HttpStatusCode.Created);
            Assert.Equal("/retirement-api",message.RequestUri.AbsolutePath);Assert.Equal(Session,message.Headers.Authorization!.Parameter);var body=await message.Content!.ReadAsStringAsync(token);Assert.DoesNotContain(Credentials.ApiToken,body);using var json=JsonDocument.Parse(body);var tool=json.RootElement.GetProperty("tool").GetString()!;calls.Add(tool);
            if(tool=="accept_source_retirement"){var args=json.RootElement.GetProperty("arguments");Assert.Equal(3,args.EnumerateObject().Count());Assert.True(args.GetProperty("irreversibleApproved").GetBoolean());Assert.Equal(f.View!.ContextHash,args.GetProperty("contextHash").GetString());await f.AcceptSourceRetirementAsync(f.View,default);throw new HttpRequestException("Synthetic private details");}
            return ConversionJson(tool=="get_source_retirement_command"?f.Saved!:reply);
        }));
        var client=new WorkspaceCloudClient(http,vault,"https://workspace.invalid/",diagnostics:writer);await client.ConnectAsync(Credentials,default);var preview=await client.PrepareSourceRetirementAsync(request,default);Assert.True(preview.Succeeded,preview.Message);
        reply=f.View! with {ContextHash=new string('f',64)};Assert.False((await client.VerifySourceRetirementAsync(f.View.CommandId,default)).Succeeded);reply=f.View! with {Targets=f.View.Targets.Select((row,i)=>i==0?row with {Task=row.Task with {Notes="Altered content"}}:row).ToArray()};Assert.False((await client.VerifySourceRetirementAsync(f.View.CommandId,default)).Succeeded);
        reply=f.View!;var lost=await client.AcceptSourceRetirementAsync(f.View,default);Assert.True(lost.OutcomeUnknown);Assert.DoesNotContain("private",lost.Message);var saved=await client.ReadSourceRetirementAsync(f.View.CommandId,default);Assert.True(saved.Succeeded,saved.Message);Assert.NotNull(saved.Value!.Receipt);Assert.Single(calls,tool=>tool=="accept_source_retirement");
        var entries=await logs.GetRecentAsync(default);Assert.Contains(entries,row=>row.Operation=="workspace-retirement-read"&&row.Mode==DiagnosticsMode.LiveRead);Assert.Contains(entries,row=>row.Operation=="workspace-retirement-accept"&&row.Context!.Result==DiagnosticsResult.Unknown);var retained=JsonSerializer.Serialize(entries);Assert.DoesNotContain(f.Plan.Source.Notes!,retained);Assert.DoesNotContain(Credentials.ApiToken,retained);
    }
    [Fact]
    public async Task Late_retirement_response_is_discarded_after_account_session_changes()
    {
        var f=new SourceRetirementFixture();var request=new WorkspaceSourceRetirementRequest(Guid.NewGuid(),f.Plan.ConversionId,f.Plan.Revision);await f.PrepareSourceRetirementAsync(request,default);var vault=Vault(out var vaultHttp);using var owned=vaultHttp;var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http=new HttpClient(new Handler(async(message,_)=>{if(message.RequestUri!.AbsolutePath=="/connect")return Json(new {workspaceId=f.State.Document.Id,sessionToken=Session,expiresAtUtc=DateTimeOffset.UtcNow.AddHours(1),consentVersion="workspace-v1"},HttpStatusCode.Created);entered.SetResult();await release.Task;return ConversionJson(f.View);}));var client=new WorkspaceCloudClient(http,vault,"https://workspace.invalid/");await client.ConnectAsync(Credentials,default);var read=client.PrepareSourceRetirementAsync(request,default);await entered.Task;client.ForgetSession();release.SetResult();Assert.False((await read).Succeeded);Assert.Null(client.WorkspaceId);
    }
}
