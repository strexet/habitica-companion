using Bunit;
using Habitica.Domain.Party;
using Habitica.Domain.Sync;
using Habitica.Domain.Tasks;
using Habitica.Domain.User;
using Habitica.Rules.Spells;
using Habitica.Storage;
using Habitica.WebApp.Pages;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using System.Text.Json;

namespace Habitica.WebApp.Tests.Pages;

public sealed class SpellsPageTests : BunitContext
{
    [Fact]
    public void Signed_out_empty_spells_has_sign_in_action()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new SpellViewModelFactory());
        Services.AddSingleton<IKeyValueStorage>(new FakeKeyValueStorage());
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(SessionViewModel.Empty));

        var cut = Render<SpellsPage>();

        Assert.Contains("No saved account data is available on this device yet.", cut.Markup);
        Assert.Contains("href=\"/sign-in\"", cut.Markup);
        Assert.Contains("empty-state-actions", cut.Markup);
        Assert.DoesNotContain("Sign in or refresh", cut.Markup);
    }

    [Fact]
    public void Renders_current_class_spells_default_target_values_and_equipment_recommendations()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new SpellViewModelFactory());
        Services.AddSingleton<IKeyValueStorage>(new FakeKeyValueStorage());
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(CreateState()));

        var cut = Render<SpellsPage>();

        Assert.Contains("Spells", cut.Markup);
        Assert.Contains("MP", cut.Markup);
        Assert.NotNull(cut.Find("[data-testid='spells-mana-bar']"));
        Assert.Contains("33.5 / 40 MP", cut.Markup);
        Assert.Contains("Available mana", cut.Markup);
        Assert.Contains("Max 40 MP", cut.Markup);
        Assert.Contains("Burst of Flames", cut.Markup);
        Assert.Contains("shop_fireball.png", cut.Markup);
        Assert.Contains("Ethereal Surge", cut.Markup);
        Assert.Contains("Bluest todo", cut.Markup);
        Assert.Contains("Checked daily", cut.Markup);
        Assert.DoesNotContain("Finished todo", cut.Markup);
        Assert.Contains("value 18", cut.Markup);
        Assert.Contains("Equipment recommendations", cut.Markup);
        Assert.Contains("Auto equip", cut.Markup);
        Assert.Contains("Maximize INT", cut.Markup);
        Assert.Contains("shop_weapon_int.png", cut.Markup);
        Assert.Contains("Equipped", cut.Markup);
        Assert.NotNull(cut.Find("[data-testid='spell-stat-context-fireball']"));
        Assert.NotNull(cut.Find("[data-testid='spell-stat-context-mpheal']"));
        Assert.NotNull(cut.Find("[data-testid='spell-stat-context-earth']"));
        Assert.Empty(cut.FindAll("[data-testid='spell-stat-context-frost']"));
    }

    [Fact]
    public void Hides_stat_point_context_until_allocation_unlocks()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new SpellViewModelFactory());
        Services.AddSingleton<IKeyValueStorage>(new FakeKeyValueStorage());
        var state = CreateState() with
        {
            Level = 9,
            UserSnapshot = CreateState().UserSnapshot! with
            {
                Level = 9,
                UnallocatedStatPoints = 3
            }
        };
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(state));

        var cut = Render<SpellsPage>();

        Assert.DoesNotContain("Stat points", cut.Markup);
        Assert.DoesNotContain("Allocate on Dashboard", cut.Markup);
    }

    [Fact]
    public void Boss_quest_context_renders_only_on_damaging_spell_cards()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new SpellViewModelFactory());
        Services.AddSingleton<IKeyValueStorage>(new FakeKeyValueStorage());
        var state = CreateState() with
        {
            PartySnapshot = new PartySnapshot(
                DateTimeOffset.Parse("2026-04-30T06:00:00Z"),
                "party-1",
                "Night Owls",
                null,
                2,
                new PartyQuestSnapshot(
                    "dragon",
                    true,
                    0m,
                    0m,
                    2,
                    BossHealthRemaining: 875m,
                    BossHealthTotal: 1000m,
                    TotalPendingDamage: 42.75m,
                    QuestType: PartyQuestType.Boss,
                    Name: "Dragon Quest"),
                Array.Empty<PartyMemberSnapshot>()),
        };
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(state));

        var cut = Render<SpellsPage>();

        Assert.NotNull(cut.Find("[data-testid='spell-boss-context-fireball']"));
        Assert.Empty(cut.FindAll("[data-testid='spell-boss-context-mpheal']"));
        Assert.Empty(cut.FindAll("[data-testid='spell-boss-context-earth']"));
        Assert.Empty(cut.FindAll("[data-testid='spell-boss-context-frost']"));
        Assert.Contains("Dragon Quest: 875/1000 hp", cut.Markup);
        Assert.Contains("Party pending: 42.75 damage", cut.Markup);
        Assert.DoesNotContain("Your pending", cut.Markup);
    }

    [Fact]
    public void Count_updates_total_mana_and_progress_bar_renders_for_active_cast()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new SpellViewModelFactory());
        Services.AddSingleton<IKeyValueStorage>(new FakeKeyValueStorage());
        var controller = new FakeAppSessionController(CreateState() with
        {
            ActiveSpellCastProgress = new SpellCastProgress("fireball", 2, 5)
        });
        Services.AddSingleton<IAppSessionController>(controller);

        var cut = Render<SpellsPage>();

        var countInput = cut.Find("[data-testid='spell-count-fireball']");
        countInput.Change("3");

        Assert.Contains("30 MP", cut.Markup);
        Assert.Contains("After cast", cut.Markup);
        Assert.Contains("3.5 MP", cut.Markup);
        Assert.Contains("Casting 2 of 5", cut.Markup);
        Assert.Contains("mud-progress-linear", cut.Markup);
        Assert.Contains("scheme-progress-primary", cut.Markup);
    }

    [Fact]
    public void Spend_all_mana_sets_max_affordable_count_and_does_not_cast()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new SpellViewModelFactory());
        Services.AddSingleton<IKeyValueStorage>(new FakeKeyValueStorage());
        var controller = new FakeAppSessionController(CreateState());
        Services.AddSingleton<IAppSessionController>(controller);

        var cut = Render<SpellsPage>();

        cut.Find("[data-testid='spend-all-mana-fireball']").Click();

        Assert.Equal("3", cut.Find("[data-testid='spell-count-fireball']").GetAttribute("value"));
        Assert.Contains("30 MP", cut.Markup);
        Assert.Contains("3.5 MP", cut.Markup);
        Assert.Empty(controller.CastSpellCalls);
    }

    [Fact]
    public void Spend_all_mana_is_disabled_when_spell_is_locked()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new SpellViewModelFactory());
        Services.AddSingleton<IKeyValueStorage>(new FakeKeyValueStorage());
        var lockedState = CreateState() with
        {
            Level = 10,
            UserSnapshot = CreateState().UserSnapshot! with
            {
                Level = 10
            }
        };
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(lockedState));

        var lockedCut = Render<SpellsPage>();

        Assert.True(lockedCut.Find("[data-testid='spend-all-mana-fireball']").HasAttribute("disabled"));
    }

    [Fact]
    public void Spend_all_mana_is_disabled_when_snapshot_is_stale()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new SpellViewModelFactory());
        Services.AddSingleton<IKeyValueStorage>(new FakeKeyValueStorage());
        var staleState = CreateState() with
        {
            UserFreshness = SnapshotFreshnessState.Stale
        };
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(staleState));

        var staleCut = Render<SpellsPage>();

        Assert.True(staleCut.Find("[data-testid='spend-all-mana-fireball']").HasAttribute("disabled"));
    }

    [Fact]
    public void Spend_all_mana_is_disabled_when_spell_is_unaffordable()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new SpellViewModelFactory());
        Services.AddSingleton<IKeyValueStorage>(new FakeKeyValueStorage());
        var unaffordableState = CreateState() with
        {
            UserSnapshot = CreateState().UserSnapshot! with
            {
                Mana = 5m
            }
        };
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(unaffordableState));

        var unaffordableCut = Render<SpellsPage>();

        Assert.True(unaffordableCut.Find("[data-testid='spend-all-mana-fireball']").HasAttribute("disabled"));
    }

    [Fact]
    public void Active_cast_shows_preparing_label_and_cancel_only_on_active_card()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new SpellViewModelFactory());
        Services.AddSingleton<IKeyValueStorage>(new FakeKeyValueStorage());
        var controller = new FakeAppSessionController(CreateState() with
        {
            IsBusy = true,
            ActiveSpellCastProgress = new SpellCastProgress("fireball", 0, 3, "Preparing...")
        });
        Services.AddSingleton<IAppSessionController>(controller);

        var cut = Render<SpellsPage>();

        Assert.Contains("Preparing...", cut.Markup);
        Assert.Single(cut.FindAll("[data-testid='cancel-spell-cast-fireball']"));
        Assert.Empty(cut.FindAll("[data-testid='cancel-spell-cast-frost']"));

        cut.Find("[data-testid='cancel-spell-cast-fireball']").Click();

        Assert.Equal(1, controller.CancelActiveSpellCastCalls);
    }

    [Fact]
    public void Cast_button_calls_session_controller_with_selected_target_and_count()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new SpellViewModelFactory());
        Services.AddSingleton<IKeyValueStorage>(new FakeKeyValueStorage());
        var controller = new FakeAppSessionController(CreateState());
        Services.AddSingleton<IAppSessionController>(controller);

        var cut = Render<SpellsPage>();

        cut.Find("[data-testid='spell-count-fireball']").Change("2");
        cut.Find("[data-testid='cast-spell-fireball']").Click();

        var request = controller.CastSpellCalls.Single();
        Assert.Equal("fireball", request.SpellId);
        Assert.Equal("todo-blue", request.TargetTaskId);
        Assert.Equal(2, request.Count);
        Assert.True(request.AutoEquipRecommendedGear);
        Assert.Equal("head_int", request.AutoEquipGearSlots!.Head);
        Assert.Equal("weapon_int", request.AutoEquipGearSlots.Weapon);
    }

    [Fact]
    public void Auto_equip_can_be_disabled_before_casting()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new SpellViewModelFactory());
        Services.AddSingleton<IKeyValueStorage>(new FakeKeyValueStorage());
        var controller = new FakeAppSessionController(CreateState());
        Services.AddSingleton<IAppSessionController>(controller);

        var cut = Render<SpellsPage>();

        cut.Find("[data-testid='spell-auto-equip-fireball']").Change(false);
        cut.Find("[data-testid='cast-spell-fireball']").Click();

        var request = controller.CastSpellCalls.Single();
        Assert.False(request.AutoEquipRecommendedGear);
        Assert.Null(request.AutoEquipGearSlots);
    }

    [Fact]
    public void Auto_equip_defaults_to_highest_value_recommendation()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new SpellViewModelFactory());
        Services.AddSingleton<IKeyValueStorage>(new FakeKeyValueStorage());
        var controller = new FakeAppSessionController(CreateHealerStateForRecommendationOrdering());
        Services.AddSingleton<IAppSessionController>(controller);

        var cut = Render<SpellsPage>();

        var options = cut.FindAll("[data-testid='spell-recommendation-selector-healAll'] option")
            .Select(static option => option.TextContent.Trim())
            .ToArray();
        Assert.Equal(
            new[] { "Balanced CON/INT", "Maximize CON", "Maximize INT" },
            options);

        cut.Find("[data-testid='cast-spell-healAll']").Click();

        var request = Assert.Single(controller.CastSpellCalls);
        Assert.True(request.AutoEquipRecommendedGear);
        Assert.Equal("head_balanced", request.AutoEquipGearSlots?.Head);
    }

    [Fact]
    public void Auto_equip_selection_change_updates_equip_plan()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new SpellViewModelFactory());
        Services.AddSingleton<IKeyValueStorage>(new FakeKeyValueStorage());
        var controller = new FakeAppSessionController(CreateHealerStateForRecommendationOrdering());
        Services.AddSingleton<IAppSessionController>(controller);

        var cut = Render<SpellsPage>();

        cut.Find("[data-testid='spell-recommendation-selector-healAll']").Change("Maximize CON");
        cut.Find("[data-testid='cast-spell-healAll']").Click();

        var request = Assert.Single(controller.CastSpellCalls);
        Assert.True(request.AutoEquipRecommendedGear);
        Assert.Equal("head_con", request.AutoEquipGearSlots?.Head);
    }

    [Fact]
    public void Single_auto_equip_option_does_not_render_selector()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new SpellViewModelFactory());
        Services.AddSingleton<IKeyValueStorage>(new FakeKeyValueStorage());
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(CreateRogueStateForToolsOfTrade()));

        var cut = Render<SpellsPage>();

        Assert.Empty(cut.FindAll("[data-testid='spell-recommendation-selector-toolsOfTrade']"));
    }

    [Fact]
    public void Effect_preview_uses_selected_auto_equip_recommendation_when_enabled()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new SpellViewModelFactory());
        Services.AddSingleton<IKeyValueStorage>(new FakeKeyValueStorage());
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(CreateRogueStateForToolsOfTrade()));

        var cut = Render<SpellsPage>();

        Assert.Contains("Tools of the Trade", cut.Markup);
        Assert.Contains("Adds approximately 28 PER to each party member.", cut.Markup);
        Assert.DoesNotContain("Adds approximately 13 PER to each party member.", cut.Markup);
        Assert.DoesNotContain("Adds approximately 0 PER to each party member.", cut.Markup);

        cut.Find("[data-testid='spell-auto-equip-toolsOfTrade']").Change(false);

        Assert.Contains("Adds approximately 13 PER to each party member.", cut.Markup);
        Assert.DoesNotContain("Adds approximately 0 PER to each party member.", cut.Markup);
    }

    [Fact]
    public void Manual_dynamic_recommendation_equip_uses_progress_aware_slot_operation()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new SpellViewModelFactory());
        Services.AddSingleton<IKeyValueStorage>(new FakeKeyValueStorage());
        var controller = new FakeAppSessionController(CreateState());
        Services.AddSingleton<IAppSessionController>(controller);

        var cut = Render<SpellsPage>();

        cut.Find("[data-testid='equip-spell-recommendation-fireball-Maximize PER']").Click();

        var call = controller.EquipGearSlotsCalls.Single();
        Assert.Equal(EquipmentSetKind.Battle, call.Kind);
        Assert.Equal("spell:fireball:Maximize PER", call.OperationId);
        Assert.Equal("head_per", call.Slots.Head);
    }

    [Fact]
    public void Cron_sensitive_buff_prompts_before_casting_when_habitica_day_is_not_started()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new SpellViewModelFactory());
        Services.AddSingleton<IKeyValueStorage>(new FakeKeyValueStorage());
        var controller = new FakeAppSessionController(CreateRogueStateForToolsOfTrade() with
        {
            UserId = "user-id",
            TaskSnapshot = new TaskCollectionSnapshot(
                DateTimeOffset.Parse("2026-04-30T06:00:00Z"),
                new[]
                {
                    new TaskSnapshot("daily-1", "Exercise", TaskType.Daily, false, 1m, null, null, IsDue: true),
                    new TaskSnapshot("daily-weekly", "Weekly review", TaskType.Daily, false, 1m, null, null, IsDue: false)
                }),
            UserSnapshot = CreateRogueStateForToolsOfTrade().UserSnapshot! with
            {
                NeedsCron = true,
                CurrentHabiticaDayKey = "2026-04-30"
            }
        });
        Services.AddSingleton<IAppSessionController>(controller);

        var cut = Render<SpellsPage>();

        cut.Find("[data-testid='cast-spell-toolsOfTrade']").Click();

        Assert.Contains("Buff timing", cut.Markup);
        Assert.Contains("Party buffs expire separately for each member", cut.Markup);
        var cronDailies = cut.Find("[data-testid='cron-unfinished-dailies']");
        Assert.Contains("1 daily due", cronDailies.TextContent);
        Assert.DoesNotContain("Weekly review", cronDailies.TextContent);
        Assert.Empty(cut.FindAll("[data-testid='complete-cron-daily-daily-1']"));
        Assert.Empty(controller.CastSpellCalls);

        cut.Find("[data-testid='cron-dailies-disclosure']").Click();
        Assert.DoesNotContain("Weekly review", cut.Find("[data-testid='cron-unfinished-dailies']").TextContent);
        cut.Find("[data-testid='complete-cron-daily-daily-1']").Click();
        Assert.Equal("daily-1", Assert.Single(controller.ScoreTaskCalls).TaskId);
        Assert.Empty(cut.FindAll("[data-testid='cron-unfinished-dailies']"));

        cut.Find("[data-testid='start-day-and-cast-toolsOfTrade']").Click();

        Assert.Equal(1, controller.StartNewDayCalls);
        Assert.Single(controller.CastSpellCalls);
    }

    private static SessionViewModel CreateState()
    {
        return new SessionViewModel(
            IsBusy: false,
            IsAuthenticated: true,
            DisplayName: "Mage Tester",
            ErrorMessage: null,
            LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-30T06:00:00Z"),
            TaskFreshness: SnapshotFreshnessState.Fresh,
            TaskSnapshot: new TaskCollectionSnapshot(
                DateTimeOffset.Parse("2026-04-30T06:00:00Z"),
                new[]
                {
                    new TaskSnapshot("todo-blue", "Bluest todo", TaskType.Todo, false, 1m, null, null, 18m),
                    new TaskSnapshot("habit-red", "Red habit", TaskType.Habit, false, 9m, null, null, -2m),
                    new TaskSnapshot("daily-checked", "Checked daily", TaskType.Daily, true, 1m, null, null, 4m),
                    new TaskSnapshot("todo-finished", "Finished todo", TaskType.Todo, true, 1m, null, null, 60m)
                }),
            ClassName: "wizard",
            Level: 15,
            UserSnapshot: new UserSnapshot(
                DateTimeOffset.Parse("2026-04-30T06:00:00Z"),
                "Mage Tester",
                "wizard",
                15,
                50m,
                50m,
                33.5m,
                40m,
                0m,
                100m,
                10m,
                "party-1",
                null,
                null,
                new EquipmentSnapshot(
                    new GearSlotsSnapshot("head_int", null, "weapon_int", null, null),
                    new GearSlotsSnapshot(null, null, null, null, null)),
                new InventorySnapshot(0, 0, 0, 0, 0, 0, new[] { "head_int", "head_per", "weapon_int", "weapon_balanced" }),
                UnallocatedStatPoints: 3,
                Stats: new CharacterStatsSnapshot(12m, 34m, 18m, 21m),
                Buffs: CharacterStatsSnapshot.Zero,
                BuffFlags: BuffFlagsSnapshot.Empty),
            UserFreshness: SnapshotFreshnessState.Fresh,
            GearCatalogSnapshot: new GearCatalogSnapshot(
                DateTimeOffset.Parse("2026-04-30T06:00:00Z"),
                new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
                {
                    ["head_int"] = new("head_int", "Int Hood", "Head", "wizard", null, new GearStatBlock(0m, 8m, 0m, 0m)),
                    ["head_per"] = new("head_per", "Per Hood", "Head", "wizard", null, new GearStatBlock(0m, 0m, 0m, 8m)),
                    ["weapon_int"] = new("weapon_int", "Int Wand", "Weapon", "wizard", null, new GearStatBlock(0m, 10m, 0m, 0m)),
                    ["weapon_balanced"] = new("weapon_balanced", "Balanced Wand", "Weapon", "wizard", null, new GearStatBlock(0m, 6m, 0m, 6m))
                }));
    }

    private static SessionViewModel CreateRogueStateForToolsOfTrade()
    {
        return new SessionViewModel(
            IsBusy: false,
            IsAuthenticated: true,
            DisplayName: "Rogue Tester",
            ErrorMessage: null,
            LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-30T06:00:00Z"),
            TaskFreshness: SnapshotFreshnessState.Fresh,
            TaskSnapshot: null,
            ClassName: "rogue",
            Level: 15,
            UserSnapshot: new UserSnapshot(
                DateTimeOffset.Parse("2026-04-30T06:00:00Z"),
                "Rogue Tester",
                "rogue",
                15,
                50m,
                50m,
                40m,
                50m,
                0m,
                100m,
                10m,
                "party-1",
                null,
                null,
                new EquipmentSnapshot(
                    new GearSlotsSnapshot(null, null, null, null, null),
                    new GearSlotsSnapshot(null, null, null, null, null)),
                new InventorySnapshot(0, 0, 0, 0, 0, 0, new[] { "head_per" }),
                UnallocatedStatPoints: 0,
                Stats: CharacterStatsSnapshot.Zero,
                Buffs: CharacterStatsSnapshot.Zero,
                BuffFlags: BuffFlagsSnapshot.Empty),
            UserFreshness: SnapshotFreshnessState.Fresh,
            GearCatalogSnapshot: new GearCatalogSnapshot(
                DateTimeOffset.Parse("2026-04-30T06:00:00Z"),
                new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
                {
                    ["head_per"] = new("head_per", "Per Hood", "Head", "rogue", null, new GearStatBlock(0m, 0m, 0m, 8m))
                }));
    }

    private static SessionViewModel CreateHealerStateForRecommendationOrdering()
    {
        return new SessionViewModel(
            IsBusy: false,
            IsAuthenticated: true,
            DisplayName: "Healer Tester",
            ErrorMessage: null,
            LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-30T06:00:00Z"),
            TaskFreshness: SnapshotFreshnessState.Fresh,
            TaskSnapshot: null,
            ClassName: "healer",
            Level: 15,
            UserSnapshot: new UserSnapshot(
                DateTimeOffset.Parse("2026-04-30T06:00:00Z"),
                "Healer Tester",
                "healer",
                15,
                50m,
                50m,
                40m,
                50m,
                0m,
                100m,
                10m,
                "party-1",
                null,
                null,
                new EquipmentSnapshot(
                    new GearSlotsSnapshot(null, null, null, null, null),
                    new GearSlotsSnapshot(null, null, null, null, null)),
                new InventorySnapshot(0, 0, 0, 0, 0, 0, new[] { "head_con", "head_int", "head_balanced" }),
                Stats: CharacterStatsSnapshot.Zero,
                Buffs: CharacterStatsSnapshot.Zero,
                BuffFlags: BuffFlagsSnapshot.Empty),
            UserFreshness: SnapshotFreshnessState.Fresh,
            GearCatalogSnapshot: new GearCatalogSnapshot(
                DateTimeOffset.Parse("2026-04-30T06:00:00Z"),
                new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
                {
                    ["head_con"] = new("head_con", "Con Hood", "Head", "healer", null, new GearStatBlock(0m, 0m, 10m, 0m)),
                    ["head_int"] = new("head_int", "Int Hood", "Head", "healer", null, new GearStatBlock(0m, 9m, 0m, 0m)),
                    ["head_balanced"] = new("head_balanced", "Balanced Hood", "Head", "healer", null, new GearStatBlock(0m, 6m, 6m, 0m))
                }));
    }

    private sealed class FakeKeyValueStorage : IKeyValueStorage
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public Task<TValue?> GetAsync<TValue>(string key, CancellationToken cancellationToken)
        {
            return Task.FromResult(_values.TryGetValue(key, out var json)
                ? JsonSerializer.Deserialize<TValue>(json, JsonOptions)
                : default);
        }

        public Task<string?> GetRawJsonAsync(string key, CancellationToken cancellationToken)
        {
            return Task.FromResult(_values.GetValueOrDefault(key));
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }

        public Task SetAsync<TValue>(string key, TValue value, CancellationToken cancellationToken)
        {
            _values[key] = JsonSerializer.Serialize(value, JsonOptions);
            return Task.CompletedTask;
        }

        public Task SetRawJsonAsync(string key, string jsonText, CancellationToken cancellationToken)
        {
            _values[key] = jsonText;
            return Task.CompletedTask;
        }
    }
}
