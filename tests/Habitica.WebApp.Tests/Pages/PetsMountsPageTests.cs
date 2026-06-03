using Bunit;
using Habitica.Application.Inventory;
using Habitica.Domain.Sync;
using Habitica.Domain.User;
using Habitica.Storage;
using Habitica.WebApp.Pages;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Pages;

public sealed class PetsMountsPageTests : BunitContext
{
    [Fact]
    public void Renders_empty_collection_groups_and_missing_companions()
    {
        var cut = RenderPage(CreateSnapshot());

        Assert.Contains("Companion collection", cut.Markup);
        Assert.Contains("Base collection", cut.Markup);
        Assert.Contains("Quest collection", cut.Markup);
        Assert.Contains("Missing mount", cut.Markup);
        Assert.Contains("Need egg Wolf and potion Base", cut.Markup);
    }

    [Fact]
    public void Search_filters_visible_companions_by_name_and_key()
    {
        var cut = RenderPage(CreateSnapshot());

        cut.Find("[data-testid='pets-mounts-search']").Input("TigerCub-Base");

        Assert.NotEmpty(cut.FindAll("[data-testid='pet-card-TigerCub-Base']"));
        Assert.Empty(cut.FindAll("[data-testid='pet-card-Wolf-Base']"));
    }

    [Fact]
    public void Fold_state_is_saved_to_local_storage_and_loaded_on_next_render()
    {
        var storage = new InMemoryKeyValueStorage();
        var first = RenderPage(CreateSnapshot(), storage: storage);

        first.Find("[data-testid='toggle-pets-mounts-group-base']").Click();

        var second = Render<PetsMountsPage>();

        second.WaitForAssertion(() => Assert.Empty(second.FindAll("[data-testid='pet-card-Wolf-Base']")));
    }

