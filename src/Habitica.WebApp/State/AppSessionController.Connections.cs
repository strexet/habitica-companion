using Habitica.Application.Integration;
using Habitica.Application.Sync;
using Habitica.Domain.Auth;

namespace Habitica.WebApp.State;

public sealed partial class AppSessionController
{
    private async Task RememberConnectionAsync(HabiticaCredentials credentials, bool workspaceConnected, bool notesConnected, CancellationToken token)
    {
        if (_connections is null || !IsCurrentCloudAccount(credentials)) return;
        await _connections.RememberConnectionAsync(credentials.UserId, workspaceConnected, notesConnected, token);
        if (IsCurrentCloudAccount(credentials)) await TryUploadCloudSyncSectionAsync(credentials, CloudSyncSection.CompanionPreferences, token);
    }

    public async Task<IntegrationLinkResult> RestoreExistingConnectionsAsync(bool legacyAcknowledged, CancellationToken cancellationToken = default)
    {
        if (_connections is null || !State.IsAuthenticated || State.IsBusy) return new(false, "Sign in before restoring an existing connection.");
        var credentials = await ResolveCredentialsAsync(cancellationToken);
        if (credentials is null || !IsCurrentCloudAccount(credentials)) return new(false, "The account changed.");
        await _connections.BootstrapAsync(credentials, legacyAcknowledged, true, cancellationToken);
        if (!IsCurrentCloudAccount(credentials)) return new(false, "The account changed; the recovery result was discarded.");
        if (_integrationClient?.HasConnection != true || _connections.IntegrationCode != "recovered") return new(false, "Existing access could not be restored. No enrollment or new permission was created.");
        await TryUploadCloudSyncSectionAsync(credentials, CloudSyncSection.CompanionPreferences, cancellationToken);
        Changed?.Invoke();
        return new(true, "Existing account access restored. Independent Workspace and Notes status is shown separately; no new consent or game action was created.");
    }
}
