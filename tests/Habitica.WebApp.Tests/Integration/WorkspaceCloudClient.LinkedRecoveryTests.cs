using System.Net;
using System.Text;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Integration;
namespace Habitica.WebApp.Tests.Integration;
public sealed partial class WorkspaceCloudClientTests
{
    private static HttpResponseMessage LinkedJson(WorkspaceLinkedRecoveryView view)=>new(HttpStatusCode.OK){Content=new StringContent(WorkspaceTaskApiJson.Serialize(view),Encoding.UTF8,"application/json")};
    private static WorkspaceLinkedRecoveryView LinkedView(Guid workspaceId,bool confirmed=false)
    {
        var state=WorkspaceJournalJson.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","linked-recovery-todo-journal-v1.json")));
        var row=state.Document.LinkedTaskRecoveries![0];var completion=state.Document.Completions[0];var task=state.Commands.Single(entry=>entry.Command.Id==completion.CommandId).TaskSnapshot!;var mapping=state.Document.Projections[0];
        var source=row.SourceContext with {WorkspaceId=workspaceId};
        row=row with {SourceContext=source};row=row with {ContextHash=WorkspaceLinkedRecovery.ContextHash(workspaceId,row,completion)};
        var review=new WorkspaceRecoveryReview("own-task-recovery-v1",row.ContextHash,Guid.Parse(mapping.HabiticaTaskId!),task.Kind,mapping.Fingerprint!,null,new(2026,10,27),row.UpstreamReviewHash,new("unknown","unknown","unknown","unknown","unknown"));
        return new(row.Id,Guid.NewGuid(),workspaceId,row.ReviewedRevision,task,mapping,completion,source,row.SourceAttemptId,row.AccountId,row.ContextHash,review,confirmed?"confirmed":"ready",DateTimeOffset.UtcNow.AddMinutes(1),false,false,confirmed?true:null,confirmed?row:null,null,"Synthetic exact recovery; resource changes unknown.");
    }
    [Fact]
    public async Task Linked_recovery_binds_review_and_reads_original_confirmed_receipt()
    {
        var vault=Vault(out var vaultHttp);using var ownedVaultHttp=vaultHttp;var view=LinkedView(_id);var request=new WorkspaceLinkedRecoveryRequest(view.RequestId,view.Task.Id,view.Completion.Id,view.Revision);var confirmed=false;
        using var http=new HttpClient(new Handler(async(message,token)=>{
            if(message.RequestUri!.AbsolutePath=="/connect")return Connection();Assert.Equal("/linked-recovery-api",message.RequestUri.AbsolutePath);
            using var json=System.Text.Json.JsonDocument.Parse(await message.Content!.ReadAsStringAsync(token));var tool=json.RootElement.GetProperty("tool").GetString();var args=json.RootElement.GetProperty("arguments");
            if(tool=="prepare_linked_task_recovery"){Assert.Equal(request.RequestId,args.GetProperty("requestId").GetGuid());return LinkedJson(view);}
            Assert.Equal(view.CommandId,args.GetProperty("commandId").GetGuid());
            if(tool=="accept_linked_task_recovery"){Assert.Equal(view.ContextHash,args.GetProperty("contextHash").GetString());Assert.Equal(view.Review.ReviewHash,args.GetProperty("reviewHash").GetString());confirmed=true;}
            Assert.True(confirmed);var row=LinkedView(_id,true).Receipt! with {Id=view.CommandId,RecoveryAttemptId=view.CommandId,ContextHash=view.ContextHash,UpstreamReviewHash=view.Review.ReviewHash};return LinkedJson(view with {State="confirmed",SavedInCompanion=true,Receipt=row});
        }));
        var client=new WorkspaceCloudClient(http,vault,"https://workspace.invalid/");await client.ConnectAsync(Credentials,default);
        var prepared=await client.PrepareLinkedRecoveryAsync(request,default);Assert.True(prepared.Succeeded,prepared.Message);
        Assert.True((await client.AcceptLinkedRecoveryAsync(prepared.Value!,default)).Succeeded);Assert.True((await client.ReadLinkedRecoveryAsync(view.CommandId,default)).Succeeded);
    }
    [Theory] [InlineData("context")] [InlineData("resources")] [InlineData("task")] [InlineData("completion")] [InlineData("receipt")] [InlineData("state")] [InlineData("missing")]
    public async Task Linked_recovery_rejects_wrong_context_resources_identity_or_receipt(string tamper)
    {
        var vault=Vault(out var vaultHttp);using var ownedVaultHttp=vaultHttp;var view=LinkedView(_id,true);var request=new WorkspaceLinkedRecoveryRequest(view.RequestId,view.Task.Id,view.Completion.Id,view.Revision);
        var bad=tamper switch {"context"=>view with {ContextHash=new string('b',64)},"resources"=>view with {Review=view.Review with {ResourceChanges=new("unknown","unknown","refund","unknown","unknown")}},"task"=>view with {Task=view.Task with {Id=Guid.NewGuid()}},"completion"=>view with {Completion=view.Completion with {Id=Guid.NewGuid()}},"receipt"=>view with {Receipt=view.Receipt! with {SourceAttemptId=Guid.NewGuid()}},"state"=>view with {OutcomeUnknown=true},_=>view with {Receipt=null,SavedInCompanion=null}};
        using var http=new HttpClient(new Handler((message,_)=>Task.FromResult(message.RequestUri!.AbsolutePath=="/connect"?Connection():LinkedJson(bad))));
        var client=new WorkspaceCloudClient(http,vault,"https://workspace.invalid/");await client.ConnectAsync(Credentials,default);Assert.False((await client.PrepareLinkedRecoveryAsync(request,default)).Succeeded);
    }
    [Fact]
    public async Task Linked_recovery_transport_failure_keeps_uncertainty_and_expired_preview_never_sends()
    {
        var vault=Vault(out var vaultHttp);using var ownedVaultHttp=vaultHttp;var view=LinkedView(_id);var sends=0;
        using var http=new HttpClient(new Handler((message,_)=>{if(message.RequestUri!.AbsolutePath=="/connect")return Task.FromResult(Connection());sends++;throw new HttpRequestException("Synthetic lost response");}));
        var client=new WorkspaceCloudClient(http,vault,"https://workspace.invalid/");await client.ConnectAsync(Credentials,default);
        Assert.False((await client.AcceptLinkedRecoveryAsync(view with {Expired=true},default)).Succeeded);Assert.Equal(0,sends);
        var result=await client.AcceptLinkedRecoveryAsync(view,default);Assert.False(result.Succeeded);Assert.True(result.OutcomeUnknown);Assert.Equal(1,sends);
    }
}

