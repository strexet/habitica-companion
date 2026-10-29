using System.Net;
using System.Text;
using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Integration;

namespace Habitica.WebApp.Tests.Integration;

public sealed partial class WorkspaceCloudClientTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Native_review_preview_requires_the_exact_requested_cue_and_server_receipt_stays_non_scoring(bool tamper)
    {
        var vault=Vault(out var vaultHttp);using var ownedVaultHttp=vaultHttp;var task=Guid.NewGuid();var requestId=Guid.NewGuid();
        var intent=new WorkspaceCommand(Guid.NewGuid(),_id,10,WorkspaceOperation.ReviewTask,task,new(Review:new(TaskReviewAction.Set,"UTC",DateTimeOffset.Parse("2099-11-01T09:00:00Z"))));
        var command=intent with{Id=Guid.NewGuid()};if(tamper)command=command with{Patch=new(Review:command.Patch!.Review! with{ReviewAfterUtc=DateTimeOffset.Parse("2099-11-02T09:00:00Z")})};
        var preview=new WorkspaceCommandPreview(command.Id,requestId,DateTimeOffset.UtcNow.AddMinutes(5),false,command,false,false);var accepted=0;
        using var http=new HttpClient(new Handler(async(request,ct)=>
        {
            if(request.RequestUri!.AbsolutePath=="/connect")return Connection();
            using var json=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));var root=json.RootElement;
            Assert.Equal(Session,request.Headers.Authorization!.Parameter);
            if(root.GetProperty("tool").GetString()=="prepare_task_command")
            {
                Assert.Equal("reviewTask",root.GetProperty("arguments").GetProperty("operation").GetString());Assert.Equal("set",root.GetProperty("arguments").GetProperty("patch").GetProperty("review").GetProperty("action").GetString());
                return new(HttpStatusCode.OK){Content=new StringContent(WorkspaceTaskApiJson.Serialize(preview),Encoding.UTF8,"application/json")};
            }
            Assert.Equal("review_task",root.GetProperty("tool").GetString());Assert.Equal(command.Id,root.GetProperty("arguments").GetProperty("commandId").GetGuid());accepted++;
            return Json(new WorkspaceMutationReceipt("reviewTask",task,command.Id,requestId,"Review task",true,"notRequested",1,0,0,null,"none",null,true,false));
        }));
        var client=new WorkspaceCloudClient(http,vault,"https://workspace.invalid/");Assert.True((await client.ConnectAsync(Credentials,default)).Succeeded);
        var result=await client.PrepareCommandAsync(requestId,intent,default);Assert.Equal(!tamper,result.Succeeded);
        if(!tamper){var receipt=await client.AcceptCommandAsync(result.Value!,default);Assert.True(receipt.Succeeded);Assert.Equal(1,receipt.Value!.AcceptedEventCount);Assert.Equal(0,receipt.Value.TotalProjectionCount);}
        Assert.Equal(tamper?0:1,accepted);
    }
    [Fact]
    public async Task A_review_receipt_cannot_claim_scoring_or_omit_the_causal_review_action()
    {
        var vault=Vault(out var vaultHttp);using var ownedVaultHttp=vaultHttp;var command=new WorkspaceCommand(Guid.NewGuid(),_id,10,WorkspaceOperation.ReviewTask,Guid.NewGuid(),new(Review:new(TaskReviewAction.MarkReviewed,"UTC")));var preview=new WorkspaceCommandPreview(command.Id,Guid.NewGuid(),DateTimeOffset.UtcNow.AddMinutes(5),false,command,false,false);
        var response=new WorkspaceMutationReceipt("reviewTask",command.TaskId,command.Id,preview.CorrelationId,"Review task",true,"notRequested",0,0,0,null,"none",null,true,false);
        using var http=new HttpClient(new Handler((request,_)=>Task.FromResult(request.RequestUri!.AbsolutePath=="/connect"?Connection():Json(response))));var client=new WorkspaceCloudClient(http,vault,"https://workspace.invalid/");await client.ConnectAsync(Credentials,default);
        Assert.False((await client.AcceptCommandAsync(preview,default)).Succeeded);response=response with{AcceptedEventCount=1,TotalProjectionCount=1,HabiticaProjectionStatus="pending"};Assert.False((await client.AcceptCommandAsync(preview,default)).Succeeded);
    }
}
