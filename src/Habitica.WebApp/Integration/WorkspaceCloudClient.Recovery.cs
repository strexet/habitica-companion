using System.Text.Json;
using Habitica.Application.Workspace;

namespace Habitica.WebApp.Integration;

public sealed partial class WorkspaceCloudClient
{
    public async Task<WorkspaceActionResult> RecoverConnectionAsync(Guid? expectedWorkspaceId, CancellationToken cancellationToken)
    {
        if (!IsAvailable || !_vault.HasConnection) return new(false, "existing_account_access_required");
        if (expectedWorkspaceId == Guid.Empty) return new(false, "workspace_mismatch");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        var requestedToken = cancellationToken; cancellationToken = deadline.Token;
        ConnectionStatusVerified = false;
        var generation = _generation; var version = _vault.SessionVersion;
        try
        {
            using var request = _vault.RecoveryHandshake(new Uri(_baseUri!, "recover"), new { expectedWorkspaceId });
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var text = await RecoveryHttp.Text(response, cancellationToken);
            if (generation != _generation || version != _vault.SessionVersion) return new(false, "connection_changed");
            if (!response.IsSuccessStatusCode) { ConnectionStatusVerified = false; return new(false, RecoveryHttp.Error(text)); }
            var value = JsonSerializer.Deserialize<RecoveredConnection>(text, RecoveryHttp.Json);
            if (value?.Recovered != true || value.WorkspaceId == Guid.Empty || expectedWorkspaceId is not null && value.WorkspaceId != expectedWorkspaceId
                || value.ConsentVersion != "workspace-v1" || value.ExecutorEnabled is null || value.ExecutorAvailable is null
                || !System.Text.RegularExpressions.Regex.IsMatch(value.SessionToken ?? "", "^[A-Za-z0-9_-]{43}$")
                || value.ExpiresAtUtc <= DateTimeOffset.UtcNow || value.ExpiresAtUtc > DateTimeOffset.UtcNow.AddHours(8).AddMinutes(1))
                return new(false, "invalid_recovery_response");
            _generation++; _linkPreparation = null; WorkspaceId = value.WorkspaceId; _token = value.SessionToken;
            ExecutorEnabled = value.ExecutorEnabled.Value; ExecutorAvailable = value.ExecutorAvailable.Value; ConnectionStatusVerified = true;
            return new(true, "recovered");
        }
        catch (OperationCanceledException) when (requestedToken.IsCancellationRequested) { throw; }
        catch { return new(false, "recovery_unavailable"); }
    }

    private sealed record RecoveredConnection(Guid WorkspaceId, string? SessionToken, DateTimeOffset ExpiresAtUtc,
        string? ConsentVersion, bool? ExecutorEnabled, bool? ExecutorAvailable, bool Recovered);
}
