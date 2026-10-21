using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

public enum HabiticaCoverageState { Missing, Pending, Linked, Retained, NeedsReview }
public sealed record WorkspaceHabiticaTaskCoverage(Guid TaskId, CompanionTaskKind Kind, bool Archived,
    HabiticaCoverageState State, string? HabiticaTaskId);
public sealed record WorkspaceHabiticaTagRequirement(Guid EntityId, WorkspaceOrganizationKind Kind, bool Archived);
public sealed record WorkspaceHabiticaCoverage(IReadOnlyList<WorkspaceHabiticaTaskCoverage> Tasks,
    IReadOnlyList<WorkspaceHabiticaTagRequirement> RequiredTags)
{
    public int MissingTaskLinks => Tasks.Count(task => task.State == HabiticaCoverageState.Missing);
    public int PendingTaskLinks => Tasks.Count(task => task.State == HabiticaCoverageState.Pending);
    public int TaskLinksNeedingReview => Tasks.Count(task => task.State == HabiticaCoverageState.NeedsReview);
    public int StoredTaskLinks => Tasks.Count(task => task.State is HabiticaCoverageState.Linked or HabiticaCoverageState.Retained);
    // Group/tag mappings are not implemented. Never fabricate coverage from matching names.
    public int MissingGroupTagLinks => RequiredTags.Count;
    public bool HasCompleteStoredCoverage => StoredTaskLinks == Tasks.Count && MissingGroupTagLinks == 0;
}

/// <summary>Required correspondence, including history. This read audit creates no game effects.</summary>
public static class WorkspaceHabiticaCoveragePolicy
{
    public static WorkspaceHabiticaCoverage Audit(WorkspaceDocument document)
    {
        var mappings = document.Projections.ToLookup(mapping => mapping.TaskId);
        var tasks = document.Tasks.Select(task =>
        {
            var candidates = mappings[task.Id].ToArray();
            var mapping = candidates.Length == 1 ? candidates[0] : null;
            var state = candidates.Length > 1 ? HabiticaCoverageState.NeedsReview : mapping switch
            {
                null => HabiticaCoverageState.Missing,
                { State: ProjectionState.Unlinked, HabiticaTaskId: null } => HabiticaCoverageState.Missing,
                { IsArchived: true } when !task.IsArchived => HabiticaCoverageState.NeedsReview,
                { State: ProjectionState.Pending } => HabiticaCoverageState.Pending,
                { State: ProjectionState.Linked, HabiticaTaskId: { Length: > 0 } } => task.IsArchived
                    ? HabiticaCoverageState.Retained : HabiticaCoverageState.Linked,
                _ => HabiticaCoverageState.NeedsReview
            };
            return new WorkspaceHabiticaTaskCoverage(task.Id, task.Kind, task.IsArchived, state, mapping?.HabiticaTaskId);
        }).ToArray();
        var tags = new List<WorkspaceHabiticaTagRequirement>();
        void Add(IEnumerable<WorkspaceEntity> entities, WorkspaceOrganizationKind kind)
        {
            foreach (var entity in entities) tags.Add(new(entity.Id, kind, entity.IsArchived));
        }
        // Native hierarchy is flattened in Habitica: every subtask above remains a real task.
        // Organization and saved selections correspond to tags, never extra reward-bearing Todos.
        Add(document.Projects, WorkspaceOrganizationKind.Project);
        Add(document.Categories, WorkspaceOrganizationKind.Category);
        Add(document.Sections, WorkspaceOrganizationKind.Section);
        Add(document.Labels, WorkspaceOrganizationKind.Label);
        Add(document.Goals, WorkspaceOrganizationKind.Goal);
        Add(document.SavedTaskViews ?? [], WorkspaceOrganizationKind.SavedTaskView);
        return new(tasks, tags);
    }
}
