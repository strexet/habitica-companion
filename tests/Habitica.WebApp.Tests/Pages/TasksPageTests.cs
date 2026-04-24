using Bunit;
using Habitica.Application.Tasks;
using Habitica.Domain.Sync;
using Habitica.Domain.Tasks;
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
                        new TaskSnapshot("todo-1", "Buy milk", TaskType.Todo, false, 1.5m, "2 liters", null)
                    }))));

        var cut = Render<TasksPage>();

        Assert.Contains("Stale local snapshot", cut.Markup);
        Assert.Contains("Buy milk", cut.Markup);
        Assert.Contains("To-Dos", cut.Markup);
    }
}
