using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

public sealed record WorkspaceReminderCue(Guid TaskId, string Text, DateTimeOffset ReviewAfterUtc, long ScheduleRevision, string TimeZoneId);

// Read existing review intent only. No clock-derived stale flag, recurrence or task mutation.
public static class WorkspaceReminderCues
{
    public static IReadOnlyList<WorkspaceReminderCue> Read(WorkspaceDocument document)
    {
        if (WorkspaceRules.Validate(document).Count != 0) throw new InvalidOperationException("reminder_data_unavailable");
        if (document.Tasks.Count > 10000) throw new InvalidOperationException("reminder_capacity");
        var schedules = (document.ReviewEvents ?? []).Where(e => e.Action is TaskReviewAction.Set or TaskReviewAction.Snooze or TaskReviewAction.Clear or TaskReviewAction.MarkReviewed)
            .GroupBy(e => e.TaskId).ToDictionary(g => g.Key, g => g.MaxBy(e => e.Revision)!);
        var rows = new List<WorkspaceReminderCue>();
        var offset = 0;
        do
        {
            var page = WorkspaceTaskQuery.Evaluate(document, new(Limit:100, Offset:offset, ExpectedRevision:document.Revision),
                task => task.ReviewAfterUtc is not null && task.State is not (CompanionTaskState.Completed or CompanionTaskState.Cancelled));
            foreach (var task in page.Tasks)
            {
                schedules.TryGetValue(task.Id, out var schedule);
                rows.Add(new(task.Id, task.Text, task.ReviewAfterUtc!.Value, schedule?.Revision ?? 0, schedule?.TimeZoneId ?? "UTC"));
            }
            if (page.NextOffset is not {} next) break;
            offset = next;
        } while (offset <= 100000);
        return rows.OrderBy(r => r.ReviewAfterUtc).ThenBy(r => r.TaskId).ToArray();
    }
}
