using System.Globalization;

namespace Habitica.Domain.Party;

public static class PartyQuestProgressCalculator
{
    public static PartyQuestSnapshot Enrich(
        PartySnapshot party,
        PartyQuestSnapshot quest,
        string? currentUserId,
        DateTimeOffset nowUtc,
        TimeZoneInfo viewerTimeZone,
        bool includeStaleMembers)
    {
        var members = party.CronDashboard?.Members ?? party.Members;
        var participationSummary = BuildParticipationSummary(members);
        var currentUser = members.FirstOrDefault(member => string.Equals(member.MemberId, currentUserId, StringComparison.Ordinal));
        var includedMembers = members
            .Where(member => ShouldIncludeForEstimate(member, includeStaleMembers))
            .Where(member => IsParticipant(member, quest))
            .ToArray();
        var awaitingIncludedMembers = includedMembers
            .Where(static member => member.CronState == PartyCronState.NotCronedYet)
            .ToArray();

        if (quest.BossHealthRemaining is not null)
        {
            var pendingUserDamage = currentUser is not null && ShouldIncludeForEstimate(currentUser, includeStaleMembers) && IsParticipant(currentUser, quest)
                ? currentUser.PendingQuestDamage ?? 0m
                : 0m;
            var pendingPartyDamage = includedMembers.Sum(member => member.PendingQuestDamage ?? 0m);
            var awaitingPendingDamage = awaitingIncludedMembers.Sum(member => member.PendingQuestDamage ?? 0m);
            var estimatedRemainingHp = Math.Max(0m, quest.BossHealthRemaining.Value - awaitingPendingDamage);
            var completionEstimate = BuildCompletionEstimate(
                awaitingIncludedMembers,
                quest.BossHealthRemaining.Value,
                member => member.PendingQuestDamage,
                nowUtc,
                viewerTimeZone,
                party.CronDashboard?.IsLowConfidence == true);

            return quest with
            {
                QuestType = PartyQuestType.Boss,
                AppliedProgress = new PartyQuestMetricSnapshot(
                    "Current boss HP",
                    quest.BossHealthRemaining.Value,
                    quest.BossHealthTotal,
                    "hp"),
                PendingUserProgress = new PartyQuestMetricSnapshot(
                    "Your pending damage",
                    pendingUserDamage,
                    null,
                    "damage"),
                PendingPartyProgress = new PartyQuestMetricSnapshot(
                    "Pending party damage",
                    pendingPartyDamage,
                    null,
                    "damage"),
                EstimatedPostCronProgress = new PartyQuestMetricSnapshot(
                    "Estimated boss HP after CRON",
                    estimatedRemainingHp,
                    quest.BossHealthTotal,
                    "hp"),
                ParticipationSummary = participationSummary,
                CompletionEstimate = completionEstimate,
                TotalPendingDamage = pendingPartyDamage
            };
        }

        var appliedItems = quest.ProgressUp;
        var pendingUserItems = currentUser is not null && ShouldIncludeForEstimate(currentUser, includeStaleMembers) && IsParticipant(currentUser, quest)
            ? currentUser.PendingQuestItems ?? 0m
            : 0m;
        var pendingPartyItems = includedMembers.Sum(member => member.PendingQuestItems ?? 0m);
        var awaitingPendingItems = awaitingIncludedMembers.Sum(member => member.PendingQuestItems ?? 0m);
        var estimatedItems = appliedItems + awaitingPendingItems;
        if (quest.CollectionItemsTotal is { } collectionTotal)
        {
            estimatedItems = Math.Min(collectionTotal, estimatedItems);
        }

        return quest with
        {
            QuestType = PartyQuestType.Collection,
            AppliedProgress = new PartyQuestMetricSnapshot(
                "Current collected items",
                appliedItems,
                quest.CollectionItemsTotal,
                "items"),
            PendingUserProgress = new PartyQuestMetricSnapshot(
                "Your pending items",
                pendingUserItems,
                null,
                "items"),
            PendingPartyProgress = new PartyQuestMetricSnapshot(
                "Pending party items",
                pendingPartyItems,
                null,
                "items"),
            EstimatedPostCronProgress = new PartyQuestMetricSnapshot(
                "Estimated items after CRON",
                estimatedItems,
                quest.CollectionItemsTotal,
                "items"),
            ParticipationSummary = participationSummary,
            CompletionEstimate = BuildCompletionEstimate(
                awaitingIncludedMembers,
                quest.CollectionItemsTotal is null ? decimal.MaxValue : Math.Max(0m, quest.CollectionItemsTotal.Value - appliedItems),
                member => member.PendingQuestItems,
                nowUtc,
                viewerTimeZone,
                party.CronDashboard?.IsLowConfidence == true),
            TotalPendingCollectionItems = pendingPartyItems
        };
    }

    private static PartyQuestParticipationSummary BuildParticipationSummary(IReadOnlyList<PartyMemberSnapshot> members)
    {
        return new PartyQuestParticipationSummary(
            AcceptedCount: members.Count(static member => member.ParticipationStatus == PartyQuestParticipationStatus.Accepted),
            PendingCount: members.Count(static member => member.ParticipationStatus == PartyQuestParticipationStatus.Pending),
            RejectedCount: members.Count(static member => member.ParticipationStatus == PartyQuestParticipationStatus.Rejected),
            UnknownCount: members.Count(static member => member.ParticipationStatus == PartyQuestParticipationStatus.Unknown),
            InnCount: members.Count(static member => member.IsInInn));
    }

