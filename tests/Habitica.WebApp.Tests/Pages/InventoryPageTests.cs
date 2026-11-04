using Bunit;
using Habitica.Application.Inventory;
using Habitica.Domain.Sync;
using Habitica.Domain.User;
using Habitica.WebApp.Pages;
using Habitica.WebApp.State;
using Habitica.WebApp.Tests.Qualification;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Pages;

public sealed class InventoryPageTests : BunitContext
{
    [Fact]
    public void Count_summaries_keep_exact_large_values_artwork_and_fallback_context()
    {
        var controller=ConfigureOptimizer("wizard");var snapshot=controller.State.UserSnapshot!;
        controller.SetState(controller.State with{UserSnapshot=snapshot with{Inventory=snapshot.Inventory with{EggCount=0,FoodCount=123456,HatchingPotionCount=int.MaxValue,QuestCount=42}}});
        var cut=Render<InventoryPage>();var chips=cut.FindAll(".habitica-icon-count-chip");Assert.Equal(4,chips.Count);
        Assert.Equal(new[]{"0","123456",int.MaxValue.ToString(),"42"},chips.Select(c=>c.QuerySelector("strong")!.TextContent));
        Assert.Equal(new[]{"Eggs","Food","Hatching potions","Quest scrolls"},chips.Select(c=>c.Children.Last().TextContent));
        foreach(var chip in chips){var image=chip.QuerySelector("img")!;Assert.Equal(chip.Children.Last().TextContent,image.GetAttribute("alt"));Assert.Contains("habitica-assets",image.GetAttribute("src"));Assert.Equal(image.GetAttribute("alt"),chip.QuerySelector(".habitica-image-fallback")!.GetAttribute("aria-label"));}
        PageLayoutEvidence.Save("inventory-count-large",cut.Markup,"/inventory","exact-summary-values-and-artwork",["counts","disabled-controls","long-content"],nameof(Count_summaries_keep_exact_large_values_artwork_and_fallback_context));
        foreach(var chip in chips){chip.Children.Last().TextContent+=" for seasonal companions";}
        PageLayoutEvidence.Save("inventory-count-translated",cut.Markup,"/inventory","longer-label-stress-preserves-count-and-art-context",["counts","long-content"],nameof(Count_summaries_keep_exact_large_values_artwork_and_fallback_context));
        AssertNoEquipmentChanges(controller);
    }

    [Theory]
    [InlineData("warrior")]
    [InlineData("wizard")]
    [InlineData("rogue")]
    [InlineData("healer")]
    [InlineData("unknown")]
    public void Optimizer_defaults_to_intelligence_without_equipping(string className)
    {
        var controller = ConfigureOptimizer(className);
        var cut = Render<InventoryPage>();

        Assert.Equal("Intelligence", cut.Find("select[data-testid='equipment-optimizer-goal']").GetAttribute("value"));
        Assert.Contains("Scholar Wand", cut.Find(".optimizer-item-strip").TextContent);
        Assert.DoesNotContain("Might Wand", cut.Find(".optimizer-item-strip").TextContent);
        AssertNoEquipmentChanges(controller);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("999")]
    [InlineData("-1")]
    public void Invalid_optimizer_goal_returns_to_intelligence_without_equipping(string? rawValue)
    {
        var controller = ConfigureOptimizer("wizard");
        var cut = Render<InventoryPage>();
        cut.Find("[data-testid='equipment-optimizer-goal']").Change("Strength");

        cut.Find("[data-testid='equipment-optimizer-goal']").Change(rawValue);

        Assert.Equal("Intelligence", cut.Find("[data-testid='equipment-optimizer-goal']").GetAttribute("value"));
        Assert.Contains("Scholar Wand", cut.Find(".optimizer-item-strip").TextContent);
        AssertNoEquipmentChanges(controller);
    }

