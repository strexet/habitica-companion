using System.Net;
using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Auth;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Integration;
namespace Habitica.WebApp.Tests.Integration;
public sealed class WorkspaceSavedViewClientTests
{
    private static readonly HabiticaCredentials Credentials=new("00000000-0000-4000-8000-000000000011","synthetic-token");
    private sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> send):HttpMessageHandler {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>send(request);}
    private static HttpResponseMessage Json(object value)=>new(HttpStatusCode.OK){Content=new StringContent(WorkspaceTaskApiJson.Serialize(value),System.Text.Encoding.UTF8,"application/json")};
    private static HttpClient Vault()=>new(new Handler(_=>Task.FromResult(Json(new {profileId="prf_"+new string('p',43),managementToken=new string('m',43)}))));
    private static HttpResponseMessage Connection(Guid id)=>Json(new {workspaceId=id,sessionToken=new string('s',43),expiresAtUtc=DateTimeOffset.UtcNow.AddHours(1),consentVersion="workspace-v1"});
    private static WorkspaceSavedTaskView View()=>new(Guid.NewGuid(),1,"Inbox",new(WorkspaceSavedTaskSource.Tasks,new(ExecutionStage:TodoExecutionStage.Inbox)));
    [Fact]
    public async Task Exact_list_and_query_reads_preserve_paging_and_have_no_account_or_write_arguments()
    {
        var id=Guid.NewGuid();var view=View();var now=DateTimeOffset.UtcNow;var task=new CompanionTask(Guid.NewGuid(),1,"Native",CompanionTaskKind.Todo,CompanionTaskState.Active,CompanionPriority.Normal,now,now);var calls=0;
        using var vault=Vault();using var http=new HttpClient(new Handler(async request=>{
            if(request.RequestUri!.AbsolutePath=="/connect")return Connection(id);Assert.Equal("/task-api",request.RequestUri.AbsolutePath);var raw=await request.Content!.ReadAsStringAsync();Assert.DoesNotContain(Credentials.ApiToken,raw);using var body=JsonDocument.Parse(raw);var args=body.RootElement.GetProperty("arguments");Assert.Equal(5,args.GetProperty("limit").GetInt32());Assert.Equal(0,args.GetProperty("offset").GetInt32());Assert.False(args.TryGetProperty("expectedRevision",out _));Assert.False(args.TryGetProperty("accountId",out _));calls++;
            if(body.RootElement.GetProperty("tool").GetString()=="list_saved_task_views")return Json(new WorkspaceSavedViewList("companion-canonical",id,1,[view],1,false,0,null));Assert.Equal(view.Id,args.GetProperty("viewId").GetGuid());return Json(new WorkspaceSavedViewTaskPage("companion-canonical",id,view.Id,1,[task],1,false,0,null));
        }));var client=new WorkspaceCloudClient(http,new ChatGptIntegrationClient(vault,"https://auth.invalid/",false),"https://workspace.invalid/");await client.ConnectAsync(Credentials,default);
        var list=await client.ListSavedTaskViewsAsync(new(Limit:5),default);Assert.True(list.Succeeded);Assert.Equal(view.Id,Assert.Single(list.Value!.Items).Id);var page=await client.QuerySavedTaskViewAsync(view.Id,new(Limit:5),default);Assert.True(page.Succeeded);Assert.Equal(task.Id,Assert.Single(page.Value!.Tasks).Id);Assert.Equal(2,calls);
    }
    [Theory]
    [InlineData("profile")][InlineData("provenance")][InlineData("revision")][InlineData("count")][InlineData("truncated")][InlineData("offset")][InlineData("next")][InlineData("archived")][InlineData("query")]
    public async Task Malformed_saved_view_pages_cannot_report_current_results(string mutation)
    {
        var id=Guid.NewGuid();var view=View();var response=new WorkspaceSavedViewList("companion-canonical",id,2,[view],1,false,0,null);
        response=mutation switch {"profile"=>response with {WorkspaceId=Guid.NewGuid()},"provenance"=>response with {Provenance="cached"},"revision"=>response with {Revision=3},"count"=>response with {MatchedCount=3},"truncated"=>response with {Truncated=true},"offset"=>response with {Offset=1},"next"=>response with {NextOffset=1},"archived"=>response with {Items=[view with {IsArchived=true}]},"query"=>response with {Items=[view with {Query=null!}]},_=>response};
        using var vault=Vault();using var http=new HttpClient(new Handler(request=>Task.FromResult(request.RequestUri!.AbsolutePath=="/connect"?Connection(id):Json(response))));var client=new WorkspaceCloudClient(http,new ChatGptIntegrationClient(vault,"https://auth.invalid/",false),"https://workspace.invalid/");await client.ConnectAsync(Credentials,default);
        var result=await client.ListSavedTaskViewsAsync(new(ExpectedRevision:2),default);Assert.False(result.Succeeded);Assert.False(result.OutcomeUnknown);Assert.Null(result.Value);
    }
    [Fact]
    public async Task Query_identity_invalid_input_and_lost_connection_never_become_mutation_uncertainty()
    {
        var id=Guid.NewGuid();var view=View();var calls=0;var entered=new TaskCompletionSource();var gate=new TaskCompletionSource();
        using var vault=Vault();using var http=new HttpClient(new Handler(async request=>{if(request.RequestUri!.AbsolutePath=="/connect")return Connection(id);calls++;if(calls==1)return Json(new WorkspaceSavedViewTaskPage("companion-canonical",id,Guid.NewGuid(),1,[],0,false,0,null));entered.SetResult();await gate.Task;return Json(new WorkspaceSavedViewList("companion-canonical",id,1,[view],1,false,0,null));}));var client=new WorkspaceCloudClient(http,new ChatGptIntegrationClient(vault,"https://auth.invalid/",false),"https://workspace.invalid/");await client.ConnectAsync(Credentials,default);
        Assert.False((await client.QuerySavedTaskViewAsync(Guid.Empty,new(),default)).Succeeded);Assert.False((await client.ListSavedTaskViewsAsync(new(Offset:1),default)).Succeeded);Assert.Equal(0,calls);var wrong=await client.QuerySavedTaskViewAsync(view.Id,new(),default);Assert.False(wrong.Succeeded);Assert.False(wrong.OutcomeUnknown);
        var pending=client.ListSavedTaskViewsAsync(new(),default);await entered.Task;client.ForgetSession();gate.SetResult();var discarded=await pending;Assert.False(discarded.Succeeded);Assert.False(discarded.OutcomeUnknown);Assert.Null(discarded.Value);
    }
}
