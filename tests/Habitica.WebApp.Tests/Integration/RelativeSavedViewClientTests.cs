using System.Net;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Integration;
namespace Habitica.WebApp.Tests.Integration;
public sealed class RelativeSavedViewClientTests
{
    private sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> respond):HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>respond(request);}
    private static HttpResponseMessage Json(object value)=>new(HttpStatusCode.OK){Content=new StringContent(WorkspaceTaskApiJson.Serialize(value),System.Text.Encoding.UTF8,"application/json")};
    [Fact]public async Task Relative_transport_binds_day_cursor_and_refuses_false_or_missing_interpreted_bounds()
    {
        var workspace=Guid.NewGuid();var view=Guid.NewGuid();var day=new DateOnly(2026,11,3);var query=new WorkspaceSavedTaskQuery(WorkspaceSavedTaskSource.Tasks,new(PlannedRelative:WorkspaceRelativeDatePreset.Today),"UTC");var evaluation=WorkspaceRelativeDates.Evaluate(query,DateTimeOffset.Parse("2026-11-03T12:00:00Z"))!;var page=new WorkspaceSavedViewTaskPage("companion-canonical",workspace,view,10,[],0,false,0,null,evaluation);int mode=0,calls=0;
        using var vaultHttp=new HttpClient(new Handler(_=>Task.FromResult(Json(new{profileId="prf_"+new string('a',43),managementToken=new string('m',43)}))));var vault=new ChatGptIntegrationClient(vaultHttp,"https://auth.invalid/");using var http=new HttpClient(new Handler(async request=>{if(request.RequestUri!.AbsolutePath=="/connect")return Json(new{workspaceId=workspace,sessionToken=new string('s',43),consentVersion="workspace-v1",expiresAtUtc=DateTimeOffset.UtcNow.AddHours(1)});using var data=System.Text.Json.JsonDocument.Parse(await request.Content!.ReadAsStringAsync());Assert.Equal("2026-11-03",data.RootElement.GetProperty("arguments").GetProperty("expectedEvaluationDay").GetString());Assert.False(data.RootElement.GetProperty("arguments").TryGetProperty("asOfUtc",out _));calls++;return Json(mode switch{1=>page with{Evaluation=evaluation with{Planned=evaluation.Planned! with{Through=day.AddDays(1)}}},2=>page with{Evaluation=null},3=>page with{Evaluation=evaluation with{TimeZoneId="Missing/Zone"}},_=>page});}));var client=new WorkspaceCloudClient(http,vault,"https://workspace.invalid/");await client.ConnectAsync(new(Guid.NewGuid().ToString("D"),"synthetic-only"),default);Assert.True((await client.QuerySavedTaskViewAsync(view,new(ExpectedRevision:10),default,day)).Succeeded);foreach(var bad in new[]{1,2,3}){mode=bad;Assert.False((await client.QuerySavedTaskViewAsync(view,new(ExpectedRevision:10),default,day)).Succeeded);}Assert.Equal(4,calls);
    }
}
