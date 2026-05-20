using Habitica.Domain.User;
using System.Text.Json.Serialization;

namespace Habitica.Domain.Party;

public sealed record PartySnapshot
{
    public PartySnapshot(
        DateTimeOffset retrievedAtUtc,
        string partyId,
        string name,
        string? summary,
        int memberCount,
        PartyQuestSnapshot? quest,
        IReadOnlyList<PartyMemberSnapshot>? members = null,
        PartyCronDashboardSnapshot? cronDashboard = null)
    {
        RetrievedAtUtc = retrievedAtUtc;
        PartyId = partyId;
        Name = name;
        Summary = summary;
        MemberCount = memberCount;
        Quest = quest;
        Members = members ?? Array.Empty<PartyMemberSnapshot>();
        CronDashboard = cronDashboard;
    }

    public DateTimeOffset RetrievedAtUtc { get; init; }

    public string PartyId { get; init; }

    public string Name { get; init; }

    public string? Summary { get; init; }

    public int MemberCount { get; init; }

    public PartyQuestSnapshot? Quest { get; init; }

    public IReadOnlyList<PartyMemberSnapshot> Members { get; init; }

    public PartyCronDashboardSnapshot? CronDashboard { get; init; }
}

public sealed record PartyQuestSnapshot(
    string? Key,
    bool IsActive,
    decimal ProgressUp,
    decimal ProgressDown,
    int ParticipantCount,
    string ProgressLabel = "Progress",
    decimal? PendingDamage = null,
    decimal? BossHealthRemaining = null,
    decimal? BossHealthTotal = null,
    decimal? TotalPendingDamage = null,
    decimal? TotalPendingCollectionItems = null,
    decimal? PendingPartyDamage = null,
    PartyQuestType QuestType = PartyQuestType.Unknown,
    decimal? CollectionItemsTotal = null,
    PartyQuestMetricSnapshot? AppliedProgress = null,
    PartyQuestMetricSnapshot? PendingUserProgress = null,
    PartyQuestMetricSnapshot? PendingPartyProgress = null,
    PartyQuestMetricSnapshot? EstimatedPostCronProgress = null,
    PartyQuestParticipationSummary? ParticipationSummary = null,
    PartyQuestCompletionEstimate? CompletionEstimate = null,
    string? Name = null,
    string? Description = null,
    IReadOnlyList<string>? RewardSummary = null)
{
    public IReadOnlyList<string> Rewards => RewardSummary ?? Array.Empty<string>();
}

public sealed record PartyQuestQueueSnapshot(
    DateTimeOffset? UpdatedAtUtc,
    IReadOnlyList<PartyQuestPoolEntry> QuestPool,
    IReadOnlyList<PartyQuestQueueEntry> Queue,
    IReadOnlyList<PartyRecentlyCompletedQuest> RecentlyCompleted);

public sealed record PartyQuestPoolEntry(
    string PartyId,
    string QuestKey,
    string QuestName,
    string OwnerUserId,
    string OwnerDisplayName,
    int AvailableCount,
    DateTimeOffset LastSeenAtUtc,
    string QuestType = "Unknown",
    IReadOnlyList<string>? RewardSummary = null)
{
    public IReadOnlyList<string> Rewards => RewardSummary ?? Array.Empty<string>();
}

public sealed record PartyQuestQueueEntry(
    string QueueItemId,
    string PartyId,
    string QuestKey,
    string QuestName,
    string OwnerUserId,
    string OwnerDisplayName,
    PartyQuestQueueStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int SortOrder,
    int? ManualPinRank,
    bool OwnerReady,
    int Version,
    IReadOnlyList<PartyQuestVote> Votes,
    IReadOnlyList<string>? RewardSummary = null,
    DateTimeOffset? SelectedAtUtc = null,
    DateTimeOffset? StartedAtUtc = null,
    DateTimeOffset? CompletedAtUtc = null)
{
    public int VoteCount => Votes.Count;

    public IReadOnlyList<string> Rewards => RewardSummary ?? Array.Empty<string>();
}

public sealed record PartyQuestVote(
    string VoterUserId,
    string VoterDisplayName,
    int VoteWeight,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc = null);

