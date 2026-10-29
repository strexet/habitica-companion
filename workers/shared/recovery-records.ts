import {workspaceObject,WorkspaceError} from './workspace-http.ts';
import {utcTicks,type CompanionTask,type Occurrence} from './workspace-domain.ts';
import {foldTaskText} from './task-query.ts';
import {hasGameFence,unarchiveReferencesAvailable} from './task-recovery.ts';
import type {Journal} from '../companion-workspace/src/journal.ts';
export function recoveryRecordsInput(input:unknown){
 const row=workspaceObject(input,['recordType','query','limit','offset','expectedRevision']);
 if(!['archivedTasks','terminalOccurrences'].includes(String(row.recordType))||row.query!==undefined&&(typeof row.query!=='string'||row.query.length>256))throw new WorkspaceError('invalid_filter',422);
 const limit=row.limit??20,offset=row.offset??0,revision=row.expectedRevision;
 if(!Number.isSafeInteger(limit)||Number(limit)<1||Number(limit)>100||!Number.isSafeInteger(offset)||Number(offset)<0||Number(offset)>100000||revision!==undefined&&(!Number.isSafeInteger(revision)||Number(revision)<1)||Number(offset)>0&&revision===undefined)throw new WorkspaceError('invalid_filter',422);
 return {recordType:row.recordType as 'archivedTasks'|'terminalOccurrences',...(row.query===undefined?{}:{query:row.query as string}),limit:Number(limit),offset:Number(offset),...(revision===undefined?{}:{expectedRevision:Number(revision)})};
}
export function queryRecoveryRecords(state:Journal,input:unknown,effectsFenced=false){
 const filter=recoveryRecordsInput(input),document=state.document;
 if(filter.expectedRevision!==undefined&&filter.expectedRevision!==document.revision)throw new WorkspaceError('stale_revision',409);
 const query=foldTaskText(filter.query??''),matches=(task:CompanionTask)=>!query||foldTaskText(task.text).includes(query)||foldTaskText(task.notes??'').includes(query),tasks=new Map(document.tasks.map(row=>[row.id,row]));
 const unresolved=effectsFenced||state.commands.some(row=>row.effects.length>0&&!['confirmed','cancelled'].includes(row.projectionState));
 const sourceGroups=new Map<string,typeof state.commands>();for(const row of state.commands){const key=row.command.taskId+':'+(row.command.expectedRevision+1);const group=sourceGroups.get(key)??[];group.push(row);sourceGroups.set(key,group);}
 const source=(taskId:string,revision:number,occurrence?:Occurrence)=>{
  const entries=(sourceGroups.get(taskId+':'+revision)??[]).filter(row=>occurrence?row.command.operation===(occurrence.state==='completed'?'completeTask':'skipOccurrence')&&row.command.occurrenceDate===occurrence.date&&(occurrence.terminalCommandId===undefined||occurrence.terminalCommandId===row.command.id):row.command.operation==='deleteTask');
  return entries.length===1?entries[0]:null;
 };
 const latest=(taskId:string,occurrence?:Occurrence)=>{
  const records=[...(document.unarchives??[]).filter(row=>!occurrence&&row.taskId===taskId).map(row=>({commandId:row.commandId,revision:row.revision,kind:'unarchive'})),...(document.restorations??[]).filter(row=>!occurrence&&row.taskId===taskId).map(row=>({commandId:row.commandId,revision:row.revision,kind:'todoRestoration'})),...(document.occurrenceCorrections??[]).filter(row=>row.taskId===taskId&&(!occurrence||row.previousOccurrence.id===occurrence.id)).map(row=>({commandId:row.commandId,revision:row.revision,kind:'occurrenceCorrection'}))];
  return records.sort((a,b)=>b.revision-a.revision||a.commandId.localeCompare(b.commandId))[0]??null;
 };
 const item=(task:CompanionTask,occurrence?:Occurrence)=>{
  const original=source(task.id,occurrence?.revision??task.revision,occurrence),completion=occurrence?document.completions.find(row=>row.taskId===task.id&&row.occurrenceId===occurrence.id&&row.revision===occurrence.revision):null;
  const gameFence=hasGameFence(document,task.id)||unresolved;
  const reason=gameFence?'unsupported_projection':!occurrence?unarchiveReferencesAvailable(document,task,{archivedRevision:task.revision,planning:null,relationships:null})?null:'unarchive_repair_required':task.isArchived?'task_unavailable':task.kind!=='daily'||occurrence.isArchived||occurrence.state==='pending'?'occurrence_unavailable':!original||original.effects.length>0?'occurrence_source_unavailable':null;
  const linked=Boolean(occurrence&&completion&&!task.isArchived&&!completion.isArchived&&document.projections.some(row=>row.taskId===task.id&&!row.isArchived&&row.state==='linked'&&row.habiticaTaskId)
   &&!document.restorations?.some(row=>row.completionId===completion.id)&&!document.occurrenceCorrections?.some(row=>row.targetCommandId===completion.commandId));
  return {recordId:occurrence?.id??task.id,task,detailSource:'currentTask',occurrence:occurrence??null,completionId:completion?.id??null,originalSourceCommandId:original?.command.id??null,sourceAvailability:original?'retained':'unavailable',nativeAction:{operation:occurrence?'correctOccurrence':'unarchiveTask',canReview:reason===null,reason},linkedRecovery:linked?unresolved?'requiresOriginalReceipt':'requiresFreshReview':'notApplicable',latestRecoveryReceipt:latest(task.id,occurrence)};
 };
 const rows=filter.recordType==='archivedTasks'?document.tasks.filter(row=>row.isArchived&&matches(row)).sort((a,b)=>{const first=utcTicks(a.updatedAtUtc),second=utcTicks(b.updatedAtUtc);return first===second?a.id.localeCompare(b.id):first>second?-1:1;}).map(row=>item(row))
  :document.occurrences.filter(row=>!row.isArchived&&row.state!=='pending'&&tasks.has(row.taskId)&&matches(tasks.get(row.taskId)!)).sort((a,b)=>b.date.localeCompare(a.date)||a.id.localeCompare(b.id)).map(row=>item(tasks.get(row.taskId)!,row));
 const items=rows.slice(filter.offset,filter.offset+filter.limit),next=filter.offset+items.length;
 return {provenance:'companion-canonical-retained-recovery',workspaceId:document.id,revision:document.revision,recordType:filter.recordType,effectsFenced:unresolved,items,matchedCount:rows.length,offset:filter.offset,hasMore:next<rows.length,nextOffset:next<rows.length?next:null};
}
