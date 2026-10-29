using System.Text.Json.Serialization;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

public sealed record WorkspaceConversionItemInput(Guid SourceItemId,ChecklistConversionMode Mode,ChecklistConversionState DesiredState,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] Guid? TargetTaskId=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? Notes=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] HabiticaTaskDetails? GameDetails=null);
public sealed record WorkspaceConversionRequest(Guid RequestId,Guid SourceTaskId,long SourceRevision,long ExpectedRevision,IReadOnlyList<WorkspaceConversionItemInput> Items,
    Guid? ProjectId=null,string? ProjectName=null,Guid? ExpectedConversionId=null);
public sealed record WorkspaceConversionTarget(ChecklistConversionBinding Binding,CompanionTask? Task,HabiticaProjection? Mapping,
    IReadOnlyList<Guid> PendingCommandIds,[property:JsonRequired] bool PlacementComplete,[property:JsonRequired] bool StoredCorrespondence,[property:JsonRequired] bool RequiresHistoricalMatch);
public sealed record WorkspaceConversionStatus(string Provenance,Guid WorkspaceId,long Revision,CompanionTask Source,
    WorkspaceChecklistConversion? Conversion,IReadOnlyList<WorkspaceConversionTarget> Targets,[property:JsonRequired] bool SourceUnchanged,
    [property:JsonRequired] bool CanonicalComplete,[property:JsonRequired] bool StoredCorrespondenceComplete,WorkspaceGroupTagState? ProjectTag,
    string NativeVerification,[property:JsonRequired] bool RetirementReady,string Disclosure);
public sealed record WorkspaceConversionPreviewTarget(ChecklistConversionBinding Binding,CompanionTask? Task);
public sealed record WorkspaceConversionPreview(Guid PreparedCommandId,Guid CorrelationId,DateTimeOffset ExpiresAtUtc,[property:JsonRequired] bool Expired,
    ChecklistConversionCommand Command,CompanionTask Source,WorkspaceChecklistConversion Conversion,IReadOnlyList<WorkspaceConversionPreviewTarget> Targets,
    WorkspaceTagChangePreview TagProjection,[property:JsonRequired] int NewCaptureCount,[property:JsonRequired] int GameScores,
    [property:JsonRequired] bool SavedInCompanion,WorkspaceConversionReceipt? Receipt=null);
public sealed record WorkspaceConversionReceipt(Guid CommandId,Guid? CorrelationId=null,Guid? ConversionId=null,Guid? ProjectId=null,Guid? SourceTaskId=null,
    bool? SavedInCompanion=null,bool? OutcomeUnknown=null,ChecklistConversionReceipt? Receipt=null,int? GameScores=null,string? Disclosure=null);
public interface IWorkspaceConversionApi
{
    Task<WorkspaceTaskResponse<WorkspaceConversionStatus>> ReadConversionAsync(Guid sourceTaskId,CancellationToken token);
    Task<WorkspaceTaskResponse<WorkspaceConversionPreview>> PrepareConversionAsync(WorkspaceConversionRequest request,CancellationToken token);
    Task<WorkspaceTaskResponse<WorkspaceConversionReceipt>> AcceptConversionAsync(WorkspaceConversionPreview preview,CancellationToken token);
    Task<WorkspaceTaskResponse<WorkspaceConversionReceipt>> ReadConversionCommandAsync(Guid commandId,CancellationToken token);
}
