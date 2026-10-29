import {activityCoverage,type ActivityWitness} from './activity-coverage.ts';
import {linkedRecoveryRows,type LinkedRecoveryReceipt} from './linked-recovery.ts';
import {recoveryHistoryValid,completionCorrected,type UnarchiveEvent,type OccurrenceCorrection} from './task-recovery.ts';
import {reviewInstant,reviewZone,type ReviewEvent} from './task-review.ts';
import {sourceRetirements,type SourceRetirement} from './source-retirements.ts';
import {checklistConversions,type ChecklistConversion} from './checklist-conversion.ts';
import {projectAlias} from './workspace-organization.ts';
import {tagLedger,type TagLedger} from './tag-projection.ts';
import {savedTaskViews,type SavedTaskView} from './saved-task-views.ts';
import {executionStage,type TodoExecutionStage} from './todo-stage.ts';
import {organizationHistory,type OrganizationEntry} from './workspace-organization.ts';
import { habiticaDetails, type HabiticaTaskDetails } from "./task-details.ts";
import { WorkspaceError, workspaceObject, uuid } from "./workspace-http.ts";
export interface Entity { id: string; revision: number; isArchived: boolean }
export interface Category extends Entity { name: string }
export interface Project extends Category { categoryId: string | null; isRemoved?:boolean; alias?:string }
export interface Section extends Category { projectId: string; position: number }
export interface Goal extends Category { taskIds: string[] }
export interface Recurrence { frequency: "daily" | "weekly" | "monthly"; interval: number; startDate: string; timeZoneId: string; weekdays: string[]; daysOfMonth: number[] }
export interface CompanionTask extends Entity {
  executionStage?:TodoExecutionStage; habiticaDetails?: HabiticaTaskDetails | null; text: string; kind: "todo" | "daily" | "habit"; state: "active" | "completed" | "waiting" | "someday" | "paused" | "cancelled"; priority: "low" | "normal" | "high" | "urgent";
  createdAtUtc: string; updatedAtUtc: string; notes: string | null; categoryId: string | null; projectId: string | null; sectionId: string | null; parentTaskId: string | null;
  labelIds: string[]; dependsOn: string[]; plannedDate: string | null; deadline: string | null; reviewAfterUtc: string | null; staleSinceUtc: string | null; recurrence: Recurrence | null;
}
export interface Occurrence extends Entity { terminalCommandId?:string; taskId: string; date: string; state: "pending" | "completed" | "skipped" }
export interface Completion extends Entity { taskId: string; commandId: string; completedAtUtc: string; occurrenceId: string | null }
export interface Restoration extends Entity {taskId:string;completionId:string;commandId:string;restoredAtUtc:string}
export interface HabitScore extends Entity { taskId: string; commandId: string; recordedAtUtc: string; direction: "up" | "down" }
export interface Projection extends Entity { taskId: string; state: "unlinked" | "pending" | "linked" | "externalConflict" | "unknownOutcome"; habiticaTaskId: string | null; fingerprint?: string | null; correlationAlias?: string | null }
export interface WorkspaceDocument { activityCoverage?:ActivityWitness[]; linkedTaskRecoveries?:LinkedRecoveryReceipt[]; unarchives?:UnarchiveEvent[]; occurrenceCorrections?:OccurrenceCorrection[]; reviewEvents?:ReviewEvent[]; sourceRetirements?:SourceRetirement[]; checklistConversions?:ChecklistConversion[]; tagLedger?:TagLedger; schemaVersion: 1; savedTaskViews?:SavedTaskView[]; id: string; revision: number; categories: Category[]; projects: Project[]; sections: Section[]; labels: Category[]; goals: Goal[]; tasks: CompanionTask[]; occurrences: Occurrence[]; completions: Completion[]; restorations?:Restoration[]; organizationHistory?:OrganizationEntry[]; habitScores: HabitScore[]; projections: Projection[] }
const fail = (): never => { throw new WorkspaceError("invalid_workspace", 422); };
function named(value: unknown, max = 256, allowEmpty = false): string { if (typeof value !== "string" || value.length > max || !allowEmpty && /^\p{White_Space}*$/u.test(value)) return fail(); return value; }
function optionalId(value: unknown) { return value === undefined || value === null ? null : uuid(value); }
function integer(value: unknown, min = 1, max = Number.MAX_SAFE_INTEGER): number { if (!Number.isSafeInteger(value) || Number(value) < min || Number(value) > max) return fail(); return Number(value); }
function archived(value: unknown) { if (value !== undefined && typeof value !== "boolean") return fail(); return value === true; }
function choice<T extends string>(value: unknown, options: readonly T[]): T { if (typeof value !== "string" || !options.includes(value as T)) return fail(); return value as T; }
function list(value: unknown): unknown[] { if (value === undefined) return []; if (!Array.isArray(value)) return fail(); return value; }
function ids(value: unknown, optional = false): string[] { if (optional && (value === undefined || value === null)) return []; const result = list(value).map(uuid); if (new Set(result).size !== result.length) return fail(); return result; }
function date(value: unknown): string { const text = named(value, 10); if (!/^\d{4}-\d{2}-\d{2}$/.test(text) || text.startsWith("0000") || !Number.isFinite(Date.parse(text)) || new Date(text).toISOString().slice(0, 10) !== text) return fail(); return text; }
export function utcTicks(value: unknown): bigint {
  if (typeof value !== "string") return fail();
  const match = value.match(/^(\d{4}-\d{2}-\d{2}T(?:[01]\d|2[0-3]):[0-5]\d:[0-5]\d)(?:\.(\d{1,7}))?(?:Z|\+00:00)$/);
  if (!match || value.startsWith("0000") || !Number.isFinite(Date.parse(match[1] + "Z")) || new Date(match[1] + "Z").toISOString().slice(0, 10) !== match[1].slice(0, 10)) return fail();
  return BigInt(Date.parse(match[1] + "Z")) * 10000n + BigInt((match[2] ?? "").padEnd(7, "0"));
}
function utc(value: unknown): string { if (utcTicks(value) === utcTicks("0001-01-01T00:00:00Z")) return fail(); return value as string; }
const optDate = (value: unknown) => value === undefined || value === null ? null : date(value);
const optUtc = (value: unknown) => value === undefined || value === null ? null : utc(value);
function entity(value: Record<string, unknown>, revision: number): Entity { return { id: uuid(value.id), revision: integer(value.revision, 1, revision), isArchived: archived(value.isArchived) }; }
const baseKeys = ["id", "revision", "isArchived"];
function recurrence(value: unknown): Recurrence | null {
  if (value === undefined || value === null) return null;
  const row = workspaceObject(value, ["frequency", "interval", "startDate", "timeZoneId", "weekdays", "daysOfMonth"]);
  if (!Array.isArray(row.weekdays) || !Array.isArray(row.daysOfMonth)) return fail();
  const frequency = choice(row.frequency, ["daily", "weekly", "monthly"]);
  const weekdays = row.weekdays.map(day => choice(day, ["sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday"]));
  const daysOfMonth = row.daysOfMonth.map(day => integer(day, 1, 31));
  if (new Set(weekdays).size !== weekdays.length || new Set(daysOfMonth).size !== daysOfMonth.length
    || frequency === "weekly" && weekdays.length === 0 || frequency !== "weekly" && weekdays.length !== 0
    || frequency === "monthly" && daysOfMonth.length === 0 || frequency !== "monthly" && daysOfMonth.length !== 0 || row.startDate === "0001-01-01") return fail();
  return { frequency, interval: integer(row.interval, 1, 366), startDate: date(row.startDate), timeZoneId: named(row.timeZoneId, 128), weekdays, daysOfMonth };
}
export { recurrence as workspaceRecurrence };
export function workspaceTask(item:unknown,revision:number):CompanionTask {
    const row = workspaceObject(item, [...baseKeys, "text", "kind", "state", "priority", "createdAtUtc", "updatedAtUtc", "notes", "categoryId", "projectId", "sectionId", "parentTaskId", "labelIds", "dependsOn", "plannedDate", "deadline", "reviewAfterUtc", "staleSinceUtc", "recurrence", "habiticaDetails", "executionStage"]);
    const task:CompanionTask = { ...entity(row, revision), habiticaDetails: habiticaDetails(row.habiticaDetails), text: named(row.text, 2048), kind: choice(row.kind, ["todo", "daily", "habit"]), state: choice(row.state, ["active", "completed", "waiting", "someday", "paused", "cancelled"]), priority: choice(row.priority, ["low", "normal", "high", "urgent"]),
      createdAtUtc: utc(row.createdAtUtc), updatedAtUtc: utc(row.updatedAtUtc), notes: row.notes === undefined || row.notes === null ? null : named(row.notes, 10000, true),
      categoryId: optionalId(row.categoryId), projectId: optionalId(row.projectId), sectionId: optionalId(row.sectionId), parentTaskId: optionalId(row.parentTaskId), labelIds: ids(row.labelIds, true), dependsOn: ids(row.dependsOn, true), plannedDate: optDate(row.plannedDate), deadline: optDate(row.deadline), reviewAfterUtc: optUtc(row.reviewAfterUtc), staleSinceUtc: optUtc(row.staleSinceUtc), recurrence: recurrence(row.recurrence) };
    if (utcTicks(task.createdAtUtc) > utcTicks(task.updatedAtUtc) || task.kind !== "todo" && ["completed", "cancelled"].includes(task.state) || task.kind !== "daily" && task.recurrence !== null) return fail();
    if(task.kind!=='habit'&&(task.habiticaDetails?.up!==undefined||task.habiticaDetails?.down!==undefined))return fail();
    const stage=executionStage(row.executionStage);if(stage){if(task.kind!=='todo')return fail();task.executionStage=stage;}
    return task;
}
export function workspaceDocument(input: unknown): WorkspaceDocument {
  const value = workspaceObject(input, ["activityCoverage", "linkedTaskRecoveries", "unarchives", "occurrenceCorrections", "reviewEvents", "sourceRetirements", "checklistConversions", "tagLedger", "schemaVersion", "id", "revision", "categories", "projects", "sections", "labels", "goals", "tasks", "occurrences", "completions", "restorations", "organizationHistory", "savedTaskViews", "habitScores", "projections"]);
  if (value.schemaVersion !== 1) return fail(); const revision = integer(value.revision); const id = uuid(value.id);
  const names = (key: string): Category[] => list(value[key]).map(item => { const row = workspaceObject(item, [...baseKeys, "name"]); return { ...entity(row, revision), name: named(row.name) }; });
  const categories = names("categories"); const labels = names("labels");
  const projects:Project[] = list(value.projects).map(item => { const row = workspaceObject(item, [...baseKeys, "name", "categoryId", "isRemoved", "alias"]),base=entity(row,revision); if(row.isRemoved!==undefined&&typeof row.isRemoved!=="boolean"||row.isRemoved===true&&!base.isArchived)return fail(); return { ...base, name: named(row.name), categoryId: optionalId(row.categoryId),...(row.isRemoved===true?{isRemoved:true}:{}),...(row.alias==null?{}:{alias:projectAlias(row.alias,true)}) }; });
  const sections = list(value.sections).map(item => { const row = workspaceObject(item, [...baseKeys, "name", "projectId", "position"]); return { ...entity(row, revision), name: named(row.name), projectId: uuid(row.projectId), position: integer(row.position ?? 0, 0, 2147483647) }; });
  const goals = list(value.goals).map(item => { const row = workspaceObject(item, [...baseKeys, "name", "taskIds"]); if (!Array.isArray(row.taskIds)) return fail(); return { ...entity(row, revision), name: named(row.name), taskIds: ids(row.taskIds) }; });
  const tasks:CompanionTask[]=list(value.tasks).map(item=>workspaceTask(item,revision));
  const parseOccurrence = (item:unknown):Occurrence => { const row = workspaceObject(item, [...baseKeys, "taskId", "date", "state", "terminalCommandId"]); if (row.date === "0001-01-01") return fail(); return { ...entity(row, revision), taskId: uuid(row.taskId), date: date(row.date), state: choice(row.state, ["pending", "completed", "skipped"]),...(row.terminalCommandId==null?{}:{terminalCommandId:uuid(row.terminalCommandId)}) }; };
  const occurrences = list(value.occurrences).map(parseOccurrence);
  const unarchives=list(value.unarchives??undefined).map(item=>{const row=workspaceObject(item,[...baseKeys,'taskId','archivedRevision','commandId','unarchivedAtUtc']);return {...entity(row,revision),taskId:uuid(row.taskId),archivedRevision:integer(row.archivedRevision),commandId:uuid(row.commandId),unarchivedAtUtc:utc(row.unarchivedAtUtc)};});
  const occurrenceCorrections=list(value.occurrenceCorrections??undefined).map(item=>{const row=workspaceObject(item,[...baseKeys,'taskId','commandId','targetCommandId','previousOccurrence','correctedAtUtc']);return {...entity(row,revision),taskId:uuid(row.taskId),commandId:uuid(row.commandId),targetCommandId:uuid(row.targetCommandId),previousOccurrence:parseOccurrence(row.previousOccurrence),correctedAtUtc:utc(row.correctedAtUtc)};});
  const completions = list(value.completions).map(item => { const row = workspaceObject(item, [...baseKeys, "taskId", "commandId", "completedAtUtc", "occurrenceId"]); return { ...entity(row, revision), taskId: uuid(row.taskId), commandId: uuid(row.commandId), completedAtUtc: utc(row.completedAtUtc), occurrenceId: optionalId(row.occurrenceId) }; });
  const restorations=list(value.restorations??undefined).map(item=>{const row=workspaceObject(item,[...baseKeys,'taskId','completionId','commandId','restoredAtUtc']);return {...entity(row,revision),taskId:uuid(row.taskId),completionId:uuid(row.completionId),commandId:uuid(row.commandId),restoredAtUtc:utc(row.restoredAtUtc)};});
  const habitScores = list(value.habitScores).map(item => { const row = workspaceObject(item, [...baseKeys, "taskId", "commandId", "recordedAtUtc", "direction"]); return { ...entity(row, revision), taskId: uuid(row.taskId), commandId: uuid(row.commandId), recordedAtUtc: utc(row.recordedAtUtc), direction: choice(row.direction, ["up", "down"]) }; });
  const projections = list(value.projections).map(item => { const row = workspaceObject(item, [...baseKeys, "taskId", "state", "habiticaTaskId", "fingerprint", "correlationAlias"]); return { ...entity(row, revision), taskId: uuid(row.taskId), state: choice(row.state, ["unlinked", "pending", "linked", "externalConflict", "unknownOutcome"]), habiticaTaskId: optionalId(row.habiticaTaskId), fingerprint: row.fingerprint === undefined || row.fingerprint === null ? null : typeof row.fingerprint === "string" && /^[a-f0-9]{64}$/.test(row.fingerprint) ? row.fingerprint : fail(), correlationAlias: row.correlationAlias === undefined || row.correlationAlias === null ? null : typeof row.correlationAlias === "string" && /^companion_[a-f0-9]{32}_[a-f0-9]{32}$/.test(row.correlationAlias) ? row.correlationAlias : fail() }; });
  const views=value.savedTaskViews==null?[]:savedTaskViews(value.savedTaskViews,revision);
  const reviewEvents=list(value.reviewEvents??undefined).map(item=>{
   const row=workspaceObject(item,[...baseKeys,'taskId','commandId','action','recordedAtUtc','timeZoneId','reviewAfterUtc','staleSinceUtc']);
   return {...entity(row,revision),taskId:uuid(row.taskId),commandId:uuid(row.commandId),action:choice(row.action,['set','snooze','clear','markReviewed','setStale','clearStale'] as const),recordedAtUtc:utc(row.recordedAtUtc),timeZoneId:reviewZone(row.timeZoneId),reviewAfterUtc:reviewInstant(row.reviewAfterUtc),staleSinceUtc:reviewInstant(row.staleSinceUtc)};
  });
  const all = [...unarchives,...occurrenceCorrections,...reviewEvents,...views,...categories, ...labels, ...projects, ...sections, ...goals, ...tasks, ...occurrences, ...completions, ...restorations, ...habitScores, ...projections];
  if (all.length + (Array.isArray(value.organizationHistory)?value.organizationHistory.length:0) > 100000 || new Set([id, ...all.map(item => item.id)]).size !== all.length + 1) return fail();
  const categoryIds = new Set(categories.map(item => item.id)); const projectMap = new Map(projects.map(item => [item.id, item])); const sectionMap = new Map(sections.map(item => [item.id, item])); const taskMap = new Map(tasks.map(item => [item.id, item])); const labelIds = new Set(labels.map(item => item.id)); const occurrenceMap = new Map(occurrences.map(item => [item.id, item]));
  for(const view of views){const f=view.query.filter;if(f.categoryId&&!categoryIds.has(f.categoryId)||f.projectId&&!projectMap.has(f.projectId)||f.labelId&&!labelIds.has(f.labelId))return fail();}
  const aliases=projects.flatMap(row=>row.alias===undefined?[]:[row.alias]);if(new Set(aliases).size!==aliases.length)return fail();
  for (const project of projects) if (project.categoryId && !categoryIds.has(project.categoryId)) return fail();
  for (const section of sections) if (!projectMap.has(section.projectId)) return fail();
  for (const goal of goals) if (goal.taskIds.some(id => !taskMap.has(id))) return fail();
  for (const task of tasks) {
    if (task.categoryId && !categoryIds.has(task.categoryId) || task.projectId && !projectMap.has(task.projectId) || task.sectionId && sectionMap.get(task.sectionId)?.projectId !== task.projectId
      || task.labelIds.some(id => !labelIds.has(id)) || task.dependsOn.some(id => id === task.id || !taskMap.has(id))
      || task.parentTaskId && (task.parentTaskId === task.id || task.kind !== "todo" || taskMap.get(task.parentTaskId)?.kind !== "todo")) return fail();
  }
  const edges = new Map(tasks.map(task => [task.id, Array.from(new Set([...task.dependsOn, ...(task.parentTaskId ? [task.parentTaskId] : [])]))]));
  const degree = new Map(Array.from(edges, ([id, values]) => [id, values.length])); const children = new Map(tasks.map(task => [task.id, [] as string[]]));
  for (const [id, values] of edges) for (const value of values) children.get(value)!.push(id);
  const queue = Array.from(degree).filter(([, count]) => count === 0).map(([id]) => id); let visited = 0;
  for (let index = 0; index < queue.length; index++) { visited++; for (const child of children.get(queue[index])!) { const count = degree.get(child)! - 1; degree.set(child, count); if (count === 0) queue.push(child); } }
  if (visited !== tasks.length) return fail();
  const occurrenceKeys = new Set<string>(); const completionKeys = new Set<string>();
  for (const occurrence of occurrences) { const key = occurrence.taskId + ":" + occurrence.date; if (taskMap.get(occurrence.taskId)?.kind !== "daily" || occurrenceKeys.has(key)||occurrence.state==='pending'&&occurrence.terminalCommandId!==undefined) return fail(); occurrenceKeys.add(key); }
  for (const completion of completions) {
    const task = taskMap.get(completion.taskId); const key = completion.taskId + ":" + completion.occurrenceId;
    if (!task || task.kind === "habit" || task.kind === "daily" && !completionCorrected(occurrenceCorrections,completion) && completionKeys.has(key) || task.kind === "todo" && completion.occurrenceId !== null
      || task.kind === "daily" && (!completion.occurrenceId || occurrenceMap.get(completion.occurrenceId)?.taskId !== task.id || !completionCorrected(occurrenceCorrections,completion) && (occurrenceMap.get(completion.occurrenceId)?.state !== "completed" || occurrenceCorrections.some(row=>row.previousOccurrence.id===completion.occurrenceId)&&occurrenceMap.get(completion.occurrenceId)?.revision!==completion.revision))) return fail();
    if(task.kind==='daily'&&!completionCorrected(occurrenceCorrections,completion))completionKeys.add(key);
  }
  const activities=[...completions,...habitScores,...restorations],activityCommands=new Set(activities.map(row=>row.commandId)),activityRevisions=new Set(activities.map(row=>row.revision));
  const reviewCommands=new Set<string>(),reviewRevisions=new Set<number>(),latestReviews=new Map<string,ReviewEvent>();
  for(const review of reviewEvents){
   if(!taskMap.has(review.taskId)||reviewCommands.has(review.commandId)||activityCommands.has(review.commandId)||reviewRevisions.has(review.revision)||activityRevisions.has(review.revision)
    ||['clear','markReviewed'].includes(review.action)&&review.reviewAfterUtc!==null||review.action==='clearStale'&&review.staleSinceUtc!==null
    ||['set','snooze'].includes(review.action)&&review.reviewAfterUtc===null||review.action==='setStale'&&(review.staleSinceUtc===null||utcTicks(review.staleSinceUtc)>utcTicks(review.recordedAtUtc)))return fail();
   reviewCommands.add(review.commandId);reviewRevisions.add(review.revision);const old=latestReviews.get(review.taskId);if(!old||old.revision<review.revision)latestReviews.set(review.taskId,review);
  }
  for(const [taskId,review] of latestReviews){const task=taskMap.get(taskId)!;if(task.revision<review.revision||(task.reviewAfterUtc===null)!==(review.reviewAfterUtc===null)||(task.staleSinceUtc===null)!==(review.staleSinceUtc===null)||task.reviewAfterUtc!==null&&utcTicks(task.reviewAfterUtc)!==utcTicks(review.reviewAfterUtc)||task.staleSinceUtc!==null&&utcTicks(task.staleSinceUtc)!==utcTicks(review.staleSinceUtc))return fail();}
  const completionMap=new Map(completions.map(value=>[value.id,value])),restoredIds=new Set<string>(),restoreCommands=new Set<string>(),completionCommands=new Set(completions.map(value=>value.commandId));
  for(const restoration of restorations){const completion=completionMap.get(restoration.completionId);if(taskMap.get(restoration.taskId)?.kind!=='todo'||!completion||completion.taskId!==restoration.taskId||completion.occurrenceId!==null||completionCommands.has(restoration.commandId)||restoreCommands.has(restoration.commandId)||restoration.revision<=completion.revision||restoredIds.has(restoration.completionId))return fail();restoredIds.add(restoration.completionId);restoreCommands.add(restoration.commandId);}
  const restored=new Map(restorations.map(value=>[value.completionId,value])),restoredTasks=new Set(restorations.map(value=>value.taskId)),groups=new Map<string,Completion[]>();
  for(const completion of completions)if(taskMap.get(completion.taskId)?.kind==='todo'){let group=groups.get(completion.taskId);if(!group){group=[];groups.set(completion.taskId,group);}group.push(completion);}
  for(const [taskId,group] of groups){group.sort((a,b)=>a.revision-b.revision);for(let index=1;index<group.length;index++){const restoration=restored.get(group[index-1].id);if(!restoration||restoration.revision>=group[index].revision)return fail();}if(restoredTasks.has(taskId)&&(taskMap.get(taskId)!.state==='completed')!==!restoredIds.has(group[group.length-1].id))return fail();}
  const completedTodos=new Set(completions.filter(value=>value.occurrenceId===null&&!restoredIds.has(value.id)).map(value=>value.taskId));
  for (const task of tasks) if (task.kind === "todo" && task.state === "completed" && !completedTodos.has(task.id)) return fail();
  const completedOccurrences = new Set(completions.filter(item=>!completionCorrected(occurrenceCorrections,item)).map(item => item.occurrenceId)); for (const occurrence of occurrences) if (occurrence.state === "completed" && !completedOccurrences.has(occurrence.id)) return fail();
  for (const score of habitScores) if (taskMap.get(score.taskId)?.kind !== "habit") return fail();
  const mapped = new Set<string>(); const external = new Set<string>();
  for (const projection of projections) {
    if (projection.correlationAlias && projection.correlationAlias !== `companion_${id.replaceAll("-", "")}_${projection.taskId.replaceAll("-", "")}` || !taskMap.has(projection.taskId) || mapped.has(projection.taskId) || projection.state === "linked" && !projection.habiticaTaskId || projection.habiticaTaskId && external.has(projection.habiticaTaskId)) return fail();
    mapped.add(projection.taskId); if (projection.habiticaTaskId) external.add(projection.habiticaTaskId);
  }
  const document:WorkspaceDocument={ ...(value.unarchives==null?{}:{unarchives}),...(value.occurrenceCorrections==null?{}:{occurrenceCorrections}),schemaVersion: 1, id, revision, categories, projects, sections, labels, goals, tasks, occurrences, completions, ...(value.restorations==null?{}:{restorations}), ...(value.reviewEvents==null?{}:{reviewEvents}), habitScores, projections };
  const coverage=activityCoverage(value.activityCoverage,document,recurrence,utc);if(coverage)document.activityCoverage=coverage;
  if(value.savedTaskViews!=null)document.savedTaskViews=views;
  if(value.organizationHistory!==undefined&&value.organizationHistory!==null){try{document.organizationHistory=organizationHistory(value.organizationHistory,document,utc);}catch{return fail();}}
  if(document.projects.some(project=>project.isRemoved&&!document.organizationHistory?.some(entry=>entry.command.operation==='remove'&&entry.command.entityId===project.id&&entry.snapshot.isRemoved)))return fail();
  const ledger=tagLedger(value.tagLedger,document,utc);if(ledger){document.tagLedger=ledger;const entities=[...ledger.mappings,...ledger.intents];const identities=new Set([id,...all.map(row=>row.id)]);for(const row of entities){if(identities.has(row.id))return fail();identities.add(row.id);}if(all.length+entities.length+(document.organizationHistory?.length??0)>100000)return fail();}
  const conversions=checklistConversions(value.checklistConversions,document,workspaceTask,utc);
  if(conversions){document.checklistConversions=conversions;const identities=new Set([id,...all.map(row=>row.id),...(ledger?.mappings??[]).map(row=>row.id),...(ledger?.intents??[]).map(row=>row.id)]);for(const row of conversions){if(identities.has(row.id))return fail();identities.add(row.id);}if(identities.size-1+(document.organizationHistory?.length??0)+conversions.reduce((n,row)=>n+row.receipts.length,0)>100000)return fail();}
  const retirements=sourceRetirements(value.sourceRetirements,document,utc,utcTicks);
  if(retirements){document.sourceRetirements=retirements;const identities=new Set([id,...all.map(row=>row.id),...(ledger?.mappings??[]).map(row=>row.id),...(ledger?.intents??[]).map(row=>row.id),...(conversions??[]).map(row=>row.id)]);for(const row of retirements){if(identities.has(row.id))return fail();identities.add(row.id);}if(identities.size-1+(document.organizationHistory?.length??0)+(conversions??[]).reduce((n,row)=>n+row.receipts.length,0)>100000)return fail();}
  const recoveries=linkedRecoveryRows(value.linkedTaskRecoveries,document,utc);
  if(recoveries){document.linkedTaskRecoveries=recoveries;const identities=new Set([id,...all.map(row=>row.id),...(ledger?.mappings??[]).map(row=>row.id),...(ledger?.intents??[]).map(row=>row.id),...(conversions??[]).map(row=>row.id),...(retirements??[]).map(row=>row.id)]);for(const row of recoveries){if(identities.has(row.id))return fail();identities.add(row.id);}if(identities.size-1+(document.organizationHistory?.length??0)+(conversions??[]).reduce((n,row)=>n+row.receipts.length,0)>100000)return fail();}
  if(!recoveryHistoryValid(document))return fail();
  return document;
}
export function emptyWorkspace(id = crypto.randomUUID()): WorkspaceDocument { return workspaceDocument({ schemaVersion: 1, id, revision: 1 }); }
