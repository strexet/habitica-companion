using Habitica.Domain.Workspace;
using Habitica.Storage;

namespace Habitica.Application.Workspace;

public enum WorkspaceRestoreOutcome { Restored, Unchanged, Conflict }

public sealed class WorkspacePortabilityService(ICompanionWorkspaceStore store, TimeProvider? clock = null)
{
    public async Task<string?> ExportAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var snapshot = await store.LoadAsync(workspaceId, cancellationToken);
        return snapshot is null ? null : WorkspaceJournalJson.Serialize(snapshot);
    }

    public async Task<WorkspaceRestoreOutcome> RestoreAsync(string json, CancellationToken cancellationToken)
    {
        var incoming = WorkspaceJournalJson.Deserialize(json);
        var existing = await store.LoadAsync(incoming.Document.Id, cancellationToken);
        if (existing is not null)
            return WorkspaceJournalJson.Serialize(existing) == WorkspaceJournalJson.Serialize(incoming) ? WorkspaceRestoreOutcome.Unchanged : WorkspaceRestoreOutcome.Conflict;
        var now = (clock ?? TimeProvider.System).GetUtcNow();
        // A copied in-flight request may already have reached Habitica; importing never grants replay permission.
        var restored = ProjectionJournal.RecoverExpired(incoming with
        {
            Commands = incoming.Commands.Select(entry => entry.Lease is null ? entry : entry with { Lease = entry.Lease with { ExpiresAtUtc = now } }).ToArray()
        }, now);
        return await store.TryCommitAsync(0, restored, cancellationToken) ? WorkspaceRestoreOutcome.Restored : WorkspaceRestoreOutcome.Conflict;
    }
}
