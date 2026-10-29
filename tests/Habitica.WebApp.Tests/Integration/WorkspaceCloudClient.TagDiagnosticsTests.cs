using System.Net;
using Habitica.Application.Diagnostics;
using Habitica.Application.Workspace;
using Habitica.Domain.Diagnostics;
using Habitica.Domain.Workspace;
using Habitica.Storage;
using Habitica.WebApp.Integration;
namespace Habitica.WebApp.Tests.Integration;
public sealed partial class WorkspaceCloudClientTests
{
    [Fact]
    public async Task Tag_observers_retain_eight_exact_operations_modes_and_original_identity_without_raw_failures()
    {
        using var authHttp=new HttpClient(new Handler((request,_)=>Task.FromResult(request.RequestUri!.AbsolutePath=="/enroll"
            ?Json(new {profileId="prf_"+new string('a',43),managementToken=Management},HttpStatusCode.Created)
            :Json(new {enabled=request.Method==HttpMethod.Post,consentVersion="habitica-tags-v1",grantedAtUtc=(string?)null}))));
        var vault=new ChatGptIntegrationClient(authHttp,"https://auth.invalid/",false);
        using var http=new HttpClient(new Handler((request,_)=>request.RequestUri!.AbsolutePath=="/connect"?Task.FromResult(Connection()):Task.FromException<HttpResponseMessage>(new HttpRequestException("credential-canary private-tag-name"))));
        var store=new DiagnosticsLogStore(new InMemoryKeyValueStorage());var client=new WorkspaceCloudClient(http,vault,"https://workspace.invalid/",diagnostics:new DiagnosticsLogWriter(store,TimeProvider.System));await client.ConnectAsync(Credentials,default);
        var command=Guid.NewGuid();var request=Guid.NewGuid();var group=Guid.NewGuid();
        var review=new WorkspaceTagReview(command,request,_id,"reconcile",1,DateTimeOffset.UtcNow.AddMinutes(5),true,0,new(group,WorkspaceOrganizationKind.Project,"private-tag-name",null,[],"private-tag-name disclosure"));
        await client.ReadTagConsentAsync(default);await client.SetTagConsentAsync(true,default);await client.ReadGroupTagsAsync(0,null,default);
        await client.InspectTagCandidatesAsync(WorkspaceOrganizationKind.Project,group,0,null,default);await client.PrepareTagReviewAsync(new(request,"reconcile",WorkspaceOrganizationKind.Project,group),default);
        await client.AcceptTagReviewAsync(review,default);await client.ReadTagReviewAsync(command,default);await client.RecoverTagReceiptAsync(Guid.NewGuid(),default);
        var rows=(await store.GetRecentAsync(default)).Where(row=>row.Operation.StartsWith("workspace-tag-",StringComparison.Ordinal)).ToArray();
        Assert.Equal(8,rows.Length);Assert.Equal(8,rows.Select(row=>row.Operation).Distinct().Count());
        var accepted=Assert.Single(rows,row=>row.Operation=="workspace-tag-accept");Assert.Equal(command,accepted.Context!.CommandId);Assert.Equal(request,accepted.Context.CorrelationId);Assert.Equal(DiagnosticsResult.Unknown,accepted.Context.Result);Assert.Equal(DiagnosticsMode.LiveMutation,accepted.Mode);
        Assert.Equal(DiagnosticsMode.LiveRead,Assert.Single(rows,row=>row.Operation=="workspace-tag-status").Mode);Assert.Equal(DiagnosticsMode.LiveMutation,Assert.Single(rows,row=>row.Operation=="workspace-tag-consent-set").Mode);
        var exported=DiagnosticsBundleExport.Serialize(DiagnosticsBundleExport.Create(rows,new(),DateTimeOffset.UtcNow));Assert.DoesNotContain("credential-canary",exported);Assert.DoesNotContain("private-tag-name",exported);Assert.DoesNotContain(Credentials.ApiToken,exported);Assert.DoesNotContain(Management,exported);Assert.DoesNotContain(Session,exported);
    }
}
