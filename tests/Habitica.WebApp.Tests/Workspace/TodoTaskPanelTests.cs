using Bunit;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Components;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Workspace;

public sealed class TodoTaskPanelTests : BunitContext
{
    private (WorkspaceTaskFixture Fixture, TodoWorkspacePresentation Presentation) Setup()
    {
        JSInterop.Mode = JSRuntimeMode.Loose; Services.AddMudServices();
        var f = new WorkspaceTaskFixture();
        Services.AddSingleton<IAppSessionController>(f.Session); Services.AddSingleton<IWorkspaceCloudClient>(f.Client);
        Services.AddSingleton(f.Database); Services.AddSingleton(f.Controller);
        var presentation = new TodoWorkspacePresentation(f.Session, f.Client, f.Database, f.Controller);
        Services.AddSingleton(presentation); Services.AddSingleton<TodoTaskDraftStore>();
        return (f, presentation);
    }
    private static void Save(string name, string markup, string state, string test) => Qualification.PageLayoutEvidence.Save(name, markup, "/tasks", state, ["form", "actions", "confirmation", "disabled-controls"], test);

    [Fact]
    public async Task Title_only_form_submission_reviews_one_native_save_and_returns_focus_to_immediate_capture()
    {
        var (f, _) = Setup(); await f.Controller.CreateLocalAsync();
        var cut = Render<TodoTaskPanel>();
        Assert.Null(cut.Find("[data-testid='todo-title']").Closest("details"));
        Assert.Equal("submit", cut.Find("[data-testid='todo-add']").GetAttribute("type"));
        Save("todo-capture-ready", cut.Markup, "title-first-loaded", nameof(Title_only_form_submission_reviews_one_native_save_and_returns_focus_to_immediate_capture));
        cut.Find("[data-testid='todo-title']").Input("A title-only task"); cut.Find("form").Submit();
        Assert.Empty(f.Controller.Snapshot!.Commands); Assert.False(f.Controller.Preview!.MayAffectHabitica);
        Assert.Equal("A title-only task", f.Controller.Preview.Command.Patch!.Text);
        Assert.Contains("Companion-only save", cut.Find("[data-testid='todo-review']").TextContent);
        cut.Find("[data-testid='todo-confirm']").Click();
        var task = Assert.Single(f.Controller.Snapshot.Document.Tasks);
        Assert.Equal("A title-only task", task.Text); Assert.Equal(CompanionPriority.Normal, task.Priority);
        Assert.Contains("Saved in Companion: Yes", cut.Find("[data-testid='todo-receipt']").TextContent);
        Assert.Contains("notRequested", cut.Find("[data-testid='todo-receipt']").TextContent);
        Assert.Equal("", cut.Find("[data-testid='todo-title']").GetAttribute("value"));
        Assert.Contains(JSInterop.Invocations, invocation => invocation.Identifier == "Blazor._internal.domWrapper.focus");
        Assert.Null(cut.Find($"[data-testid='todo-complete-{task.Id:D}']").Closest("details"));
    }

