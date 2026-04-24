using Habitica.Domain.Auth;

namespace Habitica.Storage;

public sealed class CredentialStore : ICredentialStore
{
    private readonly IKeyValueStorage _keyValueStorage;

    public CredentialStore(IKeyValueStorage keyValueStorage)
    {
        _keyValueStorage = keyValueStorage;
    }

    public Task<HabiticaCredentials?> GetPersistentCredentialsAsync(CancellationToken cancellationToken)
    {
        return _keyValueStorage.GetAsync<HabiticaCredentials>(StorageKeys.PersistentCredentials, cancellationToken);
    }

    public Task SavePersistentCredentialsAsync(HabiticaCredentials credentials, CancellationToken cancellationToken)
    {
        return _keyValueStorage.SetAsync(StorageKeys.PersistentCredentials, credentials, cancellationToken);
    }

    public Task ClearPersistentCredentialsAsync(CancellationToken cancellationToken)
    {
        return _keyValueStorage.RemoveAsync(StorageKeys.PersistentCredentials, cancellationToken);
    }
}
