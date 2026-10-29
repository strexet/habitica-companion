import {workspaceObject,uuid} from './workspace-http.ts';
import {taskPlanning,applyPlanning,type TaskPlanning} from './task-planning.ts';
import {taskRelationships,applyRelationships,relationshipReferencesAvailable,type TaskRelationships} from './task-relationships.ts';
import type {WorkspaceDocument,CompanionTask,Occurrence,Entity,Completion} from './workspace-domain.ts';
export interface TaskUnarchive {archivedRevision:number;planning:TaskPlanning|null;relationships:TaskRelationships|null}
export interface OccurrenceCorrectionPatch {occurrenceId:string;occurrenceRevision:number;targetCommandId:string}
export interface UnarchiveEvent extends Entity {taskId:string;archivedRevision:number;commandId:string;unarchivedAtUtc:string}
export interface OccurrenceCorrection extends Entity {taskId:string;commandId:string;targetCommandId:string;previousOccurrence:Occurrence;correctedAtUtc:string}
export function recoveryRevision(value:unknown):number {if(!Number.isSafeInteger(value)||Number(value)<1)throw Error('invalid_command');return Number(value);}
export function taskUnarchive(input:unknown):TaskUnarchive|null {
 if(input==null)return null;const row=workspaceObject(input,['archivedRevision','planning','relationships']);
 const planning=taskPlanning(row.planning);if(planning&&(planning.plannedDate!==null||planning.clearPlannedDate))throw Error('invalid_command');
 return {archivedRevision:recoveryRevision(row.archivedRevision),planning,relationships:taskRelationships(row.relationships)};
}
export function occurrenceCorrection(input:unknown):OccurrenceCorrectionPatch|null {
 if(input==null)return null;const row=workspaceObject(input,['occurrenceId','occurrenceRevision','targetCommandId']);
 return {occurrenceId:uuid(row.occurrenceId),occurrenceRevision:recoveryRevision(row.occurrenceRevision),targetCommandId:uuid(row.targetCommandId)};
}
export function applyUnarchive(task:CompanionTask,patch:TaskUnarchive):CompanionTask {
 const planned=patch.planning?applyPlanning(task,patch.planning):task;
 return {...(patch.relationships?applyRelationships(planned,patch.relationships):planned),isArchived:false};
}
export function unarchiveReferencesAvailable(document:WorkspaceDocument,task:CompanionTask,patch:TaskUnarchive):boolean {
 const restored=applyUnarchive(task,patch);const has=(id:string|null,rows:Entity[])=>id===null||rows.some(row=>row.id===id&&!row.isArchived);
 return (!patch.relationships||relationshipReferencesAvailable(document,task,patch.relationships))
  &&has(restored.categoryId,document.categories)&&has(restored.projectId,document.projects)&&has(restored.sectionId,document.sections)
  &&(restored.sectionId===null||document.sections.some(row=>row.id===restored.sectionId&&row.projectId===restored.projectId))
  &&restored.labelIds.every(id=>has(id,document.labels))&&has(restored.parentTaskId,document.tasks)&&restored.dependsOn.every(id=>has(id,document.tasks));
}
export const hasGameFence=(document:WorkspaceDocument,taskId:string)=>document.projections.some(row=>row.taskId===taskId&&(row.state!=='unlinked'||row.habiticaTaskId!==null));
export const completionCorrected=(rows:OccurrenceCorrection[],completion:Completion)=>rows.some(row=>row.targetCommandId===completion.commandId&&row.previousOccurrence.id===completion.occurrenceId&&row.previousOccurrence.revision===completion.revision);
export function recoveryHistoryValid(document:WorkspaceDocument):boolean {
 const activities=[...document.completions,...document.habitScores,...(document.restorations??[]),...(document.reviewEvents??[])];
 const commands=new Set(activities.map(row=>row.commandId)),revisions=new Set(activities.map(row=>row.revision)),targets=new Set<string>(),archives=new Set<string>();
 for(const row of document.unarchives??[]){if(row.isArchived||commands.has(row.commandId)||revisions.has(row.revision)||archives.has(row.taskId+':'+row.archivedRevision)||row.archivedRevision>=row.revision||!document.tasks.some(task=>task.id===row.taskId&&task.revision>=row.revision))return false;commands.add(row.commandId);revisions.add(row.revision);archives.add(row.taskId+':'+row.archivedRevision);}
 for(const row of document.occurrenceCorrections??[]){
  const previous=row.previousOccurrence,current=document.occurrences.find(value=>value.id===previous.id);
  if(row.isArchived||previous.isArchived||previous.terminalCommandId!==undefined&&previous.terminalCommandId!==row.targetCommandId||previous.taskId!==row.taskId||!['completed','skipped'].includes(previous.state)||previous.revision>=row.revision
   ||row.commandId===row.targetCommandId||commands.has(row.commandId)||targets.has(row.targetCommandId)||revisions.has(row.revision)
   ||!current||current.taskId!==row.taskId||current.date!==previous.date||current.revision<row.revision
   ||!document.tasks.some(task=>task.id===row.taskId&&task.kind==='daily'&&task.revision>=row.revision))return false;
  if(previous.state==='completed'?!document.completions.some(value=>value.taskId===row.taskId&&value.occurrenceId===previous.id&&value.revision===previous.revision&&value.commandId===row.targetCommandId):document.completions.some(value=>value.commandId===row.targetCommandId))return false;
  commands.add(row.commandId);targets.add(row.targetCommandId);revisions.add(row.revision);
 }
 const groups=new Map<string,OccurrenceCorrection[]>();for(const row of document.occurrenceCorrections??[]){const group=groups.get(row.previousOccurrence.id)??[];group.push(row);groups.set(row.previousOccurrence.id,group);}
 for(const [id,group] of groups){group.sort((a,b)=>a.revision-b.revision);for(let i=1;i<group.length;i++)if(group[i].previousOccurrence.revision<=group[i-1].revision)return false;
  const current=document.occurrences.find(row=>row.id===id)!;if((current.state==='pending')!==(current.revision===group[group.length-1].revision))return false;}
 return true;
}
