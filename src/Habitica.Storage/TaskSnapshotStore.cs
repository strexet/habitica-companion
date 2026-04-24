using Habitica.Domain.Tasks;

namespace Habitica.Storage;

public sealed class TaskSnapshotStore : ITaskSnapshotStore
{
    private readonly IKeyValueStorage _keyValueStorage;

    public TaskSnapshotStore(IKeyValueStorage keyValueStorage)
    {
        _keyValueStorage = keyValueStorage;
    }

    public Task<TaskCollectionSnapshot?> GetLatestAsync(CancellationToken cancellationToken)
    {
        return _keyValueStorage.GetAsync<TaskCollectionSnapshot>(StorageKeys.LatestTaskSnapshot, cancellationToken);
    }

    public Task SaveAsync(TaskCollectionSnapshot snapshot, CancellationToken cancellationToken)
    {
        return _keyValueStorage.SetAsync(StorageKeys.LatestTaskSnapshot, snapshot, cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        return _keyValueStorage.RemoveAsync(StorageKeys.LatestTaskSnapshot, cancellationToken);
    }
}
