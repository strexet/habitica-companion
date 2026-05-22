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
        PartySnapshot? party)
    {
        var included = new List<PendingDamageSource>();
        var excluded = new List<string>();

        AddDailyDamage(tasks, included, excluded);
        AddPartyBossDamage(party, included, excluded);

        excluded.Add("Negative Habit damage is not included because pending negative Habit state is not available in saved task data.");

        var total = included.Sum(static source => source.Damage);
        return new PendingDamageEstimate(
            total,
            included,
            excluded,
            ResolveRisk(total, user.Health));
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

        var incompleteDailies = tasks.Items
            .Where(static task => task.Type == TaskType.Daily && !task.IsCompleted)
            .ToArray();
        var damage = incompleteDailies.Sum(static task => EstimateDailyDamage(task));

        included.Add(new PendingDamageSource(
            "Incomplete Dailies",
            damage,
            incompleteDailies.Length == 0
                ? "No incomplete Dailies are present in saved task data."
                : $"{incompleteDailies.Length} incomplete Daily task{(incompleteDailies.Length == 1 ? string.Empty : "s")} using local difficulty-weight estimate."));
    }

    private static void AddPartyBossDamage(
        PartySnapshot? party,
        List<PendingDamageSource> included,
        List<string> excluded)
    {
        var quest = party?.Quest;
        if (quest is null)
        {
            excluded.Add("Party boss damage is unavailable because no saved active quest exists.");
            return;
        }

        if (!quest.IsActive || quest.QuestType != PartyQuestType.Boss)
        {
            excluded.Add("Party boss damage is unavailable because the saved quest is not an active boss quest.");
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
            "Party boss pending damage",
            damage.Value,
            "Pending boss damage from saved party quest state."));
    }

    private static decimal EstimateDailyDamage(TaskSnapshot task)
    {
        return Math.Max(0m, task.Difficulty) * DailyDifficultyDamageFactor;
    }

    private static PendingDamageRisk ResolveRisk(decimal damage, decimal health)
    {
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
