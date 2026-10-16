using System.Net;
using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.WebApp.Integration;

namespace Habitica.WebApp.Tests.Integration;

public sealed partial class WorkspaceCloudClientTests
{
    [Fact]
    public async Task Operational_status_returns_a_bounded_authenticated_snapshot()
    {
        var vault = Vault(out var vaultHttp); using var ownedVault = vaultHttp;
        var expected = new WorkspaceOperationalStatus(DateTimeOffset.UtcNow, 3, 2, 1, 4, 1, DateTimeOffset.UtcNow.AddHours(-1));
        using var http = new HttpClient(new Handler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/connect") return Task.FromResult(Connection());
            Assert.Equal(HttpMethod.Get, request.Method); Assert.Equal(Session, request.Headers.Authorization!.Parameter); return Task.FromResult(Json(expected));
        }));
        var client = new WorkspaceCloudClient(http, vault, "https://workspace.invalid/"); await client.ConnectAsync(Credentials, default);
        var result = await client.ReadOperationalStatusAsync(default); Assert.True(result.Succeeded); Assert.Equal(expected, result.Status);
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"checkedAtUtc\":\"2026-10-16T00:00:00Z\",\"pending\":-1,\"stalled\":0,\"uncertain\":0,\"blocked\":0,\"expiredLeases\":0}")]
    [InlineData("oversized")]
    public async Task Operational_status_rejects_missing_invalid_or_oversized_fields(string payload)
    {
        var vault = Vault(out var vaultHttp); using var ownedVault = vaultHttp;
        using var http = new HttpClient(new Handler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath == "/connect" ? Connection() : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload == "oversized" ? new string('x', 8193) : payload) })));
        var client = new WorkspaceCloudClient(http, vault, "https://workspace.invalid/"); await client.ConnectAsync(Credentials, default);
        var result = await client.ReadOperationalStatusAsync(default); Assert.False(result.Succeeded); Assert.Null(result.Status);
    }
    [Fact]
    public async Task Operational_status_uses_session_authority_and_discards_forgotten_connection_results()
    {
        var vault = Vault(out var vaultHttp); using var ownedVault = vaultHttp;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new Handler(async (request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/connect") return Connection();
            Assert.Equal(HttpMethod.Get, request.Method); Assert.Equal(Session, request.Headers.Authorization!.Parameter);
            entered.SetResult(); await release.Task; return Json(new WorkspaceOperationalStatus(DateTimeOffset.UtcNow, 2, 1, 1, 0, 0, null));
        }));
        var client = new WorkspaceCloudClient(http, vault, "https://workspace.invalid/"); await client.ConnectAsync(Credentials, default);
        var pending = client.ReadOperationalStatusAsync(default); await entered.Task; client.ForgetSession(); release.SetResult();
        var result = await pending; Assert.False(result.Succeeded); Assert.Null(result.Status);
    }
    [Fact]
    public async Task Rate_limited_status_is_not_retried_or_returned_as_an_empty_queue()
    {
        var vault = Vault(out var vaultHttp); using var ownedVault = vaultHttp; var statusCalls = 0;
        using var http = new HttpClient(new Handler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/connect") return Task.FromResult(Connection());
            statusCalls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("private diagnostic") });
        }));
        var client = new WorkspaceCloudClient(http, vault, "https://workspace.invalid/"); await client.ConnectAsync(Credentials, default);
        var result = await client.ReadOperationalStatusAsync(default); Assert.False(result.Succeeded); Assert.Null(result.Status); Assert.Equal(1, statusCalls); Assert.DoesNotContain("private diagnostic", JsonSerializer.Serialize(result));
    }
}