    private static bool IsParticipant(PartyMemberSnapshot member, PartyQuestSnapshot quest)
    {
        return quest.IsActive
            ? member.ParticipationStatus == PartyQuestParticipationStatus.Accepted
            : member.ParticipationStatus is PartyQuestParticipationStatus.Accepted or PartyQuestParticipationStatus.Pending or PartyQuestParticipationStatus.Unknown;
    }

    private static bool ShouldIncludeForEstimate(PartyMemberSnapshot member, bool includeStaleMembers)
    {
        if (member.IsInInn)
        {
            return false;
        }

        if (!includeStaleMembers && member.IsStale)
        {
            return false;
        }

        return true;
    }

    private static PartyQuestCompletionEstimate BuildCompletionEstimate(
        IReadOnlyList<PartyMemberSnapshot> members,
        decimal remainingWork,
        Func<PartyMemberSnapshot, decimal?> contributionSelector,
        DateTimeOffset nowUtc,
        TimeZoneInfo viewerTimeZone,
        bool lowHistoryConfidence)
    {
        if (remainingWork <= 0m)
        {
            return new PartyQuestCompletionEstimate(
                true,
                nowUtc,
                nowUtc,
                PartyQuestEstimateConfidence.High,
                "Already complete.");
        }

        var contributors = members
            .Select(member => new
            {
                Member = member,
                Contribution = contributionSelector(member) ?? 0m,
                ExpectedCronUtc = ResolveExpectedCronUtc(member, nowUtc, viewerTimeZone)
            })
            .Where(item => item.Contribution > 0m)
            .OrderBy(item => item.ExpectedCronUtc ?? DateTimeOffset.MaxValue)
            .ToArray();
        if (contributors.Length == 0)
        {
            return new PartyQuestCompletionEstimate(
                false,
                null,
                null,
                PartyQuestEstimateConfidence.Unknown,
                "No awaiting member progress is available.");
        }

        var totalContribution = contributors.Sum(item => item.Contribution);
        if (totalContribution < remainingWork)
        {
            return new PartyQuestCompletionEstimate(
                false,
                null,
                null,
                lowHistoryConfidence ? PartyQuestEstimateConfidence.Low : PartyQuestEstimateConfidence.Medium,
                "Awaiting CRON progress is not enough to finish quest.");
        }

        decimal cumulative = 0m;
        DateTimeOffset? earliest = null;
        DateTimeOffset? latest = null;
        var missingTimes = false;
        foreach (var contributor in contributors)
        {
            cumulative += contributor.Contribution;
            if (contributor.ExpectedCronUtc is null)
            {
                missingTimes = true;
            }

            if (cumulative >= remainingWork)
            {
                earliest = contributor.ExpectedCronUtc;
                latest = contributor.ExpectedCronUtc;
                break;
            }
        }

        if (earliest is not null)
        {
            latest = contributors
                .Select(static contributor => contributor.ExpectedCronUtc)
                .Where(static expectedCronUtc => expectedCronUtc is not null)
                .Max();
        }

        var confidence = missingTimes
            ? PartyQuestEstimateConfidence.Low
            : lowHistoryConfidence
                ? PartyQuestEstimateConfidence.Medium
                : PartyQuestEstimateConfidence.High;
        var summary = earliest is null
            ? "Pending progress can finish the quest, but exact CRON timing is unknown."
            : $"Estimated completion around {TimeZoneInfo.ConvertTime(earliest.Value, viewerTimeZone):MMM d, HH:mm}.";

        return new PartyQuestCompletionEstimate(
            true,
            earliest,
            latest,
            confidence,
            summary);
    }

    private static DateTimeOffset? ResolveExpectedCronUtc(
        PartyMemberSnapshot member,
        DateTimeOffset nowUtc,
        TimeZoneInfo viewerTimeZone)
    {
        var representativeTime = member.AverageCronTime
            ?? (member.LastCronUtc is not null
                ? TimeZoneInfo.ConvertTime(member.LastCronUtc.Value, viewerTimeZone).TimeOfDay
                : (TimeSpan?)null);
        if (representativeTime is null)
        {
            return member.CurrentHabiticaDayStartUtc;
        }

        var nowLocal = TimeZoneInfo.ConvertTime(nowUtc, viewerTimeZone);
        var candidateLocalDate = nowLocal.Date;
        var candidateLocal = candidateLocalDate + representativeTime.Value;
        if (candidateLocal <= nowLocal.DateTime)
        {
            candidateLocal = candidateLocal.AddDays(1);
        }

        var candidateUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(candidateLocal, viewerTimeZone));
        if (member.CurrentHabiticaDayStartUtc is not null && candidateUtc < member.CurrentHabiticaDayStartUtc.Value)
        {
            candidateUtc = member.CurrentHabiticaDayStartUtc.Value;
        }

        return candidateUtc;
    }
}
