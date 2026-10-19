using Habitica.Application.Workspace;
using Habitica.Storage;

namespace Habitica.WebApp.State;

public sealed class WorkspaceDataController(IAppSessionController session, IWorkspaceCloudClient cloud, IndexedDbWorkspaceDatabase database)
{
    public bool PartyPlanningAvailable => cloud.PartyPlanningAvailable;
    public async Task<PartyPlanningResult> ManagePartyAsync(PartyPlanningRequest request, CancellationToken cancellationToken = default)
    {
        if (!session.State.IsAuthenticated) return new(false, "Sign in before managing authenticated party planning.");
        var user = session.State.UserId;
        var result = await cloud.ManagePartyAsync(request, cancellationToken);
        return session.State.IsAuthenticated && session.State.UserId == user ? result : new(false, "The account changed; the result was discarded.");
    }
    public async Task<WorkspaceStatusResult> ReadOperationalStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!session.State.IsAuthenticated) return new(false, "Sign in before checking workspace status.");
        var user = session.State.UserId;
        var result = await cloud.ReadOperationalStatusAsync(cancellationToken);
        return session.State.IsAuthenticated && session.State.UserId == user ? result : new(false, "The account changed; the result was discarded.");
    }
    public bool IsAvailable => cloud.IsAvailable;
    public Guid? CloudWorkspaceId => cloud.WorkspaceId;
    public bool ConnectionStatusVerified => session.State.IsAuthenticated && cloud.ConnectionStatusVerified;
    public bool ExecutorEnabled => cloud.ExecutorEnabled;
    public bool ExecutorAvailable => cloud.ExecutorAvailable;
    public Task<WorkspaceActionResult> SetExecutorAsync(bool enabled, CancellationToken cancellationToken = default) => cloud.SetExecutorAsync(enabled, cancellationToken);
    public async Task<WorkspaceActionResult> ConnectAsync(bool consent, CancellationToken cancellationToken = default)
    {
        var user = session.State.UserId;
        var result = await session.ConnectWorkspaceAsync(consent, cancellationToken);
        if (!Current(user)) return new(false, "The account changed; the connection result was discarded.");
        if (!result.Succeeded) return result;
        var status = await RefreshConnectionStatusAsync(cancellationToken);
        return Current(user) ? new(true, result.Message + " " + status.Message)
            : new(false, "The account changed; the connection result was discarded.");
    }
    public async Task<WorkspaceActionResult> RefreshConnectionStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!session.State.IsAuthenticated) return new(false, "Sign in before verifying cloud status.");
        var user = session.State.UserId;
        var result = await cloud.RefreshConnectionStatusAsync(cancellationToken);
        return Current(user) ? result : new(false, "The account changed; status was discarded.");
    }
    private bool Current(string? user) => session.State.IsAuthenticated && session.State.UserId == user;
    public Task<WorkspaceActionResult> ExportCloudAsync(CancellationToken cancellationToken = default) => cloud.ExportAsync(cancellationToken);
    public Task<WorkspaceActionResult> DisconnectAsync(bool delete, CancellationToken cancellationToken = default) => cloud.DisconnectAsync(delete, cancellationToken);
    private string Scope => session.State.IsAuthenticated && Guid.TryParse(session.State.UserId, out var userId) ? userId.ToString("D") : throw new InvalidOperationException("Sign in to manage local workspaces.");
    public Task<IReadOnlyList<Guid>> ListLocalAsync(CancellationToken cancellationToken = default) => database.ListAsync(Scope, cancellationToken);
    public async Task<WorkspaceActionResult> TransferAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (cloud.WorkspaceId is null) return new(false, "Connect the cloud workspace first.");
        if (!cloud.ConnectionStatusVerified) return new(false, "Verify server execution status before transferring a local workspace.");
        if (cloud.ExecutorEnabled) return new(false, "Pause cloud execution before transferring a local workspace.");
        var scope = Scope;
        try
        {
            var transfer = new WorkspaceHandoffService(database.ForScope(scope));
            var archive = await transfer.PrepareAsync(id, cancellationToken);
            if (Scope != scope) return new(false, "Account changed. The local workspace remains frozen.");
            var snapshot = Habitica.Domain.Workspace.WorkspaceJournalJson.Deserialize(archive);
            var result = await cloud.HandoffAsync(archive, cancellationToken);
            if (result.Succeeded) await transfer.AcknowledgeAsync(id, snapshot.Authority!.TransferId!.Value, cancellationToken);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, "Transfer could not finish. The local copy may be frozen; retry to recover its receipt after active attempts finish."); }
    }
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
            return new(result != WorkspaceRestoreOutcome.Conflict, result == WorkspaceRestoreOutcome.Conflict ? "The local workspace differs. Existing data was preserved; use another browser or review the conflict." : "Workspace restored locally with execution paused. Cloud data and Habitica records remain separate.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, "The workspace backup is invalid or storage is unavailable. Existing data was preserved."); }
    }
}
