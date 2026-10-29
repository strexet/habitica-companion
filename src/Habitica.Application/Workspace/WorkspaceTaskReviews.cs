using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

public static class WorkspaceTaskReviews
{
    public static WorkspaceReadPage<TaskReviewEvent> Read(WorkspaceDocument document, Guid taskId, int limit = 50, int offset = 0, long? expectedRevision = null)
    {
        if (taskId == Guid.Empty || limit is < 1 or > 100 || offset is < 0 or > 100000 || expectedRevision is < 1 or > WorkspaceDocument.MaxRevision || offset > 0 && expectedRevision is null) throw new ArgumentException("invalid_filter");
        if (expectedRevision is {} revision && revision != document.Revision) throw new InvalidOperationException("stale_revision");
        if (WorkspaceRules.Validate(document).Count != 0) throw new InvalidOperationException("review_history_unavailable");
        if (!document.Tasks.Any(task => task.Id == taskId)) throw new InvalidOperationException("task_unavailable");
        var rows = (document.ReviewEvents ?? []).Where(value => value.TaskId == taskId).OrderByDescending(value => value.Revision).ToArray();
        var items = rows.Skip(offset).Take(limit).ToArray();var next = offset + items.Length;
        return new(document.Id,document.Revision,items,rows.Length,next < rows.Length,offset,next < rows.Length ? next : null);
    }
}
