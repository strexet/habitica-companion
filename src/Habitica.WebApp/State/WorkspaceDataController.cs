using Habitica.Application.Workspace;
using Habitica.Application.Diagnostics;
using Habitica.Domain.Diagnostics;
using Habitica.Storage;

namespace Habitica.WebApp.State;

public sealed class WorkspaceDataController(IAppSessionController session, IWorkspaceCloudClient cloud, IndexedDbWorkspaceDatabase database, DiagnosticsLogWriter? diagnostics = null, CompanionConnectionCoordinator? connections = null, IWorkspaceTransferCloudClient? transfers = null)
{
    private Task<T> ObserveAsync<T>(string operation, Func<Task<T>> action, Func<T, bool> success)
    {
        if (diagnostics is null) return action();
        var account = session.State.UserId; var authenticated = session.State.IsAuthenticated;
        var mode = operation is "workspace-export-local" or "workspace-restore-local" ? DiagnosticsMode.Local
            : operation is "workspace-status" or "workspace-operations" or "workspace-export-cloud" or "workspace-task-recover" ? DiagnosticsMode.LiveRead : DiagnosticsMode.LiveMutation;
        return diagnostics.ObserveAsync(DiagnosticsFeatureArea.Workspace, operation, action,
            value => new(success(value) ? DiagnosticsResult.Success : DiagnosticsResult.Unavailable),
            () => session.State.IsAuthenticated == authenticated && session.State.UserId == account, mode);
    }

