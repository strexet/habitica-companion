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
        if (command.Operation is not (WorkspaceOperation.CompleteTask or WorkspaceOperation.SkipOccurrence) && command.OccurrenceDate is not null)
            return Reject(SubmissionDisposition.Invalid, "unexpected_occurrence");
        var creating = command.Operation is WorkspaceOperation.CreateTodo or WorkspaceOperation.CreateDaily or WorkspaceOperation.CreateHabit;
        var patch = command.Patch;
        if (creating || command.Operation is WorkspaceOperation.UpdateTask or WorkspaceOperation.PlanTask or WorkspaceOperation.RestoreTodo or WorkspaceOperation.RelateTodo or WorkspaceOperation.SetTodoState)
        {
            if (patch is null || patch == new WorkspaceTaskPatch()
                || patch.Text is not null && (string.IsNullOrWhiteSpace(patch.Text) || patch.Text.Length > 2048)
                || patch.Notes?.Length > 10000 || patch.Priority is { } priority && !Enum.IsDefined(priority)
                || !HabiticaTaskDetails.IsValid(patch.HabiticaDetails) || patch.HabiticaDetails == new HabiticaTaskDetails()
                || patch.ClearNotes && patch.Notes is not null || patch.ClearDeadline && patch.Deadline is not null
                || patch.ClearRecurrence && patch.Recurrence is not null
                || patch.Recurrence is { } rule && !RecurrenceCalendar.Expand(rule, rule.StartDate, rule.StartDate).Succeeded
                || patch.Planning is not null && (!patch.Planning.IsValid() || command.Operation != WorkspaceOperation.PlanTask)
                || command.Operation == WorkspaceOperation.PlanTask && (patch.Planning is null || (patch with { Planning = null }) != new WorkspaceTaskPatch())
                || patch.RestoreTodo is { } restore && (restore.CompletionId == Guid.Empty || command.Operation != WorkspaceOperation.RestoreTodo)
                || command.Operation == WorkspaceOperation.RestoreTodo && (patch.RestoreTodo is null || (patch with { RestoreTodo = null }) != new WorkspaceTaskPatch())
                || patch.Relationships is { } relationships && (!relationships.IsValid() || command.Operation != WorkspaceOperation.RelateTodo)
                || command.Operation == WorkspaceOperation.RelateTodo && (patch.Relationships is null || (patch with { Relationships = null }) != new WorkspaceTaskPatch())
                || patch.TodoState is { } todoState && (!todoState.IsValid() || command.Operation != WorkspaceOperation.SetTodoState)
                || command.Operation == WorkspaceOperation.SetTodoState && (patch.TodoState is null || (patch with { TodoState = null }) != new WorkspaceTaskPatch())
                || creating && string.IsNullOrWhiteSpace(patch.Text))
                return Reject(SubmissionDisposition.Invalid, "invalid_patch");
        }
        else if (patch is not null) return Reject(SubmissionDisposition.Invalid, "unexpected_patch");
        if (snapshot.Document.OrganizationHistory?.Any(entry => entry.Command.Id == command.Id) == true) return Reject(SubmissionDisposition.IdempotencyConflict,"command_id_reused");
        var hash = Hash(command);
        var existing = snapshot.Commands.FirstOrDefault(entry => entry.Command.Id == command.Id);
        if (existing is not null)
            return existing.PayloadHash == hash ? new(SubmissionDisposition.Duplicate, snapshot, existing) : Reject(SubmissionDisposition.IdempotencyConflict, "command_id_reused");
        if (snapshot.Authority?.Mode is WorkspaceAuthorityMode.Frozen or WorkspaceAuthorityMode.Cloud)
            return Reject(SubmissionDisposition.Invalid, "workspace_owned_elsewhere");
        if (command.ExpectedRevision != snapshot.Document.Revision) return Reject(SubmissionDisposition.RevisionConflict, "stale_revision");
        var document = snapshot.Document;
        var current = document.Tasks.FirstOrDefault(task => task.Id == command.TaskId);
        if (creating ? current is not null : current is null || current.IsArchived)
            return Reject(SubmissionDisposition.Invalid, "task_unavailable");
        var mapping = document.Projections.FirstOrDefault(value => value.TaskId == command.TaskId && !value.IsArchived);
        if (command.Operation == WorkspaceOperation.RestoreTodo)
        {
            var retained = document.Projections.FirstOrDefault(value => value.TaskId == command.TaskId);
            if (command.ProjectToHabitica || retained is not null && (retained.State != ProjectionState.Unlinked || retained.HabiticaTaskId is not null)) return Reject(SubmissionDisposition.Invalid,"unsupported_projection");
            if (current!.Kind != CompanionTaskKind.Todo || current.State != CompanionTaskState.Completed) return Reject(SubmissionDisposition.Invalid,"todo_not_completed");
            var completion = document.Completions.FirstOrDefault(value => value.Id == patch!.RestoreTodo!.CompletionId);
            if (completion is null || completion.TaskId != current.Id || completion.OccurrenceId is not null) return Reject(SubmissionDisposition.Invalid,"completion_unavailable");
            if (document.Restorations?.Any(value => value.CompletionId == completion.Id) == true) return Reject(SubmissionDisposition.Invalid,"completion_already_restored");
        }
        if (command.Operation == WorkspaceOperation.PlanTask)
        {
            if (command.ProjectToHabitica) return Reject(SubmissionDisposition.Invalid,"unsupported_projection");
            if (!patch!.Planning!.ReferencesAvailable(document,current!)) return Reject(SubmissionDisposition.Invalid,"invalid_planning_reference");
        }
        if (command.Operation == WorkspaceOperation.RelateTodo)
        {
            if (command.ProjectToHabitica) return Reject(SubmissionDisposition.Invalid,"unsupported_projection");
            if (!patch!.Relationships!.ReferencesAvailable(document,current!)) return Reject(SubmissionDisposition.Invalid,"invalid_relationship_reference");
        }
        if (command.Operation == WorkspaceOperation.SetTodoState)
        {
            if (command.ProjectToHabitica) return Reject(SubmissionDisposition.Invalid,"unsupported_projection");
            if (current!.Kind != CompanionTaskKind.Todo || current.State == CompanionTaskState.Completed) return Reject(SubmissionDisposition.Invalid,"todo_state_unavailable");
        }
        var lifecycle = command.Operation is WorkspaceOperation.SkipOccurrence or WorkspaceOperation.PauseTask or WorkspaceOperation.ResumeTask;
        if (lifecycle)
        {
            if (command.ProjectToHabitica || mapping is not null && (mapping.State != ProjectionState.Unlinked || mapping.HabiticaTaskId is not null))
                return Reject(SubmissionDisposition.Invalid, "unsupported_projection");
            if (current!.Kind is not (CompanionTaskKind.Daily or CompanionTaskKind.Habit)) return Reject(SubmissionDisposition.Invalid, "recurring_task_required");
            if (command.Operation == WorkspaceOperation.ResumeTask ? current.State != CompanionTaskState.Paused : current.State != CompanionTaskState.Active)
                return Reject(SubmissionDisposition.Invalid, command.Operation == WorkspaceOperation.ResumeTask ? "task_not_paused" : "task_not_active");
            if (command.Operation == WorkspaceOperation.SkipOccurrence)
            {
                if (current.Kind != CompanionTaskKind.Daily) return Reject(SubmissionDisposition.Invalid, "daily_required");
                if (command.OccurrenceDate is null) return Reject(SubmissionDisposition.Invalid, "occurrence_required");
                if (current.Recurrence is null) return Reject(SubmissionDisposition.Invalid, "recurrence_required");
            }
        }
        var editsRecurrence = patch?.Recurrence is not null || patch?.ClearRecurrence == true;
        if (editsRecurrence && (creating ? command.Operation != WorkspaceOperation.CreateDaily || patch!.ClearRecurrence : current!.Kind != CompanionTaskKind.Daily))
            return Reject(SubmissionDisposition.Invalid, "recurrence_requires_daily");
        if (editsRecurrence && (command.ProjectToHabitica || mapping is not null && (mapping.State != ProjectionState.Unlinked || mapping.HabiticaTaskId is not null)))
            return Reject(SubmissionDisposition.Invalid, "unsupported_projection");
        if ((command.Operation is WorkspaceOperation.CreateDaily or WorkspaceOperation.CreateHabit or WorkspaceOperation.DeleteTask)
            && (command.ProjectToHabitica || mapping is not null && (mapping.State != ProjectionState.Unlinked || mapping.HabiticaTaskId is not null)))
            return Reject(SubmissionDisposition.Invalid, "unsupported_projection");
        if (command.Operation == WorkspaceOperation.CompleteTask && (current!.Kind == CompanionTaskKind.Habit || current.State is CompanionTaskState.Completed or CompanionTaskState.Cancelled or CompanionTaskState.Paused))
            return Reject(SubmissionDisposition.Invalid, "task_not_completable");
        if (command.Operation == WorkspaceOperation.ScoreHabit && (current!.Kind != CompanionTaskKind.Habit || current.State != CompanionTaskState.Active))
            return Reject(SubmissionDisposition.Invalid, "habit_not_active");
        if (command.Operation == WorkspaceOperation.CompleteTask && current!.Kind == CompanionTaskKind.Daily && command.OccurrenceDate is null)
            return Reject(SubmissionDisposition.Invalid, "occurrence_required");
        if (command.Operation == WorkspaceOperation.CompleteTask && current!.Kind == CompanionTaskKind.Todo && command.OccurrenceDate is not null)
            return Reject(SubmissionDisposition.Invalid, "unexpected_occurrence");
        if (command.Operation is WorkspaceOperation.CompleteTask or WorkspaceOperation.SkipOccurrence && current!.Kind == CompanionTaskKind.Daily && current.Recurrence is { } schedule)
        {
            var dates = RecurrenceCalendar.Expand(schedule, command.OccurrenceDate!.Value, command.OccurrenceDate.Value);
            if (!dates.Succeeded) return Reject(SubmissionDisposition.Invalid, "recurrence_unavailable");
            if (dates.Dates.Count == 0) return Reject(SubmissionDisposition.Invalid, "occurrence_not_scheduled");
        }
        if (document.Revision >= WorkspaceDocument.MaxRevision || snapshot.StoreVersion >= WorkspaceDocument.MaxRevision) return Reject(SubmissionDisposition.Invalid, "revision_exhausted");
        var revision = document.Revision + 1;
        var tasks = document.Tasks.ToList(); var completions = document.Completions.ToList();
        var occurrences = document.Occurrences.ToList(); var scores = document.HabitScores.ToList();
        var restorations = document.Restorations?.ToList();
        CompanionTask updated;
        if (creating)
            updated = new(command.TaskId, revision, patch!.Text!, command.Operation == WorkspaceOperation.CreateDaily ? CompanionTaskKind.Daily : command.Operation == WorkspaceOperation.CreateHabit ? CompanionTaskKind.Habit : CompanionTaskKind.Todo, CompanionTaskState.Active, patch.Priority ?? CompanionPriority.Normal, now, now, patch.Notes, Deadline: patch.Deadline, Recurrence: patch.Recurrence, HabiticaDetails: patch.HabiticaDetails);
        else
        {
            updated = current! with { Revision = revision, UpdatedAtUtc = now };
            if (command.Operation == WorkspaceOperation.PlanTask) updated = patch!.Planning!.Apply(updated);
            if (command.Operation == WorkspaceOperation.SetTodoState) updated = updated with { State=patch!.TodoState!.State };
            if (command.Operation == WorkspaceOperation.RelateTodo) updated = patch!.Relationships!.Apply(updated);
            if (command.Operation == WorkspaceOperation.RestoreTodo)
            {
                updated = updated with { State = CompanionTaskState.Active };
                restorations ??= []; restorations.Add(new(Guid.NewGuid(),revision,updated.Id,patch!.RestoreTodo!.CompletionId,command.Id,now));
            }
            if (command.Operation == WorkspaceOperation.PauseTask) updated = updated with { State = CompanionTaskState.Paused };
            if (command.Operation == WorkspaceOperation.ResumeTask) updated = updated with { State = CompanionTaskState.Active };
            if (command.Operation == WorkspaceOperation.SkipOccurrence)
            {
                var occurrence = occurrences.FirstOrDefault(value => value.TaskId == updated.Id && value.Date == command.OccurrenceDate);
                if (occurrence is not null && (occurrence.State != OccurrenceState.Pending || occurrence.IsArchived)) return Reject(SubmissionDisposition.Invalid, "occurrence_unavailable");
                var skipped = occurrence is null ? new TaskOccurrence(Guid.NewGuid(), revision, updated.Id, command.OccurrenceDate!.Value, OccurrenceState.Skipped)
                    : occurrence with { Revision = revision, State = OccurrenceState.Skipped };
                occurrences.RemoveAll(value => value.Id == skipped.Id); occurrences.Add(skipped);
            }
            if (command.Operation == WorkspaceOperation.DeleteTask) updated = updated with { IsArchived = true };
            if (command.Operation == WorkspaceOperation.UpdateTask)
                updated = updated with { Text = patch!.Text ?? updated.Text, Notes = patch.ClearNotes ? null : patch.Notes ?? updated.Notes, Priority = patch.Priority ?? updated.Priority, Deadline = patch.ClearDeadline ? null : patch.Deadline ?? updated.Deadline, Recurrence = patch.ClearRecurrence ? null : patch.Recurrence ?? updated.Recurrence, HabiticaDetails = HabiticaTaskDetails.Merge(updated.HabiticaDetails, patch.HabiticaDetails) };
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
            if (command.Operation is WorkspaceOperation.PlanTask or WorkspaceOperation.RestoreTodo or WorkspaceOperation.RelateTodo or WorkspaceOperation.SetTodoState) tasks[tasks.FindIndex(task => task.Id == updated.Id)] = updated;
            else tasks.RemoveAll(task => task.Id == updated.Id);
        }
        if (command.Operation is not (WorkspaceOperation.PlanTask or WorkspaceOperation.RestoreTodo or WorkspaceOperation.RelateTodo or WorkspaceOperation.SetTodoState)) tasks.Add(updated);
        var requested = command.Operation is not (WorkspaceOperation.PlanTask or WorkspaceOperation.RestoreTodo or WorkspaceOperation.RelateTodo or WorkspaceOperation.SetTodoState) && (command.ProjectToHabitica || mapping is not null && mapping.State != ProjectionState.Unlinked);
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
        var result = document with { Revision = revision, Tasks = tasks, Completions = completions, Restorations = restorations, Occurrences = occurrences, HabitScores = scores, Projections = projections };
        if (WorkspaceRules.Validate(result).Count != 0) return Reject(SubmissionDisposition.Invalid, "invalid_result");
        return new(SubmissionDisposition.Accepted, new(result, snapshot.StoreVersion + 1, snapshot.Commands.Append(entry).ToArray(), (snapshot.Events ?? Array.Empty<WorkspaceJournalEvent>()).Append(new(Guid.NewGuid(), command.Id, JournalEventKind.Accepted, now, 0, entry.Failure)).ToArray(), snapshot.Authority), entry);
    }
}
