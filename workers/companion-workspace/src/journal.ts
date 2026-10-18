import {executionStage,todoStageMatches} from '../../shared/todo-stage.ts';
import {todoStateMatches} from '../../shared/todo-state.ts';
import {validateOrganizationHistoryHashes} from '../../shared/workspace-organization.ts';
import {relationshipsMatch} from '../../shared/task-relationships.ts';
import {planningMatches} from '../../shared/task-planning.ts';
import { habiticaDetails } from "../../shared/task-details.ts";
import { workspaceDocument, workspaceRecurrence, utcTicks, type WorkspaceDocument, type CompanionTask } from '../../shared/workspace-domain.ts';
import { workspaceCommand, commandHash } from '../../shared/workspace-commands.ts';
import { workspaceObject as object, uuid, WorkspaceError } from '../../shared/workspace-http.ts';
import type { CommandEntry, JournalEvent } from './commands.ts';
export interface Authority {mode:'local'|'frozen'|'cloud'|'paused';transferId:string|null;cloudWorkspaceId:string|null}
export interface Journal {document:WorkspaceDocument;storeVersion:number;commands:CommandEntry[];events:JournalEvent[];authority?:Authority|null}
const states=['notRequested','pending','leased','executing','confirmed','blocked','unknownOutcome','cancelled'];
const failures=['none','rateLimited','credentialRepair','precondition','permanent','transient','ambiguous'];
const kinds=['accepted','leaseAcquired','leaseExpired','dispatched','resultKnown','unknownDetected','reconciled','cancelled'];
function valid(ok:unknown):asserts ok {if(!ok)throw new WorkspaceError('invalid_archive',422);}
function timestamp(v:unknown):v is string{try{return utcTicks(v)!==utcTicks('0001-01-01T00:00:00Z');}catch{return false;}}
export async function journal(value:unknown):Promise<Journal>{
 const input=object(value,['document','storeVersion','commands','events','authority']);const document=workspaceDocument(input.document);
 valid(Number.isSafeInteger(input.storeVersion)&&Number(input.storeVersion)>=document.revision&&Array.isArray(input.commands));
 const organizationIds=new Set((document.organizationHistory??[]).map(entry=>entry.command.id)),organizationRevisions=new Set((document.organizationHistory??[]).map(entry=>entry.snapshot.revision));
 await validateOrganizationHistoryHashes(document);
 const ids=new Set<string>();const commands:CommandEntry[]=[];
 for(const raw of input.commands){
  const row=object(raw,['command','payloadHash','acceptedAtUtc','projectionState','effects','confirmedEffects','failure','retryAtUtc','lease','dispatchedAtUtc','attempts','taskSnapshot']);
  const command=workspaceCommand(row.command);valid(!ids.has(command.id)&&!organizationIds.has(command.id)&&!organizationRevisions.has(command.expectedRevision+1)&&command.workspaceId===document.id&&command.expectedRevision<document.revision&&row.payloadHash===await commandHash(command));ids.add(command.id);
  valid(timestamp(row.acceptedAtUtc)&&states.includes(String(row.projectionState))&&failures.includes(String(row.failure))&&Array.isArray(row.effects)&&row.effects.length<=11&&row.effects.every(v=>['create','update','scoreUp','scoreDown'].includes(v)));
  const effects=row.effects as string[];valid(Number.isInteger(row.confirmedEffects)&&Number(row.confirmedEffects)>=0&&Number(row.confirmedEffects)<=effects.length&&Number.isSafeInteger(row.attempts)&&Number(row.attempts)>=0);
  const restoration=document.restorations?.find(value=>value.commandId===command.id);valid(!restoration||command.operation==='restoreTodo');
  if(command.operation==='restoreTodo'){const accepted=object(row.taskSnapshot);valid(!command.projectToHabitica&&row.projectionState==='notRequested'&&effects.length===0&&accepted.kind==='todo'&&accepted.state==='active'&&accepted.isArchived!==true&&restoration&&restoration.taskId===command.taskId&&restoration.completionId===command.patch?.restoreTodo?.completionId&&restoration.revision===command.expectedRevision+1&&utcTicks(restoration.restoredAtUtc)===utcTicks(row.acceptedAtUtc));}
  if(command.operation==='setTodoStage'){valid(!command.projectToHabitica&&row.projectionState==='notRequested'&&effects.length===0);const accepted=object(row.taskSnapshot);valid(command.patch?.todoStage&&todoStageMatches(command.patch.todoStage,accepted as unknown as CompanionTask));}
  if(command.operation==='setTodoState'){valid(!command.projectToHabitica&&row.projectionState==='notRequested'&&effects.length===0);const accepted=object(row.taskSnapshot);valid(command.patch?.todoState&&todoStateMatches(command.patch.todoState,accepted as unknown as CompanionTask));}
  if(command.operation==='relateTodo'){valid(!command.projectToHabitica&&row.projectionState==='notRequested'&&effects.length===0);const accepted=object(row.taskSnapshot);valid(command.patch?.relationships&&accepted.isArchived!==true&&relationshipsMatch(command.patch.relationships,accepted as unknown as CompanionTask));}
  if(command.operation==='planTask'){valid(!command.projectToHabitica&&row.projectionState==='notRequested'&&effects.length===0);const accepted=object(row.taskSnapshot);valid(command.patch?.planning&&planningMatches(command.patch.planning,accepted));}
  if(['skipOccurrence','pauseTask','resumeTask'].includes(command.operation)){
   valid(!command.projectToHabitica&&row.projectionState==='notRequested'&&effects.length===0&&command.patch===null&&command.direction===null&&command.count===1);
   const task=object(row.taskSnapshot);valid(['daily','habit'].includes(String(task.kind)));
   valid(command.operation==='skipOccurrence'?task.kind==='daily'&&task.state==='active'&&task.recurrence!=null&&command.occurrenceDate!==null&&command.occurrenceDate!=='0001-01-01':command.occurrenceDate===null&&task.state===(command.operation==='pauseTask'?'paused':'active'));
  }
  if(command.patch?.recurrence||command.patch?.clearRecurrence){valid(!command.projectToHabitica&&row.projectionState==='notRequested'&&effects.length===0&&['createDaily','updateTask'].includes(command.operation)&&!(command.operation==='createDaily'&&command.patch.clearRecurrence));const accepted=object(row.taskSnapshot);valid(accepted.kind==='daily');const rule=workspaceRecurrence(accepted.recurrence);valid(command.patch.clearRecurrence?rule===null:JSON.stringify(rule)===JSON.stringify(command.patch.recurrence));}
  valid(row.projectionState==='notRequested'?effects.length===0:effects.length>0);
  if(row.projectionState==='confirmed')valid(row.confirmedEffects===effects.length);
  if(['pending','leased','executing','unknownOutcome'].includes(String(row.projectionState)))valid(Number(row.confirmedEffects)<effects.length);
  valid(['leased','executing','unknownOutcome'].includes(String(row.projectionState))===(row.lease!=null));
  if(row.lease!=null){const lease=object(row.lease,['owner','token','expiresAtUtc']);uuid(lease.token);valid(typeof lease.owner==='string'&&lease.owner.trim().length>0&&lease.owner.length<=128&&timestamp(lease.expiresAtUtc));}
  if(['executing','unknownOutcome'].includes(String(row.projectionState)))valid(timestamp(row.dispatchedAtUtc)&&Number(row.attempts)>0);
  valid(row.retryAtUtc==null||timestamp(row.retryAtUtc));valid(row.dispatchedAtUtc==null||timestamp(row.dispatchedAtUtc));
  if(row.taskSnapshot!=null){const task=object(row.taskSnapshot,['id','revision','isArchived','text','kind','state','priority','createdAtUtc','updatedAtUtc','notes','categoryId','projectId','sectionId','parentTaskId','labelIds','dependsOn','plannedDate','deadline','reviewAfterUtc','staleSinceUtc','recurrence','habiticaDetails','executionStage']);
   habiticaDetails(task.habiticaDetails);if(executionStage(task.executionStage)!==null)valid(task.kind==='todo');
   if(task.recurrence!=null){valid(task.kind==='daily');workspaceRecurrence(task.recurrence);}
   valid(task.id===command.taskId&&task.revision===command.expectedRevision+1&&['todo','daily','habit'].includes(String(task.kind))&&['active','paused','completed','waiting','someday','cancelled'].includes(String(task.state))&&['low','normal','high','urgent'].includes(String(task.priority))&&typeof task.text==='string'&&task.text.trim().length>0&&task.text.length<=2048&&(task.notes==null||typeof task.notes==='string'&&task.notes.length<=10000)&&timestamp(task.createdAtUtc)&&timestamp(task.updatedAtUtc)&&utcTicks(task.updatedAtUtc)===utcTicks(row.acceptedAtUtc)&&utcTicks(task.createdAtUtc)<=utcTicks(task.updatedAtUtc));
  }
  commands.push({...row,command} as unknown as CommandEntry);
 }
 valid(Array.isArray(input.events)||commands.length===0&&input.events==null);const events:JournalEvent[]=[];const eventIds=new Set<string>();
 for(const raw of input.events as unknown[]??[]){const event=object(raw,['id','commandId','kind','atUtc','confirmedEffects','failure','attemptToken','evidenceReference']);const id=uuid(event.id);valid(!eventIds.has(id)&&ids.has(uuid(event.commandId))&&kinds.includes(String(event.kind))&&failures.includes(String(event.failure))&&timestamp(event.atUtc)&&Number.isInteger(event.confirmedEffects)&&Number(event.confirmedEffects)>=0&&Number(event.confirmedEffects)<=11);eventIds.add(id);if(event.attemptToken!=null)uuid(event.attemptToken);valid(event.evidenceReference==null||typeof event.evidenceReference==='string'&&event.evidenceReference.length<=256);if(event.kind==='reconciled')valid(typeof event.evidenceReference==='string'&&event.evidenceReference.trim().length>0);events.push(event as unknown as JournalEvent);}
 let authority:Authority|null=null;
 if(input.authority!=null){const row=object(input.authority,['mode','transferId','cloudWorkspaceId']);valid(['local','frozen','cloud','paused'].includes(String(row.mode)));const transferId=row.transferId==null?null:uuid(row.transferId);const cloudWorkspaceId=row.cloudWorkspaceId==null?null:uuid(row.cloudWorkspaceId);valid(row.mode!=='local'||!transferId&&!cloudWorkspaceId);valid(row.mode!=='frozen'||transferId&&!cloudWorkspaceId);valid(row.mode!=='cloud'||cloudWorkspaceId===document.id);authority={mode:row.mode as Authority['mode'],transferId,cloudWorkspaceId};}
 return {document,storeVersion:Number(input.storeVersion),commands,events,authority};
}
export function event(entry:CommandEntry,kind:string,evidenceReference:string|null=null):JournalEvent{return {id:crypto.randomUUID(),commandId:entry.command.id,kind,atUtc:new Date().toISOString(),confirmedEffects:entry.confirmedEffects,failure:entry.failure,attemptToken:entry.lease?.token??null,evidenceReference};}
