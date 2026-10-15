using Habitica.Domain.Workspace;
using Habitica.Storage;

namespace Habitica.Application.Workspace;

public sealed class WorkspaceHandoffService(ICompanionWorkspaceStore store, TimeProvider? clock = null)
{
    public async Task<WorkspaceJournalSnapshot> FreezeAsync(Guid id, CancellationToken cancellationToken)
    {
        var current = await store.LoadAsync(id, cancellationToken) ?? throw new InvalidOperationException("Workspace unavailable.");
        if (current.Authority?.Mode == WorkspaceAuthorityMode.Frozen) return current;
        if (current.Authority?.Mode is WorkspaceAuthorityMode.Cloud or WorkspaceAuthorityMode.Paused)
            throw new InvalidOperationException("This copy cannot transfer execution ownership.");
        var frozen = current with { StoreVersion = Next(current.StoreVersion), Authority = new(WorkspaceAuthorityMode.Frozen, Guid.NewGuid()) };
        if (!await store.TryCommitAsync(current.StoreVersion, frozen, cancellationToken)) throw new InvalidOperationException("Workspace changed; retry the transfer.");
        return frozen;
    }

    public async Task<string> PrepareAsync(Guid id, CancellationToken cancellationToken)
    {
        var current = await FreezeAsync(id, cancellationToken);
        var now = (clock ?? TimeProvider.System).GetUtcNow();
        if (current.Commands.Any(entry => entry.ProjectionState is ProjectionCommandState.Leased or ProjectionCommandState.Executing && entry.Lease!.ExpiresAtUtc > now))
            throw new InvalidOperationException("A local attempt is still active. Wait for its receipt or lease expiry before retrying.");
        var recovered = ProjectionJournal.RecoverExpired(current, now);
        if (recovered != current && !await store.TryCommitAsync(current.StoreVersion, recovered, cancellationToken))
            throw new InvalidOperationException("Workspace changed; retry the transfer.");
        return WorkspaceJournalJson.Serialize(recovered);
    }

    public async Task AcknowledgeAsync(Guid id, Guid transferId, CancellationToken cancellationToken)
    {
        var current = await store.LoadAsync(id, cancellationToken) ?? throw new InvalidOperationException("Workspace unavailable.");
        if (current.Authority?.Mode == WorkspaceAuthorityMode.Cloud && current.Authority.TransferId == transferId) return;
        if (current.Authority?.Mode != WorkspaceAuthorityMode.Frozen || current.Authority.TransferId != transferId)
            throw new InvalidOperationException("The transfer receipt does not match this copy.");
        var owned = current with { StoreVersion = Next(current.StoreVersion), Authority = new(WorkspaceAuthorityMode.Cloud, transferId, id) };
        if (!await store.TryCommitAsync(current.StoreVersion, owned, cancellationToken)) throw new InvalidOperationException("Workspace changed; retry the transfer receipt.");
    }

    private static long Next(long value) => value < WorkspaceDocument.MaxRevision ? value + 1 : throw new InvalidOperationException("Workspace revision exhausted.");
}
