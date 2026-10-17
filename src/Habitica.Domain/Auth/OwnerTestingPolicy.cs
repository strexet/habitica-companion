namespace Habitica.Domain.Auth;

public static class OwnerTestingPolicy
{
    public const string SharedActionUnavailable = "Own-account testing keeps party actions and Start New Day disabled.";
    public const string SpellUnavailable = "Own-account testing allows self-only spells without another target.";

    public static bool AllowsSpell(string spellId, string? targetId = null) =>
        string.IsNullOrWhiteSpace(targetId)
        && spellId is "defensiveStance" or "frost" or "stealth" or "heal" or "brightness";
}
