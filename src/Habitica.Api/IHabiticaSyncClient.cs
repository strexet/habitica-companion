using Habitica.Domain.Auth;
using Habitica.Domain.Tasks;
using Habitica.Domain.User;

namespace Habitica.Api;

public interface IHabiticaSyncClient
{
    Task<UserSummary> GetUserAsync(HabiticaCredentials credentials, CancellationToken cancellationToken);

    Task<UserSnapshot> GetUserSnapshotAsync(HabiticaCredentials credentials, CancellationToken cancellationToken);

    Task<TaskCollectionSnapshot> GetTasksAsync(HabiticaCredentials credentials, CancellationToken cancellationToken);
}