public sealed record PartyRecentlyCompletedQuest(
    string PartyId,
    string QuestKey,
    string QuestName,
    DateTimeOffset CompletedAtUtc,
    DateTimeOffset? StartedAtUtc,
    string? OwnerUserId,
    string? OwnerDisplayName,
    int? ParticipantsCount,
    IReadOnlyList<string>? RewardSummary = null,
    string? SourceQueueItemId = null,
    string? CompletedByUserId = null,
    string? CompletedByDisplayName = null,
    string? CompletionSource = null)
{
    public IReadOnlyList<string> Rewards => RewardSummary ?? Array.Empty<string>();
}

[JsonConverter(typeof(JsonStringEnumConverter<PartyQuestQueueStatus>))]
public enum PartyQuestQueueStatus
{
    Queued,
    Selected,
    InviteSent,
    Active,
    Completed,
    Skipped,
    Removed,
    Expired
}

public enum PartyCronState
{
    CronedToday,
    NotCronedYet,
    InInn,
    Unknown,
    PossiblyStale
}

public enum PartyQuestType
{
    Unknown,
    Boss,
    Collection
}

public enum PartyQuestParticipationStatus
{
    Accepted,
    Pending,
    Rejected,
    Unknown
}

public enum PartyQuestEstimateConfidence
{
    Unknown,
    Low,
    Medium,
    High
}

public enum PartyCronEventConfidence
{
    High,
    Low
}

public sealed record PartyMemberCronInput(
    string MemberId,
    string DisplayName,
    DateTimeOffset? LastCronUtc,
    int? DayStartHour,
    int? TimezoneOffsetMinutes,
    bool IsInInn = false);

public sealed record PartyMemberSnapshot(
    string MemberId,
    string DisplayName,
    DateTimeOffset? LastCronUtc,
    int? DayStartHour,
    int? TimezoneOffsetMinutes,
    PartyCronState CronState,
    string CronStateReason,
    string? CurrentHabiticaDayKey,
    DateTimeOffset? CurrentHabiticaDayStartUtc,
    TimeSpan? AverageCronTime = null,
    int AverageCronSampleCount = 0,
    decimal? PendingQuestDamage = null,
    decimal? PendingQuestItems = null,
    string? ClassName = null,
    int? Level = null,
    bool IsInInn = false,
    PartyQuestParticipationStatus ParticipationStatus = PartyQuestParticipationStatus.Unknown,
    bool IsStale = false,
    PartyMemberStatBreakdownSnapshot? Stats = null,
    DateTimeOffset? CreatedAtUtc = null,
    DateTimeOffset? LastLoggedInUtc = null,
    int? TotalLogins = null,
    IReadOnlyList<string>? EquippedGearKeys = null);

public sealed record PartyCronHistoryEvent(
    string PartyId,
    string MemberId,
    string DisplayName,
    DateTimeOffset LastCronUtc,
    string? MemberHabiticaDayKey,
    DateTimeOffset ObservedAtUtc,
    PartyCronEventConfidence Confidence);

public sealed record PartyCronHistorySnapshot(IReadOnlyList<PartyCronHistoryEvent> Events);

public sealed record PartyCronDashboardSnapshot(
    int CronedCount,
    int VisibleMemberCount,
    int UnknownCount,
    int PossiblyStaleCount,
    int HistoryDayCount,
    int SampleCount,
    bool IsLowConfidence,
    string SampleSizeWarning,
    TimeSpan? AverageBestBuffTime,
    TimeSpan? SelfFirstBuffTime,
    IReadOnlyList<PartyMemberSnapshot> Members,
    IReadOnlyList<PartyCronGraphPoint> GraphPoints,
    int AwaitingCount = 0,
    int InnCount = 0,
    int StaleCount = 0);

public sealed record PartyCronGraphPoint(
    int Hour,
    int TodayCount,
    decimal? AverageCount,
    decimal? LowerQuartileCount,
    decimal? UpperQuartileCount);

public sealed record PartyQuestMetricSnapshot(
    string Label,
    decimal Value,
    decimal? Total = null,
    string Unit = "points")
{
    public decimal? Remaining =>
        Total is null
            ? null
            : Math.Max(0m, Total.Value - Value);
}

public sealed record PartyQuestParticipationSummary(
    int AcceptedCount,
    int PendingCount,
    int RejectedCount,
    int UnknownCount,
    int InnCount);

public sealed record PartyQuestCompletionEstimate(
    bool WillCompleteAfterAwaitingCron,
    DateTimeOffset? EarliestCompletionUtc,
    DateTimeOffset? LatestCompletionUtc,
    PartyQuestEstimateConfidence Confidence,
    string Summary,
    string? FinishingMemberDisplayName = null,
    string? FinishingMemberId = null);

