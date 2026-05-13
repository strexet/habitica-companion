using Habitica.Domain.Auth;

namespace Habitica.WebApp.Sync;

public interface IRemoteUserDataSyncProvider
{
    Task<RemoteUserDataSnapshot?> DownloadAsync(HabiticaCredentials credentials, CancellationToken cancellationToken);

    Task UploadAsync(HabiticaCredentials credentials, string plainTextJson, CancellationToken cancellationToken);
}

public sealed record RemoteUserDataSnapshot(
    string PlainTextJson,
    DateTimeOffset? UpdatedAtUtc);

