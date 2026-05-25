namespace Habitica.Domain.Auth;

public static class StatAllocationEligibility
{
    public const int UnlockLevel = 10;

    public static bool IsUnlocked(int level)
    {
        return level >= UnlockLevel;
    }

    public static string GetLockedReason(int level)
    {
        return IsUnlocked(level)
            ? string.Empty
            : $"Stat allocation unlocks at level {UnlockLevel}.";
    }
}
