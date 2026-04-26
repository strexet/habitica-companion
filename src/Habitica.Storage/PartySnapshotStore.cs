using Habitica.Domain.Party;

namespace Habitica.Storage;

public sealed class PartySnapshotStore : IPartySnapshotStore
{
    private readonly IKeyValueStorage _keyValueStorage;

    public PartySnapshotStore(IKeyValueStorage keyValueStorage)
    {
        _keyValueStorage = keyValueStorage;
    }

    public Task<PartySnapshot?> GetLatestAsync(CancellationToken cancellationToken)
    {
        return _keyValueStorage.GetAsync<PartySnapshot>(StorageKeys.LatestPartySnapshot, cancellationToken);
    }

    public Task SaveAsync(PartySnapshot snapshot, CancellationToken cancellationToken)
    {
        return _keyValueStorage.SetAsync(StorageKeys.LatestPartySnapshot, snapshot, cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        return _keyValueStorage.RemoveAsync(StorageKeys.LatestPartySnapshot, cancellationToken);
    }
}
