using Habitica.Application.Workspace;
using Habitica.Storage;

namespace Habitica.WebApp.State;

public sealed class WorkspaceDataController(IAppSessionController session, IWorkspaceCloudClient cloud, IndexedDbWorkspaceDatabase database)
{
    public bool IsAvailable => cloud.IsAvailable;
    public Guid? CloudWorkspaceId => cloud.WorkspaceId;
    public Task<WorkspaceActionResult> ConnectAsync(bool consent, CancellationToken cancellationToken = default) => session.ConnectWorkspaceAsync(consent, cancellationToken);
    public Task<WorkspaceActionResult> ExportCloudAsync(CancellationToken cancellationToken = default) => cloud.ExportAsync(cancellationToken);
    public Task<WorkspaceActionResult> DisconnectAsync(bool delete, CancellationToken cancellationToken = default) => cloud.DisconnectAsync(delete, cancellationToken);
    private string Scope => session.State.IsAuthenticated && Guid.TryParse(session.State.UserId, out var userId) ? userId.ToString("D") : throw new InvalidOperationException("Sign in to manage local workspaces.");
    public Task<IReadOnlyList<Guid>> ListLocalAsync(CancellationToken cancellationToken = default) => database.ListAsync(Scope, cancellationToken);
    public async Task<WorkspaceActionResult> ExportLocalAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await new WorkspacePortabilityService(database.ForScope(Scope)).ExportAsync(id, cancellationToken);
            return new(json is not null, json is null ? "This local workspace is unavailable." : "Local backup includes readable task notes and history. Keep the file private.", json);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, "Local backup could not be read."); }
    }
    public async Task<WorkspaceActionResult> RestoreLocalAsync(string json, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await new WorkspacePortabilityService(database.ForScope(Scope)).RestoreAsync(json, cancellationToken);
            return new(result != WorkspaceRestoreOutcome.Conflict, result == WorkspaceRestoreOutcome.Conflict ? "The local workspace differs. Existing data was preserved; use another browser or review the conflict." : "Workspace restored locally. Cloud data and Habitica records remain separate.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, "The workspace backup is invalid or storage is unavailable. Existing data was preserved."); }
    }
}