public sealed record PartyStatSectionSnapshot(
    decimal? Strength,
    decimal? Intelligence,
    decimal? Constitution,
    decimal? Perception)
{
    public bool HasAnyValue =>
        Strength is not null
        || Intelligence is not null
        || Constitution is not null
        || Perception is not null;

    public static PartyStatSectionSnapshot? FromCharacterStats(CharacterStatsSnapshot snapshot)
    {
        var section = new PartyStatSectionSnapshot(
            snapshot.Strength,
            snapshot.Intelligence,
            snapshot.Constitution,
            snapshot.Perception);
        return section.HasAnyValue ? section : null;
    }
}

public sealed record PartyMemberStatBreakdownSnapshot(
    PartyStatSectionSnapshot? BaseAllocated,
    PartyStatSectionSnapshot? Gear,
    PartyStatSectionSnapshot? Buffs,
    PartyStatSectionSnapshot? Total,
    PartyStatSectionSnapshot? LevelBonus = null)
{
    public bool HasAnySection =>
        BaseAllocated?.HasAnyValue == true
        || Gear?.HasAnyValue == true
        || Buffs?.HasAnyValue == true
        || Total?.HasAnyValue == true
        || LevelBonus?.HasAnyValue == true;
}

public static class PartyCronCalculator
{
    public const int StoredHistoryDays = 90;
    public const int StatisticsWindowDays = 60;
    public const int LowConfidenceDayThreshold = 7;
    public const int StaleMemberDayThreshold = 7;
    private const double FullPartyThresholdRatio = 0.9d;
    private const double PracticalThresholdRatio = 0.8d;
    private const double SelfFirstInfluenceHalfLifeMinutes = 90d;
    private const double SelfFirstDelayPenaltyMinutes = 120d;
    private const int SelfFirstMaximumDelayMinutes = 12 * 60;
    private const double AverageCronHalfLifeDays = 14d;

    public static PartyMemberSnapshot ClassifyMember(
        PartyMemberCronInput input,
        DateTimeOffset fetchedAtUtc,
        DateTimeOffset nowUtc)
    {
        if (input.IsInInn)
        {
            return BuildInnMember(input, nowUtc);
        }

        if (input.LastCronUtc is null)
        {
            return BuildUnknownMember(input, "Missing CRON timestamp.", false);
        }

        if (input.DayStartHour is null || input.TimezoneOffsetMinutes is null)
        {
            return ClassifyPublicMemberTimestamp(input, fetchedAtUtc, nowUtc);
        }

        var dayStartHour = Math.Clamp(input.DayStartHour.Value, 0, 23);
        var currentDayStartUtc = ComputeCurrentHabiticaDayStartUtc(nowUtc, dayStartHour, input.TimezoneOffsetMinutes.Value);
        var habiticaDayKey = ComputeHabiticaDayKey(nowUtc, dayStartHour, input.TimezoneOffsetMinutes.Value);
        var isStale = IsStaleMember(input.LastCronUtc, currentDayStartUtc, nowUtc);

        if (input.LastCronUtc.Value >= currentDayStartUtc)
        {
            return new PartyMemberSnapshot(
                input.MemberId,
                input.DisplayName,
                input.LastCronUtc,
                dayStartHour,
                input.TimezoneOffsetMinutes,
                PartyCronState.CronedToday,
                "Croned today.",
                habiticaDayKey,
                currentDayStartUtc,
                IsStale: isStale);
        }

        if (fetchedAtUtc < currentDayStartUtc && nowUtc >= currentDayStartUtc)
        {
            return new PartyMemberSnapshot(
                input.MemberId,
                input.DisplayName,
                input.LastCronUtc,
                dayStartHour,
                input.TimezoneOffsetMinutes,
                PartyCronState.PossiblyStale,
                "Refresh happened before this member's current Habitica day could start.",
                habiticaDayKey,
                currentDayStartUtc,
                IsStale: isStale);
        }

        return new PartyMemberSnapshot(
            input.MemberId,
            input.DisplayName,
            input.LastCronUtc,
            dayStartHour,
            input.TimezoneOffsetMinutes,
            PartyCronState.NotCronedYet,
            "Not croned yet.",
            habiticaDayKey,
            currentDayStartUtc,
            IsStale: isStale);
    }

