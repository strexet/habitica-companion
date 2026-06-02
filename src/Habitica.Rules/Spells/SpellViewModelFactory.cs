using Habitica.Domain.Auth;
using Habitica.Domain.Party;
using Habitica.Domain.Tasks;
using Habitica.Domain.User;
using Habitica.Rules.Stats;

namespace Habitica.Rules.Spells;

public sealed class SpellViewModelFactory
{
    private static readonly IReadOnlyList<SpellDefinition> Definitions =
    [
        new("wizard", "fireball", "Burst of Flames", 10, 11, SpellTargetKind.Task, "Deal quest damage and gain experience from a selected task.", [SpellStat.Intelligence, SpellStat.Perception]),
        new("wizard", "mpheal", "Ethereal Surge", 30, 12, SpellTargetKind.Party, "Restore mana to non-wizard party members.", [SpellStat.Intelligence]),
        new("wizard", "earth", "Earthquake", 35, 13, SpellTargetKind.Party, "Increase party Intelligence.", [SpellStat.Intelligence]),
        new("wizard", "frost", "Chilling Frost", 40, 14, SpellTargetKind.Self, "Pause daily streak reset until the next cron.", []),
        new("warrior", "smash", "Brutal Smash", 10, 11, SpellTargetKind.Task, "Damage a selected task and contribute quest damage.", [SpellStat.Strength, SpellStat.Constitution]),
        new("warrior", "defensiveStance", "Defensive Stance", 25, 12, SpellTargetKind.Self, "Increase Constitution.", [SpellStat.Constitution]),
        new("warrior", "valorousPresence", "Valorous Presence", 20, 13, SpellTargetKind.Party, "Increase party Strength.", [SpellStat.Strength]),
        new("warrior", "intimidate", "Intimidating Gaze", 15, 14, SpellTargetKind.Party, "Increase party Constitution.", [SpellStat.Constitution]),
        new("rogue", "pickPocket", "Pickpocket", 10, 11, SpellTargetKind.Task, "Gain gold from a selected task.", [SpellStat.Perception]),
        new("rogue", "backStab", "Backstab", 15, 12, SpellTargetKind.Task, "Gain experience and gold from a selected task.", [SpellStat.Strength]),
        new("rogue", "toolsOfTrade", "Tools of the Trade", 25, 13, SpellTargetKind.Party, "Increase party Perception.", [SpellStat.Perception]),
        new("rogue", "stealth", "Stealth", 45, 14, SpellTargetKind.Self, "Add stealth buffs to protect missed dailies.", [SpellStat.Perception]),
        new("healer", "heal", "Healing Light", 15, 11, SpellTargetKind.Self, "Restore your HP.", [SpellStat.Constitution, SpellStat.Intelligence]),
        new("healer", "brightness", "Searing Brightness", 15, 12, SpellTargetKind.Tasks, "Make all non-reward tasks bluer.", [SpellStat.Intelligence]),
        new("healer", "protectAura", "Protective Aura", 30, 13, SpellTargetKind.Party, "Increase party Constitution.", [SpellStat.Constitution]),
        new("healer", "healAll", "Blessing", 25, 14, SpellTargetKind.Party, "Restore party HP.", [SpellStat.Constitution, SpellStat.Intelligence])
    ];