public sealed partial class WorkspaceCloudClientTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public async Task Original_linked_recovery_discovery_binds_exact_retained_completion_and_command(bool tamper)
    {
        var vault=Vault(out var vaultHttp);using var ownedVaultHttp=vaultHttp;var view=LinkedView(_id);var taskId=view.Task.Id;var completionId=view.Completion.Id;
        var eligibility=new WorkspaceLinkedRecoveryEligibility(_id,view.Revision,taskId,completionId,false,"original_recovery_pending",null,"Original command found",tamper?view with {Completion=view.Completion with {Id=Guid.NewGuid()}}:view);
        using var http=new HttpClient(new Handler((request,_)=>Task.FromResult(request.RequestUri!.AbsolutePath=="/connect"?Connection():new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(WorkspaceTaskApiJson.Serialize(eligibility),Encoding.UTF8,"application/json")})));
        var client=new WorkspaceCloudClient(http,vault,"https://workspace.invalid/");await client.ConnectAsync(Credentials,default);var response=await client.ReadLinkedRecoveryEligibilityAsync(taskId,completionId,default);Assert.Equal(!tamper,response.Succeeded);if(!tamper)Assert.Equal(view.CommandId,response.Value!.OriginalCommand!.CommandId);
    }    [Theory][InlineData("executor_consent_required",true)][InlineData("recovery_review_expired_or_changed",true)][InlineData("<private task body>",false)]
    public async Task Linked_recovery_refusal_exposes_only_bounded_server_code(string code,bool visible)
    {
        var vault=Vault(out var vaultHttp);using var ownedVaultHttp=vaultHttp;var view=LinkedView(_id);
        using var http=new HttpClient(new Handler((request,_)=>Task.FromResult(request.RequestUri!.AbsolutePath=="/connect"?Connection():new HttpResponseMessage(HttpStatusCode.Conflict){Content=new StringContent(System.Text.Json.JsonSerializer.Serialize(new {error=code}),Encoding.UTF8,"application/json")})));
        var client=new WorkspaceCloudClient(http,vault,"https://workspace.invalid/");await client.ConnectAsync(Credentials,default);var response=await client.AcceptLinkedRecoveryAsync(view,default);Assert.False(response.Succeeded);Assert.Equal(visible,response.Message.Contains(code,StringComparison.Ordinal));Assert.Contains("original",response.Message);Assert.False(response.OutcomeUnknown);
    }

}
