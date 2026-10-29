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
    public LiveTestsPageTests()
    {
        Services.AddScoped(sp=>new Habitica.WebApp.State.DiagnosticSharingController(sp.GetRequiredService<IAppSessionController>(),new DiagnosticSharingFake{DiagnosticSharingAvailable=false},TimeProvider.System));
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { DisposeAsync().AsTask().GetAwaiter().GetResult(); return; }
        base.Dispose(disposing);
    }

    [Fact]
    public async Task Agent_bundle_requires_preview_exports_exact_reviewed_copy_and_clears_after_filter_change()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var module = JSInterop.SetupModule("./js/diagnosticsExport.js");
        module.SetupVoid("copyText", _ => true).SetVoidResult();
        module.SetupVoid("downloadTextFile", _ => true).SetVoidResult();
        Services.AddMudServices();
        var entry = new DiagnosticsLogEntry("secret-canary", DateTimeOffset.Parse("2026-10-19T12:00:00Z"), DiagnosticsFeatureArea.Workspace,
            "workspace-task-accept", DiagnosticsSeverity.Warning, DiagnosticsMode.Local, "private-name secret-canary", new Dictionary<string, string>());
        var session = new FakeAppSessionController(SessionViewModel.Empty with { DiagnosticsLogEntries = [entry] });
        Services.AddSingleton<IAppSessionController>(session);
        var cut = Render<LiveTestsPage>();
        Assert.Empty(cut.FindAll("[data-testid='copy-diagnostics-bundle']"));
        Assert.Empty(module.Invocations);
        cut.Find("[data-testid='preview-diagnostics-bundle']").Click();
        var reviewed = cut.Find("[data-testid='diagnostics-bundle-preview']").TextContent;
        Assert.Contains("browser-local-store", reviewed); Assert.DoesNotContain("secret-canary", reviewed);
        Qualification.PageLayoutEvidence.Save("diagnostics-agent-bundle", cut.Markup.Replace("<details class=\"diagnostics-bundle\"", "<details open class=\"diagnostics-bundle\""), "/diagnostics", "reviewed-agent-bundle", ["logs", "filters"], nameof(Agent_bundle_requires_preview_exports_exact_reviewed_copy_and_clears_after_filter_change));
        cut.Find("[data-testid='copy-diagnostics-bundle']").Click();
        cut.Find("[data-testid='download-diagnostics-bundle']").Click();
        Assert.Equal(reviewed, Assert.Single(module.Invocations, call => call.Identifier == "copyText").Arguments[0]);
        var download = Assert.Single(module.Invocations, call => call.Identifier == "downloadTextFile");
        Assert.Equal(reviewed, download.Arguments[1]); Assert.Equal("application/json;charset=utf-8", download.Arguments[2]);
        Assert.Equal(0, System.Text.Json.JsonDocument.Parse(reviewed).RootElement.GetProperty("omittedMatchingCount").GetInt32());
        cut.FindAll(".diagnostics-filter-row select")[0].Change("Workspace");
        Assert.Empty(cut.FindAll("[data-testid='diagnostics-bundle-preview']"));
        Assert.Empty(cut.FindAll("[data-testid='copy-diagnostics-bundle']"));
        cut.Find("[data-testid='preview-diagnostics-bundle']").Click();
        Assert.Single(cut.FindAll("[data-testid='diagnostics-bundle-preview']"));
        await cut.InvokeAsync(() => session.SetState(SessionViewModel.Empty));
        Assert.Empty(cut.FindAll("[data-testid='diagnostics-bundle-preview']"));
    }

    [Fact]
    public void Compact_rows_copy_exact_redacted_entry_and_filtered_batch_with_recoverable_failures()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var module = JSInterop.SetupModule("./js/diagnosticsExport.js");
        var copy = module.SetupVoid("copyText", _ => true);
        copy.SetVoidResult();
        Services.AddMudServices();
        var entries = Enumerable.Range(0, 12).Select(i => new DiagnosticsLogEntry(
            "entry-" + i, DateTimeOffset.Parse("2026-10-19T12:00:00Z").AddSeconds(-i),
            i % 2 == 0 ? DiagnosticsFeatureArea.Sync : DiagnosticsFeatureArea.Inventory,
            i % 2 == 0 ? "cloud-sync-upload" : "inventory-equip-item",
            i % 2 == 0 ? DiagnosticsSeverity.Warning : DiagnosticsSeverity.Success,
            DiagnosticsMode.Local, "private-name\nprivate-credential", new Dictionary<string, string>
            {
                ["sectionStatuses"] = "user-profile:Success,tasks-current:Success,inventory-current:Success,party-current:Success,diagnostics:Skipped",
                ["requestCount"] = "1"
            })).ToArray();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(SessionViewModel.Empty with { DiagnosticsLogEntries = entries }));
        var cut = Render<LiveTestsPage>();
        Assert.Equal(12, cut.FindAll(".diagnostics-log-row").Count);
        Assert.All(cut.FindAll(".diagnostics-log-item"), button => Assert.Equal("button", button.GetAttribute("type")));
        Assert.All(cut.FindAll("details"), details => Assert.False(details.HasAttribute("open")));
        Qualification.PageLayoutEvidence.Save("diagnostics-compact-many", cut.Markup, "/diagnostics", "twelve-compact-rows", ["logs", "filters"], nameof(Compact_rows_copy_exact_redacted_entry_and_filtered_batch_with_recoverable_failures));
        Qualification.PageLayoutEvidence.Save("diagnostics-compact-details", cut.Markup.Replace("<details class=\"diagnostics-log-details\"", "<details open class=\"diagnostics-log-details\""), "/diagnostics", "twelve-expanded-redacted-details", ["logs", "filters"], nameof(Compact_rows_copy_exact_redacted_entry_and_filtered_batch_with_recoverable_failures));
        var expected = cut.Find(".diagnostics-log-details pre").TextContent;
        cut.Find(".diagnostics-log-item").Click();
        Assert.Equal(expected, Assert.Single(module.Invocations, x => x.Identifier == "copyText").Arguments[0]);
        Assert.Equal("true", cut.Find(".diagnostics-log-item").GetAttribute("data-selected"));
        Assert.Contains("Message copied.", cut.Find(".diagnostics-copy-status").TextContent);
        cut.FindAll(".diagnostics-filter-row select")[0].Change("Sync");
        Assert.Equal(6, cut.FindAll(".diagnostics-log-row").Count);
        cut.Find("[data-testid='copy-diagnostics-jsonl']").Click();
        var batch = Assert.IsType<string>(module.Invocations.Last(x => x.Identifier == "copyText").Arguments[0]);
        Assert.Equal(6, batch.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.DoesNotContain("inventory-equip-item", batch);
        Assert.DoesNotContain("private-", batch);
        Assert.Contains("Filtered messages copied.", cut.Find(".diagnostics-copy-status").TextContent);
        copy.SetException(new Microsoft.JSInterop.JSException("private-credential clipboard denied"));
        cut.Find(".diagnostics-log-item").Click();
        Assert.Contains("Copy failed.", cut.Find(".diagnostics-copy-status").TextContent);
        Assert.DoesNotContain("private-", cut.Markup);
        copy.SetVoidResult();
        cut.Find(".diagnostics-log-item").Click();
        Assert.Contains("Message copied.", cut.Find(".diagnostics-copy-status").TextContent);
    }

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

        var metadata = cut.Find(".diagnostics-log-details .diagnostics-metadata-inline");
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
