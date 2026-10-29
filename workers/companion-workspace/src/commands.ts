import {activityWitness} from '../../shared/activity-coverage.ts';
import {validateLinkedRecoveryHashes} from '../../shared/linked-recovery.ts';
import {hasGameFence,applyUnarchive,unarchiveReferencesAvailable} from '../../shared/task-recovery.ts';
import {reviewCanApply,applyReview} from '../../shared/task-review.ts';
import {validateConversionHashes} from '../../shared/checklist-conversion.ts';
import {dailyRuleFailure} from '../../shared/daily-projection.ts';
import {validateOrganizationHistoryHashes} from '../../shared/workspace-organization.ts';
import {applyRelationships,relationshipReferencesAvailable} from '../../shared/task-relationships.ts';
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
export async function accept(document: WorkspaceDocument, command: WorkspaceCommand, now: string, history:CommandEntry[]=[]): Promise<{ document: WorkspaceDocument; entry: CommandEntry; event: JournalEvent }> {
 document = workspaceDocument(document);
 await validateOrganizationHistoryHashes(document);await validateConversionHashes(document);await validateLinkedRecoveryHashes(document);
 if (command.workspaceId !== document.id) throw new WorkspaceError("workspace_mismatch", 409);
 if(document.organizationHistory?.some(entry=>entry.command.id===command.id)||document.linkedTaskRecoveries?.some(entry=>entry.id===command.id))throw new WorkspaceError('command_id_reused',409);
 if (command.expectedRevision !== document.revision) throw new WorkspaceError("stale_revision", 409);
 if (document.revision === Number.MAX_SAFE_INTEGER) throw new WorkspaceError("revision_exhausted", 409);
 const result = structuredClone(document); const revision = ++result.revision;
 const previousTaskRevision=document.tasks.find(task=>task.id===command.taskId)?.revision??0;
 let task = result.tasks.find(task => task.id === command.taskId); const patch = command.patch;
 if(patch?.recurrence&&expandRecurrence(patch.recurrence,patch.recurrence.startDate,patch.recurrence.startDate).failure)throw new WorkspaceError('invalid_patch',422);
 if (isTaskCreation(command.operation) ? Boolean(task) : !task || (command.operation==='unarchiveTask'?!task.isArchived:task.isArchived)) throw new WorkspaceError("task_unavailable", 422);
 const mapping = result.projections.find(row => row.taskId === command.taskId && !row.isArchived);
 if(command.operation==='ensureProjection'){
  if(!command.projectToHabitica)throw new WorkspaceError('projection_required',422);
  if(task!.state!=='active')throw new WorkspaceError('historical_projection_requires_review',409);
  if(!['todo','habit'].includes(task!.kind))throw new WorkspaceError('adoption_required',422);
  if(task!.kind==='habit'&&(task!.deadline!==null||task!.habiticaDetails?.checklist!=null))throw new WorkspaceError('unsupported_habit_fields',422);
  if(result.projections.some(row=>row.taskId===task!.id&&(row.isArchived||['unknownOutcome','externalConflict'].includes(row.state))))throw new WorkspaceError('projection_review_required',409);
 }
 if(['unarchiveTask','correctOccurrence'].includes(command.operation)){
  if(command.projectToHabitica||hasGameFence(result,command.taskId)||history.some(entry=>entry.effects.length>0&&!['confirmed','cancelled'].includes(entry.projectionState)))throw new WorkspaceError('unsupported_projection',422);
  if(command.operation==='unarchiveTask'){
   if(patch!.unarchive!.archivedRevision!==task!.revision)throw new WorkspaceError('archive_revision_mismatch',422);
   if(!unarchiveReferencesAvailable(result,task!,patch!.unarchive!))throw new WorkspaceError('unarchive_repair_required',422);
  }else{
   const target=patch!.correctOccurrence!,occurrence=result.occurrences.find(row=>row.id===target.occurrenceId),source=history.find(row=>row.command.id===target.targetCommandId);
   if(task!.kind!=='daily'||!occurrence||occurrence.taskId!==task!.id||occurrence.isArchived||occurrence.state==='pending'||occurrence.revision!==target.occurrenceRevision
    ||!source||source.command.taskId!==task!.id||source.command.expectedRevision+1!==occurrence.revision||source.command.operation!==(occurrence.state==='completed'?'completeTask':'skipOccurrence')||source.command.occurrenceDate!==occurrence.date||source.effects.length!==0)throw new WorkspaceError('occurrence_source_unavailable',422);
  }
 }
 if(command.operation==='reviewTask'&&(command.projectToHabitica||!reviewCanApply(command.patch!.review!,task!,now)))throw new WorkspaceError('invalid_review_action',422);
 if(command.operation==='setTodoStage'){if(command.projectToHabitica)throw new WorkspaceError('unsupported_projection',422);if(task!.kind!=='todo'||task!.state!=='active')throw new WorkspaceError('active_todo_required',422);}
 if(command.operation==='setTodoState'){if(command.projectToHabitica)throw new WorkspaceError('unsupported_projection',422);if(task!.kind!=='todo'||task!.state==='completed')throw new WorkspaceError('todo_state_unavailable',422);}
 if(command.operation==='planTask'){if(command.projectToHabitica)throw new WorkspaceError('unsupported_projection',422);if(!planningReferencesAvailable(result,task!,patch!.planning!))throw new WorkspaceError('invalid_planning_reference',422);}
 if(command.operation==='relateTodo'){if(command.projectToHabitica)throw new WorkspaceError('unsupported_projection',422);if(!relationshipReferencesAvailable(result,task!,patch!.relationships!))throw new WorkspaceError('invalid_relationship_reference',422);}
 if(command.operation==='restoreTodo'){
  const retained=result.projections.find(row=>row.taskId===command.taskId);
  if(command.projectToHabitica||retained&&(retained.state!=='unlinked'||retained.habiticaTaskId!==null))throw new WorkspaceError('unsupported_projection',422);
  if(task!.kind!=='todo'||task!.state!=='completed')throw new WorkspaceError('todo_not_completed',422);
  const completion=result.completions.find(row=>row.id===patch!.restoreTodo!.completionId);
  if(!completion||completion.taskId!==task!.id||completion.occurrenceId!==null)throw new WorkspaceError('completion_unavailable',422);
  if(result.restorations?.some(row=>row.completionId===completion.id))throw new WorkspaceError('completion_already_restored',422);
 }
 const resultingKind=isTaskCreation(command.operation)?command.operation==='createHabit'?'habit':command.operation==='createDaily'?'daily':'todo':task!.kind;
 if((patch?.habiticaDetails?.up!==undefined||patch?.habiticaDetails?.down!==undefined)&&resultingKind!=='habit')throw new WorkspaceError('habit_directions_required_kind',422);
 if(command.operation==='createHabit'&&command.projectToHabitica&&(patch!.deadline!==null||patch!.habiticaDetails?.checklist!=null||patch!.habiticaDetails?.up===undefined||patch!.habiticaDetails?.down===undefined))throw new WorkspaceError('unsupported_habit_fields',422);
 if(resultingKind==='habit'&&(command.projectToHabitica||mapping&&mapping.state!=='unlinked')&&(patch?.deadline!=null||patch?.clearDeadline||patch?.habiticaDetails?.checklist!=null))throw new WorkspaceError('unsupported_habit_fields',422);
 if(command.operation==='scoreHabit'&&(command.direction==='up'?task!.habiticaDetails?.up===false:task!.habiticaDetails?.down===false))throw new WorkspaceError('habit_direction_disabled',422);
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
 if(command.operation==='createDaily'&&command.projectToHabitica){const failure=dailyRuleFailure(patch?.recurrence);if(failure)throw new WorkspaceError(failure,422);if(patch?.deadline!==null)throw new WorkspaceError('unsupported_daily_fields',422);}
 const editsRecurrence=Boolean(patch?.recurrence||patch?.clearRecurrence);
 if(editsRecurrence&&(isTaskCreation(command.operation)?command.operation!=='createDaily'||patch!.clearRecurrence:task!.kind!=='daily'))throw new WorkspaceError('recurrence_requires_daily',422);
 if(editsRecurrence&&!isTaskCreation(command.operation)&&(command.projectToHabitica||mapping&&(mapping.state!=='unlinked'||mapping.habiticaTaskId!==null)))throw new WorkspaceError('unsupported_projection',422);
 if (command.operation === "deleteTask" && (command.projectToHabitica || mapping && (mapping.state !== "unlinked" || mapping.habiticaTaskId !== null))) throw new WorkspaceError("unsupported_projection", 422);
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
  if(command.operation==='reviewTask'){applyReview(patch!.review!,task!);result.reviewEvents??=[];result.reviewEvents.push({id:crypto.randomUUID(),revision,taskId:task!.id,commandId:command.id,action:patch!.review!.action,recordedAtUtc:now,timeZoneId:patch!.review!.timeZoneId,reviewAfterUtc:task!.reviewAfterUtc,staleSinceUtc:task!.staleSinceUtc,isArchived:false});}
  if(command.operation==='setTodoStage'){if(patch!.todoStage!.clearStage)delete task!.executionStage;else task!.executionStage=patch!.todoStage!.stage!;}
  if(command.operation==='setTodoState')task!.state=patch!.todoState!.state;
  if(command.operation==='relateTodo')Object.assign(task!,applyRelationships(task!,patch!.relationships!));
  if(command.operation==='unarchiveTask'){const archivedRevision=patch!.unarchive!.archivedRevision;task=applyUnarchive(task!,patch!.unarchive!);result.tasks[result.tasks.findIndex(row=>row.id===task!.id)]=task;result.unarchives??=[];result.unarchives.push({id:crypto.randomUUID(),revision,taskId:task.id,archivedRevision,commandId:command.id,unarchivedAtUtc:now,isArchived:false});}
  if(command.operation==='correctOccurrence'){const occurrence=result.occurrences.find(row=>row.id===patch!.correctOccurrence!.occurrenceId)!;result.occurrenceCorrections??=[];result.occurrenceCorrections.push({id:crypto.randomUUID(),revision,taskId:task!.id,commandId:command.id,targetCommandId:patch!.correctOccurrence!.targetCommandId,previousOccurrence:structuredClone(occurrence),correctedAtUtc:now,isArchived:false});occurrence.state='pending';occurrence.revision=revision;delete occurrence.terminalCommandId;}
  if(command.operation==='restoreTodo'){task!.state='active';result.restorations??=[];result.restorations.push({id:crypto.randomUUID(),revision,taskId:task!.id,completionId:patch!.restoreTodo!.completionId,commandId:command.id,restoredAtUtc:now,isArchived:false});}
  if(command.operation==='pauseTask')task!.state='paused';
  if(command.operation==='resumeTask')task!.state='active';
  if(command.operation==='skipOccurrence'){
   let occurrence=result.occurrences.find(row=>row.taskId===task!.id&&row.date===command.occurrenceDate);
   if(occurrence&&(occurrence.state!=='pending'||occurrence.isArchived))throw new WorkspaceError('occurrence_unavailable',422);
   if(!occurrence){occurrence={id:crypto.randomUUID(),revision,taskId:task!.id,date:command.occurrenceDate!,state:'skipped',isArchived:false};result.occurrences.push(occurrence);}
   occurrence.state='skipped';occurrence.revision=revision;occurrence.terminalCommandId=command.id;
  }
  if (command.operation === "deleteTask") task!.isArchived = true;
  if (command.operation === "updateTask") { task!.recurrence=patch!.clearRecurrence?null:patch!.recurrence??task!.recurrence; task!.habiticaDetails=mergeDetails(task!.habiticaDetails,patch!.habiticaDetails); task!.text = patch!.text ?? task!.text; task!.notes = patch!.clearNotes ? null : patch!.notes ?? task!.notes; task!.priority = patch!.priority ?? task!.priority; task!.deadline = patch!.clearDeadline ? null : patch!.deadline ?? task!.deadline; }
  if (command.operation === "completeTask") {
   let occurrenceId = null;
   if (task!.kind === "daily") {
    let occurrence = result.occurrences.find(row => row.taskId === task!.id && row.date === command.occurrenceDate);
    if (occurrence && (occurrence.state !== "pending" || occurrence.isArchived)) throw new WorkspaceError("occurrence_unavailable", 422);
    if (!occurrence) { occurrence = { id: crypto.randomUUID(), revision, taskId: task!.id, date: command.occurrenceDate!, state: "completed", isArchived: false }; result.occurrences.push(occurrence); }
    occurrence.state = "completed";occurrence.terminalCommandId=command.id; occurrence.revision = revision; occurrenceId = occurrence.id;
   } else task!.state = "completed";
   result.completions.push({ id: crypto.randomUUID(), revision, taskId: task!.id, commandId: command.id, completedAtUtc: now, occurrenceId, isArchived: false });
  }
  if (command.operation === "scoreHabit") for (let i = 0; i < command.count; i++) result.habitScores.push({ id: crypto.randomUUID(), revision, taskId: task!.id, commandId: command.id, recordedAtUtc: now, direction: command.direction!, isArchived: false });
 }
 const requested = !['planTask','restoreTodo','relateTodo','setTodoState','setTodoStage','reviewTask','unarchiveTask','correctOccurrence'].includes(command.operation)&&(command.projectToHabitica || Boolean(mapping && mapping.state !== "unlinked"));
 const effects: string[] = [];
 if (requested) {
  if (!mapping?.habiticaTaskId || command.operation==='ensureProjection') effects.push("create");
  if (command.operation === "updateTask") effects.push("update");
  if (command.operation === "completeTask") effects.push("scoreUp");
  if (command.operation === "scoreHabit") for (let i = 0; i < command.count; i++) effects.push(command.direction === "up" ? "scoreUp" : "scoreDown");
 }
 const blocked = mapping && ["externalConflict", "unknownOutcome"].includes(mapping.state);
 const failure = requested && blocked ? "precondition" : "none";
 if (requested && !mapping) result.projections.push({ id: crypto.randomUUID(), revision, taskId: task!.id, state: "pending", habiticaTaskId: null, isArchived: false });
 const entry: CommandEntry = { command, payloadHash: await commandHash(command), acceptedAtUtc: now, projectionState: !requested ? "notRequested" : blocked ? "blocked" : "pending", effects, confirmedEffects: 0, failure, retryAtUtc: null, lease: null, dispatchedAtUtc: null, attempts: 0, taskSnapshot: structuredClone(task!) };
 result.activityCoverage=[...(document.activityCoverage??[]),activityWitness(entry,previousTaskRevision)];
 return { document: workspaceDocument(result), entry, event: { id: crypto.randomUUID(), commandId: command.id, kind: "accepted", atUtc: now, confirmedEffects: 0, failure, attemptToken: null, evidenceReference: null } };
}