    public bool PartyPlanningAvailable => cloud.PartyPlanningAvailable;
    public async Task<PartyPlanningResult> ManagePartyAsync(PartyPlanningRequest request, CancellationToken cancellationToken = default)
    {
        if (!session.State.IsAuthenticated) return new(false, "Sign in before managing authenticated party planning.");
        var user = session.State.UserId;
        var result = await cloud.ManagePartyAsync(request, cancellationToken);
        return session.State.IsAuthenticated && session.State.UserId == user ? result : new(false, "The account changed; the result was discarded.");
    }
    public Task<WorkspaceStatusResult> ReadOperationalStatusAsync(CancellationToken cancellationToken = default) => ObserveAsync("workspace-operations", () => ReadOperationalStatusCoreAsync(cancellationToken), value => value.Succeeded);
    private async Task<WorkspaceStatusResult> ReadOperationalStatusCoreAsync(CancellationToken cancellationToken = default)
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
    public Task<WorkspaceActionResult> SetExecutorAsync(bool enabled, CancellationToken cancellationToken = default) => ObserveAsync("workspace-executor", () => cloud.SetExecutorAsync(enabled, cancellationToken), value => value.Succeeded);
    public Task<WorkspaceActionResult> ConnectAsync(bool consent, CancellationToken cancellationToken = default) => ObserveAsync("workspace-connect", () => ConnectCoreAsync(consent, cancellationToken), value => value.Succeeded);
    private async Task<WorkspaceActionResult> ConnectCoreAsync(bool consent, CancellationToken cancellationToken = default)
    {
        var user = session.State.UserId;
        var result = await session.ConnectWorkspaceAsync(consent, cancellationToken);
        if (!Current(user)) return new(false, "The account changed; the connection result was discarded.");
        if (!result.Succeeded) return result;
        var status = await RefreshConnectionStatusAsync(cancellationToken);
        return Current(user) ? new(true, result.Message + " " + status.Message)
            : new(false, "The account changed; the connection result was discarded.");
    }
    public Task<WorkspaceActionResult> RefreshConnectionStatusAsync(CancellationToken cancellationToken = default) => ObserveAsync("workspace-status", () => RefreshConnectionStatusCoreAsync(cancellationToken), value => value.Succeeded);
    private async Task<WorkspaceActionResult> RefreshConnectionStatusCoreAsync(CancellationToken cancellationToken = default)
    {
        if (!session.State.IsAuthenticated) return new(false, "Sign in before verifying cloud status.");
        var user = session.State.UserId;
        var result = await cloud.RefreshConnectionStatusAsync(cancellationToken);
        return Current(user) ? result : new(false, "The account changed; status was discarded.");
    }
    private bool Current(string? user) => session.State.IsAuthenticated && session.State.UserId == user;
    public Task<WorkspaceActionResult> ExportCloudAsync(CancellationToken cancellationToken = default) => ObserveAsync("workspace-export-cloud", () => cloud.ExportAsync(cancellationToken), value => value.Succeeded);
    public Task<WorkspaceTaskResponse<WorkspaceMutationReceipt>> ReadTaskReceiptAsync(bool fromCloud, Guid workspaceId, Guid commandId, CancellationToken cancellationToken = default)
        => ObserveAsync("workspace-task-recover", () => ReadTaskReceiptCoreAsync(fromCloud, workspaceId, commandId, cancellationToken), value => value.Succeeded);
    private async Task<WorkspaceTaskResponse<WorkspaceMutationReceipt>> ReadTaskReceiptCoreAsync(bool fromCloud, Guid workspaceId, Guid commandId, CancellationToken cancellationToken)
    {
        if (workspaceId == Guid.Empty || commandId == Guid.Empty || !session.State.IsAuthenticated)
            return new(false, null, "Sign in and provide an exact workspace and original command ID.", true);
        var account = Scope;
        if (fromCloud)
        {
            if (cloud.WorkspaceId != workspaceId)
                return new(false, null, "The selected cloud workspace is unavailable in this tab. Renew access before checking the original receipt.", true);
            var result = await cloud.ReadCommandAsync(commandId, cancellationToken);
            if (!SameAccount(account) || cloud.WorkspaceId != workspaceId)
                return new(false, null, "The account or workspace changed; the receipt was discarded.", true);
            return result.Succeeded && (result.Value is null || result.Value.CommandId != commandId)
                ? new(false, null, "The server returned a different command. The original outcome remains unknown.", true) : result;
        }
        var snapshot = await database.ForScope(account).LoadAsync(workspaceId, cancellationToken);
        if (!SameAccount(account)) return new(false, null, "The account changed; the receipt was discarded.", true);
        var entry = snapshot?.Commands.FirstOrDefault(row => row.Command.Id == commandId);
        return entry is null ? new(false, null, "No original receipt is visible in this browser workspace. The outcome remains unverified.", true)
            : new(true, WorkspaceTaskController.Receipt(entry, Guid.Empty), "Original browser receipt found.");
    }
    private bool SameAccount(string account) => session.State.IsAuthenticated && Guid.TryParse(session.State.UserId, out var current)
        && current.ToString("D") == account;
    public Task<WorkspaceActionResult> DisconnectAsync(bool delete, CancellationToken cancellationToken = default) => ObserveAsync(delete ? "workspace-delete" : "workspace-disconnect", async () =>
    {
        if(connections is not null && Scope is { } account) await connections.SuppressAsync(account,false,true,false,cancellationToken);
        return await cloud.DisconnectAsync(delete,cancellationToken);
    }, value => value.Succeeded);
    private string Scope => session.State.IsAuthenticated && Guid.TryParse(session.State.UserId, out var userId) ? userId.ToString("D") : throw new InvalidOperationException("Sign in to manage local workspaces.");
    public Task<IReadOnlyList<Guid>> ListLocalAsync(CancellationToken cancellationToken = default) => database.ListAsync(Scope, cancellationToken);
    public async Task<WorkspaceActionResult> RecoverLegacyTransferAsync(Guid id,CancellationToken token=default)
    {
        if(transfers is null||!cloud.ConnectionStatusVerified)return new(false,"Restore existing cloud access before checking the original legacy transfer.");var account=Scope;
        try
        {
            var snapshot=await database.ForScope(account).LoadAsync(id,token);
            if(!SameAccount(account)||snapshot?.Authority?.TransferId is not {} transferId)return new(false,"Original frozen transfer identity unavailable.");
            if(snapshot.Authority.Mode==Habitica.Domain.Workspace.WorkspaceAuthorityMode.Cloud)return new(true,"The legacy browser copy already has cloud-owned recovery authority.");
            if(snapshot.Authority.Mode!=Habitica.Domain.Workspace.WorkspaceAuthorityMode.Frozen)return new(false,"The original legacy transfer is not frozen.");
            var result=await transfers.ReadTransferReceiptAsync(transferId,token);if(!SameAccount(account))return new(false,"The account changed; original receipt discarded.");
            if(!result.Succeeded||result.Json is null)return result;
            using var doc=System.Text.Json.JsonDocument.Parse(result.Json);var value=doc.RootElement;
            if(value.GetProperty("state").GetString()!="accepted"||!value.TryGetProperty("version",out var version)||version.GetString()!="workspace-handoff-v1")return new(false,"No accepted original legacy receipt is visible. Preserve the frozen copy; only an explicit retry of the same legacy transfer can recover an unaccepted result.",OutcomeUnknown:true);
            var archive=Habitica.Domain.Workspace.WorkspaceJournalJson.Serialize(snapshot);
            var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(archive))).ToLowerInvariant();
            if(value.GetProperty("transferId").GetGuid()!=transferId||value.GetProperty("workspaceId").GetGuid()!=id||value.GetProperty("archiveHash").GetString()!=hash)return new(false,"The original legacy archive does not match. Keep the original backup and receipt; no replacement identity was created.");
            await new WorkspaceHandoffService(database.ForScope(account)).AcknowledgeAsync(id,transferId,token);
            return new(true,"Original legacy receipt verified. Tasks remain cloud-owned; remaining browser Notes require the reviewed unified move.");
        }
        catch(OperationCanceledException)when(token.IsCancellationRequested){throw;}
        catch{return new(false,"Original legacy transfer recovery is unavailable. Frozen records remain preserved.",OutcomeUnknown:true);}
    }
    public async Task<WorkspaceActionResult> RetryLegacyTransferAsync(Guid id,CancellationToken token=default)
    {
        var account=Scope;var snapshot=await database.ForScope(account).LoadAsync(id,token);
        if(!SameAccount(account)||snapshot?.Authority?.Mode!=Habitica.Domain.Workspace.WorkspaceAuthorityMode.Frozen||snapshot.Authority.TransferId is null)
            return new(false,"Only an original frozen legacy transfer can be retried. No new transfer was prepared.");
        return await TransferAsync(id,token);
    }
    public Task<WorkspaceActionResult> TransferAsync(Guid id, CancellationToken cancellationToken = default) => ObserveAsync("workspace-transfer", () => TransferCoreAsync(id, cancellationToken), value => value.Succeeded);
    private async Task<WorkspaceActionResult> TransferCoreAsync(Guid id, CancellationToken cancellationToken = default)
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
    public Task<WorkspaceActionResult> ExportLocalAsync(Guid id, CancellationToken cancellationToken = default) => ObserveAsync("workspace-export-local", () => ExportLocalCoreAsync(id, cancellationToken), value => value.Succeeded);
    private async Task<WorkspaceActionResult> ExportLocalCoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await new WorkspacePortabilityService(database.ForScope(Scope)).ExportAsync(id, cancellationToken);
            return new(json is not null, json is null ? "This local workspace is unavailable." : "Local backup includes readable task notes and history. Keep the file private.", json);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, "Local backup could not be read."); }
    }
    public Task<WorkspaceActionResult> RestoreLocalAsync(string json, CancellationToken cancellationToken = default) => ObserveAsync("workspace-restore-local", () => RestoreLocalCoreAsync(json, cancellationToken), value => value.Succeeded);
    private async Task<WorkspaceActionResult> RestoreLocalCoreAsync(string json, CancellationToken cancellationToken = default)
    {
        try
        {
            if(connections is not null&&(connections.CloudSelected||connections.State!=CompanionConnectionState.Browser))return new(false,"The active Workspace requires recovery. A local restore cannot create a browser fork.");
            var result = await new WorkspacePortabilityService(database.ForScope(Scope)).RestoreAsync(json, cancellationToken);
            return new(result != WorkspaceRestoreOutcome.Conflict, result == WorkspaceRestoreOutcome.Conflict ? "The local workspace differs. Existing data was preserved; use another browser or review the conflict." : "Workspace restored locally with execution paused. Cloud data and Habitica records remain separate.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return new(false, "The workspace backup is invalid or storage is unavailable. Existing data was preserved."); }
    }
}
