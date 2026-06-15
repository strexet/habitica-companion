using Habitica.Domain.Dashboard;
using Habitica.Domain.Party;
using Habitica.Domain.Tasks;
using Habitica.Domain.User;
using Habitica.Rules.Stats;

namespace Habitica.Application.Dashboard;

public sealed class PendingDamageEstimateFactory
{
    private const decimal MaxTaskValue = 21.27m;
    private const decimal MinTaskValue = -47.27m;
    private const double TaskDeltaBase = 0.9747d;
    private const int MaxLevelForStats = 100;

    public PendingDamageEstimate Create(
        UserSnapshot user,
        TaskCollectionSnapshot? tasks,
        PartySnapshot? party,
        string? currentUserId = null,
        GearCatalogSnapshot? gearCatalog = null)
    {
        var included = new List<PendingDamageSource>();
        var excluded = new List<string>();
        var diagnostics = new List<PendingDamageDiagnostic>();

        var dailyContext = AddDailyDamage(user, tasks, gearCatalog, included, excluded, diagnostics);
        var bossContext = AddPartyBossDamage(party, currentUserId, included, excluded, diagnostics);

        excluded.Add("Negative Habit damage is not included because pending negative Habit state is not available in saved task data.");
        excluded.Add("Inn and paused-damage state are not included because saved account data does not expose the official CRON damage pause flag.");

        var total = included.Sum(static source => source.Damage);
        var readiness = ResolveReadiness(tasks, party, included, dailyContext, bossContext);
        AddEstimateSummaryDiagnostics(user, total, readiness, dailyContext, bossContext, diagnostics);
        return new PendingDamageEstimate(
            total,
            Math.Max(0m, user.Health - total),
            included,
            excluded,
            ResolveRisk(total, user.Health, readiness),
            readiness,
            dailyContext.Damage,
            bossContext.Damage,
            dailyContext.IncludedDailyCount,
            dailyContext.UnknownDueDailyCount,
            dailyContext.MissingTaskValueCount,
            dailyContext.UsesComputedConstitution,
            dailyContext.EffectiveConstitution,
            dailyContext.MissingComputedStatInputs,
            dailyContext.MissingChecklistData,
            bossContext.IsUnavailable,
            bossContext.IsDamagePausedByInn,
            diagnostics);
    }

    public static IReadOnlyList<TaskSnapshot> GetIncompleteDailies(TaskCollectionSnapshot? tasks)
    {
        return tasks?.Items
            .Where(static task => task.Type == TaskType.Daily && !task.IsCompleted && task.IsDue == true)
            .ToArray()
            ?? Array.Empty<TaskSnapshot>();
    }

    public static IReadOnlyList<TaskSnapshot> GetUnknownDueIncompleteDailies(TaskCollectionSnapshot? tasks)
    {
        return tasks?.Items
            .Where(static task => task.Type == TaskType.Daily && !task.IsCompleted && task.IsDue is null)
            .ToArray()
            ?? Array.Empty<TaskSnapshot>();
    }

    private static DailyDamageContext AddDailyDamage(
        UserSnapshot user,
        TaskCollectionSnapshot? tasks,
        GearCatalogSnapshot? gearCatalog,
        List<PendingDamageSource> included,
        List<string> excluded,
        List<PendingDamageDiagnostic> diagnostics)
    {
        if (tasks is null)
        {
            excluded.Add("Daily damage is unavailable because tasks are not synced yet.");
            return DailyDamageContext.Empty with
            {
                MissingComputedStatInputs = true
            };
        }

        var incompleteDailies = GetIncompleteDailies(tasks);
        var unknownDailies = GetUnknownDueIncompleteDailies(tasks);
        var statContext = ResolveStatContext(user, gearCatalog);
        var missingTaskValueCount = 0;
        var damage = 0m;
        foreach (var task in incompleteDailies)
        {
            if (task.Value is null)
            {
                missingTaskValueCount++;
            }

            var taskDamage = EstimateDailyDamage(task, statContext.EffectiveConstitution);
            damage += taskDamage;
            diagnostics.Add(new PendingDamageDiagnostic(
                "daily",
                task.Id,
                taskDamage,
                $"value={FormatDiagnosticDecimal(task.Value ?? 0m)}; priority={FormatDiagnosticDecimal(task.Difficulty)}; con={FormatDiagnosticDecimal(statContext.EffectiveConstitution)}"));
        }

        included.Add(new PendingDamageSource(
            "Due Dailies",
            damage,
            incompleteDailies.Count == 0
                ? "No confirmed due unfinished Dailies are present in saved task data."
                : $"{incompleteDailies.Count} confirmed due unfinished Daily task{(incompleteDailies.Count == 1 ? string.Empty : "s")} using Habitica value, priority, and Constitution damage estimate."));
        if (unknownDailies.Count > 0)
        {
            excluded.Add($"{unknownDailies.Count} unfinished Daily task{(unknownDailies.Count == 1 ? " has" : "s have")} unknown due state and is not included in the numeric estimate.");
        }

        if (missingTaskValueCount > 0)
        {
            excluded.Add($"{missingTaskValueCount} included due Daily task{(missingTaskValueCount == 1 ? " is" : "s are")} missing Habitica value data; value 0 was used for that local estimate.");
        }

        if (statContext.MissingComputedInputs)
        {
            excluded.Add("Full official Constitution modifiers are not complete because cached account or gear-catalog data is missing.");
        }

        if (incompleteDailies.Count > 0)
        {
            excluded.Add("Checklist partial-completion reductions are not available in saved task data, so included Daily damage may be higher than Habitica applies for partially checked Dailies.");
        }

        return new DailyDamageContext(
            damage,
            incompleteDailies.Count,
            unknownDailies.Count,
            missingTaskValueCount,
            statContext.EffectiveConstitution,
            !statContext.MissingComputedInputs,
            statContext.MissingComputedInputs,
            incompleteDailies.Count > 0);
    }

