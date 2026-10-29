using System.Text.Json.Serialization;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

public sealed record WorkspaceTaskFilter(string? Query = null, CompanionTaskKind? Kind = null,
    CompanionTaskState? State = null, CompanionPriority? Priority = null, Guid? ProjectId = null,
    Guid? CategoryId = null, Guid? LabelId = null, DateOnly? PlannedFrom = null, DateOnly? PlannedTo = null,
    DateOnly? DeadlineFrom = null, DateOnly? DeadlineTo = null, ProjectionState? ProjectionState = null,
    int Limit = 50, int Offset = 0, long? ExpectedRevision = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TodoExecutionStage? ExecutionStage = null);

public sealed record WorkspaceTaskPage(Guid WorkspaceId, long Revision, IReadOnlyList<CompanionTask> Tasks,
    int MatchedCount, bool Truncated, int Offset, int? NextOffset);

public static class WorkspaceTaskQuery
{
    public static WorkspaceTaskPage Evaluate(WorkspaceDocument document, WorkspaceTaskFilter filter) => Evaluate(document, filter, null);

    internal static WorkspaceTaskPage Evaluate(WorkspaceDocument document, WorkspaceTaskFilter filter, Func<CompanionTask,bool>? eligible)
    {
        if (filter.Query?.Length > 256 || filter.Kind is { } kind && !Enum.IsDefined(kind)
            || filter.State is { } state && !Enum.IsDefined(state) || filter.Priority is { } priority && !Enum.IsDefined(priority)
            || filter.ProjectionState is { } projection && !Enum.IsDefined(projection)
            || filter.ExecutionStage is { } stage && (!Enum.IsDefined(stage)
                || filter.Kind is { } stageKind && stageKind != CompanionTaskKind.Todo
                || filter.State is { } stageState && stageState != CompanionTaskState.Active)
            || filter.ProjectId == Guid.Empty || filter.CategoryId == Guid.Empty || filter.LabelId == Guid.Empty
            || filter.Limit is < 1 or > 100 || filter.Offset is < 0 or > 100000
            || filter.ExpectedRevision is < 1 or > WorkspaceDocument.MaxRevision
            || filter.Offset > 0 && filter.ExpectedRevision is null
            || !ValidRange(filter.PlannedFrom, filter.PlannedTo) || !ValidRange(filter.DeadlineFrom, filter.DeadlineTo))
            throw new ArgumentException("invalid_filter", nameof(filter));
        if (filter.ExpectedRevision is { } revision && revision != document.Revision)
            throw new InvalidOperationException("stale_revision");
        var query = Fold(filter.Query ?? "");
        var projects = document.Projects.ToDictionary(value => value.Id);
        var mappings = document.Projections.Where(value => !value.IsArchived).ToDictionary(value => value.TaskId);
        var page = new List<CompanionTask>(); var matched = 0;
        foreach (var task in document.Tasks)
        {
            if (task.IsArchived || eligible is not null && !eligible(task) || filter.Kind is { } k && task.Kind != k || filter.State is { } s && task.State != s
                || filter.ExecutionStage is { } executionStage && (task.Kind != CompanionTaskKind.Todo || task.State != CompanionTaskState.Active
                    || (task.ExecutionStage ?? TodoExecutionStage.Inbox) != executionStage)
                || filter.Priority is { } p && task.Priority != p || filter.ProjectId is { } project && task.ProjectId != project
                || filter.LabelId is { } label && !(task.LabelIds?.Contains(label) ?? false)
                || !Within(task.PlannedDate, filter.PlannedFrom, filter.PlannedTo) || !Within(task.Deadline, filter.DeadlineFrom, filter.DeadlineTo)) continue;
            var category = task.CategoryId ?? (task.ProjectId is { } projectId && projects.TryGetValue(projectId, out var owner) && !owner.IsRemoved ? owner.CategoryId : null);
            var mapping = mappings.TryGetValue(task.Id, out var value) ? value.State : ProjectionState.Unlinked;
            if (filter.CategoryId is { } c && category != c || filter.ProjectionState is { } m && mapping != m
                || query.Length > 0 && !Fold(task.Text).Contains(query, StringComparison.Ordinal) && !Fold(task.Notes ?? "").Contains(query, StringComparison.Ordinal)) continue;
            if (matched++ >= filter.Offset && page.Count < filter.Limit) page.Add(task);
        }
        var next = filter.Offset + page.Count;
        return new(document.Id, document.Revision, page, matched, next < matched, filter.Offset, next < matched ? next : null);
    }

    private static bool ValidRange(DateOnly? from, DateOnly? to) => from != DateOnly.MinValue && to != DateOnly.MinValue && (from is null || to is null || from <= to);
    private static bool Within(DateOnly? value, DateOnly? from, DateOnly? to) => from is null && to is null || value is not null && (from is null || value >= from) && (to is null || value <= to);
    public static string Fold(string value) => WorkspaceSearchNormalization.Normalize(value).Replace("\u0130", "i\u0307", StringComparison.Ordinal).ToLowerInvariant().Replace('\u03c2', '\u03c3');
}
