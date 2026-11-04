using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Habitica.Domain.Workspace;
namespace Habitica.Application.Workspace;

public sealed record ActivityStreakFilter(Guid TaskId,DateOnly FromDay,DateTimeOffset AsOfUtc,string TimeZoneId,int Limit=20,int Offset=0,long? ExpectedRevision=null,string? ExpectedContext=null);
public sealed record ActivityStreakDay(DateOnly Day,string Status,int PositiveEvents,int NegativeEvents,string? Reason=null);
public sealed record ActivityStreakResult(Guid WorkspaceId,long Revision,Guid TaskId,string Kind,string Mode,string ModeSource,DateOnly FromDay,DateOnly ThroughDay,DateTimeOffset AsOfUtc,string TimeZoneId,string Context,int? CurrentStreak,string CurrentQuality,int LongestObserved,string LongestQuality,int PositiveDays,int NegativeDays,int ActivityDays,int PositiveEvents,int NegativeEvents,int UnknownDays,IReadOnlyList<ActivityStreakDay> Items,int Total,int? NextOffset)
{
    public int? LifetimeBest=>null;
}
public static class WorkspaceActivityStreaks
{
    public static ActivityStreakResult Evaluate(WorkspaceJournalSnapshot snapshot,ActivityStreakFilter filter)
    {
        if(!WorkspaceJournalValidation.IsValid(snapshot))throw new InvalidOperationException("activity_unavailable");
        if(filter.TaskId==Guid.Empty||filter.FromDay.Year<2||filter.FromDay.Year>9998||filter.AsOfUtc==default||filter.AsOfUtc.Offset!=TimeSpan.Zero||filter.Limit is <1 or >100||filter.Offset is <0 or >366||filter.Offset>0&&filter.ExpectedRevision is null)throw new ArgumentException("invalid_activity_filter");
        if(filter.ExpectedRevision is {} revision&&revision!=snapshot.Document.Revision)throw new InvalidOperationException("stale_revision");
        var today=RecurrenceCalendar.LocalDateAt(filter.AsOfUtc,filter.TimeZoneId);if(today.Date is not {} through)throw new ArgumentException(today.Failure??"invalid_time_zone");
        if(through.Year is <2 or >9998||through<filter.FromDay||through.DayNumber-filter.FromDay.DayNumber>=366)throw new ArgumentException("invalid_activity_range");
        var task=snapshot.Document.Tasks.FirstOrDefault(t=>t.Id==filter.TaskId);if(task is null||task.Kind==CompanionTaskKind.Todo)throw new ArgumentException("activity_task_unavailable");
        var context=Context(snapshot,filter);if(filter.ExpectedContext is not null&&filter.ExpectedContext!=context||filter.Offset>0&&filter.ExpectedContext!=context)throw new InvalidOperationException("stale_activity_context");
        var zone=TimeZoneInfo.FindSystemTimeZoneById(filter.TimeZoneId);var start=DayStart(filter.FromDay,zone);
        var intervals=start<filter.AsOfUtc?WorkspaceActivityCoverage.Read(snapshot,task.Id,start,filter.AsOfUtc):[];
        var creation=(snapshot.Document.ActivityCoverage??[]).FirstOrDefault(w=>w.TaskId==task.Id&&w.PreviousTaskRevision==0&&w.RecordedAtUtc<=filter.AsOfUtc);
        var born=creation is null?(DateOnly?)null:RecurrenceCalendar.LocalDateAt(creation.RecordedAtUtc,filter.TimeZoneId).Date;
        var occurrences=snapshot.Document.Occurrences.Where(o=>o.TaskId==task.Id).ToDictionary(o=>o.Id);
        // Reconstruct the terminal state at the explicit as-of; later corrections cannot rewrite an earlier read.
        foreach(var correction in (snapshot.Document.OccurrenceCorrections??[]).Where(c=>c.TaskId==task.Id&&c.CorrectedAtUtc>filter.AsOfUtc).OrderByDescending(c=>c.Revision))occurrences[correction.PreviousOccurrence.Id]=correction.PreviousOccurrence;
        var terminalTimes=snapshot.Commands.ToDictionary(c=>c.Command.Id,c=>c.AcceptedAtUtc);var acceptedTasks=snapshot.Commands.ToDictionary(c=>c.Command.Id,c=>c.TaskSnapshot);
        var scores=snapshot.Document.HabitScores.Where(s=>s.TaskId==task.Id&&s.RecordedAtUtc>=start&&s.RecordedAtUtc<=filter.AsOfUtc).ToLookup(s=>DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(s.RecordedAtUtc,zone).DateTime));
        var terminalDays=occurrences.Values.Where(o=>o.State!=OccurrenceState.Pending&&o.TerminalCommandId is {} id&&terminalTimes.TryGetValue(id,out var at)&&at<=filter.AsOfUtc).ToLookup(o=>o.Date);
        var observedGoal=snapshot.Commands.Where(c=>c.Command.TaskId==task.Id&&c.AcceptedAtUtc<=filter.AsOfUtc&&c.TaskSnapshot is not null).OrderByDescending(c=>c.Command.ExpectedRevision).FirstOrDefault()?.TaskSnapshot;var taskAtAsOf=observedGoal??task;
        var frequency=task.Kind==CompanionTaskKind.Habit&&taskAtAsOf.HabiticaDetails?.Up==false;var intervalIndex=0;
        var rows=new List<ActivityStreakDay>();var openUnproven=false;
        for(var day=filter.FromDay;day<=through;day=day.AddDays(1))
        {
            var from=DayStart(day,zone);var until=DayStart(day.AddDays(1),zone);var end=until<filter.AsOfUtc?until:filter.AsOfUtc;
            var positive=task.Kind==CompanionTaskKind.Habit?scores[day].Count(s=>s.Direction==HabitScoreDirection.Up):0;
            var negative=task.Kind==CompanionTaskKind.Habit?scores[day].Count(s=>s.Direction==HabitScoreDirection.Down):0;
            var terminal=terminalDays[day].OrderByDescending(o=>o.Revision).FirstOrDefault();
            if(task.Kind==CompanionTaskKind.Daily&&terminal?.TerminalCommandId is {} terminalId&&(!acceptedTasks.TryGetValue(terminalId,out var acceptedTask)||acceptedTask?.Recurrence?.TimeZoneId!=filter.TimeZoneId)){rows.Add(new(day,"unknown",0,0,"terminal_timezone_unavailable"));continue;}
            if(task.Kind==CompanionTaskKind.Daily&&terminal?.State==OccurrenceState.Completed)positive=1;
            if(positive>0||frequency&&negative>0){rows.Add(new(day,frequency?"activity":task.Kind==CompanionTaskKind.Daily?"completed":"positive",positive,negative));continue;}
            if(task.Kind==CompanionTaskKind.Daily&&terminal?.State==OccurrenceState.Skipped){rows.Add(new(day,"neutral",0,0,"explicit_skip"));continue;}
            if(from==until||born is {} birth&&day<birth){rows.Add(new(day,"neutral",0,negative,"no_eligible_day"));continue;}
            var observedFrom=creation is not null&&creation.RecordedAtUtc>from&&creation.RecordedAtUtc<end?creation.RecordedAtUtc:from;
            while(intervalIndex<intervals.Count&&intervals[intervalIndex].ThroughUtc<=observedFrom)intervalIndex++;
            var slices=new List<TaskActivityInterval>();for(var i=intervalIndex;i<intervals.Count&&intervals[i].FromUtc<end;i++)slices.Add(intervals[i]);
            string? unknown=null;var disabledGoal=false;var modes=new HashSet<bool>();var covered=observedFrom;
            foreach(var slice in slices)
            {
                if(slice.FromUtc>covered)unknown="unproven_history";
                if(slice.ThroughUtc>covered)covered=slice.ThroughUtc;
                if(!slice.IsKnown||slice.Schedule is not {} schedule){unknown=slice.UnknownReason??"unproven_history";continue;}
                if(schedule.Kind!=task.Kind){unknown="historical_kind_unavailable";continue;}
                if(schedule.IsArchived||schedule.State!=CompanionTaskState.Active){modes.Add(false);continue;}
                if(task.Kind==CompanionTaskKind.Habit){var enabled=acceptedTasks.GetValueOrDefault(schedule.CommandId)?.HabiticaDetails?.Up!=false;modes.Add(enabled);disabledGoal|=!enabled;continue;}
                if(schedule.Recurrence is not {} recurrence){unknown="schedule_unavailable";continue;}
                if(recurrence.TimeZoneId!=filter.TimeZoneId){unknown="historical_timezone_differs";continue;}
                var expanded=RecurrenceCalendar.Expand(recurrence,day,day);if(!expanded.Succeeded){unknown=expanded.Failure;continue;}modes.Add(expanded.Dates.Count>0);
            }
            if(covered<end)unknown="unproven_history";
            if(modes.Count>1)unknown="mixed_day_context";
            if(day==through){openUnproven=unknown is not null||modes.Count==0;rows.Add(new(day,"pending",0,negative,"current_day_open"));continue;}
            if(unknown is not null||modes.Count==0){rows.Add(new(day,"unknown",0,negative,unknown??"unproven_history"));continue;}
            rows.Add(new(day,modes.Contains(true)?frequency?"noObservedActivity":"missed":"neutral",0,negative,modes.Contains(true)?null:disabledGoal?"positive_goal_disabled":"not_scheduled_or_paused"));
        }
        var run=0;var knownBreak=false;var lower=false;
        for(var index=rows.Count-1;index>=0;index--){var row=rows[index];if(Success(row))run++;else if(row.Status=="missed"){knownBreak=true;break;}else if(row.Status=="unknown"){lower=true;break;}}
        if(!knownBreak&&!lower&&(born is null||born<filter.FromDay))lower=true;
        var clockUnknown=intervals.Any(i=>i.UnknownReason=="clock_order_unknown");
        int? current=frequency||clockUnknown||lower&&run==0?null:run;var quality=frequency?"notApplicable":current is null?"unavailable":lower?"lowerBound":"verified";
        var longest=0;run=0;foreach(var row in rows){if(Success(row)){run++;longest=Math.Max(longest,run);}else if(row.Status is "missed" or "unknown")run=0;}
        var unknownDays=rows.Count(r=>r.Status=="unknown");var items=rows.Skip(filter.Offset).Take(filter.Limit).ToArray();var next=filter.Offset+items.Length;
        return new(snapshot.Document.Id,snapshot.Document.Revision,task.Id,task.Kind==CompanionTaskKind.Daily?"daily":"habit",frequency?"frequency":"streak",observedGoal is null?"currentTask":"commandSnapshot",filter.FromDay,through,filter.AsOfUtc,filter.TimeZoneId,context,current,quality,longest,frequency?"notApplicable":unknownDays==0&&!openUnproven&&!clockUnknown?"verifiedInWindow":"lowerBound",rows.Count(r=>r.PositiveEvents>0),rows.Count(r=>r.NegativeEvents>0),rows.Count(r=>r.PositiveEvents+r.NegativeEvents>0),rows.Sum(r=>r.PositiveEvents),rows.Sum(r=>r.NegativeEvents),unknownDays,items,rows.Count,next<rows.Count?next:null);
    }
    private static bool Success(ActivityStreakDay row)=>row.Status is "completed" or "positive";
    private static string Context(WorkspaceJournalSnapshot snapshot,ActivityStreakFilter f)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",snapshot.Document.Id.ToString("D"),snapshot.Document.Revision.ToString(CultureInfo.InvariantCulture),f.TaskId.ToString("D"),f.FromDay.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),f.AsOfUtc.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'",CultureInfo.InvariantCulture),f.TimeZoneId)))).ToLowerInvariant();
    // Find the first instant of the civil day, including midnight gaps, folds and skipped dates.
    private static DateTimeOffset DayStart(DateOnly day,TimeZoneInfo zone)
    {
        var midnight=day.ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc);if(zone.Id=="UTC")return new(midnight);
        var low=midnight.AddHours(-36).Ticks/TimeSpan.TicksPerMillisecond;var high=midnight.AddHours(36).Ticks/TimeSpan.TicksPerMillisecond;
        while(low<high){var mid=low+(high-low)/2;var local=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(new DateTimeOffset(mid*TimeSpan.TicksPerMillisecond,TimeSpan.Zero),zone).DateTime);if(local<day)low=mid+1;else high=mid;}
        return new(low*TimeSpan.TicksPerMillisecond,TimeSpan.Zero);
    }
}
