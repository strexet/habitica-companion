import {workspaceObject,uuid,WorkspaceError} from './workspace-http.ts';
import {hasGameFence,unarchiveReferencesAvailable} from './task-recovery.ts';
import type {WorkspaceDocument} from './workspace-domain.ts';
import type {CommandEntry} from '../companion-workspace/src/commands.ts';
export function recoveryInput(input:unknown){
 const row=workspaceObject(input,['taskId','limit','offset','expectedRevision']),taskId=uuid(row.taskId);
 const limit=row.limit??50,offset=row.offset??0,revision=row.expectedRevision;
 if(!Number.isInteger(limit)||Number(limit)<1||Number(limit)>100||!Number.isInteger(offset)||Number(offset)<0||Number(offset)>100000
  ||revision!==undefined&&(!Number.isSafeInteger(revision)||Number(revision)<1)||Number(offset)>0&&revision===undefined)throw new WorkspaceError('invalid_filter',422);
 return {taskId,limit:Number(limit),offset:Number(offset),expectedRevision:revision as number|undefined};
}
export function taskRecoveryQuery(document:WorkspaceDocument,commands:CommandEntry[],input:unknown){
 const filter=recoveryInput(input);if(filter.expectedRevision!==undefined&&filter.expectedRevision!==document.revision)throw new WorkspaceError('stale_revision',409);
 const task=document.tasks.find(row=>row.id===filter.taskId)??null;
 const gameFence=hasGameFence(document,filter.taskId)||commands.some(entry=>entry.effects.length>0&&!['confirmed','cancelled'].includes(entry.projectionState));
 const all=document.occurrences.filter(row=>row.taskId===filter.taskId).sort((a,b)=>b.date.localeCompare(a.date)||a.id.localeCompare(b.id));
 const items=all.slice(filter.offset,filter.offset+filter.limit).map(occurrence=>{
  const sources=commands.filter(row=>row.command.taskId===filter.taskId&&row.command.expectedRevision+1===occurrence.revision&&row.command.occurrenceDate===occurrence.date&&row.command.operation===(occurrence.state==='completed'?'completeTask':'skipOccurrence'));
  const source=sources.length===1?sources[0]:null;
  const reason=!task||task.isArchived?'task_unavailable':gameFence?'unsupported_projection':task.kind!=='daily'||occurrence.isArchived||occurrence.state==='pending'?'occurrence_unavailable':!source||source.effects.length>0?'occurrence_source_unavailable':null;
  return {occurrence,targetCommandId:source?.command.id??null,canCorrect:reason===null,reason};
 });
 const next=filter.offset+items.length;
 return {provenance:'companion-canonical',workspaceId:document.id,revision:document.revision,task,gameFence,
  canUnarchive:Boolean(task?.isArchived&&!gameFence&&unarchiveReferencesAvailable(document,task,{archivedRevision:task.revision,planning:null,relationships:null})),
  unarchiveReason:!task||!task.isArchived?'task_not_archived':gameFence?'unsupported_projection':!unarchiveReferencesAvailable(document,task,{archivedRevision:task.revision,planning:null,relationships:null})?'unarchive_repair_required':null,
  items,total:all.length,offset:filter.offset,hasMore:next<all.length,nextOffset:next<all.length?next:null};
}