    [Fact]
    public async Task Advanced_fields_and_planned_date_have_exact_separate_previews_and_invalid_dates_never_prepare()
    {
        var (f, _) = Setup(); await f.Controller.CreateLocalAsync();
        var cut = Render<TodoTaskPanel>();
        cut.Find("[data-testid='todo-title']").Input("Long task " + new string('x', 100));
        cut.Find("[data-testid='todo-notes']").Input("<script>untrusted task note</script>\nSecond line");
        cut.Find("[data-testid='todo-priority']").Change("Urgent");
        cut.Find("[data-testid='todo-deadline']").Change("2026-02-30"); cut.Find("form").Submit();
        Assert.Null(f.Controller.Preview); Assert.Contains("valid date", cut.Find("[role='alert']").TextContent);
        cut.Find("[data-testid='todo-deadline']").Change("2026-10-25"); cut.Find("form").Submit();
        Assert.Equal(new DateOnly(2026, 10, 25), f.Controller.Preview!.Command.Patch!.Deadline);
        Assert.Empty(cut.FindAll("script")); Assert.Contains("<script>untrusted", cut.Find("[data-testid='todo-review']").TextContent);
        Save("todo-capture-review", cut.Markup, "long-name-review", nameof(Advanced_fields_and_planned_date_have_exact_separate_previews_and_invalid_dates_never_prepare));
        cut.Find("[data-testid='todo-confirm']").Click();
        var task = Assert.Single(f.Controller.Snapshot!.Document.Tasks);
        cut.Find($"[data-testid='todo-edit-{task.Id:D}']").Click();
        cut.Find("[data-testid='todo-notes']").Input("Unsaved note must remain a draft");
        cut.Find("[data-testid='todo-planned']").Change("2026-10-21");
        Save("todo-capture-advanced", cut.Markup.Replace("<details class=\"todo-advanced\"", "<details open class=\"todo-advanced\""), "advanced-edit", nameof(Advanced_fields_and_planned_date_have_exact_separate_previews_and_invalid_dates_never_prepare));
        cut.Find("[data-testid='todo-plan']").Click();
        var patch = f.Controller.Preview!.Command.Patch!;
        Assert.Equal(WorkspaceOperation.PlanTask, f.Controller.Preview.Command.Operation); Assert.Null(patch.Notes); Assert.Null(patch.Deadline);
        Assert.Equal(new DateOnly(2026, 10, 21), patch.Planning!.PlannedDate); Assert.False(f.Controller.Preview.MayAffectHabitica);
        cut.Find("[data-testid='todo-confirm']").Click();
        var saved = Assert.Single(f.Controller.Snapshot.Document.Tasks);
        Assert.Equal(task.Notes, saved.Notes); Assert.Equal(task.Deadline, saved.Deadline); Assert.Equal(new DateOnly(2026, 10, 21), saved.PlannedDate);
        Assert.Equal("Unsaved note must remain a draft", cut.Find("[data-testid='todo-notes']").GetAttribute("value"));
        cut.Find("form").Submit(); Assert.Equal("Unsaved note must remain a draft", f.Controller.Preview!.Command.Patch!.Notes);
        cut.Find("[data-testid='todo-cancel']").Click(); Assert.Equal(2, f.Controller.Snapshot.Commands.Count);
    }

    [Theory]
    [InlineData(CompanionTaskKind.Daily)]
    [InlineData(CompanionTaskKind.Habit)]
    public async Task Category_specific_primary_actions_record_reviewed_Daily_occurrence_or_Habit_event(CompanionTaskKind kind)
    {
        var (f, _) = Setup(); await f.Controller.CreateLocalAsync();
        var cut = Render<TodoTaskPanel>(p => p.Add(x => x.Kind, kind));
        cut.Find("[data-testid='todo-title']").Input("Synthetic " + kind); cut.Find("form").Submit(); cut.Find("[data-testid='todo-confirm']").Click();
        var task = Assert.Single(f.Controller.Snapshot!.Document.Tasks); Assert.Equal(kind, task.Kind);
        Assert.Null(task.Recurrence); Assert.Empty(cut.FindAll("[data-testid='todo-project']"));
        if (kind == CompanionTaskKind.Daily)
        {
            cut.Find($"[data-testid='todo-complete-{task.Id:D}']").Click();
            Assert.Null(f.Controller.Preview); Assert.Contains("occurrence date", cut.Find("[role='alert']").TextContent);
            cut.Find($"[data-testid='todo-occurrence-{task.Id:D}']").Change("2026-10-20");
            cut.Dispose(); cut = Render<TodoTaskPanel>(p => p.Add(x => x.Kind, kind));
            Assert.Equal("2026-10-20", cut.Find($"[data-testid='todo-occurrence-{task.Id:D}']").GetAttribute("value"));
            Save("todo-capture-daily", cut.Markup, "daily-row-date-action", nameof(Category_specific_primary_actions_record_reviewed_Daily_occurrence_or_Habit_event));
            cut.Find($"[data-testid='todo-complete-{task.Id:D}']").Click();
            Assert.Equal(new DateOnly(2026, 10, 20), f.Controller.Preview!.Command.OccurrenceDate);
        }
        else { cut.Find($"[data-testid='todo-up-{task.Id:D}']").Click(); Assert.Equal(HabitScoreDirection.Up, f.Controller.Preview!.Command.Direction); }
        Assert.False(f.Controller.Preview!.MayAffectHabitica); cut.Find("[data-testid='todo-confirm']").Click();
        if (kind == CompanionTaskKind.Daily) Assert.Single(f.Controller.Snapshot.Document.Completions);
        else Assert.Single(f.Controller.Snapshot.Document.HabitScores);
        Assert.Empty(f.Client.AcceptedIds);
    }

