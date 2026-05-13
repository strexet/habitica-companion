using Habitica.Domain.Auth;

namespace Habitica.WebApp.Sync;

public interface IRemotePartyDataSyncProvider
{
    Task<RemotePartyDataSnapshot?> DownloadAsync(
        HabiticaCredentials credentials,
        string partyId,
        CancellationToken cancellationToken);

    Task UploadAsync(
        HabiticaCredentials credentials,
        string partyId,
        string partySnapshotJson,
        string cronHistoryJson,
        CancellationToken cancellationToken);
}

public sealed record RemotePartyDataSnapshot(
    string? PartySnapshotJson,
    string? CronHistoryJson,
    DateTimeOffset? UpdatedAtUtc);
