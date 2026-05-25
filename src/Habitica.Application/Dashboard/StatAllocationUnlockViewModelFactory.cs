using Habitica.Domain.Auth;
using Habitica.Domain.User;

namespace Habitica.Application.Dashboard;

public sealed class StatAllocationUnlockViewModelFactory
{
    public StatAllocationUnlockViewModel Create(UserSnapshot snapshot)
    {
        var isUnlocked = StatAllocationEligibility.IsUnlocked(snapshot.Level);

        return new StatAllocationUnlockViewModel(
            isUnlocked,
            snapshot.UnallocatedStatPoints,
            isUnlocked ? null : StatAllocationEligibility.GetLockedReason(snapshot.Level));
    }
}

public sealed record StatAllocationUnlockViewModel(
    bool IsUnlocked,
    int UnallocatedPoints,
    string? LockedReason)
{
    public bool HasAllocatablePoints => IsUnlocked && UnallocatedPoints > 0;
}
