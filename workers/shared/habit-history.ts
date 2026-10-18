import {utcTicks,type CompanionTask,type WorkspaceDocument} from './workspace-domain.ts';
import {historyFilter} from './completion-history.ts';
import {foldTaskText} from './task-query.ts';
import {WorkspaceError,workspaceObject} from './workspace-http.ts';
export const habitHistoryKeys=['query','taskId','direction','recordedFromUtc','recordedThroughUtc','limit','offset','expectedRevision'];
export interface HabitHistoryCommand {commandId:string;taskId:string;operation:string;direction:string|null;snapshot:CompanionTask|null}
export function habitHistoryFilter(input:unknown){
 const value=workspaceObject(input,habitHistoryKeys),{recordedFromUtc,recordedThroughUtc,direction,...common}=value;
 if(direction!==undefined&&direction!=='up'&&direction!=='down')throw new WorkspaceError('invalid_filter',422);
 const filter=historyFilter({...common,...(recordedFromUtc!==undefined?{completedFromUtc:recordedFromUtc}:{}),...(recordedThroughUtc!==undefined?{completedThroughUtc:recordedThroughUtc}:{})});
 return {...filter,direction};
}
export function queryHabitHistory(document:WorkspaceDocument,commands:HabitHistoryCommand[],input:unknown){
 const filter=habitHistoryFilter(input);if(filter.expectedRevision!==undefined&&filter.expectedRevision!==document.revision)throw new WorkspaceError('stale_revision',409);
 const tasks=new Map(document.tasks.map(task=>[task.id,task])),snapshots=new Map(commands.map(command=>[command.commandId,command])),query=foldTaskText(filter.query??''),rows=[];
 for(let order=0;order<document.habitScores.length;order++){
  const event=document.habitScores[order],task=tasks.get(event.taskId)!,at=utcTicks(event.recordedAtUtc);
  if(filter.taskId!==undefined&&event.taskId!==filter.taskId||filter.direction!==undefined&&event.direction!==filter.direction||filter.from!==null&&at<filter.from||filter.through!==null&&at>filter.through)continue;
  const command=snapshots.get(event.commandId),accepted=command?.snapshot;
  if(command&&(command.operation!=='scoreHabit'||command.taskId!==task.id||command.direction!==event.direction)||accepted&&(accepted.id!==task.id||accepted.kind!=='habit'||typeof accepted.text!=='string'||/^\p{White_Space}*$/u.test(accepted.text)||accepted.text.length>2048||accepted.notes!=null&&(typeof accepted.notes!=='string'||accepted.notes.length>10000)))throw new WorkspaceError('history_unavailable',503);
  const taskText=accepted?.text??task.text,notes=accepted?accepted.notes??null:task.notes;
  if(query&&!foldTaskText(taskText).includes(query)&&!foldTaskText(notes??'').includes(query))continue;
  rows.push({at,revision:event.revision,order,item:{eventId:event.id,taskId:task.id,commandId:event.commandId,direction:event.direction,taskText,notes,detailSource:accepted?'commandSnapshot':'currentTask',recordedAtUtc:event.recordedAtUtc,taskArchived:task.isArchived,eventArchived:event.isArchived}});
 }
 rows.sort((a,b)=>a.at>b.at?-1:a.at<b.at?1:b.revision-a.revision||a.order-b.order);
 const items=rows.slice(filter.offset,filter.offset+filter.limit).map(row=>row.item),next=filter.offset+items.length;
 return {provenance:'companion-canonical',workspaceId:document.id,revision:document.revision,items,matchedCount:rows.length,truncated:next<rows.length,offset:filter.offset,nextOffset:next<rows.length?next:null};
}
