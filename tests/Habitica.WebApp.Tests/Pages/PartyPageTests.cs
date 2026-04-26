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
                    new PartyQuestSnapshot("dragon", true, 12.5m, 3m, 2)),
                PartyFreshness: SnapshotFreshnessState.Fresh)));

        var cut = Render<PartyPage>();

        Assert.Contains("Party overview", cut.Markup);
        Assert.Contains("Night Owls", cut.Markup);
        Assert.Contains("Quest-focused party", cut.Markup);
        Assert.Contains("dragon", cut.Markup);
        Assert.Contains("Members", cut.Markup);
    }
}
