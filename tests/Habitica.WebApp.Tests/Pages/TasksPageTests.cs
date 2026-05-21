using System.Text.Json;
using Bunit;
using Habitica.Application.Tasks;
using Habitica.Domain.Sync;
using Habitica.Domain.Tasks;
using Habitica.Domain.User;
using Habitica.Storage;
using Habitica.WebApp.Pages;
using Habitica.WebApp.State;
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
        Services.AddSingleton<IKeyValueStorage>(new FakeKeyValueStorage());
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: false,
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
        Assert.Contains("Value", cut.Markup);
        Assert.Contains("-4.2", cut.Markup);
        Assert.Contains("To-Dos", cut.Markup);
        Assert.DoesNotContain("Archive notes", cut.Markup);

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
