using System.Text.Json.Serialization;

namespace Habitica.Domain.Workspace;

public sealed record WorkspaceProjectRemoval(
    [property: JsonRequired] IReadOnlyList<Guid> TaskIds,
    [property: JsonRequired] IReadOnlyList<Guid> SectionIds)
{
    public bool IsValid() => Valid(TaskIds) && Valid(SectionIds) && TaskIds.Count + SectionIds.Count <= 100000;
    private static bool Valid(IReadOnlyList<Guid>? ids) => ids is not null && ids.Count <= 100000
        && ids.All(id => id != Guid.Empty) && ids.Distinct().Count() == ids.Count;
    public static WorkspaceProjectRemoval From(WorkspaceDocument document, Guid projectId) => new(
        document.Tasks.Where(task => task.ProjectId == projectId).Select(task => task.Id).ToArray(),
        document.Sections.Where(section => section.ProjectId == projectId).Select(section => section.Id).ToArray());
    public bool Matches(WorkspaceDocument document, Guid projectId)
    {
        if (!IsValid()) return false;
        var current = From(document, projectId);
        return TaskIds.SequenceEqual(current.TaskIds) && SectionIds.SequenceEqual(current.SectionIds);
    }
}
