using System.Text.Json;
using Habitica.Application.Workspace;

namespace Habitica.WebApp.Integration;

public sealed partial class WorkspaceCloudClient
{
    public async Task<WorkspaceStatusResult> ReadOperationalStatusAsync(CancellationToken cancellationToken)
    {
        if (!IsAvailable || WorkspaceId is null || _token is null) return new(false, "Connect the cloud workspace first.");
        var generation = _generation;
        try
        {
            using var request = Request(HttpMethod.Get, "operations");
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (generation != _generation) return new(false, "The connection changed; status was discarded.");
            if (!response.IsSuccessStatusCode) return new(false, (int)response.StatusCode == 429 ? "Status checks are temporarily limited. Wait before checking again; no action was retried." : "Saved work status is unavailable. No action was retried.");
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var bytes = new byte[8193]; var length = 0;
            while (length < bytes.Length)
            {
                var read = await stream.ReadAsync(bytes.AsMemory(length), cancellationToken);
                if (read == 0) break;
                length += read;
            }
            if (length > 8192) return new(false, "Saved work status is invalid.");
            var status = JsonSerializer.Deserialize<WorkspaceOperationalStatus>(bytes.AsSpan(0, length), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (status is null || status.CheckedAtUtc == default || new[] { status.Pending, status.Stalled, status.Uncertain, status.Blocked, status.ExpiredLeases }.Any(value => value < 0 || value > 1000000)) return new(false, "Saved work status is invalid.");
            return generation == _generation ? new(true, "Saved work status checked. No action was retried.", status) : new(false, "The connection changed; status was discarded.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, "Saved work status is unavailable. No action was retried."); }
    }
}
