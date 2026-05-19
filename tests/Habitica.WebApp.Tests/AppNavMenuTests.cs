using Bunit;
using Habitica.Domain.Sync;
using Habitica.WebApp.Components.Navigation;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests;

public sealed class AppNavMenuTests : BunitContext
{
    [Fact]
    public void Renders_diagnostics_link_instead_of_live_tests_for_authenticated_sessions()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-24T12:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: null)));

        var cut = Render<AppNavMenu>();

        Assert.Contains("Checks", cut.Markup);
        Assert.Contains("Spells", cut.Markup);
        Assert.DoesNotContain("Live Tests", cut.Markup);
        Assert.Contains("/diagnostics", cut.Markup);
        Assert.Contains("/spells", cut.Markup);
    }

    [Fact]
    public void Does_not_render_feature_links_for_unauthenticated_sessions()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: false,
                DisplayName: null,
                ErrorMessage: null,
                LastSyncedAtUtc: null,
                TaskFreshness: SnapshotFreshnessState.Missing,
                TaskSnapshot: null)));

        var cut = Render<AppNavMenu>();

        Assert.DoesNotContain("Sign In", cut.Markup);
        Assert.DoesNotContain("Dashboard", cut.Markup);
        Assert.DoesNotContain("Checks", cut.Markup);
    }
}
