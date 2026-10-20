using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

public enum WorkspaceHierarchyMode { Subtree, Children, Ancestors }
public enum WorkspaceHierarchyParentStatus { None, Available, Archived }
public enum WorkspaceHierarchyParentCoverage { None, Page, EarlierPage, OutsideSelection }
public sealed record WorkspaceHierarchyFilter(Guid? TaskId = null, WorkspaceHierarchyMode Mode = WorkspaceHierarchyMode.Subtree,
    int Limit = 50, int Offset = 0, long? ExpectedRevision = null);
public sealed record WorkspaceHierarchyNode(CompanionTask Task, int Depth, int ChildCount,
    WorkspaceHierarchyParentStatus ParentStatus, WorkspaceHierarchyParentCoverage ParentCoverage, int? ParentOffset);
public sealed record WorkspaceHierarchyPage(Guid WorkspaceId, long Revision, Guid? TaskId, WorkspaceHierarchyMode Mode,
    CompanionTask? ContextTask, IReadOnlyList<WorkspaceHierarchyNode> Items, int TotalTodoCount,
    int MatchedCount, int Offset, int? NextOffset, bool Truncated);

public static class WorkspaceTodoHierarchy
{
    public static WorkspaceHierarchyPage Evaluate(WorkspaceDocument document, WorkspaceHierarchyFilter filter)
    {
        if (!Enum.IsDefined(filter.Mode) || filter.TaskId == Guid.Empty || filter.Mode == WorkspaceHierarchyMode.Ancestors && filter.TaskId is null
            || filter.Limit is < 1 or > 100 || filter.Offset is < 0 or > 100000 || filter.Offset > 0 && filter.ExpectedRevision is null
            || filter.ExpectedRevision is < 1 or > WorkspaceDocument.MaxRevision) throw new ArgumentException("invalid_filter");
        if (filter.ExpectedRevision is { } revision && revision != document.Revision) throw new InvalidOperationException("stale_revision");
        if (WorkspaceRules.Validate(document).Count != 0) throw new InvalidOperationException("invalid_workspace");
        var todos = document.Tasks.Where(task => task.Kind == CompanionTaskKind.Todo).ToArray();
        var tasks = todos.ToDictionary(task => task.Id);
        var context = filter.TaskId is { } id ? tasks.GetValueOrDefault(id) ?? throw new InvalidOperationException("task_unavailable") : null;
        var children = tasks.Keys.ToDictionary(id => id, _ => new List<CompanionTask>());
        var roots = new List<CompanionTask>();
        foreach (var task in todos)
            if (task.ParentTaskId is { } parent) children[parent].Add(task); else roots.Add(task);
        var ordered = new List<CompanionTask>(tasks.Count); var depth = new Dictionary<Guid, int>();
        var stack = new Stack<(CompanionTask Task, int Depth)>();
        for (var index = roots.Count - 1; index >= 0; index--) stack.Push((roots[index], 0));
        while (stack.TryPop(out var entry))
        {
            ordered.Add(entry.Task); depth.Add(entry.Task.Id, entry.Depth);
            var descendants = children[entry.Task.Id];
            for (var index = descendants.Count - 1; index >= 0; index--) stack.Push((descendants[index], entry.Depth + 1));
        }
        List<CompanionTask> selected;
        if (filter.Mode == WorkspaceHierarchyMode.Children) selected = context is null ? roots : children[context.Id];
        else if (filter.Mode == WorkspaceHierarchyMode.Ancestors)
        {
            selected = []; var parent = context!.ParentTaskId;
            while (parent is { } parentId) { var ancestor = tasks[parentId]; selected.Add(ancestor); parent = ancestor.ParentTaskId; }
            selected.Reverse();
        }
        else if (context is null) selected = ordered;
        else
        {
            var first = ordered.FindIndex(task => task.Id == context.Id); var end = first + 1;
            while (end < ordered.Count && depth[ordered[end].Id] > depth[context.Id]) end++;
            selected = ordered.GetRange(first, end - first);
        }
        var positions = selected.Select((task, index) => (task.Id, index)).ToDictionary(pair => pair.Id, pair => pair.index);
        var page = selected.Skip(filter.Offset).Take(filter.Limit).ToArray();
        var next = filter.Offset + page.Length;
        var items = page.Select(task =>
        {
            var parent = task.ParentTaskId is { } parentId ? tasks[parentId] : null;
            var parentOffset = parent is not null && positions.TryGetValue(parent.Id, out var position) ? (int?)position : null;
            var coverage = parent is null ? WorkspaceHierarchyParentCoverage.None : parentOffset is null ? WorkspaceHierarchyParentCoverage.OutsideSelection
                : parentOffset < filter.Offset ? WorkspaceHierarchyParentCoverage.EarlierPage : WorkspaceHierarchyParentCoverage.Page;
            return new WorkspaceHierarchyNode(task, depth[task.Id], children[task.Id].Count,
                parent is null ? WorkspaceHierarchyParentStatus.None : parent.IsArchived ? WorkspaceHierarchyParentStatus.Archived : WorkspaceHierarchyParentStatus.Available, coverage, parentOffset);
        }).ToArray();
        return new(document.Id, document.Revision, filter.TaskId, filter.Mode, context, items, tasks.Count, selected.Count,
            filter.Offset, next < selected.Count ? next : null, next < selected.Count);
    }
}
