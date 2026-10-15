using Habitica.Domain.Workspace;

namespace Habitica.Storage;

// A store instance is bound to the active local account or authorized cloud workspace.
public interface ICompanionWorkspaceStore
{
    Task<WorkspaceJournalSnapshot?> LoadAsync(Guid workspaceId, CancellationToken cancellationToken);

    // Atomically persist canonical records, events and the journal, or leave every record unchanged.
    Task<bool> TryCommitAsync(long expectedStoreVersion, WorkspaceJournalSnapshot snapshot, CancellationToken cancellationToken);
}
