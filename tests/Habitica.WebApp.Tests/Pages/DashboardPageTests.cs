using Bunit;
using Habitica.Application.Dashboard;
using Habitica.Application.Sync;
using Habitica.Domain.Party;
using Habitica.Domain.Sync;
using Habitica.Domain.Tasks;
using Habitica.Domain.User;
using Habitica.Rules.Stats;
using Habitica.Storage;
using Habitica.WebApp.Pages;
using Habitica.WebApp.Theme;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Pages;

public sealed class DashboardPageTests : BunitContext
{
    [Fact]
    public void Signed_out_empty_dashboard_has_sign_in_action()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new CharacterStatsViewModelFactory());
        Services.AddSingleton(new PendingDamageEstimateFactory());
        Services.AddSingleton<IKeyValueStorage>(new InMemoryKeyValueStorage());
        Services.AddScoped<ColorSchemeService>();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(SessionViewModel.Empty));

        var cut = Render<DashboardPage>();

        Assert.Contains("No saved account data is available on this device yet.", cut.Markup);
        Assert.Contains("href=\"/sign-in\"", cut.Markup);
        Assert.Contains("empty-state-actions", cut.Markup);
        Assert.DoesNotContain("Sign in or refresh", cut.Markup);
    }

    [Fact]
    public void Renders_cached_user_snapshot_cards_and_freshness_state()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new CharacterStatsViewModelFactory());
        Services.AddSingleton(new PendingDamageEstimateFactory());
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: new TaskCollectionSnapshot(
                    DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                    new[]
                    {
                        new TaskSnapshot("todo-1", "Buy milk", TaskType.Todo, false, 1.5m, null, null),
                        new TaskSnapshot("daily-1", "Exercise", TaskType.Daily, false, 1m, null, null)
                    }),
                ClassName: "wizard",
                Level: 15,
                UserSnapshot: new UserSnapshot(
                    DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                    "Mage Tester",
                    "wizard",
                    15,
                    42.5m,
                    50m,
                    33.5m,
                    40m,
                    125.1m,
                    74.9m,
                    88.25m,
                    "party-123",
                    "Wolf-Base",
                    "Wolf-Base",
                    new EquipmentSnapshot(
                        new GearSlotsSnapshot("head_wizard_3", "armor_wizard_4", "weapon_wizard_5", "shield_wizard_2", "back_wizard_1"),
                        new GearSlotsSnapshot("head_special_2", "armor_special_2", "weapon_special_2", "shield_special_2", "back_special_2")),
                    new InventorySnapshot(1, 1, 1, 1, 1, 1, new[] { "armor_wizard_4", "head_wizard_3" }),
                    UnallocatedStatPoints: 3,
                    Stats: new CharacterStatsSnapshot(12m, 34m, 18m, 21m),
                    Buffs: new CharacterStatsSnapshot(1m, 2m, 3m, 4m)),
                UserFreshness: SnapshotFreshnessState.Fresh,
                DomainStates: new Dictionary<RefreshDomain, DomainRefreshState>
                {
                    [RefreshDomain.Tasks] = new(RefreshDomain.Tasks, true, DateTimeOffset.Parse("2026-04-25T08:00:00Z"), Reason: RefreshReason.AppBoot, Priority: RefreshPriority.Background),
                    [RefreshDomain.UserProfile] = new(RefreshDomain.UserProfile, false, DateTimeOffset.Parse("2026-04-25T08:00:00Z"))
                },
                GearCatalogSnapshot: new GearCatalogSnapshot(
                    DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                    new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
                    {
                        ["head_wizard_3"] = new("head_wizard_3", "Wizard Hat", "Head", "wizard", null, new GearStatBlock(0m, 0m, 0m, 5m)),
                        ["armor_wizard_4"] = new("armor_wizard_4", "Wizard Robe", "Armor", "wizard", null, new GearStatBlock(0m, 6m, 0m, 0m)),
                        ["weapon_wizard_5"] = new("weapon_wizard_5", "Wizard Staff", "Weapon", "wizard", null, new GearStatBlock(0m, 10m, 0m, 3m)),
                        ["shield_wizard_2"] = new("shield_wizard_2", "Focus", "Shield", "wizard", null, new GearStatBlock(0m, 0m, 2m, 0m)),
                        ["back_wizard_1"] = new("back_wizard_1", "Cape", "Back", null, null, new GearStatBlock(1m, 1m, 1m, 1m))
                    }))));

        Services.AddSingleton<IKeyValueStorage>(new InMemoryKeyValueStorage());
        Services.AddScoped<ColorSchemeService>();
        var cut = Render<DashboardPage>();
        var refreshStatusText = cut.Find("[data-testid='dashboard-refresh-status']").TextContent;

        Assert.Contains("Account data is up to date", cut.Markup);
        Assert.Contains("Tasks refreshing", refreshStatusText);
        Assert.Contains("Account updated", refreshStatusText);
        Assert.Contains("Mage Tester", cut.Markup);
        Assert.Contains("Level 15", cut.Markup);
        Assert.Contains("HP", cut.Markup);
        Assert.DoesNotContain("Current pet, mount, and party", cut.Markup);
        Assert.DoesNotContain("Pet-Wolf-Base.png", cut.Markup);
        Assert.DoesNotContain("Mount_Icon_Wolf-Base.png", cut.Markup);
        Assert.DoesNotContain("Saved inventory counts", cut.Markup);
        Assert.DoesNotContain("Pet_Egg_Wolf.png", cut.Markup);
        Assert.DoesNotContain("inventory_quest_scroll.png", cut.Markup);
        Assert.Contains("Open tasks", cut.Markup);
        Assert.Equal("app-input", cut.Find("[data-testid='armoire-open-count']").GetAttribute("class"));
        Assert.NotEmpty(cut.FindAll("[data-testid='buy-gems-with-gold']"));
        Assert.Contains("Companion links", cut.Markup);
        Assert.Contains("href=\"/tasks\"", cut.Markup);
        Assert.Contains("href=\"/inventory\"", cut.Markup);
        Assert.Contains("href=\"/pets-mounts\"", cut.Markup);
        Assert.Contains("href=\"/party\"", cut.Markup);
        Assert.Contains("href=\"/quests\"", cut.Markup);
        Assert.Contains("href=\"/spells\"", cut.Markup);
        var navigationPanel = cut.Find("[data-testid='dashboard-navigation-panel']");
        var navigationMarkup = navigationPanel.InnerHtml;
        Assert.Contains("href=\"https://habitica.com\"", navigationMarkup);
        Assert.Contains("Open Habitica", navigationMarkup);
        Assert.DoesNotContain("href=\"https://habitica.com/tasks\"", navigationMarkup);
        Assert.DoesNotContain("href=\"https://habitica.com/inventory/equipment\"", navigationMarkup);
        Assert.DoesNotContain("href=\"https://habitica.com/inventory/stable\"", navigationMarkup);
        Assert.DoesNotContain("href=\"https://habitica.com/party\"", navigationMarkup);
        var externalHabiticaLinks = cut
            .FindAll("[data-testid='dashboard-navigation-panel'] a")
            .Where(link => link.GetAttribute("href")?.StartsWith("https://habitica.com", StringComparison.Ordinal) == true)
            .ToArray();
        Assert.Single(externalHabiticaLinks);
        var dashboardLinkActions = cut.FindAll(".dashboard-link-actions a");
        Assert.Equal(6, dashboardLinkActions.Count);
        Assert.All(dashboardLinkActions, link =>
        {
            var href = link.GetAttribute("href") ?? string.Empty;
            Assert.Equal("Open", link.TextContent.Trim());
            Assert.StartsWith("Open ", link.GetAttribute("aria-label") ?? string.Empty, StringComparison.Ordinal);
            Assert.False(href.StartsWith("https://habitica.com", StringComparison.Ordinal), $"Expected local dashboard link, got {href}.");
        });
        var dashboardLinkCopies = cut.FindAll(".dashboard-link-copy");
        Assert.Equal(6, dashboardLinkCopies.Count);
        Assert.All(dashboardLinkCopies, copy =>
        {
            Assert.Equal(2, copy.Children.Length);
            Assert.Equal("STRONG", copy.Children[0].TagName);
            Assert.Contains("dashboard-stat-note", copy.Children[1].ClassList);
        });
        Assert.Contains("Pending damage estimate", cut.Markup);
        Assert.Contains("Incomplete Dailies", cut.Markup);
        Assert.Contains("2 HP", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid='cron-unfinished-dailies']"));
        Assert.Contains("3 unspent stat points", cut.Markup);
        Assert.Contains("#stats", cut.Markup);
        Assert.Contains("Equipment", cut.Markup);
        Assert.Contains("Effective", cut.Markup);
        Assert.Contains("STR", cut.Markup);
    }

    [Fact]
    public void Does_not_render_zero_stat_targets_when_snapshot_does_not_include_caps()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new CharacterStatsViewModelFactory());
        Services.AddSingleton(new PendingDamageEstimateFactory());
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Strixetus",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: new TaskCollectionSnapshot(
                    DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
                    Array.Empty<TaskSnapshot>()),
                ClassName: "rogue",
                Level: 32,
                UserSnapshot: new UserSnapshot(
                    DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
                    "Strixetus",
                    "rogue",
                    32,
                    50m,
                    0m,
                    171.99m,
                    0m,
                    488.17m,
                    0m,
                    1127.82m,
                    "party-123",
                    "Wolf-Base",
                    "Wolf-Base",
                    new EquipmentSnapshot(
                        new GearSlotsSnapshot("head_rogue_6", "armor_rogue_6", "weapon_rogue_6", "shield_rogue_6", "back_rogue_6"),
                        new GearSlotsSnapshot(null, null, null, null, null)),
                    new InventorySnapshot(1, 1, 1, 1, 1, 1, Array.Empty<string>())),
                UserFreshness: SnapshotFreshnessState.Fresh)));

        Services.AddSingleton<IKeyValueStorage>(new InMemoryKeyValueStorage());
        Services.AddScoped<ColorSchemeService>();
        var cut = Render<DashboardPage>();

        Assert.DoesNotContain("50 / 0", cut.Markup);
        Assert.DoesNotContain("171.99 / 0", cut.Markup);
        Assert.DoesNotContain("488.17 / 0", cut.Markup);
        Assert.Contains(">50<", cut.Markup);
        Assert.Contains(">171.99<", cut.Markup);
        Assert.Contains("Current XP", cut.Markup);
    }

    [Fact]
    public void Stat_allocation_prompt_is_hidden_until_level_ten()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new CharacterStatsViewModelFactory());
        Services.AddSingleton(new PendingDamageEstimateFactory());
        var controller = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "New Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: new TaskCollectionSnapshot(DateTimeOffset.Parse("2026-04-25T08:00:00Z"), Array.Empty<TaskSnapshot>()),
                ClassName: "warrior",
                Level: 9,
                UserSnapshot: new UserSnapshot(
                    DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                    "New Tester",
                    "warrior",
                    9,
                    42.5m,
                    50m,
                    0m,
                    0m,
                    125.1m,
                    74.9m,
                    88.25m,
                    null,
                    null,
                    null,
                    new EquipmentSnapshot(
                        new GearSlotsSnapshot(null, null, null, null, null),
                        new GearSlotsSnapshot(null, null, null, null, null)),
                    new InventorySnapshot(0, 0, 0, 0, 0, 0, Array.Empty<string>()),
                    UnallocatedStatPoints: 3,
                    Stats: CharacterStatsSnapshot.Zero,
                    Buffs: CharacterStatsSnapshot.Zero),
                UserFreshness: SnapshotFreshnessState.Fresh));
        Services.AddSingleton<IAppSessionController>(controller);

        Services.AddSingleton<IKeyValueStorage>(new InMemoryKeyValueStorage());
        Services.AddScoped<ColorSchemeService>();
        var cut = Render<DashboardPage>();

        Assert.DoesNotContain("3 unspent stat points", cut.Markup);
        Assert.Contains("Stat allocation unlocks at level 10.", cut.Markup);
        Assert.Contains("allocation unlock", cut.Markup);
        Assert.True(cut.Find("[data-testid='allocate-int-plus']").HasAttribute("disabled"));
        Assert.True(cut.Find("[data-testid='apply-stat-allocation']").HasAttribute("disabled"));
    }

    [Fact]
    public void Stat_allocation_uses_plus_buttons_and_applies_selected_points()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new CharacterStatsViewModelFactory());
        Services.AddSingleton(new PendingDamageEstimateFactory());
        var controller = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: new TaskCollectionSnapshot(DateTimeOffset.Parse("2026-04-25T08:00:00Z"), Array.Empty<TaskSnapshot>()),
                ClassName: "wizard",
                Level: 15,
                UserSnapshot: new UserSnapshot(
                    DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                    "Mage Tester",
                    "wizard",
                    15,
                    42.5m,
                    50m,
                    33.5m,
                    40m,
                    125.1m,
                    74.9m,
                    88.25m,
                    "party-123",
                    null,
                    null,
                    new EquipmentSnapshot(
                        new GearSlotsSnapshot(null, null, null, null, null),
                        new GearSlotsSnapshot(null, null, null, null, null)),
                    new InventorySnapshot(0, 0, 0, 0, 0, 0, Array.Empty<string>()),
                    UnallocatedStatPoints: 3,
                    Stats: new CharacterStatsSnapshot(12m, 34m, 18m, 21m),
                    Buffs: CharacterStatsSnapshot.Zero),
                UserFreshness: SnapshotFreshnessState.Fresh));
        Services.AddSingleton<IAppSessionController>(controller);

        Services.AddSingleton<IKeyValueStorage>(new InMemoryKeyValueStorage());
        Services.AddScoped<ColorSchemeService>();
        var cut = Render<DashboardPage>();

        cut.Find("[data-testid='allocate-int-plus']").Click();
        cut.Find("[data-testid='allocate-int-plus']").Click();
        cut.Find("[data-testid='allocate-per-plus']").Click();
        cut.Find("[data-testid='apply-stat-allocation']").Click();

        Assert.Equal(new StatAllocation(0, 2, 0, 1), controller.StatAllocationCalls.Single());
    }

    [Fact]
    public void Start_new_day_card_requires_confirmation_and_calls_session_controller()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new CharacterStatsViewModelFactory());
        Services.AddSingleton(new PendingDamageEstimateFactory());
        var controller = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: new TaskCollectionSnapshot(
                    DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                    new[]
                    {
                        new TaskSnapshot("daily-1", "Exercise", TaskType.Daily, false, 1m, null, null, IsDue: true),
                        new TaskSnapshot("daily-2", "Review notes", TaskType.Daily, false, 1m, null, null, IsDue: true)
                    }),
                ClassName: "wizard",
                Level: 15,
                UserSnapshot: new UserSnapshot(
                    DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                    "Mage Tester",
                    "wizard",
                    15,
                    42.5m,
                    50m,
                    33.5m,
                    40m,
                    125.1m,
                    74.9m,
                    88.25m,
                    "party-123",
                    null,
                    null,
                    new EquipmentSnapshot(
                        new GearSlotsSnapshot(null, null, null, null, null),
                        new GearSlotsSnapshot(null, null, null, null, null)),
                    new InventorySnapshot(0, 0, 0, 0, 0, 0, Array.Empty<string>()),
                    CurrentHabiticaDayKey: "2026-04-25",
                    NeedsCron: true),
                UserFreshness: SnapshotFreshnessState.Fresh));
        Services.AddSingleton<IAppSessionController>(controller);

        Services.AddSingleton<IKeyValueStorage>(new InMemoryKeyValueStorage());
        Services.AddScoped<ColorSchemeService>();
        var cut = Render<DashboardPage>();

        Assert.Contains("Unfinished dailies", cut.Markup);
        Assert.Equal("2", cut.Find("[data-testid='cron-dailies-count']").TextContent);
        cut.Find("[data-testid='complete-cron-daily-daily-1']").Click();
        Assert.Equal("daily-1", Assert.Single(controller.ScoreTaskCalls).TaskId);
        Assert.Equal("1", cut.Find("[data-testid='cron-dailies-count']").TextContent);
        Assert.NotNull(cut.Find("[data-testid='complete-cron-daily-daily-2']"));

        cut.Find("[data-testid='start-new-day']").Click();
        Assert.Contains("Missed Dailies may be processed", cut.Markup);
        Assert.Contains("Party buffs expire separately for each member", cut.Markup);

        cut.Find("[data-testid='confirm-start-new-day']").Click();

        Assert.Equal(1, controller.StartNewDayCalls);
        Assert.Contains("Started a new Habitica day.", cut.Markup);
    }

    [Fact]
    public void Start_new_day_can_auto_equip_recommended_cron_gear()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new CharacterStatsViewModelFactory());
        Services.AddSingleton(new PendingDamageEstimateFactory());
        var controller = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: new TaskCollectionSnapshot(DateTimeOffset.Parse("2026-04-25T08:00:00Z"), Array.Empty<TaskSnapshot>()),
                ClassName: "wizard",
                Level: 15,
                UserSnapshot: new UserSnapshot(
                    DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                    "Mage Tester",
                    "wizard",
                    15,
                    42.5m,
                    50m,
                    33.5m,
                    40m,
                    125.1m,
                    74.9m,
                    88.25m,
                    "party-123",
                    null,
                    null,
                    new EquipmentSnapshot(
                        new GearSlotsSnapshot("head_int", null, null, null, null),
                        new GearSlotsSnapshot(null, null, null, null, null)),
                    new InventorySnapshot(
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        new[] { "head_int", "head_con", "armor_con" }),
                    CurrentHabiticaDayKey: "2026-04-25",
                    NeedsCron: true),
                UserFreshness: SnapshotFreshnessState.Fresh,
                GearCatalogSnapshot: new GearCatalogSnapshot(
                    DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                    new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
                    {
                        ["head_int"] = new("head_int", "INT Hood", "Head", "wizard", null, new GearStatBlock(0m, 8m, 0m, 0m)),
                        ["head_con"] = new("head_con", "CON Helm", "Head", "warrior", null, new GearStatBlock(0m, 0m, 12m, 0m)),
                        ["armor_con"] = new("armor_con", "CON Armor", "Armor", null, null, new GearStatBlock(0m, 0m, 9m, 0m))
                    })));
        Services.AddSingleton<IAppSessionController>(controller);

        Services.AddSingleton<IKeyValueStorage>(new InMemoryKeyValueStorage());
        Services.AddScoped<ColorSchemeService>();
        var cut = Render<DashboardPage>();

        Assert.Empty(cut.FindAll("[data-testid='cron-unfinished-dailies']"));
        Assert.Contains("INT for mana", cut.Markup);
        Assert.True(cut.Find("[data-testid='start-new-day-auto-equip']").HasAttribute("checked"));
        Assert.NotNull(cut.Find("[data-testid='start-new-day-gear-stats']"));
        Assert.Contains("Recommended gear is already equipped.", cut.Markup);

        cut.Find("[data-testid='start-new-day-gear-goal']").Change("Constitution");
        Assert.Contains("Review temporary gear changes", cut.Markup);
        cut.Find("[data-testid='start-new-day']").Click();
        cut.Find("[data-testid='confirm-start-new-day']").Click();

        var request = Assert.Single(controller.StartNewDayRequests);
        Assert.True(request.AutoEquipRecommendedGear);
        Assert.Equal("CON for less damage", request.GearOptimizationGoalLabel);
        Assert.Equal("head_con", request.AutoEquipGearSlots!.Head);
        Assert.Equal("armor_con", request.AutoEquipGearSlots.Armor);
    }

    [Fact]
    public void Health_potion_action_requires_confirmation_and_calls_session_controller()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new CharacterStatsViewModelFactory());
        Services.AddSingleton(new PendingDamageEstimateFactory());
        var controller = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: new TaskCollectionSnapshot(
                    DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                    new[]
                    {
                        new TaskSnapshot("daily-1", "Exercise", TaskType.Daily, false, 1.5m, null, null)
                    }),
                ClassName: "wizard",
                Level: 15,
                UserSnapshot: new UserSnapshot(
                    DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                    "Mage Tester",
                    "wizard",
                    15,
                    10m,
                    50m,
                    33.5m,
                    40m,
                    125.1m,
                    74.9m,
                    25m,
                    "party-123",
                    null,
                    null,
                    new EquipmentSnapshot(
                        new GearSlotsSnapshot(null, null, null, null, null),
                        new GearSlotsSnapshot(null, null, null, null, null)),
                    new InventorySnapshot(0, 0, 0, 0, 0, 0, Array.Empty<string>())),
                UserFreshness: SnapshotFreshnessState.Fresh));
        Services.AddSingleton<IAppSessionController>(controller);

        Services.AddSingleton<IKeyValueStorage>(new InMemoryKeyValueStorage());
        Services.AddScoped<ColorSchemeService>();
        var cut = Render<DashboardPage>();

        cut.Find("[data-testid='buy-health-potion']").Click();
        Assert.Contains("Confirm purchase", cut.Markup);

        cut.Find("[data-testid='confirm-buy-health-potion']").Click();

        Assert.Equal(1, controller.BuyHealthPotionCalls);
    }

    [Fact]
    public void Gem_gold_purchase_card_clamps_quantity_and_requires_confirmation()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new CharacterStatsViewModelFactory());
        Services.AddSingleton(new PendingDamageEstimateFactory());
        var controller = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: new TaskCollectionSnapshot(DateTimeOffset.Parse("2026-04-25T08:00:00Z"), Array.Empty<TaskSnapshot>()),
                ClassName: "wizard",
                Level: 15,
                UserSnapshot: new UserSnapshot(
                    DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                    "Mage Tester",
                    "wizard",
                    15,
                    42.5m,
                    50m,
                    33.5m,
                    40m,
                    125.1m,
                    74.9m,
                    320m,
                    "party-123",
                    null,
                    null,
                    new EquipmentSnapshot(
                        new GearSlotsSnapshot(null, null, null, null, null),
                        new GearSlotsSnapshot(null, null, null, null, null)),
                    new InventorySnapshot(0, 0, 0, 0, 0, 0, Array.Empty<string>()),
                    GemBalance: 2m,
                    CanBuyGemsForGold: true,
                    RemainingGemPurchases: 3),
                UserFreshness: SnapshotFreshnessState.Fresh));
        Services.AddSingleton<IAppSessionController>(controller);
        Services.AddSingleton<IKeyValueStorage>(new InMemoryKeyValueStorage());
        Services.AddScoped<ColorSchemeService>();

        var cut = Render<DashboardPage>();

        var bulkArmoireIndex = cut.Markup.IndexOf("<h2>Bulk armoire</h2>", StringComparison.Ordinal);
        var buyGemsIndex = cut.Markup.IndexOf("<h2>Buy gems with gold</h2>", StringComparison.Ordinal);
        Assert.True(bulkArmoireIndex >= 0);
        Assert.True(buyGemsIndex > bulkArmoireIndex);
        Assert.Contains("Buy gems with gold", cut.Markup);
        Assert.Contains("Gem balance 2.", cut.Markup);
        Assert.Contains("Gold can buy 16 gems at 20 GP each.", cut.Markup);
        Assert.Contains("Monthly limit: 3 gems remaining.", cut.Markup);
        Assert.Contains("Available: buy up to 3 gems.", cut.Markup);
        Assert.Equal("3", cut.Find("[data-testid='armoire-open-count']").GetAttribute("value"));
        Assert.Equal("3", cut.Find("[data-testid='gem-purchase-count']").GetAttribute("value"));

        cut.Find("[data-testid='gem-purchase-count']").Change("10");
        cut.Find("[data-testid='buy-gems-with-gold']").Click();
        Assert.Contains("Confirm purchase", cut.Markup);

        cut.Find("[data-testid='confirm-buy-gems-with-gold']").Click();

        Assert.Equal(3, Assert.Single(controller.BuyGemsForGoldCalls));
    }

    [Fact]
    public void Gem_gold_purchase_card_allows_unknown_eligibility_with_cautious_copy()
    {
        var (cut, controller) = RenderDashboardForGemPurchase(canBuyGemsForGold: null, remainingGemPurchases: null);

        Assert.Contains("Buy gems with gold", cut.Markup);
        Assert.Contains("Monthly limit unavailable.", cut.Markup);
        Assert.Contains("Available to try; Habitica will confirm eligibility.", cut.Markup);
        Assert.False(cut.Find("[data-testid='buy-gems-with-gold']").HasAttribute("disabled"));
        Assert.Equal("4", cut.Find("[data-testid='gem-purchase-count']").GetAttribute("value"));

        cut.Find("[data-testid='buy-gems-with-gold']").Click();
        cut.Find("[data-testid='confirm-buy-gems-with-gold']").Click();

        Assert.Equal(4, Assert.Single(controller.BuyGemsForGoldCalls));
    }

    [Fact]
    public void Gem_gold_purchase_card_shows_known_ineligible_state()
    {
        var (cut, controller) = RenderDashboardForGemPurchase(canBuyGemsForGold: false);

        Assert.Contains("Buy gems with gold", cut.Markup);
        Assert.Contains("Subscribe in Habitica to buy gems with gold.", cut.Markup);
        Assert.True(cut.Find("[data-testid='buy-gems-with-gold']").HasAttribute("disabled"));
        var subscribeLink = cut.Find("[data-testid='gem-subscription-link']");
        Assert.Equal("https://habitica.com/user/settings/subscription", subscribeLink.GetAttribute("href"));
        Assert.Empty(controller.BuyGemsForGoldCalls);
    }

    [Fact]
    public void Gem_gold_purchase_card_shows_insufficient_gold_state()
    {
        var (cut, _) = RenderDashboardForGemPurchase(gold: 10m);

        Assert.Contains("Buy gems with gold", cut.Markup);
        Assert.Contains("Gold can buy 0 gems at 20 GP each.", cut.Markup);
        Assert.Contains("Needs 20 GP.", cut.Markup);
        Assert.True(cut.Find("[data-testid='buy-gems-with-gold']").HasAttribute("disabled"));
        Assert.Empty(cut.FindAll("[data-testid='gem-subscription-link']"));
    }

    [Fact]
    public void Gem_gold_purchase_card_shows_monthly_cap_state()
    {
        var (cut, _) = RenderDashboardForGemPurchase(remainingGemPurchases: 0);

        Assert.Contains("Buy gems with gold", cut.Markup);
        Assert.Contains("Monthly limit: 0 gems remaining.", cut.Markup);
        Assert.Contains("Monthly gem limit reached. Habitica resets this near the start of the month.", cut.Markup);
        Assert.True(cut.Find("[data-testid='buy-gems-with-gold']").HasAttribute("disabled"));
        Assert.Empty(cut.FindAll("[data-testid='gem-subscription-link']"));
    }

    [Fact]
    public void Gem_gold_purchase_card_shows_stale_refresh_state()
    {
        var (cut, _) = RenderDashboardForGemPurchase(userFreshness: SnapshotFreshnessState.Stale);

        Assert.Contains("Buy gems with gold", cut.Markup);
        Assert.Contains("Refresh account data before buying gems with gold.", cut.Markup);
        Assert.True(cut.Find("[data-testid='buy-gems-with-gold']").HasAttribute("disabled"));
        Assert.Empty(cut.FindAll("[data-testid='gem-subscription-link']"));
    }

    [Fact]
    public void Pending_quest_invitation_warning_allows_dashboard_response()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new CharacterStatsViewModelFactory());
        Services.AddSingleton(new PendingDamageEstimateFactory());
        var controller = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: new TaskCollectionSnapshot(DateTimeOffset.Parse("2026-04-25T08:00:00Z"), Array.Empty<TaskSnapshot>()),
                UserId: "user-id",
                ClassName: "wizard",
                Level: 15,
                UserSnapshot: new UserSnapshot(
                    DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                    "Mage Tester",
                    "wizard",
                    15,
                    42.5m,
                    50m,
                    33.5m,
                    40m,
                    125.1m,
                    74.9m,
                    88.25m,
                    "party-123",
                    null,
                    null,
                    new EquipmentSnapshot(
                        new GearSlotsSnapshot(null, null, null, null, null),
                        new GearSlotsSnapshot(null, null, null, null, null)),
                    new InventorySnapshot(0, 0, 0, 0, 0, 0, Array.Empty<string>())),
                UserFreshness: SnapshotFreshnessState.Fresh,
                PartySnapshot: new PartySnapshot(
                    DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                    "party-123",
                    "Night Owls",
                    "Quest party",
                    2,
                    new PartyQuestSnapshot("dragon", false, 0m, 0m, 1, Name: "Dragon"),
                    new[]
                    {
                        new PartyMemberSnapshot("user-id", "Mage Tester", null, null, null, PartyCronState.Unknown, "Unknown.", null, null, ParticipationStatus: PartyQuestParticipationStatus.Pending),
                        new PartyMemberSnapshot("other-user", "Alpha", null, null, null, PartyCronState.Unknown, "Unknown.", null, null, ParticipationStatus: PartyQuestParticipationStatus.Accepted)
                    }),
                PartyFreshness: SnapshotFreshnessState.Fresh));
        Services.AddSingleton<IAppSessionController>(controller);

        Services.AddSingleton<IKeyValueStorage>(new InMemoryKeyValueStorage());
        Services.AddScoped<ColorSchemeService>();
        var cut = Render<DashboardPage>();

        Assert.Contains("You have not responded to the current party quest invitation.", cut.Markup);

        cut.FindAll("button").Single(button => button.TextContent.Contains("Accept", StringComparison.Ordinal)).Click();
        cut.FindAll("button").Single(button => button.TextContent.Contains("Reject", StringComparison.Ordinal)).Click();

        Assert.Equal(1, controller.AcceptPartyQuestInvitationCalls);
        Assert.Equal(1, controller.RejectPartyQuestInvitationCalls);
    }

    [Fact]
    public void Appearance_panel_is_folded_until_toggled()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new CharacterStatsViewModelFactory());
        Services.AddSingleton(new PendingDamageEstimateFactory());
        Services.AddSingleton<IKeyValueStorage>(new InMemoryKeyValueStorage());
        Services.AddScoped<ColorSchemeService>();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(SessionViewModel.Empty));

        var cut = Render<DashboardPage>();

        // Collapsed by default: the toggle shows but the color-scheme controls are hidden.
        Assert.NotNull(cut.Find("[data-testid='dashboard-appearance-toggle']"));
        Assert.Empty(cut.FindAll("[data-testid='color-scheme-select']"));

        cut.Find("[data-testid='dashboard-appearance-toggle']").Click();

        Assert.Contains("Done", cut.Find("[data-testid='dashboard-appearance-toggle']").TextContent);
        Assert.NotNull(cut.Find("[data-testid='color-scheme-select']"));
    }

    private (IRenderedComponent<DashboardPage> Cut, FakeAppSessionController Controller) RenderDashboardForGemPurchase(
        decimal gold = 80m,
        bool? canBuyGemsForGold = true,
        int? remainingGemPurchases = 5,
        decimal? gemBalance = 2m,
        SnapshotFreshnessState userFreshness = SnapshotFreshnessState.Fresh)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new CharacterStatsViewModelFactory());
        Services.AddSingleton(new PendingDamageEstimateFactory());
        var controller = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: new TaskCollectionSnapshot(DateTimeOffset.Parse("2026-04-25T08:00:00Z"), Array.Empty<TaskSnapshot>()),
                ClassName: "wizard",
                Level: 15,
                UserSnapshot: CreateGemPurchaseUserSnapshot(gold, canBuyGemsForGold, remainingGemPurchases, gemBalance),
                UserFreshness: userFreshness));
        Services.AddSingleton<IAppSessionController>(controller);
        Services.AddSingleton<IKeyValueStorage>(new InMemoryKeyValueStorage());
        Services.AddScoped<ColorSchemeService>();

        return (Render<DashboardPage>(), controller);
    }

    private static UserSnapshot CreateGemPurchaseUserSnapshot(
        decimal gold,
        bool? canBuyGemsForGold,
        int? remainingGemPurchases,
        decimal? gemBalance)
    {
        return new UserSnapshot(
            DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
            "Mage Tester",
            "wizard",
            15,
            42.5m,
            50m,
            33.5m,
            40m,
            125.1m,
            74.9m,
            gold,
            "party-123",
            null,
            null,
            new EquipmentSnapshot(
                new GearSlotsSnapshot(null, null, null, null, null),
                new GearSlotsSnapshot(null, null, null, null, null)),
            new InventorySnapshot(0, 0, 0, 0, 0, 0, Array.Empty<string>()),
            GemBalance: gemBalance,
            CanBuyGemsForGold: canBuyGemsForGold,
            RemainingGemPurchases: remainingGemPurchases);
    }
}
