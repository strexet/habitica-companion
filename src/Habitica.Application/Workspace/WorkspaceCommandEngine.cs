using Habitica.Domain.Workspace;

namespace Habitica.Application.Workspace;

public static class WorkspaceCommandEngine
{
    public static string Hash(WorkspaceCommand command) => WorkspaceCommandIdentity.Hash(command);

    public static CommandSubmission Accept(WorkspaceJournalSnapshot snapshot, WorkspaceCommand command, DateTimeOffset now)
    {
        CommandSubmission Reject(SubmissionDisposition disposition, string reason) => new(disposition, snapshot, null, reason);
        if (!WorkspaceJournalValidation.IsValid(snapshot))
            return Reject(SubmissionDisposition.Invalid, "invalid_workspace");
        if (command.Id == Guid.Empty || command.ExpectedRevision < 1 || command.ExpectedRevision > WorkspaceDocument.MaxRevision || command.WorkspaceId != snapshot.Document.Id || command.TaskId == Guid.Empty
            || !Enum.IsDefined(command.Operation) || now == default || now.Offset != TimeSpan.Zero)
            return Reject(SubmissionDisposition.Invalid, "invalid_command");
        if (command.Count is < 1 or > 10 || command.Operation != WorkspaceOperation.ScoreHabit && command.Count != 1)
            return Reject(SubmissionDisposition.Invalid, "invalid_count");
        if (command.Operation == WorkspaceOperation.ScoreHabit ? command.Direction is null || !Enum.IsDefined(command.Direction.Value) : command.Direction is not null)
            return Reject(SubmissionDisposition.Invalid, "invalid_direction");
        if (command.Operation != WorkspaceOperation.CompleteTask && command.OccurrenceDate is not null)
            return Reject(SubmissionDisposition.Invalid, "unexpected_occurrence");
        var patch = command.Patch;
        if (command.Operation is WorkspaceOperation.CreateTodo or WorkspaceOperation.UpdateTask)
        {
            if (patch is null || patch == new WorkspaceTaskPatch()
                || patch.Text is not null && (string.IsNullOrWhiteSpace(patch.Text) || patch.Text.Length > 2048)
                || patch.Notes?.Length > 10000 || patch.Priority is { } priority && !Enum.IsDefined(priority)
                || !HabiticaTaskDetails.IsValid(patch.HabiticaDetails) || patch.HabiticaDetails == new HabiticaTaskDetails()
                || patch.ClearNotes && patch.Notes is not null || patch.ClearDeadline && patch.Deadline is not null
                || command.Operation == WorkspaceOperation.CreateTodo && string.IsNullOrWhiteSpace(patch.Text))
                return Reject(SubmissionDisposition.Invalid, "invalid_patch");
        }
        else if (patch is not null) return Reject(SubmissionDisposition.Invalid, "unexpected_patch");
        var hash = Hash(command);
        var existing = snapshot.Commands.FirstOrDefault(entry => entry.Command.Id == command.Id);
        if (existing is not null)
            return existing.PayloadHash == hash ? new(SubmissionDisposition.Duplicate, snapshot, existing) : Reject(SubmissionDisposition.IdempotencyConflict, "command_id_reused");
        if (snapshot.Authority?.Mode is WorkspaceAuthorityMode.Frozen or WorkspaceAuthorityMode.Cloud)
            return Reject(SubmissionDisposition.Invalid, "workspace_owned_elsewhere");
        if (command.ExpectedRevision != snapshot.Document.Revision) return Reject(SubmissionDisposition.RevisionConflict, "stale_revision");
        var document = snapshot.Document;
        var current = document.Tasks.FirstOrDefault(task => task.Id == command.TaskId);
        if (command.Operation == WorkspaceOperation.CreateTodo ? current is not null : current is null || current.IsArchived)
            return Reject(SubmissionDisposition.Invalid, "task_unavailable");
        if (command.Operation == WorkspaceOperation.CompleteTask && (current!.Kind == CompanionTaskKind.Habit || current.State is CompanionTaskState.Completed or CompanionTaskState.Cancelled or CompanionTaskState.Paused))
            return Reject(SubmissionDisposition.Invalid, "task_not_completable");
        if (command.Operation == WorkspaceOperation.ScoreHabit && (current!.Kind != CompanionTaskKind.Habit || current.State != CompanionTaskState.Active))
            return Reject(SubmissionDisposition.Invalid, "habit_not_active");
        if (command.Operation == WorkspaceOperation.CompleteTask && current!.Kind == CompanionTaskKind.Daily && command.OccurrenceDate is null)
            return Reject(SubmissionDisposition.Invalid, "occurrence_required");
        if (command.Operation == WorkspaceOperation.CompleteTask && current!.Kind == CompanionTaskKind.Todo && command.OccurrenceDate is not null)
            return Reject(SubmissionDisposition.Invalid, "unexpected_occurrence");
        if (document.Revision >= WorkspaceDocument.MaxRevision || snapshot.StoreVersion >= WorkspaceDocument.MaxRevision) return Reject(SubmissionDisposition.Invalid, "revision_exhausted");
        var revision = document.Revision + 1;
        var tasks = document.Tasks.ToList(); var completions = document.Completions.ToList();
        var occurrences = document.Occurrences.ToList(); var scores = document.HabitScores.ToList();
        CompanionTask updated;
        if (command.Operation == WorkspaceOperation.CreateTodo)
            updated = new(command.TaskId, revision, patch!.Text!, CompanionTaskKind.Todo, CompanionTaskState.Active, patch.Priority ?? CompanionPriority.Normal, now, now, patch.Notes, Deadline: patch.Deadline, HabiticaDetails: patch.HabiticaDetails);
        else
        {
            updated = current! with { Revision = revision, UpdatedAtUtc = now };
            if (command.Operation == WorkspaceOperation.UpdateTask)
                updated = updated with { Text = patch!.Text ?? updated.Text, Notes = patch.ClearNotes ? null : patch.Notes ?? updated.Notes, Priority = patch.Priority ?? updated.Priority, Deadline = patch.ClearDeadline ? null : patch.Deadline ?? updated.Deadline, HabiticaDetails = HabiticaTaskDetails.Merge(updated.HabiticaDetails, patch.HabiticaDetails) };
            if (command.Operation == WorkspaceOperation.CompleteTask)
            {
                Guid? occurrenceId = null;
                if (updated.Kind == CompanionTaskKind.Daily)
                {
                    var occurrence = occurrences.FirstOrDefault(value => value.TaskId == updated.Id && value.Date == command.OccurrenceDate);
                    if (occurrence is not null && (occurrence.State != OccurrenceState.Pending || occurrence.IsArchived)) return Reject(SubmissionDisposition.Invalid, "occurrence_unavailable");
                    var completed = occurrence is null ? new TaskOccurrence(Guid.NewGuid(), revision, updated.Id, command.OccurrenceDate!.Value, OccurrenceState.Completed) : occurrence with { Revision = revision, State = OccurrenceState.Completed };
                    occurrences.RemoveAll(value => value.Id == completed.Id); occurrences.Add(completed); occurrenceId = completed.Id;
                }
                else updated = updated with { State = CompanionTaskState.Completed };
                completions.Add(new(Guid.NewGuid(), revision, updated.Id, command.Id, now, occurrenceId));
            }
            if (command.Operation == WorkspaceOperation.ScoreHabit)
                for (var index = 0; index < command.Count; index++) scores.Add(new(Guid.NewGuid(), revision, updated.Id, command.Id, now, command.Direction!.Value));
            tasks.RemoveAll(task => task.Id == updated.Id);
        }
        tasks.Add(updated);
        var mapping = document.Projections.FirstOrDefault(value => value.TaskId == updated.Id && !value.IsArchived);
        var requested = command.ProjectToHabitica || mapping is not null && mapping.State != ProjectionState.Unlinked;
        var effects = new List<ProjectionEffectKind>();
        if (requested)
        {
            if (mapping?.HabiticaTaskId is null) effects.Add(ProjectionEffectKind.Create);
            if (command.Operation == WorkspaceOperation.UpdateTask) effects.Add(ProjectionEffectKind.Update);
            if (command.Operation == WorkspaceOperation.CompleteTask) effects.Add(ProjectionEffectKind.ScoreUp);
            if (command.Operation == WorkspaceOperation.ScoreHabit)
                for (var index = 0; index < command.Count; index++) effects.Add(command.Direction == HabitScoreDirection.Up ? ProjectionEffectKind.ScoreUp : ProjectionEffectKind.ScoreDown);
        }
        var state = !requested ? ProjectionCommandState.NotRequested
            : mapping?.State is ProjectionState.ExternalConflict or ProjectionState.UnknownOutcome ? ProjectionCommandState.Blocked : ProjectionCommandState.Pending;
        var entry = new WorkspaceCommandEntry(command, hash, now, state, effects, Failure: state == ProjectionCommandState.Blocked ? ProjectionFailure.Precondition : ProjectionFailure.None, TaskSnapshot: updated);
        var projections = document.Projections.ToList();
        if (requested && mapping is null) projections.Add(new(Guid.NewGuid(), revision, updated.Id, ProjectionState.Pending));
        var result = document with { Revision = revision, Tasks = tasks, Completions = completions, Occurrences = occurrences, HabitScores = scores, Projections = projections };
        if (WorkspaceRules.Validate(result).Count != 0) return Reject(SubmissionDisposition.Invalid, "invalid_result");
        return new(SubmissionDisposition.Accepted, new(result, snapshot.StoreVersion + 1, snapshot.Commands.Append(entry).ToArray(), (snapshot.Events ?? Array.Empty<WorkspaceJournalEvent>()).Append(new(Guid.NewGuid(), command.Id, JournalEventKind.Accepted, now, 0, entry.Failure)).ToArray(), snapshot.Authority), entry);
    }
}
