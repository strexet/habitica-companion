using Habitica.Domain.Tasks;

namespace Habitica.Storage;

public interface ITaskSnapshotStore
{
    Task<TaskCollectionSnapshot?> GetLatestAsync(CancellationToken cancellationToken);

    Task SaveAsync(TaskCollectionSnapshot snapshot, CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}
