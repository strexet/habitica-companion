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
        var organizationHistory = snapshot.Document.OrganizationHistory ?? Array.Empty<WorkspaceOrganizationEntry>();
        if (organizationHistory.Any(entry => entry.PayloadHash != WorkspaceOrganizationCommands.Hash(entry.Command))) return false;
        var organizationRevisions = organizationHistory.Select(entry => entry.Snapshot.Revision).ToHashSet();
        var organizationIds = organizationHistory.Select(entry => entry.Command.Id).Concat((snapshot.Document.ChecklistConversions??[]).SelectMany(row=>row.Receipts.Select(receipt=>receipt.Command.Id))).Concat((snapshot.Document.SourceRetirements??[]).Select(row=>row.Id)).Concat((snapshot.Document.LinkedTaskRecoveries??[]).Select(row=>row.Id)).ToHashSet();
        var ids = new HashSet<Guid>();
        var unarchives = (snapshot.Document.Unarchives ?? []).ToDictionary(row => row.CommandId);
        var corrections = (snapshot.Document.OccurrenceCorrections ?? []).ToDictionary(row => row.CommandId);
        var restorations = (snapshot.Document.Restorations ?? Array.Empty<TaskRestoration>()).ToDictionary(value => value.CommandId);
        foreach (var entry in snapshot.Commands)
        {
            if (entry is null || entry.Command is null || entry.Command.Id == Guid.Empty || !ids.Add(entry.Command.Id)
                || organizationIds.Contains(entry.Command.Id) || organizationRevisions.Contains(entry.Command.ExpectedRevision + 1)
                || entry.Command.WorkspaceId != snapshot.Document.Id || entry.Command.ExpectedRevision < 1 || entry.Command.ExpectedRevision >= snapshot.Document.Revision
                || !HabiticaTaskDetails.IsValid(entry.Command.Patch?.HabiticaDetails)
                || entry.Command.Patch?.Recurrence is { } recurrence && !RecurrenceCalendar.IsStructurallyValid(recurrence)
                || entry.Command.Patch is { ClearRecurrence: true, Recurrence: not null }
                || entry.Command.Patch?.Planning is { } planning && (!planning.IsValid() || entry.Command.Operation != WorkspaceOperation.PlanTask)
                || entry.Command.Patch?.RestoreTodo is { } restore && (restore.CompletionId == Guid.Empty || entry.Command.Operation != WorkspaceOperation.RestoreTodo)
                || entry.Command.Patch?.Relationships is { } relationships && (!relationships.IsValid() || entry.Command.Operation != WorkspaceOperation.RelateTodo)
                || entry.Command.Patch?.TodoState is { } todoState && (!todoState.IsValid() || entry.Command.Operation != WorkspaceOperation.SetTodoState)
                || entry.Command.Patch?.Review is {} reviewPatch && (!reviewPatch.IsValid() || entry.Command.Operation != WorkspaceOperation.ReviewTask)
                || snapshot.Document.ReviewEvents?.Any(value => value.CommandId == entry.Command.Id) == true && entry.Command.Operation != WorkspaceOperation.ReviewTask
                || entry.Command.Patch?.TodoStage is {} todoStage && (!todoStage.IsValid() || entry.Command.Operation != WorkspaceOperation.SetTodoStage)
                || restorations.ContainsKey(entry.Command.Id) && entry.Command.Operation != WorkspaceOperation.RestoreTodo
                || unarchives.ContainsKey(entry.Command.Id) && entry.Command.Operation != WorkspaceOperation.UnarchiveTask
                || corrections.ContainsKey(entry.Command.Id) && entry.Command.Operation != WorkspaceOperation.CorrectOccurrence
                || entry.Command.Patch?.Unarchive is {} unarchivePatch && (!unarchivePatch.IsValid() || entry.Command.Operation != WorkspaceOperation.UnarchiveTask)
                || entry.Command.Patch?.CorrectOccurrence is {} correctionPatch && (!correctionPatch.IsValid() || entry.Command.Operation != WorkspaceOperation.CorrectOccurrence)
                || entry.PayloadHash != WorkspaceCommandIdentity.Hash(entry.Command) || entry.AcceptedAtUtc == default || entry.AcceptedAtUtc.Offset != TimeSpan.Zero
                || !Enum.IsDefined(entry.ProjectionState) || !Enum.IsDefined(entry.Failure) || entry.Effects is null || entry.Effects.Count > 11
                || entry.Effects.Any(effect => !Enum.IsDefined(effect)) || entry.ConfirmedEffects < 0 || entry.ConfirmedEffects > entry.Effects.Count || entry.Attempts < 0) return false;
            if (entry.Command.Operation == WorkspaceOperation.CreateHabit && entry.Command.ProjectToHabitica &&
                (entry.Command.Patch?.Deadline is not null || entry.Command.Patch?.HabiticaDetails?.Checklist is not null
                || entry.Command.Patch?.HabiticaDetails?.Up is null || entry.Command.Patch?.HabiticaDetails?.Down is null
                || entry.TaskSnapshot is not { Kind: CompanionTaskKind.Habit, State: CompanionTaskState.Active, IsArchived: false }
                || entry.TaskSnapshot.HabiticaDetails?.Up != entry.Command.Patch.HabiticaDetails.Up || entry.TaskSnapshot.HabiticaDetails?.Down != entry.Command.Patch.HabiticaDetails.Down
                || !entry.Effects.SequenceEqual(new[] { ProjectionEffectKind.Create }))) return false;
            if (entry.Command.Operation == WorkspaceOperation.EnsureProjection &&
                (!entry.Command.ProjectToHabitica || entry.Command.Patch is not null || entry.Command.Count != 1 || entry.Command.Direction is not null
                || entry.Command.OccurrenceDate is not null || entry.TaskSnapshot is not { Kind: CompanionTaskKind.Todo or CompanionTaskKind.Habit, State: CompanionTaskState.Active, IsArchived: false }
                || !entry.Effects.SequenceEqual(new[] { ProjectionEffectKind.Create }))) return false;
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
            if (entry.Command.Operation == WorkspaceOperation.ReviewTask)
            {
                var review = snapshot.Document.ReviewEvents?.SingleOrDefault(value => value.CommandId == entry.Command.Id);
                if (entry.Command.ProjectToHabitica || entry.ProjectionState != ProjectionCommandState.NotRequested || entry.Effects.Count != 0
                    || entry.Command.Direction is not null || entry.Command.Count != 1 || entry.Command.OccurrenceDate is not null
                    || entry.Command.Patch is not {Review:{} patch} reviewedPatch || (reviewedPatch with {Review=null}) != new WorkspaceTaskPatch()
                    || entry.TaskSnapshot is not {} task || task.IsArchived || review is null || review.TaskId != entry.Command.TaskId
                    || review.Revision != entry.Command.ExpectedRevision+1 || review.Action != patch.Action || review.RecordedAtUtc != entry.AcceptedAtUtc
                    || review.TimeZoneId != patch.TimeZoneId || task.ReviewAfterUtc != review.ReviewAfterUtc || task.StaleSinceUtc != review.StaleSinceUtc
                    || patch.Apply(task) != task) return false;
            }
            if (entry.Command.Operation == WorkspaceOperation.SetTodoStage
                && (entry.Command.ProjectToHabitica || entry.ProjectionState != ProjectionCommandState.NotRequested || entry.Effects.Count != 0
                    || entry.Command.Direction is not null || entry.Command.Count != 1 || entry.Command.OccurrenceDate is not null
                    || entry.Command.Patch is not {TodoStage:{} stage} stagePatch || (stagePatch with {TodoStage=null}) != new WorkspaceTaskPatch()
                    || !stage.Matches(entry.TaskSnapshot))) return false;
            if (entry.Command.Operation == WorkspaceOperation.SetTodoState
                && (entry.Command.ProjectToHabitica || entry.ProjectionState != ProjectionCommandState.NotRequested || entry.Effects.Count != 0
                    || entry.Command.Direction is not null || entry.Command.Count != 1 || entry.Command.OccurrenceDate is not null
                    || entry.Command.Patch is not { TodoState: { } desired } statePatch || (statePatch with { TodoState = null }) != new WorkspaceTaskPatch()
                    || !desired.Matches(entry.TaskSnapshot))) return false;
            if (entry.Command.Operation == WorkspaceOperation.RelateTodo
                && (entry.Command.ProjectToHabitica || entry.ProjectionState != ProjectionCommandState.NotRequested || entry.Effects.Count != 0
                    || entry.Command.Direction is not null || entry.Command.Count != 1 || entry.Command.OccurrenceDate is not null
                    || entry.Command.Patch is not { Relationships: { } relation } relationPatch || (relationPatch with { Relationships = null }) != new WorkspaceTaskPatch()
                    || !relation.Matches(entry.TaskSnapshot))) return false;
            if (entry.Command.Operation is WorkspaceOperation.UnarchiveTask or WorkspaceOperation.CorrectOccurrence)
            {
                if (entry.Command.ProjectToHabitica || entry.ProjectionState != ProjectionCommandState.NotRequested || entry.Effects.Count != 0
                    || entry.Command.Direction is not null || entry.Command.Count != 1 || entry.Command.OccurrenceDate is not null
                    || entry.TaskSnapshot is not { IsArchived: false } acceptedRecovery) return false;
                if (entry.Command.Operation == WorkspaceOperation.UnarchiveTask)
                {
                    if (entry.Command.Patch is not { Unarchive: {} recoveryTarget } recoveryPatch || (recoveryPatch with { Unarchive = null }) != new WorkspaceTaskPatch()
                        || !unarchives.TryGetValue(entry.Command.Id, out var recovery) || recovery.TaskId != entry.Command.TaskId
                        || recovery.Revision != entry.Command.ExpectedRevision + 1 || recovery.ArchivedRevision != recoveryTarget.ArchivedRevision || recovery.UnarchivedAtUtc != entry.AcceptedAtUtc
                        || recoveryTarget.Planning is {} recoveryPlan && !recoveryPlan.Matches(acceptedRecovery) || recoveryTarget.Relationships is {} links && !links.Matches(acceptedRecovery)) return false;
                    var archiveSource = snapshot.Commands.FirstOrDefault(row => row.Command.TaskId == entry.Command.TaskId && row.Command.ExpectedRevision + 1 == recoveryTarget.ArchivedRevision);
                    if (archiveSource is not null && (archiveSource.Command.Operation != WorkspaceOperation.DeleteTask || archiveSource.TaskSnapshot is not { IsArchived: true } beforeArchive
                        || System.Text.Json.JsonSerializer.Serialize(recoveryTarget.Apply(beforeArchive) with { Revision = acceptedRecovery.Revision, UpdatedAtUtc = acceptedRecovery.UpdatedAtUtc }) != System.Text.Json.JsonSerializer.Serialize(acceptedRecovery))) return false;
                }
                else
                {
                    if (entry.Command.Patch is not { CorrectOccurrence: {} recoveryTarget } recoveryPatch || (recoveryPatch with { CorrectOccurrence = null }) != new WorkspaceTaskPatch()
                        || acceptedRecovery.Kind != CompanionTaskKind.Daily || !corrections.TryGetValue(entry.Command.Id, out var recovery) || recovery.TaskId != entry.Command.TaskId
                        || recovery.Revision != entry.Command.ExpectedRevision + 1 || recovery.CorrectedAtUtc != entry.AcceptedAtUtc
                        || recovery.TargetCommandId != recoveryTarget.TargetCommandId || recovery.PreviousOccurrence.Id != recoveryTarget.OccurrenceId || recovery.PreviousOccurrence.Revision != recoveryTarget.OccurrenceRevision) return false;
                    var source = snapshot.Commands.FirstOrDefault(row => row.Command.Id == recoveryTarget.TargetCommandId);
                    if (source is null || source.Effects.Count != 0 || source.Command.TaskId != recovery.TaskId || source.Command.ExpectedRevision + 1 != recoveryTarget.OccurrenceRevision
                        || source.Command.OccurrenceDate != recovery.PreviousOccurrence.Date || source.Command.Operation != (recovery.PreviousOccurrence.State == OccurrenceState.Completed ? WorkspaceOperation.CompleteTask : WorkspaceOperation.SkipOccurrence)) return false;
                }
            }
            if (entry.Command.Operation == WorkspaceOperation.RestoreTodo
                && (entry.Command.ProjectToHabitica || entry.ProjectionState != ProjectionCommandState.NotRequested || entry.Effects.Count != 0
                    || entry.Command.Direction is not null || entry.Command.Count != 1 || entry.Command.OccurrenceDate is not null
                    || entry.Command.Patch is not { RestoreTodo: { } target } restorePatch || (restorePatch with { RestoreTodo = null }) != new WorkspaceTaskPatch()
                    || entry.TaskSnapshot is not { Kind: CompanionTaskKind.Todo or CompanionTaskKind.Habit, State: CompanionTaskState.Active, IsArchived: false }
                    || !restorations.TryGetValue(entry.Command.Id,out var restoration) || restoration.TaskId != entry.Command.TaskId
                    || restoration.CompletionId != target.CompletionId || restoration.Revision != entry.Command.ExpectedRevision+1 || restoration.RestoredAtUtc != entry.AcceptedAtUtc)) return false;
            if (entry.Command.Operation == WorkspaceOperation.CreateDaily && entry.Command.ProjectToHabitica
                && (DailyProjectionSchedule.RuleFailure(entry.Command.Patch?.Recurrence) is not null || entry.Command.Patch?.Deadline is not null
                    || entry.TaskSnapshot is not { Kind: CompanionTaskKind.Daily, State: CompanionTaskState.Active, IsArchived: false }
                    || entry.Effects.Count != 1 || entry.Effects[0] != ProjectionEffectKind.Create)) return false;
            if ((entry.Command.Patch?.Recurrence is not null || entry.Command.Patch?.ClearRecurrence == true)
                && ((entry.Command.ProjectToHabitica ? entry.Command.Operation != WorkspaceOperation.CreateDaily || DailyProjectionSchedule.RuleFailure(entry.Command.Patch.Recurrence) is not null || entry.Command.Patch.Deadline is not null || entry.Effects.Count != 1 || entry.Effects[0] != ProjectionEffectKind.Create : entry.ProjectionState != ProjectionCommandState.NotRequested || entry.Effects.Count != 0)
                    || entry.Command.Operation is not (WorkspaceOperation.CreateDaily or WorkspaceOperation.UpdateTask)
                    || entry.Command.Operation == WorkspaceOperation.CreateDaily && entry.Command.Patch.ClearRecurrence
                    || entry.TaskSnapshot?.Kind != CompanionTaskKind.Daily)) return false;
            if (entry.Command.Patch?.ClearRecurrence == true && entry.TaskSnapshot!.Recurrence is not null) return false;
            if (entry.Command.Patch?.Recurrence is { } intended && !SameRecurrence(intended, entry.TaskSnapshot!.Recurrence)) return false;
            if (entry.TaskSnapshot is { } accepted && (accepted.Id != entry.Command.TaskId || accepted.Revision != entry.Command.ExpectedRevision + 1
                || !Enum.IsDefined(accepted.Kind) || !Enum.IsDefined(accepted.State) || !Enum.IsDefined(accepted.Priority)
                || string.IsNullOrWhiteSpace(accepted.Text) || accepted.Text.Length > 2048 || accepted.Notes?.Length > 10000
                || accepted.ExecutionStage is {} executionStage && (accepted.Kind != CompanionTaskKind.Todo || !Enum.IsDefined(executionStage))
                || !HabiticaTaskDetails.IsValid(accepted.HabiticaDetails)
                || accepted.Kind != CompanionTaskKind.Habit && (accepted.HabiticaDetails?.Up is not null || accepted.HabiticaDetails?.Down is not null)
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
        return WorkspaceLinkedRecovery.ValidJournal(snapshot) && WorkspaceActivityCoverage.ValidJournal(snapshot);
    }

    private static bool SameRecurrence(RecurrenceRule intent, RecurrenceRule? accepted) => RecurrenceCalendar.IsStructurallyValid(accepted)
        && intent.Frequency == accepted.Frequency && intent.Interval == accepted.Interval && intent.StartDate == accepted.StartDate
        && intent.TimeZoneId == accepted.TimeZoneId && intent.Weekdays.SequenceEqual(accepted.Weekdays) && intent.DaysOfMonth.SequenceEqual(accepted.DaysOfMonth);
}
