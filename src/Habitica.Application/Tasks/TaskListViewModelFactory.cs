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
        var selectedTypes = filter.SelectedTypes;
        var normalizedSearch = filter.SearchText?.Trim();

        var groups = snapshot.Items
            .Where(task => filter.IncludeCompleted || !task.IsCompleted)
            .Where(task => selectedTypes is null || selectedTypes.Contains(task.Type))
            .Where(task => string.IsNullOrWhiteSpace(normalizedSearch) || MatchesSearch(task, normalizedSearch))
            .GroupBy(task => task.Type)
            .OrderBy(group => Array.IndexOf(GroupOrder, group.Key))
            .Select(group => new TaskGroupViewModel(
                group.Key,
                GetTitle(group.Key),
                SortTasks(group, filter.SortMode)
                    .Select(task => new TaskListItemViewModel(
                        task.Id,
                        task.Text,
                        task.Type,
                        task.IsCompleted,
                        task.Difficulty,
                        task.Notes,
                        task.DueDate,
                        task.Value,
                        task.IsChallengeTask,
                        task.SupportsPositiveScore,
                        task.SupportsNegativeScore,
                        task.HistoryPoints))
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

    private static IEnumerable<TaskSnapshot> SortTasks(
        IEnumerable<TaskSnapshot> tasks,
        TaskListSortMode sortMode)
    {
        return sortMode switch
        {
            TaskListSortMode.Habitica => tasks,
            TaskListSortMode.ValueHigh => tasks
                .OrderBy(static task => task.Value is null)
                .ThenByDescending(static task => task.Value ?? decimal.MinValue),
            TaskListSortMode.ValueLow => tasks
                .OrderBy(static task => task.Value is null)
                .ThenBy(static task => task.Value ?? decimal.MaxValue),
            TaskListSortMode.DueSoon => tasks
                .OrderBy(static task => task.DueDate is null)
                .ThenBy(static task => task.DueDate ?? DateTimeOffset.MaxValue),
            _ => tasks.OrderBy(static task => task.Text, StringComparer.OrdinalIgnoreCase)
        };
    }
}
