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
    public void Signed_out_empty_pets_mounts_has_sign_in_action()
    {
        var cut = RenderPage(controller: new FakeAppSessionController(SessionViewModel.Empty));

        Assert.Contains("No saved account data is available on this device yet.", cut.Markup);
        Assert.Contains("href=\"/sign-in\"", cut.Markup);
        Assert.Contains("empty-state-actions", cut.Markup);
        Assert.DoesNotContain("Sign in or refresh", cut.Markup);
    }

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
                pets: new Dictionary<string, int>(StringComparer.Ordinal) { ["TigerCub-Base"] = 5 },
                mounts: new Dictionary<string, bool>(StringComparer.Ordinal) { ["Wolf-Base"] = true },
                food: new Dictionary<string, int>(StringComparer.Ordinal) { ["Meat"] = 2, ["Saddle"] = 1 })
        }));
        var cut = RenderPage(controller: controller);

        cut.Find("[data-testid='select-feed-TigerCub-Base']").Click();
        var foodOptions = cut.Find("[data-testid='feed-food-select-TigerCub-Base']").Children;
        Assert.Equal("Meat", foodOptions[0].GetAttribute("value"));
        Assert.Contains("+10%", foodOptions[0].TextContent);
        Assert.DoesNotContain("Saddle", cut.Find("[data-testid='feed-food-select-TigerCub-Base']").TextContent);

        Assert.Contains("Tiger Cub Base", cut.Find("[data-testid='feed-dry-run-preview']").TextContent);
        cut.Find("[data-testid='execute-feed-queue']").Click();
        cut.Find("[data-testid='equip-pet-TigerCub-Base']").Click();
        cut.Find("[data-testid='equip-mount-Wolf-Base']").Click();

        var feed = Assert.Single(controller.FeedPetCalls);
        Assert.Equal(new PetFeedQueueItem("TigerCub-Base", "Meat", 2), Assert.Single(feed));
        Assert.Equal("TigerCub-Base", Assert.Single(controller.EquipPetCalls));
        Assert.Equal("Wolf-Base", Assert.Single(controller.EquipMountCalls));
    }

    [Fact]
    public void Feed_planner_and_pet_cards_show_mount_growth_progress()
    {
        var cut = RenderPage(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                pets: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf-Base"] = 15 },
                food: new Dictionary<string, int>(StringComparer.Ordinal) { ["Meat"] = 6 })
        });

        var wolfProgress = cut.Find("[data-testid='pet-growth-Wolf-Base']");
        Assert.Equal("30", wolfProgress.GetAttribute("data-progress"));
        Assert.Contains("30% grown, 70% to mount", wolfProgress.TextContent);

        var tigerProgress = cut.Find("[data-testid='pet-growth-TigerCub-Base']");
        Assert.Equal("0", tigerProgress.GetAttribute("data-progress"));
        Assert.Contains("No mount progress", tigerProgress.TextContent);

        cut.Find("[data-testid='select-feed-Wolf-Base']").Click();

        var feedProgress = cut.Find("[data-testid='feed-growth-Wolf-Base']");
        Assert.Equal("30", feedProgress.GetAttribute("data-progress"));
        Assert.Contains("Available plan: Meat x 6; still needs 10% progress.", feedProgress.TextContent);
    }

    [Fact]
    public void Feed_queue_stays_visible_when_sequential_execution_reports_failure()
    {
        var controller = new FakeAppSessionController(CreateState(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                pets: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["Wolf-Base"] = 5,
                    ["TigerCub-Base"] = 5
                },
                food: new Dictionary<string, int>(StringComparer.Ordinal) { ["Meat"] = 20 })
        }))
        {
            FeedPetResult = InventoryActionResult.Failure("Feed failed after the first queued request.")
        };
        var cut = RenderPage(controller: controller);

        cut.Find("[data-testid='select-feed-Wolf-Base']").Click();
        cut.Find("[data-testid='select-feed-TigerCub-Base']").Click();
        cut.Find("[data-testid='execute-feed-queue']").Click();

        Assert.Equal(2, Assert.Single(controller.FeedPetCalls).Length);
        Assert.Contains("2 queued", cut.Markup);
    }

    [Fact]
    public void Missing_mount_plan_adds_matching_pet_to_feed_queue()
    {
        var cut = RenderPage(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                pets: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf-Base"] = 5 },
                food: new Dictionary<string, int>(StringComparer.Ordinal) { ["Meat"] = 9 })
        });

        cut.Find("[data-testid='plan-grow-mount-Wolf-Base']").Click();

        var queue = cut.Find("[data-testid='feed-queue-card-Wolf-Base']");
        Assert.Contains("Wolf Base", queue.TextContent);
        Assert.Contains("After plan 100%", queue.TextContent);
    }

    [Fact]
    public void Group_bulk_feed_action_adds_valid_growable_missing_mounts_without_mutation()
    {
        var controller = new FakeAppSessionController(CreateState(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                pets: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["Wolf-Base"] = 5,
                    ["TigerCub-Base"] = 5
                },
                food: new Dictionary<string, int>(StringComparer.Ordinal) { ["Meat"] = 9 })
        }));
        var cut = RenderPage(controller: controller);

        cut.Find("[data-testid='add-group-feed-queue-base']").Click();

        Assert.Empty(controller.FeedPetCalls);
        Assert.Contains("2 queued", cut.Markup);
        Assert.NotEmpty(cut.FindAll("[data-testid='feed-queue-card-Wolf-Base']"));
        var tigerQueue = cut.Find("[data-testid='feed-queue-card-TigerCub-Base']");
        Assert.Contains("Tiger Cub Base", tigerQueue.TextContent);
        Assert.Contains("This food is exhausted by earlier queued pets.", tigerQueue.TextContent);
    }

    [Fact]
    public void Group_bulk_feed_action_skips_owned_missing_and_already_queued_mounts()
    {
        var cut = RenderPage(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                pets: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["Wolf-Base"] = 5,
                    ["TigerCub-Base"] = 5,
                    ["FlyingPig-Base"] = 5
                },
                mounts: new Dictionary<string, bool>(StringComparer.Ordinal) { ["Wolf-Base"] = true },
                food: new Dictionary<string, int>(StringComparer.Ordinal) { ["Meat"] = 30 })
        });

        cut.Find("[data-testid='select-feed-TigerCub-Base']").Click();
        cut.Find("[data-testid='add-group-feed-queue-base']").Click();

        Assert.Contains("2 queued", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid='feed-queue-card-Wolf-Base']"));
        Assert.Single(cut.FindAll("[data-testid='feed-queue-card-TigerCub-Base']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='feed-queue-card-FlyingPig-Base']"));
        Assert.Empty(cut.FindAll("[data-testid='feed-queue-card-Dragon-Base']"));
    }

    [Fact]
    public void Group_bulk_feed_action_queues_no_food_candidates_for_warning_preview()
    {
        var cut = RenderPage(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                pets: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf-Base"] = 5 })
        });

        cut.Find("[data-testid='add-group-feed-queue-base']").Click();

        var queue = cut.Find("[data-testid='feed-queue-card-Wolf-Base']");
        Assert.Contains("No normal food available", queue.TextContent);
        Assert.Contains("No normal food is assigned.", queue.TextContent);
    }

    [Fact]
    public void Group_bulk_feed_action_respects_visible_mount_filter()
    {
        var cut = RenderPage(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                pets: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["Wolf-Base"] = 5,
                    ["Dragon-Base"] = 5
                },
                food: new Dictionary<string, int>(StringComparer.Ordinal) { ["Meat"] = 30 })
        });

        cut.Find("[data-testid='mount-type-filter']").Change("Dragon");
        cut.Find("[data-testid='add-group-feed-queue-base']").Click();

        Assert.Contains("1 queued", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid='feed-queue-card-Wolf-Base']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='feed-queue-card-Dragon-Base']"));
    }

    [Fact]
    public void Group_bulk_feed_action_is_disabled_without_valid_candidates()
    {
        var cut = RenderPage(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                pets: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf-Veggie"] = 5 },
                food: new Dictionary<string, int>(StringComparer.Ordinal) { ["Meat"] = 9 })
        });

        var baseButton = cut.Find("[data-testid='add-group-feed-queue-base']");
        var wackyButton = cut.Find("[data-testid='add-group-feed-queue-wacky']");

        Assert.True(baseButton.HasAttribute("disabled"));
        Assert.True(wackyButton.HasAttribute("disabled"));
        Assert.Contains("No growable mounts", cut.Find("[data-testid='pets-mounts-group-base']").TextContent);
        Assert.Contains("No growable mounts", cut.Find("[data-testid='pets-mounts-group-wacky']").TextContent);
    }

    [Fact]
    public void Transform_to_mount_confirms_and_executes_planned_food()
    {
        var controller = new FakeAppSessionController(CreateState(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                pets: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf-Base"] = 5 },
                food: new Dictionary<string, int>(StringComparer.Ordinal) { ["Meat"] = 9 })
        }));
        var cut = RenderPage(controller: controller);

        cut.Find("[data-testid='select-feed-Wolf-Base']").Click();
        cut.Find("[data-testid='transform-mount-Wolf-Base']").Click();
        Assert.Empty(controller.FeedPetCalls);

        cut.Find("[data-testid='confirm-transform-mount-Wolf-Base']").Click();

        var feed = Assert.Single(controller.FeedPetCalls);
        Assert.Equal(new PetFeedQueueItem("Wolf-Base", "Meat", 9), Assert.Single(feed));
    }

    [Fact]
    public void Queue_allocation_recalculates_after_removing_earlier_pet()
    {
        var cut = RenderPage(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                pets: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["Wolf-Base"] = 5,
                    ["TigerCub-Base"] = 5
                },
                food: new Dictionary<string, int>(StringComparer.Ordinal) { ["Meat"] = 9 })
        });

        cut.Find("[data-testid='select-feed-Wolf-Base']").Click();
        cut.Find("[data-testid='select-feed-TigerCub-Base']").Click();

        Assert.Contains("This food is exhausted by earlier queued pets.", cut.Find("[data-testid='feed-queue-card-TigerCub-Base']").TextContent);

        cut.Find("[data-testid='remove-feed-queue-Wolf-Base']").Click();

        var tigerQueue = cut.Find("[data-testid='feed-queue-card-TigerCub-Base']");
        Assert.Contains("After plan 100%", tigerQueue.TextContent);
        Assert.DoesNotContain("exhausted by earlier queued pets", tigerQueue.TextContent);
    }

    [Fact]
    public void Type_filters_compose_with_search_and_can_reset()
    {
        var cut = RenderPage(CreateSnapshot());

        cut.Find("[data-testid='pet-type-filter']").Change("FlyingPig");

        Assert.NotEmpty(cut.FindAll("[data-testid='pet-card-FlyingPig-Base']"));
        Assert.Empty(cut.FindAll("[data-testid='pet-card-Wolf-Base']"));

        cut.Find("[data-testid='pets-mounts-search']").Input("Base");
        Assert.NotEmpty(cut.FindAll("[data-testid='pet-card-FlyingPig-Base']"));
        Assert.Empty(cut.FindAll("[data-testid='pet-card-Dragon-Base']"));

        cut.Find("[data-testid='pet-type-filter']").Change(string.Empty);
        Assert.NotEmpty(cut.FindAll("[data-testid='pet-card-Wolf-Base']"));

        cut.Find("[data-testid='mount-type-filter']").Change("Dragon");
        Assert.NotEmpty(cut.FindAll("[data-testid='mount-card-Dragon-Base']"));
        Assert.Empty(cut.FindAll("[data-testid='mount-card-Wolf-Base']"));
    }

    [Fact]
    public void Saddle_flow_is_separate_and_confirmation_based()
    {
        var controller = new FakeAppSessionController(CreateState(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                pets: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf-Base"] = 5 },
                food: new Dictionary<string, int>(StringComparer.Ordinal) { ["Meat"] = 1, ["Saddle"] = 1 })
        }));
        var cut = RenderPage(controller: controller);

        cut.Find("[data-testid='select-feed-Wolf-Base']").Click();

        Assert.DoesNotContain("Saddle", cut.Find("[data-testid='feed-food-select-Wolf-Base']").TextContent);
        cut.Find("[data-testid='use-saddle-Wolf-Base']").Click();
        Assert.Empty(controller.FeedPetCalls);

        cut.Find("[data-testid='confirm-use-saddle-Wolf-Base']").Click();

        var feed = Assert.Single(controller.FeedPetCalls);
        Assert.Equal(new PetFeedQueueItem("Wolf-Base", "Saddle", 1), Assert.Single(feed));
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
