using System.Text.Json;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceTaskRecoveryTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-27T12:00:00Z");
    private static readonly DateOnly Day = new(2026, 10, 27);
    private static WorkspaceJournalSnapshot Seed(CompanionTaskKind kind = CompanionTaskKind.Daily) => new(new WorkspaceDocument
    {
        Id = Guid.NewGuid(), Tasks = [new(Guid.NewGuid(), 1, "Synthetic recovery", kind, CompanionTaskState.Active,
            CompanionPriority.Normal, Now, Now, Recurrence: kind == CompanionTaskKind.Daily ? new(RecurrenceFrequency.Daily, 1, Day, "UTC", [], []) : null)]
    }, 1, [], []);
    private static WorkspaceCommand Command(WorkspaceJournalSnapshot state, WorkspaceOperation operation, WorkspaceTaskPatch? patch = null) => new(Guid.NewGuid(), state.Document.Id, state.Document.Revision, operation, state.Document.Tasks[0].Id, patch,
        OccurrenceDate: operation is WorkspaceOperation.CompleteTask or WorkspaceOperation.SkipOccurrence && state.Document.Tasks[0].Kind == CompanionTaskKind.Daily ? Day : null);
    private static WorkspaceJournalSnapshot Apply(WorkspaceJournalSnapshot state, WorkspaceCommand command)
    {
        var result = WorkspaceCommandEngine.Accept(state, command, Now);
        Assert.Equal(SubmissionDisposition.Accepted, result.Disposition);
        Assert.Empty(result.Entry!.Effects); Assert.Equal(ProjectionCommandState.NotRequested, result.Entry.ProjectionState);
        Assert.True(WorkspaceJournalValidation.IsValid(result.Snapshot!));
        return result.Snapshot!;
    }
    private static WorkspaceCommand Correct(WorkspaceJournalSnapshot state)
    {
        var occurrence = Assert.Single(state.Document.Occurrences);
        var source = state.Commands.Last(row => row.Command.Operation is WorkspaceOperation.CompleteTask or WorkspaceOperation.SkipOccurrence);
        return Command(state, WorkspaceOperation.CorrectOccurrence, new(CorrectOccurrence: new(occurrence.Id, occurrence.Revision, source.Command.Id)));
    }
    private static WorkspaceCommand Unarchive(WorkspaceJournalSnapshot state, WorkspaceTaskPlanning? planning = null) => Command(state, WorkspaceOperation.UnarchiveTask, new(Unarchive: new(state.Document.Tasks[0].Revision, planning)));

    [Fact]
    public void V11_hashes_match_independently_encoded_neutral_fixtures()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "task-recovery-command-v11.json")));
        foreach (var row in fixture.RootElement.GetProperty("cases").EnumerateArray())
            Assert.Equal(row.GetProperty("payloadHash").GetString(), WorkspaceCommandIdentity.Hash(WorkspaceTaskApiJson.Deserialize<WorkspaceCommand>(row.GetProperty("command").GetRawText())));
    }

    [Fact]
    public void Worker_journal_round_trips_with_exact_causal_corrections_and_original_receipts()
    {
        var state = WorkspaceJournalJson.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "task-recovery-journal-v1.json")));
        Assert.True(WorkspaceJournalValidation.IsValid(state)); Assert.Equal(2, state.Document.OccurrenceCorrections!.Count);
        Assert.Single(state.Document.Unarchives!); Assert.Single(state.Document.Completions);
        Assert.Equal(OccurrenceState.Pending, state.Document.Occurrences[0].State);
        Assert.All(state.Commands, row => Assert.Empty(row.Effects));
        Assert.True(WorkspaceJournalValidation.IsValid(WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(state))));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Corrections_retain_terminal_snapshot_and_completion_cycles_without_effects(bool skip)
    {
        var state = Seed(); state = Apply(state, Command(state, skip ? WorkspaceOperation.SkipOccurrence : WorkspaceOperation.CompleteTask));
        var previous = Assert.Single(state.Document.Occurrences); var completions = state.Document.Completions.ToArray(); var correction = Correct(state);
        state = Apply(state, correction);
        Assert.Equal(previous, Assert.Single(state.Document.OccurrenceCorrections!).PreviousOccurrence);
        Assert.Equal(completions, state.Document.Completions); Assert.Equal(OccurrenceState.Pending, state.Document.Occurrences[0].State);
        Assert.Equal(SubmissionDisposition.Duplicate, WorkspaceCommandEngine.Accept(state, correction, Now).Disposition);
        Assert.Equal(SubmissionDisposition.Invalid, WorkspaceCommandEngine.Accept(state, correction with { Id = Guid.NewGuid(), ExpectedRevision = state.Document.Revision }, Now).Disposition);
        state = Apply(state, Command(state, WorkspaceOperation.CompleteTask));
        state = Apply(state, Correct(state));
        Assert.Equal(2, state.Document.OccurrenceCorrections!.Count); Assert.Equal(skip ? 1 : 2, state.Document.Completions.Count);
        Assert.All(WorkspaceCompletionHistory.Evaluate(state, new()).Items, row => Assert.NotNull(row.OccurrenceCorrection));
        Assert.Single(state.Document.Occurrences); Assert.Equal(previous.Id, state.Document.Occurrences[0].Id);
        Assert.True(WorkspaceJournalValidation.IsValid(WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(state))));
        Assert.Empty(WorkspaceRules.Validate(WorkspaceJson.Deserialize(WorkspaceJson.Serialize(state.Document))));
    }

    [Theory]
    [InlineData(CompanionTaskKind.Todo)] [InlineData(CompanionTaskKind.Daily)] [InlineData(CompanionTaskKind.Habit)]
    public void Unarchive_preserves_state_and_identity_and_does_not_undo_completion(CompanionTaskKind kind)
    {
        var state = Seed(kind);
        if (kind == CompanionTaskKind.Todo) state = Apply(state, Command(state, WorkspaceOperation.CompleteTask));
        state = Apply(state, Command(state, WorkspaceOperation.DeleteTask)); var archived = state.Document.Tasks[0]; var unarchive = Unarchive(state);
        state = Apply(state, unarchive);
        Assert.Equal(archived with { Revision = state.Document.Revision, IsArchived = false }, state.Document.Tasks[0]);
        Assert.Equal(archived.Revision, Assert.Single(state.Document.Unarchives!).ArchivedRevision);
        Assert.Equal(SubmissionDisposition.Duplicate, WorkspaceCommandEngine.Accept(state, unarchive, Now).Disposition);
        Assert.True(WorkspaceJournalValidation.IsValid(WorkspaceJournalJson.Deserialize(WorkspaceJournalJson.Serialize(state))));
    }

    [Fact]
    public void Archive_before_correction_requires_separate_unarchive_and_missing_original_source_refuses()
    {
        var state = Seed(); state = Apply(state, Command(state, WorkspaceOperation.SkipOccurrence)); var correction = Correct(state);
        state = Apply(state, Command(state, WorkspaceOperation.DeleteTask));
        Assert.Equal("task_unavailable", WorkspaceCommandEngine.Accept(state, correction with { ExpectedRevision = state.Document.Revision }, Now).ErrorCode);
        state = Apply(state, Unarchive(state));
        state = Apply(state, correction with { ExpectedRevision = state.Document.Revision });
        var terminal = Apply(state, Command(state, WorkspaceOperation.SkipOccurrence)); var next = Correct(terminal);
        // The retained earlier correction now also requires its original source journal.
        Assert.Equal("invalid_workspace", WorkspaceCommandEngine.Accept(terminal with { Commands = [], Events = [] }, next, Now).ErrorCode);
        var first=Seed();first=Apply(first,Command(first,WorkspaceOperation.SkipOccurrence));
        Assert.Equal("invalid_workspace",WorkspaceCommandEngine.Accept(first with {Commands=[],Events=[]},Correct(first),Now).ErrorCode);
        Assert.Equal("occurrence_source_unavailable",WorkspaceCommandEngine.Accept(first with {Document=first.Document with {ActivityCoverage=null},Commands=[],Events=[]},Correct(first),Now).ErrorCode);
    }

    [Fact]
    public void Archived_organization_requires_explicit_reviewed_repair_instead_of_silent_detachment()
    {
        var state = Seed(CompanionTaskKind.Todo); var project = new WorkspaceProject(Guid.NewGuid(), 1, "Archived project", IsArchived: true);
        state = state with { Document = state.Document with { Projects = [project], Tasks = [state.Document.Tasks[0] with { ProjectId = project.Id, IsArchived = true }] } };
        var original = state.Document.Tasks[0];
        Assert.Equal("unarchive_repair_required", WorkspaceCommandEngine.Accept(state, Unarchive(state), Now).ErrorCode);
        Assert.Equal(original, state.Document.Tasks[0]);
        state = Apply(state, Unarchive(state, new(ClearProject: true)));
        Assert.Null(state.Document.Tasks[0].ProjectId); Assert.Single(state.Document.Projects);
        Assert.Equal(SubmissionDisposition.Invalid, WorkspaceCommandEngine.Accept(state, Unarchive(state), Now).Disposition);
    }

    [Fact]
    public void Malformed_stale_foreign_and_concurrent_corrections_are_refused_without_mutation()
    {
        var state = Seed(); state = Apply(state, Command(state, WorkspaceOperation.CompleteTask)); var command = Correct(state); var before = WorkspaceJournalJson.Serialize(state);
        foreach (var bad in new[] { command with { Patch = null }, command with { ProjectToHabitica = true }, command with { Count = 2 }, command with { Direction = HabitScoreDirection.Down }, command with { OccurrenceDate = Day }, command with { Patch = command.Patch! with { Text = "Unexpected" } }, command with { Patch = new(CorrectOccurrence: command.Patch!.CorrectOccurrence! with { OccurrenceId = Guid.NewGuid() }) }, command with { Patch = new(CorrectOccurrence: command.Patch!.CorrectOccurrence! with { TargetCommandId = Guid.NewGuid() }) } })
            Assert.Equal(SubmissionDisposition.Invalid, WorkspaceCommandEngine.Accept(state, bad, Now).Disposition);
        Assert.Equal(SubmissionDisposition.RevisionConflict, WorkspaceCommandEngine.Accept(state, command with { ExpectedRevision = 1 }, Now).Disposition);
        var accepted = Apply(state, command);
        Assert.Equal(SubmissionDisposition.RevisionConflict, WorkspaceCommandEngine.Accept(accepted, command with { Id = Guid.NewGuid() }, Now).Disposition);
        Assert.Equal(SubmissionDisposition.IdempotencyConflict, WorkspaceCommandEngine.Accept(accepted, command with { Patch = new(CorrectOccurrence: command.Patch!.CorrectOccurrence! with { TargetCommandId = Guid.NewGuid() }) }, Now).Disposition);
        Assert.Equal(before, WorkspaceJournalJson.Serialize(state));
    }

    [Theory]
    [InlineData(ProjectionState.Linked)] [InlineData(ProjectionState.Pending)] [InlineData(ProjectionState.UnknownOutcome)] [InlineData(ProjectionState.ExternalConflict)]
    public void Retained_game_mapping_blocks_both_recovery_operations_even_when_archived(ProjectionState projection)
    {
        var state = Seed(); state = Apply(state, Command(state, WorkspaceOperation.CompleteTask)); var command = Correct(state);
        state = state with { Document = state.Document with { Projections = [new(Guid.NewGuid(), 1, command.TaskId, projection, Guid.NewGuid().ToString(), IsArchived: true)] } };
        Assert.Equal("unsupported_projection", WorkspaceCommandEngine.Accept(state, command, Now).ErrorCode);
        state = state with { Document = state.Document with { ActivityCoverage = null, Tasks = [state.Document.Tasks[0] with { IsArchived = true }] } }; // Synthetic legacy archive has no causal witness.
        Assert.Equal("unsupported_projection", WorkspaceCommandEngine.Accept(state, Unarchive(state), Now).ErrorCode);
    }

    [Fact]
    public void Tampered_causal_history_and_receipt_effects_fail_portable_validation()
    {
        var state = Seed(); state = Apply(state, Command(state, WorkspaceOperation.CompleteTask)); state = Apply(state, Correct(state)); var row = state.Document.OccurrenceCorrections![0];
        foreach (var bad in new[] { row with { TargetCommandId = Guid.NewGuid() }, row with { PreviousOccurrence = row.PreviousOccurrence with { Revision = row.Revision } }, row with { PreviousOccurrence = row.PreviousOccurrence with { Date = Day.AddDays(1) } }, row with { PreviousOccurrence = row.PreviousOccurrence with { State = OccurrenceState.Pending } } })
            Assert.NotEmpty(WorkspaceRules.Validate(state.Document with { OccurrenceCorrections = [bad] }));
        Assert.NotEmpty(WorkspaceRules.Validate(state.Document with { OccurrenceCorrections = [row, row with { Id = Guid.NewGuid() }] }));
        var last = state.Commands[^1];
        Assert.False(WorkspaceJournalValidation.IsValid(state with { Commands = state.Commands.SkipLast(1).Append(last with { Effects = [ProjectionEffectKind.ScoreDown], ProjectionState = ProjectionCommandState.Pending }).ToArray() }));
        Assert.Equal(17, (int)WorkspaceOperation.UnarchiveTask); Assert.Equal(18, (int)WorkspaceOperation.CorrectOccurrence);
        Assert.False(WorkspaceTaskApiJson.Element(new WorkspaceTaskPatch(Text: "Legacy")).TryGetProperty("unarchive", out _));
    }
}
