using Habitica.Domain.Workspace;
using Habitica.Storage;

namespace Habitica.Application.Workspace;

public sealed class WorkspaceCommandService(ICompanionWorkspaceStore store, TimeProvider? clock = null)
{
    public async Task<CommandSubmission?> SubmitAsync(WorkspaceCommand command, CancellationToken cancellationToken)
    {
        WorkspaceJournalSnapshot? current;
        try { current = await store.LoadAsync(command.WorkspaceId, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(SubmissionDisposition.Unavailable, null, null, "storage_unavailable"); }
        if (current is null) return null;
        // Accepted legacy intent is recovered verbatim before applying the prospective policy.
        if (!current.Commands.Any(entry => entry.Command.Id == command.Id) &&
            WorkspaceTodoProjectionPolicy.Rejection(current.Document, command) is { } policyError)
            return new(SubmissionDisposition.Invalid, current, null, policyError);
        var result = WorkspaceCommandEngine.Accept(current, command, (clock ?? TimeProvider.System).GetUtcNow());
        if (result.Disposition != SubmissionDisposition.Accepted) return result;
        try
        {
            if (await store.TryCommitAsync(current.StoreVersion, result.Snapshot!, cancellationToken)) return result;
            var latest = await store.LoadAsync(command.WorkspaceId, cancellationToken);
            if (latest is null) return new(SubmissionDisposition.RevisionConflict, current, null, "workspace_unavailable");
            var duplicate = latest.Commands.FirstOrDefault(entry => entry.Command.Id == command.Id);
            if (duplicate?.PayloadHash == WorkspaceCommandEngine.Hash(command)) return new(SubmissionDisposition.Duplicate, latest, duplicate);
            return new(duplicate is null ? SubmissionDisposition.RevisionConflict : SubmissionDisposition.IdempotencyConflict, latest, null, duplicate is null ? "stale_revision" : "command_id_reused");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(SubmissionDisposition.Unavailable, current, null, "acceptance_unknown"); }
    }
}
