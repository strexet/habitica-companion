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
using MudBlazor;

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

    [Theory]
    [InlineData("/todo/today","/todo/today",false,false)]
    [InlineData("/todo/inbox","/todo/inbox",false,false)]
    [InlineData("/todo/plan","/todo/plan",false,false)]
    [InlineData("/todo/projects","/todo/projects",false,false)]
    [InlineData("/todo/tasks/00000000-0000-4000-8000-000000000099","/todo/tasks",false,false)]
    [InlineData("/todo/history","/todo/history",true,false)]
    [InlineData("/inventory","/inventory",false,true)]
    [InlineData("/settings","/settings",false,false)]
    public void Compact_navigation_keeps_primary_routes_first_exact_active_context_and_secondary_groups(string route,string selected,bool planningOpen,bool gameOpen)
    {
        JSInterop.Mode=JSRuntimeMode.Loose;Services.AddMudServices();Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(CreateAuthenticatedState()));
        Services.GetRequiredService<NavigationManager>().NavigateTo(route);var cut=Render<AppNavMenu>();
        Assert.Equal(new[]{"/todo/today","/todo/inbox","/todo/plan","/todo/projects"},cut.FindAll("[data-testid='nav-primary-planning'] a").Select(link=>link.GetAttribute("href")));
        Assert.Equal("/todo/tasks",cut.Find("a.nav-capture").GetAttribute("href"));Assert.Equal("Add task",cut.Find("a.nav-capture").TextContent.Trim());
        var current=Assert.Single(cut.FindAll("a[aria-current='page']"));Assert.Equal(selected,current.GetAttribute("href"));
        var planning=cut.Find("details[data-testid='nav-more-planning']");var game=cut.Find("details[data-testid='nav-game-tools']");Assert.Equal(planningOpen,planning.HasAttribute("open"));Assert.Equal(gameOpen,game.HasAttribute("open"));
        Assert.Equal("More planning",planning.QuerySelector("summary")!.TextContent.Trim());Assert.Equal("Game tools",game.QuerySelector("summary")!.TextContent.Trim());
        foreach(var href in new[]{"/dashboard","/todo/today","/todo/inbox","/todo/plan","/todo/projects","/todo/habits","/todo/dailies","/todo/tasks","/todo/statistics","/todo/sync","/todo/lists","/todo/history","/tasks","/notes","/inventory","/pets-mounts","/party","/quests","/spells","/settings","/diagnostics"})
            Assert.Contains(cut.FindAll("a"),link=>link.GetAttribute("href")==href);
    }
    [Fact]
    public void Drawer_identity_is_compact_and_route_changes_reveal_the_active_secondary_group()
    {
        var cut=RenderLayout(CreateAuthenticatedState());Assert.Empty(cut.FindAll(".drawer-hero"));Assert.Contains("Mage Tester",cut.Find(".drawer-identity").TextContent);Assert.Empty(cut.FindAll(".drawer-identity h2"));
        var navigation=Services.GetRequiredService<NavigationManager>();navigation.NavigateTo("/inventory");cut.WaitForAssertion(()=>Assert.True(cut.Find("details[data-testid='nav-game-tools']").HasAttribute("open")));
        navigation.NavigateTo("/todo/history");cut.WaitForAssertion(()=>Assert.True(cut.Find("details[data-testid='nav-more-planning']").HasAttribute("open")));Assert.False(cut.Find("details[data-testid='nav-game-tools']").HasAttribute("open"));
    }

    [Fact]
    public void Main_navigation_identifies_the_controlled_drawer_and_tracks_toggle_state()
    {
        var cut = RenderLayout(CreateAuthenticatedState());
        var menu = cut.Find("button[aria-label='Main navigation']");
        Assert.Equal("primary-navigation", menu.GetAttribute("aria-controls"));
        Assert.Single(cut.FindAll("#primary-navigation"));
        var before = menu.GetAttribute("aria-expanded");
        menu.Click();
        Assert.NotEqual(before, cut.Find("button[aria-label='Main navigation']").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Main_layout_provides_a_skip_link_to_the_focusable_main_landmark()
    {
        var cut = RenderLayout(CreateAuthenticatedState());
        Assert.Equal("#main-content", cut.Find("a.skip-to-content").GetAttribute("href"));
        Assert.Equal("-1", cut.Find("main#main-content").GetAttribute("tabindex"));
        Assert.Single(cut.FindAll("main"));
    }

    [Fact]
    public void Escape_closes_open_navigation_without_changing_the_route()
    {
        var cut = RenderLayout(CreateAuthenticatedState());
        var navigation = Services.GetRequiredService<NavigationManager>();
        var before = navigation.Uri;
        var menu = cut.Find("button[aria-label='Main navigation']");
        if (menu.GetAttribute("aria-expanded") != "true") menu.Click();
        cut.Find("#primary-navigation").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });
        Assert.Equal("false", cut.Find("button[aria-label='Main navigation']").GetAttribute("aria-expanded"));
        Assert.Equal(before, navigation.Uri);
        Assert.True(cut.Find("#primary-navigation").HasAttribute("inert"));
    }

    [Theory]
    [InlineData("/dashboard", "dashboard")]
    [InlineData("/todo/today", "today")]
    [InlineData("/todo/tasks", "tasks")]
    [InlineData("/settings", "settings")]
    [InlineData("/todo/habits", "habits")]
    [InlineData("/todo/dailies", "dailies")]
    [InlineData("/todo/sync", "sync")]
    [InlineData("/todo/inbox", "inbox")]
    [InlineData("/todo/plan", "plan")]
    [InlineData("/todo/projects", "projects")]
    [InlineData("/inventory", "inventory")]
    public void Shell_states_keep_the_main_landmark_and_navigation_independent(string route, string name)
    {
        var cut = RenderLayout(CreateAuthenticatedState());
        Services.GetRequiredService<NavigationManager>().NavigateTo(route);
        Assert.Equal("primary-navigation", cut.Find("button[aria-label='Main navigation']").GetAttribute("aria-controls"));
        SaveShell("shell-" + name, cut.Markup, route, "authenticated-navigation");
        var menu = cut.Find("button[aria-label='Main navigation']");
        if (menu.GetAttribute("aria-expanded") != "false") menu.Click();
        Assert.True(cut.Find("#primary-navigation").HasAttribute("inert"));
        SaveShell("shell-" + name + "-closed", cut.Markup, route, "closed-navigation-inert");
    }

    [Fact]
    public void Signed_out_shell_exposes_signin_and_skip_without_private_navigation()
    {
        var cut = RenderLayout(SessionViewModel.Empty);
        Assert.Empty(cut.FindAll("#primary-navigation"));
        Assert.Single(cut.FindAll("#app-sign-in"));
        Assert.Equal("#main-content", cut.Find("a.skip-to-content").GetAttribute("href"));
        SaveShell("shell-signed-out", cut.Markup, "/dashboard", "signed-out-public-shell");
    }

    [Fact]
    public void Full_shell_evidence_uses_the_actual_App_theme_and_real_body_markers()
    {
        Assert.Contains("--mud-appbar-height:", CurrentAppThemeMarkup());
        var cut = RenderLayout(CreateAuthenticatedState());
        Assert.NotEmpty(cut.FindAll("#main-content [data-layout-content]"));
        Assert.Contains("Fixture primary content", cut.Find("#main-content").TextContent);
    }

    private static string CurrentAppThemeMarkup()
    {
        var context = new BunitContext();
        try
        {
            context.ComponentFactories.AddStub<Habitica.WebApp.Components.ForegroundReminderHost>();
            context.JSInterop.Mode = JSRuntimeMode.Loose;
            context.Services.AddMudServices();
            context.Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(SessionViewModel.Empty));
            context.Services.AddSingleton<IKeyValueStorage>(new InMemoryKeyValueStorage());
            context.Services.AddScoped<ColorSchemeService>();
            var app = context.Render<App>();
            var styles = string.Concat(app.FindAll("style").Select(element => element.OuterHtml));
            Assert.Contains("--mud-appbar-height:", styles);
            return styles;
        }
        finally { context.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }

    private static void SaveShell(string name, string markup, string route, string state) =>
        Qualification.PageLayoutEvidence.Save(name, CurrentAppThemeMarkup() + markup, route, state,
            ["navigation", "form", "actions", "visible-main-content"], nameof(Shell_states_keep_the_main_landmark_and_navigation_independent), fullLayout: true);

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
        Assert.Equal("More planning",todoSection.QuerySelector("details > summary")!.TextContent.Trim());
        foreach (var route in new[] { "/todo/today", "/todo/habits", "/todo/dailies", "/todo/tasks", "/todo/projects", "/todo/statistics", "/todo/sync", "/todo/inbox", "/todo/plan", "/todo/lists", "/todo/history", "/tasks" })
            Assert.Single(todoSection.QuerySelectorAll($"a[href='{route}']"));
        Qualification.PageLayoutEvidence.Save("todo-nav-main", "<aside class=\"app-drawer\" style=\"min-height:100vh;max-width:300px\">" + cut.Markup + "</aside>", "/todo/today", "always-open-menu", ["navigation", "selected-state"], nameof(Renders_diagnostics_link_instead_of_live_tests_for_authenticated_sessions));

        Assert.Equal(new[]{"Today","Inbox","Plan","Projects"},cut.FindAll("[data-testid='nav-primary-planning'] a").Select(link=>link.TextContent.Trim()));
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
    public void Canonical_capture_and_explicit_Habitica_cache_links_keep_distinct_names_and_routes()
    {
        JSInterop.Mode = JSRuntimeMode.Loose; Services.AddMudServices();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(CreateAuthenticatedState()));
        var cut = Render<AppNavMenu>();
        Assert.Equal("Tasks", cut.Find("[data-testid='todo-nav-section'] a[href='/todo/tasks']").TextContent.Trim());
        Assert.Equal("/todo/tasks",cut.Find("a.nav-capture").GetAttribute("href"));
        Assert.Contains("Habitica tasks", cut.Find("a[href='/tasks']").TextContent);
        Qualification.PageLayoutEvidence.Save("canonical-nav-legacy", "<aside class=\"app-drawer\" style=\"min-height:100vh;max-width:300px\">" + cut.Markup + "</aside>", "/tasks", "explicit-provider-cache-destination", ["navigation"], nameof(Canonical_capture_and_explicit_Habitica_cache_links_keep_distinct_names_and_routes));
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
            builder =>
            {
                builder.OpenElement(0, "span"); builder.AddAttribute(1, "data-layout-content", "true");
                builder.AddContent(2, "Fixture primary content with a long readable route context."); builder.CloseElement();
                builder.OpenElement(3, "button"); builder.AddAttribute(4, "type", "button");
                builder.AddAttribute(5, "class", "app-input"); builder.AddAttribute(6, "data-layout-content", "true");
                builder.AddAttribute(7, "data-layout-primary", "true"); builder.AddContent(8, "Fixture page action"); builder.CloseElement();
            }));
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