    private static BossDamageContext AddPartyBossDamage(
        PartySnapshot? party,
        string? currentUserId,
        List<PendingDamageSource> included,
        List<string> excluded,
        List<PendingDamageDiagnostic> diagnostics)
    {
        if (party is null || party.Quest is null)
        {
            excluded.Add("Party boss damage is unavailable because no saved active quest exists.");
            return BossDamageContext.Unavailable;
        }

        var quest = party.Quest;

        if (!quest.IsActive || quest.QuestType != PartyQuestType.Boss)
        {
            excluded.Add("Party boss damage is unavailable because the saved quest is not an active boss quest.");
            return BossDamageContext.Unavailable;
        }

        var exclusion = GetCurrentUserQuestDamageExclusion(party, currentUserId);
        if (exclusion is not null)
        {
            excluded.Add(exclusion.Value.Message);
            return BossDamageContext.Unavailable with
            {
                IsDamagePausedByInn = exclusion.Value.IsDamagePausedByInn
            };
        }

        var damage = quest.PendingPartyDamage
            ?? (quest.ProgressDown > 0m ? quest.ProgressDown : (decimal?)null);
        if (damage is null)
        {
            excluded.Add("Party boss damage is unavailable because the synced quest state has no pending boss damage.");
            return BossDamageContext.Unavailable;
        }

        included.Add(new PendingDamageSource(
            "Boss",
            damage.Value,
            "Saved active boss quest pending damage, counted once for the current user's next CRON."));
        diagnostics.Add(new PendingDamageDiagnostic(
            "boss",
            quest.Key,
            damage.Value,
            quest.PendingPartyDamage is not null ? "source=pendingPartyDamage" : "source=quest.progress.down"));

        return new BossDamageContext(damage.Value, false, false);
    }

    private static decimal EstimateDailyDamage(TaskSnapshot task, decimal effectiveConstitution)
    {
        var value = Math.Clamp(task.Value ?? 0m, MinTaskValue, MaxTaskValue);
        var delta = -(decimal)Math.Pow(TaskDeltaBase, (double)value);
        var conBonus = Math.Max(0.1m, 1m - effectiveConstitution / 250m);
        var priority = Math.Max(0m, task.Difficulty);
        var hpChange = JsRoundToOneDecimal(delta * conBonus * priority * 2m);

        return Math.Max(0m, -hpChange);
    }

    private static StatDamageContext ResolveStatContext(UserSnapshot user, GearCatalogSnapshot? gearCatalog)
    {
        var hasBaseStats = user.Stats is not null;
        var hasBuffs = user.Buffs is not null;
        var baseStats = user.Stats ?? CharacterStatsSnapshot.Zero;
        var equipmentStats = CharacterStatsCalculator.CalculateBattleGearStats(user, gearCatalog);
        var buffStats = user.Buffs ?? CharacterStatsSnapshot.Zero;
        var levelBonus = Math.Floor(Math.Min(Math.Max(user.Level, 0), MaxLevelForStats) / 2m);
        var effectiveConstitution = baseStats.Constitution + equipmentStats.Constitution + buffStats.Constitution + levelBonus;

        return new StatDamageContext(
            effectiveConstitution,
            MissingComputedInputs: !hasBaseStats || !hasBuffs || gearCatalog is null);
    }

