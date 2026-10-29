using System.Text.Json.Serialization;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

public sealed record WorkspaceTagGroupChange(Guid EntityId, WorkspaceOrganizationKind Kind, string Name, bool Archived, bool Removed,
    IReadOnlyList<Guid> TaskIds, [property:JsonRequired] bool RetainMembership, IReadOnlyList<Guid> RemovedTaskIds);
public sealed record WorkspaceTagChangePreview([property:JsonRequired] bool SeparateConsentRequired, [property:JsonRequired] bool ScopeAllowed,
    [property:JsonRequired] bool ConsentEnabled, IReadOnlyList<WorkspaceTagGroupChange> Groups)
{
    [JsonIgnore] public bool RequestsMetadataChanges => ScopeAllowed && ConsentEnabled && Groups.Any(group => !group.RetainMembership);
    public bool IsValid() => SeparateConsentRequired && Groups is not null && Groups.Count <= 100000
        && Groups.All(group => group.EntityId != Guid.Empty && Enum.IsDefined(group.Kind) && !string.IsNullOrWhiteSpace(group.Name) && group.Name.Length <= 256
            && group.TaskIds is not null && group.RemovedTaskIds is not null && group.TaskIds.Count <= 100000 && group.RemovedTaskIds.Count <= 100000
            && group.TaskIds.All(id => id != Guid.Empty) && group.TaskIds.Distinct().Count() == group.TaskIds.Count
            && group.RemovedTaskIds.All(id => id != Guid.Empty) && group.RemovedTaskIds.Distinct().Count() == group.RemovedTaskIds.Count)
        && Groups.Select(group => group.EntityId).Distinct().Count() == Groups.Count;
}
public sealed record WorkspaceTagTaskCoverage(int Total,int Linked,int Pending,int Missing,int NeedsReview,int Archived);
public sealed record WorkspaceTagGroupCoverage(int Total,int Confirmed,int Pending,int NeedsReview,int Missing);
public sealed record WorkspaceGroupTagState(Guid EntityId,WorkspaceOrganizationKind Kind,string Name,bool Archived,bool Removed,
    string IdentityState,string MembershipState,string? HabiticaTagId,int DesiredTaskCount,int ConfirmedMemberCount,string? Reason,IReadOnlyList<Guid>? UnknownWorkIds=null);
public sealed record WorkspaceTagStatusPage(string Provenance,Guid WorkspaceId,long Revision,[property:JsonRequired] bool AccountVerified,
    [property:JsonRequired] bool TagConsentEnabled,[property:JsonRequired] bool TagScopeAllowed,[property:JsonRequired] bool IntegrationComplete,
    WorkspaceTagTaskCoverage TaskCoverage,WorkspaceTagGroupCoverage GroupCoverage,IReadOnlyList<WorkspaceGroupTagState> Groups,int MatchedCount,int? NextOffset, [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] int RetiredSourceContainers=0);
public sealed record WorkspaceTagCandidate(Guid Id,string Name,bool RegisteredForGroup);
public sealed record WorkspaceTagCandidatePage(Guid WorkspaceId,long Revision,Guid EntityId,WorkspaceOrganizationKind Kind,string Name,string Fingerprint,
    IReadOnlyList<WorkspaceTagCandidate> Candidates,int MatchedCount,int? NextOffset);
public sealed record WorkspaceTagTaskChange(Guid TaskId,string? HabiticaTaskId,[property:JsonRequired] bool Present,string State,WorkspaceTagEvidence? Evidence,string Text);
public sealed record WorkspaceTagObservedTag(Guid Id,string Name,bool? Native=null,string? Fingerprint=null);
public sealed record WorkspaceTagObservedTask(Guid Id,string Kind,string? Fingerprint=null,IReadOnlyList<Guid>? Tags=null);
public sealed record WorkspaceTagReviewDetails(Guid EntityId,WorkspaceOrganizationKind Kind,string Name,string? HabiticaTagId,
    IReadOnlyList<WorkspaceTagTaskChange> TaskChanges,string Disclosure,string? DesiredName=null,bool? RetainsHistoricalTags=null,
    Guid? OriginalWorkId=null,Guid? OriginalAttemptId=null,WorkspaceTagObservedTag? ObservedTag=null,WorkspaceTagObservedTask? ObservedTask=null);
public sealed record WorkspaceTagReview(Guid? CommandId,Guid? RequestId,Guid WorkspaceId,string Operation,long ExpectedRevision,DateTimeOffset ExpiresAtUtc,
    bool CanAccept,[property:JsonRequired] int GameEffects,WorkspaceTagReviewDetails? Details,bool Expired=false,bool SavedInCompanion=false,WorkspaceTagReceipt? Receipt=null,
    string? Reason=null,WorkspaceTagOriginal? Original=null,string? ReviewOutcome=null);
public sealed record WorkspaceTagOriginal(Guid WorkId,Guid CommandId,long SourceRevision,string State,string? Error,Guid EntityId,WorkspaceOrganizationKind Kind,
    string Operation,Guid? OriginalAttemptId,string? Provenance=null,bool? OutcomeUnknown=null,int? GameEffects=null);
public sealed record WorkspaceTagReceipt(Guid CommandId,Guid? RequestId,Guid? WorkspaceId,string? Operation,bool? SavedInCompanion,
    [property:JsonRequired] int GameEffects,[property:JsonRequired] bool OutcomeUnknown,IReadOnlyList<Guid>? WorkIds=null,WorkspaceTagOriginal? Original=null,bool? Prepared=null,string? ReviewOutcome=null,string? Reason=null,DateTimeOffset? ClosedAtUtc=null);
public sealed record WorkspaceTagRequest(Guid RequestId,string Operation,WorkspaceOrganizationKind? Kind=null,Guid? EntityId=null,Guid? HabiticaTagId=null,Guid? WorkId=null);
public interface IWorkspaceTagApi
{
    Task<WorkspaceTaskResponse<WorkspaceTagStatusPage>> ReadGroupTagsAsync(int offset,long? expectedRevision,CancellationToken cancellationToken);
    Task<WorkspaceTaskResponse<WorkspaceTagCandidatePage>> InspectTagCandidatesAsync(WorkspaceOrganizationKind kind,Guid entityId,int offset,string? expectedFingerprint,CancellationToken cancellationToken);
    Task<WorkspaceTaskResponse<WorkspaceTagReview>> PrepareTagReviewAsync(WorkspaceTagRequest request,CancellationToken cancellationToken);
    Task<WorkspaceTaskResponse<WorkspaceTagReceipt>> AcceptTagReviewAsync(WorkspaceTagReview review,CancellationToken cancellationToken);
    Task<WorkspaceTaskResponse<WorkspaceTagReceipt>> ReadTagReviewAsync(Guid commandId,CancellationToken cancellationToken);
    Task<WorkspaceTaskResponse<WorkspaceTagOriginal>> RecoverTagReceiptAsync(Guid workId,CancellationToken cancellationToken);
}
