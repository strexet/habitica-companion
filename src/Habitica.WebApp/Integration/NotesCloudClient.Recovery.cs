using System.Text.Json;
using Habitica.Application.Notes;
using Habitica.Domain.Notes;

namespace Habitica.WebApp.Integration;

public sealed partial class NotesCloudClient
{
    public async Task<NotesCloudResult<bool>> RecoverConnectionAsync(Guid? expectedNotebookId, CancellationToken token)
    {
        if (!IsAvailable || !vault.HasConnection) return new(false, false, "existing_account_access_required");
        if (expectedNotebookId == Guid.Empty) return new(false, false, "notebook_mismatch");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        var requestedToken = token; token = deadline.Token;
        var generation = _generation; var version = vault.SessionVersion; var profile = vault.ProfileId;
        try
        {
            using var request = vault.RecoveryHandshake(new Uri(_baseUri!, "notes/recover"), new { expectedNotebookId });
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            var text = await RecoveryHttp.Text(response, token);
            if (generation != _generation || version != vault.SessionVersion || !vault.HasConnection) return new(false, false, "connection_changed");
            if (!response.IsSuccessStatusCode) return new(false, false, RecoveryHttp.Error(text));
            var value = JsonSerializer.Deserialize<RecoveredConnection>(text, RecoveryHttp.Json);
            if (value?.Recovered != true || value.NotebookId == Guid.Empty || value.AccessEpoch == Guid.Empty
                || expectedNotebookId is not null && value.NotebookId != expectedNotebookId
                || value.ConsentVersion != "notes-v1" || profile is null || value.OwnerScope != NotesModel.Scope(profile)
                || !System.Text.RegularExpressions.Regex.IsMatch(value.SessionToken ?? "", "^[A-Za-z0-9_-]{43}$")
                || value.ExpiresAtUtc <= DateTimeOffset.UtcNow || value.ExpiresAtUtc > DateTimeOffset.UtcNow.AddHours(8).AddMinutes(1))
                return new(false, false, "invalid_recovery_response");
            _generation++; _profileId = profile; _token = value.SessionToken; NotebookId = value.NotebookId; OwnerScope = value.OwnerScope;
            return new(true, true, "recovered");
        }
        catch (OperationCanceledException) when (requestedToken.IsCancellationRequested) { throw; }
        catch { return new(false, false, "recovery_unavailable"); }
    }

    private sealed record RecoveredConnection(Guid NotebookId, string? OwnerScope, Guid AccessEpoch, string? ConsentVersion,
        string? SessionToken, DateTimeOffset ExpiresAtUtc, bool Recovered);
}
