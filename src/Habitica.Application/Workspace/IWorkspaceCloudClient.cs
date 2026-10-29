using System.Text.Json.Serialization;
using Habitica.Domain.Auth;
using Habitica.Application.Integration;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

public sealed record WorkspaceActionResult(bool Succeeded, string Message, string? Json = null, bool OutcomeUnknown = false, bool KnownNotAccepted = false);
public sealed record WorkspaceOperationalStatus([property: JsonRequired] DateTimeOffset CheckedAtUtc, [property: JsonRequired] int Pending, [property: JsonRequired] int Stalled, [property: JsonRequired] int Uncertain, [property: JsonRequired] int Blocked, [property: JsonRequired] int ExpiredLeases, DateTimeOffset? OldestPendingAtUtc);
public sealed record WorkspaceStatusResult(bool Succeeded, string Message, WorkspaceOperationalStatus? Status = null);
public interface IWorkspaceCloudClient
{
    Task<IntegrationTagConsentResult> ReadTagConsentAsync(CancellationToken cancellationToken) => Task.FromResult(new IntegrationTagConsentResult(false, "Tag consent is unavailable in this client."));
    Task<IntegrationTagConsentResult> SetTagConsentAsync(bool enabled, CancellationToken cancellationToken) => Task.FromResult(new IntegrationTagConsentResult(false, "Tag consent is unavailable in this client."));
    Task<WorkspaceStatusResult> ReadOperationalStatusAsync(CancellationToken cancellationToken) => Task.FromResult(new WorkspaceStatusResult(false, "Workspace status is unavailable."));
    bool ConnectionStatusVerified => false;
    Task<WorkspaceActionResult> RefreshConnectionStatusAsync(CancellationToken cancellationToken) => Task.FromResult(new WorkspaceActionResult(false, "Cloud connection status is unavailable."));
    bool IsAvailable { get; }
    bool PartyPlanningAvailable => false;
    Task<PartyPlanningResult> ManagePartyAsync(PartyPlanningRequest request, CancellationToken cancellationToken) => Task.FromResult(new PartyPlanningResult(false, "Authenticated party planning is unavailable."));
    Guid? WorkspaceId { get; }
    bool ExecutorEnabled { get; }
    bool ExecutorAvailable { get; }
    Task<WorkspaceActionResult> ConnectAsync(HabiticaCredentials credentials, CancellationToken cancellationToken);
    Task<WorkspaceActionResult> ExportAsync(CancellationToken cancellationToken);
    Task<WorkspaceActionResult> SetExecutorAsync(bool enabled, CancellationToken cancellationToken);
    Task<WorkspaceActionResult> HandoffAsync(string archive, CancellationToken cancellationToken);
    Task<WorkspaceJournalSnapshot?> LoadWorkspaceAsync(CancellationToken cancellationToken);
    Task<WorkspaceTaskResponse<WorkspaceCommandPreview>> PrepareCommandAsync(Guid requestId, WorkspaceCommand intent, CancellationToken cancellationToken);
    Task<WorkspaceTaskResponse<WorkspaceMutationReceipt>> AcceptCommandAsync(WorkspaceCommandPreview preview, CancellationToken cancellationToken);
    Task<WorkspaceTaskResponse<WorkspaceMutationReceipt>> ReadCommandAsync(Guid commandId, CancellationToken cancellationToken);
    Task<WorkspaceTaskResponse<WorkspaceProjectionPreview>> PreviewLinkAsync(Guid taskId, Guid habiticaTaskId, CancellationToken cancellationToken);
    Task<WorkspaceActionResult> AdoptLinkAsync(WorkspaceProjectionPreview preview, CancellationToken cancellationToken);
    Task<WorkspaceActionResult> DisconnectAsync(bool deleteWorkspace, CancellationToken cancellationToken);
    void ForgetSession();
}