    [Fact]
    public void Feed_queue_preview_and_fast_equip_dispatch_to_controller()
    {
        var controller = new FakeAppSessionController(CreateState(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                pets: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf-Base"] = 0 },
                mounts: new Dictionary<string, bool>(StringComparer.Ordinal) { ["Wolf-Base"] = true },
                food: new Dictionary<string, int>(StringComparer.Ordinal) { ["Meat"] = 2, ["Saddle"] = 1 })
        }));
        var cut = RenderPage(controller: controller);

        cut.Find("[data-testid='select-feed-Wolf-Base']").Click();
        Assert.Contains("favorite", cut.Find("[data-testid='feed-food-select']").TextContent);
        cut.Find("[data-testid='add-feed-queue-item']").Click();

        Assert.Contains("Wolf Base", cut.Find("[data-testid='feed-dry-run-preview']").TextContent);
        cut.Find("[data-testid='execute-feed-queue']").Click();
        cut.Find("[data-testid='equip-pet-Wolf-Base']").Click();
        cut.Find("[data-testid='equip-mount-Wolf-Base']").Click();

        var feed = Assert.Single(controller.FeedPetCalls);
        Assert.Equal(new PetFeedQueueItem("Wolf-Base", "Meat", 1), Assert.Single(feed));
        Assert.Equal("Wolf-Base", Assert.Single(controller.EquipPetCalls));
        Assert.Equal("Wolf-Base", Assert.Single(controller.EquipMountCalls));
    }

    [Fact]
    public void Feed_queue_stays_visible_when_sequential_execution_reports_failure()
    {
        var controller = new FakeAppSessionController(CreateState(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                pets: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf-Base"] = 0 },
                food: new Dictionary<string, int>(StringComparer.Ordinal) { ["Meat"] = 2 })
        }))
        {
            FeedPetResult = InventoryActionResult.Failure("Feed failed after the first queued request.")
        };
        var cut = RenderPage(controller: controller);

        cut.Find("[data-testid='select-feed-Wolf-Base']").Click();
        cut.Find("[data-testid='add-feed-queue-item']").Click();
        cut.Find("[data-testid='add-feed-queue-item']").Click();
        cut.Find("[data-testid='execute-feed-queue']").Click();

        Assert.Equal(2, Assert.Single(controller.FeedPetCalls).Length);
        Assert.Contains("2 queued", cut.Markup);
    }

    [Fact]
    public void Ready_to_hatch_pet_dispatches_hatch_action()
    {
        var controller = new FakeAppSessionController(CreateState(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                eggs: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf"] = 1 },
                potions: new Dictionary<string, int>(StringComparer.Ordinal) { ["Base"] = 1 })
        }));
        var cut = RenderPage(controller: controller);

        Assert.Contains("Ready to hatch", cut.Markup);
        cut.Find("[data-testid='hatch-pet-Wolf-Base']").Click();

        Assert.Equal(("Wolf", "Base"), Assert.Single(controller.HatchPetCalls));
    }

    [Fact]
    public void Renders_relocated_bulk_sell_planner_and_executes_confirmed_plan()
    {
        var controller = new FakeAppSessionController(CreateState(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                eggs: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf"] = 4 },
                food: new Dictionary<string, int>(StringComparer.Ordinal) { ["Meat"] = 2 },
                potions: new Dictionary<string, int>(StringComparer.Ordinal) { ["Base"] = 1 })
        }));
        var cut = RenderPage(controller: controller);

        Assert.Contains("Safe item sell preview", cut.Markup);
        cut.Find("[data-testid='confirm-bulk-sell']").Click();
        cut.Find("[data-testid='execute-bulk-sell']").Click();

        Assert.Contains((InventorySellItemType.Egg, "Wolf", 3), controller.SellInventoryItemCalls);
        Assert.Contains((InventorySellItemType.Food, "Meat", 1), controller.SellInventoryItemCalls);
    }

    private IRenderedComponent<PetsMountsPage> RenderPage(
        UserSnapshot? snapshot = null,
        FakeAppSessionController? controller = null,
        InMemoryKeyValueStorage? storage = null)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IKeyValueStorage>(storage ?? new InMemoryKeyValueStorage());
        Services.AddSingleton<IAppSessionController>(controller ?? new FakeAppSessionController(CreateState(snapshot ?? CreateSnapshot())));
        return Render<PetsMountsPage>();
    }

    private static SessionViewModel CreateState(UserSnapshot snapshot)
    {
        return new SessionViewModel(
            IsBusy: false,
            IsAuthenticated: true,
            UserId: "user-id",
            DisplayName: snapshot.DisplayName,
            ErrorMessage: null,
            LastSyncedAtUtc: snapshot.RetrievedAtUtc,
            TaskFreshness: SnapshotFreshnessState.Fresh,
            TaskSnapshot: null,
            UserSnapshot: snapshot,
            UserFreshness: SnapshotFreshnessState.Fresh);
    }

    private static UserSnapshot CreateSnapshot()
    {
        return new UserSnapshot(
            DateTimeOffset.Parse("2026-06-03T04:00:00Z"),
            "Companion Tester",
            "wizard",
            20,
            50m,
            50m,
            40m,
            40m,
            0m,
            100m,
            100m,
            null,
            null,
            null,
            new EquipmentSnapshot(
                new GearSlotsSnapshot(null, null, null, null, null),
                new GearSlotsSnapshot(null, null, null, null, null)),
            CreateInventory());
    }

    private static InventorySnapshot CreateInventory(
        IReadOnlyDictionary<string, int>? eggs = null,
        IReadOnlyDictionary<string, int>? food = null,
        IReadOnlyDictionary<string, int>? potions = null,
        IReadOnlyDictionary<string, int>? pets = null,
        IReadOnlyDictionary<string, bool>? mounts = null)
    {
        return new InventorySnapshot(
            eggs?.Count ?? 0,
            food?.Count ?? 0,
            potions?.Count ?? 0,
            0,
            pets?.Count ?? 0,
            mounts?.Count(static item => item.Value) ?? 0,
            Array.Empty<string>(),
            OwnedEggs: eggs,
            OwnedFood: food,
            OwnedHatchingPotions: potions,
            OwnedPets: pets,
            OwnedMounts: mounts);
    }
}
