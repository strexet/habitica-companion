using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Habitica.Domain.Auth;
using Habitica.WebApp.Integration;

namespace Habitica.WebApp.Tests.Integration;

public sealed class ChatGptIntegrationClientTests
{
    private const string ProfileId = "prf_" + "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string ManagementToken = "mmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmmm";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://external.invalid/")]
    [InlineData("https://user:secret@auth.invalid/")]
    [InlineData("https://auth.invalid/?token=secret")]
    public async Task Unavailable_or_unsafe_configuration_sends_no_credentials(string? baseUrl)
    {
        var calls = 0;
        using var http = new HttpClient(new Handler((_, _) =>
        {
            calls++;
            return Task.FromResult(Json(new { error = "unexpected" }));
        }));
        var client = new ChatGptIntegrationClient(http, baseUrl);

        Assert.False(client.IsAvailable);
        Assert.False((await client.EnrollAsync(new HabiticaCredentials("fixture-user", "fixture-token"), CancellationToken.None)).Succeeded);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Enrollment_uses_explicit_consent_and_separate_management_credential_without_exposing_secrets_in_result()
    {
        var calls = 0;
        using var http = new HttpClient(new Handler(async (request, cancellation) =>
        {
            Assert.DoesNotContain("fixture-token", request.RequestUri!.ToString());
            Assert.Equal("auth.invalid", request.RequestUri.Host);
            calls++;
            if (calls == 1)
            {
                Assert.Equal("/enroll", request.RequestUri.AbsolutePath);
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellation));
                Assert.Equal("integration-v1", body.RootElement.GetProperty("consentVersion").GetString());
                Assert.Equal("fixture-token", body.RootElement.GetProperty("apiToken").GetString());
                return Json(new { profileId = ProfileId, managementToken = ManagementToken }, HttpStatusCode.Created);
            }
            Assert.Equal($"/profiles/{ProfileId}/linking-ticket", request.RequestUri.AbsolutePath);
            Assert.Equal(ManagementToken, request.Headers.Authorization!.Parameter);
            return Ticket();
        }));
        var client = new ChatGptIntegrationClient(http, "https://auth.invalid/");

        var result = await client.EnrollAsync(new HabiticaCredentials("fixture-user", "fixture-token"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(client.HasConnection);
        Assert.Equal("0123456789AB", result.Code);
        Assert.Equal(2, calls);
        Assert.DoesNotContain("fixture-token", JsonSerializer.Serialize(result));
        Assert.DoesNotContain(ManagementToken, JsonSerializer.Serialize(result));
    }

    [Fact]
    public async Task Error_responses_never_copy_secret_bearing_bodies_into_user_messages()
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Json(new { error = "fixture-private-token" }, HttpStatusCode.BadRequest))));
        var client = new ChatGptIntegrationClient(http, "https://auth.invalid/");

        var result = await client.EnrollAsync(new HabiticaCredentials("fixture-user", "fixture-private-token"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.DoesNotContain("fixture-private-token", result.Message);
        Assert.False(client.HasConnection);
    }

    [Fact]
    public async Task Forgotten_session_cannot_restore_a_management_credential_from_a_late_enrollment_response()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var http = new HttpClient(new Handler(async (_, _) =>
        {
            calls++;
            entered.SetResult();
            await release.Task;
            return Json(new { profileId = ProfileId, managementToken = ManagementToken }, HttpStatusCode.Created);
        }));
        var client = new ChatGptIntegrationClient(http, "https://auth.invalid/");
        var pending = client.EnrollAsync(new HabiticaCredentials("fixture-user", "fixture-token"), CancellationToken.None);
        await entered.Task;
        client.ForgetSession();
        release.SetResult();
        var result = await pending;

        Assert.False(result.Succeeded);
        Assert.False(client.HasConnection);
        Assert.Null(result.Code);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Pending_cleanup_retains_only_local_management_access_for_explicit_retry()
    {
        var calls = 0;
        using var http = new HttpClient(new Handler((_, _) =>
        {
            calls++;
            return Task.FromResult(calls switch
            {
                1 => Json(new { profileId = ProfileId, managementToken = ManagementToken }, HttpStatusCode.Created),
                2 => Ticket(),
                3 => Json(new { accessBlocked = true, pendingCleanup = true }, HttpStatusCode.Accepted),
                _ => Json(new { disconnected = true })
            });
        }));
        var client = new ChatGptIntegrationClient(http, "https://auth.invalid/");
        await client.EnrollAsync(new HabiticaCredentials("fixture-user", "fixture-token"), CancellationToken.None);

        var pending = await client.DisconnectAsync(false, CancellationToken.None);
        Assert.False(pending.Succeeded);
        Assert.Contains("cleanup is pending", pending.Message);
        Assert.True(client.HasConnection);
        Assert.True((await client.DisconnectAsync(false, CancellationToken.None)).Succeeded);
        Assert.False(client.HasConnection);
    }

    [Fact]
    public async Task Management_rotation_replaces_only_the_tab_token_and_status_uses_the_new_bearer()
    {
        var rotated = new string('r', 43);
        using var http = new HttpClient(new Handler(async (request, cancellation) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/enroll") return Json(new { profileId = ProfileId, managementToken = ManagementToken }, HttpStatusCode.Created);
            if (path.EndsWith("/rotate-management"))
            {
                Assert.Equal(ManagementToken, request.Headers.Authorization!.Parameter);
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellation));
                Assert.Equal("rotate-management-token", body.RootElement.GetProperty("confirm").GetString());
                return Json(new { rotated = true, managementToken = rotated });
            }
            Assert.Equal(rotated, request.Headers.Authorization!.Parameter);
            if (path.EndsWith("/revoke-grants")) return Json(new { revoked = true, pendingCleanup = true });
            Assert.Equal(HttpMethod.Get, request.Method);
            return Json(new { displayName = "Fixture account", permissions = new[] { "profile:read" }, credentialStatus = "repair_needed", lastVerifiedAtUtc = DateTimeOffset.UtcNow, permissionsTruncated = false });
        }));
        var client = new ChatGptIntegrationClient(http, "https://auth.invalid/");
        await client.RevalidateAsync(new HabiticaCredentials("fixture-user", "fixture-token"), CancellationToken.None);
        var result = await client.RotateManagementAsync(CancellationToken.None);
        Assert.True(result.Succeeded);
        Assert.DoesNotContain(rotated, JsonSerializer.Serialize(result));
        var status = await client.GetStatusAsync(CancellationToken.None);
        Assert.True(status.Succeeded);
        Assert.Equal("repair_needed", status.CredentialStatus);
        Assert.DoesNotContain(rotated, JsonSerializer.Serialize(status));
        var revoked = await client.RevokeGrantsAsync(CancellationToken.None);
        Assert.True(revoked.Succeeded);
        Assert.Contains("cleanup is pending", revoked.Message);
        Assert.True(client.HasConnection);
    }

    [Fact]
    public async Task Lost_rotation_response_forgets_an_unusable_management_token_without_copying_error_details()
    {
        using var http = new HttpClient(new Handler((request, _) => request.RequestUri!.AbsolutePath == "/enroll"
            ? Task.FromResult(Json(new { profileId = ProfileId, managementToken = ManagementToken }, HttpStatusCode.Created))
            : throw new HttpRequestException("fixture-private-token")));
        var client = new ChatGptIntegrationClient(http, "https://auth.invalid/");
        await client.RevalidateAsync(new HabiticaCredentials("fixture-user", "fixture-token"), CancellationToken.None);
        var result = await client.RotateManagementAsync(CancellationToken.None);
        Assert.False(result.Succeeded);
        Assert.False(client.HasConnection);
        Assert.DoesNotContain("fixture-private-token", result.Message);
    }

    [Fact]
    public async Task A_late_management_rotation_cannot_restore_a_forgotten_tab_session()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new Handler(async (request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/enroll") return Json(new { profileId = ProfileId, managementToken = ManagementToken }, HttpStatusCode.Created);
            entered.SetResult();
            await release.Task;
            return Json(new { rotated = true, managementToken = new string('r', 43) });
        }));
        var client = new ChatGptIntegrationClient(http, "https://auth.invalid/");
        await client.RevalidateAsync(new HabiticaCredentials("fixture-user", "fixture-token"), CancellationToken.None);
        var pending = client.RotateManagementAsync(CancellationToken.None);
        await entered.Task;
        client.ForgetSession();
        release.SetResult();
        Assert.False((await pending).Succeeded);
        Assert.False(client.HasConnection);
    }

    private static HttpResponseMessage Ticket() => Json(new { code = "0123456789AB", expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(10) });
    private static HttpResponseMessage Json(object value, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = JsonContent.Create(value) };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request, cancellationToken);
    }
}