    public SpellsPageViewModel Create(
        UserSnapshot snapshot,
        TaskCollectionSnapshot? tasks,
        GearCatalogSnapshot? catalog,
        PartySnapshot? partySnapshot = null,
        bool hasFreshUserHealth = false,
        bool hasFreshPartyHealth = false)
    {
        var className = string.IsNullOrWhiteSpace(snapshot.ClassName) ? "unknown" : snapshot.ClassName;
        var validTasks = tasks?.Items
            .Where(static task => task.Type != TaskType.Reward)
            .Where(static task => !task.IsChallengeTask)
            .Where(static task => task.Type != TaskType.Todo || !task.IsCompleted)
            .OrderByDescending(static task => GetTaskValue(task))
            .ThenBy(static task => task.Text, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? Array.Empty<TaskSnapshot>();
        var targetTasks = validTasks
            .Select(static task => new SpellTargetTaskViewModel(
                task.Id,
                task.Text,
                task.Type,
                GetTaskValue(task),
                task.Value is not null))
            .ToArray();

        var spells = Definitions
            .Where(definition => string.Equals(definition.ClassName, className, StringComparison.OrdinalIgnoreCase))
            .Select(definition => BuildSpell(snapshot, definition, targetTasks, catalog, partySnapshot, hasFreshUserHealth, hasFreshPartyHealth))
            .ToArray();

        return new SpellsPageViewModel(
            className,
            snapshot.Level,
            snapshot.Mana,
            snapshot.MaxMana,
            snapshot.UnallocatedStatPoints,
            StatAllocationEligibility.IsUnlocked(snapshot.Level),
            StatAllocationEligibility.GetLockedReason(snapshot.Level),
            snapshot.Stats ?? CharacterStatsSnapshot.Zero,
            targetTasks,
            spells);
    }

    private static SpellCardViewModel BuildSpell(
        UserSnapshot snapshot,
        SpellDefinition definition,
        IReadOnlyList<SpellTargetTaskViewModel> validTasks,
        GearCatalogSnapshot? catalog,
        PartySnapshot? partySnapshot,
        bool hasFreshUserHealth,
        bool hasFreshPartyHealth)
    {
        var selectedTask = definition.TargetKind == SpellTargetKind.Task ? validTasks.FirstOrDefault() : null;
        var description = BuildDescription(definition, selectedTask);
        var isUnlocked = snapshot.Level >= definition.UnlockLevel;
        var recommendations = BuildRecommendations(snapshot, definition, catalog);
        var targetDescriptions = validTasks.ToDictionary(
            static task => task.Id,
            task => BuildDescription(definition, task),
            StringComparer.Ordinal);
        var targetEstimates = validTasks.ToDictionary(
            static task => task.Id,
            task => EstimateEffect(snapshot, catalog, definition, task, partySnapshot, hasFreshUserHealth, hasFreshPartyHealth),
            StringComparer.Ordinal);
        var defaultEstimate = EstimateEffect(snapshot, catalog, definition, selectedTask, partySnapshot, hasFreshUserHealth, hasFreshPartyHealth);
        var recommendationsWithEstimates = recommendations
            .Select(recommendation => AddEstimateToRecommendation(snapshot, catalog, definition, selectedTask, validTasks, recommendation, partySnapshot, hasFreshUserHealth, hasFreshPartyHealth))
            .OrderByDescending(static recommendation => recommendation.EstimatedEffectScore)
            .ThenBy(static recommendation => recommendation.Name, StringComparer.Ordinal)
            .ToArray();

        return new SpellCardViewModel(
            definition.Id,
            definition.Name,
            definition.ManaCost,
            definition.UnlockLevel,
            definition.TargetKind,
            isUnlocked,
            isUnlocked ? "Available" : $"Unlocks at level {definition.UnlockLevel}",
            description,
            selectedTask?.Id,
            definition.Stats.Count > 0,
            defaultEstimate.Text,
            defaultEstimate.Values,
            targetDescriptions,
            targetEstimates.ToDictionary(pair => pair.Key, pair => pair.Value.Text, StringComparer.Ordinal),
            targetEstimates.ToDictionary(pair => pair.Key, pair => pair.Value.Values, StringComparer.Ordinal),
            recommendationsWithEstimates);
    }

    private static IReadOnlyList<SpellEquipmentRecommendation> BuildRecommendations(
        UserSnapshot snapshot,
        SpellDefinition definition,
        GearCatalogSnapshot? catalog)
    {
        if (catalog is null || definition.Stats.Count == 0)
        {
            return Array.Empty<SpellEquipmentRecommendation>();
        }

        var recommendations = new List<SpellEquipmentRecommendation>();
        foreach (var stat in definition.Stats.Distinct())
        {
            recommendations.Add(BuildRecommendation(snapshot, catalog, $"Maximize {GetStatLabel(stat)}", [stat]));
        }

        if (definition.Stats.Count > 1)
        {
            recommendations.Add(BuildRecommendation(
                snapshot,
                catalog,
                $"Balanced {string.Join("/", definition.Stats.Select(GetStatLabel))}",
                definition.Stats));
        }

        return recommendations
            .Where(static recommendation => recommendation.Slots != EmptySlots)
            .DistinctBy(static recommendation => recommendation.Name)
            .ToArray();
    }

    private static SpellEquipmentRecommendation BuildRecommendation(
        UserSnapshot snapshot,
        GearCatalogSnapshot catalog,
        string name,
        IReadOnlyList<SpellStat> stats)
    {
        var (weapon, shield) = SelectBestWeaponShieldPair(snapshot, catalog, stats);
        var slots = new GearSlotsSnapshot(
            Head: SelectBestForSlot(snapshot, catalog, "Head", stats),
            Armor: SelectBestForSlot(snapshot, catalog, "Armor", stats),
            Weapon: weapon,
            Shield: shield,
            Back: SelectBestForSlot(snapshot, catalog, "Back", stats),
            HeadAccessory: SelectBestForSlot(snapshot, catalog, "Head Accessory", stats),
            Eyewear: SelectBestForSlot(snapshot, catalog, "Eyewear", stats),
            Body: SelectBestForSlot(snapshot, catalog, "Body", stats));
        var total = ToGearStatBlock(CharacterStatsCalculator.CalculateRecommendedGearStats(snapshot, catalog, slots));

        return new SpellEquipmentRecommendation(
            name,
            slots,
            total,
            GearSlotsContainRecommendedKeys(slots, snapshot.Equipment.Battle),
            $"Prioritizes {string.Join(", ", stats.Select(GetStatLabel))}.");
    }

    private static SpellEquipmentRecommendation AddEstimateToRecommendation(
        UserSnapshot snapshot,
        GearCatalogSnapshot? catalog,
        SpellDefinition definition,
        SpellTargetTaskViewModel? selectedTask,
        IReadOnlyList<SpellTargetTaskViewModel> validTasks,
        SpellEquipmentRecommendation recommendation,
        PartySnapshot? partySnapshot,
        bool hasFreshUserHealth,
        bool hasFreshPartyHealth)
    {
        var defaultEstimate = EstimateEffect(snapshot, catalog, definition, selectedTask, partySnapshot, hasFreshUserHealth, hasFreshPartyHealth, recommendation.Slots);
        var targetEstimates = validTasks.ToDictionary(
            static task => task.Id,
            task => EstimateEffect(snapshot, catalog, definition, task, partySnapshot, hasFreshUserHealth, hasFreshPartyHealth, recommendation.Slots),
            StringComparer.Ordinal);

        return recommendation with
        {
            EstimatedEffect = defaultEstimate.Text,
            EstimatedEffectValues = defaultEstimate.Values,
            EstimatedEffectScore = defaultEstimate.Score,
            TargetEstimates = targetEstimates.ToDictionary(pair => pair.Key, pair => pair.Value.Text, StringComparer.Ordinal),
            TargetEffectValues = targetEstimates.ToDictionary(pair => pair.Key, pair => pair.Value.Values, StringComparer.Ordinal)
        };
    }

    private static string? SelectBestForSlot(
        UserSnapshot snapshot,
        GearCatalogSnapshot catalog,
        string slot,
        IReadOnlyList<SpellStat> stats,
        bool? twoHanded = null)
    {
        return snapshot.Inventory.OwnedGearKeys
            .Where(key => catalog.Items.TryGetValue(key, out var item)
                && string.Equals(item.SlotTitle, slot, StringComparison.Ordinal)
                && (twoHanded is null || item.TwoHanded == twoHanded.Value))
            .Select(key => new
            {
                Key = key,
                Stats = ToGearStatBlock(CharacterStatsCalculator.CalculateItemStats(snapshot, catalog.Items[key]))
            })
            .Select(candidate => new
            {
                candidate.Key,
                Score = Score(candidate.Stats, stats)
            })
            .Where(candidate => candidate.Score > 0m)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Key, StringComparer.Ordinal)
            .FirstOrDefault()?.Key;
    }