    [Theory]
    [InlineData("Balanced")]
    [InlineData("Strength")]
    [InlineData("Constitution")]
    [InlineData("Perception")]
    [InlineData("BossDamage")]
    [InlineData("Survival")]
    public void Explicit_optimizer_goal_survives_session_refresh_without_equipping(string goal)
    {
        var controller = ConfigureOptimizer("warrior");
        var cut = Render<InventoryPage>();
        cut.Find("[data-testid='equipment-optimizer-goal']").Change(goal);

        controller.SetState(controller.State with { DisplayName = "Refreshed account" });

        Assert.Equal(goal, cut.Find("[data-testid='equipment-optimizer-goal']").GetAttribute("value"));
        AssertNoEquipmentChanges(controller);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Optimizer_exports_long_labels_and_disabled_actions(bool stale)
    {
        var controller = ConfigureOptimizer("wizard", longLabels: true);
        if (stale) controller.SetState(controller.State with { UserFreshness = SnapshotFreshnessState.Expired });
        var cut = Render<InventoryPage>();
        cut.Find("[data-testid='optimizer-preset-name']").Input("Intelligence casting equipment");

        Assert.Equal(stale, cut.Find("[data-testid='equip-optimizer-recommendation']").HasAttribute("disabled"));
        Assert.Equal(stale, cut.Find("[data-testid='save-optimizer-recommendation']").HasAttribute("disabled"));
        AssertNoEquipmentChanges(controller);
        PageLayoutEvidence.Save("inventory-intelligence-" + (stale ? "stale" : "fresh"), cut.Markup,
            "/inventory", stale ? "stale-disabled" : "fresh-long-labels", ["form", "disabled-controls"],
            nameof(Optimizer_exports_long_labels_and_disabled_actions));
    }

    private FakeAppSessionController ConfigureOptimizer(string className, bool longLabels = false)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./js/field-focus.js").SetupVoid("revealFocusedElement",_=>true).SetVoidResult();
        Services.AddMudServices();
        Services.AddSingleton(new InventoryViewModelFactory());
        var snapshot = CreateSnapshot() with
        {
            ClassName = className,
            Inventory = new InventorySnapshot(0, 0, 0, 0, 0, 0, ["weapon_str_10", "weapon_int_12"])
        };
        var controller = new FakeAppSessionController(SessionViewModel.Empty with
        {
            IsAuthenticated = true, UserId = "synthetic-optimizer", ClassName = className,
            UserSnapshot = snapshot, UserFreshness = SnapshotFreshnessState.Fresh,
            GearCatalogSnapshot = new GearCatalogSnapshot(snapshot.RetrievedAtUtc,
                new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
                {
                    ["weapon_str_10"] = new("weapon_str_10", "Might Wand", "Weapon", "special", null, new GearStatBlock(30m, 0m, 0m, 0m)),
                    ["weapon_int_12"] = new("weapon_int_12", longLabels ? "Scholar Wand for extraordinarily long intelligence casting recommendations and equipment comparisons" : "Scholar Wand",
                        "Weapon", "special", null, new GearStatBlock(0m, 12m, 0m, 0m))
                })
        });
        Services.AddSingleton<IAppSessionController>(controller);
        return controller;
    }

    [Fact]
    public void Slot_jump_retains_exact_two_handed_identity_and_account_change_clears_transient_discovery()
    {
        var controller=ConfigureOptimizer("wizard");var catalog=controller.State.GearCatalogSnapshot!;
        controller.SetState(controller.State with {GearCatalogSnapshot=catalog with {Items=catalog.Items.ToDictionary(row=>row.Key,row=>row.Value with {TwoHanded=row.Key=="weapon_str_10"})}});
        var cut=Render<InventoryPage>();var select=cut.Find("[data-testid='inventory-owned-slot']");Assert.Equal("Two-handed",select.QuerySelector("option[value='Two-Handed Weapons']")!.TextContent);
        select.Change("Two-Handed Weapons");Assert.NotNull(cut.Find("[data-testid='owned-gear-weapon_str_10']"));cut.Find("[data-testid='inventory-owned-view']").Click();
        Assert.Equal("Two-Handed Weapons",cut.Find("#owned-slot-battle-two-handed-weapons").TextContent);
        PageLayoutEvidence.Save("equipment-discovery-two-handed",cut.Markup,"/inventory","selected-two-handed-slot-retains-exact-value",["form","record-actions","disabled-controls"],nameof(Slot_jump_retains_exact_two_handed_identity_and_account_change_clears_transient_discovery));
        cut.Find("[data-testid='inventory-owned-query']").Input("private transient query");controller.SetState(controller.State with {UserId="other-synthetic-account"});
        Assert.Equal("",cut.Find("[data-testid='inventory-owned-query']").GetAttribute("value"));Assert.Equal("",cut.Find("[data-testid='inventory-owned-slot']").GetAttribute("value"));AssertNoEquipmentChanges(controller);

    }

