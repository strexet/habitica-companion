using Habitica.Application.Tasks;
using Habitica.Domain.Tasks;
using Habitica.Domain.Workspace;
namespace Habitica.Application.Tests.Tasks;
public sealed class TodoStatisticsTests
{
    private static readonly DateTimeOffset Day=new(2026,10,23,12,0,0,TimeSpan.Zero);
    private static CompanionTask Task(CompanionTaskKind kind=CompanionTaskKind.Todo,CompanionTaskState state=CompanionTaskState.Active)=>new(Guid.NewGuid(),1,"Synthetic task",kind,state,CompanionPriority.Normal,Day.AddDays(-20),Day);
    [Fact]public void Restored_cycles_habit_directions_and_confirmed_effects_keep_distinct_counts_without_page_truncation()
    {
        var todo=Task(state:CompanionTaskState.Completed);var habit=Task(CompanionTaskKind.Habit);var first=new CompletionEvent(Guid.NewGuid(),2,todo.Id,Guid.NewGuid(),Day.AddDays(-2));var next=new CompletionEvent(Guid.NewGuid(),4,todo.Id,Guid.NewGuid(),Day);var restore=new TaskRestoration(Guid.NewGuid(),3,todo.Id,first.Id,Guid.NewGuid(),Day.AddDays(-1));
        var scores=Enumerable.Range(0,125).Select(i=>new HabitScoreEvent(Guid.NewGuid(),5,habit.Id,Guid.NewGuid(),Day,i%2==0?HabitScoreDirection.Up:HabitScoreDirection.Down)).ToArray();
        var requested=new WorkspaceCommandEntry(new(Guid.NewGuid(),Guid.NewGuid(),1,WorkspaceOperation.UpdateTask,todo.Id),new string('a',64),Day,ProjectionCommandState.UnknownOutcome,[ProjectionEffectKind.Update,ProjectionEffectKind.ScoreUp],1);
        var doc=new WorkspaceDocument{Id=requested.Command.WorkspaceId,Revision=10,Tasks=[todo,habit],Completions=[first,next],Restorations=[restore],HabitScores=scores};Assert.Empty(WorkspaceRules.Validate(doc));
        var result=TodoStatistics.Read(new WorkspaceJournalSnapshot(doc,10,[requested]),new(new(2026,10,23)));
        Assert.Equal(2,result.Completions);Assert.Equal(1,result.Restorations);Assert.Equal(63,result.HabitUp);Assert.Equal(62,result.HabitDown);Assert.Equal(127,result.Activity.Sum(b=>b.Value));Assert.Equal(2,result.RequestedEffects);Assert.Equal(1,result.ConfirmedEffects);Assert.Equal(1,result.UnknownCommands);
    }
    [Fact]public void Current_progress_excludes_archived_and_cancelled_todos_but_activity_retains_archived_events()
    {
        var p=new WorkspaceProject(Guid.NewGuid(),1,"Long project");var a=Task(state:CompanionTaskState.Completed) with{ProjectId=p.Id};var archived=Task(state:CompanionTaskState.Completed) with{IsArchived=true,ProjectId=p.Id};var cancelled=Task(state:CompanionTaskState.Cancelled) with{ProjectId=p.Id};var active=Task() with{ProjectId=p.Id};var g=new WorkspaceGoal(Guid.NewGuid(),1,"Goal",[a.Id,archived.Id,cancelled.Id,active.Id]);
        var doc=new WorkspaceDocument{Id=Guid.NewGuid(),Projects=[p],Goals=[g],Tasks=[a,archived,cancelled,active],Completions=[new(Guid.NewGuid(),1,a.Id,Guid.NewGuid(),Day),new(Guid.NewGuid(),1,archived.Id,Guid.NewGuid(),Day,IsArchived:true)]};Assert.Empty(WorkspaceRules.Validate(doc));var r=TodoStatistics.Read(new WorkspaceJournalSnapshot(doc,1,[]),new(new(2026,10,23)));Assert.Equal(2,r.Completions);Assert.Equal(1,r.ArchivedEvents);Assert.Equal(1,r.Projects[0].Completed);Assert.Equal(2,r.Projects[0].Eligible);Assert.Equal(1,r.Goals[0].Completed);Assert.Equal(2,r.Goals[0].Eligible);
    }
    [Fact]public void Utc_boundaries_and_missing_value_segments_do_not_invent_cross_task_delta_or_future_activity()
    {
        var filter=new StatisticsFilter(new(2026,10,23));var a=new TaskSnapshot("a","A",TaskType.Todo,false,1,null,null,History:[new(filter.FromUtc.AddTicks(-1),999),new(filter.FromUtc,2),new(filter.ThroughUtc,4),new(filter.ThroughUtc.AddTicks(1),999)]);var b=a with{Id="b",Text="B",History=[new(Day,100),new(Day.AddHours(1),103)]};var c=a with{Id="c",History=null};var h=TodoStatistics.Read(new TaskCollectionSnapshot(Day,[a,b,c]),filter);Assert.Equal(4,h.HistoryPoints);Assert.Equal(5,h.PerTaskValueMovement);Assert.Equal(2,h.TasksWithValueMovement);Assert.Equal(4,h.History.Sum(v=>v.Value));Assert.Equal(3,h.TaskCount);
        var missing=TodoStatistics.Read(new TaskCollectionSnapshot(Day,[c]),filter);Assert.Null(missing.PerTaskValueMovement);Assert.Equal(0,missing.HistoryPoints);Assert.Equal(3,TodoStatistics.Read(new TaskCollectionSnapshot(Day,[a,b,c]),filter with{ProjectId=Guid.NewGuid()}).TaskCount);
    }
    [Theory][InlineData(2024,3,1,StatisticsPeriod.Year,2023,3,2)][InlineData(2026,1,3,StatisticsPeriod.Week,2025,12,28)][InlineData(2026,3,1,StatisticsPeriod.Month,2026,1,31)]
    public void Period_ranges_are_explicit_inclusive_utc_dates(int y,int m,int d,StatisticsPeriod p,int sy,int sm,int sd){var f=new StatisticsFilter(new(y,m,d),p);Assert.Equal(new DateOnly(sy,sm,sd),f.FromDay);Assert.Equal(TimeSpan.Zero,f.FromUtc.Offset);Assert.True(f.Contains(f.ThroughUtc));Assert.False(f.Contains(f.FromUtc.AddTicks(-1)));}
    [Fact]public void Invalid_source_and_missing_project_fail_instead_of_substituting_zero()
    {var doc=new WorkspaceDocument{Id=Guid.NewGuid()};Assert.Throws<InvalidOperationException>(()=>TodoStatistics.Read(new WorkspaceJournalSnapshot(doc,1,[]),new(new(2026,10,23),ProjectId:Guid.NewGuid())));Assert.Throws<InvalidOperationException>(()=>TodoStatistics.Read(new WorkspaceJournalSnapshot(doc with{Revision=0},1,[]),new(new(2026,10,23))));Assert.Throws<ArgumentException>(()=>TodoStatistics.Read(new WorkspaceJournalSnapshot(doc,1,[]),new(new(2026,10,23),Query:new string('x',257))));}
    [Fact]public void Converted_completed_items_with_uncertain_historical_time_are_explicitly_excluded_from_period_charts()
    {
        using var fixture=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","checklist-conversion-reuse.json")));
        var options=new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web){Converters={new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase,false)}};
        var doc=System.Text.Json.JsonSerializer.Deserialize<WorkspaceDocument>(fixture.RootElement.GetProperty("acceptedDocument").GetRawText(),options)!;
        var result=TodoStatistics.Read(new WorkspaceJournalSnapshot(doc,1,[]),new(new(2026,10,23),StatisticsPeriod.Year));Assert.Equal(2,result.UnknownHistoricalTimes);Assert.Equal(doc.Completions.Count-2,result.Completions);Assert.Equal(result.Completions+result.HabitUp+result.HabitDown,result.Activity.Sum(b=>b.Value));
    }
}
