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
        Assert.True(WorkspaceJournalValidation.IsValid(state with { Commands = state.Commands.Select(entry => entry with { TaskSnapshot = null }).ToArray() }));
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
        var initial = Empty(); var command = Create(initial);
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

    [Fact]
    public void NeutralCommandFixtureHasPortableUtf8IdentityHash()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "command-v1.json")));
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false) } };
        var command = fixture.RootElement.GetProperty("command").Deserialize<WorkspaceCommand>(options)!;
        Assert.Equal(fixture.RootElement.GetProperty("payloadHash").GetString(), WorkspaceCommandEngine.Hash(command));
        Assert.NotEqual(WorkspaceCommandEngine.Hash(command), WorkspaceCommandEngine.Hash(command with { Patch = command.Patch! with { Notes = null } }));
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
