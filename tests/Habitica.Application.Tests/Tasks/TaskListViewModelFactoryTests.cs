using Habitica.Application.Tasks;
using Habitica.Domain.Tasks;

namespace Habitica.Application.Tests.Tasks;

public sealed class TaskListViewModelFactoryTests
{
    private readonly TaskListViewModelFactory _factory = new();
    private readonly TaskOrderPlanner _orderPlanner = new();

    [Fact]
    public void Create_groups_tasks_by_type_and_hides_completed_items_when_requested()
    {
        var snapshot = new TaskCollectionSnapshot(
            DateTimeOffset.Parse("2026-04-24T12:00:00Z"),
            new[]
            {
                new TaskSnapshot("todo-open", "Buy milk", TaskType.Todo, false, 1, null, null, 2.7m),
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
        Assert.Equal(2.7m, viewModel.Groups.Single(group => group.Type == TaskType.Todo).Items[0].Value);
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

    [Fact]
    public void Create_filters_by_selected_types_and_sorts_by_value()
    {
        var snapshot = new TaskCollectionSnapshot(
            DateTimeOffset.Parse("2026-04-24T12:00:00Z"),
            new[]
            {
                new TaskSnapshot("habit-low", "Low habit", TaskType.Habit, false, 1m, null, null, -3m),
                new TaskSnapshot("habit-high", "High habit", TaskType.Habit, false, 1m, null, null, 10m),
                new TaskSnapshot("todo-open", "Buy milk", TaskType.Todo, false, 1m, null, null, 2m)
            });

        var viewModel = _factory.Create(
            snapshot,
            new TaskListFilter(
                SelectedTypes: new[] { TaskType.Habit },
                SortMode: TaskListSortMode.ValueHigh));

        var group = Assert.Single(viewModel.Groups);
        Assert.Equal(TaskType.Habit, group.Type);
        Assert.Equal(new[] { "High habit", "Low habit" }, group.Items.Select(item => item.Text).ToArray());
    }

    [Fact]
    public void Create_preserves_snapshot_order_for_default_habitica_sort()
    {
        var snapshot = new TaskCollectionSnapshot(
            DateTimeOffset.Parse("2026-04-24T12:00:00Z"),
            new[]
            {
                new TaskSnapshot("todo-b", "Beta", TaskType.Todo, false, 1m, null, null, 2m),
                new TaskSnapshot("todo-a", "Alpha", TaskType.Todo, false, 1m, null, null, 1m),
                new TaskSnapshot("todo-c", "Gamma", TaskType.Todo, false, 1m, null, null, 3m)
            });

        var viewModel = _factory.Create(snapshot, new TaskListFilter());

        Assert.Equal(new[] { "Beta", "Alpha", "Gamma" }, viewModel.Groups.Single().Items.Select(item => item.Text).ToArray());
    }

    [Fact]
    public void Reorder_visible_subset_preserves_hidden_items_in_place()
    {
        var nextOrder = _orderPlanner.ReorderVisibleSubset(
            new[] { "todo-1", "todo-hidden", "todo-2", "todo-3" },
            new[] { "todo-1", "todo-2", "todo-3" },
            "todo-3",
            "todo-1",
            TaskDropPlacement.Before);

        Assert.Equal(new[] { "todo-3", "todo-hidden", "todo-1", "todo-2" }, nextOrder);
    }

    [Fact]
    public void Reorder_visible_subset_ignores_invalid_cross_subset_drop()
    {
        var currentOrder = new[] { "todo-1", "todo-hidden", "todo-2" };

        var nextOrder = _orderPlanner.ReorderVisibleSubset(
            currentOrder,
            new[] { "todo-1", "todo-2" },
            "todo-1",
            "todo-hidden",
            TaskDropPlacement.After);

        Assert.Equal(currentOrder, nextOrder);
    }

    [Fact]
    public void Move_visible_item_to_index_preserves_hidden_items_in_place()
    {
        var nextOrder = _orderPlanner.MoveVisibleItemToIndex(
            new[] { "todo-1", "todo-hidden", "todo-2", "todo-3" },
            new[] { "todo-1", "todo-2", "todo-3" },
            "todo-3",
            0);

        Assert.Equal(new[] { "todo-3", "todo-hidden", "todo-1", "todo-2" }, nextOrder);
    }
}
