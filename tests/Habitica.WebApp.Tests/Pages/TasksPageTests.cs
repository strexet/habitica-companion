using System.Text.Json;
using Bunit;
using Habitica.Application.Tasks;
using Habitica.Domain.Sync;
using Habitica.Domain.Tasks;
using Habitica.Domain.User;
using Habitica.Storage;
using Habitica.WebApp.Pages;
using Habitica.WebApp.State;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Pages;

public sealed class TasksPageTests : BunitContext
{
    [Fact]
    public void Renders_cached_tasks_and_freshness_state()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new TaskListViewModelFactory());
        Services.AddSingleton(new TaskOrderPlanner());
        Services.AddSingleton<IKeyValueStorage>(new FakeKeyValueStorage());
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: null,
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-24T10:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Stale,
                TaskSnapshot: new TaskCollectionSnapshot(
                    DateTimeOffset.Parse("2026-04-24T10:00:00Z"),
                    new[]
                    {
                        new TaskSnapshot("todo-1", "Buy milk", TaskType.Todo, false, 1.5m, "2 liters", null, -4.2m),
                        new TaskSnapshot("todo-2", "Archive notes", TaskType.Todo, true, 1m, null, null, 5m)
                    }))));

        var cut = Render<TasksPage>();

        Assert.Contains("Tasks may need refresh", cut.Markup);
        Assert.Contains("Buy milk", cut.Markup);
        var taskCard = cut.Find("[data-task-id='todo-1']");
        Assert.Contains("2 liters", taskCard.TextContent);
        Assert.DoesNotContain("Priority", taskCard.TextContent);
        Assert.DoesNotContain("-4.2", taskCard.TextContent);
        Assert.Null(taskCard.QuerySelector("[data-testid='move-task-top-todo-1']"));
        Assert.Null(taskCard.QuerySelector("[data-testid='move-task-bottom-todo-1']"));
        Assert.NotNull(taskCard.QuerySelector(".task-details-toggle"));
        Assert.NotNull(taskCard.QuerySelector("[data-testid='score-task-todo-1']"));
        var taskActions = taskCard.QuerySelector(".task-card-actions");
        Assert.NotNull(taskActions);
        Assert.NotNull(taskActions!.QuerySelector("[data-testid='score-task-todo-1']"));
        Assert.NotNull(taskActions.QuerySelector(".task-details-toggle"));
        Assert.Contains("Refresh tasks before scoring.", taskCard.TextContent);
        var taskCardStyle = taskCard.GetAttribute("style");
        Assert.Contains("color-mix", taskCardStyle);
        Assert.Contains("var(--task-neutral)", taskCardStyle);
        Assert.Contains("var(--task-negative)", taskCardStyle);
        Assert.DoesNotContain("var(--task-positive)", taskCardStyle);
        Assert.Contains("To-Dos", cut.Markup);
        Assert.DoesNotContain("Archive notes", cut.Markup);

        cut.Find("[data-task-id='todo-1'] .task-details-toggle").Click();
        taskCard = cut.Find("[data-task-id='todo-1']");
        Assert.Contains("Value", taskCard.TextContent);
        Assert.Contains("-4.2", taskCard.TextContent);
        Assert.Contains("Priority", taskCard.TextContent);
        Assert.Contains("Due", taskCard.TextContent);
        Assert.Contains("Open", taskCard.TextContent);
        Assert.NotNull(taskCard.QuerySelector("[data-testid='score-task-todo-1']"));

        cut.FindAll("button").Single(button => button.TextContent.Contains("Show completed", StringComparison.Ordinal)).Click();

        Assert.Contains("Archive notes", cut.Markup);
        Assert.Contains("completed-task", cut.Markup);

        cut.FindAll("button").Single(button => button.GetAttribute("aria-label") == "Collapse To-Dos").Click();

        Assert.DoesNotContain("Buy milk", cut.Markup);
    }

    [Fact]
    public async Task Loads_stored_task_category_preferences_for_current_user()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new TaskListViewModelFactory());
        Services.AddSingleton(new TaskOrderPlanner());
        var storage = new FakeKeyValueStorage();
        await storage.SetAsync(
            $"{StorageKeys.TasksPagePreferences}/user-id",
            new
            {
                foldedCategories = new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    ["Habit"] = true
                },
                showCompletedCategories = new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    ["Habit"] = true
                }
            },
            CancellationToken.None);
        Services.AddSingleton<IKeyValueStorage>(storage);
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                UserId: "user-id",
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-24T10:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: new TaskCollectionSnapshot(
                    DateTimeOffset.Parse("2026-04-24T10:00:00Z"),
                    new[]
                    {
                        new TaskSnapshot("habit-1", "Read docs", TaskType.Habit, true, 1m, null, null, 8m)
                    }))));

        var cut = Render<TasksPage>();

        Assert.Contains("Habits", cut.Markup);
        Assert.Contains("1", cut.Markup);
        Assert.DoesNotContain("Read docs", cut.Markup);
        Assert.Equal("Expand Habits", cut.FindAll("button").Single(button => button.GetAttribute("aria-label") == "Expand Habits").GetAttribute("title"));
    }

    [Fact]
    public void Task_actions_score_habits_through_session_controller()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new TaskListViewModelFactory());
        Services.AddSingleton(new TaskOrderPlanner());
        Services.AddSingleton<IKeyValueStorage>(new FakeKeyValueStorage());
        var controller = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                UserId: "user-id",
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-24T10:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: new TaskCollectionSnapshot(
                    DateTimeOffset.Parse("2026-04-24T10:00:00Z"),
                    new[]
                    {
                        new TaskSnapshot("habit-1", "Read docs", TaskType.Habit, false, 1m, null, null, 8m, SupportsPositiveScore: true, SupportsNegativeScore: true)
                    }),
                UserSnapshot: new UserSnapshot(
                    DateTimeOffset.Parse("2026-04-24T10:00:00Z"),
                    "Mage Tester",
                    "wizard",
                    15,
                    50m,
                    50m,
                    20m,
                    40m,
                    0m,
                    100m,
                    10m,
                    "party-1",
                    null,
                    null,
                    new EquipmentSnapshot(
                        new GearSlotsSnapshot(null, null, null, null, null),
                        new GearSlotsSnapshot(null, null, null, null, null)),
                    new InventorySnapshot(0, 0, 0, 0, 0, 0, Array.Empty<string>())),
                UserFreshness: SnapshotFreshnessState.Fresh));
        Services.AddSingleton<IAppSessionController>(controller);

        var cut = Render<TasksPage>();

        var taskActions = cut.Find("[data-task-id='habit-1'] .task-card-actions");
        Assert.NotNull(taskActions.QuerySelector("[data-testid='task-score-count-habit-1']"));
        Assert.NotNull(taskActions.QuerySelector("[data-testid='score-task-up-habit-1']"));
        Assert.NotNull(taskActions.QuerySelector("[data-testid='score-task-down-habit-1']"));
        Assert.NotNull(taskActions.QuerySelector(".task-details-toggle"));

        cut.Find("[data-testid='task-score-count-habit-1']").Change("3");
        cut.Find("[data-testid='score-task-up-habit-1']").Click();

        var request = Assert.Single(controller.ScoreTaskCalls);
        Assert.Equal("habit-1", request.TaskId);
        Assert.Equal(TaskScoreDirection.Up, request.Direction);
        Assert.Equal(3, request.Count);
    }

    [Fact]
    public void Type_filter_can_hide_a_task_group()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new TaskListViewModelFactory());
        Services.AddSingleton(new TaskOrderPlanner());
        Services.AddSingleton<IKeyValueStorage>(new FakeKeyValueStorage());
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: false,
                DisplayName: null,
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-24T10:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: new TaskCollectionSnapshot(
                    DateTimeOffset.Parse("2026-04-24T10:00:00Z"),
                    new[]
                    {
                        new TaskSnapshot("todo-1", "Buy milk", TaskType.Todo, false, 1m, null, null, 1m),
                        new TaskSnapshot("habit-1", "Read docs", TaskType.Habit, false, 1m, null, null, 2m)
                    }))));

        var cut = Render<TasksPage>();

        cut.Find("[data-testid='task-type-filter-Todo']").Change(false);

        Assert.DoesNotContain("Buy milk", cut.Markup);
        Assert.Contains("Read docs", cut.Markup);
    }

    [Fact]
    public void Task_statistics_render_history_charts_for_selected_period()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new TaskListViewModelFactory());
        Services.AddSingleton(new TaskOrderPlanner());
        Services.AddSingleton<IKeyValueStorage>(new FakeKeyValueStorage());
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: false,
                DisplayName: null,
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.UtcNow,
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: new TaskCollectionSnapshot(
                    DateTimeOffset.UtcNow,
                    new[]
                    {
                        new TaskSnapshot(
                            "todo-history",
                            "History task",
                            TaskType.Todo,
                            false,
                            1m,
                            null,
                            DateTimeOffset.Now.Date,
                            4m,
                            History: new[]
                            {
                                new TaskHistoryPoint(DateTimeOffset.Now.AddDays(-2), 2m),
                                new TaskHistoryPoint(DateTimeOffset.Now.AddDays(-1), 4m)
                            })
                    }))));

        var cut = Render<TasksPage>();

        Assert.Contains("Task statistics", cut.Markup);
        Assert.Contains("Task-history histogram", cut.Markup);
        Assert.Contains("Month activity", cut.Markup);

        cut.Find("[data-testid='task-analysis-period']").Change("Month");
        Assert.Contains("Last 30 days", cut.Markup);

        cut.FindAll("button").Single(button => button.TextContent.Contains("Details", StringComparison.Ordinal)).Click();
        Assert.Contains("Task value history", cut.Markup);
        Assert.Contains("Task month activity", cut.Markup);
    }

    [Fact]
    public async Task Task_drag_drop_persists_order_for_current_list()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new TaskListViewModelFactory());
        Services.AddSingleton(new TaskOrderPlanner());
        var storage = new FakeKeyValueStorage();
        Services.AddSingleton<IKeyValueStorage>(storage);
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: false,
                DisplayName: null,
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-24T10:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: new TaskCollectionSnapshot(
                    DateTimeOffset.Parse("2026-04-24T10:00:00Z"),
                    new[]
                    {
                        new TaskSnapshot("todo-1", "Alpha", TaskType.Todo, false, 1m, null, null, 1m),
                        new TaskSnapshot("todo-2", "Beta", TaskType.Todo, false, 1m, null, null, 2m),
                        new TaskSnapshot("todo-3", "Gamma", TaskType.Todo, false, 1m, null, null, 3m)
                    }))));

        var cut = Render<TasksPage>();
        AssertMarkupOrder(cut.Markup, "Alpha", "Beta", "Gamma");
        Assert.Empty(cut.FindAll("[data-testid^='move-task-']"));

        cut.Find("[data-testid='rearrange-tasks-Todo']").Click();

        Assert.NotEmpty(cut.FindAll("[data-testid^='move-task-']"));
        Assert.NotNull(cut.Find("[data-testid='drag-task-todo-1']"));

        await cut.InvokeAsync(() => cut.Instance.HandleTaskDropped("Todo", "todo-1", "todo-2", insertAfter: true));

        AssertMarkupOrder(cut.Markup, "Beta", "Alpha", "Gamma");
        var preferences = await storage.GetAsync<TaskOrderPreferences>(StorageKeys.TaskOrderPreferences, CancellationToken.None);
        Assert.NotNull(preferences);
        Assert.Equal(new[] { "todo-2", "todo-1", "todo-3" }, preferences!.OrdersByType["Todo"]);

        var rerendered = Render<TasksPage>();
        AssertMarkupOrder(rerendered.Markup, "Beta", "Alpha", "Gamma");
    }

    [Fact]
    public async Task Task_reorder_keyboard_handle_uses_same_local_order_path()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new TaskListViewModelFactory());
        Services.AddSingleton(new TaskOrderPlanner());
        var storage = new FakeKeyValueStorage();
        Services.AddSingleton<IKeyValueStorage>(storage);
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: false,
                DisplayName: null,
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-24T10:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: new TaskCollectionSnapshot(
                    DateTimeOffset.Parse("2026-04-24T10:00:00Z"),
                    new[]
                    {
                        new TaskSnapshot("todo-1", "Alpha", TaskType.Todo, false, 1m, null, null, 1m),
                        new TaskSnapshot("todo-2", "Beta", TaskType.Todo, false, 1m, null, null, 2m),
                        new TaskSnapshot("todo-3", "Gamma", TaskType.Todo, false, 1m, null, null, 3m)
                    }))));

        var cut = Render<TasksPage>();

        cut.Find("[data-testid='rearrange-tasks-Todo']").Click();
        cut.Find("[data-testid='drag-task-todo-1']").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        AssertMarkupOrder(cut.Markup, "Beta", "Alpha", "Gamma");
        var preferences = await storage.GetAsync<TaskOrderPreferences>(StorageKeys.TaskOrderPreferences, CancellationToken.None);
        Assert.NotNull(preferences);
        Assert.Equal(new[] { "todo-2", "todo-1", "todo-3" }, preferences!.OrdersByType["Todo"]);
    }

    [Fact]
    public async Task Task_reorder_buttons_use_same_local_order_path()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new TaskListViewModelFactory());
        Services.AddSingleton(new TaskOrderPlanner());
        var storage = new FakeKeyValueStorage();
        Services.AddSingleton<IKeyValueStorage>(storage);
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: false,
                DisplayName: null,
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-24T10:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: new TaskCollectionSnapshot(
                    DateTimeOffset.Parse("2026-04-24T10:00:00Z"),
                    new[]
                    {
                        new TaskSnapshot("todo-1", "Alpha", TaskType.Todo, false, 1m, null, null, 1m),
                        new TaskSnapshot("todo-2", "Beta", TaskType.Todo, false, 1m, null, null, 2m),
                        new TaskSnapshot("todo-3", "Gamma", TaskType.Todo, false, 1m, null, null, 3m)
                    }))));

        var cut = Render<TasksPage>();

        cut.Find("[data-testid='rearrange-tasks-Todo']").Click();
        cut.Find("[data-testid='move-task-down-todo-1']").Click();
        AssertMarkupOrder(cut.Markup, "Beta", "Alpha", "Gamma");

        cut.Find("[data-testid='move-task-bottom-todo-1']").Click();
        AssertMarkupOrder(cut.Markup, "Beta", "Gamma", "Alpha");

        cut.Find("[data-testid='move-task-top-todo-1']").Click();
        AssertMarkupOrder(cut.Markup, "Alpha", "Beta", "Gamma");

        var preferences = await storage.GetAsync<TaskOrderPreferences>(StorageKeys.TaskOrderPreferences, CancellationToken.None);
        Assert.NotNull(preferences);
        Assert.Equal(new[] { "todo-1", "todo-2", "todo-3" }, preferences!.OrdersByType["Todo"]);
    }

    [Fact]
    public async Task Stored_task_order_ignores_unknown_ids_and_appends_new_tasks()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new TaskListViewModelFactory());
        Services.AddSingleton(new TaskOrderPlanner());
        var storage = new FakeKeyValueStorage();
        await storage.SetAsync(
            StorageKeys.TaskOrderPreferences,
            new TaskOrderPreferences(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Todo"] = new[] { "missing-task", "todo-2" }
            }),
            CancellationToken.None);
        Services.AddSingleton<IKeyValueStorage>(storage);
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: false,
                DisplayName: null,
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-24T10:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: new TaskCollectionSnapshot(
                    DateTimeOffset.Parse("2026-04-24T10:00:00Z"),
                    new[]
                    {
                        new TaskSnapshot("todo-1", "Alpha", TaskType.Todo, false, 1m, null, null, 1m),
                        new TaskSnapshot("todo-2", "Beta", TaskType.Todo, false, 1m, null, null, 2m),
                        new TaskSnapshot("todo-3", "Gamma", TaskType.Todo, false, 1m, null, null, 3m)
                    }))));

        var cut = Render<TasksPage>();

        AssertMarkupOrder(cut.Markup, "Beta", "Alpha", "Gamma");
    }

    private static void AssertMarkupOrder(string markup, params string[] labels)
    {
        var previousIndex = -1;

        foreach (var label in labels)
        {
            var index = markup.IndexOf(label, StringComparison.Ordinal);
            Assert.True(index > previousIndex, $"{label} should render after the previous label.");
            previousIndex = index;
        }
    }

    private sealed class FakeKeyValueStorage : IKeyValueStorage
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public Task<TValue?> GetAsync<TValue>(string key, CancellationToken cancellationToken)
        {
            return Task.FromResult(_values.TryGetValue(key, out var value) ? JsonSerializer.Deserialize<TValue>(value, JsonOptions) : default);
        }

        public Task SetAsync<TValue>(string key, TValue value, CancellationToken cancellationToken)
        {
            _values[key] = JsonSerializer.Serialize(value, JsonOptions);
            return Task.CompletedTask;
        }

        public Task<string?> GetRawJsonAsync(string key, CancellationToken cancellationToken)
        {
            return Task.FromResult(_values.GetValueOrDefault(key));
        }

        public Task SetRawJsonAsync(string key, string jsonText, CancellationToken cancellationToken)
        {
            _values[key] = jsonText;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }
    }
}
