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
    private BunitJSModuleInterop? _petsMountsPageModule;

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
    public void Hatch_planner_renders_empty_state()
    {
        var cut = RenderPage(CreateSnapshot());

        Assert.Contains("Hatch queue", cut.Markup);
        Assert.Contains("Choose Add to Hatch Queue on a missing pet", cut.Markup);
        Assert.Contains("0 queued", cut.Markup);
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
    public void Queue_progress_is_hidden_without_active_execution()
    {
        var cut = RenderPage(CreateSnapshot());

        Assert.Empty(cut.FindAll("[data-testid='feed-queue-progress']"));
        Assert.Empty(cut.FindAll("[data-testid='hatch-queue-progress']"));
    }

    [Fact]
    public void Feed_queue_progress_renders_in_feed_block_and_guards_matching_controls()
    {
        var controller = new FakeAppSessionController(CreateState(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                pets: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf-Base"] = 5 },
                food: new Dictionary<string, int>(StringComparer.Ordinal) { ["Meat"] = 9 })
        }));
        var cut = RenderPage(controller: controller);

        cut.Find("[data-testid='select-feed-Wolf-Base']").Click();
        controller.SetState(controller.State with
        {
            ActivePetsMountsQueueProgress = new PetsMountsQueueProgress(PetsMountsQueueOperation.Feed, 1, 3),
            IsBusy = true
        });

        cut.WaitForAssertion(() =>
        {
            var progress = cut.Find("[data-testid='feed-queue-progress']");
            Assert.Contains("Feeding 1 of 3", progress.TextContent);
            Assert.Contains("mud-progress-linear", progress.InnerHtml);
            Assert.Empty(cut.FindAll("[data-testid='hatch-queue-progress']"));
            Assert.True(cut.Find("[data-testid='execute-feed-queue']").HasAttribute("disabled"));
            Assert.True(cut.Find("[data-testid='clear-feed-queue']").HasAttribute("disabled"));
            Assert.True(cut.Find("[data-testid='remove-feed-queue-Wolf-Base']").HasAttribute("disabled"));
            Assert.True(cut.Find("[data-testid='feed-food-select-Wolf-Base']").HasAttribute("disabled"));
            Assert.True(cut.Find("[data-testid='transform-mount-Wolf-Base']").HasAttribute("disabled"));
            Assert.True(cut.Find("[data-testid='select-feed-Wolf-Base']").HasAttribute("disabled"));
        });
    }

    [Fact]
    public void Hatch_queue_progress_renders_in_hatch_block_and_guards_matching_controls()
    {
        var controller = new FakeAppSessionController(CreateState(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                eggs: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf"] = 1 },
                potions: new Dictionary<string, int>(StringComparer.Ordinal) { ["Base"] = 1 })
        }));
        var cut = RenderPage(controller: controller);

        cut.Find("[data-testid='select-hatch-Wolf-Base']").Click();
        cut.Find("[data-testid='execute-hatch-queue']").Click();
        controller.SetState(controller.State with
        {
            ActivePetsMountsQueueProgress = new PetsMountsQueueProgress(PetsMountsQueueOperation.Hatch, 0, 1)
        });

        cut.WaitForAssertion(() =>
        {
            var progress = cut.Find("[data-testid='hatch-queue-progress']");
            Assert.Contains("Hatching 0 of 1", progress.TextContent);
            Assert.Contains("mud-progress-linear", progress.InnerHtml);
            Assert.Empty(cut.FindAll("[data-testid='feed-queue-progress']"));
            Assert.True(cut.Find("[data-testid='execute-hatch-queue']").HasAttribute("disabled"));
            Assert.True(cut.Find("[data-testid='confirm-hatch-queue']").HasAttribute("disabled"));
            Assert.True(cut.Find("[data-testid='clear-hatch-queue']").HasAttribute("disabled"));
            Assert.True(cut.Find("[data-testid='remove-hatch-queue-Wolf-Base']").HasAttribute("disabled"));
            Assert.True(cut.Find("[data-testid='select-hatch-Wolf-Base']").HasAttribute("disabled"));
        });
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
    public void Ready_to_hatch_pet_queues_and_confirmed_execution_dispatches_hatch_action()
    {
        var controller = new FakeAppSessionController(CreateState(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                eggs: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf"] = 1 },
                potions: new Dictionary<string, int>(StringComparer.Ordinal) { ["Base"] = 1 })
        }));
        var cut = RenderPage(controller: controller);

        Assert.Contains("Ready to hatch", cut.Markup);
        cut.Find("[data-testid='select-hatch-Wolf-Base']").Click();
        Assert.Empty(controller.HatchPetCalls);

        cut.WaitForAssertion(() => Assert.Contains("Wolf Base", cut.Find("[data-testid='hatch-queue-card-Wolf-Base']").TextContent));
        var queue = cut.Find("[data-testid='hatch-queue-card-Wolf-Base']");
        Assert.Contains("Egg available 1 / 1", queue.TextContent);
        Assert.Contains("Potion available 1 / 1", queue.TextContent);

        cut.Find("[data-testid='execute-hatch-queue']").Click();
        Assert.Empty(controller.HatchPetCalls);

        cut.Find("[data-testid='confirm-hatch-queue']").Click();
        Assert.Equal(("Wolf", "Base"), Assert.Single(controller.HatchPetCalls));
        Assert.Empty(cut.FindAll("[data-testid='hatch-queue-card-Wolf-Base']"));
    }

    [Fact]
    public void Hatch_queue_warns_and_blocks_execution_when_egg_is_missing()
    {
        var controller = new FakeAppSessionController(CreateState(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                potions: new Dictionary<string, int>(StringComparer.Ordinal) { ["Base"] = 1 })
        }));
        var cut = RenderPage(controller: controller);

        cut.Find("[data-testid='select-hatch-Wolf-Base']").Click();

        var queue = cut.Find("[data-testid='hatch-queue-card-Wolf-Base']");
        Assert.Contains("Need egg Wolf before hatching.", queue.TextContent);
        Assert.Contains("Potion plan 1", queue.TextContent);
        Assert.True(cut.Find("[data-testid='execute-hatch-queue']").HasAttribute("disabled"));
        Assert.Empty(controller.HatchPetCalls);
    }

    [Fact]
    public void Hatch_queue_warns_and_blocks_execution_when_potion_is_missing()
    {
        var controller = new FakeAppSessionController(CreateState(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                eggs: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf"] = 1 })
        }));
        var cut = RenderPage(controller: controller);

        cut.Find("[data-testid='select-hatch-Wolf-Base']").Click();

        var queue = cut.Find("[data-testid='hatch-queue-card-Wolf-Base']");
        Assert.Contains("Need potion Base before hatching.", queue.TextContent);
        Assert.Contains("Egg plan 1", queue.TextContent);
        Assert.True(cut.Find("[data-testid='execute-hatch-queue']").HasAttribute("disabled"));
        Assert.Empty(controller.HatchPetCalls);
    }

    [Fact]
    public void Hatch_queue_does_not_add_duplicate_pet_rows()
    {
        var cut = RenderPage(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                eggs: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf"] = 2 },
                potions: new Dictionary<string, int>(StringComparer.Ordinal) { ["Base"] = 2 })
        });

        cut.Find("[data-testid='select-hatch-Wolf-Base']").Click();
        cut.Find("[data-testid='select-hatch-Wolf-Base']").Click();

        Assert.Single(cut.FindAll("[data-testid='hatch-queue-card-Wolf-Base']"));
    }

    [Fact]
    public void Owned_pet_does_not_show_hatch_queue_action()
    {
        var cut = RenderPage(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                eggs: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf"] = 1 },
                potions: new Dictionary<string, int>(StringComparer.Ordinal) { ["Base"] = 1 },
                pets: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf-Base"] = 5 })
        });

        Assert.Empty(cut.FindAll("[data-testid='select-hatch-Wolf-Base']"));
    }

    [Fact]
    public void Group_bulk_hatch_action_adds_valid_missing_pets_without_mutation()
    {
        var controller = new FakeAppSessionController(CreateState(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                eggs: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["Wolf"] = 1,
                    ["TigerCub"] = 1
                },
                potions: new Dictionary<string, int>(StringComparer.Ordinal) { ["Base"] = 2 })
        }));
        var cut = RenderPage(controller: controller);

        cut.Find("[data-testid='add-group-hatch-queue-base']").Click();

        Assert.Empty(controller.HatchPetCalls);
        Assert.Contains("2 queued", cut.Markup);
        Assert.NotEmpty(cut.FindAll("[data-testid='hatch-queue-card-Wolf-Base']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='hatch-queue-card-TigerCub-Base']"));
    }

    [Fact]
    public void Group_bulk_hatch_action_skips_owned_and_already_queued_pets()
    {
        var controller = new FakeAppSessionController(CreateState(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                eggs: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["Wolf"] = 1,
                    ["TigerCub"] = 1,
                    ["FlyingPig"] = 1
                },
                potions: new Dictionary<string, int>(StringComparer.Ordinal) { ["Base"] = 3 },
                pets: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf-Base"] = 5 })
        }));
        var cut = RenderPage(controller: controller);

        cut.Find("[data-testid='select-hatch-TigerCub-Base']").Click();
        cut.Find("[data-testid='add-group-hatch-queue-base']").Click();

        Assert.Empty(controller.HatchPetCalls);
        Assert.Contains("2 queued", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid='hatch-queue-card-Wolf-Base']"));
        Assert.Single(cut.FindAll("[data-testid='hatch-queue-card-TigerCub-Base']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='hatch-queue-card-FlyingPig-Base']"));
    }

    [Fact]
    public void Group_bulk_hatch_action_skips_pets_missing_eggs()
    {
        var cut = RenderPage(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                eggs: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf"] = 1 },
                potions: new Dictionary<string, int>(StringComparer.Ordinal) { ["Base"] = 9 })
        });

        cut.Find("[data-testid='add-group-hatch-queue-base']").Click();

        Assert.Contains("1 queued", cut.Markup);
        Assert.NotEmpty(cut.FindAll("[data-testid='hatch-queue-card-Wolf-Base']"));
        Assert.Empty(cut.FindAll("[data-testid='hatch-queue-card-TigerCub-Base']"));
    }

    [Fact]
    public void Group_bulk_hatch_action_skips_pets_missing_hatching_potions()
    {
        var cut = RenderPage(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                eggs: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf"] = 2 },
                potions: new Dictionary<string, int>(StringComparer.Ordinal) { ["White"] = 1 })
        });

        cut.Find("[data-testid='add-group-hatch-queue-magic-potion']").Click();

        Assert.Contains("1 queued", cut.Markup);
        Assert.NotEmpty(cut.FindAll("[data-testid='hatch-queue-card-Wolf-White']"));
        Assert.Empty(cut.FindAll("[data-testid='hatch-queue-card-Wolf-Desert']"));
    }

    [Fact]
    public void Group_bulk_hatch_action_skips_special_unhatchable_pets()
    {
        var cut = RenderPage(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                eggs: new Dictionary<string, int>(StringComparer.Ordinal) { ["Unknown"] = 1 },
                potions: new Dictionary<string, int>(StringComparer.Ordinal) { ["Base"] = 1 },
                pets: new Dictionary<string, int>(StringComparer.Ordinal) { ["Unknown-Base"] = 5 })
        });

        var specialButton = cut.Find("[data-testid='add-group-hatch-queue-special']");

        Assert.True(specialButton.HasAttribute("disabled"));
        Assert.Contains("No hatchable pets", cut.Find("[data-testid='pets-mounts-group-special']").TextContent);
        Assert.Empty(cut.FindAll("[data-testid='hatch-queue-card-Unknown-Base']"));
    }

    [Fact]
    public void Group_bulk_hatch_action_is_disabled_without_valid_candidates()
    {
        var cut = RenderPage(CreateSnapshot());

        var baseButton = cut.Find("[data-testid='add-group-hatch-queue-base']");

        Assert.True(baseButton.HasAttribute("disabled"));
        Assert.Contains("No hatchable pets", cut.Find("[data-testid='pets-mounts-group-base']").TextContent);
    }

    [Fact]
    public void Group_bulk_hatch_action_recalculates_shared_resource_allocation()
    {
        var cut = RenderPage(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                eggs: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf"] = 1 },
                potions: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["White"] = 1,
                    ["Desert"] = 1
                })
        });

        cut.Find("[data-testid='add-group-hatch-queue-magic-potion']").Click();

        Assert.Contains("2 queued", cut.Markup);
        var desertQueue = cut.Find("[data-testid='hatch-queue-card-Wolf-Desert']");
        Assert.Contains("Egg reserved 1", desertQueue.TextContent);
        Assert.Contains("Egg Wolf is reserved by earlier queued pets.", desertQueue.TextContent);
    }

    [Fact]
    public void Hatch_queue_reserves_earlier_shared_resources_and_recalculates_after_remove()
    {
        var cut = RenderPage(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                eggs: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf"] = 1 },
                potions: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["Base"] = 1,
                    ["White"] = 1
                })
        });

        cut.Find("[data-testid='select-hatch-Wolf-Base']").Click();
        cut.Find("[data-testid='pets-mounts-search']").Input("Wolf-White");
        cut.Find("[data-testid='select-hatch-Wolf-White']").Click();

        var whiteQueue = cut.Find("[data-testid='hatch-queue-card-Wolf-White']");
        Assert.Contains("Egg reserved 1", whiteQueue.TextContent);
        Assert.Contains("Egg Wolf is reserved by earlier queued pets.", whiteQueue.TextContent);

        cut.Find("[data-testid='remove-hatch-queue-Wolf-Base']").Click();

        whiteQueue = cut.Find("[data-testid='hatch-queue-card-Wolf-White']");
        Assert.Contains("Egg available 1 / 1", whiteQueue.TextContent);
        Assert.DoesNotContain("reserved by earlier queued pets", whiteQueue.TextContent);
    }

    [Fact]
    public void Hatch_queue_clear_removes_all_rows()
    {
        var cut = RenderPage(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                eggs: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf"] = 1 },
                potions: new Dictionary<string, int>(StringComparer.Ordinal) { ["Base"] = 1 })
        });

        cut.Find("[data-testid='select-hatch-Wolf-Base']").Click();
        cut.Find("[data-testid='clear-hatch-queue']").Click();

        Assert.Empty(cut.FindAll("[data-testid='hatch-queue-card-Wolf-Base']"));
        Assert.Contains("Choose Add to Hatch Queue on a missing pet", cut.Markup);
    }

    [Fact]
    public void Hatch_queue_execution_stops_on_first_failure()
    {
        var controller = new FakeAppSessionController(CreateState(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                eggs: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["Wolf"] = 1,
                    ["TigerCub"] = 1
                },
                potions: new Dictionary<string, int>(StringComparer.Ordinal) { ["Base"] = 2 })
        }));
        controller.HatchPetResults.Enqueue(InventoryActionResult.Success("Wolf hatched."));
        controller.HatchPetResults.Enqueue(InventoryActionResult.Failure("Tiger hatch failed."));
        var cut = RenderPage(controller: controller);

        cut.Find("[data-testid='select-hatch-Wolf-Base']").Click();
        cut.Find("[data-testid='select-hatch-TigerCub-Base']").Click();
        cut.Find("[data-testid='execute-hatch-queue']").Click();
        cut.Find("[data-testid='confirm-hatch-queue']").Click();

        Assert.Equal(2, controller.HatchPetCalls.Count);
        Assert.Equal(("Wolf", "Base"), controller.HatchPetCalls[0]);
        Assert.Equal(("TigerCub", "Base"), controller.HatchPetCalls[1]);
        Assert.Empty(cut.FindAll("[data-testid='hatch-queue-card-Wolf-Base']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='hatch-queue-card-TigerCub-Base']"));
    }

    [Fact]
    public void Single_feed_queue_add_runs_scroll_stability_flow()
    {
        var cut = RenderPage(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                pets: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf-Base"] = 5 },
                food: new Dictionary<string, int>(StringComparer.Ordinal) { ["Meat"] = 9 })
        });
        var module = GetPetsMountsPageModule();

        cut.Find("[data-testid='select-feed-Wolf-Base']").Click();

        AssertQueueScrollCorrectionRan(cut, module);
    }

    [Fact]
    public void Missing_mount_queue_add_runs_scroll_stability_flow()
    {
        var cut = RenderPage(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                pets: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf-Base"] = 5 },
                food: new Dictionary<string, int>(StringComparer.Ordinal) { ["Meat"] = 9 })
        });
        var module = GetPetsMountsPageModule();

        cut.Find("[data-testid='plan-grow-mount-Wolf-Base']").Click();

        AssertQueueScrollCorrectionRan(cut, module);
    }

    [Fact]
    public void Group_feed_queue_add_runs_scroll_stability_flow()
    {
        var cut = RenderPage(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                pets: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["Wolf-Base"] = 5,
                    ["TigerCub-Base"] = 5
                },
                food: new Dictionary<string, int>(StringComparer.Ordinal) { ["Meat"] = 20 })
        });
        var module = GetPetsMountsPageModule();

        cut.Find("[data-testid='add-group-feed-queue-base']").Click();

        AssertQueueScrollCorrectionRan(cut, module);
    }

    [Fact]
    public void Single_hatch_queue_add_runs_scroll_stability_flow()
    {
        var cut = RenderPage(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                eggs: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf"] = 1 },
                potions: new Dictionary<string, int>(StringComparer.Ordinal) { ["Base"] = 1 })
        });
        var module = GetPetsMountsPageModule();

        cut.Find("[data-testid='select-hatch-Wolf-Base']").Click();

        AssertQueueScrollCorrectionRan(cut, module);
    }

    [Fact]
    public void Group_hatch_queue_add_runs_scroll_stability_flow()
    {
        var cut = RenderPage(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                eggs: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["Wolf"] = 1,
                    ["TigerCub"] = 1
                },
                potions: new Dictionary<string, int>(StringComparer.Ordinal) { ["Base"] = 2 })
        });
        var module = GetPetsMountsPageModule();

        cut.Find("[data-testid='add-group-hatch-queue-base']").Click();

        AssertQueueScrollCorrectionRan(cut, module);
    }

    [Fact]
    public void Queue_remove_does_not_run_scroll_stability_flow()
    {
        var cut = RenderPage(CreateSnapshot() with
        {
            Inventory = CreateInventory(
                pets: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf-Base"] = 5 },
                food: new Dictionary<string, int>(StringComparer.Ordinal) { ["Meat"] = 9 })
        });
        var module = GetPetsMountsPageModule();

        cut.Find("[data-testid='select-feed-Wolf-Base']").Click();
        AssertQueueScrollCorrectionRan(cut, module);
        var captureCount = CountModuleInvocations(module, "captureQueueAddScrollAnchor");
        var applyCount = CountModuleInvocations(module, "applyQueueAddScrollAnchor");

        cut.Find("[data-testid='remove-feed-queue-Wolf-Base']").Click();

        Assert.Equal(captureCount, CountModuleInvocations(module, "captureQueueAddScrollAnchor"));
        Assert.Equal(applyCount, CountModuleInvocations(module, "applyQueueAddScrollAnchor"));
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
        SetupPetsMountsPageModule();
        Services.AddMudServices();
        Services.AddSingleton<IKeyValueStorage>(storage ?? new InMemoryKeyValueStorage());
        Services.AddSingleton<IAppSessionController>(controller ?? new FakeAppSessionController(CreateState(snapshot ?? CreateSnapshot())));
        return Render<PetsMountsPage>();
    }

    private BunitJSModuleInterop SetupPetsMountsPageModule()
    {
        _petsMountsPageModule = JSInterop.SetupModule("./js/petsMountsPage.js");
        _petsMountsPageModule.SetupVoid("captureQueueAddScrollAnchor", _ => true).SetVoidResult();
        _petsMountsPageModule.SetupVoid("applyQueueAddScrollAnchor", _ => true).SetVoidResult();
        _petsMountsPageModule.SetupVoid("discardQueueAddScrollAnchor", _ => true).SetVoidResult();
        return _petsMountsPageModule;
    }

    private BunitJSModuleInterop GetPetsMountsPageModule()
    {
        return _petsMountsPageModule
            ?? throw new InvalidOperationException("Pets & Mounts JS module was not configured.");
    }

    private static void AssertQueueScrollCorrectionRan(
        IRenderedComponent<PetsMountsPage> cut,
        BunitJSModuleInterop module)
    {
        cut.WaitForAssertion(() =>
        {
            Assert.True(CountModuleInvocations(module, "captureQueueAddScrollAnchor") > 0);
            Assert.True(CountModuleInvocations(module, "applyQueueAddScrollAnchor") > 0);
        });
    }

    private static int CountModuleInvocations(BunitJSModuleInterop module, string identifier)
    {
        return module.Invocations.Count(invocation => invocation.Identifier == identifier);
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
