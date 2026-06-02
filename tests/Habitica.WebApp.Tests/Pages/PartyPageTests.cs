using Bunit;
using Habitica.Domain.Party;
using Habitica.Domain.Sync;
using Habitica.Domain.User;
using Habitica.WebApp.Pages;
using Habitica.WebApp.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Pages;

public sealed class PartyPageTests : BunitContext
{
    [Fact]
    public void Filters_member_cards_by_available_class()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: null,
                UserSnapshot: CreateSnapshot(),
                UserFreshness: SnapshotFreshnessState.Fresh,
                PartySnapshot: new PartySnapshot(
                    DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                    "party-123",
                    "Night Owls",
                    "Quest-focused party",
                    3,
                    null,
                    new[]
                    {
                        new PartyMemberSnapshot("user-1", "Alpha", null, null, null, PartyCronState.Unknown, "Unknown.", null, null, ClassName: "wizard"),
                        new PartyMemberSnapshot("user-2", "Beta", null, null, null, PartyCronState.Unknown, "Unknown.", null, null, ClassName: "healer"),
                        new PartyMemberSnapshot("user-3", "Gamma", null, null, null, PartyCronState.Unknown, "Unknown.", null, null)
                    }),
                PartyFreshness: SnapshotFreshnessState.Fresh)));

        var cut = Render<PartyPage>();
        var filter = cut.Find("[data-testid='party-member-class-filter']");

        Assert.Equal(new[] { "All classes", "Healer", "Wizard" }, filter.Children.Select(option => option.TextContent.Trim()));
        Assert.Equal(new[] { "Alpha", "Beta", "Gamma" }, GetRenderedMemberNames(cut));

        filter.Change("wizard");

        Assert.Equal(new[] { "Alpha" }, GetRenderedMemberNames(cut));
    }

    [Fact]
    public void Renders_cached_party_summary_and_quest_state()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./js/partyPage.js").SetupVoid("scrollToElement", _ => true);
        Services.AddMudServices();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: null,
                ClassName: "wizard",
                Level: 15,
                UserSnapshot: new UserSnapshot(
                    DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
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
                    new InventorySnapshot(1, 5, 1, 1, 1, 1, Array.Empty<string>())),
                UserFreshness: SnapshotFreshnessState.Fresh,
                PartySnapshot: new PartySnapshot(
                    DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                    "party-123",
                    "Night Owls",
                    "Quest-focused party",
                    4,
                    new PartyQuestSnapshot(
                        "seaserpent",
                        true,
                        12.5m,
                        3m,
                        2,
                        "Pending damage",
                        PendingDamage: 12.5m,
                        BossHealthRemaining: 875.25m,
                        BossHealthTotal: 1000m,
                        TotalPendingDamage: 42.75m,
                        PendingPartyDamage: 3m,
                        CompletionEstimate: new PartyQuestCompletionEstimate(
                            true,
                            DateTimeOffset.Parse("2026-04-26T10:15:00Z"),
                            DateTimeOffset.Parse("2026-04-26T10:15:00Z"),
                            PartyQuestEstimateConfidence.High,
                            "Expected to finish when Alpha checks in around Apr 26, 10:15.",
                            "Alpha",
                            "user-1"),
                        Description: "A cached sea-serpent quest description.",
                        RewardSummary: new[] { "10 Gold", "100 XP", "Sea Serpent Egg" },
                        StarterUserId: "user-1",
                        StarterDisplayName: "Alpha",
                        StartedAtUtc: DateTimeOffset.Parse("2026-04-26T08:00:00Z")),
                    new[]
                    {
                        new PartyMemberSnapshot(
                            "user-1",
                            "Alpha",
                            DateTimeOffset.Parse("2026-04-26T08:15:00Z"),
                            0,
                            0,
                            PartyCronState.CronedToday,
                            "Croned today.",
                            "2026-04-26",
                            DateTimeOffset.Parse("2026-04-26T00:00:00Z"),
                            TimeSpan.Parse("08:15"),
                            1,
                            PendingQuestDamage: 7.2m,
                            Health: 28.5m,
                            MaxHealth: 50m,
                            Mana: 12m,
                            MaxMana: 80m),
                        new PartyMemberSnapshot(
                            "user-2",
                            "Beta",
                            DateTimeOffset.Parse("2026-04-25T09:45:00Z"),
                            null,
                            null,
                            PartyCronState.NotCronedYet,
                            "Habitica public member data hides day start/timezone; classified from public CRON timestamp by UTC day.",
                            "2026-04-26",
                            DateTimeOffset.Parse("2026-04-26T00:00:00Z"),
                            TimeSpan.Parse("09:45"),
                            1,
                            PendingQuestDamage: 5.3m,
                            Health: 7m,
                            MaxHealth: 50m,
                            Mana: 22m,
                            MaxMana: 60m),
                        new PartyMemberSnapshot(
                            "user-3",
                            "Gamma",
                            null,
                            null,
                            null,
                            PartyCronState.Unknown,
                            "Missing lastCron.",
                            null,
                            null,
                            null,
                            0)
                    },
                    new PartyCronDashboardSnapshot(
                        CronedCount: 1,
                        VisibleMemberCount: 3,
                        UnknownCount: 1,
                        PossiblyStaleCount: 0,
                        HistoryDayCount: 1,
                        SampleCount: 2,
                        IsLowConfidence: true,
                        SampleSizeWarning: "Early estimate: based on 1 day of CRON history.",
                        AverageBestBuffTime: TimeSpan.Parse("09:45"),
                        SelfFirstBuffTime: TimeSpan.Parse("09:45"),
                        Members: new[]
                        {
                            new PartyMemberSnapshot(
                                "user-1",
                                "Alpha",
                                DateTimeOffset.Parse("2026-04-26T08:15:00Z"),
                                0,
                                0,
                                PartyCronState.CronedToday,
                                "Croned today.",
                                "2026-04-26",
                                DateTimeOffset.Parse("2026-04-26T00:00:00Z"),
                                TimeSpan.Parse("08:15"),
                                1,
                                PendingQuestDamage: 7.2m,
                                ClassName: "wizard",
                                Level: 15,
                                Health: 28.5m,
                                MaxHealth: 50m,
                                Mana: 12m,
                                MaxMana: 80m,
                                Stats: new PartyMemberStatBreakdownSnapshot(
                                    new PartyStatSectionSnapshot(30m, 70m, 0m, 0m),
                                    new PartyStatSectionSnapshot(20m, 10m, 5m, 7m),
                                    new PartyStatSectionSnapshot(80m, 50m, 213m, 192m),
                                    null))
                        },
                        GraphPoints: new[]
                        {
                            new PartyCronGraphPoint(8, 1, 1m, 0m, 2m),
                            new PartyCronGraphPoint(9, 2, 2m, 1m, 3m)
                        })),
                PartyFreshness: SnapshotFreshnessState.Fresh)));

        var cut = Render<PartyPage>();
        var questsCut = RenderQuestsWorkspace();

        Assert.Contains("Party overview", cut.Markup);
        Assert.Contains("Night Owls", cut.Markup);
        Assert.Contains("Quest-focused party", cut.Markup);
        Assert.Empty(cut.FindAll(".party-card-grid"));
        Assert.Single(cut.FindAll(".party-quest-summary-panel"));
        Assert.Contains("seaserpent", cut.Markup);
        Assert.Contains("href=\"/quests\"", cut.Markup);
        Assert.DoesNotContain("inventory_quest_scroll.png", cut.Markup);
        Assert.Contains("inventory_quest_scroll.png", questsCut.Markup);
        Assert.Contains("Pending party progress", questsCut.Markup);
        Assert.Contains("42.75 damage", questsCut.Markup);
        Assert.Contains("Current boss HP", questsCut.Markup);
        Assert.Contains("875.25/1000 hp", questsCut.Markup);
        Assert.Contains("Estimated boss HP after CRON", questsCut.Markup);
        Assert.Contains("832.5/1000 hp", questsCut.Markup);
        Assert.Contains("Participants", questsCut.Markup);
        Assert.Contains(">2</dd>", questsCut.Markup);
        Assert.Contains("<dt>Starter</dt>", questsCut.Markup);
        Assert.Contains("<dt>Started</dt>", questsCut.Markup);
        Assert.DoesNotContain("<dt>Started</dt><dd>Unavailable</dd>", questsCut.Markup);
        Assert.DoesNotContain("<dt>Accepted</dt>", questsCut.Markup);
        Assert.DoesNotContain("<dt>Pending</dt>", questsCut.Markup);
        Assert.DoesNotContain("<dt>Rejected</dt>", questsCut.Markup);
        Assert.DoesNotContain("<dt>Unknown</dt>", questsCut.Markup);
        Assert.DoesNotContain("<dt>In Inn</dt>", questsCut.Markup);
        Assert.Contains("Expected finish", questsCut.Markup);
        Assert.Contains("quest-estimate-alert", questsCut.Markup);
        Assert.Contains("Finishing member", questsCut.Markup);
        Assert.Contains("Timing confidence", questsCut.Markup);
        Assert.Contains("Alpha", questsCut.Find(".inline-link-button").TextContent);
        Assert.DoesNotContain("Estimate range", questsCut.Markup);
        Assert.DoesNotContain("Damage taken", questsCut.Markup);
        Assert.Contains("CRON summary", cut.Markup);
        Assert.Contains("CRON applied 1/3", cut.Markup);
        Assert.Contains("Data gaps", cut.Markup);
        Assert.Contains("1 unknown", cut.Markup);
        Assert.Contains("Average best buff time", cut.Markup);
        Assert.Contains("Self-first buff time", cut.Markup);
        Assert.Contains("Early estimate: based on 1 day of CRON history.", cut.Markup);
        Assert.Contains("Alpha", cut.Markup);
        Assert.Contains("Beta", cut.Markup);
        Assert.Contains("Gamma", cut.Markup);
        Assert.Contains("HP 28.5/50", cut.Markup);
        Assert.Contains("MP 12/80", cut.Markup);
        Assert.Contains("Low HP", cut.Markup);
        Assert.Contains("Low MP", cut.Markup);
        Assert.Contains("Pending quest", cut.Markup);
        Assert.Contains("7.2 damage", cut.Markup);
        Assert.Contains("5.3 damage", cut.Markup);
        Assert.Contains("Avg 08:15", cut.Markup);
        Assert.DoesNotContain("Avg 08:15 (1 day)", cut.Markup);
        Assert.Contains("Not enough history", cut.Markup);
        Assert.DoesNotContain("Sea Serpent Egg", questsCut.Markup);
        Assert.DoesNotContain("A cached sea-serpent quest description.", questsCut.Markup);
        questsCut.Find("[data-testid='toggle-active-quest-details']").Click();
        Assert.Contains("A cached sea-serpent quest description.", questsCut.Markup);
        Assert.Contains("Sea Serpent Egg", questsCut.Markup);
        Assert.DoesNotContain("Reward details are not available yet.", questsCut.Markup);
        Assert.Empty(questsCut.FindAll("[data-testid='active-quest-participants']"));
        questsCut.Find("[data-testid='toggle-active-quest-participants']").Click();
        Assert.Contains("Alpha", questsCut.Find("[data-testid='active-quest-participants']").TextContent);
        questsCut.Find("[data-testid='active-quest-participants'] .inline-link-button").Click();
        Assert.EndsWith("/party?member=user-1", Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
        Assert.Contains("CRON statistics", cut.Markup);
        Assert.Contains("Historical average", cut.Markup);
        Assert.Contains("1 stored observation day", cut.Markup);
        Assert.Contains("cron-band-path", cut.Markup);
        Assert.Contains("cron-today-path", cut.Markup);
        Assert.Contains("cron-average-path", cut.Markup);
        Assert.Contains("text-anchor=\"end\"", cut.Markup);
        Assert.Contains("Members", cut.Markup);
        Assert.Contains("Details", cut.Markup);
        Assert.DoesNotContain("User ID", cut.Markup);
        Assert.DoesNotContain("Strength", cut.Markup);
        Assert.DoesNotContain("Day start", cut.Markup);
        Assert.DoesNotContain("Habitica public member data hides day start/timezone", cut.Markup);

        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Low HP").Click();
        Assert.Equal("Beta", cut.FindAll(".party-member-card .party-member-identity strong").First().TextContent);

        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Low MP").Click();
        Assert.Equal("Alpha", cut.FindAll(".party-member-card .party-member-identity strong").First().TextContent);

        cut.FindAll("[data-testid='member-details']").First().Click();

        Assert.Contains("User ID", cut.Markup);
        Assert.Contains("user-1", cut.Markup);
        Assert.DoesNotContain("CRON reason", cut.Markup);
        Assert.DoesNotContain("Habitica public member data hides day start/timezone", cut.Markup);
        Assert.Contains("Equipment", cut.Markup);
        Assert.Contains("Buffs", cut.Markup);
        Assert.Contains("Effective", cut.Markup);
        Assert.Contains("Strength", cut.Markup);
        Assert.Contains("137", cut.Markup);
    }

    [Fact]
    public void Renders_party_description_markdown_without_raw_html()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        var sessionController = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: null,
                ClassName: "wizard",
                Level: 15,
                UserSnapshot: CreateSnapshot(),
                UserFreshness: SnapshotFreshnessState.Fresh,
                PartySnapshot: new PartySnapshot(
                    DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                    "party-123",
                    "Night Owls",
                    "Line one\n**Bold move** <script>alert(1)</script>\n[Site](https://example.com)",
                    1,
                    null,
                    Array.Empty<PartyMemberSnapshot>()),
                PartyFreshness: SnapshotFreshnessState.Fresh));
        Services.AddSingleton<IAppSessionController>(sessionController);

        var cut = Render<PartyPage>();

        Assert.Contains("Line one", cut.Markup);
        Assert.Contains("<br", cut.Markup);
        Assert.Contains("<strong>Bold move</strong>", cut.Markup);
        Assert.Contains("<a href=\"https://example.com/\"", cut.Markup);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", cut.Markup);
        Assert.DoesNotContain("<script>", cut.Markup);
    }

    [Fact]
    public void Renders_quest_description_markdown_and_html_breaks_without_raw_html()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        var sessionController = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: null,
                ClassName: "wizard",
                Level: 15,
                UserSnapshot: CreateSnapshot(),
                UserFreshness: SnapshotFreshnessState.Fresh,
                PartySnapshot: new PartySnapshot(
                    DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                    "party-123",
                    "Night Owls",
                    "Quest-focused party",
                    1,
                    new PartyQuestSnapshot(
                        "magicalAxolotl",
                        true,
                        0m,
                        0m,
                        1,
                        "Pending damage",
                        Name: "Magical Axolotl",
                        Description: "Bubbles and <strong>fire</strong>.<br><br>Look out, <em>willpower</em> and **habits**! <script>alert(1)</script>"),
                    Array.Empty<PartyMemberSnapshot>()),
                PartyFreshness: SnapshotFreshnessState.Fresh));
        Services.AddSingleton<IAppSessionController>(sessionController);

        var cut = RenderQuestsWorkspace();

        cut.Find("[data-testid='toggle-active-quest-details']").Click();

        Assert.Contains("Magical Axolotl", cut.Markup);
        Assert.Contains("<strong>fire</strong>", cut.Markup);
        Assert.Contains("<strong>habits</strong>", cut.Markup);
        Assert.Contains("<em>willpower</em>", cut.Markup);
        Assert.Contains("<p>Bubbles and", cut.Markup);
        Assert.Contains("</p><p>Look out", cut.Markup);
        Assert.DoesNotContain("&lt;br", cut.Markup);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", cut.Markup);
        Assert.DoesNotContain("<script>", cut.Markup);
    }

    [Fact]
    public void Renders_shared_quest_queue_pool_and_recent_history()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        var sessionController = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: null,
                UserId: "user-id",
                ClassName: "wizard",
                Level: 15,
                UserSnapshot: CreateSnapshot(),
                UserFreshness: SnapshotFreshnessState.Fresh,
                PartySnapshot: new PartySnapshot(
                    DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                    "party-123",
                    "Night Owls",
                    "Quest-focused party",
                    1,
                    null,
                    Array.Empty<PartyMemberSnapshot>()),
                PartyFreshness: SnapshotFreshnessState.Fresh,
                PartyQuestQueue: new PartyQuestQueueSnapshot(
                    DateTimeOffset.Parse("2026-04-26T09:30:00Z"),
                    new[]
                    {
                        new PartyQuestPoolEntry(
                            "party-123",
                            "moonstone",
                            "Moonstone Chain",
                            "user-id",
                            "Mage Tester",
                            2,
                            DateTimeOffset.Parse("2026-04-26T09:30:00Z"),
                            "Collection",
                            new[] { "450 Gold", "Wolf Cub" }),
                        new PartyQuestPoolEntry(
                            "party-123",
                            "moonstone",
                            "Moonstone Chain",
                            "user-2",
                            "Alpha",
                            3,
                            DateTimeOffset.Parse("2026-04-26T09:31:00Z"),
                            "Collection",
                            new[] { "450 Gold", "Wolf Cub" }),
                        new PartyQuestPoolEntry(
                            "party-123",
                            "sunstone",
                            "Sunstone Chain",
                            "user-2",
                            "Alpha",
                            1,
                            DateTimeOffset.Parse("2026-04-26T09:32:00Z"),
                            "Collection",
                            new[] { "450 Gold", "Wolf Cub" }),
                        new PartyQuestPoolEntry(
                            "party-123",
                            "kraken",
                            "The Kraken of Inkomplete",
                            "user-2",
                            "Alpha",
                            1,
                            DateTimeOffset.Parse("2026-04-26T09:33:00Z"),
                            "Boss",
                            new[] { "Cuttlefish (Egg)" })
                    },
                    new[]
                    {
                        new PartyQuestQueueEntry(
                            "queue-1",
                            "party-123",
                            "moonstone",
                            "Moonstone Chain",
                            "user-id",
                            "Mage Tester",
                            PartyQuestQueueStatus.Queued,
                            DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                            DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                            1,
                            null,
                            false,
                            1,
                            new[]
                            {
                                new PartyQuestVote("user-2", "Alpha", 1, DateTimeOffset.Parse("2026-04-26T09:10:00Z"))
                            },
                            new[] { "450 Gold", "Wolf Cub" }),
                        new PartyQuestQueueEntry(
                            "queue-2",
                            "party-123",
                            "sunstone",
                            "Alpha Quest",
                            "user-2",
                            "Alpha",
                            PartyQuestQueueStatus.Queued,
                            DateTimeOffset.Parse("2026-04-26T09:05:00Z"),
                            DateTimeOffset.Parse("2026-04-26T09:05:00Z"),
                            2,
                            null,
                            false,
                            1,
                            Array.Empty<PartyQuestVote>(),
                            new[] { "450 Gold", "Wolf Cub" })
                    },
                    new[]
                    {
                        new PartyRecentlyCompletedQuest(
                            "party-123",
                            "gryphon",
                            "Gryphon Quest",
                            DateTimeOffset.Parse("2026-04-25T09:00:00Z"),
                            null,
                            "user-2",
                            "Alpha",
                            3,
                            new[] { "300 XP" }),
                        new PartyRecentlyCompletedQuest(
                            "party-123",
                            "phoenix",
                            "Phoenix Quest",
                            DateTimeOffset.Parse("2026-04-25T10:00:00Z"),
                            null,
                            null,
                            null,
                            2,
                            new[] { "400 XP" },
                            null,
                            "user-id",
                            "Mage Tester",
                            "auto",
                            "habitica-chat-boss:phoenix:chat-1")
                    })));
        Services.AddSingleton<IAppSessionController>(sessionController);

        var cut = RenderQuestsWorkspace();

        Assert.Contains("Shared quest planning", cut.Markup);
        Assert.Contains("Moonstone Chain", cut.Markup);
        Assert.Contains("Alpha Quest", cut.Markup);
        Assert.Contains("1 vote", cut.Markup);
        Assert.Contains("Mark completed", cut.Markup);
        Assert.Contains("Alpha", cut.Markup);
        Assert.Contains("Quest pool is open by default", cut.Markup);
        Assert.DoesNotContain("Quest pool is hidden", cut.Markup);
        Assert.Contains("Available from Alpha, Mage Tester", cut.Markup);
        Assert.Contains("5 scrolls owned", cut.Markup);
        Assert.NotNull(cut.Find("[data-testid='quest-pool-search']"));

        cut.Find("[data-testid='toggle-quest-pool']").Click();

        Assert.Contains("Quest pool is hidden", cut.Markup);
        Assert.DoesNotContain("Available from Alpha, Mage Tester", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid='quest-pool-search']"));

        cut.Render(parameters => parameters.Add(component => component.QuestWorkspaceOnly, true));

        Assert.Contains("Quest pool is hidden", cut.Markup);
        Assert.DoesNotContain("Available from Alpha, Mage Tester", cut.Markup);

        cut.Find("[data-testid='toggle-quest-pool']").Click();

        Assert.Contains("Available from Alpha, Mage Tester", cut.Markup);
        Assert.Contains("5 scrolls owned", cut.Markup);
        Assert.Contains("450 Gold", cut.Markup);
        Assert.Contains("Wolf Cub", cut.Markup);
        Assert.Contains("Sunstone Chain", cut.Markup);
        Assert.Contains("Gryphon Quest", cut.Markup);
        Assert.Contains("300 XP", cut.Markup);
        Assert.Contains("Marked manually", cut.Markup);
        Assert.Contains("Phoenix Quest", cut.Markup);
        Assert.Contains("Auto-detected by Mage Tester", cut.Markup);

        SetQuestPoolSearch(cut, "  SUNSTONE  ");

        Assert.Equal(new[] { "Sunstone Chain" }, GetRenderedQuestPoolNames(cut));

        SetQuestPoolSearch(cut, "collection");

        Assert.Equal(new[] { "Moonstone Chain", "Sunstone Chain" }, GetRenderedQuestPoolNames(cut));

        SetQuestPoolSearch(cut, "Mage Tester");

        Assert.Equal(new[] { "Moonstone Chain" }, GetRenderedQuestPoolNames(cut));

        SetQuestPoolSearch(cut, "cuttle");

        Assert.Equal(new[] { "The Kraken of Inkomplete" }, GetRenderedQuestPoolNames(cut));

        SetQuestPoolSearch(cut, "");

        Assert.Equal(new[] { "Moonstone Chain", "Sunstone Chain", "The Kraken of Inkomplete" }, GetRenderedQuestPoolNames(cut));

        cut.Find("[data-testid='hide-not-owned-quests']").Change(true);

        Assert.Contains("Moonstone Chain", cut.Markup);
        Assert.DoesNotContain("Alpha Quest", cut.Markup);
        Assert.DoesNotContain("Sunstone Chain", cut.Markup);
        Assert.Contains("Available from Mage Tester", cut.Markup);
        Assert.DoesNotContain("Available from Alpha, Mage Tester", cut.Markup);

        SetQuestPoolSearch(cut, "Alpha");

        Assert.Empty(GetRenderedQuestPoolNames(cut));
        Assert.Contains("No quests match this search.", cut.Markup);

        SetQuestPoolSearch(cut, "");

        Assert.Equal(new[] { "Moonstone Chain" }, GetRenderedQuestPoolNames(cut));
    }

    [Fact]
    public void Next_quest_renders_above_queue_without_blocking_pool_changes()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(CreateQueueControlState(
            new[]
            {
                CreateQueueEntry("queue-selected", PartyQuestQueueStatus.Selected, expiresAtUtc: DateTimeOffset.Parse("2026-04-29T09:30:00Z")),
                CreateQueueEntry("queue-skipped", PartyQuestQueueStatus.Skipped),
                CreateQueueEntry("queue-expired", PartyQuestQueueStatus.Expired)
            })));

        var cut = RenderQuestsWorkspace();

        Assert.Contains("Next Quest", cut.Markup);
        Assert.Contains("Selected next", cut.Markup);
        Assert.Contains("Next quest until", cut.Markup);
        Assert.Contains("Skipped; can return to queue", cut.Markup);
        Assert.Contains("Expired; can return to queue", cut.Markup);
        Assert.Contains("Skip", cut.Markup);
        Assert.Contains("Return to queue", cut.Markup);
        Assert.Contains("Select", cut.Markup);
        Assert.DoesNotContain("Replace next", cut.Markup);
        Assert.DoesNotContain("Queue changes are locked while a quest is selected.", cut.Markup);

        var addButton = cut.FindAll("button").Single(button => button.TextContent.Contains("Add to queue", StringComparison.Ordinal));
        Assert.False(addButton.HasAttribute("disabled"));
        Assert.DoesNotContain("Queue is locked while a quest is selected.", cut.Markup);
    }

    [Fact]
    public void Quests_workspace_renders_empty_pool_state_without_expanding_disclosure()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(CreateSelectedQuestState("user-id")));

        var partyCut = Render<PartyPage>();

        Assert.Empty(partyCut.FindAll("[data-testid='toggle-quest-pool']"));

        var questsCut = RenderQuestsWorkspace();

        Assert.Equal("Hide quest pool", questsCut.Find("[data-testid='toggle-quest-pool']").TextContent.Trim());
        Assert.NotNull(questsCut.Find("[data-testid='quest-pool-search']"));
        Assert.Contains("No shared quest scroll availability has been published yet.", questsCut.Markup);
    }

    [Fact]
    public void Invite_sent_queue_item_is_hidden_from_next_quest_and_queue()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(CreateQueueControlState(
            new[]
            {
                CreateQueueEntry("queue-invite", PartyQuestQueueStatus.InviteSent, questName: "Invited Quest"),
                CreateQueueEntry("queue-queued", PartyQuestQueueStatus.Queued, questName: "Visible Quest")
            })));

        var cut = RenderQuestsWorkspace();

        Assert.DoesNotContain("Next Quest", cut.Markup);
        Assert.DoesNotContain("Invited Quest", cut.Markup);
        Assert.Contains("Visible Quest", cut.Markup);
    }

    [Fact]
    public void Queue_management_actions_call_session_controller()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        var sessionController = new FakeAppSessionController(CreateQueueControlState(
            new[]
            {
                CreateQueueEntry("queue-selected", PartyQuestQueueStatus.Selected),
                CreateQueueEntry("queue-queued", PartyQuestQueueStatus.Queued)
            }));
        Services.AddSingleton<IAppSessionController>(sessionController);

        var cut = RenderQuestsWorkspace();

        cut.FindAll("button").Single(button => button.TextContent.Contains("Pin", StringComparison.Ordinal)).Click();
        cut.FindAll("button").Single(button => button.TextContent.Contains("Select", StringComparison.Ordinal)).Click();
        cut.FindAll("button").Single(button => button.TextContent.Contains("Skip", StringComparison.Ordinal)).Click();
        cut.FindAll("button").First(button => button.TextContent.Contains("Return to queue", StringComparison.Ordinal)).Click();

        Assert.Equal(("queue-queued", 1, true), Assert.Single(sessionController.PinPartyQuestQueueCalls));
        Assert.Contains(sessionController.SelectPartyQuestQueueCalls, call => call == ("queue-queued", 1));
        Assert.Equal(("queue-selected", 1), Assert.Single(sessionController.SkipPartyQuestQueueCalls));
        Assert.Equal(("queue-selected", 1), Assert.Single(sessionController.RequeuePartyQuestQueueCalls));
    }

    [Fact]
    public void Queue_return_and_expire_actions_call_session_controller()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        var sessionController = new FakeAppSessionController(CreateQueueControlState(
            new[]
            {
                CreateQueueEntry("queue-skipped", PartyQuestQueueStatus.Skipped)
            }));
        Services.AddSingleton<IAppSessionController>(sessionController);

        var cut = RenderQuestsWorkspace();

        cut.FindAll("button").Single(button => button.TextContent.Contains("Return to queue", StringComparison.Ordinal)).Click();
        cut.FindAll("button").Single(button => button.TextContent.Contains("Expire", StringComparison.Ordinal)).Click();

        Assert.Equal(("queue-skipped", 1), Assert.Single(sessionController.RequeuePartyQuestQueueCalls));
        Assert.Equal(("queue-skipped", 1), Assert.Single(sessionController.ExpirePartyQuestQueueCalls));
    }

    [Fact]
    public void Hides_zero_unknown_cron_summary_card()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: null,
                ClassName: "wizard",
                Level: 15,
                UserSnapshot: new UserSnapshot(
                    DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
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
                    new InventorySnapshot(1, 5, 1, 1, 1, 1, Array.Empty<string>())),
                UserFreshness: SnapshotFreshnessState.Fresh,
                PartySnapshot: new PartySnapshot(
                    DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                    "party-123",
                    "Night Owls",
                    "Quest-focused party",
                    2,
                    null,
                    new[]
                    {
                        new PartyMemberSnapshot(
                            "user-1",
                            "Alpha",
                            DateTimeOffset.Parse("2026-04-26T08:15:00Z"),
                            0,
                            0,
                            PartyCronState.CronedToday,
                            "Croned today.",
                            "2026-04-26",
                            DateTimeOffset.Parse("2026-04-26T00:00:00Z"),
                            TimeSpan.Parse("08:15"),
                            1),
                        new PartyMemberSnapshot(
                            "user-2",
                            "Beta",
                            DateTimeOffset.Parse("2026-04-26T09:45:00Z"),
                            0,
                            0,
                            PartyCronState.CronedToday,
                            "Croned today.",
                            "2026-04-26",
                            DateTimeOffset.Parse("2026-04-26T00:00:00Z"),
                            TimeSpan.Parse("09:45"),
                            1)
                    },
                    new PartyCronDashboardSnapshot(
                        CronedCount: 2,
                        VisibleMemberCount: 2,
                        UnknownCount: 0,
                        PossiblyStaleCount: 0,
                        HistoryDayCount: 1,
                        SampleCount: 2,
                        IsLowConfidence: true,
                        SampleSizeWarning: "Early estimate: based on 1 day of CRON history.",
                        AverageBestBuffTime: TimeSpan.Parse("09:45"),
                        SelfFirstBuffTime: TimeSpan.Parse("09:45"),
                        Members: Array.Empty<PartyMemberSnapshot>(),
                        GraphPoints: Array.Empty<PartyCronGraphPoint>())),
                PartyFreshness: SnapshotFreshnessState.Fresh)));

        var cut = Render<PartyPage>();

        Assert.Contains("CRON applied 2/2", cut.Markup);
        Assert.DoesNotContain("Unknown 0", cut.Markup);
        Assert.DoesNotContain("0 possibly stale", cut.Markup);
    }

    [Fact]
    public void Renders_collection_pending_items_in_quest_state_and_member_list()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: null,
                ClassName: "wizard",
                Level: 15,
                UserSnapshot: new UserSnapshot(
                    DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
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
                    new InventorySnapshot(1, 5, 1, 1, 1, 1, Array.Empty<string>())),
                UserFreshness: SnapshotFreshnessState.Fresh,
                PartySnapshot: new PartySnapshot(
                    DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                    "party-123",
                    "Night Owls",
                    "Quest-focused party",
                    2,
                    new PartyQuestSnapshot(
                        "evilsanta",
                        true,
                        9m,
                        0m,
                        2,
                        "Items collected",
                        TotalPendingCollectionItems: 7m),
                    new[]
                    {
                        new PartyMemberSnapshot(
                            "user-1",
                            "Alpha",
                            DateTimeOffset.Parse("2026-04-26T08:15:00Z"),
                            0,
                            0,
                            PartyCronState.CronedToday,
                            "Croned today.",
                            "2026-04-26",
                            DateTimeOffset.Parse("2026-04-26T00:00:00Z"),
                            PendingQuestItems: 3m),
                        new PartyMemberSnapshot(
                            "user-2",
                            "Beta",
                            DateTimeOffset.Parse("2026-04-26T09:45:00Z"),
                            0,
                            0,
                            PartyCronState.CronedToday,
                            "Croned today.",
                            "2026-04-26",
                            DateTimeOffset.Parse("2026-04-26T00:00:00Z"),
                            PendingQuestItems: 4m)
                    }),
                PartyFreshness: SnapshotFreshnessState.Fresh)));

        var cut = Render<PartyPage>();
        var questsCut = RenderQuestsWorkspace();

        Assert.Contains("Pending party items", questsCut.Markup);
        Assert.Contains("7 items", questsCut.Markup);
        Assert.Contains("Pending quest", cut.Markup);
        Assert.Contains("3 items", cut.Markup);
        Assert.Contains("4 items", cut.Markup);
        Assert.DoesNotContain("Pending party damage", questsCut.Markup);
    }

    [Fact]
    public void Renders_party_sync_roles_settings_and_kick_list_for_management()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: null,
                UserId: "owner-id",
                UserSnapshot: CreateSnapshot(),
                UserFreshness: SnapshotFreshnessState.Fresh,
                PartySnapshot: new PartySnapshot(
                    DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                    "party-123",
                    "Night Owls",
                    "Quest-focused party",
                    4,
                    null,
                    new[]
                    {
                        new PartyMemberSnapshot("owner-id", "Mage Tester", null, null, null, PartyCronState.Unknown, "Unknown.", null, null),
                        new PartyMemberSnapshot("admin-id", "Admin", null, null, null, PartyCronState.Unknown, "Unknown.", null, null),
                        new PartyMemberSnapshot("officer-id", "Alpha", null, null, null, PartyCronState.Unknown, "Unknown.", null, null),
                        new PartyMemberSnapshot("kicked-id", "Beta", null, null, null, PartyCronState.Unknown, "Unknown.", null, null),
                    },
                    leaderId: "owner-id"),
                PartyFreshness: SnapshotFreshnessState.Fresh,
                PartyQuestQueue: new PartyQuestQueueSnapshot(
                    DateTimeOffset.Parse("2026-04-26T09:30:00Z"),
                    Array.Empty<PartyQuestPoolEntry>(),
                    Array.Empty<PartyQuestQueueEntry>(),
                    Array.Empty<PartyRecentlyCompletedQuest>(),
                    new PartySyncManagementState(
                        "owner-id",
                        "Mage Tester",
                        new[] { new PartySyncParticipant("admin-id", "Admin") },
                        new[] { new PartySyncOfficer("officer-id", "Alpha", DateTimeOffset.Parse("2026-04-26T09:20:00Z"), "owner-id", "Mage Tester") },
                        new[] { new PartySyncKick("kicked-id", "Beta", DateTimeOffset.Parse("2026-04-26T09:25:00Z"), "owner-id", "Mage Tester", null) },
                        PartySyncSettings.Default,
                        CurrentUserIsOwner: true,
                        CurrentUserIsAdmin: false,
                        CurrentUserIsOfficer: false,
                        CurrentUserCanManageSettings: true,
                        CurrentUserCanManageOfficers: true,
                        CurrentUserCanManageQueue: true,
                        CurrentUserCanModerateMembers: true,
                        CurrentUserIsKicked: false,
                        CurrentUserCanManageProofs: true,
                        InviteProofMode: new PartySyncInviteProofModeState(
                            Enabled: true,
                            AccessStatus: "active-proof",
                            HasActiveProof: true,
                            ActiveProofId: "proof-12345678",
                            InviteProofs: new[]
                            {
                                new PartySyncInviteProofSummary(
                                    "proof-12345678",
                                    "Family devices",
                                    "owner-id",
                                    "Mage Tester",
                                    DateTimeOffset.Parse("2026-04-26T09:25:00Z"),
                                    null,
                                    null,
                                    null,
                                    "active")
                            }))))));

        var cut = Render<PartyPage>();

        Assert.Contains("Owner, admins, and Officers", cut.Markup);
        Assert.Contains("Party sync settings", cut.Markup);
        Assert.Contains("Officer queue changes", cut.Markup);
        Assert.Contains("Lets Officers add, remove, and update shared quest queue entries; enable when trusted Officers help plan quests.", cut.Markup);
        Assert.Contains("Officer moderation", cut.Markup);
        Assert.Contains("Lets Officers remove or restore members in the companion app; enable when trusted Officers help keep the shared queue clean.", cut.Markup);
        Assert.Contains("Limit queue editing", cut.Markup);
        Assert.Contains("Only Officers and the party owner can change the quest queue; enable when regular members should vote but not edit entries.", cut.Markup);
        Assert.Contains("Member auto updates", cut.Markup);
        Assert.Contains("Lets members publish start and completion updates for their own queued quests; enable when quest owners should keep shared status current.", cut.Markup);
        Assert.Contains("Invite proofs", cut.Markup);
        Assert.Contains("Enabled - active proof", cut.Markup);
        Assert.Contains("Family devices", cut.Markup);
        Assert.Contains("active - proof-12345678", cut.Markup);
        Assert.DoesNotContain("officer-only queue edits", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Assign party owner", cut.Markup);
        AssertMarkupOrder(
            cut.Markup,
            "<p class=\"section-label\">Summary</p>",
            "<p class=\"section-label\">Quests</p>",
            "<p class=\"section-label\">Members</p>",
            "<p class=\"section-label\">CRON statistics</p>",
            "<p class=\"section-label\">Party sync roles</p>",
            "<p class=\"section-label\">Party sync settings</p>",
            "<p class=\"section-label\">Party sync moderation</p>");
        Assert.DoesNotContain("Shared quest planning", cut.Markup);
        Assert.Contains("Kicked users", cut.Markup);
        Assert.Contains("Beta", cut.Markup);

        Assert.DoesNotContain("User ID", cut.Find("#party-member-owner-id").TextContent);
        cut.FindAll(".party-management-summary .inline-link-button")
            .Single(button => button.TextContent.Trim() == "Mage Tester")
            .Click();
        Assert.Contains("User ID", cut.Find("#party-member-owner-id").TextContent);

        Assert.DoesNotContain("User ID", cut.Find("#party-member-admin-id").TextContent);
        cut.FindAll(".party-management-summary .inline-link-button")
            .Single(button => button.TextContent.Trim() == "Admin")
            .Click();
        Assert.Contains("User ID", cut.Find("#party-member-admin-id").TextContent);

        Assert.DoesNotContain("User ID", cut.Find("#party-member-officer-id").TextContent);
        cut.FindAll(".party-management-summary .inline-link-button")
            .Single(button => button.TextContent.Trim() == "Alpha")
            .Click();
        Assert.Contains("User ID", cut.Find("#party-member-officer-id").TextContent);

        Assert.DoesNotContain("User ID", cut.Find("#party-member-kicked-id").TextContent);
        cut.FindAll(".party-kick-list-panel .inline-link-button")
            .Single(button => button.TextContent.Trim() == "Beta")
            .Click();
        Assert.Contains("User ID", cut.Find("#party-member-kicked-id").TextContent);
    }

    [Fact]
    public void App_admin_can_assign_party_owner_from_role_control()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        var sessionController = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                DisplayName: "Admin",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: null,
                UserId: "admin-id",
                UserSnapshot: CreateSnapshot(),
                UserFreshness: SnapshotFreshnessState.Fresh,
                PartySnapshot: new PartySnapshot(
                    DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                    "party-123",
                    "Night Owls",
                    "Quest-focused party",
                    3,
                    null,
                    new[]
                    {
                        new PartyMemberSnapshot("owner-id", "Mage Tester", null, null, null, PartyCronState.Unknown, "Unknown.", null, null),
                        new PartyMemberSnapshot("admin-id", "Admin", null, null, null, PartyCronState.Unknown, "Unknown.", null, null),
                        new PartyMemberSnapshot("member-id", "Beta", null, null, null, PartyCronState.Unknown, "Unknown.", null, null),
                    },
                    leaderId: "owner-id"),
                PartyFreshness: SnapshotFreshnessState.Fresh,
                PartyQuestQueue: new PartyQuestQueueSnapshot(
                    DateTimeOffset.Parse("2026-04-26T09:30:00Z"),
                    Array.Empty<PartyQuestPoolEntry>(),
                    Array.Empty<PartyQuestQueueEntry>(),
                    Array.Empty<PartyRecentlyCompletedQuest>(),
                    new PartySyncManagementState(
                        "owner-id",
                        "Mage Tester",
                        new[] { new PartySyncParticipant("admin-id", "Admin") },
                        Array.Empty<PartySyncOfficer>(),
                        Array.Empty<PartySyncKick>(),
                        PartySyncSettings.Default,
                        CurrentUserIsOwner: false,
                        CurrentUserIsAdmin: true,
                        CurrentUserIsOfficer: false,
                        CurrentUserCanManageSettings: true,
                        CurrentUserCanManageOfficers: true,
                        CurrentUserCanManageQueue: true,
                        CurrentUserCanModerateMembers: true,
                        CurrentUserIsKicked: false))));
        Services.AddSingleton<IAppSessionController>(sessionController);

        var cut = Render<PartyPage>();

        cut.Find(".party-management-summary select[aria-label=\"Assign party owner\"]").Change("member-id");

        Assert.Equal(("member-id", "Beta"), sessionController.AssignPartyOwnerCalls.Single());
    }

    [Fact]
    public void Selected_quest_owner_can_start_inactive_selected_quest()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./js/partyPage.js").SetupVoid("scrollToElement", _ => true);
        Services.AddMudServices();
        var sessionController = new FakeAppSessionController(CreateSelectedQuestState("user-id"));
        Services.AddSingleton<IAppSessionController>(sessionController);

        var cut = Render<QuestsPage>();

        Assert.Contains("Quest workspace", cut.Markup);
        Assert.DoesNotContain("Party member status", cut.Markup);
        cut.FindAll("button").Single(button => button.TextContent.Contains("Start quest", StringComparison.Ordinal)).Click();

        Assert.Equal("queue-1", Assert.Single(sessionController.StartSelectedPartyQuestCalls));
    }

    [Fact]
    public void Selected_quest_owner_can_invite_when_no_party_quest_is_active()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./js/partyPage.js").SetupVoid("scrollToElement", _ => true);
        Services.AddMudServices();
        var sessionController = new FakeAppSessionController(CreateSelectedQuestState("user-id", hasPartyQuest: false));
        Services.AddSingleton<IAppSessionController>(sessionController);

        var cut = RenderQuestsWorkspace();

        cut.FindAll("button")
            .Single(button => button.TextContent.Contains("Invite party", StringComparison.Ordinal)
                && !button.HasAttribute("disabled"))
            .Click();

        Assert.Equal(("queue-1", 1), Assert.Single(sessionController.InvitePartyQuestCalls));
    }

    [Fact]
    public void Quest_invite_action_is_disabled_when_party_already_has_active_quest()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./js/partyPage.js").SetupVoid("scrollToElement", _ => true);
        Services.AddMudServices();
        var sessionController = new FakeAppSessionController(CreateSelectedQuestState("user-id", isActive: true));
        Services.AddSingleton<IAppSessionController>(sessionController);

        var cut = RenderQuestsWorkspace();

        var inviteButton = cut.FindAll("button")
            .Single(button => button.TextContent.Contains("Invite party", StringComparison.Ordinal));

        Assert.True(inviteButton.HasAttribute("disabled"));
        Assert.Contains("Resolve the current Habitica quest before inviting another.", cut.Markup);
    }

    [Fact]
    public void Quest_invite_action_is_disabled_with_copy_for_non_owner()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./js/partyPage.js").SetupVoid("scrollToElement", _ => true);
        Services.AddMudServices();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(CreateSelectedQuestState("other-user", hasPartyQuest: false)));

        var nonOwnerCut = RenderQuestsWorkspace();

        var nonOwnerInviteButton = nonOwnerCut.FindAll("button")
            .Single(button => button.TextContent.Contains("Invite party", StringComparison.Ordinal));
        Assert.True(nonOwnerInviteButton.HasAttribute("disabled"));
        Assert.Contains("Only the quest owner can invite.", nonOwnerCut.Markup);
    }

    [Fact]
    public void Quest_invite_action_is_disabled_with_copy_for_stale_party_data()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./js/partyPage.js").SetupVoid("scrollToElement", _ => true);
        Services.AddMudServices();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(
            CreateSelectedQuestState("user-id", hasPartyQuest: false) with
            {
                PartyFreshness = SnapshotFreshnessState.Stale
            }));

        var staleCut = RenderQuestsWorkspace();

        var staleInviteButton = staleCut.FindAll("button")
            .Single(button => button.TextContent.Contains("Invite party", StringComparison.Ordinal));
        Assert.True(staleInviteButton.HasAttribute("disabled"));
        Assert.Contains("Refresh party data before inviting.", staleCut.Markup);
    }

    [Fact]
    public void Selected_quest_party_leader_can_start_inactive_selected_quest()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./js/partyPage.js").SetupVoid("scrollToElement", _ => true);
        Services.AddMudServices();
        var sessionController = new FakeAppSessionController(CreateSelectedQuestState(
            "leader-id",
            ownerUserId: "user-id",
            leaderId: "leader-id"));
        Services.AddSingleton<IAppSessionController>(sessionController);

        var cut = RenderQuestsWorkspace();

        cut.FindAll("button").Single(button => button.TextContent.Contains("Start quest", StringComparison.Ordinal)).Click();

        Assert.Equal("queue-1", Assert.Single(sessionController.StartSelectedPartyQuestCalls));
    }

    [Fact]
    public void Inactive_quest_renders_response_lists_instead_of_progress_estimates()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./js/partyPage.js").SetupVoid("scrollToElement", _ => true);
        Services.AddMudServices();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(CreateSelectedQuestState("user-id")));

        var cut = RenderQuestsWorkspace();

        Assert.Contains("Quest invitation", cut.Markup);
        Assert.Contains("Accepted", cut.Markup);
        Assert.Contains("Mage Tester", cut.Markup);
        Assert.Contains("Pending", cut.Markup);
        Assert.Contains("Beta", cut.Markup);
        Assert.Contains("Rejected", cut.Markup);
        Assert.Contains("Gamma", cut.Markup);
        Assert.DoesNotContain("Current progress", cut.Markup);
        Assert.DoesNotContain("Estimated post-CRON", cut.Markup);
        Assert.DoesNotContain("Expected finish", cut.Markup);
        Assert.All(cut.FindAll(".party-quest-response-list .inline-link-button"), button => Assert.Equal("button", button.GetAttribute("type")));

        var navigation = Services.GetRequiredService<NavigationManager>();
        cut.FindAll(".party-quest-response-list .inline-link-button").First().Click();

        Assert.EndsWith("/party?member=user-id", navigation.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void Pending_quest_invitation_can_be_accepted_or_rejected_from_quests_page()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./js/partyPage.js").SetupVoid("scrollToElement", _ => true);
        Services.AddMudServices();
        var sessionController = new FakeAppSessionController(CreateSelectedQuestState("other-user"));
        Services.AddSingleton<IAppSessionController>(sessionController);

        var cut = RenderQuestsWorkspace();

        Assert.Contains("You have not responded to this quest invitation.", cut.Markup);

        cut.FindAll("button").Single(button => button.TextContent.Contains("Accept", StringComparison.Ordinal)).Click();
        cut.FindAll("button").Single(button => button.TextContent.Contains("Reject", StringComparison.Ordinal)).Click();

        Assert.Equal(1, sessionController.AcceptPartyQuestInvitationCalls);
        Assert.Equal(1, sessionController.RejectPartyQuestInvitationCalls);
    }

    [Fact]
    public void Party_member_query_expands_member_details()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./js/partyPage.js").SetupVoid("scrollToElement", _ => true);
        Services.AddMudServices();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(CreateSelectedQuestState("user-id")));

        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/party?member=user-id");

        var cut = Render<PartyPage>();

        Assert.Contains("User ID", cut.Find("#party-member-user-id").TextContent);
    }

    [Fact]
    public void Manager_can_remove_recently_completed_quest()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        var completedAtUtc = DateTimeOffset.Parse("2026-04-25T09:00:00Z");
        var sessionController = new FakeAppSessionController(CreateQueueControlState(
            Array.Empty<PartyQuestQueueEntry>(),
            new[]
            {
                new PartyRecentlyCompletedQuest(
                    "party-123",
                    "gryphon",
                    "Gryphon Quest",
                    completedAtUtc,
                    null,
                    "user-id",
                    "Quest Owner",
                    3,
                    new[] { "300 XP" })
            }));
        Services.AddSingleton<IAppSessionController>(sessionController);

        var cut = RenderQuestsWorkspace();

        cut.FindAll("button").Single(button => button.TextContent.Contains("Remove", StringComparison.Ordinal)).Click();

        Assert.Equal(("gryphon", completedAtUtc), Assert.Single(sessionController.RemoveRecentlyCompletedQuestCalls));
    }

    [Fact]
    public void Selected_quest_start_action_is_hidden_for_non_owner()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./js/partyPage.js").SetupVoid("scrollToElement", _ => true);
        Services.AddMudServices();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(CreateSelectedQuestState("other-user")));

        var cut = RenderQuestsWorkspace();

        Assert.DoesNotContain("Start quest", cut.Markup);
    }

    [Fact]
    public void Selected_quest_start_action_is_hidden_for_active_quest()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./js/partyPage.js").SetupVoid("scrollToElement", _ => true);
        Services.AddMudServices();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(CreateSelectedQuestState("user-id", isActive: true)));

        var cut = RenderQuestsWorkspace();

        Assert.DoesNotContain("Start quest", cut.Markup);
    }

    [Fact]
    public void Active_quest_without_completion_timing_hides_optional_estimate_fields()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./js/partyPage.js").SetupVoid("scrollToElement", _ => true);
        Services.AddMudServices();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(CreateSelectedQuestState(
            "user-id",
            isActive: true,
            hasMeaningfulCompletionTiming: false)));

        var cut = RenderQuestsWorkspace();

        Assert.Contains("Participants", cut.Markup);
        Assert.Contains(">1</dd>", cut.Markup);
        Assert.Contains("Expected finish", cut.Markup);
        Assert.Contains(">Unknown</dd>", cut.Markup);
        Assert.DoesNotContain("Finishing member", cut.Markup);
        Assert.DoesNotContain("Timing confidence", cut.Markup);
        Assert.DoesNotContain("quest-estimate-alert", cut.Markup);
        Assert.DoesNotContain("<dt>Accepted</dt>", cut.Markup);
        Assert.DoesNotContain("<dt>Pending</dt>", cut.Markup);
        Assert.DoesNotContain("<dt>Rejected</dt>", cut.Markup);
        Assert.DoesNotContain("<dt>Unknown</dt>", cut.Markup);
        Assert.DoesNotContain("<dt>In Inn</dt>", cut.Markup);
        Assert.Contains("<dt>Owner</dt><dd><span>Unavailable</span></dd>", cut.Markup);
        Assert.Contains("<dt>Started</dt><dd>Unavailable</dd>", cut.Markup);
    }

    [Fact]
    public void Active_quest_uses_shared_queue_owner_and_start_time_when_cached()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./js/partyPage.js").SetupVoid("scrollToElement", _ => true);
        Services.AddMudServices();
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(CreateSelectedQuestState(
            "user-id",
            isActive: true,
            queueStatus: PartyQuestQueueStatus.Active,
            startedAtUtc: DateTimeOffset.Parse("2026-04-26T08:00:00Z"))));

        var cut = RenderQuestsWorkspace();

        Assert.Contains("<dt>Owner</dt>", cut.Markup);
        Assert.Contains("Mage Tester", cut.Markup);
        Assert.Contains("<dt>Started</dt>", cut.Markup);
        Assert.DoesNotContain("<dt>Started</dt><dd>Unavailable</dd>", cut.Markup);
    }

    [Fact]
    public void Selected_quest_start_failure_renders_inline_error()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./js/partyPage.js").SetupVoid("scrollToElement", _ => true);
        Services.AddMudServices();
        var sessionController = new FakeAppSessionController(CreateSelectedQuestState("user-id"))
        {
            StartSelectedPartyQuestResult = PartyQuestActionResult.Failure("Habitica rejected the quest start.")
        };
        Services.AddSingleton<IAppSessionController>(sessionController);

        var cut = RenderQuestsWorkspace();

        cut.FindAll("button").Single(button => button.TextContent.Contains("Start quest", StringComparison.Ordinal)).Click();

        Assert.Contains("Habitica rejected the quest start.", cut.Markup);
    }

    private static void AssertMarkupOrder(string markup, params string[] labels)
    {
        var previousIndex = -1;

        foreach (var label in labels)
        {
            var index = markup.IndexOf(label, StringComparison.Ordinal);
            Assert.True(index > previousIndex, $"{label} should render after the previous section.");
            previousIndex = index;
        }
    }

    private static SessionViewModel CreateSelectedQuestState(
        string currentUserId,
        bool isActive = false,
        string ownerUserId = "user-id",
        string leaderId = "user-id",
        bool hasPartyQuest = true,
        bool hasMeaningfulCompletionTiming = true,
        PartyQuestQueueStatus queueStatus = PartyQuestQueueStatus.Selected,
        DateTimeOffset? startedAtUtc = null)
    {
        return new SessionViewModel(
            IsBusy: false,
            IsAuthenticated: true,
            DisplayName: "Mage Tester",
            ErrorMessage: null,
            LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
            TaskFreshness: SnapshotFreshnessState.Fresh,
            TaskSnapshot: null,
            UserId: currentUserId,
            UserSnapshot: CreateSnapshot(),
            UserFreshness: SnapshotFreshnessState.Fresh,
            PartySnapshot: new PartySnapshot(
                DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                "party-123",
                "Night Owls",
                "Quest-focused party",
                3,
                hasPartyQuest
                    ? new PartyQuestSnapshot(
                        "dragon",
                        isActive,
                        0m,
                        0m,
                        1,
                        AppliedProgress: new PartyQuestMetricSnapshot("Current boss HP", 50m, 100m, "hp"),
                        EstimatedPostCronProgress: new PartyQuestMetricSnapshot("Estimated boss HP after CRON", 25m, 100m, "hp"),
                        ParticipationSummary: new PartyQuestParticipationSummary(1, 1, 1, 0, 0),
                        CompletionEstimate: hasMeaningfulCompletionTiming
                            ? new PartyQuestCompletionEstimate(
                                true,
                                DateTimeOffset.Parse("2026-04-26T10:15:00Z"),
                                DateTimeOffset.Parse("2026-04-26T10:15:00Z"),
                                PartyQuestEstimateConfidence.High,
                                "Expected to finish when Mage Tester checks in around Apr 26, 10:15.",
                                "Mage Tester",
                                "user-id")
                            : new PartyQuestCompletionEstimate(
                                false,
                                null,
                                null,
                                PartyQuestEstimateConfidence.Medium,
                                "Completion timing is unavailable."),
                        Name: "Dragon")
                    : null,
                new[]
                {
                    new PartyMemberSnapshot("user-id", "Mage Tester", null, null, null, PartyCronState.Unknown, "Unknown.", null, null, ParticipationStatus: PartyQuestParticipationStatus.Accepted),
                    new PartyMemberSnapshot("other-user", "Beta", null, null, null, PartyCronState.Unknown, "Unknown.", null, null, ParticipationStatus: PartyQuestParticipationStatus.Pending),
                    new PartyMemberSnapshot("rejected-user", "Gamma", null, null, null, PartyCronState.Unknown, "Unknown.", null, null, ParticipationStatus: PartyQuestParticipationStatus.Rejected)
                },
                leaderId: leaderId),
            PartyFreshness: SnapshotFreshnessState.Fresh,
            PartyQuestQueue: new PartyQuestQueueSnapshot(
                DateTimeOffset.Parse("2026-04-26T09:30:00Z"),
                Array.Empty<PartyQuestPoolEntry>(),
                new[]
                {
                    new PartyQuestQueueEntry(
                        "queue-1",
                        "party-123",
                        "dragon",
                        "Dragon",
                        ownerUserId,
                        "Mage Tester",
                        queueStatus,
                        DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
                        DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                        1,
                        null,
                        true,
                        1,
                        Array.Empty<PartyQuestVote>(),
                        StartedAtUtc: startedAtUtc)
                },
                Array.Empty<PartyRecentlyCompletedQuest>()));
    }

    private static SessionViewModel CreateQueueControlState(
        IReadOnlyList<PartyQuestQueueEntry> queueEntries,
        IReadOnlyList<PartyRecentlyCompletedQuest>? recentlyCompleted = null)
    {
        return new SessionViewModel(
            IsBusy: false,
            IsAuthenticated: true,
            DisplayName: "Mage Tester",
            ErrorMessage: null,
            LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
            TaskFreshness: SnapshotFreshnessState.Fresh,
            TaskSnapshot: null,
            UserId: "admin-id",
            UserSnapshot: CreateSnapshot(),
            UserFreshness: SnapshotFreshnessState.Fresh,
            PartySnapshot: new PartySnapshot(
                DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                "party-123",
                "Night Owls",
                "Quest-focused party",
                2,
                null,
                new[]
                {
                    new PartyMemberSnapshot("admin-id", "Mage Tester", null, null, null, PartyCronState.Unknown, "Unknown.", null, null),
                    new PartyMemberSnapshot("user-id", "Quest Owner", null, null, null, PartyCronState.Unknown, "Unknown.", null, null)
                },
                leaderId: "owner-id"),
            PartyFreshness: SnapshotFreshnessState.Fresh,
            PartyQuestQueue: new PartyQuestQueueSnapshot(
                DateTimeOffset.Parse("2026-04-26T09:30:00Z"),
                new[]
                {
                    new PartyQuestPoolEntry(
                        "party-123",
                        "moonstone",
                        "Moonstone Chain",
                        "admin-id",
                        "Mage Tester",
                        1,
                        DateTimeOffset.Parse("2026-04-26T09:30:00Z"),
                        "Collection",
                        new[] { "450 Gold" })
                },
                queueEntries,
                recentlyCompleted ?? Array.Empty<PartyRecentlyCompletedQuest>(),
                new PartySyncManagementState(
                    "owner-id",
                    "Owner",
                    new[] { new PartySyncParticipant("admin-id", "Mage Tester") },
                    Array.Empty<PartySyncOfficer>(),
                    Array.Empty<PartySyncKick>(),
                    PartySyncSettings.Default,
                    CurrentUserIsOwner: false,
                    CurrentUserIsAdmin: true,
                    CurrentUserIsOfficer: false,
                    CurrentUserCanManageSettings: true,
                    CurrentUserCanManageOfficers: true,
                    CurrentUserCanManageQueue: true,
                    CurrentUserCanModerateMembers: true,
                    CurrentUserIsKicked: false)));
    }

    private static PartyQuestQueueEntry CreateQueueEntry(
        string queueItemId,
        PartyQuestQueueStatus status,
        DateTimeOffset? expiresAtUtc = null,
        string questName = "Moonstone Chain")
    {
        return new PartyQuestQueueEntry(
            queueItemId,
            "party-123",
            "moonstone",
            questName,
            "user-id",
            "Quest Owner",
            status,
            DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
            DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
            1,
            null,
            false,
            1,
            Array.Empty<PartyQuestVote>(),
            new[] { "450 Gold" },
            SelectedAtUtc: status is PartyQuestQueueStatus.Selected or PartyQuestQueueStatus.InviteSent ? DateTimeOffset.Parse("2026-04-26T09:00:00Z") : null,
            ExpiresAtUtc: expiresAtUtc);
    }

    private static UserSnapshot CreateSnapshot()
    {
        return new UserSnapshot(
            DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
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
            new InventorySnapshot(1, 5, 1, 1, 1, 1, Array.Empty<string>()));
    }

    private static string[] GetRenderedMemberNames(IRenderedComponent<PartyPage> cut)
    {
        return cut.FindAll(".party-member-card .party-member-identity strong")
            .Select(element => element.TextContent)
            .ToArray();
    }

    private IRenderedComponent<PartyPage> RenderQuestsWorkspace()
    {
        return Render<PartyPage>(parameters => parameters.Add(component => component.QuestWorkspaceOnly, true));
    }

    private static string[] GetRenderedQuestPoolNames(IRenderedComponent<PartyPage> cut)
    {
        return cut.FindAll("[data-testid='quest-pool-grid'] .habitica-quest-identity strong")
            .Select(element => element.TextContent)
            .ToArray();
    }

    private static void SetQuestPoolSearch(IRenderedComponent<PartyPage> cut, string searchText)
    {
        cut.Find("[data-testid='quest-pool-search']").Input(searchText);
    }
}