    [Fact]
    public void Owned_discovery_searches_names_and_keys_reveals_slots_and_preserves_equipment_optimizer_and_drafts()
    {
        var controller=ConfigureOptimizer("wizard");var snapshot=controller.State.UserSnapshot!;
        var items=Enumerable.Range(0,300).ToDictionary(i=>$"weapon_search_{i:D3}",i=>new GearCatalogItem($"weapon_search_{i:D3}",i==290?"Žárová hůl pro dlouhé porovnání výbavy":"Discovery wand "+i,"Weapon","special",null,new GearStatBlock(0,i+1,0,0)),StringComparer.Ordinal);
        items.Add("back_special_1",new("back_special_1","Retained cosmetic cape","Back","special",null,GearStatBlock.Zero));
        var equipment=snapshot.Equipment with {Battle=new(null,null,"weapon_search_000",null,null),Costume=new(null,null,"weapon_search_001",null,null)};
        controller.SetState(controller.State with {UserSnapshot=snapshot with {Inventory=snapshot.Inventory with {OwnedGearKeys=items.Keys.ToArray()},Equipment=equipment},GearCatalogSnapshot=new(snapshot.RetrievedAtUtc,items)});
        var cut=Render<InventoryPage>();var before=System.Text.Json.JsonSerializer.Serialize(controller.State.UserSnapshot);
        Assert.Equal("search",cut.Find("[data-testid='inventory-owned-query']").GetAttribute("type"));
        cut.Find("[data-testid='battle-preset-name']").Input("Retained battle draft");cut.Find("[data-testid='optimizer-preset-name']").Input("Retained optimizer draft");cut.Find("[data-testid='equipment-optimizer-goal']").Change("Constitution");
        cut.Find("[data-testid='toggle-non-battle-equipment']").Click();
        PageLayoutEvidence.Save("equipment-discovery-initial",cut.Markup,"/inventory","hundreds-of-owned-items-before-filter",["form","long-content","disabled-controls"],nameof(Owned_discovery_searches_names_and_keys_reveals_slots_and_preserves_equipment_optimizer_and_drafts));
        cut.Find("[data-testid='inventory-owned-query']").Input("  ŽÁROVÁ  ");
        var results=cut.Find("[data-testid='inventory-owned-results']");Assert.Single(results.QuerySelectorAll("[data-testid^='owned-gear-']"));Assert.NotNull(results.QuerySelector("[data-testid='owned-gear-weapon_search_290']"));Assert.Contains("1 match",cut.Find("[data-testid='inventory-owned-count']").TextContent);
        Assert.Equal("weapon_search_000",controller.State.UserSnapshot!.Equipment.Battle.Weapon);Assert.Equal("weapon_search_001",controller.State.UserSnapshot.Equipment.Costume.Weapon);
        PageLayoutEvidence.Save("equipment-discovery-name",cut.Markup,"/inventory","translated-name-reveals-folded-owned-item",["form","long-content","record-actions","disabled-controls"],nameof(Owned_discovery_searches_names_and_keys_reveals_slots_and_preserves_equipment_optimizer_and_drafts));
        cut.Find("[data-testid='inventory-owned-query']").Input("WEAPON_SEARCH_049");Assert.Single(cut.FindAll("[data-testid='inventory-owned-results'] [data-testid^='owned-gear-']"));Assert.NotNull(cut.Find("[data-testid='owned-gear-weapon_search_049']"));
        cut.Find("[data-testid='inventory-owned-slot']").Change("Back");Assert.Contains("No owned equipment matches",cut.Find("[data-testid='inventory-owned-empty']").TextContent);
        PageLayoutEvidence.Save("equipment-discovery-empty",cut.Markup,"/inventory","query-and-slot-have-no-owned-match",["form","empty-state","disabled-controls"],nameof(Owned_discovery_searches_names_and_keys_reveals_slots_and_preserves_equipment_optimizer_and_drafts));
        cut.Find("[data-testid='inventory-owned-reset']").Click();cut.Find("[data-testid='inventory-owned-slot']").Change("Back");Assert.NotNull(cut.Find("[data-testid='owned-gear-back_special_1']"));var focusCount=JSInterop.Invocations.Count(row=>row.Identifier=="Blazor._internal.domWrapper.focus");cut.Find("[data-testid='inventory-owned-view']").Click();Assert.True(JSInterop.Invocations.Count(row=>row.Identifier=="Blazor._internal.domWrapper.focus")>focusCount);
        PageLayoutEvidence.Save("equipment-discovery-slot",cut.Markup,"/inventory","slot-jump-reveals-owned-accessory",["form","record-actions","disabled-controls"],nameof(Owned_discovery_searches_names_and_keys_reveals_slots_and_preserves_equipment_optimizer_and_drafts));
        cut.Find("[data-testid='inventory-owned-slot']").Change("Weapon");Assert.Equal(20,cut.FindAll("[data-testid='inventory-owned-results'] [data-testid^='owned-gear-']").Count);Assert.Contains("300 matches",cut.Find("[data-testid='inventory-owned-count']").TextContent);
        var first=cut.FindAll("[data-testid='inventory-owned-results'] [data-testid^='owned-gear-']").Select(row=>row.GetAttribute("data-testid")).ToHashSet();cut.Find("[data-testid='inventory-owned-next']").Click();var second=cut.FindAll("[data-testid='inventory-owned-results'] [data-testid^='owned-gear-']").Select(row=>row.GetAttribute("data-testid")).ToArray();Assert.Equal(20,second.Length);Assert.Empty(second.Where(first.Contains));
        cut.Find("[data-testid='inventory-owned-reset']").Click();Assert.Equal("",cut.Find("[data-testid='inventory-owned-query']").GetAttribute("value"));Assert.Equal("Retained battle draft",cut.Find("[data-testid='battle-preset-name']").GetAttribute("value"));Assert.Equal("Retained optimizer draft",cut.Find("[data-testid='optimizer-preset-name']").GetAttribute("value"));Assert.Equal("Constitution",cut.Find("[data-testid='equipment-optimizer-goal']").GetAttribute("value"));
        Assert.NotNull(cut.Find("[data-testid='owned-gear-back_special_1']"));Assert.Empty(cut.FindAll("[data-testid='owned-gear-weapon_search_049']"));Assert.Equal(before,System.Text.Json.JsonSerializer.Serialize(controller.State.UserSnapshot));AssertNoEquipmentChanges(controller);Assert.Empty(controller.RenamePresetCalls);Assert.Empty(controller.RemovePresetCalls);
    }