    private static (string? Weapon, string? Shield) SelectBestWeaponShieldPair(
        UserSnapshot snapshot,
        GearCatalogSnapshot catalog,
        IReadOnlyList<SpellStat> stats)
    {
        var oneHandedWeapon = SelectBestForSlot(snapshot, catalog, "Weapon", stats, twoHanded: false);
        var twoHandedWeapon = SelectBestForSlot(snapshot, catalog, "Weapon", stats, twoHanded: true);
        var shield = SelectBestForSlot(snapshot, catalog, "Shield", stats);

        var oneHandedScore = ScoreKey(snapshot, catalog, oneHandedWeapon, stats);
        var twoHandedScore = ScoreKey(snapshot, catalog, twoHandedWeapon, stats);
        var shieldScore = ScoreKey(snapshot, catalog, shield, stats);

        return twoHandedScore > oneHandedScore + shieldScore
            ? (twoHandedWeapon, null)
            : (oneHandedWeapon, shield);
    }

    private static decimal ScoreKey(
        UserSnapshot snapshot,
        GearCatalogSnapshot catalog,
        string? key,
        IReadOnlyList<SpellStat> stats)
    {
        if (string.IsNullOrWhiteSpace(key) || !catalog.Items.TryGetValue(key, out var item))
        {
            return 0m;
        }

        return Score(ToGearStatBlock(CharacterStatsCalculator.CalculateItemStats(snapshot, item)), stats);
    }

