using System.Globalization;
using Bunit;
using Habitica.Application.Dashboard;
using Habitica.Application.Sync;
using Habitica.Domain.Sync;
using Habitica.Rules.Stats;
using Habitica.Storage;
using Habitica.WebApp;
using Habitica.WebApp.Components.Navigation;
using Habitica.WebApp.Layout;
using Habitica.WebApp.State;
using Habitica.WebApp.Theme;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests;

public sealed class AppNavMenuTests : BunitContext
{
    public AppNavMenuTests() => ComponentFactories.AddStub<Habitica.WebApp.Components.ForegroundReminderHost>();
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
            return;
        }

        base.Dispose(disposing);
    }

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
        Assert.Contains("Quests", cut.Markup);
        Assert.Contains("Pets &amp; Mounts", cut.Markup);
        Assert.Contains("Spells", cut.Markup);
        Assert.DoesNotContain("Live Tests", cut.Markup);
        Assert.DoesNotContain("Checks", cut.Markup);
        Assert.Contains("/diagnostics", cut.Markup);
        Assert.Contains("/quests", cut.Markup);
        Assert.Contains("/spells", cut.Markup);
        Assert.Contains("/pets-mounts", cut.Markup);

        var todoSection = cut.Find("[data-testid='todo-nav-section']");
        Assert.NotNull(todoSection.QuerySelector(".todo-nav-heading"));
        Assert.Null(todoSection.QuerySelector("details"));
        foreach (var route in new[] { "/todo/today", "/todo/habits", "/todo/dailies", "/todo/tasks", "/todo/projects", "/todo/statistics", "/todo/sync", "/todo/inbox", "/todo/plan", "/todo/lists", "/todo/history", "/tasks" })
            Assert.Single(todoSection.QuerySelectorAll($"a[href='{route}']"));
        Qualification.PageLayoutEvidence.Save("todo-nav-main", "<aside class=\"app-drawer\" style=\"min-height:100vh;max-width:300px\">" + cut.Markup + "</aside>", "/todo/today", "always-open-menu", ["navigation", "selected-state"], nameof(Renders_diagnostics_link_instead_of_live_tests_for_authenticated_sessions));

        AssertNavOrder(cut.Markup, "Dashboard", "Tasks", "Equipment", "Pets &amp; Mounts", "Party", "Quests", "Spells", "Settings", "Diagnostics");
        Assert.Contains("href=\"/inventory\"", cut.Markup);
    }

    [Fact]
    public void Todo_category_tabs_mark_only_the_selected_destination()
    {
        Services.AddMudServices();
        var cut = Render<Habitica.WebApp.Components.TodoCategoryTabs>(parameters => parameters.Add(x => x.Active, "dailies"));
        Assert.Equal(4, cut.FindAll("a").Count);
        Assert.Equal("page", cut.Find("a[href='/todo/dailies']").GetAttribute("aria-current"));
        Assert.Null(cut.Find("a[href='/todo/tasks']").GetAttribute("aria-current"));
    }

    [Fact]
    public void Todo_navigation_highlights_the_exact_selected_route()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(SessionViewModel.Empty with { IsAuthenticated = true }));
        Services.GetRequiredService<NavigationManager>().NavigateTo("/todo/habits");
        var cut = Render<AppNavMenu>();
        Assert.Contains("active", cut.Find("a[href='/todo/habits']").ClassList);
        Assert.DoesNotContain("active", cut.Find("a[href='/todo/dailies']").ClassList);
        Assert.DoesNotContain("active", cut.Find("a[href='/tasks']").ClassList);
        Qualification.PageLayoutEvidence.Save("todo-nav-selected", "<aside class=\"app-drawer\" style=\"min-height:100vh;max-width:300px\">" + cut.Markup + "</aside>", "/todo/habits", "selected-Habits-route", ["navigation", "selected-state"], nameof(Todo_navigation_highlights_the_exact_selected_route));
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
        Assert.DoesNotContain("Diagnostics", cut.Markup);
    }

    [Fact]
    public void Root_route_sends_authenticated_sessions_to_dashboard()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new CharacterStatsViewModelFactory());
        Services.AddSingleton(new PendingDamageEstimateFactory());
        Services.AddSingleton(new HealthPotionRecoveryEstimateFactory());
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: null,
                TaskFreshness: SnapshotFreshnessState.Missing,
                TaskSnapshot: null)));

        Services.AddSingleton<IKeyValueStorage>(new InMemoryKeyValueStorage());
        Services.AddScoped<ColorSchemeService>();
        var navigation = Services.GetRequiredService<NavigationManager>();
        var cut = Render<App>();

        cut.WaitForAssertion(() => Assert.EndsWith("/dashboard", navigation.Uri, StringComparison.Ordinal));
        Assert.DoesNotContain("Sign in with Habitica", cut.Markup);
    }

    [Fact]
    public void Root_route_sends_unauthenticated_sessions_to_sign_in()
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

        Services.AddSingleton<IKeyValueStorage>(new InMemoryKeyValueStorage());
        Services.AddScoped<ColorSchemeService>();
        var navigation = Services.GetRequiredService<NavigationManager>();
        var cut = Render<App>();

        cut.WaitForAssertion(() => Assert.EndsWith("/sign-in", navigation.Uri, StringComparison.Ordinal));
        Assert.Contains("Sign in with Habitica", cut.Markup);
    }

    [Fact]
    public void Saved_credentials_are_initialized_before_root_route_renders_sign_in()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new CharacterStatsViewModelFactory());
        Services.AddSingleton(new PendingDamageEstimateFactory());
        Services.AddSingleton(new HealthPotionRecoveryEstimateFactory());
        var controller = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: false,
                DisplayName: null,
                ErrorMessage: null,
                LastSyncedAtUtc: null,
                TaskFreshness: SnapshotFreshnessState.Missing,
                TaskSnapshot: null))
        {
            StateAfterInitialize = new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: null,
                TaskFreshness: SnapshotFreshnessState.Missing,
                TaskSnapshot: null)
        };
        Services.AddSingleton<IAppSessionController>(controller);

        Services.AddSingleton<IKeyValueStorage>(new InMemoryKeyValueStorage());
        Services.AddScoped<ColorSchemeService>();
        var navigation = Services.GetRequiredService<NavigationManager>();
        var cut = Render<App>();

        cut.WaitForAssertion(() => Assert.EndsWith("/dashboard", navigation.Uri, StringComparison.Ordinal));
        Assert.Equal(1, controller.InitializeCalls);
        Assert.DoesNotContain("Sign in with Habitica", cut.Markup);
    }

    [Fact]
    public void Main_layout_renders_compact_synced_time_without_date()
    {
        var syncedAt = DateTimeOffset.Parse("2026-04-24T12:42:00Z", CultureInfo.InvariantCulture);
        var cut = RenderLayout(CreateAuthenticatedState(lastSyncedAtUtc: syncedAt));

        var syncText = cut.Find(".topbar-sync-slot").TextContent.Trim();
        var expectedTime = syncedAt.ToLocalTime().ToString("h:mm tt", CultureInfo.CurrentCulture);

        Assert.Equal($"Synced {expectedTime}", syncText);
        Assert.DoesNotContain("2026", syncText);
        Assert.Contains("id=\"app-refresh\"", cut.Markup);
    }

    [Fact]
    public void Main_layout_renders_stale_sync_status_without_hiding_refresh()
    {
        var cut = RenderLayout(CreateAuthenticatedState(taskFreshness: SnapshotFreshnessState.Stale));

        Assert.Equal("Sync stale", cut.Find(".topbar-sync-slot").TextContent.Trim());
        Assert.Contains("sync-chip--warning", cut.Markup);
        Assert.Contains("id=\"app-refresh\"", cut.Markup);
    }

    [Fact]
    public void Main_layout_renders_failed_sync_status_for_domain_errors()
    {
        var cut = RenderLayout(CreateAuthenticatedState(
            domainStates: new Dictionary<RefreshDomain, DomainRefreshState>
            {
                [RefreshDomain.Tasks] = new(RefreshDomain.Tasks, false, LastError: "Tasks failed")
            }));

        Assert.Equal("Sync failed", cut.Find(".topbar-sync-slot").TextContent.Trim());
        Assert.Contains("sync-chip--danger", cut.Markup);
        Assert.Contains("Failed: tasks", cut.Markup);
    }

    [Fact]
    public void Main_layout_replaces_refresh_button_with_active_sync_status()
    {
        var cut = RenderLayout(CreateAuthenticatedState(
            domainStates: new Dictionary<RefreshDomain, DomainRefreshState>
            {
                [RefreshDomain.Tasks] = new(RefreshDomain.Tasks, true)
            }));

        Assert.Empty(cut.FindAll("#app-refresh"));
        Assert.Equal("Syncing tasks...", cut.Find("#app-refresh-status").TextContent.Trim());
        Assert.Contains("sync-chip--busy", cut.Markup);
        Assert.DoesNotContain("Refresh</", cut.Markup);
    }

    private static void AssertNavOrder(string markup, params string[] labels)
    {
        var previousIndex = -1;

        foreach (var label in labels)
        {
            var index = markup.IndexOf(label, StringComparison.Ordinal);
            Assert.True(index > previousIndex, $"{label} should render after the previous navigation item.");
            previousIndex = index;
        }
    }

    private IRenderedComponent<MainLayout> RenderLayout(SessionViewModel state)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(state));

        return Render<MainLayout>(parameters => parameters.Add(
            layout => layout.Body,
            builder => builder.AddContent(0, "Layout body")));
    }

    private static SessionViewModel CreateAuthenticatedState(
        DateTimeOffset? lastSyncedAtUtc = null,
        SnapshotFreshnessState taskFreshness = SnapshotFreshnessState.Fresh,
        SnapshotFreshnessState userFreshness = SnapshotFreshnessState.Fresh,
        SnapshotFreshnessState partyFreshness = SnapshotFreshnessState.Fresh,
        IReadOnlyDictionary<RefreshDomain, DomainRefreshState>? domainStates = null)
    {
        return new SessionViewModel(
            IsBusy: false,
            IsAuthenticated: true,
            DisplayName: "Mage Tester",
            ErrorMessage: null,
            LastSyncedAtUtc: lastSyncedAtUtc ?? DateTimeOffset.Parse("2026-04-24T12:42:00Z", CultureInfo.InvariantCulture),
            TaskFreshness: taskFreshness,
            TaskSnapshot: null,
            UserFreshness: userFreshness,
            PartyFreshness: partyFreshness,
            DomainStates: domainStates);
    }
}
