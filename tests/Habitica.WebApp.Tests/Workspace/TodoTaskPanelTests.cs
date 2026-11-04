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

    [Theory]
    [InlineData(-420,0,"Etc/GMT-7","daily-timezones-mismatch")]
    [InlineData(-330,0,null,"daily-timezones-fractional")]
    [InlineData(0,4,null,"daily-timezones-daystart")]
    public async Task Observed_account_limits_choices_without_rewriting_the_current_Daily_draft(int offset,int start,string? matching,string fixture)
    {
        var(f,presentation)=Setup();await f.Controller.CreateLocalAsync();var cut=Render<TodoTaskPanel>(p=>p.Add(x=>x.Kind,CompanionTaskKind.Daily));
        cut.Find("[data-testid='todo-title']").Input("Preserved account schedule");cut.Find("[data-testid='daily-start']").Change("2026-11-04");
        f.Session.SetState(f.Session.State with{UserFreshness=Habitica.Domain.Sync.SnapshotFreshnessState.Fresh,UserSnapshot=new Habitica.Domain.User.UserSnapshot(DateTimeOffset.UtcNow,"Synthetic",null,1,50,50,0,0,0,0,0,null,null,null,null!,null!,DayStartHour:start,TimezoneOffsetMinutes:offset)});
        Assert.Equal(new DailyAccountSchedule(offset,start),presentation.ObservedDailyAccountSchedule);
        Assert.Equal("UTC",cut.Find("[data-testid='daily-zone']").GetAttribute("value"));
        var help=cut.Find("[data-testid='daily-zone-help']").TextContent;
        Assert.Contains(matching is null?"unsupported":"not been changed automatically",help);
        Assert.Equal(matching is null,cut.Find("[data-testid='daily-zone']").HasAttribute("disabled"));
        Qualification.PageLayoutEvidence.Save(fixture,cut.Markup,"/todo/dailies","observed-account-schedule-limits",["form","actions","disabled-controls"],nameof(Observed_account_limits_choices_without_rewriting_the_current_Daily_draft));
        cut.Find("form").Submit();Assert.Null(f.Controller.Preview);Assert.Empty(f.Controller.Snapshot!.Commands);
        if(matching is not null){Assert.False(cut.Find($"option[value='{matching}']").HasAttribute("disabled"));Assert.True(cut.Find("[data-testid='daily-zone'] option[value='UTC']").HasAttribute("disabled"));cut.Find("[data-testid='daily-zone']").Change(matching);cut.Find("form").Submit();Assert.Equal(matching,f.Controller.Preview!.Command.Patch!.Recurrence!.TimeZoneId);cut.Find("[data-testid='todo-cancel']").Click();}
        f.Session.SetState(f.Session.State with{UserFreshness=Habitica.Domain.Sync.SnapshotFreshnessState.Stale});Assert.Null(presentation.ObservedDailyAccountSchedule);
        Assert.Contains("not currently verified",cut.Find("[data-testid='daily-zone-help']").TextContent);Assert.Empty(f.Client.AcceptedIds);
    }

    [Fact]
    public async Task Linked_Daily_offset_choices_keep_exact_ids_and_review_without_writes()
    {
        var (f, _) = Setup(); await f.Controller.CreateLocalAsync();
        var cut = Render<TodoTaskPanel>(p=>p.Add(x=>x.Kind,CompanionTaskKind.Daily));
        var zone=cut.Find("select[data-testid='daily-zone']");
        var expected=new Dictionary<string,string>{{"UTC","UTC+00:00"},{"Etc/GMT-7","UTC+07:00"},{"Etc/GMT+5","UTC−05:00"},{"Etc/GMT-14","UTC+14:00"},{"Etc/GMT+12","UTC−12:00"}};
        foreach(var row in expected)Assert.Equal(row.Value,zone.QuerySelector($"option[value='{row.Key}']")!.TextContent);
        Assert.Null(zone.QuerySelector("option[value='Etc/GMT+13']")); Assert.Null(zone.QuerySelector("option[value='Europe/Paris']"));
        cut.Find("[data-testid='todo-title']").Input("Readable Daily offsets");cut.Find("[data-testid='daily-start']").Change("2026-11-04");
        foreach(var row in expected)
        {
            cut.Find("[data-testid='daily-zone']").Change(row.Key);
            var captured=new AngleSharp.Html.Parser.HtmlParser().ParseDocument(Qualification.PageLayoutEvidence.NormalizeBoundFields(cut.Markup));
            Assert.Equal(row.Key,captured.QuerySelector("[data-testid='daily-zone'] option[selected]")?.GetAttribute("value"));
            cut.Find("form").Submit();
            Assert.Equal(row.Key,f.Controller.Preview!.Command.Patch!.Recurrence!.TimeZoneId);
            Assert.Contains(row.Key,cut.Find("[data-testid='todo-review']").TextContent);
            if(row.Key=="Etc/GMT-7")Qualification.PageLayoutEvidence.Save("daily-timezones-review",cut.Markup,"/todo/dailies","exact-fixed-zone-review",["form","actions","disabled-controls"],nameof(Linked_Daily_offset_choices_keep_exact_ids_and_review_without_writes));
            cut.Find("[data-testid='todo-cancel']").Click();Assert.Equal(row.Key,cut.Find("[data-testid='daily-zone']").GetAttribute("value"));
        }
        Assert.Empty(f.Controller.Snapshot!.Commands);Assert.Empty(f.Client.AcceptedIds);
        Qualification.PageLayoutEvidence.Save("daily-timezones-capture",cut.Markup,"/todo/dailies","readable-offset-selection",["form","actions"],nameof(Linked_Daily_offset_choices_keep_exact_ids_and_review_without_writes));
    }

    [Fact]
    public async Task Native_project_receipt_cannot_imply_existing_Habitica_tag_coverage()
    {
        var (f, presentation) = Setup(); f.Client.Connected = true;
        f.Client.State = f.Client.State with { Document = f.Client.State.Document with
        { Projects = [new(Guid.NewGuid(), 1, "Synthetic project")] } };
        await presentation.RefreshSourcesAsync();
        await presentation.SelectAsync(new(TodoWorkspaceSource.Cloud, f.Client.State.Document.Id));
        var cut = Render<TodoTaskPanel>();
        Assert.Contains("Projects and groups awaiting Habitica tags: 1.", cut.Find("[data-testid='todo-habitica-coverage']").TextContent);
        Assert.Empty(f.Client.AcceptedIds);
        Save("todo-coverage-groups", cut.Markup, "project-without-Habitica-tag", nameof(Native_project_receipt_cannot_imply_existing_Habitica_tag_coverage));
    }

    [Fact]
    public async Task Title_only_form_submission_reviews_required_game_intent_and_returns_focus_to_immediate_capture()
    {
        var (f, _) = Setup(); await f.Controller.CreateLocalAsync();
        var cut = Render<TodoTaskPanel>();
        Assert.Null(cut.Find("[data-testid='todo-title']").Closest("details"));
        Assert.Equal("submit", cut.Find("[data-testid='todo-add']").GetAttribute("type"));
        Save("todo-capture-ready", cut.Markup, "title-first-loaded", nameof(Title_only_form_submission_reviews_required_game_intent_and_returns_focus_to_immediate_capture));
        cut.Find("[data-testid='todo-title']").Input("A title-only task"); cut.Find("form").Submit();
        Assert.Empty(f.Controller.Snapshot!.Commands); Assert.True(f.Controller.Preview!.MayAffectHabitica);
        Assert.Equal("A title-only task", f.Controller.Preview.Command.Patch!.Text);
        Assert.Contains("separately enabled executor", cut.Find("[data-testid='todo-review']").TextContent);
        cut.Find("[data-testid='todo-confirm']").Click();
        var task = Assert.Single(f.Controller.Snapshot.Document.Tasks);
        Assert.Equal("A title-only task", task.Text); Assert.Equal(CompanionPriority.Normal, task.Priority);
        Assert.Contains("Saved in Companion: Yes", cut.Find("[data-testid='todo-receipt']").TextContent);
        Assert.Contains("pending", cut.Find("[data-testid='todo-receipt']").TextContent);
        Assert.Equal("", cut.Find("[data-testid='todo-title']").GetAttribute("value"));
        Assert.Contains(JSInterop.Invocations, invocation => invocation.Identifier == "Blazor._internal.domWrapper.focus");
        Assert.Null(cut.Find($"[data-testid='todo-complete-{task.Id:D}']").Closest("details"));
        var coverage = cut.Find("[data-testid='todo-habitica-coverage']").TextContent;
        Assert.Contains("pending: 1", coverage);
        Assert.Contains("Unlinked task completions earn no Habitica rewards", coverage);
        Save("todo-coverage-pending", cut.Markup, "saved-task-awaiting-game-link", nameof(Title_only_form_submission_reviews_required_game_intent_and_returns_focus_to_immediate_capture));
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
        Assert.Equal(CompanionPriority.Urgent, task.Priority);
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
        cut.Find("[data-testid='todo-title']").Input("Synthetic " + kind);
        if (kind == CompanionTaskKind.Daily) { cut.Find("[data-testid='daily-start']").Change("2026-10-19"); Save("todo-daily-create-form", cut.Markup, "explicit-daily-schedule", nameof(Category_specific_primary_actions_record_reviewed_Daily_occurrence_or_Habit_event)); }
        cut.Find("form").Submit();
        if (kind == CompanionTaskKind.Daily) { Assert.True(f.Controller.Preview!.MayAffectHabitica); Save("todo-daily-create-review", cut.Markup, "reviewed-daily-and-penalties", nameof(Category_specific_primary_actions_record_reviewed_Daily_occurrence_or_Habit_event)); }
        if (kind == CompanionTaskKind.Habit)
        {
            var directions = f.Controller.Preview!.Command.Patch!.HabiticaDetails!;
            Assert.True(directions.Up); Assert.True(directions.Down);
            Assert.Contains("Positive (+): Enabled", cut.Find("[data-testid='todo-review']").TextContent);
            Save("todo-habit-create-review", cut.Markup, "required-Habit-creation-directions", nameof(Category_specific_primary_actions_record_reviewed_Daily_occurrence_or_Habit_event));
        }
        cut.Find("[data-testid='todo-confirm']").Click();
        var task = Assert.Single(f.Controller.Snapshot!.Document.Tasks); Assert.Equal(kind, task.Kind);
        if (kind == CompanionTaskKind.Daily) Assert.NotNull(task.Recurrence); else Assert.Null(task.Recurrence); Assert.Empty(cut.FindAll("[data-testid='todo-project']"));
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
        Assert.True(f.Controller.Preview!.MayAffectHabitica); cut.Find("[data-testid='todo-confirm']").Click();
        if (kind == CompanionTaskKind.Daily) Assert.Single(f.Controller.Snapshot.Document.Completions);
        else Assert.Single(f.Controller.Snapshot.Document.HabitScores);
        Assert.Empty(f.Client.AcceptedIds);
    }

    [Fact]
    public async Task Draft_survives_component_recreation_source_changes_and_review_cancellation_but_clears_on_sign_out()
    {
        var (f, presentation) = Setup(); await f.Controller.CreateLocalAsync(); var first = f.Controller.Snapshot!.Document.Id;
        await f.Controller.CreateLocalAsync(); var second = f.Controller.Snapshot!.Document.Id;
        var cut = Render<TodoTaskPanel>(); cut.Find("[data-testid='todo-title']").Input("Private retained draft");
        cut.Find("form").Submit(); cut.Find("[data-testid='todo-cancel']").Click(); Assert.Empty(f.Controller.Snapshot.Commands);
        cut.Dispose(); cut = Render<TodoTaskPanel>();
        Assert.Equal("Private retained draft", cut.Find("[data-testid='todo-title']").GetAttribute("value"));
        Assert.Empty(cut.FindAll("[data-testid='todo-source']"));await cut.InvokeAsync(()=>presentation.SelectAsync(new(TodoWorkspaceSource.Browser,first)));
        Assert.Equal("", cut.Find("[data-testid='todo-title']").GetAttribute("value"));
        await cut.InvokeAsync(()=>presentation.SelectAsync(new(TodoWorkspaceSource.Browser,second)));
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
        Assert.Single(f.Controller.Snapshot.Document.Projections);
        await f.SeedLegacyLocalAsync("Legacy archive candidate");
        await presentation.RefreshSourcesAsync();
        await presentation.SelectAsync(new(TodoWorkspaceSource.Browser, f.Controller.Snapshot!.Document.Id));
        task = f.Controller.Snapshot.Document.Tasks.Single();
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Habit_rows_disable_only_the_unavailable_scoring_direction(bool up)
    {
        var (f, presentation) = Setup(); f.Client.Connected = true; var now = DateTimeOffset.UtcNow;
        f.Client.State = f.Client.State with { Document = f.Client.State.Document with { Tasks = [new(Guid.NewGuid(), 1, "Long Habit " + new string('x', 100), CompanionTaskKind.Habit, CompanionTaskState.Active, CompanionPriority.Normal, now, now, HabiticaDetails: new(Up: up, Down: !up))] } };
        await presentation.RefreshSourcesAsync(); await presentation.SelectAsync(presentation.Sources.Single());
        var cut = Render<TodoTaskPanel>(parameters => parameters.Add(x => x.Kind, CompanionTaskKind.Habit));
        Assert.Equal(!up, cut.FindAll("button").Single(button => button.TextContent.Trim() == "Record +1").HasAttribute("disabled"));
        Assert.Equal(up, cut.FindAll("button").Single(button => button.TextContent.Trim() == "Record −1").HasAttribute("disabled"));
        Save("todo-habit-direction-row", cut.Markup, "disabled-direction-without-score", nameof(Habit_rows_disable_only_the_unavailable_scoring_direction));
        Assert.Null(f.Controller.Preview); Assert.Empty(f.Client.AcceptedIds);
    }

    [Fact]
    public async Task Explicit_game_projection_is_reviewed_and_saved_without_dispatching_a_live_effect()
    {
        var (f, _) = Setup(); await f.Controller.CreateLocalAsync();
        var cut = Render<TodoTaskPanel>(); cut.Find("[data-testid='todo-title']").Input("Explicit projected Todo");
         cut.Find("form").Submit();
        Assert.True(f.Controller.Preview!.MayAffectHabitica); Assert.True(f.Controller.Preview.Command.ProjectToHabitica);
        Assert.Empty(f.Controller.Snapshot!.Commands); Assert.Contains("separately enabled executor", cut.Find("[data-testid='todo-review']").TextContent);
        cut.Find("[data-testid='todo-confirm']").Click();
        var command = Assert.Single(f.Controller.Snapshot.Commands); Assert.Equal(ProjectionCommandState.Pending, command.ProjectionState);
        Assert.Single(command.Effects); Assert.Equal(0, command.ConfirmedEffects); Assert.Empty(f.Client.AcceptedIds);
        Assert.Contains("pending", cut.Find("[data-testid='todo-receipt']").TextContent);
        Assert.Empty(cut.FindAll("[data-testid='todo-project']"));
    }
    [Theory]
    [InlineData("Weekly")]
    [InlineData("Monthly")]
    public async Task New_Daily_reviews_complete_schedule_and_refuses_missing_or_unrepresentable_rules(string frequency)
    {
        var (f, _) = Setup(); await f.Controller.CreateLocalAsync();
        var cut = Render<TodoTaskPanel>(p => p.Add(x => x.Kind, CompanionTaskKind.Daily));
        cut.Find("[data-testid='todo-title']").Input(new string('W', 130) + " reviewed Daily");
        cut.Find("form").Submit(); Assert.Null(f.Controller.Preview); Assert.Empty(f.Controller.Snapshot!.Commands);
        cut.Find("[data-testid='daily-start']").Change("2026-10-19");
        cut.Find("[data-testid='daily-frequency']").Change(frequency);
        cut.Find("[data-testid='daily-interval']").Change("2");
        if (frequency == "Weekly") cut.Find("[data-testid='daily-creation-fields'] input[type=checkbox]").Change(true);
        else cut.Find("[data-testid='daily-month-days']").Change("31, 15");
        cut.Find("[data-testid='daily-zone']").Change("Europe/Paris");
        cut.Find("form").Submit(); Assert.Null(f.Controller.Preview); Assert.Empty(f.Controller.Snapshot.Commands);
        cut.Find("[data-testid='daily-zone']").Change("Etc/GMT-7");
        Save("todo-daily-create-" + frequency.ToLowerInvariant(), cut.Markup, "explicit-" + frequency.ToLowerInvariant() + "-fixed-zone-rule", nameof(New_Daily_reviews_complete_schedule_and_refuses_missing_or_unrepresentable_rules));
        cut.Find("form").Submit(); var preview = f.Controller.Preview!;
        Assert.True(preview.MayAffectHabitica); Assert.Equal(2, preview.Command.Patch!.Recurrence!.Interval);
        Assert.Equal("Etc/GMT-7", preview.Command.Patch.Recurrence.TimeZoneId);
        Assert.Contains("future missed Dailies", cut.Find("[data-testid='todo-review']").TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(f.Controller.Snapshot.Commands); Assert.Empty(f.Client.AcceptedIds);
    }

    [Theory]
    [InlineData(CompanionTaskKind.Todo)]
    [InlineData(CompanionTaskKind.Habit)]
    [InlineData(CompanionTaskKind.Daily)]
    public async Task Capture_rejects_invalid_Daily_inputs_and_retains_review_fences(CompanionTaskKind kind)
    {
        var (f, _) = Setup(); await f.Controller.CreateLocalAsync();
        var cut = Render<TodoTaskPanel>(p => p.Add(x => x.Kind, kind));
        cut.Find("[data-testid='todo-title']").Input("Synthetic capture with long retained title and reviewed game intent");
        if (kind == CompanionTaskKind.Daily)
        {
            cut.Find("form").Submit(); Assert.Null(f.Controller.Preview);
            cut.Find("[data-testid='daily-start']").Change("not-a-date");
            cut.Find("form").Submit(); Assert.Null(f.Controller.Preview);
            cut.Find("[data-testid='daily-start']").Change("2026-10-19");
            cut.Find("[data-testid='daily-interval']").Change("0");
            cut.Find("form").Submit(); Assert.Null(f.Controller.Preview);
            cut.Find("[data-testid='daily-interval']").Change("1");
            cut.Find("[data-testid='daily-zone']").Change("UTC");
        }
        var route = kind == CompanionTaskKind.Daily ? "/todo/dailies" : kind == CompanionTaskKind.Habit ? "/todo/habits" : "/todo/tasks";
        void Capture(string suffix) => Qualification.PageLayoutEvidence.Save("daily-order-"+kind.ToString().ToLowerInvariant()+suffix, cut.Markup, route, "real-task-panel-capture"+suffix, ["capture", "primary-action", "native-inputs", "disabled-controls"], nameof(Capture_rejects_invalid_Daily_inputs_and_retains_review_fences));
        Capture("-ready");
        cut.Find("form").Submit(); Assert.NotNull(f.Controller.Preview);
        Assert.Equal(kind == CompanionTaskKind.Daily ? WorkspaceOperation.CreateDaily : kind == CompanionTaskKind.Habit ? WorkspaceOperation.CreateHabit : WorkspaceOperation.CreateTodo, f.Controller.Preview.Command.Operation);
        Assert.True(cut.Find("form.todo-capture fieldset").HasAttribute("disabled"));
        Capture("-review");
        Assert.Empty(f.Controller.Snapshot!.Commands); Assert.Empty(f.Client.AcceptedIds);
        cut.Find("[data-testid='todo-cancel']").Click();
        Assert.False(cut.Find("form.todo-capture fieldset").HasAttribute("disabled"));
        Assert.Contains("Synthetic capture", cut.Find("[data-testid='todo-title']").GetAttribute("value"));
    }

    [Fact]
    public async Task Habit_capture_reviews_configured_directions_and_keeps_urgency_separate_from_game_difficulty_without_consent()
    {
        var(f,_) = Setup();await f.Controller.CreateLocalAsync();var cut=Render<TodoTaskPanel>(p=>p.Add(x=>x.Kind,CompanionTaskKind.Habit));
        cut.Find("[data-testid='todo-title']").Input("Synthetic negative-only Habit");cut.Find("[data-testid='todo-habit-positive']").Change(false);
        cut.Find("[data-testid='todo-priority']").Change("Urgent");cut.FindAll("select").Single(el=>el.ParentElement!.TextContent.StartsWith("Game difficulty",StringComparison.Ordinal)).Change("2");
        Assert.Empty(cut.FindAll("[data-testid='todo-project']"));Assert.Empty(cut.FindAll("[data-testid='todo-deadline']"));
        Assert.Contains("separate account consent",cut.Find("[data-testid='todo-required-integration']").TextContent);
        Save("defaults-habit-config",cut.Markup.Replace("<details class=\"todo-advanced\"","<details open class=\"todo-advanced\"",StringComparison.Ordinal),"negative-only-Habit-configuration",nameof(Habit_capture_reviews_configured_directions_and_keeps_urgency_separate_from_game_difficulty_without_consent));
        cut.Find("form").Submit();var command=f.Controller.Preview!.Command;Assert.True(command.ProjectToHabitica);Assert.Equal(CompanionPriority.Urgent,command.Patch!.Priority);Assert.Equal(2,command.Patch.HabiticaDetails!.Difficulty);Assert.False(command.Patch.HabiticaDetails.Up);Assert.True(command.Patch.HabiticaDetails.Down);
        Save("defaults-habit-review",cut.Markup,"required-direction-game-intent",nameof(Habit_capture_reviews_configured_directions_and_keeps_urgency_separate_from_game_difficulty_without_consent));
        cut.Find("[data-testid='todo-confirm']").Click();Assert.Empty(f.Client.AcceptedIds);Assert.Empty(f.Client.PreparationIds);Assert.Equal(ProjectionCommandState.Pending,f.Controller.Snapshot!.Commands.Single().ProjectionState);Assert.Empty(f.Controller.Snapshot.Document.HabitScores);
    }
    [Theory]
    [InlineData(false,false,false,"unverified")]
    [InlineData(true,false,false,"unavailable")]
    [InlineData(true,true,false,"paused")]
    [InlineData(true,true,true,"enabled")]
    public async Task Cloud_readiness_is_explicit_and_does_not_offer_a_reward_free_opt_out(bool verified,bool available,bool enabled,string state)
    {
        var(f,todo)=Setup();f.Client.Connected=true;f.Client.ConnectionStatusVerified=verified;f.Client.ExecutorAvailable=available;f.Client.ExecutorEnabled=enabled;
        await todo.RefreshSourcesAsync();await todo.SelectAsync(new(TodoWorkspaceSource.Cloud,f.Client.State.Document.Id));var cut=Render<TodoTaskPanel>();
        Assert.Contains(state,cut.Find("[data-testid='todo-required-integration']").TextContent,StringComparison.OrdinalIgnoreCase);Assert.Empty(cut.FindAll("[data-testid='todo-project']"));
        Assert.Empty(f.Client.PreparationIds);Assert.Empty(f.Client.AcceptedIds);
        Save("defaults-cloud-"+state,cut.Markup,"required-game-intent-"+state,nameof(Cloud_readiness_is_explicit_and_does_not_offer_a_reward_free_opt_out));
    }

    [Theory]
    [InlineData(CompanionTaskKind.Todo,"/todo/tasks")]
    [InlineData(CompanionTaskKind.Habit,"/todo/habits")]
    [InlineData(CompanionTaskKind.Daily,"/todo/dailies")]
    public async Task Same_named_task_controls_keep_exact_context_preview_targets_and_disabled_fences(CompanionTaskKind kind,string route)
    {
        var (f,_) = Setup();await f.Controller.CreateLocalAsync();var cut=Render<TodoTaskPanel>(p=>p.Add(x=>x.Kind,kind));
        const string title="Same task with a long descriptive title for record navigation";
        for(var i=0;i<2;i++)
        {
            cut.Find("[data-testid='todo-title']").Input(i==0?title:title.Replace(" ","  "));cut.Find("[data-testid='todo-notes']").Input("Retained private task notes");
            if(kind==CompanionTaskKind.Daily)cut.Find("[data-testid='daily-start']").Change("2026-11-03");
            cut.Find("form").Submit();Assert.NotNull(f.Controller.Preview);cut.Find("[data-testid='todo-confirm']").Click();
        }
        var tasks=f.Controller.Snapshot!.Document.Tasks.ToArray();Assert.Equal(2,tasks.Length);
        foreach(var task in tasks)
        {
            var row=cut.Find($"[data-testid='todo-row-{task.Id:D}']");Assert.Contains(task.Id.ToString("D"),row.TextContent);
            foreach(var action in row.QuerySelectorAll(".todo-row-actions button,.todo-row-actions a"))
            {var name=action.GetAttribute("aria-label");Assert.Contains(task.Text,name);Assert.Contains(task.Id.ToString("D"),name);Assert.Contains(action.TextContent.Trim(),name);}
            var notes=row.QuerySelector("details summary")!;Assert.Contains(task.Text,notes.GetAttribute("aria-label"));Assert.Contains(task.Id.ToString("D"),notes.GetAttribute("aria-label"));
            Assert.NotNull(cut.Find("#"+notes.GetAttribute("aria-controls")));
            if(kind==CompanionTaskKind.Daily){var date=cut.Find($"[data-testid='todo-occurrence-{task.Id:D}']");Assert.Contains(task.Text,date.GetAttribute("aria-label"));date.Change("2026-11-04");}
        }
        if(kind==CompanionTaskKind.Daily)cut.Find("[data-testid='daily-start']").Change("2026-11-03");
        Qualification.PageLayoutEvidence.Save("workspace-names-"+kind.ToString().ToLowerInvariant()+"-ready",cut.Markup,route,"same-named-long-tasks-ready",["record-actions","long-content","persistent-labels"],nameof(Same_named_task_controls_keep_exact_context_preview_targets_and_disabled_fences));
        var evidence=Environment.GetEnvironmentVariable("HABITICA_BROWSER_EVIDENCE");
        if(!string.IsNullOrWhiteSpace(evidence))File.WriteAllText(Path.Combine(evidence,"workspace-names-"+kind.ToString().ToLowerInvariant()+"-journal.json"),WorkspaceJournalJson.Serialize(f.Controller.Snapshot));
        var before=WorkspaceJournalJson.Serialize(f.Controller.Snapshot);var id=tasks[1].Id;
        cut.Find($"[data-testid='{(kind==CompanionTaskKind.Habit?"todo-down":"todo-complete")}-{id:D}']").Click();
        Assert.Equal(id,f.Controller.Preview!.Command.TaskId);Assert.Equal(kind==CompanionTaskKind.Habit?WorkspaceOperation.ScoreHabit:WorkspaceOperation.CompleteTask,f.Controller.Preview.Command.Operation);
        if(kind==CompanionTaskKind.Habit)Assert.Equal(HabitScoreDirection.Down,f.Controller.Preview.Command.Direction);
        if(kind==CompanionTaskKind.Daily)Assert.Equal(new DateOnly(2026,11,4),f.Controller.Preview.Command.OccurrenceDate);
        Assert.All(cut.FindAll(".todo-row-actions button"),button=>Assert.True(button.HasAttribute("disabled")));
        Qualification.PageLayoutEvidence.Save("workspace-names-"+kind.ToString().ToLowerInvariant()+"-frozen",cut.Markup,route,"same-named-tasks-original-review-fence",["record-actions","long-content","disabled-controls"],nameof(Same_named_task_controls_keep_exact_context_preview_targets_and_disabled_fences));
        cut.Find("[data-testid='todo-cancel']").Click();Assert.Equal(before,WorkspaceJournalJson.Serialize(f.Controller.Snapshot));Assert.Empty(f.Client.AcceptedIds);
    }

}
