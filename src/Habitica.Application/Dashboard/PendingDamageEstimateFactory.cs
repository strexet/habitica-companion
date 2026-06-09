using Habitica.Domain.Dashboard;
using Habitica.Domain.Party;
using Habitica.Domain.Tasks;
using Habitica.Domain.User;

namespace Habitica.Application.Dashboard;

public sealed class PendingDamageEstimateFactory
{
    private const decimal DailyDifficultyDamageFactor = 2m;

    public PendingDamageEstimate Create(
        UserSnapshot user,
        TaskCollectionSnapshot? tasks,
        PartySnapshot? party,
        string? currentUserId = null)
    {
        var included = new List<PendingDamageSource>();
        var excluded = new List<string>();

        AddDailyDamage(tasks, included, excluded);
        AddPartyBossDamage(party, currentUserId, included, excluded);

        excluded.Add("Negative Habit damage is not included because pending negative Habit state is not available in saved task data.");
        excluded.Add("Inn and paused-damage state are not included because saved account data does not expose the official CRON damage pause flag.");

        var total = included.Sum(static source => source.Damage);
        var readiness = ResolveReadiness(tasks, party, included, excluded);
        return new PendingDamageEstimate(
            total,
            Math.Max(0m, user.Health - total),
            included,
            excluded,
            ResolveRisk(total, user.Health, readiness),
            readiness);
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

    private static void AddDailyDamage(
        TaskCollectionSnapshot? tasks,
        List<PendingDamageSource> included,
        List<string> excluded)
    {
        if (tasks is null)
        {
            excluded.Add("Daily damage is unavailable because tasks are not synced yet.");
            return;
        }

        var incompleteDailies = GetIncompleteDailies(tasks);
        var unknownDailies = GetUnknownDueIncompleteDailies(tasks);
        var damage = incompleteDailies.Sum(static task => EstimateDailyDamage(task));

        included.Add(new PendingDamageSource(
            "Due Dailies",
            damage,
            incompleteDailies.Count == 0
                ? "No confirmed due unfinished Dailies are present in saved task data."
                : $"{incompleteDailies.Count} confirmed due unfinished Daily task{(incompleteDailies.Count == 1 ? string.Empty : "s")} using local difficulty-weight estimate."));
        if (unknownDailies.Count > 0)
        {
            excluded.Add($"{unknownDailies.Count} unfinished Daily task{(unknownDailies.Count == 1 ? " has" : "s have")} unknown due state and is not included in the numeric estimate.");
        }
    }

    private static void AddPartyBossDamage(
        PartySnapshot? party,
        string? currentUserId,
        List<PendingDamageSource> included,
        List<string> excluded)
    {
        if (party is null || party.Quest is null)
        {
            excluded.Add("Party boss damage is unavailable because no saved active quest exists.");
            return;
        }

        var quest = party.Quest;

        if (!quest.IsActive || quest.QuestType != PartyQuestType.Boss)
        {
            excluded.Add("Party boss damage is unavailable because the saved quest is not an active boss quest.");
            return;
        }

        if (IsCurrentUserExcludedFromQuestDamage(party, currentUserId))
        {
            excluded.Add("Party boss damage is not included because the current user is not an active quest participant or is marked as resting in the Inn.");
            return;
        }

        var damage = quest.PendingPartyDamage
            ?? (quest.ProgressDown > 0m ? quest.ProgressDown : (decimal?)null);
        if (damage is null)
        {
            excluded.Add("Party boss damage is unavailable because the synced quest state has no pending boss damage.");
            return;
        }

        included.Add(new PendingDamageSource(
            "Boss",
            damage.Value,
            "Saved active boss quest pending damage, counted once for the current user's next CRON."));
    }

    private static decimal EstimateDailyDamage(TaskSnapshot task)
    {
        return Math.Max(0m, task.Difficulty) * DailyDifficultyDamageFactor;
    }

    private static bool IsCurrentUserExcludedFromQuestDamage(PartySnapshot party, string? currentUserId)
    {
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return false;
        }

        var currentUser = party.Members.FirstOrDefault(member =>
            string.Equals(member.MemberId, currentUserId, StringComparison.Ordinal));
        if (currentUser is null)
        {
            return false;
        }

        return currentUser.IsInInn
            || currentUser.ParticipationStatus is PartyQuestParticipationStatus.Rejected or PartyQuestParticipationStatus.Pending;
    }

    private static PendingDamageReadiness ResolveReadiness(
        TaskCollectionSnapshot? tasks,
        PartySnapshot? party,
        IReadOnlyList<PendingDamageSource> included,
        IReadOnlyList<string> excluded)
    {
        if (tasks is null || GetUnknownDueIncompleteDailies(tasks).Count > 0)
        {
            return PendingDamageReadiness.Incomplete;
        }

        if (party?.Quest is { IsActive: true, QuestType: PartyQuestType.Boss }
            && !included.Any(static source => source.Label == "Boss")
            && excluded.Any(static source => source.Contains("pending boss damage", StringComparison.OrdinalIgnoreCase)))
        {
            return PendingDamageReadiness.Incomplete;
        }

        return PendingDamageReadiness.Estimated;
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
}
