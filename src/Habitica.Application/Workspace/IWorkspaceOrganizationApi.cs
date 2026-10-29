using System.Text.Json.Serialization;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

public sealed record WorkspaceOrganizationPreview(Guid PreparedCommandId,Guid CorrelationId,DateTimeOffset ExpiresAtUtc,bool Expired,
    WorkspaceOrganizationCommand Command,WorkspaceOrganizationSnapshot Snapshot,[property:JsonRequired] bool MayAffectHabitica,
    bool SavedInCompanion,WorkspaceOrganizationReceipt? Receipt=null,WorkspaceTagChangePreview? TagProjection=null);
public sealed record WorkspaceOrganizationReceipt(Guid CommandId,Guid CorrelationId,Guid WorkspaceId,WorkspaceOrganizationKind Kind,
    WorkspaceOrganizationOperation Operation,Guid EntityId,WorkspaceOrganizationEntry Entry,bool SavedInCompanion,
    [property:JsonRequired] bool MayAffectHabitica,[property:JsonRequired] int AcceptedEventCount,[property:JsonRequired] int ConfirmedEffectCount,
    bool RefreshSucceeded,[property:JsonRequired] bool OutcomeUnknown);
public interface IWorkspaceOrganizationApi
{
    Task<WorkspaceTaskResponse<WorkspaceOrganizationPreview>> PrepareOrganizationAsync(Guid requestId,WorkspaceOrganizationCommand intent,CancellationToken cancellationToken);
    Task<WorkspaceTaskResponse<WorkspaceOrganizationReceipt>> AcceptOrganizationAsync(WorkspaceOrganizationPreview preview,CancellationToken cancellationToken);
    Task<WorkspaceTaskResponse<WorkspaceOrganizationReceipt>> ReadOrganizationAsync(Guid commandId,CancellationToken cancellationToken);
}
