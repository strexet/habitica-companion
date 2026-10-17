using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceOccurrenceLifecycleTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-17T00:00:00Z");
    private static readonly RecurrenceRule Rule = new(RecurrenceFrequency.Daily, 1, new(2026, 10, 17), "UTC", [], []);
    private static WorkspaceJournalSnapshot Empty() => new(new WorkspaceDocument { Id = Guid.NewGuid() }, 1, []);
    private static WorkspaceCommand Intent(WorkspaceJournalSnapshot state, Guid id, WorkspaceOperation operation, DateOnly? date = null, WorkspaceTaskPatch? patch = null, bool project = false) => new(Guid.NewGuid(), state.Document.Id, state.Document.Revision, operation, id, patch, OccurrenceDate: date, ProjectToHabitica: project);
    private static CommandSubmission Apply(WorkspaceJournalSnapshot state, WorkspaceCommand command) => WorkspaceCommandEngine.Accept(state, command, Now);
    private static WorkspaceJournalSnapshot Create(CompanionTaskKind kind, out Guid id)
    {
        var state = Empty(); id = Guid.NewGuid();
        var operation = kind == CompanionTaskKind.Daily ? WorkspaceOperation.CreateDaily : kind == CompanionTaskKind.Habit ? WorkspaceOperation.CreateHabit : WorkspaceOperation.CreateTodo;
        return Apply(state, Intent(state, id, operation, patch: new(Text: "Synthetic recurring work", Recurrence: kind == CompanionTaskKind.Daily ? Rule : null))).Snapshot!;
    }

    [Fact]
    public void AppendedOperationsRetainPortableIdentityEncoding()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "lifecycle-command-v1.json")));
        foreach (var value in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var command = WorkspaceTaskApiJson.Deserialize<WorkspaceCommand>(value.GetProperty("command").GetRawText());
            Assert.Equal(value.GetProperty("payloadHash").GetString(), WorkspaceCommandIdentity.Hash(command));
        }
        Assert.Equal(6, (int)WorkspaceOperation.DeleteTask);
    }

    [Fact]
    public void Skip_pause_resume_and_explicit_restart_retain_history_without_backfill()
    {
        var state = Create(CompanionTaskKind.Daily, out var id);
        var skipped = Apply(state, Intent(state, id, WorkspaceOperation.SkipOccurrence, new(2026, 10, 17)));
        Assert.Equal(SubmissionDisposition.Accepted, skipped.Disposition); state = skipped.Snapshot!;
        Assert.Equal(OccurrenceState.Skipped, Assert.Single(state.Document.Occurrences).State); Assert.Empty(state.Document.Completions); Assert.Empty(state.Document.HabitScores);
        Assert.Equal(SubmissionDisposition.Duplicate, Apply(state, skipped.Entry!.Command).Disposition);
        Assert.Equal("occurrence_unavailable", Apply(state, Intent(state, id, WorkspaceOperation.SkipOccurrence, new(2026, 10, 17))).ErrorCode);
        Assert.Equal("occurrence_unavailable", Apply(state, Intent(state, id, WorkspaceOperation.CompleteTask, new(2026, 10, 17))).ErrorCode);
        state = Apply(state, Intent(state, id, WorkspaceOperation.CompleteTask, new(2026, 10, 18))).Snapshot!;
        Assert.Equal("occurrence_unavailable", Apply(state, Intent(state, id, WorkspaceOperation.SkipOccurrence, new(2026, 10, 18))).ErrorCode);
        state = Apply(state, Intent(state, id, WorkspaceOperation.PauseTask)).Snapshot!;
        Assert.Equal(CompanionTaskState.Paused, state.Document.Tasks[0].State);
        Assert.Equal("task_not_completable", Apply(state, Intent(state, id, WorkspaceOperation.CompleteTask, new(2026, 10, 19))).ErrorCode);
        Assert.Equal("task_not_active", Apply(state, Intent(state, id, WorkspaceOperation.SkipOccurrence, new(2026, 10, 19))).ErrorCode);
        state = Apply(state, Intent(state, id, WorkspaceOperation.UpdateTask, patch: new(Recurrence: Rule with { StartDate = new(2026, 10, 20) }))).Snapshot!;
        state = Apply(state, Intent(state, id, WorkspaceOperation.ResumeTask)).Snapshot!;
        Assert.Equal(CompanionTaskState.Active, state.Document.Tasks[0].State);
        Assert.Equal(new DateOnly(2026, 10, 20), state.Document.Tasks[0].Recurrence!.StartDate);
        Assert.Equal(2, state.Document.Occurrences.Count); Assert.Single(state.Document.Completions);
        Assert.Equal("occurrence_not_scheduled", Apply(state, Intent(state, id, WorkspaceOperation.CompleteTask, new(2026, 10, 19))).ErrorCode);
        Assert.Equal(SubmissionDisposition.Duplicate, Apply(state, skipped.Entry.Command).Disposition);
        Assert.Equal(2, state.Document.Occurrences.Count);
        Assert.All(state.Commands, entry => { Assert.Empty(entry.Effects); Assert.Equal(ProjectionCommandState.NotRequested, entry.ProjectionState); });
        Assert.True(WorkspaceJournalValidation.IsValid(WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(state))));
    }

    [Fact]
    public void Habit_pause_blocks_new_scores_and_resume_preserves_history()
    {
        var state = Create(CompanionTaskKind.Habit, out var id);
        var score = new WorkspaceCommand(Guid.NewGuid(), state.Document.Id, state.Document.Revision, WorkspaceOperation.ScoreHabit, id, Direction: HabitScoreDirection.Up);
        state = Apply(state, score).Snapshot!;
        var paused = Apply(state, Intent(state, id, WorkspaceOperation.PauseTask)); state = paused.Snapshot!;
        Assert.Equal(SubmissionDisposition.Duplicate, Apply(state, score).Disposition);
        Assert.Equal("habit_not_active", Apply(state, score with { Id = Guid.NewGuid(), ExpectedRevision = state.Document.Revision }).ErrorCode);
        Assert.Equal("task_not_active", Apply(state, Intent(state, id, WorkspaceOperation.PauseTask)).ErrorCode);
        state = Apply(state, Intent(state, id, WorkspaceOperation.ResumeTask)).Snapshot!;
        Assert.Single(state.Document.HabitScores); Assert.Equal("task_not_paused", Apply(state, Intent(state, id, WorkspaceOperation.ResumeTask)).ErrorCode);
    }

    [Fact]
    public void Lifecycle_rejects_unsupported_mappings_kinds_dates_and_archive_effects()
    {
        var state = Create(CompanionTaskKind.Daily, out var id);
        foreach (var operation in new[] { WorkspaceOperation.SkipOccurrence, WorkspaceOperation.PauseTask, WorkspaceOperation.ResumeTask })
        {
            var date = operation == WorkspaceOperation.SkipOccurrence ? new DateOnly(2026, 10, 17) : (DateOnly?)null;
            Assert.Equal("unsupported_projection", Apply(state, Intent(state, id, operation, date, project: true)).ErrorCode);
            foreach (var projection in new[] { ProjectionState.Linked, ProjectionState.Pending, ProjectionState.UnknownOutcome, ProjectionState.ExternalConflict })
            {
                var mapped = state with { Document = state.Document with { Projections = [new(Guid.NewGuid(), state.Document.Revision, id, projection, projection == ProjectionState.Linked ? Guid.NewGuid().ToString("D") : null)] } };
                Assert.Equal("unsupported_projection", Apply(mapped, Intent(mapped, id, operation, date)).ErrorCode);
            }
        }
        Assert.Equal("occurrence_required", Apply(state, Intent(state, id, WorkspaceOperation.SkipOccurrence)).ErrorCode);
        var unscheduled = Apply(state, Intent(state, id, WorkspaceOperation.UpdateTask, patch: new(ClearRecurrence: true))).Snapshot!;
        Assert.Equal("recurrence_required", Apply(unscheduled, Intent(unscheduled, id, WorkspaceOperation.SkipOccurrence, new(2026, 10, 17))).ErrorCode);
        var todo = Create(CompanionTaskKind.Todo, out var todoId);
        Assert.Equal("recurring_task_required", Apply(todo, Intent(todo, todoId, WorkspaceOperation.PauseTask)).ErrorCode);
        var skip = Apply(state, Intent(state, id, WorkspaceOperation.SkipOccurrence, new(2026, 10, 17))); var entry = skip.Entry!;
        var command = entry.Command with { ProjectToHabitica = true };
        Assert.False(WorkspaceJournalValidation.IsValid(skip.Snapshot! with { Commands = skip.Snapshot!.Commands.Take(1).Append(entry with { Command = command, PayloadHash = WorkspaceCommandIdentity.Hash(command), Effects = [ProjectionEffectKind.ScoreUp], ProjectionState = ProjectionCommandState.Pending }).ToArray() }));
    }
}
