using System.Text.Json;
using System.Text.Json.Serialization;
using Habitica.Application.Workspace;
using Habitica.Domain.Workspace;
using Habitica.Storage;

namespace Habitica.Application.Tests.Workspace;

public sealed class WorkspaceCommandTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);
    private static WorkspaceJournalSnapshot Empty() => new(new WorkspaceDocument { Id = Guid.NewGuid() }, 1, Array.Empty<WorkspaceCommandEntry>());
    private static WorkspaceCommand Create(WorkspaceJournalSnapshot snapshot, bool project = false) => new(Guid.NewGuid(), snapshot.Document.Id, snapshot.Document.Revision, WorkspaceOperation.CreateTodo, Guid.NewGuid(), new(Text: "Read chapter"), ProjectToHabitica: project);
    private static WorkspaceJournalSnapshot Accept(WorkspaceJournalSnapshot state, WorkspaceCommand command) => WorkspaceCommandEngine.Accept(state, command, Now).Snapshot!;

    [Fact]
    public void AcceptedCommandAtomicallyContainsCanonicalDataHistoryAndProjectionIntent()
    {
        var original = Empty(); var command = Create(original, true);
        var result = WorkspaceCommandEngine.Accept(original, command, Now);
        Assert.True(result.SavedInCompanion); Assert.Equal(SubmissionDisposition.Accepted, result.Disposition);
        Assert.Empty(original.Document.Tasks); Assert.Empty(original.Commands);
        var next = result.Snapshot!;
        Assert.Single(next.Document.Tasks); Assert.Single(next.Commands); Assert.Single(next.Events!);
        Assert.Equal(2, next.Document.Revision); Assert.Equal(2, next.StoreVersion);
        Assert.Equal(ProjectionCommandState.Pending, result.Entry!.ProjectionState); Assert.Equal(new[] { ProjectionEffectKind.Create }, result.Entry.Effects);
        Assert.Equal(ProjectionState.Pending, Assert.Single(next.Document.Projections).State);
        Assert.Empty(WorkspaceRules.Validate(next.Document));
    }

    [Fact]
    public void AcceptedTaskSnapshotPreservesIntentAcrossLaterEditsAndRejectsMismatchedHistory()
    {
        var state = Empty(); var create = Create(state, true); state = Accept(state, create);
        var accepted = Assert.Single(state.Commands).TaskSnapshot;
        Assert.NotNull(accepted); Assert.Equal("Read chapter", accepted.Text);
        state = Accept(state, new(Guid.NewGuid(), state.Document.Id, state.Document.Revision, WorkspaceOperation.UpdateTask, create.TaskId, new(Text: "Later edit")));
        Assert.Equal("Read chapter", state.Commands[0].TaskSnapshot!.Text);
        Assert.Equal("Later edit", state.Commands[1].TaskSnapshot!.Text);
        Assert.True(WorkspaceJournalValidation.IsValid(state));
        Assert.False(WorkspaceJournalValidation.IsValid(state with { Commands = state.Commands.Select(entry => entry with { TaskSnapshot = entry.TaskSnapshot! with { Id = Guid.NewGuid() } }).ToArray() }));
        Assert.False(WorkspaceJournalValidation.IsValid(state with { Commands = state.Commands.Select(entry => entry with { TaskSnapshot = null }).ToArray() }));
        Assert.True(WorkspaceJournalValidation.IsValid(state with { Document = state.Document with { ActivityCoverage = null }, Commands = state.Commands.Select(entry => entry with { TaskSnapshot = null }).ToArray() }));
    }

    [Fact]
    public void DuplicateIdUsesOriginalReceiptAndDifferentPayloadCannotReuseId()
    {
        var state = Empty(); var command = Create(state); var accepted = Accept(state, command);
        var duplicate = WorkspaceCommandEngine.Accept(accepted, command, Now.AddMinutes(1));
        Assert.Equal(SubmissionDisposition.Duplicate, duplicate.Disposition); Assert.Equal(accepted, duplicate.Snapshot);
        Assert.Equal(SubmissionDisposition.IdempotencyConflict, WorkspaceCommandEngine.Accept(accepted, command with { Patch = new(Text: "Different") }, Now).Disposition);
        Assert.Single(duplicate.Snapshot!.Document.Tasks); Assert.Single(duplicate.Snapshot.Commands);
        Assert.Equal(ProjectionCommandState.NotRequested, duplicate.Entry!.ProjectionState);
    }

    [Fact]
    public void StaleAndCrossWorkspaceCommandsDoNotChangeState()
    {
        var state = Empty(); var command = Create(state);
        var stale = WorkspaceCommandEngine.Accept(state, command with { ExpectedRevision = 2 }, Now);
        Assert.Equal(SubmissionDisposition.RevisionConflict, stale.Disposition); Assert.Same(state, stale.Snapshot);
        Assert.Equal(SubmissionDisposition.Invalid, WorkspaceCommandEngine.Accept(state, command with { WorkspaceId = Guid.NewGuid() }, Now).Disposition);
        Assert.Equal(SubmissionDisposition.Invalid, WorkspaceCommandEngine.Accept(state, command with { Patch = new(Text: " ") }, Now).Disposition);
        Assert.Equal(SubmissionDisposition.Invalid, WorkspaceCommandEngine.Accept(state, command with { Direction = HabitScoreDirection.Down }, Now).Disposition);
    }

    [Fact]
    public void PartialUpdateAndTodoCompletionKeepSavedStateSeparateFromGameEffects()
    {
        var state = Empty(); var create = Create(state); state = Accept(state, create);
        var edit = new WorkspaceCommand(Guid.NewGuid(), state.Document.Id, state.Document.Revision, WorkspaceOperation.UpdateTask, create.TaskId, new(Notes: "Private", Priority: CompanionPriority.High));
        state = Accept(state, edit);
        var task = Assert.Single(state.Document.Tasks); Assert.Equal("Read chapter", task.Text); Assert.Equal("Private", task.Notes);
        var clear = edit with { Id = Guid.NewGuid(), ExpectedRevision = state.Document.Revision, Patch = new(ClearNotes: true) };
        state = Accept(state, clear); Assert.Null(Assert.Single(state.Document.Tasks).Notes);
        var complete = new WorkspaceCommand(Guid.NewGuid(), state.Document.Id, state.Document.Revision, WorkspaceOperation.CompleteTask, create.TaskId, ProjectToHabitica: true);
        var result = WorkspaceCommandEngine.Accept(state, complete, Now);
        Assert.Equal(CompanionTaskState.Completed, Assert.Single(result.Snapshot!.Document.Tasks).State);
        Assert.Single(result.Snapshot.Document.Completions); Assert.Equal(new[] { ProjectionEffectKind.Create, ProjectionEffectKind.ScoreUp }, result.Entry!.Effects);
        Assert.Equal(0, result.Entry.ConfirmedEffects);
        Assert.Equal(SubmissionDisposition.Invalid, WorkspaceCommandEngine.Accept(result.Snapshot, complete with { Id = Guid.NewGuid(), ExpectedRevision = result.Snapshot.Document.Revision }, Now).Disposition);
    }

    private static WorkspaceJournalSnapshot WithRecurring(CompanionTaskKind kind)
    {
        var state = Empty(); var task = new CompanionTask(Guid.NewGuid(), 1, "Recurring", kind, CompanionTaskState.Active, CompanionPriority.Normal, Now, Now);
        return state with { Document = state.Document with { Tasks = new[] { task } } };
    }

    [Fact]
    public void DailyCompletionUsesOccurrencesAndHabitScoresAreBoundedOrderedEvents()
    {
        var daily = WithRecurring(CompanionTaskKind.Daily);
        var command = new WorkspaceCommand(Guid.NewGuid(), daily.Document.Id, 1, WorkspaceOperation.CompleteTask, daily.Document.Tasks[0].Id, OccurrenceDate: new DateOnly(2026, 10, 15));
        var result = WorkspaceCommandEngine.Accept(daily, command, Now);
        Assert.Equal(SubmissionDisposition.Accepted, result.Disposition); Assert.Equal(CompanionTaskState.Active, Assert.Single(result.Snapshot!.Document.Tasks).State);
        Assert.Single(result.Snapshot.Document.Occurrences); Assert.Single(result.Snapshot.Document.Completions);
        Assert.Equal(SubmissionDisposition.Invalid, WorkspaceCommandEngine.Accept(result.Snapshot, command with { Id = Guid.NewGuid(), ExpectedRevision = result.Snapshot.Document.Revision }, Now).Disposition);
        var habit = WithRecurring(CompanionTaskKind.Habit);
        Assert.Equal(SubmissionDisposition.Invalid, WorkspaceCommandEngine.Accept(habit, command with { WorkspaceId = habit.Document.Id, TaskId = habit.Document.Tasks[0].Id }, Now).Disposition);
        var score = new WorkspaceCommand(Guid.NewGuid(), habit.Document.Id, 1, WorkspaceOperation.ScoreHabit, habit.Document.Tasks[0].Id, Direction: HabitScoreDirection.Down, Count: 10, ProjectToHabitica: true);
        var scored = WorkspaceCommandEngine.Accept(habit, score, Now);
        Assert.Equal(10, scored.Snapshot!.Document.HabitScores.Count); Assert.Equal(11, scored.Entry!.Effects.Count);
        Assert.All(scored.Snapshot.Document.HabitScores, value => Assert.Equal(HabitScoreDirection.Down, value.Direction));
        Assert.Equal(SubmissionDisposition.Invalid, WorkspaceCommandEngine.Accept(habit, score with { Count = 11 }, Now).Disposition);
        Assert.Equal(SubmissionDisposition.Invalid, WorkspaceCommandEngine.Accept(habit, score with { Count = 0 }, Now).Disposition);
    }

    [Fact]
    public void LeaseExpiryDistinguishesUndispatchedAndUnknownAttempts()
    {
        var state = Empty(); var command = Create(state, true); state = Accept(state, command);
        var lease = ProjectionJournal.Acquire(state, command.Id, "fixture-owner", Now, TimeSpan.FromSeconds(30)); Assert.True(lease.Acquired);
        var recovered = ProjectionJournal.RecoverExpired(lease.Snapshot, Now.AddSeconds(31));
        Assert.Equal(ProjectionCommandState.Pending, recovered.Commands[0].ProjectionState);
        var nextLease = ProjectionJournal.Acquire(recovered, command.Id, "next-owner", Now.AddSeconds(32), TimeSpan.FromSeconds(30)); Assert.True(nextLease.Acquired);
        Assert.Throws<InvalidOperationException>(() => ProjectionJournal.Dispatch(nextLease.Snapshot, command.Id, lease.Entry!.Lease!.Token, Now.AddSeconds(33)));
        var dispatched = ProjectionJournal.Dispatch(nextLease.Snapshot, command.Id, nextLease.Entry!.Lease!.Token, Now.AddSeconds(33));
        var unknown = ProjectionJournal.RecoverExpired(dispatched, Now.AddSeconds(63));
        Assert.Equal(ProjectionCommandState.UnknownOutcome, unknown.Commands[0].ProjectionState);
        Assert.False(ProjectionJournal.Acquire(unknown, command.Id, "third-owner", Now.AddMinutes(10), TimeSpan.FromSeconds(30)).Acquired);
        Assert.Throws<InvalidOperationException>(() => ProjectionJournal.CancelPending(unknown, command.Id, Now.AddMinutes(10)));
        var known = ProjectionJournal.ReconcileVerified(unknown, command.Id, nextLease.Entry.Lease.Token, true, Now.AddMinutes(11), "fixture:verified-projection");
        Assert.Equal(ProjectionCommandState.Confirmed, known.Commands[0].ProjectionState); Assert.Equal(1, known.Commands[0].ConfirmedEffects);
        Assert.Contains(known.Events!, value => value.Kind == JournalEventKind.Reconciled && value.EvidenceReference == "fixture:verified-projection");
    }

    [Fact]
    public void UnknownOutcomePausesOtherGameEffectsButCanonicalSavesContinue()
    {
        var state = Empty(); var first = Create(state, true); state = Accept(state, first);
        var lease = ProjectionJournal.Acquire(state, first.Id, "owner", Now, TimeSpan.FromSeconds(30));
        state = ProjectionJournal.Dispatch(lease.Snapshot, first.Id, lease.Entry!.Lease!.Token, Now);
        state = ProjectionJournal.RecordKnownResult(state, first.Id, lease.Entry.Lease.Token, false, ProjectionFailure.Ambiguous, Now);
        var second = Create(state, true); var saved = WorkspaceCommandEngine.Accept(state, second, Now); Assert.True(saved.SavedInCompanion);
        Assert.False(ProjectionJournal.Acquire(saved.Snapshot!, second.Id, "owner", Now, TimeSpan.FromSeconds(30)).Acquired);
        Assert.Equal(2, saved.Snapshot!.Document.Tasks.Count); Assert.Equal(0, saved.Snapshot.Commands[0].ConfirmedEffects);
    }

    [Fact]
    public void KnownRefusalRespectsRetryDeadlineAndConfirmedEffectsCannotRepeat()
    {
        var state = Empty(); var command = Create(state, true); state = Accept(state, command);
        var lease = ProjectionJournal.Acquire(state, command.Id, "owner", Now, TimeSpan.FromSeconds(30));
        state = ProjectionJournal.Dispatch(lease.Snapshot, command.Id, lease.Entry!.Lease!.Token, Now);
        state = ProjectionJournal.RecordKnownResult(state, command.Id, lease.Entry.Lease.Token, false, ProjectionFailure.RateLimited, Now, Now.AddMinutes(2));
        Assert.False(ProjectionJournal.Acquire(state, command.Id, "owner", Now.AddMinutes(1), TimeSpan.FromSeconds(30)).Acquired);
        var other = Create(state, true); var withOther = Accept(state, other);
        Assert.False(ProjectionJournal.Acquire(withOther, other.Id, "other-owner", Now.AddMinutes(1), TimeSpan.FromSeconds(30)).Acquired);
        var retry = ProjectionJournal.Acquire(state, command.Id, "owner", Now.AddMinutes(2), TimeSpan.FromSeconds(30));
        state = ProjectionJournal.Dispatch(retry.Snapshot, command.Id, retry.Entry!.Lease!.Token, Now.AddMinutes(2));
        state = ProjectionJournal.RecordKnownResult(state, command.Id, retry.Entry.Lease.Token, true, ProjectionFailure.None, Now.AddMinutes(2));
        Assert.Equal(ProjectionCommandState.Confirmed, state.Commands[0].ProjectionState); Assert.Equal(2, state.Commands[0].Attempts);
        Assert.False(ProjectionJournal.Acquire(state, command.Id, "owner", Now.AddHours(1), TimeSpan.FromSeconds(30)).Acquired);
        Assert.Throws<InvalidOperationException>(() => ProjectionJournal.RecordKnownResult(state, command.Id, retry.Entry.Lease.Token, true, ProjectionFailure.None, Now));
    }

    [Fact]
    public async Task AtomicCompareExchangeReturnsConflictAndRecoversLostAcceptanceAcknowledgement()
    {
        var initial = Empty(); var command = Create(initial, true);
        var accepted = Accept(initial, command);
        var store = new FakeStore(initial) { Competing = accepted };
        var service = new WorkspaceCommandService(store);
        var duplicate = await service.SubmitAsync(command, default);
        Assert.Equal(SubmissionDisposition.Duplicate, duplicate!.Disposition); Assert.Single(store.State!.Document.Tasks);
        store = new FakeStore(initial) { Competing = Accept(initial, Create(initial)) };
        Assert.Equal(SubmissionDisposition.RevisionConflict, (await new WorkspaceCommandService(store).SubmitAsync(command, default))!.Disposition);
        store = new FakeStore(initial) { FailAfterCommit = true };
        var uncertain = await new WorkspaceCommandService(store).SubmitAsync(command, default);
        Assert.Null(uncertain!.SavedInCompanion); Assert.Equal("acceptance_unknown", uncertain.ErrorCode);
        store.FailAfterCommit = false;
        var recovered = await new WorkspaceCommandService(store).SubmitAsync(command, default);
        Assert.Equal(SubmissionDisposition.Duplicate, recovered!.Disposition); Assert.Single(store.State!.Commands);
    }

    [Theory]
    [InlineData("command-v1.json")]
    [InlineData("projection-command-v1.json")]
    [InlineData("command-v2.json")]
    [InlineData("command-v3.json")]
    [InlineData("command-v3-clear.json")]
    [InlineData("command-v3-details.json")]
    public void NeutralCommandFixtureHasPortableUtf8IdentityHash(string filename)
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", filename)));
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false) } };
        var command = fixture.RootElement.GetProperty("command").Deserialize<WorkspaceCommand>(options)!;
        Assert.Equal(fixture.RootElement.GetProperty("payloadHash").GetString(), WorkspaceCommandEngine.Hash(command));
        Assert.NotEqual(WorkspaceCommandEngine.Hash(command), WorkspaceCommandEngine.Hash(command.Patch is null ? command with { ProjectToHabitica = false } : command with { Patch = command.Patch with { Notes = null } }));
    }

    [Fact]
    public void CorruptedJournalCannotSupplyFalseDuplicateAcceptance()
    {
        var state = Empty(); var command = Create(state, true); state = Accept(state, command);
        var accepted = state.Commands[0];
        foreach (var invalid in new[] { accepted with { PayloadHash = "invalid" }, accepted with { Effects = Array.Empty<ProjectionEffectKind>() }, accepted with { ConfirmedEffects = 2 }, accepted with { Command = command with { WorkspaceId = Guid.NewGuid() } }, accepted with { ProjectionState = ProjectionCommandState.Confirmed } })
        {
            var corrupted = state with { Commands = new[] { invalid } };
            Assert.False(WorkspaceJournalValidation.IsValid(corrupted));
            Assert.Equal(SubmissionDisposition.Invalid, WorkspaceCommandEngine.Accept(corrupted, command, Now).Disposition);
        }
    }

    [Fact]
    public void PerTaskOrderingAndCredentialRepairPreventUnsafeLeases()
    {
        var state = Empty(); var first = Create(state, true); state = Accept(state, first);
        var update = new WorkspaceCommand(Guid.NewGuid(), state.Document.Id, state.Document.Revision, WorkspaceOperation.UpdateTask, first.TaskId, new(Notes: "Updated"));
        state = Accept(state, update);
        Assert.False(ProjectionJournal.Acquire(state, update.Id, "owner", Now, TimeSpan.FromSeconds(30)).Acquired);
        var lease = ProjectionJournal.Acquire(state, first.Id, "owner", Now, TimeSpan.FromSeconds(30));
        state = ProjectionJournal.Dispatch(lease.Snapshot, first.Id, lease.Entry!.Lease!.Token, Now);
        state = ProjectionJournal.RecordKnownResult(state, first.Id, lease.Entry.Lease.Token, false, ProjectionFailure.CredentialRepair, Now);
        var other = Create(state, true); state = Accept(state, other);
        Assert.False(ProjectionJournal.Acquire(state, other.Id, "owner", Now, TimeSpan.FromSeconds(30)).Acquired);
        Assert.Equal(ProjectionCommandState.Blocked, state.Commands[0].ProjectionState);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NewTodoIntentRequiresProjectionWithoutRewritingLegacyReceipts(bool requested)
    {
        var state = Empty(); var input = Create(state, requested);
        var prepared = WorkspaceTodoProjectionPolicy.Prepare(state.Document, input);
        Assert.True(prepared.ProjectToHabitica);
        Assert.Equal("todo-required-v1", WorkspaceTodoProjectionPolicy.Version);
        var store = new FakeStore(state); var service = new WorkspaceCommandService(store);
        var refusal = await service.SubmitAsync(input with { ProjectToHabitica = false }, default);
        Assert.Equal("todo_projection_required", refusal!.ErrorCode); Assert.Same(state, store.State);
        var saved = await service.SubmitAsync(prepared, default);
        Assert.Equal(SubmissionDisposition.Accepted, saved!.Disposition);
        Assert.Equal(new[] { ProjectionEffectKind.Create }, saved.Entry!.Effects);
        Assert.Equal(0, saved.Entry.ConfirmedEffects);
        var legacyCommand = Create(state); var legacy = Accept(state, legacyCommand);
        var before = WorkspaceJournalJson.Serialize(legacy);
        var recovered = await new WorkspaceCommandService(new FakeStore(legacy)).SubmitAsync(legacyCommand, default);
        Assert.Equal(SubmissionDisposition.Duplicate, recovered!.Disposition);
        Assert.Equal(before, WorkspaceJournalJson.Serialize(recovered.Snapshot!));
        Assert.Empty(recovered.Entry!.Effects);
        Assert.Equal(SubmissionDisposition.IdempotencyConflict, (await new WorkspaceCommandService(new FakeStore(legacy))
            .SubmitAsync(legacyCommand with { ProjectToHabitica = true }, default))!.Disposition);
    }

    [Fact]
    public async Task UnlinkedActiveCompletionOrdersCreateBeforeScoreAndHistoricalCompletedEditRequiresReview()
    {
        var state = Empty(); var create = Create(state); state = Accept(state, create);
        var complete = WorkspaceTodoProjectionPolicy.Prepare(state.Document, new(Guid.NewGuid(), state.Document.Id,
            state.Document.Revision, WorkspaceOperation.CompleteTask, create.TaskId));
        var result = await new WorkspaceCommandService(new FakeStore(state)).SubmitAsync(complete, default);
        Assert.Equal(new[] { ProjectionEffectKind.Create, ProjectionEffectKind.ScoreUp }, result!.Entry!.Effects);
        Assert.Equal(0, result.Entry.ConfirmedEffects);
        var historical = Accept(state, complete with { ProjectToHabitica = false });
        var edit = WorkspaceTodoProjectionPolicy.Prepare(historical.Document, new(Guid.NewGuid(), historical.Document.Id,
            historical.Document.Revision, WorkspaceOperation.UpdateTask, create.TaskId, new(Notes: "Reviewed historical note")));
        var store = new FakeStore(historical);
        Assert.Equal("historical_projection_requires_review", (await new WorkspaceCommandService(store).SubmitAsync(edit, default))!.ErrorCode);
        Assert.Same(historical, store.State);
        var plan = new WorkspaceCommand(Guid.NewGuid(), state.Document.Id, state.Document.Revision,
            WorkspaceOperation.PlanTask, create.TaskId, new(Planning: new(PlannedDate: new(2026, 10, 21))));
        Assert.Equal(plan, WorkspaceTodoProjectionPolicy.Prepare(state.Document, plan));
        Assert.Null(WorkspaceTodoProjectionPolicy.Rejection(state.Document, plan));
    }

    [Fact]
    public async Task ProjectionOnlyRepairKeepsTaskContentAndRecoversOriginalCreateWithoutFakeEdits()
    {
        var state = Empty(); var create = Create(state); state = Accept(state, create);
        var task = state.Document.Tasks.Single();
        var command = new WorkspaceCommand(Guid.NewGuid(), state.Document.Id, state.Document.Revision,
            WorkspaceOperation.EnsureProjection, task.Id, ProjectToHabitica: true);
        var store = new FakeStore(state) { FailAfterCommit = true }; var service = new WorkspaceCommandService(store);
        Assert.Equal("acceptance_unknown", (await service.SubmitAsync(command, default))!.ErrorCode);
        store.FailAfterCommit = false;
        var recovered = await service.SubmitAsync(command, default);
        Assert.Equal(SubmissionDisposition.Duplicate, recovered!.Disposition);
        Assert.Equal(new[] { ProjectionEffectKind.Create }, recovered.Entry!.Effects);
        Assert.Equal(task with { Revision = state.Document.Revision + 1, UpdatedAtUtc = recovered.Entry.AcceptedAtUtc }, recovered.Snapshot!.Document.Tasks.Single());
        Assert.Empty(recovered.Snapshot.Document.Completions); Assert.Empty(recovered.Snapshot.Document.HabitScores);
        Assert.Equal("projection_already_pending", (await service.SubmitAsync(command with { Id = Guid.NewGuid(), ExpectedRevision = recovered.Snapshot.Document.Revision }, default))!.ErrorCode);
        Assert.Equal(2, store.State!.Commands.Count);
        var accepted = recovered.Snapshot;
        var lease = ProjectionJournal.Acquire(accepted, command.Id, "fixture", recovered.Entry.AcceptedAtUtc, TimeSpan.FromSeconds(30));
        var dispatched = ProjectionJournal.Dispatch(lease.Snapshot, command.Id, lease.Entry!.Lease!.Token, recovered.Entry.AcceptedAtUtc);
        var unknown = ProjectionJournal.RecordKnownResult(dispatched, command.Id, lease.Entry.Lease.Token, false, ProjectionFailure.Ambiguous, recovered.Entry.AcceptedAtUtc);
        var unknownService = new WorkspaceCommandService(new FakeStore(unknown));
        Assert.Equal(ProjectionCommandState.UnknownOutcome, (await unknownService.SubmitAsync(command, default))!.Entry!.ProjectionState);
        Assert.Equal("projection_already_pending", (await unknownService.SubmitAsync(command with { Id = Guid.NewGuid(), ExpectedRevision = unknown.Document.Revision }, default))!.ErrorCode);

        Assert.Equal(SubmissionDisposition.IdempotencyConflict, (await service.SubmitAsync(command with { ProjectToHabitica = false }, default))!.Disposition);
    }

    [Fact]
    public void ProjectionOnlyRepairRequiresAnActiveTodoAndDoesNotReopenOrScoreHistory()
    {
        var state = Empty(); var create = Create(state); state = Accept(state, create);
        WorkspaceCommand Repair(WorkspaceJournalSnapshot s) => new(Guid.NewGuid(), s.Document.Id, s.Document.Revision,
            WorkspaceOperation.EnsureProjection, create.TaskId, ProjectToHabitica: true);
        Assert.Equal("projection_required", WorkspaceCommandEngine.Accept(state, Repair(state) with { ProjectToHabitica = false }, Now).ErrorCode);
        Assert.Equal("unexpected_patch", WorkspaceCommandEngine.Accept(state, Repair(state) with { Patch = new(Text: "Fake") }, Now).ErrorCode);
        var completed = Accept(state, new(Guid.NewGuid(), state.Document.Id, state.Document.Revision, WorkspaceOperation.CompleteTask, create.TaskId));
        Assert.Equal("historical_projection_requires_review", WorkspaceCommandEngine.Accept(completed, Repair(completed), Now).ErrorCode);
        var archived = completed with { Document = completed.Document with { ActivityCoverage = null, Tasks = [completed.Document.Tasks.Single() with { IsArchived = true }] } };
        Assert.Equal("task_unavailable", WorkspaceCommandEngine.Accept(archived, Repair(archived), Now).ErrorCode);
        foreach (var kind in new[] { CompanionTaskKind.Daily })
        {
            var recurring = state with { Document = state.Document with { ActivityCoverage = null, Tasks = [state.Document.Tasks.Single() with { Kind = kind }] } };
            Assert.Equal("adoption_required", WorkspaceCommandEngine.Accept(recurring, Repair(recurring), Now).ErrorCode);
        }
        var linked = state with { Document = state.Document with { Projections = [new(Guid.NewGuid(), 1, create.TaskId, ProjectionState.Linked, Guid.NewGuid().ToString("D"), Fingerprint: new string('a', 64))] } };
        Assert.Equal(new[] { ProjectionEffectKind.Create }, WorkspaceCommandEngine.Accept(linked, Repair(linked), Now).Entry!.Effects);
        var uncertain = state with { Document = state.Document with { Projections = [new(Guid.NewGuid(), 1, create.TaskId, ProjectionState.UnknownOutcome)] } };
        Assert.Equal("projection_review_required", WorkspaceCommandEngine.Accept(uncertain, Repair(uncertain), Now).ErrorCode);
        Assert.Equal("stale_revision", WorkspaceCommandEngine.Accept(state, Repair(state) with { ExpectedRevision = 1 }, Now).ErrorCode);
    }

    [Fact]
    public void HabitDirectionsHavePortableV9IdentityCreateOnlyEffectsAndBoundedScoring()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "habit-commands-v9.json")));
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false) } };
        foreach (var row in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var command = row.GetProperty("command").Deserialize<WorkspaceCommand>(options)!;
            Assert.Equal(row.GetProperty("payloadHash").GetString(), WorkspaceCommandEngine.Hash(command));
            var state = new WorkspaceJournalSnapshot(new WorkspaceDocument { Id = command.WorkspaceId }, 1, [], []);
            var accepted = WorkspaceCommandEngine.Accept(state, command, Now);
            Assert.Equal(SubmissionDisposition.Accepted, accepted.Disposition);
            Assert.Equal(new[] { ProjectionEffectKind.Create }, accepted.Entry!.Effects);
            Assert.Empty(accepted.Snapshot!.Document.HabitScores);
            var task = accepted.Snapshot.Document.Tasks.Single(); Assert.Equal(CompanionTaskKind.Habit, task.Kind);
            Assert.Equal(CompanionPriority.Normal, task.Priority); Assert.Equal(1.5, task.HabiticaDetails!.Difficulty);
            foreach (var direction in new[] { HabitScoreDirection.Up, HabitScoreDirection.Down })
            {
                var score = new WorkspaceCommand(Guid.NewGuid(), command.WorkspaceId, 2, WorkspaceOperation.ScoreHabit, task.Id, Direction: direction, Count: 2, ProjectToHabitica: true);
                var result = WorkspaceCommandEngine.Accept(accepted.Snapshot, score, Now);
                if (direction == HabitScoreDirection.Up ? task.HabiticaDetails.Up == true : task.HabiticaDetails.Down == true) Assert.Equal(2, result.Snapshot!.Document.HabitScores.Count);
                else Assert.Equal("habit_direction_disabled", result.ErrorCode);
            }
        }
    }

    [Fact]
    public void HabitCreationDefaultsAreExplicitAndUnsupportedFieldsNeverBecomeTodoPayloads()
    {
        var state = Empty(); var raw = new WorkspaceCommand(Guid.NewGuid(), state.Document.Id, 1, WorkspaceOperation.CreateHabit, Guid.NewGuid(), new(Text: "Habit"));
        var prepared = WorkspaceTodoProjectionPolicy.Prepare(state.Document, raw);
        Assert.True(prepared.ProjectToHabitica); Assert.True(prepared.Patch!.HabiticaDetails!.Up); Assert.True(prepared.Patch.HabiticaDetails.Down);
        Assert.Equal("habit_projection_required", WorkspaceTodoProjectionPolicy.Rejection(state.Document, raw));
        foreach (var patch in new[] { prepared.Patch with { Deadline = new(2026, 10, 21) }, prepared.Patch with { HabiticaDetails = prepared.Patch.HabiticaDetails with { Checklist = [] } } })
            Assert.Equal("unsupported_habit_fields", WorkspaceCommandEngine.Accept(state, prepared with { Patch = patch }, Now).ErrorCode);
        Assert.Equal("habit_directions_required_kind", WorkspaceCommandEngine.Accept(state, prepared with { Operation = WorkspaceOperation.CreateTodo }, Now).ErrorCode);
        var legacy = WorkspaceCommandEngine.Accept(state, raw, Now).Snapshot!;
        var repaired = WorkspaceCommandEngine.Accept(legacy, new(Guid.NewGuid(), state.Document.Id, 2, WorkspaceOperation.EnsureProjection, raw.TaskId, ProjectToHabitica: true), Now);
        Assert.Equal(new[] { ProjectionEffectKind.Create }, repaired.Entry!.Effects);
        Assert.Equal(CompanionTaskKind.Habit, repaired.Snapshot!.Document.Tasks.Single().Kind);
        Assert.Empty(repaired.Snapshot.Document.HabitScores);

    }

    private sealed class FakeStore(WorkspaceJournalSnapshot initial) : ICompanionWorkspaceStore
    {
        public WorkspaceJournalSnapshot? State { get; private set; } = initial;
        public WorkspaceJournalSnapshot? Competing { get; init; }
        public bool FailAfterCommit { get; set; }
        public Task<WorkspaceJournalSnapshot?> LoadAsync(Guid id, CancellationToken token) => Task.FromResult(State?.Document.Id == id ? State : null);
        public Task<bool> TryCommitAsync(long expected, WorkspaceJournalSnapshot next, CancellationToken token)
        {
            if (Competing is not null) { State = Competing; return Task.FromResult(false); }
            if (State?.StoreVersion != expected) return Task.FromResult(false);
            State = next;
            if (FailAfterCommit) throw new IOException("Synthetic lost acknowledgement.");
            return Task.FromResult(true);
        }
    }
}
