using Habitica.Application.Tasks;
using Habitica.Domain.Tasks;

namespace Habitica.Application.Tests.Tasks;

public sealed class TaskListViewModelFactoryTests
{
    private readonly TaskListViewModelFactory _factory = new();

    [Fact]
    public void Create_groups_tasks_by_type_and_hides_completed_items_when_requested()
    {
        var snapshot = new TaskCollectionSnapshot(
            DateTimeOffset.Parse("2026-04-24T12:00:00Z"),
            new[]
            {
                new TaskSnapshot("todo-open", "Buy milk", TaskType.Todo, false, 1, null, null),
                new TaskSnapshot("todo-complete", "Archive notes", TaskType.Todo, true, 1, null, null),
                new TaskSnapshot("daily-open", "Exercise", TaskType.Daily, false, 1.5m, null, null),
                new TaskSnapshot("habit-open", "Read docs", TaskType.Habit, false, 0.5m, null, null)
            });

        var viewModel = _factory.Create(snapshot, new TaskListFilter(IncludeCompleted: false));

        Assert.Equal(
            new[] { TaskType.Todo, TaskType.Daily, TaskType.Habit },
            viewModel.Groups.Select(group => group.Type).ToArray());
        Assert.Single(viewModel.Groups.Single(group => group.Type == TaskType.Todo).Items);
        Assert.Equal("Buy milk", viewModel.Groups.Single(group => group.Type == TaskType.Todo).Items[0].Text);
    }

    [Fact]
    public void Create_filters_tasks_by_search_text_case_insensitively()
    {
        var snapshot = new TaskCollectionSnapshot(
            DateTimeOffset.Parse("2026-04-24T12:00:00Z"),
            new[]
            {
                new TaskSnapshot("todo-open", "Buy milk", TaskType.Todo, false, 1, null, null),
                new TaskSnapshot("daily-open", "Evening Run", TaskType.Daily, false, 1.5m, null, null)
            });

        var viewModel = _factory.Create(snapshot, new TaskListFilter(SearchText: "run"));

        Assert.Equal(1, viewModel.TotalVisibleTasks);
        Assert.Single(viewModel.Groups);
        Assert.Equal("Evening Run", viewModel.Groups[0].Items[0].Text);
    }
}
