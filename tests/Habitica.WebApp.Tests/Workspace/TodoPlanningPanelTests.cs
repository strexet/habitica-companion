using Bunit;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Components;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Workspace;

public sealed class TodoPlanningPanelTests : BunitContext
{
    private static readonly DateOnly Day = new(2026, 10, 20);
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    private static CompanionTask Task(string text, DateOnly? planned = null, DateOnly? due = null) => new(Guid.NewGuid(), 1, text, CompanionTaskKind.Todo, CompanionTaskState.Active, CompanionPriority.Normal, Now, Now, PlannedDate: planned, Deadline: due);
    private static WorkspaceDocument Doc(params CompanionTask[] tasks) => new() { Id = Guid.NewGuid(), Revision = 10, Tasks = tasks };
    private async Task<(WorkspaceTaskFixture Fixture, TodoWorkspacePresentation Todo)> Setup(WorkspaceDocument doc)
    {
        JSInterop.Mode = JSRuntimeMode.Loose; Services.AddMudServices(); var f = new WorkspaceTaskFixture(); f.Client.Connected = true;
        Assert.Empty(WorkspaceRules.Validate(doc)); f.Client.State = new(doc, doc.Revision, [], []); await f.Controller.LoadAsync(true);
        Services.AddSingleton<IAppSessionController>(f.Session); Services.AddSingleton<IWorkspaceCloudClient>(f.Client); Services.AddSingleton(f.Database); Services.AddSingleton(f.Controller);
        var todo = new TodoWorkspacePresentation(f.Session, f.Client, f.Database, f.Controller); Services.AddSingleton(todo); Services.AddSingleton<TodoTaskDraftStore>(); Services.AddSingleton(TimeProvider.System);
        return (f, todo);
    }
    private static void Save(string name, string markup, string state, string test, string route = "/todo/plan") => Qualification.PageLayoutEvidence.Save(name, markup, route, state, ["form", "actions", "confirmation", "disabled-controls", "error", "navigation"], test);

    [Fact]
    public async Task Inbox_anytime_overdue_and_bounded_agenda_views_remain_distinct_and_pure()
    {
        var inbox = Task("Inbox", due: Day.AddDays(1)); var project = new WorkspaceProject(Guid.NewGuid(), 1, "Project");
        var assigned = Task("Anytime project") with { ProjectId = project.Id }; var planned = Task("Earlier plan", Day.AddDays(-1), Day.AddDays(-2));
        var recurring = Task("Weekly Tuesday") with { Kind = CompanionTaskKind.Daily, Recurrence = new(RecurrenceFrequency.Weekly, 1, new(2026, 1, 1), "UTC", [DayOfWeek.Tuesday], []) };
        var (f, _) = await Setup(Doc(inbox, assigned, planned, recurring) with { Projects = [project] }); var before = WorkspaceJournalJson.Serialize(f.Client.State);
        var cut = Render<TodoPlanningPanel>(p => p.Add(x => x.View, TodoPlanningView.Inbox).Add(x => x.Day, Day));
        Assert.Single(cut.FindAll("[data-testid^='plan-row-']")); Assert.Contains("deadline alone", cut.Markup);
        Save("todo-plan-inbox", cut.Markup, "inbox-deadline", nameof(Inbox_anytime_overdue_and_bounded_agenda_views_remain_distinct_and_pure), "/todo/inbox");
        cut.Find("[data-testid='plan-view']").Change("Anytime"); Assert.Equal(2, cut.FindAll("[data-testid^='plan-row-']").Count);
        cut.Find("[data-testid='plan-view']").Change("Overdue"); Assert.Single(cut.FindAll("[data-testid^='plan-row-']")); Assert.Contains("Planned earlier.", cut.Markup); Assert.Contains("Deadline overdue.", cut.Markup);
        cut.Find("[data-testid='plan-view']").Change("Week"); Assert.Contains("2026-10-19–2026-10-25", cut.Markup); Assert.Contains("Weekly Tuesday", cut.Markup);
        Save("todo-plan-week", cut.Markup, "week-agenda", nameof(Inbox_anytime_overdue_and_bounded_agenda_views_remain_distinct_and_pure));
        cut.Find("[data-testid='plan-view']").Change("Month"); Assert.Contains("2026-10-01–2026-10-31", cut.Markup);
        cut.Find("[data-testid='plan-next-period']").Click(); Assert.Contains("2026-11-01–2026-11-30", cut.Markup);
        cut.Find("[data-testid='plan-view']").Change("Upcoming"); Assert.Contains("2026-11-20–2026-12-19", cut.Markup);
        Assert.Empty(f.Client.PreparationIds); Assert.Equal(before, WorkspaceJournalJson.Serialize(f.Client.State));
    }

