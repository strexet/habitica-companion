using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Habitica.Domain.Auth;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Integration;

namespace Habitica.WebApp.Tests.Integration;

public sealed class WorkspaceCloudClientTests
{
    private const string Management = "mmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmm";
    private const string Session = "sssssssssssssssssssssssssssssssssssssssssss";
    private readonly Guid _id = Guid.NewGuid();
    private static readonly HabiticaCredentials Credentials = new("00000000-0000-4000-8000-000000000011", "synthetic-habitica-token");
    private HttpResponseMessage Connection() => Json(new { workspaceId = _id, sessionToken = Session, expiresAtUtc = DateTimeOffset.UtcNow.AddHours(1), consentVersion = "workspace-v1" }, HttpStatusCode.Created);
    private static ChatGptIntegrationClient Vault(out HttpClient http)
    {
        http = new HttpClient(new Handler(async (request, cancellation) =>
        {
            Assert.Equal("/enroll", request.RequestUri!.AbsolutePath);
            using var value = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellation));
            Assert.Equal("integration-v1", value.RootElement.GetProperty("consentVersion").GetString());
            return Json(new { profileId = "prf_" + new string('a', 43), managementToken = Management }, HttpStatusCode.Created);
        }));
        return new ChatGptIntegrationClient(http, "https://auth.invalid/", linkingEnabled: false);
    }

    [Fact]
    public async Task Workspace_connection_is_independent_of_ChatGPT_and_uses_separate_management_and_session_tokens()
    {
        var vault = Vault(out var vaultHttp); using var ownedVaultHttp = vaultHttp;
        var calls = 0;
        using var http = new HttpClient(new Handler(async (request, cancellation) =>
        {
            calls++;
            Assert.Equal("workspace.invalid", request.RequestUri!.Host);
            Assert.DoesNotContain(Credentials.ApiToken, request.RequestUri.ToString());
            if (request.RequestUri.AbsolutePath == "/connect")
            {
                Assert.Equal(Management, request.Headers.Authorization!.Parameter);
                var body = await request.Content!.ReadAsStringAsync(cancellation);
                Assert.Contains("workspace-v1", body); Assert.DoesNotContain(Credentials.ApiToken, body);
                return Connection();
            }
            Assert.Equal(Session, request.Headers.Authorization!.Parameter);
            return Json(new { disconnected = true, retained = true });
        }));
        var client = new WorkspaceCloudClient(http, vault, "https://workspace.invalid/");
        Assert.False(vault.IsAvailable); Assert.True(client.IsAvailable);
        var connected = await client.ConnectAsync(Credentials, CancellationToken.None);
        Assert.True(connected.Succeeded); Assert.Equal(_id, client.WorkspaceId);
        Assert.DoesNotContain(Management, JsonSerializer.Serialize(connected)); Assert.DoesNotContain(Session, JsonSerializer.Serialize(connected));
        Assert.True((await client.DisconnectAsync(false, CancellationToken.None)).Succeeded);
        Assert.Null(client.WorkspaceId); Assert.True(vault.HasConnection); Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Late_connection_response_cannot_restore_a_forgotten_workspace_session()
    {
        var vault = Vault(out var vaultHttp); using var ownedVaultHttp = vaultHttp;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new Handler(async (_, _) => { entered.SetResult(); await release.Task; return Connection(); }));
        var client = new WorkspaceCloudClient(http, vault, "https://workspace.invalid/");
        var pending = client.ConnectAsync(Credentials, CancellationToken.None);
        await entered.Task; client.ForgetSession(); vault.ForgetSession(); release.SetResult();
        Assert.False((await pending).Succeeded); Assert.Null(client.WorkspaceId); Assert.False(vault.HasConnection);
    }

    [Fact]
    public async Task Export_rejects_another_workspace_and_does_not_expose_raw_error_data()
    {
        var vault = Vault(out var vaultHttp); using var ownedVaultHttp = vaultHttp;
        using var http = new HttpClient(new Handler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath == "/connect" ? Connection() : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(WorkspaceJournalJson.Serialize(new(new WorkspaceDocument { Id = Guid.NewGuid() }, 1, []))) })));
        var client = new WorkspaceCloudClient(http, vault, "https://workspace.invalid/");
        await client.ConnectAsync(Credentials, CancellationToken.None);
        var result = await client.ExportAsync(CancellationToken.None);
        Assert.False(result.Succeeded); Assert.Null(result.Json);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("http://workspace.invalid/")]
    [InlineData("https://workspace.invalid/?secret=x")]
    public async Task Unsafe_workspace_configuration_never_enrolls_credentials(string? url)
    {
        var calls = 0;
        using var http = new HttpClient(new Handler((_, _) => { calls++; throw new InvalidOperationException(); }));
        var client = new WorkspaceCloudClient(http, new ChatGptIntegrationClient(http, "https://auth.invalid/"), url);
        Assert.False((await client.ConnectAsync(Credentials, CancellationToken.None)).Succeeded); Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Executor_requires_validated_response_and_late_result_cannot_restore_session_state()
    {
        var vault = Vault(out var vaultHttp); using var ownedVaultHttp = vaultHttp;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new Handler(async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath == "/connect") return Connection();
            Assert.Equal("/executor", request.RequestUri.AbsolutePath);
            Assert.Equal(Session, request.Headers.Authorization!.Parameter);
            var body = await request.Content!.ReadAsStringAsync(token); Assert.Contains("executor-v1", body); Assert.DoesNotContain(Credentials.ApiToken, body);
            entered.SetResult(); await release.Task; return Json(new { executorEnabled = true, executorAvailable = true });
        }));
        var client = new WorkspaceCloudClient(http, vault, "https://workspace.invalid/");
        await client.ConnectAsync(Credentials, default);
        var pending = client.SetExecutorAsync(true, default); await entered.Task; client.ForgetSession(); release.SetResult();
        Assert.False((await pending).Succeeded); Assert.False(client.ExecutorEnabled); Assert.Null(client.WorkspaceId);
    }

    [Fact]
    public async Task Transfer_accepts_only_matching_frozen_receipt_and_updates_the_cloud_identity()
    {
        var vault = Vault(out var vaultHttp); using var ownedVaultHttp = vaultHttp;
        var localId = Guid.NewGuid(); var transferId = Guid.NewGuid();
        var snapshot = new WorkspaceJournalSnapshot(new WorkspaceDocument { Id = localId }, 2, [], [], new(WorkspaceAuthorityMode.Frozen, transferId));
        using var http = new HttpClient(new Handler(async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath == "/connect") return Connection();
            Assert.Equal("/handoff", request.RequestUri.AbsolutePath); Assert.Equal(Session, request.Headers.Authorization!.Parameter);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            Assert.Equal("workspace-handoff-v1", body.RootElement.GetProperty("consentVersion").GetString());
            Assert.Equal(transferId, body.RootElement.GetProperty("transferId").GetGuid());
            Assert.Equal(localId, WorkspaceJournalJson.Deserialize(body.RootElement.GetProperty("archive").GetString()!).Document.Id);
            return Json(new { accepted = true, transferId, workspaceId = localId, executorEnabled = false }, HttpStatusCode.Created);
        }));
        var client = new WorkspaceCloudClient(http, vault, "https://workspace.invalid/"); await client.ConnectAsync(Credentials, default);
        Assert.True((await client.HandoffAsync(WorkspaceJournalJson.Serialize(snapshot), default)).Succeeded);
        Assert.Equal(localId, client.WorkspaceId); Assert.False(client.ExecutorEnabled);
        Assert.False((await client.HandoffAsync(WorkspaceJournalJson.Serialize(snapshot with { Authority = null }), default)).Succeeded);
    }

    [Fact]
    public async Task LateRenewalCannotRestoreCloudAccessAfterTheTabSessionIsForgotten()
    {
        var vault = Vault(out var vaultHttp); using var ownedVaultHttp = vaultHttp;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var calls = 0;
        using var http = new HttpClient(new Handler(async (_, _) =>
        {
            if (++calls == 1) return Connection();
            entered.SetResult(); await release.Task; return Connection();
        }));
        var client = new WorkspaceCloudClient(http, vault, "https://workspace.invalid/"); Assert.True((await client.ConnectAsync(Credentials, default)).Succeeded);
        var renewed = client.ConnectAsync(Credentials, default); await entered.Task; client.ForgetSession(); release.SetResult();
        Assert.False((await renewed).Succeeded); Assert.Null(client.WorkspaceId); Assert.False(client.ExecutorEnabled);
    }

    private static HttpResponseMessage Json(object value, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = JsonContent.Create(value) };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => response(request, cancellationToken);
    }
}