    public static IReadOnlyList<PartyCronHistoryEvent> CreateHistoryEvents(PartySnapshot party)
    {
        return party.Members
            .Where(static member => member.LastCronUtc is not null)
            .Select(member => new PartyCronHistoryEvent(
                party.PartyId,
                member.MemberId,
                member.DisplayName,
                member.LastCronUtc!.Value.ToUniversalTime(),
                member.CurrentHabiticaDayKey,
                party.RetrievedAtUtc.ToUniversalTime(),
                member.CronState is PartyCronState.Unknown or PartyCronState.PossiblyStale
                    || member.DayStartHour is null
                    || member.TimezoneOffsetMinutes is null
                    ? PartyCronEventConfidence.Low
                    : PartyCronEventConfidence.High))
            .ToArray();
    }

    public static PartyCronDashboardSnapshot BuildDashboard(
        PartySnapshot party,
        PartyCronHistorySnapshot history,
        string currentUserId,
        DateTimeOffset nowUtc,
        TimeZoneInfo viewerTimeZone)
    {
        var visibleMembers = party.Members;
        var relevantEvents = GetRelevantEvents(party.PartyId, history, nowUtc, viewerTimeZone);
        var observedDayCount = relevantEvents
            .Select(eventEntry => ToViewerLocal(eventEntry.ObservedAtUtc, viewerTimeZone).Date)
            .Distinct()
            .Count();
        var sampleCount = relevantEvents.Count;
        var isLowConfidence = observedDayCount < LowConfidenceDayThreshold;
        var enrichedMembers = EnrichMembersWithAverages(visibleMembers, relevantEvents, nowUtc, viewerTimeZone);
        var graphPoints = BuildGraphPoints(relevantEvents, nowUtc, viewerTimeZone, observedDayCount);
        var averageBestBuffTime = ComputeAverageBestBuffTime(relevantEvents, visibleMembers.Count, nowUtc, viewerTimeZone);
        var currentUser = enrichedMembers.FirstOrDefault(member => string.Equals(member.MemberId, currentUserId, StringComparison.Ordinal));
        var selfFirstBuffTime = ComputeSelfFirstBuffTime(currentUser, enrichedMembers, averageBestBuffTime, viewerTimeZone);
        var sampleSizeWarning = BuildSampleSizeWarning(observedDayCount, sampleCount);

        return new PartyCronDashboardSnapshot(
            CronedCount: visibleMembers.Count(static member => member.CronState == PartyCronState.CronedToday),
            VisibleMemberCount: visibleMembers.Count,
            UnknownCount: visibleMembers.Count(static member => member.CronState == PartyCronState.Unknown),
            PossiblyStaleCount: visibleMembers.Count(static member => member.CronState == PartyCronState.PossiblyStale),
            HistoryDayCount: observedDayCount,
            SampleCount: sampleCount,
            IsLowConfidence: isLowConfidence,
            SampleSizeWarning: sampleSizeWarning,
            AverageBestBuffTime: averageBestBuffTime,
            SelfFirstBuffTime: selfFirstBuffTime,
            Members: enrichedMembers,
            GraphPoints: graphPoints,
            AwaitingCount: visibleMembers.Count(static member => member.CronState == PartyCronState.NotCronedYet),
            InnCount: visibleMembers.Count(static member => member.CronState == PartyCronState.InInn),
            StaleCount: visibleMembers.Count(static member => member.IsStale));
    }

    private static PartyMemberSnapshot BuildUnknownMember(PartyMemberCronInput input, string reason, bool isStale)
    {
        return new PartyMemberSnapshot(
            input.MemberId,
            input.DisplayName,
            input.LastCronUtc,
            input.DayStartHour,
            input.TimezoneOffsetMinutes,
            PartyCronState.Unknown,
            reason,
            null,
            null,
            IsInInn: input.IsInInn,
            IsStale: isStale);
    }

    private static PartyMemberSnapshot BuildInnMember(PartyMemberCronInput input, DateTimeOffset nowUtc)
    {
        DateTimeOffset? currentDayStartUtc = null;
        string? habiticaDayKey = null;
        if (input.DayStartHour is not null && input.TimezoneOffsetMinutes is not null)
        {
            var dayStartHour = Math.Clamp(input.DayStartHour.Value, 0, 23);
            currentDayStartUtc = ComputeCurrentHabiticaDayStartUtc(nowUtc, dayStartHour, input.TimezoneOffsetMinutes.Value);
            habiticaDayKey = ComputeHabiticaDayKey(nowUtc, dayStartHour, input.TimezoneOffsetMinutes.Value);
        }

        return new PartyMemberSnapshot(
            input.MemberId,
            input.DisplayName,
            input.LastCronUtc,
            input.DayStartHour,
            input.TimezoneOffsetMinutes,
            PartyCronState.InInn,
            "Member is resting in the Inn.",
            habiticaDayKey,
            currentDayStartUtc,
            IsInInn: true,
            IsStale: IsStaleMember(input.LastCronUtc, currentDayStartUtc, nowUtc));
    }

