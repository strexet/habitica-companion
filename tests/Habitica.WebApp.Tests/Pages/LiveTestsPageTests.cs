using Bunit;
using Habitica.Application.Diagnostics;
using Habitica.Domain.Diagnostics;
using Habitica.Domain.Sync;
using Habitica.WebApp.Pages;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Pages;

public sealed class LiveTestsPageTests : BunitContext
{
    [Fact]
    public void Renders_diagnostics_sections_preset_runner_and_console()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: null,
                DiagnosticsLogEntries: new[]
                {
                    new DiagnosticsLogEntry(
                        "entry-1",
                        DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
                        DiagnosticsFeatureArea.Diagnostics,
                        "safe-live-tests",
                        DiagnosticsSeverity.Success,
                        DiagnosticsMode.LiveRead,
                        "Completed safe diagnostics suite.",
                        new Dictionary<string, string>())
                })));

        var cut = Render<LiveTestsPage>();

        Assert.Contains("Diagnostics", cut.Markup);
        Assert.Contains("Safe checks", cut.Markup);
        Assert.Contains("Guarded tests", cut.Markup);
        Assert.Contains("Quick account reads", cut.Markup);
        Assert.Contains("App messages", cut.Markup);
        Assert.Contains("Copy all messages", cut.Markup);
        Assert.Contains("Download messages", cut.Markup);
        Assert.Contains("Clear messages", cut.Markup);
        Assert.Contains("Check account", cut.Markup);
        Assert.Contains("Run safe live tests", cut.Markup);
        Assert.Contains("Run reversible gear test", cut.Markup);
        Assert.Contains("Completed safe diagnostics suite.", cut.Markup);
    }

    [Fact]
    public void Diagnostics_console_exports_filtered_entries_as_jsonl()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: null,
                DiagnosticsLogEntries: new[]
                {
                    new DiagnosticsLogEntry(
                        "entry-1",
                        DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
                        DiagnosticsFeatureArea.Inventory,
                        "inventory-equip-item",
                        DiagnosticsSeverity.Success,
                        DiagnosticsMode.LiveMutation,
                        "Equipped Wizard Wand.",
                        new Dictionary<string, string>
                        {
                            ["itemKey"] = "weapon_wizard_5"
                        })
                })));

        var cut = Render<LiveTestsPage>();

        cut.Find("[data-testid='copy-diagnostics-jsonl']").Click();
        cut.Find("[data-testid='download-diagnostics-jsonl']").Click();

        var invocations = JSInterop.Invocations.Select(invocation => invocation.Identifier).ToArray();
        Assert.Contains("import", invocations);
        Assert.Contains("inventory-equip-item", cut.Markup);
        Assert.Contains("itemKey", cut.Markup);
    }

    [Fact]
    public void Diagnostics_preset_button_renders_the_returned_preview()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        var controller = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-27T12:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: null))
        {
            DiagnosticsPresetResult = new DiagnosticsPresetRunResult(
                DiagnosticsPreset.UserAccount,
                true,
                1,
                "Mage Tester level 15 account snapshot loaded.",
                "{\n  \"displayName\": \"Mage Tester\"\n}")
        };
        Services.AddSingleton<IAppSessionController>(controller);

        var cut = Render<LiveTestsPage>();

        cut.Find("[data-testid='preset-user-account']").Click();

        Assert.Equal(1, controller.DiagnosticsPresetCalls);
        Assert.Contains("Mage Tester level 15 account snapshot loaded.", cut.Markup);
        Assert.Contains("\"displayName\": \"Mage Tester\"", cut.Markup);
    }
}
