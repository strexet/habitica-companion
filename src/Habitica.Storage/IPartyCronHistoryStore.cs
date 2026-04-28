using Habitica.Domain.Party;

namespace Habitica.Storage;

public interface IPartyCronHistoryStore
{
    Task<PartyCronHistorySnapshot> GetAsync(CancellationToken cancellationToken);

    Task<PartyCronHistorySnapshot> UpsertAsync(
        IEnumerable<PartyCronHistoryEvent> events,
        DateTimeOffset pruneReferenceUtc,
        CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}
