using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

public static class WorkspaceJournalValidation
{
    public static bool IsValid(WorkspaceJournalSnapshot snapshot)
    {
        if (snapshot.StoreVersion < snapshot.Document.Revision || snapshot.StoreVersion > WorkspaceDocument.MaxRevision
            || snapshot.Commands is null || WorkspaceRules.Validate(snapshot.Document).Count != 0) return false;
        var ids = new HashSet<Guid>();
        foreach (var entry in snapshot.Commands)
        {
            if (entry is null || entry.Command is null || entry.Command.Id == Guid.Empty || !ids.Add(entry.Command.Id)
                || entry.Command.WorkspaceId != snapshot.Document.Id || entry.Command.ExpectedRevision < 1 || entry.Command.ExpectedRevision >= snapshot.Document.Revision
                || entry.PayloadHash != WorkspaceCommandEngine.Hash(entry.Command) || entry.AcceptedAtUtc == default || entry.AcceptedAtUtc.Offset != TimeSpan.Zero
                || !Enum.IsDefined(entry.ProjectionState) || !Enum.IsDefined(entry.Failure) || entry.Effects is null || entry.Effects.Count > 11
                || entry.Effects.Any(effect => !Enum.IsDefined(effect)) || entry.ConfirmedEffects < 0 || entry.ConfirmedEffects > entry.Effects.Count || entry.Attempts < 0) return false;
            if (entry.ProjectionState == ProjectionCommandState.NotRequested ? entry.Effects.Count != 0 : entry.Effects.Count == 0) return false;
            if (entry.ProjectionState == ProjectionCommandState.Confirmed && entry.ConfirmedEffects != entry.Effects.Count) return false;
            if (entry.ProjectionState is ProjectionCommandState.Pending or ProjectionCommandState.Leased or ProjectionCommandState.Executing or ProjectionCommandState.UnknownOutcome
                && entry.ConfirmedEffects >= entry.Effects.Count) return false;
            var needsLease = entry.ProjectionState is ProjectionCommandState.Leased or ProjectionCommandState.Executing or ProjectionCommandState.UnknownOutcome;
            if (needsLease != (entry.Lease is not null)) return false;
            if (entry.Lease is { } lease && (string.IsNullOrWhiteSpace(lease.Owner) || lease.Owner.Length > 128 || lease.Token == Guid.Empty || lease.ExpiresAtUtc.Offset != TimeSpan.Zero)) return false;
            if (entry.ProjectionState is ProjectionCommandState.Executing or ProjectionCommandState.UnknownOutcome && (entry.DispatchedAtUtc is null || entry.Attempts < 1)) return false;
            if (entry.RetryAtUtc is { } retry && retry.Offset != TimeSpan.Zero || entry.DispatchedAtUtc is { } dispatched && dispatched.Offset != TimeSpan.Zero) return false;
        }
        if (snapshot.Commands.Count > 0 && snapshot.Events is null) return false;
        var events = new HashSet<Guid>();
        foreach (var value in snapshot.Events ?? Array.Empty<WorkspaceJournalEvent>())
            if (value is null || value.Id == Guid.Empty || !events.Add(value.Id) || !ids.Contains(value.CommandId) || !Enum.IsDefined(value.Kind) || !Enum.IsDefined(value.Failure)
                || value.AtUtc == default || value.AtUtc.Offset != TimeSpan.Zero || value.ConfirmedEffects is < 0 or > 11 || value.AttemptToken == Guid.Empty
                || value.Kind == JournalEventKind.Reconciled && string.IsNullOrWhiteSpace(value.EvidenceReference)) return false;
        return true;
    }
}
