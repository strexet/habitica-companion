using Habitica.Application.Inventory;
using Habitica.Domain.User;

namespace Habitica.Application.Tests.Inventory;

public sealed class InventoryViewModelFactoryTests
{
    [Fact]
    public void Create_groups_owned_gear_by_slot_and_marks_equipped_items()
    {
        var factory = new InventoryViewModelFactory();
        var snapshot = new UserSnapshot(
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
                new[]
                {
                    "head_wizard_3",
                    "head_special_2",
                    "armor_wizard_4",
                    "weapon_wizard_5",
                    "weapon_warrior_6",
                    "shield_wizard_2"
                }));

        var viewModel = factory.Create(snapshot);

        var headGroup = Assert.Single(viewModel.Groups, group => group.SlotTitle == "Head");
        var weaponGroup = Assert.Single(viewModel.Groups, group => group.SlotTitle == "Weapon");

        Assert.Contains(headGroup.Items, item => item.Key == "head_wizard_3" && item.IsBattleEquipped);
        Assert.Contains(headGroup.Items, item => item.Key == "head_special_2" && item.IsCostumeEquipped);
        Assert.Equal("weapon_wizard_5", weaponGroup.BattleEquippedKey);
        Assert.Equal("weapon_special_2", weaponGroup.CostumeEquippedKey);
        Assert.Equal(new[] { "weapon_warrior_6", "weapon_wizard_5" }, weaponGroup.Items.Select(item => item.Key));
    }
}
