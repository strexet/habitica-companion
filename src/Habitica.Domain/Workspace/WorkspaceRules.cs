namespace Habitica.Domain.Workspace;

public sealed record WorkspaceViolation(string Code, Guid? EntityId = null);
public sealed record GoalProgress(int CompletedTodos, int EligibleTodos)
{
    public decimal? Fraction => EligibleTodos == 0 ? null : (decimal)CompletedTodos / EligibleTodos;
}

public static class WorkspaceRules
{
    public static IReadOnlyList<WorkspaceViolation> Validate(WorkspaceDocument document)
    {
        var errors = new List<WorkspaceViolation>();
        if (document.SchemaVersion != WorkspaceDocument.CurrentSchemaVersion) errors.Add(new("unsupported_schema"));
        if (document.Id == Guid.Empty || document.Revision < 1) errors.Add(new("invalid_workspace"));
        if (document.Categories is null || document.Projects is null || document.Sections is null || document.Labels is null
            || document.Goals is null || document.Tasks is null || document.Occurrences is null || document.Completions is null
            || document.HabitScores is null || document.Projections is null)
        {
            errors.Add(new("missing_collection"));
            return errors;
        }
        var all = document.Categories.Cast<WorkspaceEntity>().Concat(document.Projects).Concat(document.Sections)
            .Concat(document.Labels).Concat(document.Goals).Concat(document.Tasks).Concat(document.Occurrences)
            .Concat(document.Completions).Concat(document.HabitScores).Concat(document.Projections).ToArray();
        if (all.Any(entity => entity is null)) { errors.Add(new("null_entity")); return errors; }
        if (all.Length > 100000) { errors.Add(new("workspace_too_large")); return errors; }
        var identities = new HashSet<Guid> { document.Id };
        foreach (var entity in all)
        {
            if (entity.Id == Guid.Empty || !identities.Add(entity.Id)) errors.Add(new("invalid_or_duplicate_id", entity.Id));
            if (entity.Revision < 1 || entity.Revision > document.Revision) errors.Add(new("invalid_revision", entity.Id));
        }
        var categories = document.Categories.Select(value => value.Id).ToHashSet();
        var projects = document.Projects.GroupBy(value => value.Id).ToDictionary(group => group.Key, group => group.First());
        var sections = document.Sections.GroupBy(value => value.Id).ToDictionary(group => group.Key, group => group.First());
        var labels = document.Labels.Select(value => value.Id).ToHashSet();
        var tasks = document.Tasks.GroupBy(value => value.Id).ToDictionary(group => group.Key, group => group.First());
        var occurrences = document.Occurrences.GroupBy(value => value.Id).ToDictionary(group => group.Key, group => group.First());
        void Name(string? name, Guid id) { if (string.IsNullOrWhiteSpace(name) || name.Length > 256) errors.Add(new("invalid_name", id)); }
        void Reference(Guid? id, bool exists, Guid owner) { if (id is not null && !exists) errors.Add(new("missing_reference", owner)); }
        foreach (var category in document.Categories) Name(category.Name, category.Id);
        foreach (var project in document.Projects) { Name(project.Name, project.Id); Reference(project.CategoryId, project.CategoryId is { } id && categories.Contains(id), project.Id); }
        foreach (var section in document.Sections)
        {
            Name(section.Name, section.Id); Reference(section.ProjectId, projects.ContainsKey(section.ProjectId), section.Id);
            if (section.Position < 0) errors.Add(new("invalid_position", section.Id));
        }
        foreach (var label in document.Labels) Name(label.Name, label.Id);
        foreach (var goal in document.Goals)
        {
            Name(goal.Name, goal.Id);
            if (goal.TaskIds is null || goal.TaskIds.Distinct().Count() != goal.TaskIds.Count || goal.TaskIds.Any(id => !tasks.ContainsKey(id))) errors.Add(new("invalid_goal_tasks", goal.Id));
        }
        foreach (var task in document.Tasks)
        {
            if (string.IsNullOrWhiteSpace(task.Text) || task.Text.Length > 2048 || task.Notes?.Length > 10000) errors.Add(new("invalid_task_text", task.Id));
            if (!Enum.IsDefined(task.Kind) || !Enum.IsDefined(task.State) || !Enum.IsDefined(task.Priority)) errors.Add(new("invalid_task_enum", task.Id));
            if (task.CreatedAtUtc == default || task.CreatedAtUtc.Offset != TimeSpan.Zero || task.UpdatedAtUtc.Offset != TimeSpan.Zero || task.CreatedAtUtc > task.UpdatedAtUtc
                || task.ReviewAfterUtc?.Offset != null && task.ReviewAfterUtc.Value.Offset != TimeSpan.Zero
                || task.StaleSinceUtc?.Offset != null && task.StaleSinceUtc.Value.Offset != TimeSpan.Zero) errors.Add(new("invalid_task_time", task.Id));
            if (task.Kind != CompanionTaskKind.Todo && task.State is CompanionTaskState.Completed or CompanionTaskState.Cancelled) errors.Add(new("invalid_lifecycle", task.Id));
            if (task.Kind == CompanionTaskKind.Todo && task.Recurrence is not null || task.Kind == CompanionTaskKind.Habit && task.Recurrence is not null) errors.Add(new("invalid_recurrence_kind", task.Id));
            Reference(task.CategoryId, task.CategoryId is { } categoryId && categories.Contains(categoryId), task.Id);
            Reference(task.ProjectId, task.ProjectId is { } projectId && projects.ContainsKey(projectId), task.Id);
            if (task.ParentTaskId is { } parentId && (parentId == task.Id || !tasks.TryGetValue(parentId, out var parent) || parent.Kind != CompanionTaskKind.Todo || task.Kind != CompanionTaskKind.Todo)) errors.Add(new("invalid_parent_task", task.Id));
            Reference(task.SectionId, task.SectionId is { } sectionId && sections.TryGetValue(sectionId, out var section) && section.ProjectId == task.ProjectId, task.Id);
            if (task.LabelIds is not null && (task.LabelIds.Distinct().Count() != task.LabelIds.Count || task.LabelIds.Any(id => !labels.Contains(id)))) errors.Add(new("invalid_labels", task.Id));
            if (task.DependsOn is not null && (task.DependsOn.Distinct().Count() != task.DependsOn.Count || task.DependsOn.Any(id => id == task.Id || !tasks.ContainsKey(id)))) errors.Add(new("invalid_dependencies", task.Id));
            if (task.Recurrence is { } recurrence && (!Enum.IsDefined(recurrence.Frequency) || recurrence.StartDate == default || recurrence.Interval is < 1 or > 366
                || string.IsNullOrWhiteSpace(recurrence.TimeZoneId) || recurrence.TimeZoneId.Length > 128
                || recurrence.Weekdays is null || recurrence.DaysOfMonth is null
                || recurrence.Weekdays.Any(day => !Enum.IsDefined(day)) || recurrence.Weekdays.Distinct().Count() != recurrence.Weekdays.Count
                || recurrence.DaysOfMonth.Any(day => day is < 1 or > 31) || recurrence.DaysOfMonth.Distinct().Count() != recurrence.DaysOfMonth.Count
                || recurrence.Frequency == RecurrenceFrequency.Weekly && recurrence.Weekdays.Count == 0
                || recurrence.Frequency == RecurrenceFrequency.Monthly && recurrence.DaysOfMonth.Count == 0
                || recurrence.Frequency != RecurrenceFrequency.Weekly && recurrence.Weekdays.Count != 0
                || recurrence.Frequency != RecurrenceFrequency.Monthly && recurrence.DaysOfMonth.Count != 0)) errors.Add(new("invalid_recurrence", task.Id));
        }
        // Iterative traversal avoids stack exhaustion for long imported dependency chains.
        var edges = tasks.Values.ToDictionary(task => task.Id, task => (task.DependsOn ?? Array.Empty<Guid>()).Concat(task.ParentTaskId is { } parentId ? new[] { parentId } : Array.Empty<Guid>()).Distinct().Where(tasks.ContainsKey).ToArray());
        var degrees = edges.ToDictionary(pair => pair.Key, pair => pair.Value.Length);
        var dependents = tasks.Keys.ToDictionary(id => id, _ => new List<Guid>());
        foreach (var pair in edges) foreach (var id in pair.Value) dependents[id].Add(pair.Key);
        var queue = new Queue<Guid>(degrees.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        var visited = 0;
        while (queue.TryDequeue(out var id)) { visited++; foreach (var dependent in dependents[id]) if (--degrees[dependent] == 0) queue.Enqueue(dependent); }
        if (visited != tasks.Count) errors.Add(new("dependency_cycle"));
        var occurrenceKeys = new HashSet<(Guid, DateOnly)>();
        foreach (var occurrence in document.Occurrences)
        {
            if (!tasks.TryGetValue(occurrence.TaskId, out var task) || task.Kind != CompanionTaskKind.Daily || !Enum.IsDefined(occurrence.State)
                || occurrence.Date == default || !occurrenceKeys.Add((occurrence.TaskId, occurrence.Date))) errors.Add(new("invalid_occurrence", occurrence.Id));
        }
        var completionKeys = new HashSet<(Guid, Guid?)>();
        foreach (var completion in document.Completions)
        {
            if (!tasks.TryGetValue(completion.TaskId, out var task) || task.Kind == CompanionTaskKind.Habit || completion.CommandId == Guid.Empty
                || completion.CompletedAtUtc == default || completion.CompletedAtUtc.Offset != TimeSpan.Zero || !completionKeys.Add((completion.TaskId, completion.OccurrenceId))
                || task.Kind == CompanionTaskKind.Daily && (completion.OccurrenceId is not { } occurrenceId || !occurrences.TryGetValue(occurrenceId, out var occurrence) || occurrence.TaskId != task.Id || occurrence.State != OccurrenceState.Completed)
                || task.Kind == CompanionTaskKind.Todo && completion.OccurrenceId is not null) errors.Add(new("invalid_completion", completion.Id));
        }
        var completedTodos = document.Completions.Where(completion => completion.OccurrenceId is null).Select(completion => completion.TaskId).ToHashSet();
        var completedOccurrences = document.Completions.Where(completion => completion.OccurrenceId is not null).Select(completion => completion.OccurrenceId!.Value).ToHashSet();
        foreach (var task in document.Tasks.Where(task => task.Kind == CompanionTaskKind.Todo && task.State == CompanionTaskState.Completed))
            if (!completedTodos.Contains(task.Id)) errors.Add(new("missing_completion_event", task.Id));
        foreach (var occurrence in document.Occurrences.Where(occurrence => occurrence.State == OccurrenceState.Completed))
            if (!completedOccurrences.Contains(occurrence.Id)) errors.Add(new("missing_completion_event", occurrence.Id));
        foreach (var score in document.HabitScores)
            if (!tasks.TryGetValue(score.TaskId, out var task) || task.Kind != CompanionTaskKind.Habit || score.CommandId == Guid.Empty
                || score.RecordedAtUtc == default || score.RecordedAtUtc.Offset != TimeSpan.Zero || !Enum.IsDefined(score.Direction)) errors.Add(new("invalid_habit_event", score.Id));
        var projectionTasks = new HashSet<Guid>(); var externalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var projection in document.Projections)
        {
            if (!tasks.ContainsKey(projection.TaskId) || !projectionTasks.Add(projection.TaskId) || !Enum.IsDefined(projection.State)
                || projection.HabiticaTaskId is not null && (!Guid.TryParseExact(projection.HabiticaTaskId, "D", out var external) || external == Guid.Empty || !externalIds.Add(projection.HabiticaTaskId))
                || projection.State == ProjectionState.Linked && projection.HabiticaTaskId is null) errors.Add(new("invalid_projection", projection.Id));
        }
        return errors;
    }

    public static GoalProgress Progress(WorkspaceGoal goal, IEnumerable<CompanionTask> tasks)
    {
        var linked = (goal.TaskIds ?? Array.Empty<Guid>()).ToHashSet();
        var eligible = tasks.Where(task => linked.Contains(task.Id) && task.Kind == CompanionTaskKind.Todo && !task.IsArchived && task.State != CompanionTaskState.Cancelled).ToArray();
        return new(eligible.Count(task => task.State == CompanionTaskState.Completed), eligible.Length);
    }
}
