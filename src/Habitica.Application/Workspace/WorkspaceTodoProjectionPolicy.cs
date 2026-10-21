using Habitica.Domain.Workspace;
namespace Habitica.Application.Workspace;

/// <summary>Prospective policy only. Stored commands and hashes retain their original meaning.</summary>
public static class WorkspaceTodoProjectionPolicy
{
    public const string Version = "todo-required-v1";
    public const string DailyVersion = "daily-required-v1";
    public const string HabitVersion = "habit-required-v1";
    public const string TaskVersion = "task-required-v1";
    public static bool IsNativeScheduleEdit(WorkspaceCommand command) => command.Operation == WorkspaceOperation.UpdateTask
        && command.Patch is { } patch && (patch.Recurrence is not null || patch.ClearRecurrence)
        && (patch with { Recurrence = null, ClearRecurrence = false }) == new WorkspaceTaskPatch();
    public static bool Applies(WorkspaceDocument document, WorkspaceCommand command) =>
        command.Operation is WorkspaceOperation.CreateTodo or WorkspaceOperation.CreateDaily or WorkspaceOperation.CreateHabit
        || !IsNativeScheduleEdit(command) && command.Operation is WorkspaceOperation.UpdateTask or WorkspaceOperation.CompleteTask or WorkspaceOperation.ScoreHabit
            && document.Tasks.Any(task => task.Id == command.TaskId);
    public static WorkspaceCommand Prepare(WorkspaceDocument document, WorkspaceCommand command)
    {
        if (command.Operation == WorkspaceOperation.CreateHabit && command.Patch is { } patch)
        {
            var details = patch.HabiticaDetails ?? new HabiticaTaskDetails();
            return command with { ProjectToHabitica = true, Patch = patch with { HabiticaDetails = details with { Up = details.Up ?? true, Down = details.Down ?? true } } };
        }
        return Applies(document, command) ? command with { ProjectToHabitica = true } : command;
    }
    public static string? Rejection(WorkspaceDocument document, WorkspaceCommand command)
    {
        var task = document.Tasks.FirstOrDefault(task => task.Id == command.TaskId);
        var mappings = document.Projections.Where(mapping => mapping.TaskId == command.TaskId).ToArray();
        if (IsNativeScheduleEdit(command)) return mappings.Any(mapping => mapping.HabiticaTaskId is not null || mapping.State != ProjectionState.Unlinked) ? "unsupported_projection" : null;
        if (!Applies(document, command)) return null;
        var kind = command.Operation == WorkspaceOperation.CreateDaily ? CompanionTaskKind.Daily
            : command.Operation == WorkspaceOperation.CreateHabit ? CompanionTaskKind.Habit : task?.Kind ?? CompanionTaskKind.Todo;
        if (!command.ProjectToHabitica) return kind switch { CompanionTaskKind.Daily => "daily_projection_required", CompanionTaskKind.Habit => "habit_projection_required", _ => "todo_projection_required" };
        if (mappings.Any(mapping => mapping.IsArchived && (mapping.HabiticaTaskId is not null || mapping.State != ProjectionState.Unlinked)
            || mapping.State == ProjectionState.Unlinked && mapping.HabiticaTaskId is not null)) return "projection_review_required";
        var linked = mappings.Any(mapping => !mapping.IsArchived && mapping.HabiticaTaskId is not null);
        if (task?.Kind == CompanionTaskKind.Todo && task.State == CompanionTaskState.Completed
            && !mappings.Any(mapping => !mapping.IsArchived && (mapping.HabiticaTaskId is not null || mapping.State != ProjectionState.Unlinked)))
            return "historical_projection_requires_review";
        if (!linked && task?.Kind == CompanionTaskKind.Habit && (task.Deadline is not null || task.HabiticaDetails?.Checklist is not null)) return "unsupported_habit_fields";
        if (!linked && task?.Kind == CompanionTaskKind.Daily && task.Deadline is not null) return "unsupported_daily_fields";
        if (kind == CompanionTaskKind.Daily && (command.Operation == WorkspaceOperation.CreateDaily || !linked))
            return DailyProjectionSchedule.RuleFailure(command.Patch?.Recurrence ?? task?.Recurrence);
        return null;
    }
}