    private static decimal Score(GearStatBlock stats, IReadOnlyList<SpellStat> prioritizedStats)
    {
        return prioritizedStats.Sum(stat => stat switch
        {
            SpellStat.Strength => stats.Strength,
            SpellStat.Intelligence => stats.Intelligence,
            SpellStat.Constitution => stats.Constitution,
            SpellStat.Perception => stats.Perception,
            _ => 0m
        });
    }

    private static string BuildDescription(SpellDefinition definition, SpellTargetTaskViewModel? selectedTask)
    {
        return selectedTask is null
            ? definition.Description
            : $"{definition.Description} Best cached target: {selectedTask.Text} (value {selectedTask.Value:0.##}).";
    }

    private static SpellEffectEstimate EstimateEffect(
        UserSnapshot snapshot,
        GearCatalogSnapshot? catalog,
        SpellDefinition definition,
        SpellTargetTaskViewModel? selectedTask,
        PartySnapshot? partySnapshot,
        bool hasFreshUserHealth,
        bool hasFreshPartyHealth,
        GearSlotsSnapshot? battleGearOverride = null)
    {
        var baseStats = snapshot.Stats ?? CharacterStatsSnapshot.Zero;
        var equipmentStats = battleGearOverride is not null && catalog is not null
            ? CharacterStatsCalculator.CalculateRecommendedGearStats(snapshot, catalog, battleGearOverride)
            : CharacterStatsCalculator.CalculateBattleGearStats(snapshot, catalog);
        var unbuffedStats = CharacterStatsCalculator.Add(
            CharacterStatsCalculator.Add(baseStats, equipmentStats),
            CalculateLevelBonusStats(snapshot.Level));
        var stats = CharacterStatsCalculator.Add(unbuffedStats, snapshot.Buffs ?? CharacterStatsSnapshot.Zero);
        var targetValue = selectedTask?.Value ?? 0m;
        var targetName = selectedTask is null ? "the selected target" : selectedTask.Text;

        return definition.Id switch
        {
            "pickPocket" => BuildEstimate($"Best cast on {targetName}. You will gain approximately {DiminishingReturns(CalculateTaskBonus(targetValue, stats.Perception), 25m, 75m):0.##} GP.", new SpellEffectValue(DiminishingReturns(CalculateTaskBonus(targetValue, stats.Perception), 25m, 75m), "GP")),
            "backStab" => BuildEstimate($"Best cast on {targetName}. You will gain approximately {DiminishingReturns(CalculateTaskBonus(targetValue, stats.Strength), 75m, 50m):0.##} XP and {DiminishingReturns(CalculateTaskBonus(targetValue, stats.Strength), 18m, 75m):0.##} GP before possible critical hits.", new SpellEffectValue(DiminishingReturns(CalculateTaskBonus(targetValue, stats.Strength), 75m, 50m), "XP"), new SpellEffectValue(DiminishingReturns(CalculateTaskBonus(targetValue, stats.Strength), 18m, 75m), "GP")),
            "fireball" => BuildEstimate($"Best cast on {targetName}. You will gain approximately {DiminishingReturns(CalculateFireballBonus(targetValue, stats.Intelligence), 75m, 37.5m):0.##} XP and deal {Math.Ceiling(stats.Intelligence / 10m):0.##} boss damage before possible XP critical hits.", new SpellEffectValue(DiminishingReturns(CalculateFireballBonus(targetValue, stats.Intelligence), 75m, 37.5m), "XP"), new SpellEffectValue(Math.Ceiling(stats.Intelligence / 10m), "boss damage")),
            "smash" => BuildEstimate($"Best cast on {targetName}. You will add approximately {DiminishingReturns(stats.Strength, 2.5m, 35m):0.##} task value and deal {DiminishingReturns(stats.Strength, 55m, 70m):0.##} boss damage before possible critical hits.", new SpellEffectValue(DiminishingReturns(stats.Strength, 2.5m, 35m), "task value"), new SpellEffectValue(DiminishingReturns(stats.Strength, 55m, 70m), "boss damage")),
            "mpheal" => BuildEstimate($"Restores approximately {RoundSpellIncrement(DiminishingReturns(stats.Intelligence, 25m, 125m)):0.##} MP to each non-mage party member.", new SpellEffectValue(RoundSpellIncrement(DiminishingReturns(stats.Intelligence, 25m, 125m)), "MP to each non-mage party member")),
            "earth" => BuildEstimate($"Adds approximately {RoundSpellIncrement(DiminishingReturns(unbuffedStats.Intelligence, 30m, 200m)):0.##} INT to each party member.", new SpellEffectValue(RoundSpellIncrement(DiminishingReturns(unbuffedStats.Intelligence, 30m, 200m)), "INT to each party member")),
            "defensiveStance" => BuildEstimate($"Adds approximately {RoundSpellIncrement(DiminishingReturns(unbuffedStats.Constitution, 40m, 200m)):0.##} CON to you.", new SpellEffectValue(RoundSpellIncrement(DiminishingReturns(unbuffedStats.Constitution, 40m, 200m)), "CON to you")),
            "valorousPresence" => BuildEstimate($"Adds approximately {RoundSpellIncrement(DiminishingReturns(unbuffedStats.Strength, 20m, 200m)):0.##} STR to each party member.", new SpellEffectValue(RoundSpellIncrement(DiminishingReturns(unbuffedStats.Strength, 20m, 200m)), "STR to each party member")),
            "intimidate" => BuildEstimate($"Adds approximately {RoundSpellIncrement(DiminishingReturns(unbuffedStats.Constitution, 24m, 200m)):0.##} CON to each party member.", new SpellEffectValue(RoundSpellIncrement(DiminishingReturns(unbuffedStats.Constitution, 24m, 200m)), "CON to each party member")),
            "toolsOfTrade" => BuildEstimate($"Adds approximately {RoundSpellIncrement(DiminishingReturns(unbuffedStats.Perception, 100m, 50m)):0.##} PER to each party member.", new SpellEffectValue(RoundSpellIncrement(DiminishingReturns(unbuffedStats.Perception, 100m, 50m)), "PER to each party member")),
            "stealth" => BuildEstimate($"Prevents approximately {Math.Max(1m, Math.Ceiling(stats.Perception / 100m)):0.##} unfinished Dailies from causing cron damage.", new SpellEffectValue(Math.Max(1m, Math.Ceiling(stats.Perception / 100m)), "protected Dailies")),
            "heal" => BuildSelfHealEstimate(snapshot, (stats.Constitution + stats.Intelligence + 5m) * 0.075m, hasFreshUserHealth),
            "healAll" => BuildPartyHealEstimate(partySnapshot, (stats.Constitution + stats.Intelligence + 5m) * 0.04m, hasFreshPartyHealth),
            "brightness" => BuildEstimate($"Adds approximately {4m * (stats.Intelligence / (stats.Intelligence + 40m)):0.##} task value to each non-reward task.", new SpellEffectValue(4m * (stats.Intelligence / (stats.Intelligence + 40m)), "task value to each non-reward task")),
            "protectAura" => BuildEstimate($"Adds approximately {RoundSpellIncrement(DiminishingReturns(unbuffedStats.Constitution, 200m, 200m)):0.##} CON to each party member.", new SpellEffectValue(RoundSpellIncrement(DiminishingReturns(unbuffedStats.Constitution, 200m, 200m)), "CON to each party member")),
            _ => new SpellEffectEstimate("Approximate effect depends on current stats and Habitica server state.", Array.Empty<SpellEffectValue>(), 0m)
        };
    }

