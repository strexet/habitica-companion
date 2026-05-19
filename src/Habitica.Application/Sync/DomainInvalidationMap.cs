namespace Habitica.Application.Sync;

public static class DomainInvalidationMap
{
    public static IReadOnlyList<RefreshDomain> ForEquipGear { get; } = new[] { RefreshDomain.UserProfile };

    public static IReadOnlyList<RefreshDomain> ForCastSpell { get; } = new[] { RefreshDomain.UserProfile, RefreshDomain.Tasks };

    public static IReadOnlyList<RefreshDomain> ForAllocateStats { get; } = new[] { RefreshDomain.UserProfile };

    public static IReadOnlyList<RefreshDomain> ForSavePreset { get; } = Array.Empty<RefreshDomain>();
}
