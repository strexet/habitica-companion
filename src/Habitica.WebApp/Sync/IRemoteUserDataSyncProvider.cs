using Habitica.Domain.Auth;

namespace Habitica.WebApp.Sync;

public interface IRemoteUserDataSyncProvider
{
    Task<RemoteUserDataSnapshot?> DownloadAsync(HabiticaCredentials credentials, CancellationToken cancellationToken);

    Task UploadAsync(HabiticaCredentials credentials, string plainTextJson, CancellationToken cancellationToken);

    Task<RemoteUserDataSnapshot?> DownloadSectionAsync(
        HabiticaCredentials credentials,
        string sectionKey,
        CancellationToken cancellationToken);

    Task<SectionUploadResult> UploadSectionAsync(
        HabiticaCredentials credentials,
        string sectionKey,
        string plainTextJson,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RemoteUserDataSnapshot?>> DownloadAllSectionsAsync(
        HabiticaCredentials credentials,
        IReadOnlyList<string> sectionKeys,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListSectionsAsync(
        HabiticaCredentials credentials,
        CancellationToken cancellationToken);
}

public sealed record RemoteUserDataSnapshot(
    string PlainTextJson,
    DateTimeOffset? UpdatedAtUtc);

public sealed record SectionUploadResult(
    bool Succeeded,
    string? ErrorMessage = null,
    int? PayloadBytes = null);