    private static PartyMemberSnapshot ClassifyPublicMemberTimestamp(
        PartyMemberCronInput input,
        DateTimeOffset fetchedAtUtc,
        DateTimeOffset nowUtc)
    {
        var currentUtcDayStart = new DateTimeOffset(nowUtc.UtcDateTime.Date, TimeSpan.Zero);
        var dayKey = currentUtcDayStart.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        const string limitedPublicDataReason = "Habitica public member data hides day start/timezone; classified from public CRON timestamp by UTC day.";
        var isStale = IsStaleMember(input.LastCronUtc, currentUtcDayStart, nowUtc);

        if (input.LastCronUtc!.Value >= currentUtcDayStart)
        {
            return new PartyMemberSnapshot(
                input.MemberId,
                input.DisplayName,
                input.LastCronUtc,
                input.DayStartHour,
                input.TimezoneOffsetMinutes,
                PartyCronState.CronedToday,
                limitedPublicDataReason,
                dayKey,
                currentUtcDayStart,
                IsStale: isStale);
        }

        if (fetchedAtUtc < currentUtcDayStart && nowUtc >= currentUtcDayStart)
        {
            return new PartyMemberSnapshot(
                input.MemberId,
                input.DisplayName,
                input.LastCronUtc,
                input.DayStartHour,
                input.TimezoneOffsetMinutes,
                PartyCronState.PossiblyStale,
                "Refresh happened before the current UTC day started; public member day start/timezone is hidden.",
                dayKey,
                currentUtcDayStart,
                IsStale: isStale);
        }

        return new PartyMemberSnapshot(
            input.MemberId,
            input.DisplayName,
            input.LastCronUtc,
            input.DayStartHour,
            input.TimezoneOffsetMinutes,
            PartyCronState.NotCronedYet,
            limitedPublicDataReason,
            dayKey,
            currentUtcDayStart,
            IsStale: isStale);
    }

    private static bool IsStaleMember(
        DateTimeOffset? lastCronUtc,
        DateTimeOffset? currentDayStartUtc,
        DateTimeOffset nowUtc)
    {
        if (lastCronUtc is null)
        {
            return false;
        }

        var referenceUtc = currentDayStartUtc?.ToUniversalTime() ?? nowUtc.ToUniversalTime();
        return lastCronUtc.Value.ToUniversalTime() < referenceUtc.AddDays(-StaleMemberDayThreshold);
    }

    private static DateTimeOffset ComputeCurrentHabiticaDayStartUtc(
        DateTimeOffset nowUtc,
        int dayStartHour,
        int timezoneOffsetMinutes)
    {
        var nowLocal = ToMemberLocalClock(nowUtc, timezoneOffsetMinutes);
        var todayStartLocal = new DateTimeOffset(
            nowLocal.Year,
            nowLocal.Month,
            nowLocal.Day,
            dayStartHour,
            0,
            0,
            nowLocal.Offset);
        var currentStartLocal = nowLocal < todayStartLocal
            ? todayStartLocal.AddDays(-1)
            : todayStartLocal;

        return currentStartLocal.ToUniversalTime();
    }

