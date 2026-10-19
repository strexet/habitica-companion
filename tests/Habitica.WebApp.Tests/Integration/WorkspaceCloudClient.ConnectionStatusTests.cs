using System.Net;
using Habitica.WebApp.Integration;

namespace Habitica.WebApp.Tests.Integration;

public sealed partial class WorkspaceCloudClientTests
{
    private HttpResponseMessage VerifiedConnection(bool enabled) => Json(new
    {
        workspaceId = _id, sessionToken = Session, expiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
        consentVersion = "workspace-v1", executorEnabled = enabled, executorAvailable = true
    }, HttpStatusCode.Created);
    private HttpResponseMessage ServerStatus(bool enabled) => Json(new
    {
        document = new { id = _id }, storeVersion = 1, executorEnabled = enabled, executorAvailable = true
    });

    [Fact]
    public async Task Independent_tabs_require_explicit_connection_and_reread_actual_executor_state()
    {
        var enabled = true; var connects = 0; var reads = 0;
        var vault = Vault(out var vaultHttp); using var owner = vaultHttp;
        using var http = new HttpClient(new Handler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/connect") { connects++; return Task.FromResult(VerifiedConnection(enabled)); }
            Assert.Equal(HttpMethod.Get, request.Method); Assert.Equal("/workspace", request.RequestUri.AbsolutePath);
            Assert.Equal(Session, request.Headers.Authorization!.Parameter); reads++; return Task.FromResult(ServerStatus(enabled));
        }));
        var a = new WorkspaceCloudClient(http, vault, "https://workspace.invalid/");
        Assert.True((await a.ConnectAsync(Credentials, default)).Succeeded); Assert.True(a.ConnectionStatusVerified); Assert.True(a.ExecutorEnabled);
        var b = new WorkspaceCloudClient(http, vault, "https://workspace.invalid/");
        Assert.Null(b.WorkspaceId); Assert.False(b.ConnectionStatusVerified);
        Assert.False((await b.RefreshConnectionStatusAsync(default)).Succeeded); Assert.Equal(0, reads);
        Assert.True((await b.ConnectAsync(Credentials, default)).Succeeded); Assert.True(b.ExecutorEnabled); Assert.Equal(a.WorkspaceId,b.WorkspaceId);
        enabled = false;
        Assert.True((await a.RefreshConnectionStatusAsync(default)).Succeeded);
        Assert.True(a.ConnectionStatusVerified); Assert.False(a.ExecutorEnabled); Assert.Equal(2, connects); Assert.Equal(1, reads);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task Failed_status_never_labels_the_retained_executor_value_as_verified(HttpStatusCode status)
    {
        var vault = Vault(out var vaultHttp); using var owner = vaultHttp;
        using var http = new HttpClient(new Handler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath == "/connect"
            ? VerifiedConnection(true) : new HttpResponseMessage(status) { Content = new StringContent("private raw response") })));
        var client = new WorkspaceCloudClient(http, vault, "https://workspace.invalid/");
        await client.ConnectAsync(Credentials, default); Assert.True(client.ExecutorEnabled);
        var result = await client.RefreshConnectionStatusAsync(default);
        Assert.False(result.Succeeded); Assert.False(client.ConnectionStatusVerified);
        Assert.DoesNotContain("private raw response",result.Message);
        if (status == HttpStatusCode.Unauthorized) Assert.Null(client.WorkspaceId);
    }

    [Theory]
    [InlineData("{\"document\":{\"id\":\"00000000-0000-4000-8000-000000000099\"},\"storeVersion\":1,\"executorEnabled\":false,\"executorAvailable\":true}")]
    [InlineData("{\"document\":{},\"storeVersion\":1,\"executorAvailable\":true}")]
    [InlineData("{\"document\":{},\"executorEnabled\":false,\"executorAvailable\":true}")]
    public async Task Incomplete_or_wrong_workspace_status_remains_unknown(string json)
    {
        var vault = Vault(out var vaultHttp); using var owner = vaultHttp;
        using var http = new HttpClient(new Handler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath == "/connect"
            ? VerifiedConnection(true) : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) })));
        var client = new WorkspaceCloudClient(http, vault, "https://workspace.invalid/");await client.ConnectAsync(Credentials,default);
        Assert.False((await client.RefreshConnectionStatusAsync(default)).Succeeded);Assert.False(client.ConnectionStatusVerified);
    }

    [Fact]
    public async Task Late_status_cannot_overwrite_a_replacement_tab_session()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var vault = Vault(out var vaultHttp); using var owner = vaultHttp;
        using var http = new HttpClient(new Handler((request, _) =>
        {
            if(request.RequestUri!.AbsolutePath == "/connect")return Task.FromResult(VerifiedConnection(false));
            entered.SetResult();return response.Task;
        }));
        var client = new WorkspaceCloudClient(http,vault,"https://workspace.invalid/");await client.ConnectAsync(Credentials,default);
        var pending = client.RefreshConnectionStatusAsync(default);await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        client.ForgetSession();await client.ConnectAsync(Credentials,default);
        response.SetResult(ServerStatus(true));Assert.False((await pending.WaitAsync(TimeSpan.FromSeconds(3))).Succeeded);
        Assert.True(client.ConnectionStatusVerified);Assert.False(client.ExecutorEnabled);
    }
}
