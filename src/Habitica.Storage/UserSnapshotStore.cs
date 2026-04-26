using Habitica.Domain.User;

namespace Habitica.Storage;

public sealed class UserSnapshotStore : IUserSnapshotStore
{
    private readonly IKeyValueStorage _keyValueStorage;

    public UserSnapshotStore(IKeyValueStorage keyValueStorage)
    {
        _keyValueStorage = keyValueStorage;
    }

    public Task<UserSnapshot?> GetLatestAsync(CancellationToken cancellationToken)
    {
        return _keyValueStorage.GetAsync<UserSnapshot>(StorageKeys.LatestUserSnapshot, cancellationToken);
    }

    public Task SaveAsync(UserSnapshot snapshot, CancellationToken cancellationToken)
    {
        return _keyValueStorage.SetAsync(StorageKeys.LatestUserSnapshot, snapshot, cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        return _keyValueStorage.RemoveAsync(StorageKeys.LatestUserSnapshot, cancellationToken);
    }
}