    [Fact]
    public async Task Review_set_and_clear_preserves_identity_deadline_recurrence_history_mapping_and_recovers_original_receipt()
    {
        var task = Task(new string('W', 150) + " long task", Day, Day.AddDays(2));
        var mapping = new HabiticaProjection(Guid.NewGuid(), 1, task.Id, ProjectionState.Linked, "00000000-0000-4000-8000-000000000099");
        var (f, _) = await Setup(Doc(task) with { Projections = [mapping] });
        var cut = Render<TodoPlanningPanel>(p => p.Add(x => x.Day, Day));
        cut.Find($"[data-testid='plan-edit-{task.Id:D}']").Click(); cut.Find("[data-testid='plan-date']").Change("2026-10-25");
        cut.Find("form").Submit(); var preview = f.Controller.Preview!;
        Assert.Equal(task.Id, preview.Command.TaskId); Assert.Equal(new DateOnly(2026, 10, 25), preview.Command.Patch!.Planning!.PlannedDate); Assert.False(preview.MayAffectHabitica);
        Assert.Contains("Deadline and recurrence unchanged", cut.Find("[data-testid='todo-review']").TextContent);
        Save("todo-plan-review", cut.Markup, "long-task-placement-review", nameof(Review_set_and_clear_preserves_identity_deadline_recurrence_history_mapping_and_recovers_original_receipt));
        var id = preview.PreparedCommandId; f.Client.LoseAcceptance = true; cut.Find("[data-testid='todo-confirm']").Click(); Assert.True(f.Controller.AcceptanceUnknown);
        cut.Find("[data-testid='todo-recover']").Click(); Assert.Equal(id, f.Controller.LastReceipt!.CommandId); Assert.Single(f.Client.AcceptedIds);
        Assert.NotNull(cut.Find("[data-testid='plan-reload']")); cut.Find("[data-testid='plan-reload']").Click(); cut.Find("[data-testid='plan-date']").Change(""); cut.Find("form").Submit();
        Assert.True(f.Controller.Preview!.Command.Patch!.Planning!.ClearPlannedDate); cut.Find("[data-testid='todo-confirm']").Click();
        var saved = Assert.Single(f.Controller.Snapshot!.Document.Tasks); Assert.Equal(task.Id, saved.Id); Assert.Null(saved.PlannedDate); Assert.Equal(task.Deadline, saved.Deadline); Assert.Equal(task.Text, saved.Text);
        Assert.Equal(mapping, Assert.Single(f.Controller.Snapshot.Document.Projections)); Assert.Empty(f.Controller.Snapshot.Document.Completions); Assert.Empty(f.Controller.Snapshot.Document.HabitScores); Assert.All(f.Controller.Snapshot.Commands, command => Assert.Empty(command.Effects));
        Assert.Contains(JSInterop.Invocations, invocation => invocation.Identifier == "Blazor._internal.domWrapper.focus");
    }

