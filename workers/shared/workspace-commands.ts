import {todoStage as parseTodoStage,type TodoStage} from './todo-stage.ts';
import {todoState as parseTodoState,type TodoState} from './todo-state.ts';
import {taskRelationships,type TaskRelationships} from './task-relationships.ts';
import { habiticaDetails, type HabiticaTaskDetails } from "./task-details.ts";
import {workspaceRecurrence,type Recurrence} from './workspace-domain.ts';
import {taskPlanning,type TaskPlanning} from './task-planning.ts';
import {todoRestore,type TodoRestore} from './todo-restore.ts';
export type WorkspaceOperation = "createTodo" | "updateTask" | "completeTask" | "scoreHabit" | "createDaily" | "createHabit" | "deleteTask" | "skipOccurrence" | "pauseTask" | "resumeTask" | "planTask" | "restoreTodo" | "relateTodo" | "setTodoState" | "setTodoStage";
export interface WorkspacePatch { text: string | null; notes: string | null; clearNotes: boolean; priority: "low" | "normal" | "high" | "urgent" | null; deadline: string | null; clearDeadline: boolean; habiticaDetails?: HabiticaTaskDetails | null; recurrence?:Recurrence|null;clearRecurrence?:boolean;planning?:TaskPlanning|null;restoreTodo?:TodoRestore|null;relationships?:TaskRelationships|null;todoState?:TodoState|null;todoStage?:TodoStage|null }
export interface WorkspaceCommand { id: string; workspaceId: string; expectedRevision: number; operation: WorkspaceOperation; taskId: string; patch: WorkspacePatch | null; direction: "up" | "down" | null; count: number; occurrenceDate: string | null; projectToHabitica: boolean }
const operations = ["createTodo", "updateTask", "completeTask", "scoreHabit", "createDaily", "createHabit", "deleteTask", "skipOccurrence", "pauseTask", "resumeTask", "planTask", "restoreTodo", "relateTodo", "setTodoState", "setTodoStage"];
export function isTaskCreation(operation: unknown): boolean { return ["createTodo", "createDaily", "createHabit"].includes(operation as string); }
function object(value: unknown): Record<string, unknown> {
  if (!value || typeof value !== "object" || Array.isArray(value)) throw new Error("invalid_command");
  return value as Record<string, unknown>;
}
function keys(value: Record<string, unknown>, allowed: string[]) { if (Object.keys(value).some(key => !allowed.includes(key))) throw new Error("invalid_command"); }
function id(value: unknown) {
  if (typeof value !== "string" || !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value) || value === "00000000-0000-0000-0000-000000000000") throw new Error("invalid_command");
  return value.toLowerCase();
}
function text(value: unknown, max: number): string | null {
  if (value === undefined || value === null) return null;
  if (typeof value !== "string" || value.length > max) throw new Error("invalid_command"); return value;
}
function bool(value: unknown) { if (value !== undefined && typeof value !== "boolean") throw new Error("invalid_command"); return value === true; }
function date(value: unknown): string | null {
  if (value === undefined || value === null) return null;
  if (typeof value !== "string" || !/^\d{4}-\d{2}-\d{2}$/.test(value) || value.startsWith("0000") || !Number.isFinite(Date.parse(value)) || new Date(value).toISOString().slice(0, 10) !== value) throw new Error("invalid_command");
  return value;
}
export function workspaceCommand(value: unknown): WorkspaceCommand {
  const input = object(value); keys(input, ["id", "workspaceId", "expectedRevision", "operation", "taskId", "patch", "direction", "count", "occurrenceDate", "projectToHabitica"]);
  if (!Number.isSafeInteger(input.expectedRevision) || Number(input.expectedRevision) < 1 || (typeof input.operation !== "string" || !operations.includes(input.operation))) throw new Error("invalid_command");
  let patch: WorkspacePatch | null = null;
  if (input.patch !== undefined && input.patch !== null) {
    const candidate = object(input.patch); keys(candidate, ["text", "notes", "clearNotes", "priority", "deadline", "clearDeadline", "habiticaDetails", "recurrence", "clearRecurrence", "planning", "restoreTodo", "relationships", "todoState", "todoStage"]);
    if (candidate.priority !== undefined && candidate.priority !== null && (typeof candidate.priority !== "string" || !["low", "normal", "high", "urgent"].includes(candidate.priority))) throw new Error("invalid_command");
    patch = { text: text(candidate.text, 2048), notes: text(candidate.notes, 10000), clearNotes: bool(candidate.clearNotes), priority: candidate.priority as WorkspacePatch["priority"] ?? null, deadline: date(candidate.deadline), clearDeadline: bool(candidate.clearDeadline), habiticaDetails: habiticaDetails(candidate.habiticaDetails,false) };
    let recurrence:Recurrence|null;try{recurrence=workspaceRecurrence(candidate.recurrence);}catch{throw new Error('invalid_command');}
    const clearRecurrence=bool(candidate.clearRecurrence);
    const planning=taskPlanning(candidate.planning);if(planning)patch.planning=planning;
    const restore=todoRestore(candidate.restoreTodo);if(restore)patch.restoreTodo=restore;
    const relationships=taskRelationships(candidate.relationships);if(relationships)patch.relationships=relationships;
    const todoState=parseTodoState(candidate.todoState);if(todoState)patch.todoState=todoState;
    const todoStage=parseTodoStage(candidate.todoStage);if(todoStage)patch.todoStage=todoStage;
    if(clearRecurrence&&recurrence)throw new Error('invalid_command');
    // Do not introduce new default keys into legacy previews or request identities.
    if(recurrence||clearRecurrence)Object.assign(patch,{...(recurrence?{recurrence}:{}),...(clearRecurrence?{clearRecurrence:true}:{})});
    if (patch.text !== null && /^\p{White_Space}*$/u.test(patch.text) || patch.clearNotes && patch.notes !== null || patch.clearDeadline && patch.deadline !== null) throw new Error("invalid_command");
    if (Object.values(patch).every(value => value === null || value === false)) throw new Error("invalid_command");
  }
  const operation = input.operation as WorkspaceOperation;
  if (isTaskCreation(operation) || operation === "updateTask" || operation === "planTask" || operation === "restoreTodo" || operation === "relateTodo" || operation === "setTodoState" || operation === "setTodoStage") { if (!patch || isTaskCreation(operation) && (patch.text === null || /^\p{White_Space}*$/u.test(patch.text))) throw new Error("invalid_command"); }
  else if (patch) throw new Error("invalid_command");
  if(operation==='setTodoStage'? !patch?.todoStage||Object.entries(patch).some(([key,value])=>key!=='todoStage'&&value!==null&&value!==false):Boolean(patch?.todoStage))throw new Error('invalid_command');
  if(operation==='setTodoState'? !patch?.todoState||Object.entries(patch).some(([key,value])=>key!=='todoState'&&value!==null&&value!==false):Boolean(patch?.todoState))throw new Error('invalid_command');
  if(operation==='relateTodo'? !patch?.relationships||Object.entries(patch).some(([key,value])=>key!=='relationships'&&value!==null&&value!==false):Boolean(patch?.relationships))throw new Error('invalid_command');
  if(operation==='restoreTodo'? !patch?.restoreTodo||Object.entries(patch).some(([key,value])=>key!=='restoreTodo'&&value!==null&&value!==false):Boolean(patch?.restoreTodo))throw new Error('invalid_command');
  if(operation==='planTask'? !patch?.planning||Object.entries(patch).some(([key,value])=>key!=='planning'&&value!==null&&value!==false):Boolean(patch?.planning))throw new Error('invalid_command');
  if (input.direction !== undefined && input.direction !== null && (typeof input.direction !== "string" || !["up", "down"].includes(input.direction))) throw new Error("invalid_command");
  const direction = input.direction as WorkspaceCommand["direction"] ?? null;
  const count = input.count === undefined ? 1 : input.count;
  if (!Number.isInteger(count) || Number(count) < 1 || Number(count) > 10 || operation !== "scoreHabit" && count !== 1 || (operation === "scoreHabit" ? direction === null : direction !== null)) throw new Error("invalid_command");
  const occurrenceDate = date(input.occurrenceDate);
  if (!["completeTask","skipOccurrence"].includes(operation) && occurrenceDate !== null) throw new Error("invalid_command");
  return { id: id(input.id), workspaceId: id(input.workspaceId), expectedRevision: Number(input.expectedRevision), operation, taskId: id(input.taskId), patch, direction, count: Number(count), occurrenceDate, projectToHabitica: bool(input.projectToHabitica) };
}
export async function commandHash(command: WorkspaceCommand): Promise<string> {
  const patch = command.patch;
  const fields = [command.id, command.workspaceId, String(command.expectedRevision), command.operation, command.taskId, patch === null ? "0" : "1", patch?.text ?? null, patch?.notes ?? null, patch?.clearNotes ? "1" : "0", patch?.priority ?? null, patch?.deadline ?? null, patch?.clearDeadline ? "1" : "0", command.direction, String(command.count), command.occurrenceDate, command.projectToHabitica ? "1" : "0"];
  const encoder = new TextEncoder();
  const details=patch?.habiticaDetails;
  const extended=Boolean(patch?.recurrence||patch?.clearRecurrence);
  const planning=patch?.planning,restore=patch?.restoreTodo,relationships=patch?.relationships,todoState=patch?.todoState,todoStage=patch?.todoStage;
  if(extended||planning||restore||relationships||todoState||todoStage)fields.push(details?'1':'0');
  if(details){fields.push(details.difficulty===null?null:String(details.difficulty),details.attribute,details.checklist===null?null:String(details.checklist.length));for(const item of details.checklist??[])fields.push(item.id,item.text,item.completed?"1":"0");}
  if(extended||planning||restore||relationships||todoState||todoStage){const rule=patch?.recurrence;fields.push(patch?.clearRecurrence?'1':'0',rule?'1':'0',rule?.frequency??null,rule?String(rule.interval):null,rule?.startDate??null,rule?.timeZoneId??null,rule?String(rule.weekdays.length):null,rule?String(rule.daysOfMonth.length):null);if(rule)fields.push(...rule.weekdays,...rule.daysOfMonth.map(String));}
  if(restore||relationships||todoState||todoStage)fields.push(planning?'1':'0');
  if(planning){fields.push(planning.plannedDate,planning.clearPlannedDate?'1':'0',planning.categoryId,planning.clearCategory?'1':'0',planning.projectId,planning.clearProject?'1':'0',planning.sectionId,planning.clearSection?'1':'0',planning.labelIds===null?null:String(planning.labelIds.length));if(planning.labelIds)fields.push(...planning.labelIds);}
  if(relationships||todoState||todoStage)fields.push(restore?'1':'0');
  if(restore)fields.push(restore.completionId);
  if(relationships){fields.push(relationships.parentTaskId,relationships.clearParentTask?'1':'0',relationships.dependsOn===null?null:String(relationships.dependsOn.length));if(relationships.dependsOn)fields.push(...relationships.dependsOn);}
  if(todoState||todoStage)fields.push(relationships?'1':'0');
  if(todoState)fields.push(todoState.state);
  if(todoStage)fields.push(todoState?'1':'0',todoStage.stage,todoStage.clearStage?'1':'0');
  const payload = (todoStage?"companion-command-v8\n":todoState?"companion-command-v7\n":relationships?"companion-command-v6\n":restore?"companion-command-v5\n":planning?"companion-command-v4\n":extended?"companion-command-v3\n":details?"companion-command-v2\n":"companion-command-v1\n") + fields.map(value => value === null ? "-1:" : encoder.encode(value).length + ":" + value).join("");
  return Array.from(new Uint8Array(await crypto.subtle.digest("SHA-256", encoder.encode(payload))), value => value.toString(16).padStart(2, "0")).join("");
}
