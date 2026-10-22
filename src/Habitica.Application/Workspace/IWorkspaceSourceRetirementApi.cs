using System.Text.Json.Serialization;
using Habitica.Domain.Workspace;
namespace Habitica.Application.Workspace;

public sealed record WorkspaceSourceRetirementRequest(Guid RequestId,Guid ConversionId,long ExpectedRevision);
public sealed record WorkspaceSourceRetirementView(Guid CommandId,Guid RequestId,Guid WorkspaceId,Guid ConversionId,string ContextHash,long Revision,
    CompanionTask Source,HabiticaProjection SourceMapping,string HabiticaProjectTagId,string HabiticaTaskId,WorkspaceProject Project,
    IReadOnlyList<ConversionRetirementTarget> Targets,int VerifiedTargets,int VerificationSteps,int CompletedSteps,string State,
    [property:JsonRequired] bool SourceAlreadyAbsent,DateTimeOffset? VerifiedAtUtc,DateTimeOffset ExpiresAtUtc,[property:JsonRequired] bool Expired,
    [property:JsonRequired] bool OutcomeUnknown,bool? SavedInCompanion,WorkspaceSourceRetirement? Receipt,string? Error,int GameScores,string Disclosure);
public interface IWorkspaceSourceRetirementApi
{
    Task<WorkspaceTaskResponse<WorkspaceSourceRetirementView>> PrepareSourceRetirementAsync(WorkspaceSourceRetirementRequest request,CancellationToken token);
    Task<WorkspaceTaskResponse<WorkspaceSourceRetirementView>> VerifySourceRetirementAsync(Guid commandId,CancellationToken token);
    Task<WorkspaceTaskResponse<WorkspaceSourceRetirementView>> AcceptSourceRetirementAsync(WorkspaceSourceRetirementView reviewed,CancellationToken token);
    Task<WorkspaceTaskResponse<WorkspaceSourceRetirementView>> ReadSourceRetirementAsync(Guid commandId,CancellationToken token);
    Task<WorkspaceTaskResponse<WorkspaceSourceRetirementView>> ReconcileSourceRetirementAsync(Guid commandId,CancellationToken token);
}