    [Fact]
    public void Same_named_gear_and_presets_have_record_context_and_persistent_input_labels()
    {
        var controller=ConfigureOptimizer("wizard");var stamp=controller.State.UserSnapshot!.RetrievedAtUtc;
        var catalog=controller.State.GearCatalogSnapshot!;
        controller.SetState(controller.State with
        {
            UserSnapshot=controller.State.UserSnapshot with { Equipment=controller.State.UserSnapshot.Equipment with { Battle=new GearSlotsSnapshot(null,null,"weapon_int_12",null,null) } },
            GearCatalogSnapshot=catalog with { Items=catalog.Items.ToDictionary(row=>row.Key,row=>row.Value with { Text="Same named wand with long descriptive comparison text" }) },
            EquipmentPresets=[new("preset-one","synthetic-optimizer",EquipmentSetKind.Battle,"Same casting preset",stamp,new(null,null,"weapon_str_10",null,null)),
                new("preset-two","synthetic-optimizer",EquipmentSetKind.Battle,"Same casting preset",stamp,new(null,null,"weapon_int_12",null,null))]
        });
        var cut=Render<InventoryPage>();
        foreach(var id in new[]{"battle-preset-name","optimizer-preset-name"})
        { Assert.NotNull(cut.Find("#"+id));Assert.False(string.IsNullOrWhiteSpace(cut.Find("label[for='"+id+"']").TextContent)); }
        var names=cut.FindAll("[data-testid^='equip-best-']").Select(button=>button.GetAttribute("aria-label")).ToArray();Assert.Equal(names.Length,names.Distinct().Count());
        foreach(var key in new[]{"weapon_str_10","weapon_int_12"})Assert.Contains(key,cut.Find("[data-testid='equip-best-"+key+"']").GetAttribute("aria-label"));
        cut.Find("[data-testid='toggle-other-items-Weapon']").Click();
        Assert.StartsWith("Unequip battle",cut.Find("[data-testid='equip-battle-weapon_int_12']").GetAttribute("aria-label"));
        Assert.StartsWith("Equip battle",cut.Find("[data-testid='equip-battle-weapon_str_10']").GetAttribute("aria-label"));
        foreach(var preset in new[]{"preset-one","preset-two"})
        {
            foreach(var action in new[]{"equip","rename","remove"})Assert.Contains(preset,cut.Find("[data-testid='"+action+"-preset-"+preset+"']").GetAttribute("aria-label"));
        }
        Assert.Contains("Intelligence",cut.Find("[data-testid='equip-optimizer-recommendation']").GetAttribute("aria-label"));
        cut.Find("[data-testid='rename-preset-preset-two']").Click();var rename=cut.Find("[data-testid='rename-preset-name-preset-two']");
        Assert.False(string.IsNullOrWhiteSpace(cut.Find("label[for='"+rename.Id+"']").TextContent));
        Assert.Contains("preset-two",cut.Find("[data-testid='save-rename-preset-preset-two']").GetAttribute("aria-label"));
        AssertNoEquipmentChanges(controller);
        PageLayoutEvidence.Save("game-inventory-context",cut.Markup,"/inventory","same-named-gear-and-presets-rename-input-visible",["persistent-labels","record-actions","long-content","disabled-controls"],nameof(Same_named_gear_and_presets_have_record_context_and_persistent_input_labels));
        cut.Find("[data-testid='equip-battle-weapon_str_10']").Click();Assert.Equal(new[]{(EquipmentSetKind.Battle,"weapon_str_10")},controller.EquipItemCalls);
        rename.Input("Renamed second preset");cut.Find("[data-testid='save-rename-preset-preset-two']").Click();Assert.Equal(("preset-two","Renamed second preset"),Assert.Single(controller.RenamePresetCalls));
        Assert.Empty(controller.EquipPresetCalls);Assert.Empty(controller.SavePresetCalls);Assert.Empty(controller.RemovePresetCalls);
    }

