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
    public void Opening_diagnostics_requests_local_messages_once_and_exposes_unavailable_refresh()
    {
        JSInterop.Mode = JSRuntimeMode.Loose; Services.AddMudServices();
        var session = new FakeAppSessionController(SessionViewModel.Empty) { DiagnosticsRefreshSucceeded = false };
        Services.AddSingleton<IAppSessionController>(session);
        var cut = Render<LiveTestsPage>();
        Assert.Equal(1, session.DiagnosticsRefreshCalls);
        Assert.Contains("Recent messages could not be refreshed", cut.Markup);
        Qualification.PageLayoutEvidence.Save("diagnostics-read-unavailable", cut.Markup, "/diagnostics", "local-log-read-unavailable", ["logs", "error", "disabled-controls"], nameof(Opening_diagnostics_requests_local_messages_once_and_exposes_unavailable_refresh));
    }

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
        Assert.NotNull(cut.Find(".warning-card"));
        Assert.Contains("Quick account reads", cut.Markup);
        Assert.Contains("App messages", cut.Markup);
        Assert.Contains("Copy all messages", cut.Markup);
        Assert.Contains("Download messages", cut.Markup);
        Assert.Contains("Clear messages", cut.Markup);
        Assert.Contains("Check account", cut.Markup);
        Assert.Contains("Run safe live tests", cut.Markup);
        Assert.Contains("Run reversible gear test", cut.Markup);
        Assert.Contains("safe live tests: Success.", cut.Markup);
    }

    [Fact]
    public void Diagnostics_console_exports_filtered_entries_as_jsonl()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var module = JSInterop.SetupModule("./js/diagnosticsExport.js");
        module.SetupVoid("copyText", _ => true).SetVoidResult();
        module.SetupVoid("downloadTextFile", _ => true).SetVoidResult();
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
                        "private-task-canary credential-canary",
                        new Dictionary<string, string>
                        {
                            ["itemKey"] = "credential-canary",
                            ["requestCount"] = "1"
                        })
                })));

        var cut = Render<LiveTestsPage>();

        cut.Find("[data-testid='copy-diagnostics-jsonl']").Click();
        cut.Find("[data-testid='download-diagnostics-jsonl']").Click();

        var invocations = JSInterop.Invocations.Select(invocation => invocation.Identifier).ToArray();
        Assert.Contains("import", invocations);
        Assert.Contains("inventory-equip-item", cut.Markup);
        Assert.DoesNotContain("itemKey", cut.Markup);
        Assert.DoesNotContain("credential-canary", cut.Markup);
        Assert.DoesNotContain("private-task-canary", cut.Markup);
        var exported = module.Invocations.Where(call => call.Identifier is "copyText" or "downloadTextFile")
            .Select(call => Assert.IsType<string>(call.Arguments.Last())).ToArray();
        Assert.Equal(2, exported.Length);
        foreach (var json in exported)
        {
            Assert.Contains("requestCount", json);
            Assert.DoesNotContain("credential-canary", json); Assert.DoesNotContain("private-task-canary", json);
        }
    }

    [Fact]
    public void Diagnostics_console_keeps_long_metadata_in_dedicated_wrapping_block()
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
                        DiagnosticsFeatureArea.Sync,
                        "cloud-sync-upload",
                        DiagnosticsSeverity.Warning,
                        DiagnosticsMode.Local,
                        "Cloud sync upload status: 8 uploaded, 0 failed, 1 skipped.",
                        new Dictionary<string, string>
                        {
                            ["sectionStatuses"] =
                                "user-profile:Success,tasks-current:Success,inventory-current:Success," +
                                "party-current:Success,diagnostics:Skipped"
                        })
                })));

        var cut = Render<LiveTestsPage>();

        var metadata = cut.Find(".diagnostics-log-item .diagnostics-metadata-inline");
        Assert.Contains("sectionStatuses", metadata.TextContent);
        Assert.Contains("diagnostics:Skipped", metadata.TextContent);
        Qualification.PageLayoutEvidence.Save("diagnostics-multiline", cut.Markup, "/diagnostics", "multiline-logs", ["logs", "filters", "disabled-controls"], nameof(Diagnostics_console_keeps_long_metadata_in_dedicated_wrapping_block));
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
