namespace Habitica.Application.Workspace;
public interface IWorkspaceTransferCloudClient
{
    Task<WorkspaceActionResult> StageAsync(string artifact,CancellationToken token);
    Task<WorkspaceActionResult> PreviewTransferAsync(Guid transferId,bool archiveConflicts,CancellationToken token);
    Task<WorkspaceActionResult> AcceptTransferAsync(Guid transferId,string reviewHash,CancellationToken token);
    Task<WorkspaceActionResult> ReadTransferReceiptAsync(Guid transferId,CancellationToken token);
    Task<WorkspaceActionResult> ExportTransferAsync(Guid transferId,CancellationToken token);
}
