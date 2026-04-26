using Habitica.Domain.Party;

namespace Habitica.Storage;

public interface IPartySnapshotStore
{
    Task<PartySnapshot?> GetLatestAsync(CancellationToken cancellationToken);

    Task SaveAsync(PartySnapshot snapshot, CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}
