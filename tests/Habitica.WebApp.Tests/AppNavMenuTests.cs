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
    public void Renders_authenticated_navigation_links_when_session_is_active()
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

        Assert.Contains("Tasks", cut.Markup);
        Assert.Contains("Settings", cut.Markup);
        Assert.DoesNotContain("Sign In", cut.Markup);
    }
}