    private static string ComputeHabiticaDayKey(DateTimeOffset utcTimestamp, int dayStartHour, int timezoneOffsetMinutes)
    {
        var local = ToMemberLocalClock(utcTimestamp, timezoneOffsetMinutes).AddHours(-dayStartHour);
        return local.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset ToMemberLocalClock(DateTimeOffset utcTimestamp, int timezoneOffsetMinutes)
    {
        return utcTimestamp.ToUniversalTime().ToOffset(TimeSpan.FromMinutes(-timezoneOffsetMinutes));
    }

    private static IReadOnlyList<PartyCronHistoryEvent> GetRelevantEvents(
        string partyId,
        PartyCronHistorySnapshot history,
        DateTimeOffset nowUtc,
        TimeZoneInfo viewerTimeZone)
    {
        var cutoffUtc = nowUtc.ToUniversalTime().AddDays(-StatisticsWindowDays);
        var todayLocal = ToViewerLocal(nowUtc, viewerTimeZone).Date;

        return history.Events
            .Where(eventEntry => string.Equals(eventEntry.PartyId, partyId, StringComparison.Ordinal))
            .Where(eventEntry => eventEntry.LastCronUtc >= cutoffUtc)
            .GroupBy(eventEntry => new
            {
                eventEntry.MemberId,
                LocalDay = ToViewerLocal(eventEntry.LastCronUtc, viewerTimeZone).Date
            })
            .Select(group => group.OrderByDescending(static eventEntry => eventEntry.LastCronUtc).First())
            .Where(eventEntry => ToViewerLocal(eventEntry.LastCronUtc, viewerTimeZone).Date <= todayLocal)
            .OrderBy(static eventEntry => eventEntry.LastCronUtc)
            .ToArray();
    }

    private static IReadOnlyList<PartyMemberSnapshot> EnrichMembersWithAverages(
        IReadOnlyList<PartyMemberSnapshot> members,
        IReadOnlyList<PartyCronHistoryEvent> events,
        DateTimeOffset nowUtc,
        TimeZoneInfo viewerTimeZone)
    {
        return members
            .Select(member =>
            {
                var memberEvents = events
                    .Where(eventEntry => string.Equals(eventEntry.MemberId, member.MemberId, StringComparison.Ordinal))
                    .ToArray();
                TimeSpan? average = memberEvents.Length == 0
                    ? null
                    : WeightedCircularAverage(memberEvents, nowUtc, viewerTimeZone);

                return member with
                {
                    AverageCronTime = average,
                    AverageCronSampleCount = memberEvents.Length
                };
            })
            .ToArray();
    }

    private static IReadOnlyList<PartyCronGraphPoint> BuildGraphPoints(
        IReadOnlyList<PartyCronHistoryEvent> events,
        DateTimeOffset nowUtc,
        TimeZoneInfo viewerTimeZone,
        int observedDayCount)
    {
        if (events.Count == 0)
        {
            return Array.Empty<PartyCronGraphPoint>();
        }

        var todayLocal = ToViewerLocal(nowUtc, viewerTimeZone).Date;
        var dailyCurves = events
            .GroupBy(eventEntry => ToViewerLocal(eventEntry.LastCronUtc, viewerTimeZone).Date)
            .ToDictionary(
                static group => group.Key,
                group => BuildDailyCumulativeCounts(group.Select(eventEntry => ToViewerLocal(eventEntry.LastCronUtc, viewerTimeZone).Hour)));
        var todayCurve = dailyCurves.TryGetValue(todayLocal, out var currentDayCurve)
            ? currentDayCurve
            : new int[24];

        var eventHours = events
            .Select(eventEntry => ToViewerLocal(eventEntry.LastCronUtc, viewerTimeZone).Hour)
            .Distinct()
            .OrderBy(static hour => hour)
            .ToArray();
        var points = new List<PartyCronGraphPoint>(eventHours.Length);
        foreach (var hour in eventHours)
        {
            var values = dailyCurves.Values.Select(curve => curve[hour]).OrderBy(static value => value).ToArray();

            points.Add(new PartyCronGraphPoint(
                hour,
                todayCurve[hour],
                values.Length == 0 ? null : (decimal?)Math.Round((decimal)values.Average(), 2),
                observedDayCount < LowConfidenceDayThreshold ? null : (decimal?)Percentile(values, 0.25m),
                observedDayCount < LowConfidenceDayThreshold ? null : (decimal?)Percentile(values, 0.75m)));
        }

        return points;
    }

    private static int[] BuildDailyCumulativeCounts(IEnumerable<int> eventHours)
    {
        var counts = new int[24];
        foreach (var hour in eventHours)
        {
            if (hour is >= 0 and < 24)
            {
                counts[hour]++;
            }
        }

        var cumulative = 0;
        for (var hour = 0; hour < counts.Length; hour++)
        {
            cumulative += counts[hour];
            counts[hour] = cumulative;
        }

        return counts;
    }

    private static TimeSpan? ComputeAverageBestBuffTime(
        IReadOnlyList<PartyCronHistoryEvent> events,
        int visibleMemberCount,
        DateTimeOffset nowUtc,
        TimeZoneInfo viewerTimeZone)
    {
        if (events.Count == 0 || visibleMemberCount <= 0)
        {
            return null;
        }

        var dailyEventTimes = events
            .GroupBy(eventEntry => ToViewerLocal(eventEntry.LastCronUtc, viewerTimeZone).Date)
            .Select(group => group
                .OrderBy(eventEntry => eventEntry.LastCronUtc)
                .Select(eventEntry => ToViewerLocal(eventEntry.LastCronUtc, viewerTimeZone).TimeOfDay)
                .ToArray())
            .Where(static times => times.Length > 0)
            .ToArray();
        if (dailyEventTimes.Length == 0)
        {
            return null;
        }

        var fullThresholdDays = dailyEventTimes.Count(times => times.Length >= visibleMemberCount);
        var thresholdRatio = fullThresholdDays >= Math.Ceiling(dailyEventTimes.Length * FullPartyThresholdRatio)
            ? 1d
            : PracticalThresholdRatio;
        var threshold = Math.Max(1, (int)Math.Ceiling(visibleMemberCount * thresholdRatio));
        var practicalTimes = dailyEventTimes
            .Where(times => times.Length >= threshold)
            .Select(times => times[Math.Min(threshold, times.Length) - 1])
            .ToArray();

        return practicalTimes.Length == 0 ? null : CircularMedian(practicalTimes);
    }

    private static TimeSpan? ComputeSelfFirstBuffTime(
        PartyMemberSnapshot? currentUser,
        IReadOnlyList<PartyMemberSnapshot> members,
        TimeSpan? averageBestBuffTime,
        TimeZoneInfo viewerTimeZone)
    {
        if (currentUser is null
            || (currentUser.LastCronUtc is null && currentUser.AverageCronTime is null))
        {
            return averageBestBuffTime;
        }

        var anchor = GetSelfFirstAnchor(currentUser, averageBestBuffTime, viewerTimeZone);
        if (anchor is null)
        {
            return averageBestBuffTime;
        }

        var signalDelays = members
            .Where(member => currentUser is null || !string.Equals(member.MemberId, currentUser.MemberId, StringComparison.Ordinal))
            .Select(member => GetMemberRepresentativeCronTime(member, viewerTimeZone))
            .Where(static time => time is not null)
            .Select(time => ForwardDistanceMinutes(anchor.Value, time!.Value))
            .Where(static delay => delay is > 0 and <= SelfFirstMaximumDelayMinutes)
            .OrderBy(static delay => delay)
            .ToArray();
        var candidateDelays = signalDelays
            .Distinct()
            .ToArray();
        if (candidateDelays.Length == 0)
        {
            return TrimToMinute(anchor.Value);
        }

        var bestDelay = 0;
        var bestScore = 1d;
        foreach (var candidateDelay in candidateDelays)
        {
            var nearbyMemberWeight = signalDelays
                .Where(delay => delay <= candidateDelay)
                .Sum(SelfFirstInfluenceWeight);
            var score = 1d + nearbyMemberWeight - candidateDelay / SelfFirstDelayPenaltyMinutes;
            if (score > bestScore)
            {
                bestScore = score;
                bestDelay = candidateDelay;
            }
        }

        return MinutesToTimeSpan(MinutesOfDay(anchor.Value) + bestDelay);
    }

    private static TimeSpan? GetSelfFirstAnchor(
        PartyMemberSnapshot? currentUser,
        TimeSpan? averageBestBuffTime,
        TimeZoneInfo viewerTimeZone)
    {
        if (currentUser?.CronState == PartyCronState.CronedToday && currentUser.LastCronUtc is not null)
        {
            return ToViewerLocal(currentUser.LastCronUtc.Value, viewerTimeZone).TimeOfDay;
        }

        return currentUser?.AverageCronTime ?? averageBestBuffTime;
    }

    private static TimeSpan? GetMemberRepresentativeCronTime(
        PartyMemberSnapshot member,
        TimeZoneInfo viewerTimeZone)
    {
        if (member.CronState == PartyCronState.CronedToday && member.LastCronUtc is not null)
        {
            return ToViewerLocal(member.LastCronUtc.Value, viewerTimeZone).TimeOfDay;
        }

        return member.AverageCronTime;
    }

    private static double SelfFirstInfluenceWeight(int delayMinutes)
    {
        return Math.Pow(0.5d, delayMinutes / SelfFirstInfluenceHalfLifeMinutes);
    }

    private static string BuildSampleSizeWarning(int distinctDayCount, int sampleCount)
    {
        if (sampleCount == 0)
        {
            return "No CRON history stored yet.";
        }

        if (distinctDayCount < LowConfidenceDayThreshold)
        {
            var dayLabel = distinctDayCount == 1 ? "day" : "days";
            return $"Early estimate: based on {distinctDayCount} {dayLabel} of CRON history.";
        }

        return string.Empty;
    }

    private static DateTimeOffset ToViewerLocal(DateTimeOffset utcTimestamp, TimeZoneInfo viewerTimeZone)
    {
        return TimeZoneInfo.ConvertTime(utcTimestamp.ToUniversalTime(), viewerTimeZone);
    }

    private static TimeSpan CircularAverage(IReadOnlyList<TimeSpan> times)
    {
        var sin = 0d;
        var cos = 0d;
        foreach (var time in times)
        {
            var angle = MinutesOfDay(time) / 1440d * Math.Tau;
            sin += Math.Sin(angle);
            cos += Math.Cos(angle);
        }

        var averageAngle = Math.Atan2(sin / times.Count, cos / times.Count);
        if (averageAngle < 0)
        {
            averageAngle += Math.Tau;
        }

        return MinutesToTimeSpan((int)Math.Round(averageAngle / Math.Tau * 1440d) % 1440);
    }

    private static TimeSpan WeightedCircularAverage(
        IReadOnlyList<PartyCronHistoryEvent> events,
        DateTimeOffset nowUtc,
        TimeZoneInfo viewerTimeZone)
    {
        var sin = 0d;
        var cos = 0d;
        var totalWeight = 0d;
        foreach (var eventEntry in events)
        {
            var ageDays = Math.Max(0d, (nowUtc.ToUniversalTime() - eventEntry.LastCronUtc.ToUniversalTime()).TotalDays);
            var weight = Math.Pow(0.5d, ageDays / AverageCronHalfLifeDays);
            var time = ToViewerLocal(eventEntry.LastCronUtc, viewerTimeZone).TimeOfDay;
            var angle = MinutesOfDay(time) / 1440d * Math.Tau;
            sin += Math.Sin(angle) * weight;
            cos += Math.Cos(angle) * weight;
            totalWeight += weight;
        }

        if (totalWeight <= 0d)
        {
            return CircularAverage(events.Select(eventEntry => ToViewerLocal(eventEntry.LastCronUtc, viewerTimeZone).TimeOfDay).ToArray());
        }

        var averageAngle = Math.Atan2(sin / totalWeight, cos / totalWeight);
        if (averageAngle < 0)
        {
            averageAngle += Math.Tau;
        }

        return MinutesToTimeSpan((int)Math.Round(averageAngle / Math.Tau * 1440d) % 1440);
    }

    private static TimeSpan CircularMedian(IReadOnlyList<TimeSpan> times)
    {
        if (times.Count == 1)
        {
            return TrimToMinute(times[0]);
        }

        var average = CircularAverage(times);
        return times
            .Select(time => new
            {
                Time = TrimToMinute(time),
                Distance = CircularDistanceMinutes(time, average)
            })
            .OrderBy(static item => item.Distance)
            .ThenBy(static item => MinutesOfDay(item.Time))
            .ElementAt((times.Count - 1) / 2)
            .Time;
    }

    private static decimal Percentile(IReadOnlyList<int> values, decimal percentile)
    {
        if (values.Count == 0)
        {
            return 0m;
        }

        var index = (values.Count - 1) * percentile;
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);
        if (lower == upper)
        {
            return values[lower];
        }

        var weight = index - lower;
        return Math.Round(values[lower] + (values[upper] - values[lower]) * weight, 2);
    }

    private static int CircularDistanceMinutes(TimeSpan left, TimeSpan right)
    {
        var difference = Math.Abs(MinutesOfDay(left) - MinutesOfDay(right));
        return Math.Min(difference, 1440 - difference);
    }

    private static int ForwardDistanceMinutes(TimeSpan from, TimeSpan to)
    {
        return (MinutesOfDay(to) - MinutesOfDay(from) + 1440) % 1440;
    }

    private static int MinutesOfDay(TimeSpan time)
    {
        return (int)Math.Round(time.TotalMinutes) % 1440;
    }

    private static TimeSpan MinutesToTimeSpan(int minutes)
    {
        return TimeSpan.FromMinutes((minutes + 1440) % 1440);
    }

    private static TimeSpan TrimToMinute(TimeSpan time)
    {
        return TimeSpan.FromMinutes(MinutesOfDay(time));
    }
}