    [Fact]
    public void Whitespace_equivalent_gear_and_preset_names_expose_distinct_public_references()
    {
        var controller=ConfigureOptimizer("wizard");var stamp=controller.State.UserSnapshot!.RetrievedAtUtc;var catalog=controller.State.GearCatalogSnapshot!;
        const string name="Same named wand with long descriptive comparison text";
        controller.SetState(controller.State with
        {
            GearCatalogSnapshot=catalog with {Items=catalog.Items.ToDictionary(row=>row.Key,row=>row.Value with {Text=row.Key=="weapon_int_12"?name.Replace(" ","  "):name})},
            EquipmentPresets=[new("spaced-one","synthetic-optimizer",EquipmentSetKind.Battle,"Same casting preset",stamp,new(null,null,"weapon_str_10",null,null)),
                new("spaced-two","synthetic-optimizer",EquipmentSetKind.Battle,"Same  casting  preset",stamp,new(null,null,"weapon_int_12",null,null))]
        });
        var cut=Render<InventoryPage>();
        foreach(var key in new[]{"weapon_str_10","weapon_int_12"})Assert.Contains(key,cut.Find("[data-testid='equip-best-"+key+"']").GetAttribute("aria-label"));
        foreach(var id in new[]{"spaced-one","spaced-two"})
        {Assert.Contains(id,cut.Find("[data-testid='rename-preset-"+id+"']").GetAttribute("aria-label"));Assert.Contains("Preset reference: "+id,cut.Markup);}
        cut.Find("[data-testid='rename-preset-spaced-two']").Click();AssertNoEquipmentChanges(controller);
        PageLayoutEvidence.Save("game-inventory-normalized-context",cut.Markup,"/inventory","whitespace-equivalent-gear-and-presets",["record-actions","long-content","persistent-labels"],nameof(Whitespace_equivalent_gear_and_preset_names_expose_distinct_public_references));
    }

    private static void AssertNoEquipmentChanges(FakeAppSessionController controller)
    {
        Assert.Empty(controller.EquipGearSlotsCalls);
        Assert.Empty(controller.EquipItemCalls);
        Assert.Empty(controller.EquipPresetCalls);
        Assert.Empty(controller.SavePresetCalls);
        Assert.Empty(controller.SavePresetWithSlotsCalls);
    }

