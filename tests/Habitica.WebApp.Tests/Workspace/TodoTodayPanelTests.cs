using Bunit;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Components;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Workspace;

public sealed class TodoTodayPanelTests : BunitContext
{
    private static readonly DateOnly Monday = new(2026, 10, 19);
    private static readonly DateTimeOffset Created = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    private static CompanionTask Task(string text, CompanionTaskKind kind = CompanionTaskKind.Todo, RecurrenceRule? rule = null, DateOnly? planned = null, DateOnly? deadline = null)
        => new(Guid.NewGuid(), 1, text, kind, CompanionTaskState.Active, CompanionPriority.Normal, Created, Created, PlannedDate: planned, Deadline: deadline, Recurrence: rule);
    private static RecurrenceRule Weekly(params DayOfWeek[] days) => new(RecurrenceFrequency.Weekly, 1, new(2026, 1, 1), "UTC", days, []);
    private async Task<(WorkspaceTaskFixture Fixture, TodoWorkspacePresentation Todo)> Setup(WorkspaceDocument document, TimeProvider? clock = null)
    {
        JSInterop.Mode = JSRuntimeMode.Loose; Services.AddMudServices();
        var f = new WorkspaceTaskFixture(); f.Client.Connected = true;
        f.Client.State = new(document, document.Revision, [], []); await f.Controller.LoadAsync(true);
        Services.AddSingleton<IAppSessionController>(f.Session); Services.AddSingleton<IWorkspaceCloudClient>(f.Client);
        Services.AddSingleton(f.Database); Services.AddSingleton(f.Controller);
        var todo = new TodoWorkspacePresentation(f.Session, f.Client, f.Database, f.Controller);
        Services.AddSingleton(todo); Services.AddSingleton<TodoTaskDraftStore>(); Services.AddSingleton(clock ?? TimeProvider.System);
        Services.AddSingleton<Habitica.Application.Notifications.IForegroundReminderBrowser>(new ReminderBrowserFixture());
        Services.AddSingleton<ForegroundReminderController>();
        return (f, todo);
    }
    private static WorkspaceDocument Document(params CompanionTask[] tasks) => new() { Id = Guid.NewGuid(), Revision = 10, Tasks = tasks };
    private static void Save(string name, string markup, string state, string test) => Qualification.PageLayoutEvidence.Save(name, markup, "/todo/today", state, ["form", "actions", "confirmation", "disabled-controls", "error"], test);

    [Fact]
    public async Task Groups_unique_planned_deadline_and_due_rows_and_preserves_recorded_and_inactive_states_without_writes()
    {
        var planned = Task("Planned deadline", planned: Monday, deadline: Monday);
        var weekly = Task("Weekly Monday", CompanionTaskKind.Daily, Weekly(DayOfWeek.Monday));
        var monthly = Task("Monthly nineteenth", CompanionTaskKind.Daily, new(RecurrenceFrequency.Monthly, 1, new(2026, 1, 1), "UTC", [], [19]));
        var unscheduled = Task("Unscheduled Daily", CompanionTaskKind.Daily);
        var future = Task("Tuesday only", CompanionTaskKind.Daily, Weekly(DayOfWeek.Tuesday));
        var info = unscheduled with { Id = Guid.NewGuid(), Text = "Unscheduled planned", PlannedDate = Monday };
        var paused = weekly with { Id = Guid.NewGuid(), Text = "Paused", State = CompanionTaskState.Paused, PlannedDate = Monday };
        var skipped = weekly with { Id = Guid.NewGuid(), Text = "Skipped" };
        var completed = weekly with { Id = Guid.NewGuid(), Text = "Completed" };
        var archived = weekly with { Id = Guid.NewGuid(), Text = "Archived", IsArchived = true };
        var done = new TaskOccurrence(Guid.NewGuid(), 1, completed.Id, Monday, OccurrenceState.Completed);
        var doc = Document(planned, weekly, monthly, unscheduled, future, info, paused, skipped, completed, archived) with
        {
            Occurrences = [new(Guid.NewGuid(), 1, skipped.Id, Monday, OccurrenceState.Skipped), done],
            Completions = [new(Guid.NewGuid(), 1, completed.Id, Guid.NewGuid(), Created, done.Id)]
        };
        var (f, todo) = await Setup(doc); var before = WorkspaceJournalJson.Serialize(f.Client.State);
        var cut = Render<TodoTodayPanel>(p => p.Add(x => x.Day, Monday));
        var view = TodoTodayProjection.Read(todo, Monday);
        Assert.Empty(cut.FindAll("[data-testid='todo-title']"));
        Assert.Equal(7, view.Total); Assert.Equal(7, view.Rows.Select(row => row.Item.TaskId).Distinct().Count());
        var item = Assert.Single(view.Rows, row => row.Item.TaskId == planned.Id); Assert.True(item.Item.Planned && item.Item.Deadline);
        Assert.Equal(2, view.Rows.Count(row => row.Group == TodoTodayGroup.Dailies));
        Assert.Equal(2, view.Rows.Count(row => row.Group == TodoTodayGroup.Recorded));
        Assert.False(view.Rows.Single(row => row.Item.TaskId == info.Id).CanAct);
        Assert.True(cut.Find($"[data-testid='today-complete-{info.Id:D}']").HasAttribute("disabled"));
        Assert.DoesNotContain("Tuesday only", cut.Find("[aria-label='Tasks for the selected day']").TextContent);
        Assert.Equal(before, WorkspaceJournalJson.Serialize(f.Client.State)); Assert.Empty(f.Client.PreparationIds);
        Save("todo-today-groups", cut.Markup, "planned-due-recorded-inactive", nameof(Groups_unique_planned_deadline_and_due_rows_and_preserves_recorded_and_inactive_states_without_writes));
    }

