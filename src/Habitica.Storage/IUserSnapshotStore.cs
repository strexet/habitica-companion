using Habitica.Domain.User;

namespace Habitica.Storage;

public interface IUserSnapshotStore
{
    Task<UserSnapshot?> GetLatestAsync(CancellationToken cancellationToken);

    Task SaveAsync(UserSnapshot snapshot, CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}
