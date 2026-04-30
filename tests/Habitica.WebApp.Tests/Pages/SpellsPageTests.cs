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
        Assert.Contains("Burst of Flames", cut.Markup);
        Assert.Contains("Ethereal Surge", cut.Markup);
        Assert.Contains("Bluest todo", cut.Markup);
        Assert.Contains("value 18", cut.Markup);
        Assert.Contains("Equipment recommendations", cut.Markup);
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

        Assert.Equal(new SpellCastRequest("fireball", "todo-blue", 2), controller.CastSpellCalls.Single());
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
                    new TaskSnapshot("habit-red", "Red habit", TaskType.Habit, false, 9m, null, null, -2m)
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
}
