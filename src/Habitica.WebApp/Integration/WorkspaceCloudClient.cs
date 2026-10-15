using System.Net.Http.Headers;
using System.Net.Http.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Auth;
using Habitica.Domain.Workspace;

namespace Habitica.WebApp.Integration;

public sealed class WorkspaceCloudClient : IWorkspaceCloudClient
{
    private readonly HttpClient _http;
    private readonly ChatGptIntegrationClient _vault;
    private readonly Uri? _baseUri;
    private string? _token;
    private int _generation;
    public Guid? WorkspaceId { get; private set; }
    public bool IsAvailable => _baseUri is not null && _vault.IsVaultAvailable;

    public WorkspaceCloudClient(HttpClient http, ChatGptIntegrationClient vault, string? baseUrl)
    {
        _http = http; _vault = vault;
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback)
            && uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)) _baseUri = uri;
    }

    public async Task<WorkspaceActionResult> ConnectAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
    {
        if (!IsAvailable) return new(false, "Cloud workspace is unavailable on this app.");
        var generation = _generation;
        try
        {
            var enrolled = await _vault.EnrollProfileAsync(credentials, cancellationToken);
            if (!enrolled.Succeeded || generation != _generation) return new(false, "Cloud connection could not be confirmed. Revalidate before trying again.");
            using var request = _vault.WorkspaceHandshake(new Uri(_baseUri!, "connect"));
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return new(false, "Cloud connection could not be confirmed. Revalidate before trying again.");
            var result = await response.Content.ReadFromJsonAsync<Connection>(cancellationToken);
            if (result is null || result.WorkspaceId == Guid.Empty || result.ConsentVersion != "workspace-v1" || result.ExpiresAtUtc <= DateTimeOffset.UtcNow
                || !System.Text.RegularExpressions.Regex.IsMatch(result.SessionToken ?? "", "^[A-Za-z0-9_-]{43}$") || generation != _generation) return new(false, "Cloud connection could not be confirmed.");
            WorkspaceId = result.WorkspaceId; _token = result.SessionToken;
            return new(true, "Cloud workspace connected for this tab. Background execution is separately controlled. Browser data has not been uploaded.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, "Cloud connection result is unknown. Revalidate before managing cloud data."); }
    }

    public async Task<WorkspaceActionResult> ExportAsync(CancellationToken cancellationToken)
    {
        var generation = _generation; var id = WorkspaceId;
        if (id is null) return new(false, "Connect the cloud workspace first.");
        try
        {
            using var request = Request(HttpMethod.Get, "export");
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode) return new(false, "Cloud export is unavailable. Revalidate the connection or try later.");
            var json = await BoundedTextAsync(response.Content, cancellationToken);
            var snapshot = WorkspaceJournalJson.Deserialize(json);
            if (generation != _generation || snapshot.Document.Id != id) return new(false, "The account session changed; the export was discarded.");
            return new(true, "Cloud export includes readable task notes and command history. Keep the file private.", WorkspaceJournalJson.Serialize(snapshot));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, "Cloud export could not be validated. Existing data remains available."); }
    }

    public async Task<WorkspaceActionResult> DisconnectAsync(bool deleteWorkspace, CancellationToken cancellationToken)
    {
        if (WorkspaceId is null) return new(false, "Connect the cloud workspace first.");
        var generation = _generation;
        try
        {
            using var request = Request(deleteWorkspace ? HttpMethod.Delete : HttpMethod.Post, deleteWorkspace ? "workspace" : "disconnect");
            request.Content = deleteWorkspace ? JsonContent.Create(new { confirmWorkspaceId = WorkspaceId }) : JsonContent.Create(new { });
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return new(false, "Cloud cleanup could not be confirmed. Reconnect to check its status.");
            var result = await response.Content.ReadFromJsonAsync<Cleanup>(cancellationToken);
            if (result is null || (deleteWorkspace ? !result.Deleted : !result.Disconnected)) return new(false, "Cloud cleanup could not be confirmed.");
            if (generation == _generation) ForgetSession();
            return new(true, deleteWorkspace ? "Cloud workspace and command history deleted. Browser copies and Habitica data are separate." : "Cloud workspace disconnected. Retained tasks and history remain on the server until deletion.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, "Cloud cleanup outcome is unknown. Reconnect to check its status."); }
    }

    public void ForgetSession() { _generation++; _token = null; WorkspaceId = null; }
    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, new Uri(_baseUri!, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token); return request;
    }
    private static async Task<string> BoundedTextAsync(HttpContent content, CancellationToken cancellationToken)
    {
        const int limit = 16 * 1024 * 1024;
        if (content.Headers.ContentLength > limit) throw new InvalidOperationException();
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream(); var buffer = new byte[8192]; int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0) { if (output.Length + read > limit) throw new InvalidOperationException(); await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken); }
        return System.Text.Encoding.UTF8.GetString(output.GetBuffer(), 0, (int)output.Length);
    }
    private sealed record Connection(Guid WorkspaceId, string? SessionToken, DateTimeOffset ExpiresAtUtc, string ConsentVersion);
    private sealed record Cleanup(bool Deleted, bool Disconnected);
}
