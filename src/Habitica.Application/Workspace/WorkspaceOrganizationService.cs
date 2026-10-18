using Habitica.Domain.Workspace;
using Habitica.Storage;

namespace Habitica.Application.Workspace;

public sealed class WorkspaceOrganizationService(ICompanionWorkspaceStore store, TimeProvider? clock = null)
{
    public async Task<OrganizationSubmission?> SubmitAsync(WorkspaceOrganizationCommand command, CancellationToken cancellationToken)
    {
        WorkspaceJournalSnapshot? current;
        try { current=await store.LoadAsync(command.WorkspaceId,cancellationToken); }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(SubmissionDisposition.Unavailable,null,null,"storage_unavailable"); }
        if(current is null) return null;
        var result=WorkspaceOrganizationEngine.Accept(current,command,(clock??TimeProvider.System).GetUtcNow());
        if(result.Disposition!=SubmissionDisposition.Accepted) return result;
        try
        {
            if(await store.TryCommitAsync(current.StoreVersion,result.Snapshot!,cancellationToken)) return result;
            var latest=await store.LoadAsync(command.WorkspaceId,cancellationToken);
            if(latest is null) return new(SubmissionDisposition.RevisionConflict,current,null,"workspace_unavailable");
            // Validate the complete reloaded journal before returning any retained receipt.
            var recovered=WorkspaceOrganizationEngine.Accept(latest,command,(clock??TimeProvider.System).GetUtcNow());
            return recovered.Disposition is SubmissionDisposition.Duplicate or SubmissionDisposition.IdempotencyConflict or SubmissionDisposition.Invalid
                ? recovered : new(SubmissionDisposition.RevisionConflict,latest,null,"stale_revision");
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(SubmissionDisposition.Unavailable,current,null,"acceptance_unknown"); }
    }
}
