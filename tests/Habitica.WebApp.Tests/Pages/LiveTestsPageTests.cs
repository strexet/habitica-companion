using Bunit;
using Habitica.Application.Diagnostics;
using Habitica.Domain.Sync;
using Habitica.WebApp.Pages;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Pages;

public sealed class LiveTestsPageTests : BunitContext
{
    [Fact]
    public void Renders_safe_and_reversible_test_actions()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-26T10:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: null))
        {
            SafeLiveTestResult = new LiveTestSuiteResult(
                DateTimeOffset.Parse("2026-04-26T10:00:00Z"),
                DateTimeOffset.Parse("2026-04-26T10:00:10Z"),
                new[]
                {
                    new LiveTestResult("auth", "Account snapshot", LiveTestStatus.Passed, LiveTestRisk.Safe, 1, "Account snapshot refreshed.")
                })
        });

        var cut = Render<LiveTestsPage>();

        Assert.Contains("Live test lab", cut.Markup);
        Assert.Contains("Run safe live tests", cut.Markup);
        Assert.Contains("Run reversible gear test", cut.Markup);
        Assert.Contains("temporarily changes equipped battle gear", cut.Markup);
    }

    [Fact]
    public void Safe_live_test_run_renders_returned_results()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        var controller = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-26T10:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: null))
        {
            SafeLiveTestResult = new LiveTestSuiteResult(
                DateTimeOffset.Parse("2026-04-26T10:00:00Z"),
                DateTimeOffset.Parse("2026-04-26T10:00:10Z"),
                new[]
                {
                    new LiveTestResult("safe-live-tests", "Safe live tests", LiveTestStatus.Failed, LiveTestRisk.Safe, 0, "Sign in is required before running live tests.")
                })
        };
        Services.AddSingleton<IAppSessionController>(controller);

        var cut = Render<LiveTestsPage>();

        cut.Find("button").Click();

        Assert.Equal(1, controller.SafeLiveTestCalls);
        Assert.Contains("Sign in is required before running live tests.", cut.Markup);
        Assert.Contains("Failed", cut.Markup);
    }
}
