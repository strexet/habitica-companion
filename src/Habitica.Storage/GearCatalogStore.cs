using Habitica.Domain.User;

namespace Habitica.Storage;

public sealed class GearCatalogStore : IGearCatalogStore
{
    private readonly IKeyValueStorage _keyValueStorage;

    public GearCatalogStore(IKeyValueStorage keyValueStorage)
    {
        _keyValueStorage = keyValueStorage;
    }

    public Task<GearCatalogSnapshot?> GetLatestAsync(CancellationToken cancellationToken)
    {
        return _keyValueStorage.GetAsync<GearCatalogSnapshot>(StorageKeys.LatestGearCatalog, cancellationToken);
    }

    public Task SaveAsync(GearCatalogSnapshot catalog, CancellationToken cancellationToken)
    {
        return _keyValueStorage.SetAsync(StorageKeys.LatestGearCatalog, catalog, cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        return _keyValueStorage.RemoveAsync(StorageKeys.LatestGearCatalog, cancellationToken);
    }
}
