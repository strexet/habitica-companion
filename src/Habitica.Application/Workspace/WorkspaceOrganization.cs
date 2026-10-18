using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

public sealed record WorkspaceReadFilter(int Limit = 50, int Offset = 0, long? ExpectedRevision = null);
public sealed record WorkspaceReadPage<T>(Guid WorkspaceId, long Revision, IReadOnlyList<T> Items, int MatchedCount, bool Truncated, int Offset, int? NextOffset);
public sealed record WorkspaceGoalSummary(WorkspaceGoal Goal, int CompletedTodos, int EligibleTodos);

public static class WorkspaceOrganization
{
    public static WorkspaceReadPage<WorkspaceCategory> Categories(WorkspaceDocument document, WorkspaceReadFilter filter) => Page(document, filter, document.Categories.Where(value => !value.IsArchived));
    public static WorkspaceReadPage<WorkspaceLabel> Labels(WorkspaceDocument document, WorkspaceReadFilter filter) => Page(document, filter, document.Labels.Where(value => !value.IsArchived));
    public static WorkspaceReadPage<WorkspaceProject> Projects(WorkspaceDocument document, WorkspaceReadFilter filter, Guid? categoryId = null)
    {
        if (categoryId == Guid.Empty) throw new ArgumentException("invalid_filter");
        return Page(document, filter, document.Projects.Where(value => !value.IsArchived && (categoryId is null || value.CategoryId == categoryId)));
    }
    public static WorkspaceReadPage<WorkspaceSection> Sections(WorkspaceDocument document, WorkspaceReadFilter filter, Guid projectId)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("invalid_filter");
        var active = document.Projects.Any(value => value.Id == projectId && !value.IsArchived);
        return Page(document, filter, document.Sections.Where(value => active && !value.IsArchived && value.ProjectId == projectId).OrderBy(value => value.Position));
    }
    public static WorkspaceReadPage<WorkspaceGoalSummary> Goals(WorkspaceDocument document, WorkspaceReadFilter filter)
    {
        var page = Page(document, filter, document.Goals.Where(value => !value.IsArchived));
        var tasks = document.Tasks.ToDictionary(task => task.Id);
        return new(page.WorkspaceId,page.Revision,page.Items.Select(goal =>
        {
            var linked = goal.TaskIds.Select(id => tasks[id]);
            var progress = WorkspaceRules.Progress(goal,linked);
            return new WorkspaceGoalSummary(goal,progress.CompletedTodos,progress.EligibleTodos);
        }).ToArray(),page.MatchedCount,page.Truncated,page.Offset,page.NextOffset);
    }
    private static WorkspaceReadPage<T> Page<T>(WorkspaceDocument document, WorkspaceReadFilter filter, IEnumerable<T> source)
    {
        if (filter.Limit is < 1 or > 100 || filter.Offset is < 0 or > 100000 || filter.ExpectedRevision is < 1 or > WorkspaceDocument.MaxRevision
            || filter.Offset > 0 && filter.ExpectedRevision is null) throw new ArgumentException("invalid_filter");
        if (filter.ExpectedRevision is { } revision && revision != document.Revision) throw new InvalidOperationException("stale_revision");
        var matched = source.ToArray(); var items = matched.Skip(filter.Offset).Take(filter.Limit).ToArray(); var next = filter.Offset + items.Length;
        return new(document.Id,document.Revision,items,matched.Length,next < matched.Length,filter.Offset,next < matched.Length ? next : null);
    }
}
