using Habitica.Domain.Tasks;

namespace Habitica.Application.Tasks;

public sealed class TaskListViewModelFactory
{
    private static readonly TaskType[] GroupOrder =
    {
        TaskType.Todo,
        TaskType.Daily,
        TaskType.Habit,
        TaskType.Reward
    };

    public TaskListViewModel Create(TaskCollectionSnapshot snapshot, TaskListFilter filter)
    {
        var selectedTypes = filter.SelectedTypes ?? Array.Empty<TaskType>();
        var normalizedSearch = filter.SearchText?.Trim();

        var groups = snapshot.Items
            .Where(task => filter.IncludeCompleted || !task.IsCompleted)
            .Where(task => selectedTypes.Count == 0 || selectedTypes.Contains(task.Type))
            .Where(task => string.IsNullOrWhiteSpace(normalizedSearch) || MatchesSearch(task, normalizedSearch))
            .GroupBy(task => task.Type)
            .OrderBy(group => Array.IndexOf(GroupOrder, group.Key))
            .Select(group => new TaskGroupViewModel(
                group.Key,
                GetTitle(group.Key),
                group
                    .OrderBy(task => task.IsCompleted)
                    .ThenBy(task => task.Text, StringComparer.OrdinalIgnoreCase)
                    .Select(task => new TaskListItemViewModel(
                        task.Id,
                        task.Text,
                        task.IsCompleted,
                        task.Difficulty,
                        task.Notes,
                        task.DueDate,
                        task.Value))
                    .ToArray()))
            .ToArray();

        return new TaskListViewModel(groups, groups.Sum(group => group.Items.Count));
    }

    private static string GetTitle(TaskType taskType)
    {
        return taskType switch
        {
            TaskType.Todo => "To-Dos",
            TaskType.Daily => "Dailies",
            TaskType.Habit => "Habits",
            TaskType.Reward => "Rewards",
            _ => "Tasks"
        };
    }

    private static bool MatchesSearch(TaskSnapshot task, string searchText)
    {
        return task.Text.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(task.Notes)
                && task.Notes.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    }
}
