using Bunit;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Pages;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Workspace;

public sealed class TodoCategoryPageTests : BunitContext
{
    private readonly DateTimeOffset _now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    private async Task<WorkspaceTaskFixture> SetupAsync()
    {
        JSInterop.Mode = JSRuntimeMode.Loose; Services.AddMudServices();
        var fixture = new WorkspaceTaskFixture(); fixture.Client.Connected = true;
        var tasks = new[]
        {
            Task("Task row", CompanionTaskKind.Todo),
            Task("Daily row", CompanionTaskKind.Daily),
            Task("Habit row", CompanionTaskKind.Habit)
        };
        var document = fixture.Client.State.Document with { Revision = 3, Tasks = tasks };
        fixture.Client.State = new(document, document.Revision, [], []);
        Services.AddSingleton<IAppSessionController>(fixture.Session);
        Services.AddSingleton<IWorkspaceCloudClient>(fixture.Client);
        Services.AddSingleton(fixture.Database);
        Services.AddSingleton(fixture.Controller);
        Services.AddSingleton<TodoTaskDraftStore>();
        Services.AddSingleton(TimeProvider.System);
        var todo = new TodoWorkspacePresentation(fixture.Session, fixture.Client, fixture.Database, fixture.Controller);
        Services.AddSingleton(todo);
        Services.AddSingleton<Habitica.Application.Notifications.IForegroundReminderBrowser>(new ReminderBrowserFixture());
        Services.AddSingleton<ForegroundReminderController>();
        await todo.RefreshSourcesAsync();
        await todo.SelectAsync(new(TodoWorkspaceSource.Cloud, document.Id));
        return fixture;
    }

    private CompanionTask Task(string text, CompanionTaskKind kind) => new(Guid.NewGuid(), 1, text, kind, CompanionTaskState.Active, CompanionPriority.Normal, _now, _now);

    [Fact]
    public async Task Habits_and_Dailies_open_their_own_immediate_capture_and_actions()
    {
        var fixture = await SetupAsync();
        var habits = Render<TodoHabitsPage>();
        Assert.Equal("page", habits.Find("a[href='/todo/habits']").GetAttribute("aria-current"));
        Assert.Null(habits.Find("[data-testid='todo-title']").Closest("details"));
        Assert.Contains("Habit row", habits.Find(".todo-direct-list").TextContent);
        Assert.DoesNotContain("Daily row", habits.Find(".todo-direct-list").TextContent);
        Assert.NotEmpty(habits.FindAll("[data-testid^='todo-up-']"));
        Assert.Contains("Record +1", habits.Find(".todo-row-actions").FirstElementChild?.TextContent);
        Assert.Empty(habits.FindAll("[data-testid='workspace-executor-enable']"));
        Qualification.PageLayoutEvidence.Save("todo-habits-main", habits.Markup, "/todo/habits", "capture-and-score-visible", ["navigation", "form", "actions"], nameof(Habits_and_Dailies_open_their_own_immediate_capture_and_actions));

        var dailies = Render<TodoDailiesPage>();
        Assert.Equal("page", dailies.Find("a[href='/todo/dailies']").GetAttribute("aria-current"));
        Assert.Null(dailies.Find("[data-testid='todo-title']").Closest("details"));
        Assert.Contains("Daily row", dailies.Find(".todo-direct-list").TextContent);
        Assert.DoesNotContain("Habit row", dailies.Find(".todo-direct-list").TextContent);
        Assert.NotEmpty(dailies.FindAll("[data-testid^='todo-complete-']"));
        Assert.Contains("Complete", dailies.Find(".todo-row-actions").Children[1].TextContent);
        Assert.Empty(dailies.FindAll("[data-testid='workspace-executor-enable']"));
        Qualification.PageLayoutEvidence.Save("todo-dailies-main", dailies.Markup, "/todo/dailies", "capture-and-complete-visible", ["navigation", "form", "actions"], nameof(Habits_and_Dailies_open_their_own_immediate_capture_and_actions));
        Assert.Empty(fixture.Client.PreparationIds);
        Assert.Empty(fixture.Client.AcceptedIds);
    }

    [Fact]
    public void Category_and_legacy_routes_keep_direct_deep_links()
    {
        AssertRoute<TodoHabitsPage>("/todo/habits");
        AssertRoute<TodoDailiesPage>("/todo/dailies");
        AssertRoute<TodoTasksPage>("/todo/tasks");
        AssertRoute<Habitica.WebApp.Pages.TodoTodayPage>("/todo/today");
        AssertRoute<TasksPage>("/tasks");
    }

    private static void AssertRoute<T>(string path) => Assert.Contains(typeof(T).GetCustomAttributes(typeof(RouteAttribute), true).Cast<RouteAttribute>(), route => route.Template == path);

    [Fact]
    public async Task Tasks_capture_and_Today_direct_link_keep_existing_working_views()
    {
        var fixture = await SetupAsync();
        var tasks = Render<TodoTasksPage>();
        Assert.Equal("page", tasks.Find("a[href='/todo/tasks']").GetAttribute("aria-current"));
        Assert.Null(tasks.Find("[data-testid='todo-title']").Closest("details"));
        Assert.NotNull(tasks.Find("[data-testid='hierarchy-branches']"));
        Qualification.PageLayoutEvidence.Save("todo-tasks-main", tasks.Markup, "/todo/tasks", "capture-and-hierarchy-visible", ["navigation", "form", "actions"], nameof(Tasks_capture_and_Today_direct_link_keep_existing_working_views));

        var today = Render<Habitica.WebApp.Pages.TodoTodayPage>();
        Assert.Equal("page", today.Find("a[href='/todo/today']").GetAttribute("aria-current"));
        Assert.NotNull(today.Find("[data-testid='today-date']"));
        Assert.Equal("/todo/tasks", today.Find("a[href='/todo/tasks']:not([aria-current])").GetAttribute("href"));
        Assert.Empty(today.FindAll("[data-testid='todo-title']"));
        Qualification.PageLayoutEvidence.Save("todo-today-main", today.Markup, "/todo/today", "day-and-direct-create-link-visible", ["navigation", "form", "actions"], nameof(Tasks_capture_and_Today_direct_link_keep_existing_working_views));
        Assert.Empty(fixture.Client.PreparationIds);
        Assert.Empty(fixture.Client.AcceptedIds);
    }
}
