using Habitica.Domain.Workspace;

namespace Habitica.Domain.Workspace;

public static class WorkspaceJournalValidation
{
    public static bool IsValid(WorkspaceJournalSnapshot snapshot)
    {
        if (snapshot.Document is null || snapshot.StoreVersion < snapshot.Document.Revision || snapshot.StoreVersion > WorkspaceDocument.MaxRevision
            || snapshot.Commands is null || WorkspaceRules.Validate(snapshot.Document).Count != 0) return false;
        if (snapshot.Authority is { } authority && (!Enum.IsDefined(authority.Mode) || authority.TransferId == Guid.Empty || authority.CloudWorkspaceId == Guid.Empty
            || authority.Mode == WorkspaceAuthorityMode.Local && (authority.TransferId is not null || authority.CloudWorkspaceId is not null)
            || authority.Mode == WorkspaceAuthorityMode.Frozen && (authority.TransferId is null || authority.CloudWorkspaceId is not null)
            || authority.Mode == WorkspaceAuthorityMode.Cloud && authority.CloudWorkspaceId != snapshot.Document.Id)) return false;
        var ids = new HashSet<Guid>();
        foreach (var entry in snapshot.Commands)
        {
            if (entry is null || entry.Command is null || entry.Command.Id == Guid.Empty || !ids.Add(entry.Command.Id)
                || entry.Command.WorkspaceId != snapshot.Document.Id || entry.Command.ExpectedRevision < 1 || entry.Command.ExpectedRevision >= snapshot.Document.Revision
                || !HabiticaTaskDetails.IsValid(entry.Command.Patch?.HabiticaDetails)
                || entry.Command.Patch?.Recurrence is { } recurrence && !RecurrenceCalendar.IsStructurallyValid(recurrence)
                || entry.Command.Patch is { ClearRecurrence: true, Recurrence: not null }
                || entry.Command.Patch?.Planning is { } planning && (!planning.IsValid() || entry.Command.Operation != WorkspaceOperation.PlanTask)
                || entry.PayloadHash != WorkspaceCommandIdentity.Hash(entry.Command) || entry.AcceptedAtUtc == default || entry.AcceptedAtUtc.Offset != TimeSpan.Zero
                || !Enum.IsDefined(entry.ProjectionState) || !Enum.IsDefined(entry.Failure) || entry.Effects is null || entry.Effects.Count > 11
                || entry.Effects.Any(effect => !Enum.IsDefined(effect)) || entry.ConfirmedEffects < 0 || entry.ConfirmedEffects > entry.Effects.Count || entry.Attempts < 0) return false;
            if (entry.Command.Operation is WorkspaceOperation.SkipOccurrence or WorkspaceOperation.PauseTask or WorkspaceOperation.ResumeTask)
            {
                var task = entry.TaskSnapshot;
                if (entry.Command.ProjectToHabitica || entry.ProjectionState != ProjectionCommandState.NotRequested || entry.Effects.Count != 0
                    || entry.Command.Patch is not null || entry.Command.Direction is not null || entry.Command.Count != 1
                    || task is null || task.Kind is not (CompanionTaskKind.Daily or CompanionTaskKind.Habit)) return false;
                if (entry.Command.Operation == WorkspaceOperation.SkipOccurrence
                    ? task.Kind != CompanionTaskKind.Daily || task.State != CompanionTaskState.Active || task.Recurrence is null || entry.Command.OccurrenceDate is null || entry.Command.OccurrenceDate == default(DateOnly)
                    : entry.Command.OccurrenceDate is not null || task.State != (entry.Command.Operation == WorkspaceOperation.PauseTask ? CompanionTaskState.Paused : CompanionTaskState.Active)) return false;
            }
            if (entry.Command.Operation == WorkspaceOperation.PlanTask
                && (entry.Command.ProjectToHabitica || entry.ProjectionState != ProjectionCommandState.NotRequested || entry.Effects.Count != 0
                    || entry.Command.Direction is not null || entry.Command.Count != 1 || entry.Command.OccurrenceDate is not null
                    || entry.Command.Patch is not { Planning: { } plan } planningPatch || (planningPatch with { Planning = null }) != new WorkspaceTaskPatch()
                    || !plan.Matches(entry.TaskSnapshot))) return false;
            if ((entry.Command.Patch?.Recurrence is not null || entry.Command.Patch?.ClearRecurrence == true)
                && (entry.Command.ProjectToHabitica || entry.ProjectionState != ProjectionCommandState.NotRequested || entry.Effects.Count != 0
                    || entry.Command.Operation is not (WorkspaceOperation.CreateDaily or WorkspaceOperation.UpdateTask)
                    || entry.Command.Operation == WorkspaceOperation.CreateDaily && entry.Command.Patch.ClearRecurrence
                    || entry.TaskSnapshot?.Kind != CompanionTaskKind.Daily)) return false;
            if (entry.Command.Patch?.ClearRecurrence == true && entry.TaskSnapshot!.Recurrence is not null) return false;
            if (entry.Command.Patch?.Recurrence is { } intended && !SameRecurrence(intended, entry.TaskSnapshot!.Recurrence)) return false;
            if (entry.TaskSnapshot is { } accepted && (accepted.Id != entry.Command.TaskId || accepted.Revision != entry.Command.ExpectedRevision + 1
                || !Enum.IsDefined(accepted.Kind) || !Enum.IsDefined(accepted.State) || !Enum.IsDefined(accepted.Priority)
                || string.IsNullOrWhiteSpace(accepted.Text) || accepted.Text.Length > 2048 || accepted.Notes?.Length > 10000
                || !HabiticaTaskDetails.IsValid(accepted.HabiticaDetails)
                || accepted.Recurrence is { } schedule && (accepted.Kind != CompanionTaskKind.Daily || !RecurrenceCalendar.IsStructurallyValid(schedule))
                || accepted.CreatedAtUtc == default || accepted.CreatedAtUtc.Offset != TimeSpan.Zero || accepted.UpdatedAtUtc != entry.AcceptedAtUtc || accepted.CreatedAtUtc > accepted.UpdatedAtUtc)) return false;
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

    private static bool SameRecurrence(RecurrenceRule intent, RecurrenceRule? accepted) => RecurrenceCalendar.IsStructurallyValid(accepted)
        && intent.Frequency == accepted.Frequency && intent.Interval == accepted.Interval && intent.StartDate == accepted.StartDate
        && intent.TimeZoneId == accepted.TimeZoneId && intent.Weekdays.SequenceEqual(accepted.Weekdays) && intent.DaysOfMonth.SequenceEqual(accepted.DaysOfMonth);
}
