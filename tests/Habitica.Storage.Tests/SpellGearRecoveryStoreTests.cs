using System.Text.Json;

namespace Habitica.Storage.Tests;

public sealed class SpellGearRecoveryStoreTests
{
    private static SpellGearRecovery Valid() => new(Guid.NewGuid(), 1,
        [new("Weapon", "weapon_warrior_1", "weapon_wizard_1", GearRecoverySlotStatus.Prepared)]);

    [Fact]
    public void JournalRetainsExactOriginalAndTemporarySlots()
    {
        var value = Valid();
        SpellGearRecoveryStore.Validate(value);
        var roundtrip = JsonSerializer.Deserialize<SpellGearRecovery>(JsonSerializer.Serialize(value))!;
        Assert.Equal(value.OperationId, roundtrip.OperationId);
        Assert.Equal(value.Slots[0], Assert.Single(roundtrip.Slots));
    }

    [Fact]
    public void InvalidOrUnboundedRecoveryFailsClosed()
    {
        var value = Valid(); var slot = value.Slots[0];
        foreach (var invalid in new[] {
            value with { OperationId = Guid.Empty }, value with { Revision = 0 },
            value with { Slots = [] }, value with { Slots = [slot, slot] },
            value with { Slots = [slot with { Slot = "arbitrary" }] },
            value with { Slots = [slot with { OriginalKey = slot.TemporaryKey }] },
            value with { Slots = [slot with { TemporaryKey = "unsafe/key" }] },
            value with { Slots = [slot with { Status = (GearRecoverySlotStatus)99 }] } })
            Assert.Throws<JsonException>(() => SpellGearRecoveryStore.Validate(invalid));
    }
}