    private static SpellEffectEstimate BuildEstimate(string text, params SpellEffectValue[] values)
    {
        return new SpellEffectEstimate(text, values, values.Sum(static value => value.Value));
    }

    private static SpellEffectEstimate BuildSelfHealEstimate(UserSnapshot snapshot, decimal maximumHeal, bool hasFreshUserHealth)
    {
        if (!hasFreshUserHealth || snapshot.MaxHealth <= 0m)
        {
            return BuildEstimate(
                $"Restores up to approximately {maximumHeal:0.##} HP to you (theoretical maximum; fresh HP is unavailable).",
                new SpellEffectValue(maximumHeal, "maximum HP to you"));
        }

        var effectiveHeal = Math.Min(maximumHeal, Math.Max(0m, snapshot.MaxHealth - snapshot.Health));
        return BuildEstimate(
            $"Restores approximately {effectiveHeal:0.##} HP to you based on fresh HP ({maximumHeal:0.##} HP theoretical maximum before overheal).",
            new SpellEffectValue(effectiveHeal, "effective HP to you"));
    }

    private static SpellEffectEstimate BuildPartyHealEstimate(PartySnapshot? partySnapshot, decimal maximumHeal, bool hasFreshPartyHealth)
    {
        var members = hasFreshPartyHealth
            ? partySnapshot?.Members
                .Where(static member => member.Health is not null && member.MaxHealth is > 0m)
                .ToArray()
                ?? Array.Empty<PartyMemberSnapshot>()
            : Array.Empty<PartyMemberSnapshot>();
        if (members.Length == 0)
        {
            return BuildEstimate(
                $"Restores up to approximately {maximumHeal:0.##} HP to each party member (theoretical maximum; fresh party-member HP is unavailable).",
                new SpellEffectValue(maximumHeal, "maximum HP to each party member"));
        }

        var effectiveHeals = members
            .Select(member => Math.Min(maximumHeal, Math.Max(0m, member.MaxHealth!.Value - member.Health!.Value)))
            .ToArray();
        var totalHeal = effectiveHeals.Sum();
        var minimumHeal = effectiveHeals.Min();
        var maximumEffectiveHeal = effectiveHeals.Max();
        var partyMemberCount = Math.Max(
            partySnapshot?.MemberCount ?? members.Length,
            partySnapshot?.Members.Count ?? members.Length);
        var unavailableCount = Math.Max(0, partyMemberCount - members.Length);
        var coverage = unavailableCount == 0
            ? string.Empty
            : $" HP is unavailable for {FormatCount(unavailableCount, "party member")}.";
        var estimate = minimumHeal == maximumEffectiveHeal
            ? $"Restores approximately {minimumHeal:0.##} HP to each of {FormatCount(members.Length, "party member")} with fresh HP ({maximumHeal:0.##} HP maximum per member before overheal)."
            : $"Restores approximately {minimumHeal:0.##}-{maximumEffectiveHeal:0.##} HP per party member across {FormatCount(members.Length, "party member")} with fresh HP ({totalHeal:0.##} HP total; {maximumHeal:0.##} HP maximum per member before overheal).";

        return BuildEstimate(
            estimate + coverage,
            new SpellEffectValue(totalHeal, "effective party HP restored"));
    }