    [Fact]
    public async Task Draft_survives_component_recreation_source_changes_and_review_cancellation_but_clears_on_sign_out()
    {
        var (f, _) = Setup(); await f.Controller.CreateLocalAsync(); var first = f.Controller.Snapshot!.Document.Id;
        await f.Controller.CreateLocalAsync(); var second = f.Controller.Snapshot!.Document.Id;
        var cut = Render<TodoTaskPanel>(); cut.Find("[data-testid='todo-title']").Input("Private retained draft");
        cut.Find("form").Submit(); cut.Find("[data-testid='todo-cancel']").Click(); Assert.Empty(f.Controller.Snapshot.Commands);
        cut.Dispose(); cut = Render<TodoTaskPanel>();
        Assert.Equal("Private retained draft", cut.Find("[data-testid='todo-title']").GetAttribute("value"));
        cut.Find("[data-testid='todo-source']").Change($"Browser|{first:D}");
        Assert.Equal("", cut.Find("[data-testid='todo-title']").GetAttribute("value"));
        cut.Find("[data-testid='todo-source']").Change($"Browser|{second:D}");
        Assert.Equal("Private retained draft", cut.Find("[data-testid='todo-title']").GetAttribute("value"));
        f.Session.SetState(SessionViewModel.Empty);
        cut.WaitForAssertion(() => Assert.DoesNotContain("Private retained draft", cut.Markup));
        Assert.Null(f.Controller.Preview);
    }

    [Fact]
    public async Task Lost_acceptance_recovers_original_after_navigation_without_clearing_the_unsaved_title_early()
    {
        var (f, presentation) = Setup(); f.Client.Connected = true;
        await presentation.RefreshSourcesAsync(); await presentation.SelectAsync(presentation.Sources.Single());
        var cut = Render<TodoTaskPanel>(); cut.Find("[data-testid='todo-title']").Input("Recover exact original"); cut.Find("form").Submit();
        var original = f.Controller.Preview!.PreparedCommandId; f.Client.LoseAcceptance = true;
        cut.Find("[data-testid='todo-confirm']").Click(); Assert.True(f.Controller.AcceptanceUnknown);
        Save("todo-capture-unknown", cut.Markup, "uncertain-original-command", nameof(Lost_acceptance_recovers_original_after_navigation_without_clearing_the_unsaved_title_early));
        cut.Dispose(); cut = Render<TodoTaskPanel>();
        Assert.Equal("Recover exact original", cut.Find("[data-testid='todo-title']").GetAttribute("value"));
        Assert.Equal(original, f.Controller.Preview!.PreparedCommandId);
        cut.Find("[data-testid='todo-recover']").Click();
        Assert.Equal(original, f.Controller.LastReceipt!.CommandId); Assert.Single(f.Client.AcceptedIds);
        Assert.Single(f.Controller.Snapshot!.Document.Tasks); Assert.Equal("", cut.Find("[data-testid='todo-title']").GetAttribute("value"));
    }

