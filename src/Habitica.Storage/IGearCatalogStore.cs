using Habitica.Domain.User;

namespace Habitica.Storage;

public interface IGearCatalogStore
{
    Task<GearCatalogSnapshot?> GetLatestAsync(CancellationToken cancellationToken);

    Task SaveAsync(GearCatalogSnapshot catalog, CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}