    private static string FormatCount(int count, string unit)
    {
        return $"{count} {unit}{(count == 1 ? string.Empty : "s")}";
    }

    private static decimal CalculateTaskBonus(decimal value, decimal stat)
    {
        return Math.Max(value, 0m) + 1m + stat * 0.5m;
    }

    private static decimal CalculateFireballBonus(decimal value, decimal intelligence)
    {
        return (Math.Max(value, 0m) + 1m) * intelligence * 0.075m;
    }

    private static decimal DiminishingReturns(decimal bonus, decimal max, decimal halfway)
    {
        return bonus <= 0m ? 0m : max * (bonus / (bonus + halfway));
    }

    private static decimal RoundSpellIncrement(decimal value)
    {
        return Math.Ceiling(value);
    }

    private static CharacterStatsSnapshot CalculateLevelBonusStats(int level)
    {
        var levelBonus = Math.Floor(Math.Min(level, 100) / 2m);
        return new CharacterStatsSnapshot(levelBonus, levelBonus, levelBonus, levelBonus);
    }

    private static bool GearSlotsContainRecommendedKeys(GearSlotsSnapshot recommendation, GearSlotsSnapshot current)
    {
        return SlotMatches(recommendation.Head, current.Head)
            && SlotMatches(recommendation.HeadAccessory, current.HeadAccessory)
            && SlotMatches(recommendation.Eyewear, current.Eyewear)
            && SlotMatches(recommendation.Armor, current.Armor)
            && SlotMatches(recommendation.Body, current.Body)
            && SlotMatches(recommendation.Weapon, current.Weapon)
            && SlotMatches(recommendation.Shield, current.Shield)
            && SlotMatches(recommendation.Back, current.Back);
    }

