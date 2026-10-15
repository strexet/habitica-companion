namespace Habitica.Domain.Workspace;

public enum WorkspaceOperation { CreateTodo, UpdateTask, CompleteTask, ScoreHabit }
public enum ProjectionCommandState { NotRequested, Pending, Leased, Executing, Confirmed, Blocked, UnknownOutcome, Cancelled }
public enum ProjectionFailure { None, RateLimited, CredentialRepair, Precondition, Permanent, Transient, Ambiguous }
public enum ProjectionEffectKind { Create, Update, ScoreUp, ScoreDown }
public enum JournalEventKind { Accepted, LeaseAcquired, LeaseExpired, Dispatched, ResultKnown, UnknownDetected, Reconciled, Cancelled }
public enum SubmissionDisposition { Accepted, Duplicate, RevisionConflict, IdempotencyConflict, Invalid, Unavailable }

public sealed record WorkspaceTaskPatch(
    string? Text = null,
    string? Notes = null,
    bool ClearNotes = false,
    CompanionPriority? Priority = null,
    DateOnly? Deadline = null,
    bool ClearDeadline = false);

public sealed record WorkspaceCommand(
    Guid Id,
    Guid WorkspaceId,
    long ExpectedRevision,
    WorkspaceOperation Operation,
    Guid TaskId,
    WorkspaceTaskPatch? Patch = null,
    HabitScoreDirection? Direction = null,
    int Count = 1,
    DateOnly? OccurrenceDate = null,
    bool ProjectToHabitica = false);

public sealed record ProjectionLease(string Owner, Guid Token, DateTimeOffset ExpiresAtUtc);
public sealed record WorkspaceCommandEntry(
    WorkspaceCommand Command,
    string PayloadHash,
    DateTimeOffset AcceptedAtUtc,
    ProjectionCommandState ProjectionState,
    IReadOnlyList<ProjectionEffectKind> Effects,
    int ConfirmedEffects = 0,
    ProjectionFailure Failure = ProjectionFailure.None,
    DateTimeOffset? RetryAtUtc = null,
    ProjectionLease? Lease = null,
    DateTimeOffset? DispatchedAtUtc = null,
    int Attempts = 0);

public sealed record WorkspaceJournalEvent(Guid Id, Guid CommandId, JournalEventKind Kind, DateTimeOffset AtUtc, int ConfirmedEffects, ProjectionFailure Failure, Guid? AttemptToken = null, string? EvidenceReference = null);
public sealed record WorkspaceJournalSnapshot(WorkspaceDocument Document, long StoreVersion, IReadOnlyList<WorkspaceCommandEntry> Commands, IReadOnlyList<WorkspaceJournalEvent>? Events = null);
public sealed record CommandSubmission(SubmissionDisposition Disposition, WorkspaceJournalSnapshot? Snapshot, WorkspaceCommandEntry? Entry, string? ErrorCode = null)
{
    public bool? SavedInCompanion => Disposition == SubmissionDisposition.Unavailable ? null : Disposition is SubmissionDisposition.Accepted or SubmissionDisposition.Duplicate;
}
