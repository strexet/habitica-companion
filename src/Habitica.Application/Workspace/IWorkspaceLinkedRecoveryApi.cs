using System.Text.Json.Serialization;
using Habitica.Domain.Workspace;
namespace Habitica.Application.Workspace;

public sealed record WorkspaceLinkedRecoveryRequest(Guid RequestId,Guid TaskId,Guid CompletionId,long ExpectedRevision);
public sealed record WorkspaceRecoveryResourceChanges([property:JsonRequired] string Hp,[property:JsonRequired] string Mp,[property:JsonRequired] string Gp,[property:JsonRequired] string Xp,[property:JsonRequired] string Streak);
public sealed record WorkspaceRecoveryReview(string Contract,string ContextHash,Guid TaskId,CompanionTaskKind TaskKind,string Fingerprint,DateOnly? OccurrenceDate,DateOnly AccountDay,string ReviewHash,WorkspaceRecoveryResourceChanges ResourceChanges);
public sealed record WorkspaceLinkedRecoveryPlan(Guid WorkspaceId,long ReviewedRevision,CompanionTask Task,HabiticaProjection Mapping,CompletionEvent Completion,CompletionScoreSource SourceContext,Guid SourceAttemptId,Guid AccountId,string ContextHash);
public sealed record WorkspaceLinkedRecoveryEligibility(Guid WorkspaceId,long Revision,Guid TaskId,Guid CompletionId,[property:JsonRequired] bool Available,string? Reason,WorkspaceLinkedRecoveryPlan? Plan,string Disclosure,[property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] WorkspaceLinkedRecoveryView? OriginalCommand=null);
public sealed record WorkspaceLinkedRecoveryView(Guid CommandId,Guid RequestId,Guid WorkspaceId,long Revision,CompanionTask Task,HabiticaProjection Mapping,CompletionEvent Completion,
    CompletionScoreSource SourceContext,Guid SourceAttemptId,Guid AccountId,string ContextHash,WorkspaceRecoveryReview Review,string State,DateTimeOffset ExpiresAtUtc,
    [property:JsonRequired] bool Expired,[property:JsonRequired] bool OutcomeUnknown,bool? SavedInCompanion,LinkedTaskRecovery? Receipt,string? Error,string Disclosure);
public interface IWorkspaceLinkedRecoveryApi
{
    Task<WorkspaceTaskResponse<WorkspaceLinkedRecoveryEligibility>> ReadLinkedRecoveryEligibilityAsync(Guid taskId,Guid completionId,CancellationToken token);
    Task<WorkspaceTaskResponse<WorkspaceLinkedRecoveryView>> PrepareLinkedRecoveryAsync(WorkspaceLinkedRecoveryRequest request,CancellationToken token);
    Task<WorkspaceTaskResponse<WorkspaceLinkedRecoveryView>> AcceptLinkedRecoveryAsync(WorkspaceLinkedRecoveryView reviewed,CancellationToken token);
    Task<WorkspaceTaskResponse<WorkspaceLinkedRecoveryView>> ReadLinkedRecoveryAsync(Guid commandId,CancellationToken token);
}
