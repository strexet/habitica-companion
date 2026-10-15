import { workspaceDocument, type WorkspaceDocument, type CompanionTask } from "../../shared/workspace-domain";
import { commandHash, type WorkspaceCommand } from "../../shared/workspace-commands";
import { WorkspaceError } from "../../shared/workspace-http";
export interface CommandEntry {
 command: WorkspaceCommand; payloadHash: string; acceptedAtUtc: string;
 projectionState: string; effects: string[]; confirmedEffects: number; failure: string;
 retryAtUtc: string | null; lease: null; dispatchedAtUtc: string | null; attempts: number;
}
export interface JournalEvent { id: string; commandId: string; kind: string; atUtc: string; confirmedEffects: number; failure: string; attemptToken: null; evidenceReference: null }
export async function accept(document: WorkspaceDocument, command: WorkspaceCommand, now: string): Promise<{ document: WorkspaceDocument; entry: CommandEntry; event: JournalEvent }> {
 document = workspaceDocument(document);
 if (command.workspaceId !== document.id) throw new WorkspaceError("workspace_mismatch", 409);
 if (command.expectedRevision !== document.revision) throw new WorkspaceError("stale_revision", 409);
 if (document.revision === Number.MAX_SAFE_INTEGER) throw new WorkspaceError("revision_exhausted", 409);
 const result = structuredClone(document); const revision = ++result.revision;
 let task = result.tasks.find(task => task.id === command.taskId); const patch = command.patch;
 if (command.operation === "createTodo" ? Boolean(task) : !task || task.isArchived) throw new WorkspaceError("task_unavailable", 422);
 if (command.operation === "completeTask" && (task!.kind === "habit" || ["completed", "cancelled", "paused"].includes(task!.state))) throw new WorkspaceError("task_not_completable", 422);
 if (command.operation === "scoreHabit" && (task!.kind !== "habit" || task!.state !== "active")) throw new WorkspaceError("habit_not_active", 422);
 if (command.operation === "completeTask" && (task!.kind === "daily" ? command.occurrenceDate === null : command.occurrenceDate !== null)) throw new WorkspaceError(task!.kind === "daily" ? "occurrence_required" : "unexpected_occurrence", 422);
 if (command.operation === "createTodo") {
  task = { id: command.taskId, revision, isArchived: false, text: patch!.text!, kind: "todo", state: "active", priority: patch!.priority ?? "normal", createdAtUtc: now, updatedAtUtc: now, notes: patch!.notes, categoryId: null, projectId: null, sectionId: null, parentTaskId: null, labelIds: [], dependsOn: [], plannedDate: null, deadline: patch!.deadline, reviewAfterUtc: null, staleSinceUtc: null, recurrence: null } satisfies CompanionTask;
  result.tasks.push(task);
 } else {
  task!.revision = revision; task!.updatedAtUtc = now;
  if (command.operation === "updateTask") { task!.text = patch!.text ?? task!.text; task!.notes = patch!.clearNotes ? null : patch!.notes ?? task!.notes; task!.priority = patch!.priority ?? task!.priority; task!.deadline = patch!.clearDeadline ? null : patch!.deadline ?? task!.deadline; }
  if (command.operation === "completeTask") {
   let occurrenceId = null;
   if (task!.kind === "daily") {
    let occurrence = result.occurrences.find(row => row.taskId === task!.id && row.date === command.occurrenceDate);
    if (occurrence && (occurrence.state !== "pending" || occurrence.isArchived)) throw new WorkspaceError("occurrence_unavailable", 422);
    if (!occurrence) { occurrence = { id: crypto.randomUUID(), revision, taskId: task!.id, date: command.occurrenceDate!, state: "completed", isArchived: false }; result.occurrences.push(occurrence); }
    occurrence.state = "completed"; occurrence.revision = revision; occurrenceId = occurrence.id;
   } else task!.state = "completed";
   result.completions.push({ id: crypto.randomUUID(), revision, taskId: task!.id, commandId: command.id, completedAtUtc: now, occurrenceId, isArchived: false });
  }
  if (command.operation === "scoreHabit") for (let i = 0; i < command.count; i++) result.habitScores.push({ id: crypto.randomUUID(), revision, taskId: task!.id, commandId: command.id, recordedAtUtc: now, direction: command.direction!, isArchived: false });
 }
 const mapping = result.projections.find(row => row.taskId === task!.id && !row.isArchived);
 const requested = command.projectToHabitica || Boolean(mapping && mapping.state !== "unlinked");
 const effects: string[] = [];
 if (requested) {
  if (!mapping?.habiticaTaskId) effects.push("create");
  if (command.operation === "updateTask") effects.push("update");
  if (command.operation === "completeTask") effects.push("scoreUp");
  if (command.operation === "scoreHabit") for (let i = 0; i < command.count; i++) effects.push(command.direction === "up" ? "scoreUp" : "scoreDown");
 }
 const blocked = mapping && ["externalConflict", "unknownOutcome"].includes(mapping.state);
 const failure = requested && blocked ? "precondition" : "none";
 if (requested && !mapping) result.projections.push({ id: crypto.randomUUID(), revision, taskId: task!.id, state: "pending", habiticaTaskId: null, isArchived: false });
 const entry: CommandEntry = { command, payloadHash: await commandHash(command), acceptedAtUtc: now, projectionState: !requested ? "notRequested" : blocked ? "blocked" : "pending", effects, confirmedEffects: 0, failure, retryAtUtc: null, lease: null, dispatchedAtUtc: null, attempts: 0 };
 return { document: workspaceDocument(result), entry, event: { id: crypto.randomUUID(), commandId: command.id, kind: "accepted", atUtc: now, confirmedEffects: 0, failure, attemptToken: null, evidenceReference: null } };
}
