import type { CompanionTask, WorkspaceDocument } from './workspace-domain.ts';
import { WorkspaceError, workspaceObject, uuid } from './workspace-http.ts';

export interface TaskFilter {
 query?:string; kind?:CompanionTask['kind']; state?:CompanionTask['state']; priority?:CompanionTask['priority'];
 projectId?:string; categoryId?:string; labelId?:string; plannedFrom?:string; plannedTo?:string;
 deadlineFrom?:string; deadlineTo?:string; projectionState?:'unlinked'|'pending'|'linked'|'externalConflict'|'unknownOutcome';
 limit?:number; offset?:number; expectedRevision?:number;
}
export const taskFilterKeys=['query','kind','state','priority','projectId','categoryId','labelId','plannedFrom','plannedTo','deadlineFrom','deadlineTo','projectionState','limit','offset','expectedRevision'];
const invalid=():never=>{throw new WorkspaceError('invalid_filter',422);};
export function taskFilter(input:unknown):TaskFilter {
 const value={...workspaceObject(input,taskFilterKeys)};
 if(value.query!==undefined&&(typeof value.query!=='string'||value.query.length>256))invalid();
 for(const [key,choices] of Object.entries({kind:['todo','daily','habit'],state:['active','completed','waiting','someday','paused','cancelled'],priority:['low','normal','high','urgent'],projectionState:['unlinked','pending','linked','externalConflict','unknownOutcome']}))
  if(value[key]!==undefined&&(typeof value[key]!=='string'||!choices.includes(value[key] as string)))invalid();
 for(const key of ['projectId','categoryId','labelId'])if(value[key]!==undefined)value[key]=uuid(value[key]);
 for(const key of ['plannedFrom','plannedTo','deadlineFrom','deadlineTo'])if(value[key]!==undefined){const date=value[key];if(typeof date!=='string'||!/^\d{4}-\d{2}-\d{2}$/.test(date)||date.startsWith('0000')||date==='0001-01-01'||!Number.isFinite(Date.parse(date))||new Date(date).toISOString().slice(0,10)!==date)invalid();}
 for(const [key,min,max] of [['limit',1,100],['offset',0,100000],['expectedRevision',1,Number.MAX_SAFE_INTEGER]] as const)
  if(value[key]!==undefined&&(!Number.isSafeInteger(value[key])||Number(value[key])<min||Number(value[key])>max))invalid();
 if(Number(value.offset??0)>0&&value.expectedRevision===undefined)invalid();
 for(const [from,to] of [['plannedFrom','plannedTo'],['deadlineFrom','deadlineTo']])if(value[from]!==undefined&&value[to]!==undefined&&String(value[from])>String(value[to]))invalid();
 return value as TaskFilter;
}
const fold=(value:string)=>value.normalize('NFKC').replaceAll('\u0130','i\u0307').toLowerCase().replaceAll('\u03c2','\u03c3');
export {fold as foldTaskText};
const within=(value:string|null,from?:string,to?:string)=>from===undefined&&to===undefined||value!==null&&(from===undefined||value>=from)&&(to===undefined||value<=to);
export function queryTasks(document:WorkspaceDocument,input:unknown){
 const filter=taskFilter(input);
 if(filter.expectedRevision!==undefined&&filter.expectedRevision!==document.revision)throw new WorkspaceError('stale_revision',409);
 const projects=new Map(document.projects.map(project=>[project.id,project]));
 const mappings=new Map(document.projections.filter(mapping=>!mapping.isArchived).map(mapping=>[mapping.taskId,mapping.state]));
 const query=fold(filter.query??'');const offset=filter.offset??0,limit=filter.limit??50;
 const tasks:CompanionTask[]=[];let matchedCount=0;
 for(const task of document.tasks){
  if(task.isArchived||filter.kind!==undefined&&task.kind!==filter.kind||filter.state!==undefined&&task.state!==filter.state
   ||filter.priority!==undefined&&task.priority!==filter.priority||filter.projectId!==undefined&&task.projectId!==filter.projectId
   ||filter.labelId!==undefined&&!task.labelIds.includes(filter.labelId)
   ||!within(task.plannedDate,filter.plannedFrom,filter.plannedTo)||!within(task.deadline,filter.deadlineFrom,filter.deadlineTo))continue;
  const category=task.categoryId??(task.projectId?projects.get(task.projectId)?.categoryId:null);
  if(filter.categoryId!==undefined&&category!==filter.categoryId||filter.projectionState!==undefined&&(mappings.get(task.id)??'unlinked')!==filter.projectionState
   ||query&&!fold(task.text).includes(query)&&!fold(task.notes??'').includes(query))continue;
  if(matchedCount++>=offset&&tasks.length<limit)tasks.push(task);
 }
 const next=offset+tasks.length;
 return {provenance:'companion-canonical',workspaceId:document.id,revision:document.revision,tasks,matchedCount,truncated:next<matchedCount,offset,nextOffset:next<matchedCount?next:null};
}
