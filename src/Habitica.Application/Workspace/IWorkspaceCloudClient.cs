using Habitica.Domain.Auth;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

public sealed record WorkspaceActionResult(bool Succeeded, string Message, string? Json = null);
public interface IWorkspaceCloudClient
{
    bool IsAvailable { get; }
    Guid? WorkspaceId { get; }
    Task<WorkspaceActionResult> ConnectAsync(HabiticaCredentials credentials, CancellationToken cancellationToken);
    Task<WorkspaceActionResult> ExportAsync(CancellationToken cancellationToken);
    Task<WorkspaceActionResult> DisconnectAsync(bool deleteWorkspace, CancellationToken cancellationToken);
    void ForgetSession();
}
