using Bunit;
using Habitica.Domain.Party;
using Habitica.Domain.Sync;
using Habitica.Domain.User;
using Habitica.WebApp.Pages;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Pages;

public sealed class PartyPageTests : BunitContext
{
    [Fact]
    public void Renders_cached_party_summary_and_quest_state()
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
                        RewardSummary: new[] { "10 Gold", "100 XP", "Sea Serpent Egg" }),
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
                            PendingQuestDamage: 7.2m),
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
                            PendingQuestDamage: 5.3m),
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
                            Stats: new PartyMemberStatBreakdownSnapshot(
                                null,
                                null,
                                null,
                                new PartyStatSectionSnapshot(12m, 34m, 18m, 21m)))
                        },
                        GraphPoints: new[]
                        {
                            new PartyCronGraphPoint(8, 1, 1m, 0m, 2m),
                            new PartyCronGraphPoint(9, 2, 2m, 1m, 3m)
                        })),
                PartyFreshness: SnapshotFreshnessState.Fresh)));

        var cut = Render<PartyPage>();

        Assert.Contains("Party overview", cut.Markup);
        Assert.Contains("Night Owls", cut.Markup);
        Assert.Contains("Quest-focused party", cut.Markup);
        Assert.Contains("seaserpent", cut.Markup);
        Assert.Contains("Pending party progress", cut.Markup);
        Assert.Contains("42.75 damage", cut.Markup);
        Assert.Contains("Current boss HP", cut.Markup);
        Assert.Contains("875.25/1000 hp", cut.Markup);
        Assert.Contains("Estimated boss HP after CRON", cut.Markup);
        Assert.Contains("832.5/1000 hp", cut.Markup);
        Assert.DoesNotContain("Damage taken", cut.Markup);
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
        Assert.Contains("Pending quest", cut.Markup);
        Assert.Contains("7.2 damage", cut.Markup);
        Assert.Contains("5.3 damage", cut.Markup);
        Assert.Contains("Avg 08:15 (1 day)", cut.Markup);
        Assert.Contains("Not enough history", cut.Markup);
        Assert.Contains("Sea Serpent Egg", cut.Markup);
        Assert.DoesNotContain("Reward details are not available yet.", cut.Markup);
        Assert.Contains("CRON statistics", cut.Markup);
        Assert.Contains("Historical average", cut.Markup);
        Assert.Contains("1 stored observation day", cut.Markup);
        Assert.Contains("cron-band-path", cut.Markup);
        Assert.Contains("cron-today-path", cut.Markup);
        Assert.Contains("cron-average-path", cut.Markup);
        Assert.Contains("text-anchor=\"end\"", cut.Markup);
        Assert.Contains("Members", cut.Markup);
        Assert.Contains("Details", cut.Markup);
        Assert.DoesNotContain("Member ID", cut.Markup);
        Assert.DoesNotContain("user-1", cut.Markup);
        Assert.DoesNotContain("STR 12", cut.Markup);
        Assert.DoesNotContain("Day start", cut.Markup);
        Assert.DoesNotContain("Habitica public member data hides day start/timezone", cut.Markup);

        cut.FindAll("[data-testid='member-details']").First().Click();

        Assert.DoesNotContain("Member ID", cut.Markup);
        Assert.DoesNotContain("user-1", cut.Markup);
        Assert.Contains("Total", cut.Markup);
        Assert.Contains("STR 12", cut.Markup);
    }

    [Fact]
    public void Renders_party_description_markdown_without_raw_html()
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
                PartyFreshness: SnapshotFreshnessState.Fresh)));

        var cut = Render<PartyPage>();

        Assert.Contains("Line one", cut.Markup);
        Assert.Contains("<br", cut.Markup);
        Assert.Contains("<strong>Bold move</strong>", cut.Markup);
        Assert.Contains("<a href=\"https://example.com/\"", cut.Markup);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", cut.Markup);
        Assert.DoesNotContain("<script>", cut.Markup);
    }

    [Fact]
    public void Renders_shared_quest_queue_pool_and_recent_history()
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
                            new[] { "450 Gold", "Wolf Cub" })
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
                            new[] { "300 XP" })
                    }))));

        var cut = Render<PartyPage>();

        Assert.Contains("Shared quest planning", cut.Markup);
        Assert.Contains("Moonstone Chain", cut.Markup);
        Assert.Contains("1 vote", cut.Markup);
        Assert.Contains("Alpha", cut.Markup);
        Assert.Contains("450 Gold", cut.Markup);
        Assert.Contains("Wolf Cub", cut.Markup);
        Assert.Contains("Gryphon Quest", cut.Markup);
        Assert.Contains("300 XP", cut.Markup);
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

        Assert.Contains("Pending party items", cut.Markup);
        Assert.Contains("7 items", cut.Markup);
        Assert.Contains("Pending quest", cut.Markup);
        Assert.Contains("3 items", cut.Markup);
        Assert.Contains("4 items", cut.Markup);
        Assert.DoesNotContain("Pending party damage", cut.Markup);
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
}
