using Bunit;
using Habitica.Domain.Diagnostics;
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

        Assert.Contains("Diagnostics", cut.Markup);
        Assert.DoesNotContain("Live Tests", cut.Markup);
        Assert.Contains("/diagnostics", cut.Markup);
    }

    [Fact]
    public void Renders_diagnostics_link_when_only_diagnostics_history_is_cached()
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
                TaskSnapshot: null,
                DiagnosticsLogEntries: new[]
                {
                    new DiagnosticsLogEntry(
                        "entry-1",
                        DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
                        DiagnosticsFeatureArea.Diagnostics,
                        "safe-live-tests",
                        DiagnosticsSeverity.Warning,
                        DiagnosticsMode.LiveRead,
                        "warning",
                        new Dictionary<string, string>())
                })));

        var cut = Render<AppNavMenu>();

        Assert.Contains("Diagnostics", cut.Markup);
        Assert.Contains("/diagnostics", cut.Markup);
    }
}
