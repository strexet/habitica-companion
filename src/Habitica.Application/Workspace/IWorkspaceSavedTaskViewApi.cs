using System.Text.Json.Serialization;
using Habitica.Domain.Workspace;
namespace Habitica.Application.Workspace;
public sealed record WorkspaceSavedViewList([property:JsonRequired] string Provenance,[property:JsonRequired] Guid WorkspaceId,
    [property:JsonRequired] long Revision,[property:JsonRequired] IReadOnlyList<WorkspaceSavedTaskView> Items,
    [property:JsonRequired] int MatchedCount,[property:JsonRequired] bool Truncated,[property:JsonRequired] int Offset,[property:JsonRequired] int? NextOffset);
public sealed record WorkspaceSavedViewTaskPage([property:JsonRequired] string Provenance,[property:JsonRequired] Guid WorkspaceId,
    [property:JsonRequired] Guid ViewId,[property:JsonRequired] long Revision,[property:JsonRequired] IReadOnlyList<CompanionTask> Tasks,
    [property:JsonRequired] int MatchedCount,[property:JsonRequired] bool Truncated,[property:JsonRequired] int Offset,[property:JsonRequired] int? NextOffset);
public interface IWorkspaceSavedTaskViewApi
{
    Task<WorkspaceTaskResponse<WorkspaceSavedViewList>> ListSavedTaskViewsAsync(WorkspaceReadFilter page,CancellationToken cancellationToken);
    Task<WorkspaceTaskResponse<WorkspaceSavedViewTaskPage>> QuerySavedTaskViewAsync(Guid viewId,WorkspaceReadFilter page,CancellationToken cancellationToken);
}
