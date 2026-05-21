using Bunit;
using Habitica.Domain.Sync;
using Habitica.Domain.Tasks;
using Habitica.Domain.User;
using Habitica.Rules.Spells;
using Habitica.WebApp.Pages;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Pages;

public sealed class SpellsPageTests : BunitContext
{
    [Fact]
    public void Renders_current_class_spells_default_target_values_and_equipment_recommendations()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new SpellViewModelFactory());
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(CreateState()));

        var cut = Render<SpellsPage>();

        Assert.Contains("Spells", cut.Markup);
        Assert.Contains("MP", cut.Markup);
        Assert.Contains("33.5 / 40", cut.Markup);
        Assert.Contains("Available mana", cut.Markup);
        Assert.Contains("Max 40 MP", cut.Markup);
        Assert.Contains("Burst of Flames", cut.Markup);
        Assert.Contains("Ethereal Surge", cut.Markup);
        Assert.Contains("Bluest todo", cut.Markup);
        Assert.Contains("Checked daily", cut.Markup);
        Assert.DoesNotContain("Finished todo", cut.Markup);
        Assert.Contains("value 18", cut.Markup);
        Assert.Contains("Equipment recommendations", cut.Markup);
        Assert.Contains("Auto equip", cut.Markup);
        Assert.Contains("Maximize INT", cut.Markup);
        Assert.Contains("Equipped", cut.Markup);
    }

    [Fact]
    public void Count_updates_total_mana_and_progress_bar_renders_for_active_cast()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new SpellViewModelFactory());
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
    }

    [Fact]
    public void Cast_button_calls_session_controller_with_selected_target_and_count()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new SpellViewModelFactory());
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
    public void Effect_preview_uses_selected_auto_equip_recommendation_when_enabled()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new SpellViewModelFactory());
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
        var controller = new FakeAppSessionController(CreateState());
        Services.AddSingleton<IAppSessionController>(controller);

        var cut = Render<SpellsPage>();

        cut.Find("[data-testid='equip-spell-recommendation-fireball-Maximize PER']").Click();

        var call = controller.EquipGearSlotsCalls.Single();
        Assert.Equal(EquipmentSetKind.Battle, call.Kind);
        Assert.Equal("spell:fireball:Maximize PER", call.OperationId);
        Assert.Equal("head_per", call.Slots.Head);
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
}