    [Fact]
    public void Signed_out_empty_equipment_page_has_sign_in_action()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new InventoryViewModelFactory());
        Services.AddSingleton<IAppSessionController>(new FakeAppSessionController(SessionViewModel.Empty));

        var cut = Render<InventoryPage>();

        Assert.Contains("No saved account data is available on this device yet.", cut.Markup);
        Assert.Contains("href=\"/sign-in\"", cut.Markup);
        Assert.Contains("empty-state-actions", cut.Markup);
        Assert.DoesNotContain("Sign in or refresh", cut.Markup);
    }

    [Fact]
    public void Renders_equipment_groups_from_cached_user_snapshot()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new InventoryViewModelFactory());
        var controller = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                UserId: "user-id",
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: null,
                ClassName: "wizard",
                Level: 15,
                UserSnapshot: new UserSnapshot(
                    DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
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
                        new GearSlotsSnapshot("head_wizard_3", "armor_wizard_4", "weapon_wizard_5", "shield_wizard_2", "back_wizard_1", Eyewear: "eyewear_special_1"),
                        new GearSlotsSnapshot("head_special_2", "armor_special_2", "weapon_special_2", "shield_special_2", "back_special_2")),
                    new InventorySnapshot(
                        1,
                        5,
                        1,
                        1,
                        1,
                        1,
                        new[] { "head_wizard_3", "head_special_2", "eyewear_special_1", "weapon_wizard_5", "weapon_warrior_6" })),
                UserFreshness: SnapshotFreshnessState.Fresh,
                GearCatalogSnapshot: new GearCatalogSnapshot(
                    DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
                    new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
                    {
                        ["head_wizard_3"] = new("head_wizard_3", "Wizard Hat", "Head", "wizard", null, new GearStatBlock(0m, 2m, 0m, 0m)),
                        ["eyewear_special_1"] = new("eyewear_special_1", "Scholar Spectacles", "Eyewear", "special", null, new GearStatBlock(0m, 0m, 0m, 4m)),
                        ["weapon_wizard_5"] = new("weapon_wizard_5", "Wizard Wand", "Weapon", "wizard", null, new GearStatBlock(0m, 12m, 0m, 2m)),
                        ["weapon_warrior_6"] = new("weapon_warrior_6", "Warrior Sword", "Weapon", "warrior", null, new GearStatBlock(10m, 0m, 10m, 0m))
                    }),
                EquipmentPresets: new[]
                {
                    new EquipmentPreset(
                        "preset-1",
                        "user-id",
                        EquipmentSetKind.Battle,
                        "Casting",
                        DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                        new GearSlotsSnapshot("head_wizard_3", null, "weapon_wizard_5", null, null, Eyewear: "eyewear_special_1"))
                }));
        Services.AddSingleton<IAppSessionController>(controller);

        var cut = Render<InventoryPage>();

        Assert.Contains("Equipment", cut.Markup);
        Assert.DoesNotContain("Inventory and equipped gear", cut.Markup);
        Assert.Contains("Equipment explorer", cut.Markup);
        Assert.Contains("Equipped battle gear", cut.Markup);
        Assert.DoesNotContain("Equipped costume", cut.Markup);
        Assert.DoesNotContain("Costume loadout", cut.Markup);
        Assert.Contains("Battle gear presets", cut.Markup);
        Assert.DoesNotContain("Costume presets", cut.Markup);
        Assert.Contains("Casting", cut.Markup);
        Assert.Contains("preset-1", cut.Markup);
        Assert.Contains("Wizard Hat", cut.Markup);
        Assert.Contains("Scholar Spectacles", cut.Markup);
        Assert.Contains("Wizard Wand", cut.Markup);
        Assert.Contains("shop_weapon_wizard_5.png", cut.Markup);
        Assert.Contains("Pet-Wolf-Base.png", cut.Markup);
        Assert.Contains("Mount_Icon_Wolf-Base.png", cut.Markup);
        Assert.Contains("Pet_HatchingPotion_Base.png", cut.Markup);
        Assert.Contains("Best in Category", cut.Markup);
        Assert.Contains("Warrior Sword", cut.Markup);
        Assert.Contains("Head", cut.Markup);
        Assert.Contains("Eyewear", cut.Markup);
        Assert.Contains("Weapon", cut.Markup);
        Assert.NotEmpty(cut.FindAll(".inventory-preset-list"));
        var highlightedStats = cut.FindAll(".highest-stat").Select(element => element.TextContent).ToArray();
        Assert.Contains("INT\u00a018", highlightedStats);
        Assert.Contains("STR\u00a010", highlightedStats);
        Assert.Contains("CON\u00a010", highlightedStats);
        Assert.DoesNotContain("Battle equipped", cut.Markup);
        Assert.DoesNotContain("Costume equipped", cut.Markup);
        Assert.DoesNotContain("equip-costume", cut.Markup);
    }

    [Fact]
    public void Other_items_are_collapsed_by_default_and_can_be_expanded()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new InventoryViewModelFactory());
        var controller = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                UserId: "user-id",
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: null,
                ClassName: "wizard",
                Level: 15,
                UserSnapshot: CreateSnapshot() with
                {
                    Inventory = new InventorySnapshot(1, 5, 1, 1, 1, 1, new[] { "weapon_per_6", "weapon_per_10" })
                },
                UserFreshness: SnapshotFreshnessState.Fresh,
                GearCatalogSnapshot: new GearCatalogSnapshot(
                    DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
                    new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
                    {
                        ["weapon_per_6"] = new("weapon_per_6", "Scout Wand", "Weapon", "special", null, new GearStatBlock(0m, 0m, 0m, 6m)),
                        ["weapon_per_10"] = new("weapon_per_10", "Seer Wand", "Weapon", "special", null, new GearStatBlock(0m, 0m, 0m, 10m))
                    })));
        Services.AddSingleton<IAppSessionController>(controller);

        var cut = Render<InventoryPage>();

        Assert.Contains("Seer Wand", cut.Markup);
        Assert.DoesNotContain("Scout Wand", cut.Markup);

        cut.Find("[data-testid='toggle-other-items-Weapon']").Click();

        Assert.Contains("Scout Wand", cut.Markup);
    }

    [Fact]
    public void Non_battle_equipment_is_collapsed_by_default_and_can_be_expanded()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new InventoryViewModelFactory());
        var controller = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                UserId: "user-id",
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: null,
                ClassName: "wizard",
                Level: 15,
                UserSnapshot: CreateSnapshot() with
                {
                    Inventory = new InventorySnapshot(1, 5, 1, 1, 1, 1, new[] { "back_special_1", "weapon_wizard_5" })
                },
                UserFreshness: SnapshotFreshnessState.Fresh,
                GearCatalogSnapshot: new GearCatalogSnapshot(
                    DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
                    new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
                    {
                        ["back_special_1"] = new("back_special_1", "Festival Cape", "Back", "special", null, GearStatBlock.Zero),
                        ["weapon_wizard_5"] = new("weapon_wizard_5", "Wizard Wand", "Weapon", "wizard", null, new GearStatBlock(0m, 12m, 0m, 2m))
                    })));
        Services.AddSingleton<IAppSessionController>(controller);

        var cut = Render<InventoryPage>();

        Assert.Contains("Other equipment", cut.Markup);
        Assert.DoesNotContain("Festival Cape", cut.Markup);

        cut.Find("[data-testid='toggle-non-battle-equipment']").Click();

        Assert.Contains("Festival Cape", cut.Markup);
    }

    [Fact]
    public void Equipped_best_in_category_item_has_disabled_equipped_button()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new InventoryViewModelFactory());
        var controller = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                UserId: "user-id",
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: null,
                ClassName: "wizard",
                Level: 15,
                UserSnapshot: CreateSnapshot() with
                {
                    Inventory = new InventorySnapshot(
                        1,
                        5,
                        1,
                        1,
                        1,
                        1,
                        new[] { "head_wizard_3", "head_special_2", "eyewear_special_1", "weapon_wizard_5", "weapon_warrior_6" })
                },
                UserFreshness: SnapshotFreshnessState.Fresh,
                GearCatalogSnapshot: new GearCatalogSnapshot(
                    DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
                    new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
                    {
                        ["weapon_wizard_5"] = new("weapon_wizard_5", "Wizard Wand", "Weapon", "wizard", null, new GearStatBlock(0m, 12m, 0m, 2m))
                    })));
        Services.AddSingleton<IAppSessionController>(controller);

        var cut = Render<InventoryPage>();

        var button = cut.Find("[data-testid='equip-best-weapon_wizard_5']");
        Assert.Contains("Equipped battle", button.TextContent);
        Assert.True(button.HasAttribute("disabled"));
    }

    [Fact]
    public void Inventory_buttons_call_session_controller_actions()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new InventoryViewModelFactory());
        var controller = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                UserId: "user-id",
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: null,
                ClassName: "wizard",
                Level: 15,
                UserSnapshot: CreateSnapshot() with
                {
                    Inventory = new InventorySnapshot(
                        1,
                        5,
                        1,
                        1,
                        1,
                        1,
                        new[] { "head_wizard_3", "head_special_2", "eyewear_special_1", "weapon_wizard_5", "weapon_warrior_6" })
                },
                UserFreshness: SnapshotFreshnessState.Fresh,
                GearCatalogSnapshot: new GearCatalogSnapshot(
                    DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
                    new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
                    {
                        ["head_wizard_3"] = new("head_wizard_3", "Wizard Hat", "Head", "wizard", null, new GearStatBlock(0m, 2m, 0m, 0m)),
                        ["eyewear_special_1"] = new("eyewear_special_1", "Scholar Spectacles", "Eyewear", "special", null, new GearStatBlock(0m, 0m, 0m, 4m)),
                        ["weapon_wizard_5"] = new("weapon_wizard_5", "Wizard Wand", "Weapon", "wizard", null, new GearStatBlock(0m, 12m, 0m, 2m)),
                        ["weapon_warrior_6"] = new("weapon_warrior_6", "Warrior Sword", "Weapon", "warrior", null, new GearStatBlock(10m, 0m, 1m, 0m))
                    }),
                EquipmentPresets: new[]
                {
                    new EquipmentPreset(
                        "preset-1",
                        "user-id",
                        EquipmentSetKind.Battle,
                        "Casting",
                        DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                        new GearSlotsSnapshot(null, null, "weapon_wizard_5", null, null))
                }));
        Services.AddSingleton<IAppSessionController>(controller);

        var cut = Render<InventoryPage>();

        cut.Find("[data-testid='equip-best-eyewear_special_1']").Click();
        cut.Find("[data-testid='toggle-other-items-Weapon']").Click();
        cut.Find("[data-testid='equip-battle-weapon_warrior_6']").Click();
        cut.Find("[data-testid='equip-preset-item-preset-1-weapon_wizard_5']").Click();
        cut.Find("[data-testid='equip-preset-preset-1']").Click();
        cut.Find("[data-testid='rename-preset-preset-1']").Click();
        cut.Find("[data-testid='rename-preset-name-preset-1']").Input("Focused Casting");
        cut.Find("[data-testid='save-rename-preset-preset-1']").Click();
        cut.Find("[data-testid='remove-preset-preset-1']").Click();
        cut.Find("[data-testid='confirm-remove-preset-preset-1']").Click();

        Assert.Equal(
            new[] { (EquipmentSetKind.Battle, "eyewear_special_1"), (EquipmentSetKind.Battle, "weapon_warrior_6"), (EquipmentSetKind.Battle, "weapon_wizard_5") },
            controller.EquipItemCalls);
        Assert.Equal("preset-1", controller.EquipPresetCalls.Single());
        Assert.Equal(("preset-1", "Focused Casting"), controller.RenamePresetCalls.Single());
        Assert.Equal("preset-1", controller.RemovePresetCalls.Single());
    }

    [Fact]
    public void Optimizer_can_equip_and_save_recommendations()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new InventoryViewModelFactory());
        var controller = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                UserId: "user-id",
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: null,
                ClassName: "wizard",
                Level: 15,
                UserSnapshot: CreateSnapshot() with
                {
                    Inventory = new InventorySnapshot(1, 5, 1, 1, 1, 1, new[] { "weapon_str_10", "weapon_int_12" })
                },
                UserFreshness: SnapshotFreshnessState.Fresh,
                GearCatalogSnapshot: new GearCatalogSnapshot(
                    DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
                    new Dictionary<string, GearCatalogItem>(StringComparer.Ordinal)
                    {
                        ["weapon_str_10"] = new("weapon_str_10", "Might Wand", "Weapon", "special", null, new GearStatBlock(10m, 0m, 0m, 0m)),
                        ["weapon_int_12"] = new("weapon_int_12", "Scholar Wand", "Weapon", "special", null, new GearStatBlock(0m, 12m, 0m, 0m))
                    })));
        Services.AddSingleton<IAppSessionController>(controller);

        var cut = Render<InventoryPage>();

        cut.Find("[data-testid='equipment-optimizer-goal']").Change("Strength");
        Assert.Contains("Might Wand", cut.Markup);

        cut.Find("[data-testid='equip-optimizer-recommendation']").Click();
        cut.Find("[data-testid='optimizer-preset-name']").Input("Boss strength");
        cut.Find("[data-testid='save-optimizer-recommendation']").Click();

        var equipCall = Assert.Single(controller.EquipGearSlotsCalls);
        Assert.Equal("weapon_str_10", equipCall.Slots.Weapon);
        var saveCall = Assert.Single(controller.SavePresetWithSlotsCalls);
        Assert.Equal("Boss strength", saveCall.Name);
        Assert.Equal("weapon_str_10", saveCall.Slots.Weapon);
    }

    [Fact]
    public void Does_not_render_bulk_sell_planner_after_companion_page_relocation()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new InventoryViewModelFactory());
        var controller = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: false,
                IsAuthenticated: true,
                UserId: "user-id",
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: null,
                ClassName: "wizard",
                Level: 15,
                UserSnapshot: CreateSnapshot() with
                {
                    Inventory = new InventorySnapshot(
                        4,
                        5,
                        1,
                        1,
                        1,
                        1,
                        Array.Empty<string>(),
                        OwnedEggs: new Dictionary<string, int>(StringComparer.Ordinal) { ["Wolf"] = 4 },
                        OwnedFood: new Dictionary<string, int>(StringComparer.Ordinal) { ["Meat"] = 2 },
                        OwnedHatchingPotions: new Dictionary<string, int>(StringComparer.Ordinal) { ["Base"] = 1 })
                },
                UserFreshness: SnapshotFreshnessState.Fresh));
        Services.AddSingleton<IAppSessionController>(controller);

        var cut = Render<InventoryPage>();

        Assert.DoesNotContain("Safe item sell preview", cut.Markup);
        Assert.DoesNotContain("Bulk sell planner", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid='confirm-bulk-sell']"));
        Assert.Empty(controller.SellInventoryItemCalls);
    }

    [Fact]
    public void Active_preset_equip_progress_renders_for_matching_preset()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(new InventoryViewModelFactory());
        var controller = new FakeAppSessionController(
            new SessionViewModel(
                IsBusy: true,
                IsAuthenticated: true,
                UserId: "user-id",
                DisplayName: "Mage Tester",
                ErrorMessage: null,
                LastSyncedAtUtc: DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
                TaskFreshness: SnapshotFreshnessState.Fresh,
                TaskSnapshot: null,
                ClassName: "wizard",
                Level: 15,
                UserSnapshot: CreateSnapshot(),
                UserFreshness: SnapshotFreshnessState.Fresh,
                EquipmentPresets: new[]
                {
                    new EquipmentPreset(
                        "preset-1",
                        "user-id",
                        EquipmentSetKind.Battle,
                        "Casting",
                        DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                        new GearSlotsSnapshot("head_wizard_3", "armor_wizard_4", "weapon_wizard_5", "shield_wizard_2", null))
                },
                ActiveEquipmentProgress: new EquipmentProgress("preset:preset-1", "Equipping Casting", 2, 4)));
        Services.AddSingleton<IAppSessionController>(controller);

        var cut = Render<InventoryPage>();

        Assert.Contains("Equipping Casting", cut.Markup);
        Assert.Contains("2 of 4", cut.Markup);
        Assert.Contains("mud-progress-linear", cut.Markup);
        Assert.Contains("scheme-progress-primary", cut.Markup);
    }

    private static UserSnapshot CreateSnapshot()
    {
        return new UserSnapshot(
            DateTimeOffset.Parse("2026-04-26T08:00:00Z"),
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
            new InventorySnapshot(
                1,
                5,
                1,
                1,
                1,
                1,
                new[] { "head_wizard_3", "head_special_2", "weapon_wizard_5", "weapon_warrior_6" }));
    }
}
