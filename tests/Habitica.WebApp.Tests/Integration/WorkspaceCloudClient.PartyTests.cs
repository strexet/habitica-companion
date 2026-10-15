using System.Net;
using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.WebApp.Integration;

namespace Habitica.WebApp.Tests.Integration;

public sealed partial class WorkspaceCloudClientTests
{
    [Fact]
    public async Task PartyConsentUsesTheReviewedPartyAndContributionExportUsesManagementIdentityAfterDisconnect()
    {
        var vault = Vault(out var vaultHttp); using var ownedVaultHttp = vaultHttp; var party = Guid.NewGuid(); var consentCalls = 0;
        using var http = new HttpClient(new Handler(async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath == "/connect") return Connection();
            if (request.RequestUri.AbsolutePath == "/party/consent")
            {
                consentCalls++; Assert.Equal(Session, request.Headers.Authorization!.Parameter);
                var body = await request.Content!.ReadAsStringAsync(token); Assert.DoesNotContain(Credentials.ApiToken, body);
                using var doc = JsonDocument.Parse(body); Assert.Equal(party, doc.RootElement.GetProperty("expectedPartyId").GetGuid());
                return Json(new { consented = true });
            }
            Assert.Equal("/party/contributions", request.RequestUri.AbsolutePath); Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(Management, request.Headers.Authorization!.Parameter); Assert.Null(request.Content);
            return Json(new { contributions = Array.Empty<object>(), commands = Array.Empty<object>(), apiToken = Credentials.ApiToken });
        }));
        var client = new WorkspaceCloudClient(http, vault, "https://workspace.invalid/", true); await client.ConnectAsync(Credentials, default);
        Assert.False((await client.ManagePartyAsync(new(PartyPlanningAction.Consent), default)).Succeeded); Assert.Equal(0, consentCalls);
        Assert.True((await client.ManagePartyAsync(new(PartyPlanningAction.Consent, ExpectedPartyId: party), default)).Succeeded); Assert.Equal(1, consentCalls);
        client.ForgetSession(); var exported = await client.ManagePartyAsync(new(PartyPlanningAction.ExportOwn), default);
        Assert.True(exported.Succeeded); Assert.DoesNotContain(Credentials.ApiToken, exported.ExportJson); Assert.DoesNotContain("apiToken", exported.ExportJson);
    }
    [Fact]
    public async Task PartyClientDiscardsLateViewsAndRedactsServerFailures()
    {
        var vault = Vault(out var vaultHttp); using var ownedVaultHttp = vaultHttp;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new Handler(async (request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/connect") return Connection();
            entered.SetResult(); await release.Task;
            return Json(new { party = new { id = Guid.NewGuid(), name = "Private late party", memberCount = 2 }, memberRole = "leader", retrievedAtUtc = DateTimeOffset.UtcNow, provenance = "habitica-verified-party" });
        }));
        var client = new WorkspaceCloudClient(http, vault, "https://workspace.invalid/", true); await client.ConnectAsync(Credentials, default);
        var pending = client.ManagePartyAsync(new(PartyPlanningAction.Context), default); await entered.Task; client.ForgetSession(); release.SetResult();
        var result = await pending; Assert.False(result.Succeeded); Assert.Null(result.View); Assert.DoesNotContain("Private late party", result.Message);
        using var failed = new HttpClient(new Handler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath == "/connect" ? Connection() : new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent(Credentials.ApiToken) })));
        var second = new WorkspaceCloudClient(failed, vault, "https://workspace.invalid/", true); await second.ConnectAsync(Credentials, default);
        var error = await second.ManagePartyAsync(new(PartyPlanningAction.Context), default); Assert.False(error.Succeeded); Assert.DoesNotContain(Credentials.ApiToken, error.Message);
    }
}
