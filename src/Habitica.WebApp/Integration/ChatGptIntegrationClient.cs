using System.Net.Http.Headers;
using System.Net.Http.Json;
using Habitica.Application.Integration;
using Habitica.Domain.Auth;

namespace Habitica.WebApp.Integration;

public sealed partial class ChatGptIntegrationClient : IChatGptIntegrationClient
{
    private readonly HttpClient _http;
    private readonly Uri? _baseUri;
    private readonly bool _linkingEnabled;
    private string? _profileId;
    private string? _managementToken;
    private int _sessionVersion;

    public ChatGptIntegrationClient(HttpClient http, string? authBaseUrl, bool linkingEnabled = true)
    {
        _http = http;
        _linkingEnabled = linkingEnabled;
        if (Uri.TryCreate(authBaseUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
            && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
            && uri.AbsolutePath == "/")
        {
            _baseUri = uri;
        }
    }

    public bool IsAvailable => _baseUri is not null && _linkingEnabled;
    internal bool IsVaultAvailable => _baseUri is not null;
    public bool HasConnection => _profileId is not null && _managementToken is not null;
    internal string? ProfileId=>_profileId;
    internal int SessionVersion=>_sessionVersion;

    public async Task<IntegrationLinkResult> EnrollAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
    {
        if (!IsAvailable) return Unavailable();
        var result = await EnrollProfileAsync(credentials, cancellationToken);
        return result.Succeeded ? await CreateLinkAsync(cancellationToken) : result;
    }

    internal async Task<IntegrationLinkResult> EnrollProfileAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
    {
        if (_baseUri is null) return Unavailable();
        var sessionVersion = _sessionVersion;
        try
        {
            using var response = await _http.PostAsJsonAsync(new Uri(_baseUri, "enroll"), new
            {
                habiticaUserId = credentials.UserId,
                apiToken = credentials.ApiToken,
                consentVersion = "integration-v1"
            }, cancellationToken);
            if (!response.IsSuccessStatusCode) return Unavailable();
            var enrollment = await response.Content.ReadFromJsonAsync<Enrollment>(cancellationToken);
            if (enrollment is null || !System.Text.RegularExpressions.Regex.IsMatch(enrollment.ProfileId ?? "", "^prf_[A-Za-z0-9_-]{43}$")
                || !System.Text.RegularExpressions.Regex.IsMatch(enrollment.ManagementToken ?? "", "^[A-Za-z0-9_-]{43}$")) return Unavailable();
            if (_sessionVersion != sessionVersion) return new(false, "Enrollment may have completed. Revalidate the account to manage the connection.");
            _profileId = enrollment.ProfileId;
            _managementToken = enrollment.ManagementToken;
            _sessionVersion++;
            return new(true, "Credentials verified for this tab.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, "Enrollment result is unavailable. Revalidate the account before trying again."); }
    }

    public async Task<IntegrationLinkResult> CreateLinkAsync(CancellationToken cancellationToken)
    {
        if (!IsAvailable || !HasConnection) return Unavailable();
        var sessionVersion = _sessionVersion;
        try
        {
            using var request = ManagementRequest(HttpMethod.Post, "linking-ticket");
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return new(false, "The connection is enabled, but a linking code is unavailable. Generate a new code or disconnect.");
            var ticket = await response.Content.ReadFromJsonAsync<LinkTicket>(cancellationToken);
            if (_sessionVersion != sessionVersion) return Unavailable();
            if (ticket is null || ticket.Code is null || !System.Text.RegularExpressions.Regex.IsMatch(ticket.Code, "^[0123456789ABCDEFGHJKMNPQRSTVWXYZ]{12}$")
                || ticket.ExpiresAtUtc <= DateTimeOffset.UtcNow) return Unavailable();
            return new(true, "Enter this one-time code on the Companion authorization page, then review the account and requested access.", ticket.Code, ticket.ExpiresAtUtc);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, "The connection is enabled, but a linking code is unavailable. Generate a new code or disconnect."); }
    }

    public async Task<IntegrationLinkResult> DisconnectAsync(bool deleteProfile, CancellationToken cancellationToken)
    {
        if (_baseUri is null || !HasConnection) return Unavailable();
        var sessionVersion = _sessionVersion;
        try
        {
            using var request = ManagementRequest(deleteProfile ? HttpMethod.Delete : HttpMethod.Post, deleteProfile ? "" : "disconnect");
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return new(false, "Connection cleanup could not be confirmed. Revalidate the account to manage it.");
            var cleanup = await response.Content.ReadFromJsonAsync<CleanupResult>(cancellationToken);
            if (cleanup?.PendingCleanup == true) return new(false, "Integration access is blocked, but cleanup is pending. Confirm cleanup again to retry.");
            if (cleanup is null || (deleteProfile ? cleanup.Deleted != true : cleanup.Disconnected != true)) return new(false, "Connection cleanup could not be confirmed. Revalidate the account to manage it.");
            if (_sessionVersion == sessionVersion) ForgetSession();
            return new(true, deleteProfile ? "Integration profile deleted. Browser and Habitica data remain available." : "Integration disconnected. Browser and Habitica data remain available.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, "Connection cleanup could not be confirmed. Revalidate the account to manage it."); }
    }

    public async Task<IntegrationStatusResult> GetStatusAsync(CancellationToken cancellationToken)
    {
        if (_baseUri is null || !HasConnection) return new(false, "Revalidate credentials to view integration status.");
        var version = _sessionVersion;
        try
        {
            using var request = ManagementRequest(HttpMethod.Get, "status");
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return new(false, "Integration status is unavailable. Revalidate credentials if access changed.");
            var status = await response.Content.ReadFromJsonAsync<StatusPayload>(cancellationToken);
            if (version != _sessionVersion || status is null || status.DisplayName?.Length > 256 || status.Permissions is null || status.Permissions.Length > KnownPermissions.Length
                || status.Permissions.Any(scope => !KnownPermissions.Contains(scope)) || status.CredentialStatus is not ("last_verified" or "repair_needed"))
                return new(false, "Integration status is unavailable.");
            return new(true, "Integration status loaded.", status.DisplayName, status.Permissions, status.CredentialStatus, status.LastVerifiedAtUtc, status.LastUsedAtUtc, status.PermissionsTruncated);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, "Integration status is unavailable."); }
    }

    public Task<IntegrationLinkResult> RevalidateAsync(HabiticaCredentials credentials, CancellationToken cancellationToken) => EnrollProfileAsync(credentials, cancellationToken);
    public Task<IntegrationLinkResult> RevokeGrantsAsync(CancellationToken cancellationToken) => ManageAsync(false, cancellationToken);
    public Task<IntegrationLinkResult> RotateManagementAsync(CancellationToken cancellationToken) => ManageAsync(true, cancellationToken);

    private async Task<IntegrationLinkResult> ManageAsync(bool rotate, CancellationToken cancellationToken)
    {
        if (_baseUri is null || !HasConnection) return Unavailable();
        var version = _sessionVersion;
        try
        {
            using var request = ManagementRequest(HttpMethod.Post, rotate ? "rotate-management" : "revoke-grants");
            request.Content = JsonContent.Create(new { confirm = rotate ? "rotate-management-token" : "revoke-chatgpt-grants" });
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                if (rotate && _sessionVersion == version) ForgetSession();
                return new(false, "Management change could not be confirmed. Revalidate credentials before retrying.");
            }
            var result = await response.Content.ReadFromJsonAsync<ManagementPayload>(cancellationToken);
            if (_sessionVersion != version) return Unavailable();
            if (rotate)
            {
                if (result?.Rotated != true || !System.Text.RegularExpressions.Regex.IsMatch(result.ManagementToken ?? "", "^[A-Za-z0-9_-]{43}$"))
                {
                    ForgetSession();
                    return new(false, "Management credential may have changed. Revalidate credentials to manage the connection.");
                }
                _managementToken = result.ManagementToken;
                _sessionVersion++;
                return new(true, "Management credential replaced for this tab. ChatGPT grants and stored credentials are unchanged.");
            }
            return result?.Revoked == true
                ? new(true, result.PendingCleanup ? "ChatGPT access revoked. Provider cleanup is pending; stored credentials and separate Workspace access remain available." : "ChatGPT access revoked. Stored credentials and separate Workspace access remain available.")
                : new(false, "Grant revocation could not be confirmed. Revalidate credentials before retrying.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (rotate && _sessionVersion == version) ForgetSession();
            throw;
        }
        catch
        {
            if (rotate && _sessionVersion == version) ForgetSession();
            return new(false, "Management change could not be confirmed. Revalidate credentials before retrying.");
        }
    }

    private static readonly string[] KnownPermissions = ["profile:read", "account:read", "tasks:read", "habitica.tasks.write", "party:read", "party.planning.write", "habitica.spells.execute", "habitica.cron.execute", "workspace.organization.write", "habitica.tags.write","notes.read","notes.write"];
    private sealed record StatusPayload(string? DisplayName, string[]? Permissions, string? CredentialStatus, DateTimeOffset? LastVerifiedAtUtc, DateTimeOffset? LastUsedAtUtc, bool PermissionsTruncated);
    private sealed record ManagementPayload(bool Rotated, string? ManagementToken, bool Revoked, bool PendingCleanup);

    public void ForgetSession()
    {
        _sessionVersion++;
        _profileId = null;
        _managementToken = null;
    }

    internal HttpRequestMessage WorkspaceHandshake(Uri uri)
    {
        if (!HasConnection) throw new InvalidOperationException("Credential verification is required.");
        var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _managementToken);
        request.Content = JsonContent.Create(new { consentVersion = "workspace-v1" });
        return request;
    }

    internal HttpRequestMessage NotesHandshake(Uri uri)
    {
        if(!HasConnection)throw new InvalidOperationException("Credential verification is required.");
        var request=new HttpRequestMessage(HttpMethod.Post,uri);request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",_managementToken);
        request.Content=JsonContent.Create(new{consentVersion="notes-v1"});return request;
    }

    internal HttpRequestMessage DiagnosticOwnerRequest(Uri uri,HttpMethod method,object? body)
    {
        if(!HasConnection||!uri.AbsolutePath.StartsWith("/diagnostics/",StringComparison.Ordinal)||method!=HttpMethod.Get&&method!=HttpMethod.Post)throw new InvalidOperationException("Diagnostic owner access is unavailable.");
        var request=new HttpRequestMessage(method,uri);request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",_managementToken);
        if(body is not null)request.Content=new StringContent(System.Text.Json.JsonSerializer.Serialize(body,Habitica.Application.Diagnostics.DiagnosticSharing.Json),System.Text.Encoding.UTF8,"application/json");return request;
    }

    private HttpRequestMessage ManagementRequest(HttpMethod method, string suffix)
    {
        var path = "profiles/" + Uri.EscapeDataString(_profileId!) + (suffix.Length > 0 ? "/" + suffix : "");
        var request = new HttpRequestMessage(method, new Uri(_baseUri!, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _managementToken);
        return request;
    }

    private static IntegrationLinkResult Unavailable() => new(false, "Management access is unavailable in this tab. Verify the account to check server connection status.");
    private sealed record CleanupResult(bool PendingCleanup, bool? Deleted, bool? Disconnected);
    private sealed record Enrollment(string? ProfileId, string? ManagementToken);
    private sealed record LinkTicket(string? Code, DateTimeOffset ExpiresAtUtc);
}