    [Fact]
    public async Task Placement_controls_page_existing_records_and_review_exact_replacement_and_project_section_clear()
    {
        var projects = Enumerable.Range(0, 21).Select(i => new WorkspaceProject(Guid.NewGuid(), 1, "Project " + i)).ToArray();
        var section = new WorkspaceSection(Guid.NewGuid(), 1, "Old section", projects[0].Id); var category = new WorkspaceCategory(Guid.NewGuid(), 1, "Category"); var label = new WorkspaceLabel(Guid.NewGuid(), 1, "Selected label");
        var task = Task("Place me") with { ProjectId = projects[0].Id, SectionId = section.Id };
        var (f, _) = await Setup(Doc(task) with { Projects = projects, Sections = [section], Categories = [category], Labels = [label] });
        var cut = Render<TodoPlanningPanel>(p => p.Add(x => x.View, TodoPlanningView.Anytime).Add(x => x.Day, Day)); cut.Find($"[data-testid='plan-edit-{task.Id:D}']").Click();
        cut.Find("[aria-label='Next Primary project']").Click(); Assert.Contains("Project 0 (retained selection)", cut.Markup);
        cut.Find("[data-testid='plan-project']").Change(projects[20].Id.ToString()); cut.Find("[data-testid='plan-category']").Change(category.Id.ToString());
        cut.Find("[data-testid='plan-label']").Change(label.Id.ToString()); cut.FindAll("button").Single(button => button.TextContent == "Add selected label").Click();
        Save("todo-plan-editor", cut.Markup, "paged-placement-editor", nameof(Placement_controls_page_existing_records_and_review_exact_replacement_and_project_section_clear));
        Save("todo-plan-labels", cut.Markup.Replace("<details><summary>Labels", "<details open><summary>Labels", StringComparison.Ordinal), "expanded-label-placement", nameof(Placement_controls_page_existing_records_and_review_exact_replacement_and_project_section_clear));
        cut.Find("form").Submit(); var plan = f.Controller.Preview!.Command.Patch!.Planning!;
        Assert.Equal(projects[20].Id, plan.ProjectId); Assert.True(plan.ClearSection); Assert.Equal(category.Id, plan.CategoryId); Assert.Equal(new[] { label.Id }, plan.LabelIds);
        var review = cut.Find("[data-testid='todo-review']").TextContent; Assert.Contains("Project 20", review); Assert.Contains("Clear section", review); Assert.Contains("Selected label", review); Assert.Contains(label.Id.ToString(), review);
        cut.Find("[data-testid='todo-confirm']").Click(); var saved = f.Controller.Snapshot!.Document.Tasks[0]; Assert.Equal(projects[20].Id, saved.ProjectId); Assert.Null(saved.SectionId); Assert.Equal(category.Id, saved.CategoryId);
    }

    [Fact]
    public async Task Revision_change_resets_pages_and_stale_editor_requires_explicit_reload_without_losing_draft()
    {
        var tasks = Enumerable.Range(0, 21).Select(i => Task("Inbox " + i)).ToArray(); var (f, todo) = await Setup(Doc(tasks));
        var cut = Render<TodoPlanningPanel>(p => p.Add(x => x.View, TodoPlanningView.Inbox).Add(x => x.Day, Day)); cut.Find("[data-testid='plan-next']").Click(); Assert.Single(cut.FindAll("[data-testid^='plan-row-']"));
        cut.Find($"[data-testid='plan-edit-{tasks[20].Id:D}']").Click(); cut.Find("[data-testid='plan-date']").Change("2026-10-23");
        f.Client.State = f.Client.State with { Document = f.Client.State.Document with { Revision = 11 }, StoreVersion = 11 };
        await cut.InvokeAsync(() => todo.LoadSelectedAsync());
        Assert.Equal(20, cut.FindAll("[data-testid^='plan-row-']").Count); Assert.True(cut.Find("[data-testid='plan-previous']").HasAttribute("disabled"));
        Assert.Contains("Task data changed", cut.Markup); cut.Find("form").Submit(); Assert.Empty(f.Client.PreparationIds);
        Assert.Equal("2026-10-23", cut.Find("[data-testid='plan-date']").GetAttribute("value"));
        cut.Find("[data-testid='plan-reload']").Click(); Assert.Equal("", cut.Find("[data-testid='plan-date']").GetAttribute("value"));
    }

