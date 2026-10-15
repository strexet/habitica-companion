using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

public sealed record LeaseTransition(WorkspaceJournalSnapshot Snapshot, WorkspaceCommandEntry? Entry, bool Acquired);

public static class ProjectionJournal
{
    public static WorkspaceJournalSnapshot RecoverExpired(WorkspaceJournalSnapshot snapshot, DateTimeOffset now)
    {
        var changed = false;
        var events = (snapshot.Events ?? Array.Empty<WorkspaceJournalEvent>()).ToList();
        var entries = snapshot.Commands.Select(entry =>
        {
            if (entry.Lease is null || entry.Lease.ExpiresAtUtc > now) return entry;
            if (entry.ProjectionState == ProjectionCommandState.Executing)
            {
                changed = true;
                events.Add(new(Guid.NewGuid(), entry.Command.Id, JournalEventKind.UnknownDetected, now, entry.ConfirmedEffects, ProjectionFailure.Ambiguous, entry.Lease.Token));
                return entry with { ProjectionState = ProjectionCommandState.UnknownOutcome, Failure = ProjectionFailure.Ambiguous };
            }
            if (entry.ProjectionState == ProjectionCommandState.Leased)
            {
                changed = true;
                events.Add(new(Guid.NewGuid(), entry.Command.Id, JournalEventKind.LeaseExpired, now, entry.ConfirmedEffects, entry.Failure, entry.Lease.Token));
                return entry with { ProjectionState = ProjectionCommandState.Pending, Lease = null };
            }
            return entry;
        }).ToArray();
        return changed ? snapshot with { Commands = entries, Events = events, StoreVersion = NextVersion(snapshot.StoreVersion) } : snapshot;
    }

    public static LeaseTransition Acquire(WorkspaceJournalSnapshot snapshot, Guid commandId, string owner, DateTimeOffset now, TimeSpan duration)
    {
        if (string.IsNullOrWhiteSpace(owner) || owner.Length > 128 || now.Offset != TimeSpan.Zero || duration < TimeSpan.FromSeconds(1) || duration > TimeSpan.FromMinutes(5))
            throw new ArgumentException("Invalid projection lease.");
        snapshot = RecoverExpired(snapshot, now);
        var entry = snapshot.Commands.FirstOrDefault(value => value.Command.Id == commandId);
        if (entry is null || entry.ProjectionState != ProjectionCommandState.Pending || entry.RetryAtUtc > now || now < entry.AcceptedAtUtc
            || snapshot.Commands.Any(value => value.ProjectionState is ProjectionCommandState.UnknownOutcome or ProjectionCommandState.Leased or ProjectionCommandState.Executing
                || value.Failure == ProjectionFailure.RateLimited && value.RetryAtUtc > now
                || value.Failure == ProjectionFailure.CredentialRepair && value.ProjectionState == ProjectionCommandState.Blocked))
            return new(snapshot, entry, false);
        foreach (var prior in snapshot.Commands.TakeWhile(value => value.Command.Id != commandId))
            if (prior.Command.TaskId == entry.Command.TaskId && prior.ProjectionState is not (ProjectionCommandState.NotRequested or ProjectionCommandState.Confirmed or ProjectionCommandState.Cancelled)) return new(snapshot, entry, false);
        var leased = entry with { ProjectionState = ProjectionCommandState.Leased, Lease = new(owner, Guid.NewGuid(), now + duration) };
        return new(Replace(snapshot, leased, JournalEventKind.LeaseAcquired, now), leased, true);
    }

    public static WorkspaceJournalSnapshot Dispatch(WorkspaceJournalSnapshot snapshot, Guid commandId, Guid leaseToken, DateTimeOffset now)
    {
        var entry = Leased(snapshot, commandId, leaseToken);
        if (entry.ProjectionState != ProjectionCommandState.Leased || entry.Lease!.ExpiresAtUtc <= now || now.Offset != TimeSpan.Zero || now < entry.AcceptedAtUtc) throw new InvalidOperationException("Projection lease is not dispatchable.");
        return Replace(snapshot, entry with { ProjectionState = ProjectionCommandState.Executing, DispatchedAtUtc = now, Attempts = checked(entry.Attempts + 1), Failure = ProjectionFailure.None, RetryAtUtc = null }, JournalEventKind.Dispatched, now);
    }