    private static bool SlotMatches(string? recommendedKey, string? currentKey)
    {
        return string.IsNullOrWhiteSpace(recommendedKey)
            || string.Equals(recommendedKey, currentKey, StringComparison.Ordinal);
    }

    private static decimal GetTaskValue(TaskSnapshot task)
    {
        return task.Value ?? task.Difficulty;
    }

    private static GearStatBlock ToGearStatBlock(CharacterStatsSnapshot stats)
    {
        return new GearStatBlock(stats.Strength, stats.Intelligence, stats.Constitution, stats.Perception);
    }

    private static string GetStatLabel(SpellStat stat)
    {
        return stat switch
        {
            SpellStat.Strength => "STR",
            SpellStat.Intelligence => "INT",
            SpellStat.Constitution => "CON",
            SpellStat.Perception => "PER",
            _ => stat.ToString()
        };
    }

    private static GearSlotsSnapshot EmptySlots { get; } = new(null, null, null, null, null);
}

public sealed record SpellsPageViewModel(
    string ClassName,
    int Level,
    decimal Mana,
    decimal MaxMana,
    int UnallocatedStatPoints,
    bool IsStatAllocationUnlocked,
    string StatAllocationLockedReason,
    CharacterStatsSnapshot Stats,
    IReadOnlyList<SpellTargetTaskViewModel> TargetTasks,
    IReadOnlyList<SpellCardViewModel> Spells);

public sealed record SpellCardViewModel(
    string Id,
    string Name,
    int ManaCost,
    int UnlockLevel,
    SpellTargetKind TargetKind,
    bool IsUnlocked,
    string AvailabilityLabel,
    string Description,
    string? SelectedTargetTaskId,
    bool HasStatPointContext,
    string EstimatedEffect,
    IReadOnlyList<SpellEffectValue> EstimatedEffectValues,
    IReadOnlyDictionary<string, string> TargetDescriptions,
    IReadOnlyDictionary<string, string> TargetEstimates,
    IReadOnlyDictionary<string, IReadOnlyList<SpellEffectValue>> TargetEffectValues,
    IReadOnlyList<SpellEquipmentRecommendation> EquipmentRecommendations);

public sealed record SpellEffectEstimate(
    string Text,
    IReadOnlyList<SpellEffectValue> Values,
    decimal Score);

public sealed record SpellEffectValue(
    decimal Value,
    string Unit);

public sealed record SpellTargetTaskViewModel(
    string Id,
    string Text,
    TaskType Type,
    decimal Value,
    bool HasServerValue);

public sealed record SpellEquipmentRecommendation(
    string Name,
    GearSlotsSnapshot Slots,
    GearStatBlock TotalStats,
    bool IsEquipped,
    string Rationale)
{
    public string EstimatedEffect { get; init; } = string.Empty;

    public IReadOnlyList<SpellEffectValue> EstimatedEffectValues { get; init; } = Array.Empty<SpellEffectValue>();

    public decimal EstimatedEffectScore { get; init; }

    public IReadOnlyDictionary<string, string> TargetEstimates { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, IReadOnlyList<SpellEffectValue>> TargetEffectValues { get; init; } =
        new Dictionary<string, IReadOnlyList<SpellEffectValue>>(StringComparer.Ordinal);
}

public enum SpellTargetKind
{
    Task,
    Tasks,
    Self,
    Party
}

internal sealed record SpellDefinition(
    string ClassName,
    string Id,
    string Name,
    int ManaCost,
    int UnlockLevel,
    SpellTargetKind TargetKind,
    string Description,
    IReadOnlyList<SpellStat> Stats);

internal enum SpellStat
{
    Strength,
    Intelligence,
    Constitution,
    Perception
}