    [Fact]
    public async Task Empty_unavailable_invalid_timezone_and_bad_dates_remain_explicit_without_partial_agenda()
    {
        var invalid = Task("Unavailable schedule") with { Kind = CompanionTaskKind.Daily, Recurrence = new(RecurrenceFrequency.Daily, 1, new(2026, 1, 1), "Unavailable/Zone", [], []) };
        var (f, _) = await Setup(Doc(Task("Otherwise visible", Day), invalid)); var cut = Render<TodoPlanningPanel>(p => p.Add(x => x.Day, Day));
        Assert.Contains("No partial agenda", cut.Find("[data-testid='plan-error']").TextContent); Assert.Empty(cut.FindAll("[data-testid^='plan-row-']"));
        Save("todo-plan-unavailable", cut.Markup, "timezone-unavailable", nameof(Empty_unavailable_invalid_timezone_and_bad_dates_remain_explicit_without_partial_agenda));
        cut.Find("[data-testid='plan-day']").Change("2026-02-30"); Assert.Contains("valid civil day", cut.Markup);
        cut.Find("[data-testid='plan-day']").Change("2026-10-20"); cut.Find("[data-testid='plan-view']").Change("Inbox"); Assert.NotNull(cut.Find("[data-testid='plan-empty']"));
        Save("todo-plan-empty", cut.Markup, "empty-inbox", nameof(Empty_unavailable_invalid_timezone_and_bad_dates_remain_explicit_without_partial_agenda), "/todo/inbox");
        f.Session.SetState(SessionViewModel.Empty); cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='plan-empty']"))); Assert.Contains("Unavailable data is not an empty", cut.Markup); Assert.Empty(f.Client.PreparationIds);
    }

    [Fact]
    public void Range_boundaries_use_civil_Monday_weeks_month_lengths_and_bounded_upcoming()
    {
        Assert.Equal((new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 25)), TodoPlanningRange.For(TodoPlanningView.Week, Day));
        Assert.Equal((new DateOnly(2028, 2, 1), new DateOnly(2028, 2, 29)), TodoPlanningRange.For(TodoPlanningView.Month, new(2028, 2, 20)));
        Assert.Equal(DateOnly.MaxValue, TodoPlanningRange.For(TodoPlanningView.Upcoming, DateOnly.MaxValue).Through);
        Assert.Throws<ArgumentException>(() => TodoPlanningRange.For(TodoPlanningView.Month, default));
    }

    [Fact]
    public async Task Daily_placement_preserves_existing_schedule_occurrence_and_completion_without_game_effects()
    {
        var rule = new RecurrenceRule(RecurrenceFrequency.Weekly, 1, new(2026, 1, 1), "America/New_York", [DayOfWeek.Tuesday], []);
        var daily = Task("Scheduled Daily", Day) with { Kind = CompanionTaskKind.Daily, Recurrence = rule };
        var occurrence = new TaskOccurrence(Guid.NewGuid(), 1, daily.Id, Day, OccurrenceState.Completed);
        var completion = new CompletionEvent(Guid.NewGuid(), 1, daily.Id, Guid.NewGuid(), Now, occurrence.Id);
        var (f, _) = await Setup(Doc(daily) with { Occurrences = [occurrence], Completions = [completion] });
        var cut = Render<TodoPlanningPanel>(p => p.Add(x => x.Day, Day)); cut.FindAll($"[data-testid='plan-edit-{daily.Id:D}']")[0].Click();
        cut.Find("[data-testid='plan-date']").Change("2026-10-22"); cut.Find("form").Submit(); cut.Find("[data-testid='todo-confirm']").Click();
        var saved = f.Controller.Snapshot!.Document; Assert.Equal(rule, saved.Tasks[0].Recurrence); Assert.Equal(occurrence, Assert.Single(saved.Occurrences)); Assert.Equal(completion, Assert.Single(saved.Completions));
        Assert.Empty(Assert.Single(f.Controller.Snapshot.Commands).Effects);
    }

    [Fact]
    public async Task Clearing_project_category_and_labels_is_explicit_and_bad_dates_or_archived_label_replacements_are_refused()
    {
        var project = new WorkspaceProject(Guid.NewGuid(), 1, "Project"); var category = new WorkspaceCategory(Guid.NewGuid(), 1, "Category");
        var archived = new WorkspaceLabel(Guid.NewGuid(), 1, "Archived", true); var active = new WorkspaceLabel(Guid.NewGuid(), 1, "Active");
        var task = Task("Clear placement") with { ProjectId = project.Id, CategoryId = category.Id, LabelIds = [archived.Id] };
        var (f, _) = await Setup(Doc(task) with { Projects = [project], Categories = [category], Labels = [archived, active] });
        var cut = Render<TodoPlanningPanel>(p => p.Add(x => x.View, TodoPlanningView.Anytime).Add(x => x.Day, Day)); cut.Find($"[data-testid='plan-edit-{task.Id:D}']").Click();
        cut.Find("[data-testid='plan-date']").Change("bad-date"); cut.Find("form").Submit(); Assert.Empty(f.Client.PreparationIds);
        cut.Find("[data-testid='plan-date']").Change(""); cut.Find("[data-testid='plan-label']").Change(active.Id.ToString()); cut.FindAll("button").Single(button => button.TextContent == "Add selected label").Click();
        cut.Find("form").Submit(); Assert.Empty(f.Client.PreparationIds); Assert.Contains("Remove archived labels", cut.Markup);
        for (var i = 0; i < 2; i++) cut.Find("[aria-label^='Remove label']").Click();
        cut.Find("[data-testid='plan-project']").Change(""); cut.Find("[data-testid='plan-category']").Change(""); cut.Find("form").Submit();
        var plan = f.Controller.Preview!.Command.Patch!.Planning!; Assert.True(plan.ClearProject); Assert.True(plan.ClearCategory); Assert.Empty(plan.LabelIds!);
        Assert.Contains("Clear all labels", cut.Find("[data-testid='todo-review']").TextContent); cut.Find("[data-testid='todo-confirm']").Click();
        var saved = f.Controller.Snapshot!.Document.Tasks[0]; Assert.Null(saved.ProjectId); Assert.Null(saved.CategoryId); Assert.Empty(saved.LabelIds!);
    }

    [Fact]
    public async Task Removed_project_tasks_appear_as_unassigned_while_the_exact_project_and_section_references_remain_retained()
    {
        var project = new WorkspaceProject(Guid.NewGuid(), 1, new string('P', 90) + " removed project");
        var section = new WorkspaceSection(Guid.NewGuid(), 1, "Retained section", project.Id);
        var task = Task("Retained task") with { ProjectId = project.Id, SectionId = section.Id };
        var doc = Doc(task) with { Projects = [project], Sections = [section] };
        var removed = WorkspaceOrganizationEngine.Accept(new(doc, 10, [], []), new(Guid.NewGuid(), doc.Id, 10, WorkspaceOrganizationKind.Project,
            WorkspaceOrganizationOperation.Remove, project.Id, new(Removal: WorkspaceProjectRemoval.From(doc, project.Id))), Now);
        Assert.Equal(SubmissionDisposition.Accepted, removed.Disposition);
        var (f, _) = await Setup(removed.Snapshot!.Document); var cut = Render<TodoPlanningPanel>(p => p.Add(x => x.View, TodoPlanningView.Inbox).Add(x => x.Day, Day));
        Assert.Single(cut.FindAll("[data-testid^='plan-row-']")); Assert.Contains("Unassigned · removed project reference retained", cut.Markup);
        cut.Find($"[data-testid='plan-edit-{task.Id:D}']").Click(); Assert.Contains("Unassigned (removed project:", cut.Markup);
        Assert.Equal(project.Id.ToString(), cut.Find("[data-testid='plan-project']").GetAttribute("value"));
        Assert.Equal(project.Id, f.Controller.Snapshot!.Document.Tasks[0].ProjectId); Assert.Equal(section.Id, f.Controller.Snapshot.Document.Tasks[0].SectionId);
        Assert.Empty(f.Client.PreparationIds); Assert.Empty(f.Client.AcceptedIds);
        Save("todo-plan-removed-project", cut.Markup, "removed-project-unassigned-retained-placement", nameof(Removed_project_tasks_appear_as_unassigned_while_the_exact_project_and_section_references_remain_retained), "/todo/inbox");
    }
}
