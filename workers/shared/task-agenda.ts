import type { WorkspaceDocument } from './workspace-domain.ts';
import { expandRecurrence } from './recurrence-calendar.ts';
import { WorkspaceError, workspaceObject } from './workspace-http.ts';
export const agendaKeys=['from','through','limit','offset','expectedRevision'];
export interface AgendaFilter {from:string;through:string;limit?:number;offset?:number;expectedRevision?:number}
export interface AgendaItem {taskId:string;text:string;kind:string;taskState:string;date:string;planned:boolean;deadline:boolean;occurrenceState:string|null;occurrenceId:string|null;ruleTimeZoneId:string|null}
const invalid=():never=>{throw new WorkspaceError('invalid_filter',422);};
export function agendaFilter(input:unknown):AgendaFilter {
 const value=workspaceObject(input,agendaKeys);
 for(const key of ['from','through']){const date=value[key];if(typeof date!=='string'||!/^\d{4}-\d{2}-\d{2}$/.test(date)||date.startsWith('0000')||date==='0001-01-01'||!Number.isFinite(Date.parse(date))||new Date(date).toISOString().slice(0,10)!==date)invalid();}
 const days=(Date.parse(String(value.through))-Date.parse(String(value.from)))/86400000+1;
 if(days<1||days>366)invalid();
 for(const [key,min,max] of [['limit',1,100],['offset',0,10000],['expectedRevision',1,Number.MAX_SAFE_INTEGER]] as const)
  if(value[key]!==undefined&&(!Number.isSafeInteger(value[key])||Number(value[key])<min||Number(value[key])>max))invalid();
 if(Number(value.offset??0)>0&&value.expectedRevision===undefined)invalid();
 return {from:String(value.from),through:String(value.through),limit:value.limit as number|undefined,offset:value.offset as number|undefined,expectedRevision:value.expectedRevision as number|undefined};
}
export function queryAgenda(document:WorkspaceDocument,input:unknown){
 const filter=agendaFilter(input),offset=filter.offset??0,limit=filter.limit??50;
 if(filter.expectedRevision!==undefined&&filter.expectedRevision!==document.revision)throw new WorkspaceError('stale_revision',409);
 const activeRules=document.tasks.filter(task=>!task.isArchived&&task.kind==='daily'&&task.state==='active'&&task.recurrence).length;
 const days=(Date.parse(filter.through)-Date.parse(filter.from))/86400000+1;
 if(activeRules*days>250000)throw new WorkspaceError('agenda_capacity',422);
 const occurrences=new Map<string,WorkspaceDocument['occurrences']>();
 for(const occurrence of document.occurrences)if(occurrence.date>=filter.from&&occurrence.date<=filter.through){let list=occurrences.get(occurrence.taskId);if(!list){list=[];occurrences.set(occurrence.taskId,list);}list.push(occurrence);}
 const rows:{order:number;item:AgendaItem}[]=[];
 for(let order=0;order<document.tasks.length;order++){
  const task=document.tasks[order];if(task.isArchived||['completed','cancelled'].includes(task.state))continue;
  const dates=new Map<string,AgendaItem>();
  const archivedDates=new Set((occurrences.get(task.id)??[]).filter(occurrence=>occurrence.isArchived).map(occurrence=>occurrence.date));
  const item=(date:string):AgendaItem=>dates.get(date)??{taskId:task.id,text:task.text,kind:task.kind,taskState:task.state,date,planned:false,deadline:false,occurrenceState:null,occurrenceId:null,ruleTimeZoneId:task.recurrence?.timeZoneId??null};
  const explicit=(date:string|null,planned:boolean)=>{if(date===null||date<filter.from||date>filter.through)return;const value=item(date);dates.set(date,{...value,planned:value.planned||planned,deadline:value.deadline||!planned});};
  explicit(task.plannedDate,true);explicit(task.deadline,false);
  if(task.kind==='daily'&&task.state==='active'&&task.recurrence){const expansion=expandRecurrence(task.recurrence,filter.from,filter.through);if(expansion.failure)throw new WorkspaceError('recurrence_unavailable',422);for(const date of expansion.dates)if(!archivedDates.has(date))dates.set(date,{...item(date),occurrenceState:'pending'});}
  for(const occurrence of occurrences.get(task.id)??[]){if(occurrence.isArchived||occurrence.state==='pending'&&!dates.get(occurrence.date)?.occurrenceState)continue;dates.set(occurrence.date,{...item(occurrence.date),occurrenceState:occurrence.state,occurrenceId:occurrence.id});}
  if(rows.length+dates.size>10000)throw new WorkspaceError('agenda_capacity',422);
  for(const value of dates.values())rows.push({order,item:value});
 }
 rows.sort((a,b)=>a.item.date<b.item.date?-1:a.item.date>b.item.date?1:a.order-b.order);
 const items=rows.slice(offset,offset+limit).map(row=>row.item),next=offset+items.length;
 return {provenance:'companion-canonical',workspaceId:document.id,revision:document.revision,items,matchedCount:rows.length,truncated:next<rows.length,offset,nextOffset:next<rows.length?next:null};
}