    [Fact]
    public async Task Daily_action_reviews_exact_source_date_and_original_receipt_recovers_without_duplicate_occurrences()
    {
        var daily = Task("Exact occurrence", CompanionTaskKind.Daily, Weekly(DayOfWeek.Monday));
        var (f, _) = await Setup(Document(daily)); var cut = Render<TodoTodayPanel>(p => p.Add(x => x.Day, Monday));
        cut.Find($"[data-testid='today-complete-{daily.Id:D}']").Click();
        Assert.Contains(JSInterop.Invocations, call => call.Identifier == "Blazor._internal.domWrapper.focus");
        Assert.Equal(Monday, f.Controller.Preview!.Command.OccurrenceDate); Assert.Equal(daily.Id, f.Controller.Preview.Command.TaskId);
        Assert.Equal(f.Client.State.Document.Id, f.Controller.Preview.Command.WorkspaceId); Assert.True(f.Controller.Preview.MayAffectHabitica);
        var original = f.Controller.Preview.PreparedCommandId; f.Client.LoseAcceptance = true;
        Save("todo-today-review", cut.Markup, "exact-occurrence-review", nameof(Daily_action_reviews_exact_source_date_and_original_receipt_recovers_without_duplicate_occurrences));
        cut.Find("[data-testid='todo-confirm']").Click(); Assert.True(f.Controller.AcceptanceUnknown);
        Assert.True(cut.Find($"[data-testid='today-complete-{daily.Id:D}']").HasAttribute("disabled"));
        cut.Find("[data-testid='todo-recover']").Click();
        Assert.Equal(original, f.Controller.LastReceipt!.CommandId); Assert.Single(f.Client.AcceptedIds);
        Assert.Single(f.Controller.Snapshot!.Document.Occurrences); Assert.Single(f.Controller.Snapshot.Document.Completions);
        Assert.Contains("Already recorded", cut.Markup); Assert.True(cut.Find($"[data-testid='today-complete-{daily.Id:D}']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Civil_dates_cover_weekly_monthly_and_dst_boundaries_without_utc_day_or_everyday_inference()
    {
        var sunday = Task("DST Sunday", CompanionTaskKind.Daily, Weekly(DayOfWeek.Sunday) with { TimeZoneId = "America/New_York" });
        var monthly = Task("Month end", CompanionTaskKind.Daily, new(RecurrenceFrequency.Monthly, 1, new(2026, 1, 1), "UTC", [], [31]));
        var (f, todo) = await Setup(Document(sunday, monthly), new FixedClock(new(2026, 11, 1, 3, 30, 0, TimeSpan.Zero), "America/New_York"));
        var cut = Render<TodoTodayPanel>();
        Assert.Equal("2026-10-31", cut.Find("[data-testid='today-date']").GetAttribute("value"));
        Assert.Single(TodoTodayProjection.Read(todo, new(2026, 10, 31)).Rows);
        Assert.Equal(sunday.Id, Assert.Single(TodoTodayProjection.Read(todo, new(2026, 11, 1)).Rows).Item.TaskId);
        Assert.Empty(TodoTodayProjection.Read(todo, new(2026, 11, 30)).Rows);
        cut.Find("[data-testid='today-date']").Change("2026-11-01");
        Assert.Single(cut.FindAll($"[data-testid='today-row-{sunday.Id:D}']")); Assert.Contains("America/New_York", cut.Markup);
        Assert.Empty(f.Client.PreparationIds);
    }

    [Fact]
    public async Task Missing_timezone_fails_the_whole_view_without_partial_tasks_and_invalid_dates_never_prepare()
    {
        var invalid = Task("Unavailable schedule", CompanionTaskKind.Daily, Weekly(DayOfWeek.Monday) with { TimeZoneId = "Unavailable/Zone" });
        var (f, _) = await Setup(Document(Task("Would otherwise appear", planned: Monday), invalid));
        var cut = Render<TodoTodayPanel>(p => p.Add(x => x.Day, Monday));
        Assert.Contains("No partial agenda", cut.Find("[data-testid='today-error']").TextContent);
        Assert.Empty(cut.FindAll("[data-testid^='today-row-']"));
        Save("todo-today-unavailable", cut.Markup, "timezone-unavailable", nameof(Missing_timezone_fails_the_whole_view_without_partial_tasks_and_invalid_dates_never_prepare));
        cut.Find("[data-testid='today-date']").Change("2026-02-30"); Assert.Contains("valid civil day", cut.Find("[data-testid='today-error']").TextContent);
        Assert.Empty(f.Client.PreparationIds); Assert.Empty(f.Client.AcceptedIds);
    }

    [Fact]
    public async Task Empty_day_and_unavailable_account_are_distinct_and_paginated_rows_follow_current_revision()
    {
        var tasks = Enumerable.Range(0, 21).Select(i => Task("Planned " + i, planned: Monday)).ToArray();
        var (f, _) = await Setup(Document(tasks)); var cut = Render<TodoTodayPanel>(p => p.Add(x => x.Day, Monday));
        Assert.Equal(20, cut.FindAll("[data-testid^='today-row-']").Count);
        cut.FindAll("button").Single(button => button.TextContent == "Next day rows").Click(); Assert.Single(cut.FindAll("[data-testid^='today-row-']"));
        cut.Find("[data-testid='today-date']").Change("2026-10-20"); Assert.NotNull(cut.Find("[data-testid='today-empty']"));
        Save("todo-today-empty", cut.Markup, "empty-selected-day", nameof(Empty_day_and_unavailable_account_are_distinct_and_paginated_rows_follow_current_revision));
        f.Session.SetState(SessionViewModel.Empty);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='today-empty']")));
        Assert.Contains("Unavailable task data is not an empty day", cut.Markup); Assert.Empty(f.Client.PreparationIds);
    }

    [Fact]
    public async Task Planned_Todo_completion_and_Habit_events_require_projection()
    {
        var todo = Task("Planned Todo", planned: Monday);
        var habit = Task("Planned Habit", CompanionTaskKind.Habit, planned: Monday);
        var (f, _) = await Setup(Document(todo, habit)); var cut = Render<TodoTodayPanel>(p => p.Add(x => x.Day, Monday));
        cut.Find($"[data-testid='today-complete-{todo.Id:D}']").Click();
        Assert.Equal(WorkspaceOperation.CompleteTask, f.Controller.Preview!.Command.Operation); Assert.Null(f.Controller.Preview.Command.OccurrenceDate);
        cut.Find("[data-testid='todo-confirm']").Click(); Assert.Equal(CompanionTaskState.Completed, f.Controller.Snapshot!.Document.Tasks.Single(task => task.Id == todo.Id).State);
        Assert.Empty(cut.FindAll($"[data-testid='today-row-{todo.Id:D}']"));
        Save("todo-today-habit", cut.Markup, "planned-habit-current-event", nameof(Planned_Todo_completion_and_Habit_events_require_projection));
        foreach (var label in new[] { "Record +1 now", "Record −1 now" })
        {
            cut.Find($"[data-testid='today-row-{habit.Id:D}']").QuerySelectorAll("button").Single(button => button.TextContent == label).Click();
            Assert.Equal(WorkspaceOperation.ScoreHabit, f.Controller.Preview!.Command.Operation); Assert.Null(f.Controller.Preview.Command.OccurrenceDate);
            Assert.Equal(label.Contains("+1") ? HabitScoreDirection.Up : HabitScoreDirection.Down, f.Controller.Preview.Command.Direction);
            Assert.True(f.Controller.Preview.MayAffectHabitica); cut.Find("[data-testid='todo-confirm']").Click();
        }
        Assert.Equal(2, f.Controller.Snapshot.Document.HabitScores.Count);
        Assert.Equal(new[] { ProjectionEffectKind.Create, ProjectionEffectKind.ScoreUp }, f.Controller.Snapshot.Commands[0].Effects);
        Assert.Equal(new[]{ProjectionEffectKind.Create,ProjectionEffectKind.ScoreUp},f.Controller.Snapshot.Commands[1].Effects);
        Assert.Equal(new[]{ProjectionEffectKind.Create,ProjectionEffectKind.ScoreDown},f.Controller.Snapshot.Commands[2].Effects);
        Assert.Contains(JSInterop.Invocations, call => call.Identifier == "Blazor._internal.domWrapper.focus");
    }

    [Fact]
    public async Task Civil_day_input_remains_ISO_with_a_non_Gregorian_display_culture()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new("ar-SA");
            var planned = Task("Invariant civil date", planned: Monday);
            await Setup(Document(planned)); var cut = Render<TodoTodayPanel>(p => p.Add(x => x.Day, Monday));
            Assert.Equal("2026-10-19", cut.Find("[data-testid='today-date']").GetAttribute("value"));
            Assert.Single(cut.FindAll($"[data-testid='today-row-{planned.Id:D}']"));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
    }

    private sealed class FixedClock(DateTimeOffset instant, string zone) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById(zone);
    }
}
