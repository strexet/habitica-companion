using Bunit;
using Habitica.Application.Tasks;
using Habitica.Application.Workspace;
using Habitica.Domain.Tasks;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Components;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
namespace Habitica.WebApp.Tests.Workspace;
public sealed class TodoStatisticsPanelTests:BunitContext
{
    private static readonly DateTimeOffset At=new(2026,10,23,12,0,0,TimeSpan.Zero);
    private sealed class Clock:TimeProvider{public override DateTimeOffset GetUtcNow()=>At;}
    private async Task<(WorkspaceTaskFixture,TodoWorkspacePresentation)> Setup(WorkspaceDocument? doc=null)
    {
        JSInterop.Mode=JSRuntimeMode.Loose;Services.AddMudServices();var f=new WorkspaceTaskFixture();f.Client.Connected=true;
        if(doc is not null){f.Client.State=new(doc,doc.Revision,[],[]);await f.Controller.LoadAsync(true);}
        Services.AddSingleton<IAppSessionController>(f.Session);Services.AddSingleton(f.Controller);Services.AddSingleton(f.Database);Services.AddSingleton<IWorkspaceCloudClient>(f.Client);Services.AddSingleton<TodoTaskDraftStore>();Services.AddSingleton<TimeProvider>(new Clock());
        var todo=new TodoWorkspacePresentation(f.Session,f.Client,f.Database,f.Controller);Services.AddSingleton(todo);return(f,todo);
    }
    private static void Save(string name,string markup,string state,string test)=>Qualification.PageLayoutEvidence.Save(name,markup,"/todo/statistics",state,["charts","form","filters","tables","error"],test);
    [Fact]public async Task Period_kind_search_and_current_project_filters_keep_sources_separate_and_do_not_write()
    {
        var p=new WorkspaceProject(Guid.NewGuid(),1,"Companion Development");var task=new CompanionTask(Guid.NewGuid(),1,"Build useful reports",CompanionTaskKind.Todo,CompanionTaskState.Completed,CompanionPriority.High,At.AddDays(-20),At,ProjectId:p.Id);var habit=task with{Id=Guid.NewGuid(),Kind=CompanionTaskKind.Habit,State=CompanionTaskState.Active,Text="Read notes"};
        var doc=new WorkspaceDocument{Id=Guid.NewGuid(),Revision=5,Projects=[p],Tasks=[task,habit],Completions=[new(Guid.NewGuid(),1,task.Id,Guid.NewGuid(),At)],HabitScores=[new(Guid.NewGuid(),1,habit.Id,Guid.NewGuid(),At,HabitScoreDirection.Down)]};var(f,todo)=await Setup(doc);f.Session.SetState(f.Session.State with{TaskSnapshot=new(At,[new("native","Native task",TaskType.Todo,false,1,null,null,History:[new(At.AddDays(-2),2),new(At.AddDays(-1),4)])])});
        var cut=Render<TodoStatisticsPanel>();Save("todo-statistics-overview",cut.Markup,"default-sourced-overview",nameof(Period_kind_search_and_current_project_filters_keep_sources_separate_and_do_not_write));Assert.Contains("Task-history histogram",cut.Markup);Assert.Contains("Month activity",cut.Markup);Assert.Contains("0 confirmed of 0 requested",cut.Markup);Assert.Contains("2026-10-17 through 2026-10-23",cut.Markup);cut.Find("[data-testid=stats-period]").Change("Month");Assert.Contains("2026-09-24",cut.Markup);cut.Find("[data-testid=stats-project]").Change(p.Id.ToString());Assert.Contains("does not apply here",cut.Markup);cut.Find("[data-testid=stats-kind]").Change("Habit");Assert.Contains("Negative Habit events",cut.Markup);Assert.Empty(f.Client.PreparationIds);Assert.Empty(f.Client.AcceptedIds);
        Save("todo-statistics-data",cut.Markup,"separate-canonical-native-and-game",nameof(Period_kind_search_and_current_project_filters_keep_sources_separate_and_do_not_write));
        cut.Find("[data-testid=stats-query]").Change("no match");Assert.Contains("No retained dated activity",cut.Markup);Save("todo-statistics-empty",cut.Markup,"retained-empty-not-complete-history",nameof(Period_kind_search_and_current_project_filters_keep_sources_separate_and_do_not_write));cut.Find("[data-testid=stats-project]").Change(Guid.NewGuid().ToString());Assert.Contains("selected Companion project",cut.Markup);Assert.Contains("Current cached tasks",cut.Find("[aria-label='Habitica cache statistics']").TextContent);Save("todo-statistics-project-error",cut.Markup,"independent-source-error",nameof(Period_kind_search_and_current_project_filters_keep_sources_separate_and_do_not_write));
    }
    [Fact]public async Task Unavailable_and_account_changed_views_do_not_keep_private_filters_or_fabricated_totals()
    {
        var(f,todo)=await Setup();var cut=Render<TodoStatisticsPanel>();Assert.Contains("Companion activity is unavailable",cut.Markup);Assert.Contains("Habitica cache unavailable",cut.Markup);cut.Find("[data-testid=stats-query]").Change("private query");f.Session.SetState(f.Session.State with{UserId=Guid.NewGuid().ToString("D")});cut.WaitForAssertion(()=>Assert.DoesNotContain("private query",cut.Markup));Assert.Empty(f.Client.PreparationIds);Save("todo-statistics-unavailable",cut.Markup,"unavailable-not-zero",nameof(Unavailable_and_account_changed_views_do_not_keep_private_filters_or_fabricated_totals));
    }
    [Fact]public async Task Long_project_names_and_year_charts_keep_full_paged_progress_and_accessible_values()
    {
        var projects=Enumerable.Range(0,24).Select(i=>new WorkspaceProject(Guid.NewGuid(),1,new string('W',180)+i)).ToArray();var(f,todo)=await Setup(new WorkspaceDocument{Id=Guid.NewGuid(),Projects=projects});var cut=Render<TodoStatisticsPanel>();cut.Find("[data-testid=stats-period]").Change("Year");Assert.Equal(20,cut.FindAll(".todo-stat-progress article").Count);cut.FindAll("button").Single(b=>b.TextContent.Contains("Next progress")).Click();Assert.Equal(4,cut.FindAll(".todo-stat-progress article").Count);Assert.Contains("21–24 of 24",cut.Markup);Save("todo-statistics-long",cut.Markup,"year-long-names-paged-progress",nameof(Long_project_names_and_year_charts_keep_full_paged_progress_and_accessible_values));
    }
}
