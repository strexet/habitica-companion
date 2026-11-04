using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Habitica.Application.Integration;
using Habitica.Domain.Auth;
using Habitica.Domain.Notes;
using Habitica.WebApp.Integration;

namespace Habitica.WebApp.Tests.Integration;

public sealed class ConnectionRecoveryTests
{
    private const string Profile = "prf_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Management = "mmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmm";
    private const string Session = "sssssssssssssssssssssssssssssssssssssssssss";
    private static readonly HabiticaCredentials Credentials = new("00000000-0000-4000-8000-000000000011", "fixture-private-key");
    private static readonly IntegrationRecoveryDescriptor Descriptor = new("integration-recovery-v1", Profile, Guid.NewGuid());
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => action(request, token); }
    private static HttpResponseMessage Json(object value, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = JsonContent.Create(value) };
    private static HttpResponseMessage Recovered(IntegrationRecoveryDescriptor? descriptor = null, DateTimeOffset? expires = null) => Json(new
    { profileId = Profile, managementToken = Management, recovered = true, descriptor = descriptor ?? Descriptor, expiresAtUtc = expires ?? DateTimeOffset.UtcNow.AddHours(8) });

    [Fact]
    public async Task No_descriptor_or_bootstrap_acknowledgement_sends_no_credentials()
    {
        var calls = 0;
        using var http = new HttpClient(new Handler((_, _) => { calls++; return Task.FromResult(Recovered()); }));
        var client = new ChatGptIntegrationClient(http, "https://auth.invalid/");
        Assert.Equal("recovery_acknowledgement_required", (await client.RecoverAsync(Credentials, null, false, default)).Code);
        Assert.Equal("recovery_descriptor_stale", (await client.RecoverAsync(Credentials, Descriptor with { ConnectionId = Guid.Empty }, false, default)).Code);
        Assert.Equal(0, calls); Assert.False(client.HasConnection);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Existing_only_recovery_supplies_management_access_without_enrollment_or_link_ticket(bool legacy)
    {
        var paths = new List<string>();
        using var http = new HttpClient(new Handler(async (request, token) =>
        {
            paths.Add(request.RequestUri!.AbsolutePath);
            if (request.RequestUri.AbsolutePath == "/recover")
            {
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
                Assert.Equal(Credentials.ApiToken, body.RootElement.GetProperty("apiToken").GetString());
                Assert.False(body.RootElement.TryGetProperty("consentVersion", out _));
                Assert.Equal(!legacy, body.RootElement.TryGetProperty("descriptor", out var descriptor));
                if (!legacy) Assert.False(descriptor.TryGetProperty("isValid", out _));
                return Recovered();
            }
            Assert.Equal("/profiles/" + Profile + "/status", request.RequestUri.AbsolutePath);
            Assert.Equal(Management, request.Headers.Authorization!.Parameter);
            return Json(new { permissions = new[] { "tasks:read" }, credentialStatus = "last_verified" });
        }));
        var client = new ChatGptIntegrationClient(http, "https://auth.invalid/", linkingEnabled: false);
        var result = await client.RecoverAsync(Credentials, legacy ? null : Descriptor, legacy, default);
        Assert.True(result.Succeeded); Assert.True(client.HasConnection); Assert.Equal(Descriptor, client.RecoveryDescriptor);
        Assert.Equal(new[] { "tasks:read" }, (await client.GetStatusAsync(default)).Permissions);
        Assert.Equal(new[] { "/recover", "/profiles/" + Profile + "/status" }, paths);
        Assert.DoesNotContain(Management, JsonSerializer.Serialize(result)); Assert.DoesNotContain(Credentials.ApiToken, JsonSerializer.Serialize(result));
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("foreign")]
    public async Task Invalid_recovery_response_cannot_establish_access(string scenario)
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Recovered(
            scenario == "foreign" ? Descriptor with { ConnectionId = Guid.NewGuid() } : Descriptor,
            scenario == "expired" ? DateTimeOffset.UtcNow.AddMinutes(-1) : scenario == "future" ? DateTimeOffset.UtcNow.AddDays(1) : null))));
        var client = new ChatGptIntegrationClient(http, "https://auth.invalid/");
        Assert.False((await client.RecoverAsync(Credentials, Descriptor, false, default)).Succeeded); Assert.False(client.HasConnection);
    }

    [Fact]
    public async Task Delayed_recovery_cannot_resurrect_forgotten_account_access()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new Handler((_, _) => { entered.SetResult(); return reply.Task; }));
        var client = new ChatGptIntegrationClient(http, "https://auth.invalid/");
        var pending = client.RecoverAsync(Credentials, Descriptor, false, default); await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        client.ForgetSession(); reply.SetResult(Recovered());
        Assert.Equal("connection_changed", (await pending.WaitAsync(TimeSpan.FromSeconds(3))).Code); Assert.False(client.HasConnection);
    }

    [Theory]
    [InlineData("existing_connection_revoked")]
    [InlineData("integration_consent_unsupported")]
    [InlineData("try_later")]
    [InlineData("fixture-private-key")]
    public async Task Failed_recovery_retains_safe_state_codes_without_copying_error_content(string code)
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Json(new { error = code, detail = Credentials.ApiToken }, HttpStatusCode.Forbidden))));
        var client = new ChatGptIntegrationClient(http, "https://auth.invalid/");
        var result = await client.RecoverAsync(Credentials, Descriptor, false, default);
        Assert.Equal(code == Credentials.ApiToken ? "recovery_unavailable" : code, result.Code);
        Assert.DoesNotContain(Credentials.ApiToken, JsonSerializer.Serialize(result)); Assert.False(client.HasConnection);
    }

    [Fact]
    public async Task Workspace_and_Notes_recover_same_records_without_connect_or_enroll()
    {
        var workspaceId = Guid.NewGuid(); var notebookId = Guid.NewGuid(); var paths = new List<string>();
        using var http = new HttpClient(new Handler(async (request, token) =>
        {
            paths.Add(request.RequestUri!.Host + request.RequestUri.AbsolutePath);
            if (request.RequestUri.Host == "auth.invalid") return Recovered();
            Assert.Equal(Management, request.Headers.Authorization!.Parameter);
            var body = await request.Content!.ReadAsStringAsync(token); Assert.DoesNotContain(Credentials.ApiToken, body); Assert.DoesNotContain("consentVersion", body);
            if (request.RequestUri.AbsolutePath == "/recover") return Json(new
            { workspaceId, sessionToken = Session, expiresAtUtc = DateTimeOffset.UtcNow.AddHours(8), consentVersion = "workspace-v1", executorEnabled = true, executorAvailable = false, recovered = true });
            Assert.Equal("/notes/recover", request.RequestUri.AbsolutePath);
            return Json(new { notebookId, ownerScope = NotesModel.Scope(Profile), accessEpoch = Guid.NewGuid(), sessionToken = Session,
                expiresAtUtc = DateTimeOffset.UtcNow.AddHours(8), consentVersion = "notes-v1", recovered = true });
        }));
        var vault = new ChatGptIntegrationClient(http, "https://auth.invalid/");
        var workspace = new WorkspaceCloudClient(http, vault, "https://workspace.invalid/");
        var notes = new NotesCloudClient(http, vault, "https://workspace.invalid/");
        Assert.False((await workspace.RecoverConnectionAsync(null, default)).Succeeded); Assert.False((await notes.RecoverConnectionAsync(null, default)).Succeeded); Assert.Empty(paths);
        Assert.True((await vault.RecoverAsync(Credentials, Descriptor, false, default)).Succeeded);
        Assert.True((await workspace.RecoverConnectionAsync(workspaceId, default)).Succeeded); Assert.True(workspace.ConnectionStatusVerified); Assert.True(workspace.ExecutorEnabled); Assert.False(workspace.ExecutorAvailable);
        Assert.True((await notes.RecoverConnectionAsync(notebookId, default)).Succeeded); Assert.True(notes.IsConnected);
        Assert.Equal(workspaceId, workspace.WorkspaceId); Assert.Equal(notebookId, notes.NotebookId);
        Assert.Equal(new[] { "auth.invalid/recover", "workspace.invalid/recover", "workspace.invalid/notes/recover" }, paths);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Late_cloud_recovery_is_discarded_after_vault_or_store_session_change(bool notesSource)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new Handler((request, _) =>
        {
            if (request.RequestUri!.Host == "auth.invalid") return Task.FromResult(Recovered());
            entered.SetResult(); return reply.Task;
        }));
        var vault = new ChatGptIntegrationClient(http, "https://auth.invalid/"); await vault.RecoverAsync(Credentials, Descriptor, false, default);
        var workspace = new WorkspaceCloudClient(http, vault, "https://workspace.invalid/"); var notes = new NotesCloudClient(http, vault, "https://workspace.invalid/");
        var pending = notesSource ? RecoverNote() : RecoverWorkspace();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); vault.ForgetSession(); workspace.ForgetSession(); notes.ForgetSession();
        reply.SetResult(Json(new { recovered = true }));
        Assert.False(await pending.WaitAsync(TimeSpan.FromSeconds(3))); Assert.Null(workspace.WorkspaceId); Assert.False(notes.IsConnected);
        async Task<bool> RecoverNote() => (await notes.RecoverConnectionAsync(null, default)).Succeeded;
        async Task<bool> RecoverWorkspace() => (await workspace.RecoverConnectionAsync(null, default)).Succeeded;
    }
}
