using Habitica.Domain.Tasks;
using Habitica.Domain.Workspace;
namespace Habitica.Application.Tasks;

public enum StatisticsPeriod { Week, Month, Year }
public sealed record StatisticsFilter(DateOnly ThroughDay, StatisticsPeriod Period=StatisticsPeriod.Week, CompanionTaskKind? Kind=null, string Query="",Guid? ProjectId=null)
{
    public DateOnly FromDay=>Period switch {StatisticsPeriod.Month=>ThroughDay.AddDays(-29),StatisticsPeriod.Year=>ThroughDay.AddYears(-1).AddDays(1),_=>ThroughDay.AddDays(-6)};
    public DateTimeOffset FromUtc=>new(FromDay.ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc));
    public DateTimeOffset ThroughUtc=>new(ThroughDay.ToDateTime(TimeOnly.MaxValue,DateTimeKind.Utc));
    public void Validate(){if(ThroughDay.Year<2||ThroughDay.Year>9998||!Enum.IsDefined(Period)||Kind is {} k&&!Enum.IsDefined(k)||Query is null||Query.Length>256||ProjectId==Guid.Empty)throw new ArgumentException("invalid_statistics_filter");}
    public bool Contains(DateTimeOffset date)=>date>=FromUtc&&date<=ThroughUtc;
    public bool Matches(string title,string? notes)=>Query.Length==0||title.Contains(Query,StringComparison.OrdinalIgnoreCase)||notes?.Contains(Query,StringComparison.OrdinalIgnoreCase)==true;
}
public sealed record StatisticsBucket(string Label,int Value);
public sealed record StatisticsProgress(Guid Id,string Name,int Completed,int Eligible,bool Archived);
public sealed record CanonicalStatistics(int TaskCount,int Completions,int Restorations,int HabitUp,int HabitDown,int ArchivedEvents,int UnknownHistoricalTimes,
    int RequestedEffects,int ConfirmedEffects,int UnknownCommands,int BlockedCommands,IReadOnlyList<StatisticsBucket> Activity,
    IReadOnlyList<StatisticsProgress> Projects,IReadOnlyList<StatisticsProgress> Goals);
public sealed record HabiticaStatistics(int TaskCount,int CompletedTodos,int HistoryPoints,int TasksWithHistory,decimal? PerTaskValueMovement,
    int TasksWithValueMovement,IReadOnlyList<StatisticsBucket> History,IReadOnlyList<StatisticsBucket> Months);

