using Habitica.Application.Dashboard;
using Habitica.Domain.User;

namespace Habitica.Application.Tests.Dashboard;

public sealed class StatAllocationUnlockViewModelFactoryTests
{
    [Fact]
    public void Create_locks_allocation_before_level_ten()
    {
        var factory = new StatAllocationUnlockViewModelFactory();

        var viewModel = factory.Create(CreateSnapshot(level: 9, unallocatedStatPoints: 3));

        Assert.False(viewModel.IsUnlocked);
        Assert.False(viewModel.HasAllocatablePoints);
        Assert.Equal(3, viewModel.UnallocatedPoints);
        Assert.Equal("Stat allocation unlocks at level 10.", viewModel.LockedReason);
    }

    [Fact]
    public void Create_allows_allocation_at_level_ten()
    {
        var factory = new StatAllocationUnlockViewModelFactory();

        var viewModel = factory.Create(CreateSnapshot(level: 10, unallocatedStatPoints: 3));

        Assert.True(viewModel.IsUnlocked);
        Assert.True(viewModel.HasAllocatablePoints);
        Assert.Null(viewModel.LockedReason);
    }

    private static UserSnapshot CreateSnapshot(int level, int unallocatedStatPoints)
    {
        return new UserSnapshot(
            DateTimeOffset.Parse("2026-04-30T06:00:00Z"),
            "Tester",
            "warrior",
            level,
            50m,
            50m,
            0m,
            0m,
            0m,
            100m,
            10m,
            null,
            null,
            null,
            new EquipmentSnapshot(
                new GearSlotsSnapshot(null, null, null, null, null),
                new GearSlotsSnapshot(null, null, null, null, null)),
            new InventorySnapshot(0, 0, 0, 0, 0, 0, Array.Empty<string>()),
            UnallocatedStatPoints: unallocatedStatPoints);
    }
}
