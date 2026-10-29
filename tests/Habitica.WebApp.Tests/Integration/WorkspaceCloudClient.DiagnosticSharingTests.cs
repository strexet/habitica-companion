using System.Net;
using System.Text.Json;
using Habitica.Application.Diagnostics;
using Habitica.Domain.Diagnostics;
using Habitica.WebApp.Integration;
namespace Habitica.WebApp.Tests.Integration;
public sealed partial class WorkspaceCloudClientTests
{
    [Fact]
    public async Task Diagnostic_upload_uses_owner_identity_metadata_preview_and_original_recovery_after_lost_acknowledgement()
    {
        var vault=Vault(out var vaultHttp);using var owned=vaultHttp;var at=DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());var requestId=DiagnosticSharing.NewRequestId(at);var upload=Guid.NewGuid();var sent=new List<string>();
        var log=new DiagnosticsLogEntry("private-id",at,DiagnosticsFeatureArea.Workspace,"workspace-tag-accept",DiagnosticsSeverity.Warning,DiagnosticsMode.LiveMutation,"private-title credential-canary",new Dictionary<string,string>(),new(DiagnosticsResult.Unknown));
        var bundle=DiagnosticSharing.Prepare(DiagnosticsBundleExport.Create([log],new(),at));var hash=DiagnosticSharing.Hash(bundle);var size=DiagnosticSharing.Bytes(bundle);
        var receipt=new DiagnosticUploadReceipt(_id,upload,requestId,hash,1,size,at,at.AddDays(7),true);var prepared=new DiagnosticUploadView(_id,requestId,false,true,upload,hash,"prepared",1,size,at,at.AddMinutes(5),at.AddDays(7));
        var accepted=prepared with {Saved=true,OutcomeUnknown=false,State="accepted",Receipt=receipt};var lost=true;
        using var http=new HttpClient(new Handler(async(r,t)=>
        {
            if(r.RequestUri!.AbsolutePath=="/connect")return Connection();Assert.Equal(Management,r.Headers.Authorization!.Parameter);var text=r.Content is null?"":await r.Content.ReadAsStringAsync(t);Assert.DoesNotContain("private-title",text);Assert.DoesNotContain("credential-canary",text);Assert.DoesNotContain(Credentials.ApiToken,text);sent.Add(r.RequestUri.AbsolutePath);
            if(r.RequestUri.AbsolutePath.EndsWith("/prepare")){Assert.DoesNotContain("entries",text);Assert.Contains(hash,text);return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(prepared,DiagnosticSharing.Json))};}
            if(r.RequestUri.AbsolutePath.EndsWith("/accept")){Assert.Contains("entries",text);if(lost){lost=false;throw new HttpRequestException("raw-response-canary");}}
            return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(accepted,DiagnosticSharing.Json))};
        }));
        var client=new WorkspaceCloudClient(http,vault,"https://workspace.invalid/");await client.ConnectAsync(Credentials,default);var preview=await client.PrepareDiagnosticUploadAsync(requestId,bundle,default);Assert.True(preview.Succeeded);Assert.DoesNotContain(sent,s=>s.EndsWith("/accept"));
        var uncertain=await client.AcceptDiagnosticUploadAsync(preview.Value!,bundle,default);Assert.False(uncertain.Succeeded);Assert.True(uncertain.OutcomeUnknown);var recovered=await client.RecoverDiagnosticUploadAsync(requestId,default);Assert.True(recovered.Succeeded);Assert.Equal(receipt,recovered.Value!.Receipt);Assert.Single(sent,s=>s.EndsWith("/accept"));
    }
    [Fact]
    public async Task Diagnostic_transport_refuses_changed_hash_foreign_identity_and_late_account_responses()
    {
        var vault=Vault(out var vaultHttp);using var owned=vaultHttp;var at=DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());var requestId=DiagnosticSharing.NewRequestId(at);var entry=new DiagnosticsLogEntry("id",at,DiagnosticsFeatureArea.Workspace,"workspace-tag-status",DiagnosticsSeverity.Info,DiagnosticsMode.LiveRead,"",new Dictionary<string,string>());var b=DiagnosticSharing.Prepare(DiagnosticsBundleExport.Create([entry],new(),at));var view=new DiagnosticUploadView(_id,requestId,false,true,Guid.NewGuid(),DiagnosticSharing.Hash(b),"prepared",1,DiagnosticSharing.Bytes(b),at,at.AddMinutes(5),at.AddDays(7));var delayed=false;var entered=new TaskCompletionSource();var release=new TaskCompletionSource();
        using var http=new HttpClient(new Handler(async(r,_)=>{if(r.RequestUri!.AbsolutePath=="/connect")return Connection();if(delayed){entered.SetResult();await release.Task;}return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(view,DiagnosticSharing.Json))};}));
        var client=new WorkspaceCloudClient(http,vault,"https://workspace.invalid/");await client.ConnectAsync(Credentials,default);
        view=view with {BundleHash=new string('a',64)};Assert.False((await client.PrepareDiagnosticUploadAsync(requestId,b,default)).Succeeded);view=view with {BundleHash=DiagnosticSharing.Hash(b),WorkspaceId=Guid.NewGuid()};Assert.False((await client.PrepareDiagnosticUploadAsync(requestId,b,default)).Succeeded);
        view=view with {WorkspaceId=_id};delayed=true;var pending=client.PrepareDiagnosticUploadAsync(requestId,b,default);await entered.Task;client.ForgetSession();release.SetResult();Assert.False((await pending).Succeeded);
    }
    [Fact]
    public async Task Diagnostic_reads_require_sanitized_exact_bundle_and_source_labelled_non_live_receipts()
    {
        var vault=Vault(out var vaultHttp);using var owned=vaultHttp;var at=DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());var requestId=DiagnosticSharing.NewRequestId(at);var uploadId=Guid.NewGuid();var commandId=Guid.NewGuid();
        var eventRow=new DiagnosticsLogEntry("id",at,DiagnosticsFeatureArea.Workspace,"workspace-tag-status",DiagnosticsSeverity.Info,DiagnosticsMode.LiveRead,"",new Dictionary<string,string>());var b=DiagnosticSharing.Prepare(DiagnosticsBundleExport.Create([eventRow],new(),at));
        var receipt=new DiagnosticUploadReceipt(_id,uploadId,requestId,DiagnosticSharing.Hash(b),1,DiagnosticSharing.Bytes(b),at,at.AddDays(7),true);
        var bundle=new DiagnosticReadBundle(_id,requestId,true,false,uploadId,receipt.BundleHash,"accepted",1,receipt.PayloadBytes,at,at.AddMinutes(5),at.AddDays(7),receipt,b,"explicit-browser-upload",false,false);
        var item=new DiagnosticReadItem("server-task-"+commandId.ToString("D"),"server-task-receipt",at,at,"server-acceptance",null,new(commandId,"workspace-task-accept",false,RecordedProjectionState:"unknownOutcome",RecordedConfirmedCount:0,RecordedRequestedCount:1),null);
        var page=new DiagnosticReadPage(_id,1,1,at,at.AddMinutes(1),[item],1,null,false,new("Explicit browser copies","Reduced original receipts",0,200,[]));
        using var http=new HttpClient(new Handler((r,_)=>Task.FromResult(r.RequestUri!.AbsolutePath=="/connect"?Connection():new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(r.RequestUri.AbsolutePath.EndsWith("/bundle")?(object)bundle:page,DiagnosticSharing.Json))})));
        var client=new WorkspaceCloudClient(http,vault,"https://workspace.invalid/");await client.ConnectAsync(Credentials,default);Assert.True((await client.ReadDiagnosticBundleAsync(uploadId,default)).Succeeded);
        var query=new DiagnosticReadQuery(at.AddDays(-1),at.AddDays(1));Assert.True((await client.QueryDiagnosticsAsync(query,default)).Succeeded);
        page=page with {Items=[item with {Receipt=item.Receipt! with {LiveVerification=true}}]};Assert.False((await client.QueryDiagnosticsAsync(query,default)).Succeeded);
        page=page with {Items=[item with {Source="raw-provider-log"}]};Assert.False((await client.QueryDiagnosticsAsync(query,default)).Succeeded);
        bundle=bundle with {Bundle=b with {Entries=[b.Entries[0] with {Message="private-note-canary"}]}};Assert.False((await client.ReadDiagnosticBundleAsync(uploadId,default)).Succeeded);
    }

}
