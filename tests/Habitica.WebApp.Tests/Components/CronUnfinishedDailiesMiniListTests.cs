using Bunit;
using Habitica.Domain.Sync;
using Habitica.Domain.Tasks;
using Habitica.WebApp.Components;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;

namespace Habitica.WebApp.Tests.Components;

public sealed class CronUnfinishedDailiesMiniListTests : BunitContext
{
    [Fact]
    public void Check_button_invokes_completion_callback()
    {
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(CreateState(SnapshotFreshnessState.Fresh)));
        string? completedTaskId = null;

        var cut = Render<CronUnfinishedDailiesMiniList>(parameters => parameters
            .Add(component => component.Dailies, CreateDailies())
            .Add(component => component.OnComplete, daily => completedTaskId = daily.Id));

        cut.Find("[data-testid='complete-cron-daily-daily-1']").Click();

        Assert.Equal("daily-1", completedTaskId);
    }

    [Fact]
    public void Collapsed_list_expands_and_stale_state_links_to_refresh()
    {
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(CreateState(SnapshotFreshnessState.Stale)));

        var cut = Render<CronUnfinishedDailiesMiniList>(parameters => parameters
            .Add(component => component.Dailies, CreateDailies())
            .Add(component => component.StartCollapsed, true));

        Assert.Contains("1 daily due", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid='complete-cron-daily-daily-1']"));

        cut.Find("[data-testid='cron-dailies-disclosure']").Click();

        Assert.Contains("Refresh tasks to check off.", cut.Markup);
        Assert.Equal("#app-refresh", cut.Find("a").GetAttribute("href"));
        Assert.Empty(cut.FindAll("[data-testid='complete-cron-daily-daily-1']"));
    }

    [Fact]
    public void Empty_list_renders_no_container()
    {
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(CreateState(SnapshotFreshnessState.Fresh)));

        var cut = Render<CronUnfinishedDailiesMiniList>(parameters => parameters
            .Add(component => component.Dailies, Array.Empty<TaskSnapshot>()));

        Assert.Empty(cut.FindAll("[data-testid='cron-unfinished-dailies']"));
    }

    private static IReadOnlyList<TaskSnapshot> CreateDailies()
    {
        return new[]
        {
            new TaskSnapshot("daily-1", "Exercise", TaskType.Daily, false, 1.5m, null, null, IsDue: true)
        };
    }

    private static SessionViewModel CreateState(SnapshotFreshnessState freshness)
    {
        return new SessionViewModel(
            IsBusy: false,
            IsAuthenticated: true,
            DisplayName: "Mage Tester",
            ErrorMessage: null,
            LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
            TaskFreshness: freshness,
            TaskSnapshot: new TaskCollectionSnapshot(DateTimeOffset.Parse("2026-04-25T08:00:00Z"), CreateDailies()));
    }
}