    [Fact]
    public async Task Lost_preparation_reuses_original_request_and_archive_requires_a_concrete_review()
    {
        var (f, presentation) = Setup(); f.Client.Connected = true;
        await presentation.RefreshSourcesAsync(); await presentation.SelectAsync(presentation.Sources.Single());
        var cut = Render<TodoTaskPanel>(); f.Client.LosePreparation = true;
        cut.Find("[data-testid='todo-title']").Input("Original preparation"); cut.Find("form").Submit();
        Assert.True(f.Controller.HasPendingIntent); Assert.Null(f.Controller.Preview);
        cut.Find("[data-testid='todo-retry-preview']").Click(); Assert.Equal(2, f.Client.PreparationIds.Count);
        Assert.Single(f.Client.PreparationIds.Distinct()); cut.Find("[data-testid='todo-confirm']").Click();
        var task = Assert.Single(f.Controller.Snapshot!.Document.Tasks);
        cut.Find($"[data-testid='todo-edit-{task.Id:D}']").Click(); cut.Find("[data-testid='todo-archive']").Click();
        Assert.False(f.Controller.Snapshot.Document.Tasks[0].IsArchived); Assert.Contains("retain history", cut.Find("[data-testid='todo-review']").TextContent);
        cut.Find("[data-testid='todo-confirm']").Click(); Assert.True(f.Controller.Snapshot.Document.Tasks[0].IsArchived);
        Assert.Equal(2, f.Controller.Snapshot.Commands.Count);
    }

    [Fact]
    public async Task Editing_draft_and_selection_survive_navigation_and_refuse_overwriting_a_changed_task()
    {
        var (f, presentation) = Setup(); await f.Controller.CreateLocalAsync();
        await f.Controller.PrepareAsync(WorkspaceTaskFixture.Todo("Saved title")); await f.Controller.ConfirmAsync();
        var task = f.Controller.Snapshot!.Document.Tasks.Single();
        var cut = Render<TodoTaskPanel>(); cut.Find($"[data-testid='todo-edit-{task.Id:D}']").Click();
        cut.Find("[data-testid='todo-title']").Input("Retained editing draft"); cut.Dispose(); cut = Render<TodoTaskPanel>();
        Assert.Equal("Retained editing draft", cut.Find("[data-testid='todo-title']").GetAttribute("value"));
        Assert.Contains("Review changes", cut.Find("[data-testid='todo-add']").TextContent);
        await f.Controller.PrepareAsync(WorkspaceTaskFixture.Todo("Saved newer title") with { Operation = WorkspaceOperation.UpdateTask, TaskId = task.Id });
        await f.Controller.ConfirmAsync(); await presentation.LoadSelectedAsync();
        cut.Find("form").Submit(); Assert.Null(f.Controller.Preview);
        Assert.Contains("task changed", cut.Find("[role='alert']").TextContent);
        Assert.Equal("Retained editing draft", cut.Find("[data-testid='todo-title']").GetAttribute("value"));
        cut.FindAll("button").Single(button => button.TextContent.Contains("Reload current task and replace this draft")).Click();
        Assert.Equal("Saved newer title", cut.Find("[data-testid='todo-title']").GetAttribute("value"));
        Assert.Equal(2, f.Controller.Snapshot.Commands.Count);
    }

    [Fact]
    public async Task Explicit_game_projection_is_reviewed_and_saved_without_dispatching_a_live_effect()
    {
        var (f, _) = Setup(); await f.Controller.CreateLocalAsync();
        var cut = Render<TodoTaskPanel>(); cut.Find("[data-testid='todo-title']").Input("Explicit projected Todo");
        cut.Find("[data-testid='todo-project']").Change(true); cut.Find("form").Submit();
        Assert.True(f.Controller.Preview!.MayAffectHabitica); Assert.True(f.Controller.Preview.Command.ProjectToHabitica);
        Assert.Empty(f.Controller.Snapshot!.Commands); Assert.Contains("separately enabled executor", cut.Find("[data-testid='todo-review']").TextContent);
        cut.Find("[data-testid='todo-confirm']").Click();
        var command = Assert.Single(f.Controller.Snapshot.Commands); Assert.Equal(ProjectionCommandState.Pending, command.ProjectionState);
        Assert.Single(command.Effects); Assert.Equal(0, command.ConfirmedEffects); Assert.Empty(f.Client.AcceptedIds);
        Assert.Contains("pending", cut.Find("[data-testid='todo-receipt']").TextContent);
        Assert.False(cut.Find("[data-testid='todo-project']").HasAttribute("checked"));
    }
}
