using Bunit;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Components;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using System.Text.Json;
namespace Habitica.WebApp.Tests.Workspace;

public sealed class ActivityStreakStatisticsTests:BunitContext
{
    private sealed class Clock(DateTimeOffset at):TimeProvider{public override DateTimeOffset GetUtcNow()=>at;}
    private async Task<(WorkspaceTaskFixture Fixture,TodoWorkspacePresentation Todo,WorkspaceJournalSnapshot Snapshot)> Setup(string name,bool longName=false)
    {
        JSInterop.Mode=JSRuntimeMode.Loose;Services.AddMudServices();var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","activity-streaks-v1.json")));var row=json.RootElement.GetProperty("cases").EnumerateArray().Single(c=>c.GetProperty("name").GetString()==name);var state=WorkspaceJournalJson.Deserialize(row.GetProperty("snapshot").GetRawText());var asOf=DateTimeOffset.Parse(row.GetProperty("request").GetProperty("asOfUtc").GetString()!);if(longName){var command=new WorkspaceCommand(Guid.NewGuid(),state.Document.Id,state.Document.Revision,WorkspaceOperation.UpdateTask,state.Document.Tasks[0].Id,new(Text:"Long retained activity "+new string('W',180)));state=WorkspaceCommandEngine.Accept(state,command,asOf.AddMinutes(-1)).Snapshot!;}var f=new WorkspaceTaskFixture();f.Client.Connected=true;f.Client.State=state;await f.Controller.LoadAsync(true);var todo=new TodoWorkspacePresentation(f.Session,f.Client,f.Database,f.Controller);
        Services.AddSingleton<IAppSessionController>(f.Session);Services.AddSingleton(todo);Services.AddSingleton<TimeProvider>(new Clock(asOf));return(f,todo,state);
    }
    private static void Evidence(string name,string markup,string state)=>Qualification.PageLayoutEvidence.Save(name,markup,"/todo/statistics",state,["form","actions","disabled-controls","error","charts"],nameof(ActivityStreakStatisticsTests));
    [Fact]public async Task Verified_series_lower_bounds_negative_frequency_and_paging_remain_read_only()
    {
        var(f,_,state)=await Setup("habit-positive-days-deduplicate-and-negative-events-stay-separate");var cut=Render<TodoStatisticsPanel>();cut.Find("[data-testid=streak-task]").Change(state.Document.Tasks[0].Id.ToString());Assert.Contains("verified",cut.Markup);Assert.Contains("2",cut.Find("[data-testid=streak-current]").TextContent);Assert.Contains("Lifetime best is unavailable",cut.Markup);Assert.Equal(20,cut.FindAll("[data-testid=streak-days] article").Count);cut.Find("[data-testid=streak-next]").Click();Assert.Equal(10,cut.FindAll("[data-testid=streak-days] article").Count);Assert.Contains("Current day open",cut.Markup);Assert.True(cut.Find("[data-testid=streak-next]").HasAttribute("disabled"));Assert.Empty(f.Client.PreparationIds);Assert.Empty(f.Client.AcceptedIds);Evidence("activity-series-verified",cut.Markup,"verified-run-current-day-grace-paging");
        cut.Find("[data-testid=streak-zone]").Change("Mars/Olympus");Assert.Contains("unavailable",cut.Find("[data-testid=streak-error]").TextContent);Assert.Empty(cut.FindAll("[data-testid=streak-current]"));Evidence("activity-series-error",cut.Markup,"invalid-zone-no-fabricated-zero");
    }
    [Fact]public async Task Unknown_history_and_account_changes_never_preserve_a_false_zero_or_private_task()
    {
        var(f,_,state)=await Setup("legacy-positive-evidence-retains-a-lower-bound");f.Client.State=state with{Commands=[],Events=[]};await f.Controller.LoadAsync(true);var cut=Render<TodoStatisticsPanel>();cut.Find("[data-testid=streak-task]").Change(state.Document.Tasks[0].Id.ToString());Assert.Contains("lower bound",cut.Markup);Assert.Contains("historical settings are unavailable",cut.Markup);Evidence("activity-series-lower",cut.Markup,"legacy-prefix-lower-bound");cut.Find("[data-testid=streak-from]").Change("2026-10-01");cut.Find("[data-testid=streak-asof]").Change("2026-10-28T12:00");f.Session.SetState(f.Session.State with{UserId=Guid.NewGuid().ToString("D")});cut.WaitForAssertion(()=>Assert.Empty(cut.FindAll("[data-testid=streak-current]")));Assert.DoesNotContain(state.Document.Tasks[0].Id.ToString(),cut.Markup);Assert.Equal("2026-09-30",cut.Find("[data-testid=streak-from]").GetAttribute("value"));Assert.Contains("2026-10-29T12:00",cut.Find("[data-testid=streak-asof]").GetAttribute("value"));Assert.Contains("unavailable",cut.Markup);Evidence("activity-series-unavailable",cut.Markup,"account-change-private-context-cleared");
    }
    [Fact]public async Task Negative_only_habits_show_frequency_without_positive_streak_goals()
    {
        var(f,_,state)=await Setup("negative-only-habit-is-frequency-not-positive-streak");var cut=Render<TodoStatisticsPanel>();cut.Find("[data-testid=streak-task]").Change(state.Document.Tasks[0].Id.ToString());cut.Find("[data-testid=streak-from]").Change("2026-10-27");Assert.Single(cut.FindAll("[data-testid=streak-frequency]"));Assert.Empty(cut.FindAll("[data-testid=streak-current]"));Assert.Contains("Observed activity",cut.Markup);Assert.DoesNotContain("Known eligible day without completion",cut.Markup);Assert.Empty(f.Client.AcceptedIds);Evidence("activity-series-frequency",cut.Markup,"negative-only-frequency-not-a-positive-failure");
    }
    [Fact]public async Task Long_exact_task_labels_and_calendar_rows_remain_available_without_commands()
    {
        var(f,_,state)=await Setup("habit-positive-days-deduplicate-and-negative-events-stay-separate",true);var cut=Render<TodoStatisticsPanel>();cut.Find("[data-testid=streak-task]").Change(state.Document.Tasks[0].Id.ToString());cut.Find("[data-testid=streak-from]").Change("2026-10-27");Assert.Contains(new string('W',180),cut.Markup);Assert.Equal(3,cut.FindAll("[data-testid=streak-days] article").Count);Assert.Empty(f.Client.AcceptedIds);Evidence("activity-series-long",cut.Markup,"long-private-task-context-calendar-values");
    }

}