/// <summary>Read-only retained-data aggregates. Neither source supplies a complete lifetime activity denominator.</summary>
public static class TodoStatistics
{
    public static CanonicalStatistics Read(WorkspaceJournalSnapshot snapshot,StatisticsFilter filter)
    {
        filter.Validate();var document=snapshot.Document;if(snapshot.Commands is null||snapshot.Commands.Any(c=>c is null||c.Command is null||c.Effects is null||c.ConfirmedEffects<0||c.ConfirmedEffects>c.Effects.Count)||WorkspaceRules.Validate(document).Count!=0)throw new InvalidOperationException("statistics_unavailable");
        if(filter.ProjectId is {} project&&!document.Projects.Any(p=>p.Id==project))throw new InvalidOperationException("statistics_project_unavailable");
        var tasks=document.Tasks.Where(t=>(filter.Kind is null||t.Kind==filter.Kind)&&(filter.ProjectId is null||t.ProjectId==filter.ProjectId)&&filter.Matches(t.Text,t.Notes)).ToArray();var ids=tasks.Select(t=>t.Id).ToHashSet();
        // Conversion-only historical completion times cannot establish a real completion date.
        var uncertain=(document.ChecklistConversions??[]).SelectMany(c=>c.Items.Where(i=>i.HistoricalTimeUncertain&&i.TargetTaskId is not null).Select(i=>(Task:i.TargetTaskId!.Value,c.Revision))).GroupBy(i=>i.Task).ToDictionary(g=>g.Key,g=>g.Max(i=>i.Revision));
        var knownCommands=snapshot.Commands.Where(c=>c.Command.Operation==WorkspaceOperation.CompleteTask).Select(c=>(c.Command.Id,c.Command.TaskId)).ToHashSet();
        bool UnknownTime(CompletionEvent e)=>uncertain.TryGetValue(e.TaskId,out var revision)&&e.Revision<=revision&&!knownCommands.Contains((e.CommandId,e.TaskId));
        var allCompletions=document.Completions.Where(e=>ids.Contains(e.TaskId)).ToArray();var completions=allCompletions.Where(e=>!UnknownTime(e)&&filter.Contains(e.CompletedAtUtc)).ToArray();
        var scores=document.HabitScores.Where(e=>ids.Contains(e.TaskId)&&filter.Contains(e.RecordedAtUtc)).ToArray();
        var restored=(document.Restorations??[]).Count(e=>ids.Contains(e.TaskId)&&filter.Contains(e.RestoredAtUtc));
        var commands=snapshot.Commands.Where(c=>ids.Contains(c.Command.TaskId)&&filter.Contains(c.AcceptedAtUtc)).ToArray();
        var projects=document.Projects.Where(p=>filter.ProjectId is null||p.Id==filter.ProjectId).Select(p=>Progress(p.Id,p.Name,tasks.Where(t=>t.ProjectId==p.Id),p.IsArchived||p.IsRemoved)).ToArray();
        var goals=document.Goals.Select(g=>{var progress=WorkspaceRules.Progress(g,tasks);return new StatisticsProgress(g.Id,g.Name,progress.CompletedTodos,progress.EligibleTodos,g.IsArchived);}).ToArray();
        return new(tasks.Length,completions.Length,restored,scores.Count(e=>e.Direction==HabitScoreDirection.Up),scores.Count(e=>e.Direction==HabitScoreDirection.Down),completions.Count(e=>e.IsArchived)+scores.Count(e=>e.IsArchived),allCompletions.Count(UnknownTime),
            commands.Sum(c=>c.Effects.Count),commands.Sum(c=>c.ConfirmedEffects),commands.Count(c=>c.ProjectionState==ProjectionCommandState.UnknownOutcome),commands.Count(c=>c.ProjectionState==ProjectionCommandState.Blocked),
            Buckets(completions.Select(e=>e.CompletedAtUtc).Concat(scores.Select(e=>e.RecordedAtUtc)),filter),projects,goals);
    }
    public static HabiticaStatistics Read(TaskCollectionSnapshot snapshot,StatisticsFilter filter)
    {
        filter.Validate();if(snapshot.Items is null||snapshot.Items.Any(t=>t is null||t.Text is null||t.HistoryPoints.Any(p=>p is null||p.Date==default)))throw new InvalidOperationException("statistics_cache_unavailable");var items=snapshot.Items.Where(t=>t.Type!=TaskType.Reward&&(filter.Kind is null||Kind(t.Type)==filter.Kind)&&filter.Matches(t.Text,t.Notes)).ToArray();
        var perTask=items.Select(t=>t.HistoryPoints.Where(p=>filter.Contains(p.Date)).OrderBy(p=>p.Date).ToArray()).ToArray();var points=perTask.SelectMany(p=>p).ToArray();var movements=perTask.Where(p=>p.Length>=2).Select(p=>p[^1].Value-p[0].Value).ToArray();
        var endMonth=new DateOnly(filter.ThroughDay.Year,filter.ThroughDay.Month,1);var months=Enumerable.Range(0,6).Select(i=>endMonth.AddMonths(i-5)).ToArray();
        return new(items.Length,items.Count(t=>t.Type==TaskType.Todo&&t.IsCompleted),points.Length,perTask.Count(p=>p.Length>0),movements.Length==0?null:movements.Sum(),movements.Length,
            Buckets(points.Select(p=>p.Date),filter),months.Select(m=>new StatisticsBucket(m.ToString("yyyy-MM",System.Globalization.CultureInfo.InvariantCulture),items.Sum(t=>t.HistoryPoints.Count(p=>p.Date>=new DateTimeOffset(m.ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc))&&p.Date<new DateTimeOffset(m.AddMonths(1).ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc))&&p.Date<=filter.ThroughUtc)))).ToArray());
    }
    private static CompanionTaskKind? Kind(TaskType type)=>type switch{TaskType.Todo=>CompanionTaskKind.Todo,TaskType.Daily=>CompanionTaskKind.Daily,TaskType.Habit=>CompanionTaskKind.Habit,_=>null};
    private static StatisticsProgress Progress(Guid id,string name,IEnumerable<CompanionTask> tasks,bool archived)
    {var eligible=tasks.Where(t=>t.Kind==CompanionTaskKind.Todo&&!t.IsArchived&&t.State!=CompanionTaskState.Cancelled).ToArray();return new(id,name,eligible.Count(t=>t.State==CompanionTaskState.Completed),eligible.Length,archived);}
    private static IReadOnlyList<StatisticsBucket> Buckets(IEnumerable<DateTimeOffset> dates,StatisticsFilter filter)
    {
        var count=filter.Period switch{StatisticsPeriod.Year=>12,StatisticsPeriod.Month=>10,_=>7};var start=filter.FromUtc;var duration=filter.ThroughUtc.UtcTicks-start.UtcTicks+1;var values=new int[count];
        foreach(var date in dates){if(!filter.Contains(date))continue;var index=(int)((date.UtcTicks-start.UtcTicks)*count/duration);values[Math.Clamp(index,0,count-1)]++;}
        return Enumerable.Range(0,count).Select(i=>new StatisticsBucket(start.AddTicks(duration*i/count).ToString("MM-dd",System.Globalization.CultureInfo.InvariantCulture),values[i])).ToArray();
    }
}