    public static WorkspaceJournalSnapshot RecordKnownResult(WorkspaceJournalSnapshot snapshot, Guid commandId, Guid leaseToken, bool succeeded, ProjectionFailure failure, DateTimeOffset now, DateTimeOffset? retryAtUtc = null)
    {
        var entry = Leased(snapshot, commandId, leaseToken);
        if (entry.ProjectionState != ProjectionCommandState.Executing) throw new InvalidOperationException("No matching in-flight effect exists.");
        if (now < entry.DispatchedAtUtc || now.Offset != TimeSpan.Zero || retryAtUtc?.Offset != null && retryAtUtc.Value.Offset != TimeSpan.Zero || !Enum.IsDefined(failure)
            || succeeded && failure != ProjectionFailure.None || !succeeded && failure == ProjectionFailure.None) throw new ArgumentException("Invalid projection result.");
        if (succeeded)
        {
            var count = checked(entry.ConfirmedEffects + 1);
            if (count > entry.Effects.Count) throw new InvalidOperationException("Effect count exceeds the accepted command.");
            return Replace(snapshot, entry with { ConfirmedEffects = count, ProjectionState = count == entry.Effects.Count ? ProjectionCommandState.Confirmed : ProjectionCommandState.Pending, Lease = null, DispatchedAtUtc = null, Failure = ProjectionFailure.None }, JournalEventKind.ResultKnown, now, attemptToken: leaseToken);
        }
        if (failure == ProjectionFailure.Ambiguous)
            return Replace(snapshot, entry with { ProjectionState = ProjectionCommandState.UnknownOutcome, Failure = failure }, JournalEventKind.UnknownDetected, now, attemptToken: leaseToken);
        // Only a known refusal before effect acceptance is eligible for deferred retry.
        var resumable = failure is ProjectionFailure.RateLimited or ProjectionFailure.Transient;
        if (resumable && (retryAtUtc is null || retryAtUtc <= now)) throw new ArgumentException("A safe deferred retry requires a future deadline.");
        return Replace(snapshot, entry with { ProjectionState = resumable ? ProjectionCommandState.Pending : ProjectionCommandState.Blocked, Failure = failure, RetryAtUtc = resumable ? retryAtUtc : null, Lease = null, DispatchedAtUtc = null }, JournalEventKind.ResultKnown, now, attemptToken: leaseToken);
    }

    public static WorkspaceJournalSnapshot ReconcileVerified(WorkspaceJournalSnapshot snapshot, Guid commandId, Guid attemptToken, bool effectApplied, DateTimeOffset now, string evidenceReference)
    {
        if (string.IsNullOrWhiteSpace(evidenceReference) || evidenceReference.Length > 256 || now.Offset != TimeSpan.Zero) throw new ArgumentException("Verified reconciliation evidence is required.");
        var entry = Leased(snapshot, commandId, attemptToken);
        if (entry.ProjectionState != ProjectionCommandState.UnknownOutcome) throw new InvalidOperationException("Only an unknown attempt can be reconciled.");
        var count = entry.ConfirmedEffects + (effectApplied ? 1 : 0);
        if (count > entry.Effects.Count) throw new InvalidOperationException("Effect count exceeds the accepted command.");
        return Replace(snapshot, entry with { ConfirmedEffects = count, ProjectionState = count == entry.Effects.Count ? ProjectionCommandState.Confirmed : ProjectionCommandState.Pending, Failure = ProjectionFailure.None, Lease = null, DispatchedAtUtc = null, RetryAtUtc = null }, JournalEventKind.Reconciled, now, evidenceReference, attemptToken);
    }

    public static WorkspaceJournalSnapshot CancelPending(WorkspaceJournalSnapshot snapshot, Guid commandId, DateTimeOffset now)
    {
        var entry = snapshot.Commands.Single(value => value.Command.Id == commandId);
        if (entry.ProjectionState != ProjectionCommandState.Pending) throw new InvalidOperationException("Only undispatched pending work can be cancelled.");
        return Replace(snapshot, entry with { ProjectionState = ProjectionCommandState.Cancelled, Lease = null, RetryAtUtc = null }, JournalEventKind.Cancelled, now);
    }

    private static long NextVersion(long current)
    {
        if (current >= WorkspaceDocument.MaxRevision) throw new InvalidOperationException("Workspace revision is exhausted.");
        return checked(current + 1);
    }

    private static WorkspaceCommandEntry Leased(WorkspaceJournalSnapshot snapshot, Guid commandId, Guid token)
    {
        var entry = snapshot.Commands.Single(value => value.Command.Id == commandId);
        if (token == Guid.Empty || entry.Lease?.Token != token) throw new InvalidOperationException("Projection attempt does not own the lease.");
        return entry;
    }

    private static WorkspaceJournalSnapshot Replace(WorkspaceJournalSnapshot snapshot, WorkspaceCommandEntry entry, JournalEventKind kind, DateTimeOffset now, string? evidenceReference = null, Guid? attemptToken = null) => snapshot with
    {
        StoreVersion = NextVersion(snapshot.StoreVersion),
        Commands = snapshot.Commands.Select(value => value.Command.Id == entry.Command.Id ? entry : value).ToArray(),
        Events = (snapshot.Events ?? Array.Empty<WorkspaceJournalEvent>()).Append(new(Guid.NewGuid(), entry.Command.Id, kind, now, entry.ConfirmedEffects, entry.Failure, attemptToken ?? entry.Lease?.Token, evidenceReference)).ToArray()
    };
}