    private static decimal JsRoundToOneDecimal(decimal value)
    {
        return Math.Floor(value * 10m + 0.5m) / 10m;
    }

    private static QuestDamageExclusion? GetCurrentUserQuestDamageExclusion(PartySnapshot party, string? currentUserId)
    {
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return null;
        }

        var currentUser = party.Members.FirstOrDefault(member =>
            string.Equals(member.MemberId, currentUserId, StringComparison.Ordinal));
        if (currentUser is null)
        {
            return null;
        }

        if (currentUser.IsInInn)
        {
            return new QuestDamageExclusion(
                "Party boss damage is not included because the current user is marked as resting in the Inn.",
                IsDamagePausedByInn: true);
        }

        return currentUser.ParticipationStatus is PartyQuestParticipationStatus.Rejected or PartyQuestParticipationStatus.Pending
            ? new QuestDamageExclusion(
                "Party boss damage is not included because the current user is not an active quest participant.",
                IsDamagePausedByInn: false)
            : null;
    }

    private static PendingDamageReadiness ResolveReadiness(
        TaskCollectionSnapshot? tasks,
        PartySnapshot? party,
        IReadOnlyList<PendingDamageSource> included,
        DailyDamageContext dailyContext,
        BossDamageContext bossContext)
    {
        if (tasks is null
            || dailyContext.UnknownDueDailyCount > 0
            || dailyContext.MissingTaskValueCount > 0
            || dailyContext.MissingComputedStatInputs
            || dailyContext.MissingChecklistData)
        {
            return PendingDamageReadiness.Incomplete;
        }

        if (party?.Quest is { IsActive: true, QuestType: PartyQuestType.Boss }
            && !included.Any(static source => source.Label == "Boss")
            && bossContext.IsUnavailable)
        {
            return PendingDamageReadiness.Incomplete;
        }

        return PendingDamageReadiness.Estimated;
    }

    private static void AddEstimateSummaryDiagnostics(
        UserSnapshot user,
        decimal total,
        PendingDamageReadiness readiness,
        DailyDamageContext dailyContext,
        BossDamageContext bossContext,
        List<PendingDamageDiagnostic> diagnostics)
    {
        diagnostics.Add(new PendingDamageDiagnostic(
            "estimate",
            "summary",
            total,
            $"hpBefore={FormatDiagnosticDecimal(user.Health)}; readiness={readiness}; includedDailies={dailyContext.IncludedDailyCount}; unknownDueDailies={dailyContext.UnknownDueDailyCount}; bossUnavailable={bossContext.IsUnavailable}"));
        diagnostics.Add(new PendingDamageDiagnostic(
            "estimate",
            "post-cron-comparison",
            0m,
            "actualHpDelta=unavailable until a post-CRON refreshed account snapshot is compared with the pre-CRON estimate"));
    }

    private static PendingDamageRisk ResolveRisk(decimal damage, decimal health, PendingDamageReadiness readiness)
    {
        if (readiness == PendingDamageReadiness.Incomplete && damage <= 0m)
        {
            return PendingDamageRisk.Info;
        }

        if (damage <= 0m)
        {
            return PendingDamageRisk.None;
        }

        if (health <= 0m || damage >= health)
        {
            return PendingDamageRisk.Danger;
        }

        return damage >= health * 0.75m
            ? PendingDamageRisk.Warning
            : PendingDamageRisk.Info;
    }

    private static string FormatDiagnosticDecimal(decimal value)
    {
        return value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed record DailyDamageContext(
        decimal Damage,
        int IncludedDailyCount,
        int UnknownDueDailyCount,
        int MissingTaskValueCount,
        decimal EffectiveConstitution,
        bool UsesComputedConstitution,
        bool MissingComputedStatInputs,
        bool MissingChecklistData)
    {
        public static DailyDamageContext Empty { get; } = new(
            0m,
            0,
            0,
            0,
            0m,
            false,
            false,
            false);
    }

    private sealed record BossDamageContext(decimal Damage, bool IsUnavailable, bool IsDamagePausedByInn)
    {
        public static BossDamageContext Unavailable { get; } = new(0m, true, false);
    }

    private sealed record StatDamageContext(decimal EffectiveConstitution, bool MissingComputedInputs);

    private readonly record struct QuestDamageExclusion(string Message, bool IsDamagePausedByInn);
}
