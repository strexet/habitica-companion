using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

/// <summary>Prospective policy only. Stored commands and hashes retain their original meaning.</summary>
public static class WorkspaceTodoProjectionPolicy
{
    public const string Version = "todo-required-v1";
    public static bool Applies(WorkspaceDocument document, WorkspaceCommand command) =>
        command.Operation == WorkspaceOperation.CreateTodo ||
        command.Operation is WorkspaceOperation.UpdateTask or WorkspaceOperation.CompleteTask &&
        document.Tasks.Any(task => task.Id == command.TaskId && task.Kind == CompanionTaskKind.Todo);
    public const string DailyVersion = "daily-required-v1";
    public const string HabitVersion = "habit-required-v1";
    public static WorkspaceCommand Prepare(WorkspaceDocument document, WorkspaceCommand command)
    {
        if (command.Operation == WorkspaceOperation.CreateDaily) return command with { ProjectToHabitica = true };
        if (command.Operation == WorkspaceOperation.CreateHabit && command.Patch is { } patch)
        {
            var details = patch.HabiticaDetails ?? new HabiticaTaskDetails();
            return command with { ProjectToHabitica = true, Patch = patch with { HabiticaDetails = details with { Up = details.Up ?? true, Down = details.Down ?? true } } };
        }
        return Applies(document, command) ? command with { ProjectToHabitica = true } : command;
    }
    public static string? Rejection(WorkspaceDocument document, WorkspaceCommand command)
    {
        if (command.Operation == WorkspaceOperation.CreateDaily) return command.ProjectToHabitica ? DailyProjectionSchedule.RuleFailure(command.Patch?.Recurrence) : "daily_projection_required";
        if (command.Operation == WorkspaceOperation.CreateHabit) return command.ProjectToHabitica ? null : "habit_projection_required";
        if (!Applies(document, command)) return null;
        if (!command.ProjectToHabitica) return "todo_projection_required";
        var task = document.Tasks.FirstOrDefault(task => task.Id == command.TaskId);
        // Historical completion import must not create an active remote duplicate or grant rewards.
        if (task?.State == CompanionTaskState.Completed &&
            !document.Projections.Any(mapping => mapping.TaskId == task.Id &&
                (mapping.HabiticaTaskId is not null || mapping.State != ProjectionState.Unlinked)))
            return "historical_projection_requires_review";
        return null;
    }
}
