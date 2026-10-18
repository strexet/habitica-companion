import {utcTicks,type CompanionTask,type WorkspaceDocument} from './workspace-domain.ts';
import {taskFilter,foldTaskText} from './task-query.ts';
import {WorkspaceError,workspaceObject,uuid} from './workspace-http.ts';
export const historyKeys=['query','taskId','kind','completedFromUtc','completedThroughUtc','limit','offset','expectedRevision'];
export interface HistoryCommand {commandId:string;taskId:string;operation:string;snapshot:CompanionTask|null}
export function historyFilter(input:unknown){
 const value=workspaceObject(input,historyKeys),{completedFromUtc,completedThroughUtc,taskId,...common}=value;
 const filter=taskFilter(common);const selectedTask=taskId===undefined?undefined:uuid(taskId);
 if(filter.kind==='habit')throw new WorkspaceError('invalid_filter',422);
 let from:bigint|null=null,through:bigint|null=null;
 try{if(completedFromUtc!==undefined){from=utcTicks(completedFromUtc);if(from===utcTicks('0001-01-01T00:00:00Z'))throw new Error();}if(completedThroughUtc!==undefined){through=utcTicks(completedThroughUtc);if(through===utcTicks('0001-01-01T00:00:00Z'))throw new Error();}}catch{throw new WorkspaceError('invalid_filter',422);}
 if(from!==null&&through!==null&&from>through)throw new WorkspaceError('invalid_filter',422);
 return {query:filter.query,taskId:selectedTask,kind:filter.kind,from,through,limit:filter.limit??50,offset:filter.offset??0,expectedRevision:filter.expectedRevision};
}
export function queryCompletionHistory(document:WorkspaceDocument,commands:HistoryCommand[],input:unknown){
 const filter=historyFilter(input);if(filter.expectedRevision!==undefined&&filter.expectedRevision!==document.revision)throw new WorkspaceError('stale_revision',409);
 const tasks=new Map(document.tasks.map(task=>[task.id,task])),occurrences=new Map(document.occurrences.map(value=>[value.id,value]));
 const snapshots=new Map(commands.map(command=>[command.commandId,command]));const query=foldTaskText(filter.query??'');
 const rows=[];
 for(let order=0;order<document.completions.length;order++){
  const event=document.completions[order],task=tasks.get(event.taskId)!;const at=utcTicks(event.completedAtUtc);
  if(filter.taskId!==undefined&&event.taskId!==filter.taskId||filter.kind!==undefined&&task.kind!==filter.kind||filter.from!==null&&at<filter.from||filter.through!==null&&at>filter.through)continue;
  const command=snapshots.get(event.commandId),accepted=command?.snapshot;
  if(command&&(command.operation!=='completeTask'||command.taskId!==task.id)||accepted&&(accepted.id!==task.id||accepted.kind!==task.kind||typeof accepted.text!=='string'||/^\p{White_Space}*$/u.test(accepted.text)||accepted.text.length>2048||accepted.notes!=null&&(typeof accepted.notes!=='string'||accepted.notes.length>10000)))throw new WorkspaceError('history_unavailable',503);
  const taskText=accepted?.text??task.text,notes=accepted?accepted.notes??null:task.notes;
  if(query&&!foldTaskText(taskText).includes(query)&&!foldTaskText(notes??'').includes(query))continue;
  rows.push({at,revision:event.revision,order,item:{eventId:event.id,taskId:task.id,commandId:event.commandId,kind:task.kind,taskText,notes,detailSource:accepted?'commandSnapshot':'currentTask',completedAtUtc:event.completedAtUtc,occurrenceId:event.occurrenceId,occurrenceDate:event.occurrenceId?occurrences.get(event.occurrenceId)!.date:null,taskArchived:task.isArchived,eventArchived:event.isArchived}});
 }
 rows.sort((a,b)=>a.at>b.at?-1:a.at<b.at?1:b.revision-a.revision||a.order-b.order);
 const items=rows.slice(filter.offset,filter.offset+filter.limit).map(row=>row.item),next=filter.offset+items.length;
 return {provenance:'companion-canonical',workspaceId:document.id,revision:document.revision,items,matchedCount:rows.length,truncated:next<rows.length,offset:filter.offset,nextOffset:next<rows.length?next:null};
}
