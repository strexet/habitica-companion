using Bunit;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.WebApp.Components;
using Habitica.WebApp.State;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Habitica.WebApp.Tests.Workspace;

public sealed class TodoHistoryPanelTests : BunitContext
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = new(2026, 10, 1);
    private static WorkspaceJournalSnapshot Empty() => new(new WorkspaceDocument { Id = Guid.NewGuid() }, 1, [], []);
    private static WorkspaceJournalSnapshot Apply(WorkspaceJournalSnapshot state, WorkspaceOperation operation, Guid id, WorkspaceTaskPatch? patch = null, DateOnly? date = null, HabitScoreDirection? direction = null)
    {
        var result = WorkspaceCommandEngine.Accept(state, new(Guid.NewGuid(), state.Document.Id, state.Document.Revision, operation, id, patch, Direction: direction, OccurrenceDate: date), Now.AddSeconds(state.Document.Revision));
        Assert.Equal(SubmissionDisposition.Accepted, result.Disposition); return result.Snapshot!;
    }
    private static WorkspaceJournalSnapshot CompletedTodo(out Guid id)
    {
        id = Guid.NewGuid(); var state = Apply(Empty(), WorkspaceOperation.CreateTodo, id, new(Text: "Original completion title", Notes: "Original notes\nsecond line"));
        return Apply(state, WorkspaceOperation.CompleteTask, id);
    }
    private async Task<(WorkspaceTaskFixture Fixture, TodoWorkspacePresentation Todo)> Setup(WorkspaceJournalSnapshot state)
    {
        Assert.True(WorkspaceJournalValidation.IsValid(state)); JSInterop.Mode = JSRuntimeMode.Loose; Services.AddMudServices();
        var f = new WorkspaceTaskFixture(); f.Client.State = state; f.Client.Connected = true; await f.Controller.LoadAsync(true);
        Services.AddSingleton<IAppSessionController>(f.Session); Services.AddSingleton<IWorkspaceCloudClient>(f.Client); Services.AddSingleton(f.Database); Services.AddSingleton(f.Controller);
        var todo = new TodoWorkspacePresentation(f.Session, f.Client, f.Database, f.Controller); Services.AddSingleton(todo); Services.AddSingleton<TodoTaskDraftStore>();
        return (f, todo);
    }
    private static void Save(string name, string markup, string state, string test) => Qualification.PageLayoutEvidence.Save(name, markup, "/todo/history", state, ["form", "actions", "confirmation", "disabled-controls", "error"], test);

    [Fact]
    public async Task Retained_history_labels_historical_snapshots_current_fallback_UTC_civil_dates_and_archive_flags_without_writes()
    {
        var state = CompletedTodo(out var todoId); state = Apply(state, WorkspaceOperation.UpdateTask, todoId, new(Text: "Renamed current task", Notes: "Current notes"));
        var daily = Guid.NewGuid(); state = Apply(state, WorkspaceOperation.CreateDaily, daily, new(Text: "Daily completion")); state = Apply(state, WorkspaceOperation.CompleteTask, daily, date: Day);
        state = Apply(state, WorkspaceOperation.DeleteTask, daily);
        state = state with { Document = state.Document with { Completions = state.Document.Completions.Select(row => row.TaskId == daily ? row with { IsArchived = true } : row).ToArray() } };
        var (f, todo) = await Setup(state); var before = WorkspaceJournalJson.Serialize(f.Client.State); var cut = Render<TodoHistoryPanel>();
        Assert.Equal(2, cut.FindAll("[data-testid^='history-row-']").Count); Assert.Contains("Original completion title", cut.Markup); Assert.DoesNotContain("Renamed current task", cut.Markup);
        Assert.Contains("Historical details saved", cut.Markup); Assert.Contains("Recorded UTC: 2026-10-01", cut.Markup); Assert.Contains("Daily civil occurrence date: 2026-10-01", cut.Markup); Assert.Contains("Task archived: Yes", cut.Markup); Assert.Contains("Event archived: Yes", cut.Markup);
        Save("todo-history-completions", cut.Markup, "snapshot-archive-and-civil-labels", nameof(Retained_history_labels_historical_snapshots_current_fallback_UTC_civil_dates_and_archive_flags_without_writes));
        Save("todo-history-expanded", cut.Markup.Replace("<details><summary>Retained notes", "<details open><summary>Retained notes", StringComparison.Ordinal).Replace("<details><summary>Event references", "<details open><summary>Event references", StringComparison.Ordinal), "expanded-retained-notes-and-identities", nameof(Retained_history_labels_historical_snapshots_current_fallback_UTC_civil_dates_and_archive_flags_without_writes));
        cut.Find("[data-testid='history-query']").Change("Original notes"); Assert.Single(cut.FindAll("[data-testid^='history-row-']"));
        Assert.Empty(f.Client.PreparationIds); Assert.Equal(before, WorkspaceJournalJson.Serialize(f.Client.State));
        f.Client.State = state with { Document = state.Document with { ActivityCoverage = null }, Commands = [] }; await cut.InvokeAsync(() => todo.LoadSelectedAsync()); cut.Find("[data-testid='history-query']").Change("Renamed");
        Assert.Contains("Current task details (historical snapshot unavailable)", cut.Markup); Assert.Contains("Current notes", cut.Markup);
    }

    [Fact]
    public async Task Exact_native_restore_recovers_original_receipt_retains_cycles_and_never_reverses_game_rewards()
    {
        var state = CompletedTodo(out var id); var completion = state.Document.Completions.Single(); var (f, todo) = await Setup(state); var cut = Render<TodoHistoryPanel>();
        cut.Find($"[data-testid='history-restore-{completion.Id:D}']").Click();
        var preview = f.Controller.Preview!; Assert.Equal(WorkspaceOperation.RestoreTodo, preview.Command.Operation); Assert.Equal(completion.Id, preview.Command.Patch!.RestoreTodo!.CompletionId); Assert.False(preview.MayAffectHabitica);
        Assert.Contains(completion.Id.ToString(), cut.Find("[data-testid='todo-review']").TextContent); Assert.Contains("No Habitica reward is reversed", cut.Markup);
        Save("todo-history-review", cut.Markup, "exact-restoration-review", nameof(Exact_native_restore_recovers_original_receipt_retains_cycles_and_never_reverses_game_rewards));
        f.Client.LoseAcceptance = true; cut.Find("[data-testid='todo-confirm']").Click(); Assert.True(f.Controller.AcceptanceUnknown);
        Save("todo-history-unknown", cut.Markup, "original-restoration-uncertain", nameof(Exact_native_restore_recovers_original_receipt_retains_cycles_and_never_reverses_game_rewards));
        cut.Find("[data-testid='todo-recover']").Click(); Assert.Equal(preview.PreparedCommandId, f.Controller.LastReceipt!.CommandId); Assert.Single(f.Client.AcceptedIds);
        Assert.Equal(CompanionTaskState.Active, f.Controller.Snapshot!.Document.Tasks.Single().State); Assert.Equal(completion, Assert.Single(f.Controller.Snapshot.Document.Completions)); Assert.Single(f.Controller.Snapshot.Document.Restorations!);
        Assert.Empty(cut.FindAll("[data-testid^='history-restore-']")); Assert.Contains("already been restored", cut.Markup);
        await cut.InvokeAsync(async () => { await todo.PrepareAsync(WorkspaceTaskFixture.Todo() with { Operation = WorkspaceOperation.CompleteTask, TaskId = id }); await todo.ConfirmAsync(); });
        Assert.Equal(2, cut.FindAll("[data-testid^='history-row-']").Count); Assert.Empty(cut.FindAll("[data-testid^='history-restore-']"));
        Assert.Single(f.Controller.Snapshot.Document.Restorations!); Assert.Equal(2, f.Controller.Snapshot.Document.Completions.Count);
        Assert.All(f.Controller.Snapshot.Commands.SkipLast(1), command => Assert.Empty(command.Effects));
        Assert.Equal(new[] { ProjectionEffectKind.Create, ProjectionEffectKind.ScoreUp }, f.Controller.Snapshot.Commands[^1].Effects);
        Assert.Contains(JSInterop.Invocations, invocation => invocation.Identifier == "Blazor._internal.domWrapper.focus");
    }

    [Fact]
    public async Task Linked_uncertain_archived_and_retained_external_mappings_never_offer_or_prepare_native_restore()
    {
        var rows = new List<CompanionTask>(); var completions = new List<CompletionEvent>(); var mappings = new List<HabiticaProjection>();
        foreach (var mapping in new[] { ProjectionState.Linked, ProjectionState.Pending, ProjectionState.UnknownOutcome, ProjectionState.ExternalConflict, ProjectionState.Unlinked })
        {
            var state = CompletedTodo(out var id); rows.Add(state.Document.Tasks.Single()); completions.Add(state.Document.Completions.Single());
            mappings.Add(new(Guid.NewGuid(), 1, id, mapping, Guid.NewGuid().ToString("D"), IsArchived: true));
        }
        var archived = CompletedTodo(out _); rows.Add(archived.Document.Tasks.Single() with { IsArchived = true }); completions.Add(archived.Document.Completions.Single());
        var stateAll = new WorkspaceJournalSnapshot(new WorkspaceDocument { Id = Guid.NewGuid(), Revision = 10, Tasks = rows, Completions = completions, Projections = mappings }, 10, [], []);
        var (f, _) = await Setup(stateAll); var cut = Render<TodoHistoryPanel>(); Assert.Empty(cut.FindAll("[data-testid^='history-restore-']")); Assert.Contains("separate reconciliation", cut.Markup); Assert.Contains("Archived or unavailable", cut.Markup);
        await Assert.ThrowsAsync<InvalidOperationException>(() => cut.InvokeAsync(() => cut.FindComponent<TodoTaskPanel>().Instance.ReviewRestoreAsync(rows[0].Id, completions[0].Id, 10)));
        Assert.Empty(f.Client.PreparationIds); Assert.Empty(f.Client.AcceptedIds);
    }

    [Fact]
    public async Task Habit_direction_search_and_inclusive_UTC_days_filter_retained_events_without_undo_or_inferred_streaks()
    {
        var id = Guid.NewGuid(); var state = Apply(Empty(), WorkspaceOperation.CreateHabit, id, new(Text: new string('W', 130) + " Habit", Notes: "Retained Habit notes"));
        state = Apply(state, WorkspaceOperation.ScoreHabit, id, direction: HabitScoreDirection.Up); state = Apply(state, WorkspaceOperation.ScoreHabit, id, direction: HabitScoreDirection.Down);
        var (f, _) = await Setup(state); var cut = Render<TodoHistoryPanel>(); cut.Find("[data-testid='history-activity']").Change("Habit");
        Assert.Equal(2, cut.FindAll("[data-testid^='history-row-']").Count); Assert.Empty(cut.FindAll("[data-testid^='history-restore-']")); Assert.Contains("Habit undo is unavailable", cut.Markup);
        cut.Find("[data-testid='history-direction']").Change("Down"); Assert.Single(cut.FindAll("[data-testid^='history-row-']"));
        cut.Find("[data-testid='history-from']").Change("2026-10-01"); cut.Find("[data-testid='history-through']").Change("2026-10-01"); Assert.Single(cut.FindAll("[data-testid^='history-row-']"));
        Save("todo-history-habit", cut.Markup, "long-habit-expanded-UTC-and-direction-filters", nameof(Habit_direction_search_and_inclusive_UTC_days_filter_retained_events_without_undo_or_inferred_streaks));
        cut.Find("[data-testid='history-query']").Change("Retained Habit notes"); Assert.Single(cut.FindAll("[data-testid^='history-row-']"));
        cut.Find("[data-testid='history-through']").Change("2026-09-30"); Assert.NotNull(cut.Find("[data-testid='history-error']")); Assert.Empty(cut.FindAll("[data-testid^='history-row-']"));
        Save("todo-history-error", cut.Markup, "invalid-UTC-range", nameof(Habit_direction_search_and_inclusive_UTC_days_filter_retained_events_without_undo_or_inferred_streaks));
        cut.FindAll("button").Single(button => button.TextContent == "Clear filters").Click(); Assert.Equal("Completions", cut.Find("[data-testid='history-activity']").GetAttribute("value"));
        cut.Find("[data-testid='history-activity']").Change("Habit"); Assert.Equal("", cut.Find("[data-testid='history-direction']").GetAttribute("value"));
        Assert.Empty(f.Client.PreparationIds); Assert.Empty(f.Client.AcceptedIds);
    }

    [Fact]
    public async Task Stable_paging_resets_on_filters_and_revision_and_empty_is_distinct_from_unavailable()
    {
        var id = Guid.NewGuid(); var state = Apply(Empty(), WorkspaceOperation.CreateDaily, id, new(Text: "Paged Daily"));
        for (var i = 0; i < 25; i++) state = Apply(state, WorkspaceOperation.CompleteTask, id, date: Day.AddDays(i));
        var (f, todo) = await Setup(state); var cut = Render<TodoHistoryPanel>(); Assert.Equal(20, cut.FindAll("[data-testid^='history-row-']").Count);
        cut.Find("[data-testid='history-next']").Click(); Assert.Equal(5, cut.FindAll("[data-testid^='history-row-']").Count);
        f.Client.State = state with { Document = state.Document with { Revision = state.Document.Revision + 1 }, StoreVersion = state.StoreVersion + 1 }; await cut.InvokeAsync(() => todo.LoadSelectedAsync());
        Assert.Equal(20, cut.FindAll("[data-testid^='history-row-']").Count); Assert.True(cut.Find("[data-testid='history-previous']").HasAttribute("disabled"));
        cut.Find("[data-testid='history-activity']").Change("Todo"); Assert.NotNull(cut.Find("[data-testid='history-empty']"));
        Save("todo-history-empty", cut.Markup, "empty-filtered-history", nameof(Stable_paging_resets_on_filters_and_revision_and_empty_is_distinct_from_unavailable));
        cut.Find("[data-testid='history-query']").Change("Private account search");
        f.Session.SetState(SessionViewModel.Empty); cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='history-empty']"))); Assert.Contains("Unavailable history is not an empty", cut.Markup);
        Assert.Equal("", cut.Find("[data-testid='history-query']").GetAttribute("value"));
        Assert.Empty(f.Client.PreparationIds);
    }
}
