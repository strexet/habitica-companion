import { mergeDetails } from "../../shared/task-details.ts";
import { workspaceDocument, type WorkspaceDocument, type CompanionTask } from "../../shared/workspace-domain.ts";
import { commandHash, isTaskCreation, type WorkspaceCommand } from "../../shared/workspace-commands.ts";
import { WorkspaceError } from "../../shared/workspace-http.ts";
import {expandRecurrence} from '../../shared/recurrence-calendar.ts';
import {applyPlanning,planningReferencesAvailable} from '../../shared/task-planning.ts';
export interface CommandEntry {
 command: WorkspaceCommand; payloadHash: string; acceptedAtUtc: string;
 projectionState: string; effects: string[]; confirmedEffects: number; failure: string;
 retryAtUtc: string | null; lease: {owner:string;token:string;expiresAtUtc:string} | null; dispatchedAtUtc: string | null; attempts: number; taskSnapshot?: CompanionTask | null;
}
export interface JournalEvent { id: string; commandId: string; kind: string; atUtc: string; confirmedEffects: number; failure: string; attemptToken: string | null; evidenceReference: string | null }
export async function accept(document: WorkspaceDocument, command: WorkspaceCommand, now: string): Promise<{ document: WorkspaceDocument; entry: CommandEntry; event: JournalEvent }> {
 document = workspaceDocument(document);
 if (command.workspaceId !== document.id) throw new WorkspaceError("workspace_mismatch", 409);
 if (command.expectedRevision !== document.revision) throw new WorkspaceError("stale_revision", 409);
 if (document.revision === Number.MAX_SAFE_INTEGER) throw new WorkspaceError("revision_exhausted", 409);
 const result = structuredClone(document); const revision = ++result.revision;
 let task = result.tasks.find(task => task.id === command.taskId); const patch = command.patch;
 if(patch?.recurrence&&expandRecurrence(patch.recurrence,patch.recurrence.startDate,patch.recurrence.startDate).failure)throw new WorkspaceError('invalid_patch',422);
 if (isTaskCreation(command.operation) ? Boolean(task) : !task || task.isArchived) throw new WorkspaceError("task_unavailable", 422);
 const mapping = result.projections.find(row => row.taskId === command.taskId && !row.isArchived);
 if(command.operation==='planTask'){if(command.projectToHabitica)throw new WorkspaceError('unsupported_projection',422);if(!planningReferencesAvailable(result,task!,patch!.planning!))throw new WorkspaceError('invalid_planning_reference',422);}
 if(command.operation==='restoreTodo'){
  const retained=result.projections.find(row=>row.taskId===command.taskId);
  if(command.projectToHabitica||retained&&(retained.state!=='unlinked'||retained.habiticaTaskId!==null))throw new WorkspaceError('unsupported_projection',422);
  if(task!.kind!=='todo'||task!.state!=='completed')throw new WorkspaceError('todo_not_completed',422);
  const completion=result.completions.find(row=>row.id===patch!.restoreTodo!.completionId);
  if(!completion||completion.taskId!==task!.id||completion.occurrenceId!==null)throw new WorkspaceError('completion_unavailable',422);
  if(result.restorations?.some(row=>row.completionId===completion.id))throw new WorkspaceError('completion_already_restored',422);
 }
 const lifecycle=['skipOccurrence','pauseTask','resumeTask'].includes(command.operation);
 if(lifecycle){
  if(command.projectToHabitica||mapping&&(mapping.state!=='unlinked'||mapping.habiticaTaskId!==null))throw new WorkspaceError('unsupported_projection',422);
  if(!['daily','habit'].includes(task!.kind))throw new WorkspaceError('recurring_task_required',422);
  if(command.operation==='resumeTask'?task!.state!=='paused':task!.state!=='active')throw new WorkspaceError(command.operation==='resumeTask'?'task_not_paused':'task_not_active',422);
  if(command.operation==='skipOccurrence'){
   if(task!.kind!=='daily')throw new WorkspaceError('daily_required',422);
   if(command.occurrenceDate===null)throw new WorkspaceError('occurrence_required',422);
   if(!task!.recurrence)throw new WorkspaceError('recurrence_required',422);
  }
 }
 const editsRecurrence=Boolean(patch?.recurrence||patch?.clearRecurrence);
 if(editsRecurrence&&(isTaskCreation(command.operation)?command.operation!=='createDaily'||patch!.clearRecurrence:task!.kind!=='daily'))throw new WorkspaceError('recurrence_requires_daily',422);
 if(editsRecurrence&&(command.projectToHabitica||mapping&&(mapping.state!=='unlinked'||mapping.habiticaTaskId!==null)))throw new WorkspaceError('unsupported_projection',422);
 if (["createDaily", "createHabit", "deleteTask"].includes(command.operation) && (command.projectToHabitica || mapping && (mapping.state !== "unlinked" || mapping.habiticaTaskId !== null))) throw new WorkspaceError("unsupported_projection", 422);
 if (command.operation === "completeTask" && (task!.kind === "habit" || ["completed", "cancelled", "paused"].includes(task!.state))) throw new WorkspaceError("task_not_completable", 422);
 if (command.operation === "scoreHabit" && (task!.kind !== "habit" || task!.state !== "active")) throw new WorkspaceError("habit_not_active", 422);
 if (command.operation === "completeTask" && (task!.kind === "daily" ? command.occurrenceDate === null : command.occurrenceDate !== null)) throw new WorkspaceError(task!.kind === "daily" ? "occurrence_required" : "unexpected_occurrence", 422);
 if(['completeTask','skipOccurrence'].includes(command.operation)&&task!.kind==='daily'&&task!.recurrence){const dates=expandRecurrence(task!.recurrence,command.occurrenceDate!,command.occurrenceDate!);if(dates.failure)throw new WorkspaceError('recurrence_unavailable',422);if(!dates.dates.length)throw new WorkspaceError('occurrence_not_scheduled',422);}
 if (isTaskCreation(command.operation)) {
  task = { id: command.taskId, revision, isArchived: false, text: patch!.text!, kind: command.operation === "createDaily" ? "daily" : command.operation === "createHabit" ? "habit" : "todo", state: "active", priority: patch!.priority ?? "normal", createdAtUtc: now, updatedAtUtc: now, notes: patch!.notes, categoryId: null, projectId: null, sectionId: null, parentTaskId: null, labelIds: [], dependsOn: [], plannedDate: null, deadline: patch!.deadline, reviewAfterUtc: null, staleSinceUtc: null, recurrence: patch!.recurrence ?? null, habiticaDetails: patch!.habiticaDetails ?? null } satisfies CompanionTask;
  result.tasks.push(task);
 } else {
  task!.revision = revision; task!.updatedAtUtc = now;
  if(command.operation==='planTask'){const planned=applyPlanning(task!,patch!.planning!);Object.assign(task!,planned);}
  if(command.operation==='restoreTodo'){task!.state='active';result.restorations??=[];result.restorations.push({id:crypto.randomUUID(),revision,taskId:task!.id,completionId:patch!.restoreTodo!.completionId,commandId:command.id,restoredAtUtc:now,isArchived:false});}
  if(command.operation==='pauseTask')task!.state='paused';
  if(command.operation==='resumeTask')task!.state='active';
  if(command.operation==='skipOccurrence'){
   let occurrence=result.occurrences.find(row=>row.taskId===task!.id&&row.date===command.occurrenceDate);
   if(occurrence&&(occurrence.state!=='pending'||occurrence.isArchived))throw new WorkspaceError('occurrence_unavailable',422);
   if(!occurrence){occurrence={id:crypto.randomUUID(),revision,taskId:task!.id,date:command.occurrenceDate!,state:'skipped',isArchived:false};result.occurrences.push(occurrence);}
   occurrence.state='skipped';occurrence.revision=revision;
  }
  if (command.operation === "deleteTask") task!.isArchived = true;
  if (command.operation === "updateTask") { task!.recurrence=patch!.clearRecurrence?null:patch!.recurrence??task!.recurrence; task!.habiticaDetails=mergeDetails(task!.habiticaDetails,patch!.habiticaDetails); task!.text = patch!.text ?? task!.text; task!.notes = patch!.clearNotes ? null : patch!.notes ?? task!.notes; task!.priority = patch!.priority ?? task!.priority; task!.deadline = patch!.clearDeadline ? null : patch!.deadline ?? task!.deadline; }
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
 const requested = !['planTask','restoreTodo'].includes(command.operation)&&(command.projectToHabitica || Boolean(mapping && mapping.state !== "unlinked"));
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
 const entry: CommandEntry = { command, payloadHash: await commandHash(command), acceptedAtUtc: now, projectionState: !requested ? "notRequested" : blocked ? "blocked" : "pending", effects, confirmedEffects: 0, failure, retryAtUtc: null, lease: null, dispatchedAtUtc: null, attempts: 0, taskSnapshot: structuredClone(task!) };
 return { document: workspaceDocument(result), entry, event: { id: crypto.randomUUID(), commandId: command.id, kind: "accepted", atUtc: now, confirmedEffects: 0, failure, attemptToken: null, evidenceReference: null } };
}
