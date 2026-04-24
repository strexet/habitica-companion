using Habitica.Domain.Auth;

namespace Habitica.Storage;

public interface ICredentialStore
{
    Task<HabiticaCredentials?> GetPersistentCredentialsAsync(CancellationToken cancellationToken);

    Task SavePersistentCredentialsAsync(HabiticaCredentials credentials, CancellationToken cancellationToken);

    Task ClearPersistentCredentialsAsync(CancellationToken cancellationToken);
}
