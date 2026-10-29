import {reminderInput,type ReminderTool} from '../../shared/reminder-tools.ts';
import {reminderIntent,nextReminder,type ReminderIntent} from '../../shared/reminder-calendar.ts';
import {workspaceDocument,type WorkspaceDocument} from '../../shared/workspace-domain.ts';
import {reviewInstant} from '../../shared/task-review.ts';
import {workspaceObject,workspaceTokenHash,workspaceSessionToken,uuid,WorkspaceError} from '../../shared/workspace-http.ts';
import type {WorkspaceRow} from './executor.ts';
export interface ReminderSchedule {profile_id:string;schedule_id:string;revision:number;connection_id:string;access_epoch:string;task_id:string;cue_utc:string;cue_revision:number;consent_epoch:string;state:string;reason:string;commit_tag:string;intent_json:string;next_at_utc:string|null;next_civil_date:string|null;lease_token:string|null;lease_until_ms:number|null;updated_at_utc:string}
interface Command {profile_id:string;request_id:string;command_id:string;connection_id:string;access_epoch:string;grant_epoch:string;input_hash:string;intent_json:string;expected_workspace_revision:number;token_hash:string|null;expires_at_utc:string;receipt_json:string|null}
export interface ReminderPushStatus {profileId:string;connectionId:string;available:boolean;dispatchAvailable:boolean;enabled:boolean;consentEpoch:string|null;devices:{deviceId:string;revision:number;subscriptionId:string;state:string;expiresAtMs:number|null}[]}
const now=()=>new Date().toISOString();
const command=(env:WorkspaceEnv,profile:string,id:string)=>env.WORKSPACE_DB.prepare('SELECT * FROM workspace_reminder_commands WHERE profile_id=? AND command_id=?').bind(profile,id).first<Command>();
export async function currentReminderWorkspace(env:WorkspaceEnv,row:WorkspaceRow):Promise<WorkspaceRow>{
 const current=await env.WORKSPACE_DB.prepare('SELECT * FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1').bind(row.profile_id,row.connection_id,row.access_epoch).first<WorkspaceRow>();
 if(!current)throw new WorkspaceError('workspace_changed',409);return current;
}
export function reminderCueRevision(document:WorkspaceDocument,taskId:string):number{return Math.max(0,...(document.reviewEvents??[]).filter(e=>e.taskId===taskId&&['set','snooze','clear','markReviewed'].includes(e.action)).map(e=>e.revision));}
export function reminderTask(document:WorkspaceDocument,intent:ReminderIntent){
 const task=document.tasks.find(t=>t.id===intent.taskId);
 if(!task||task.isArchived||['completed','cancelled'].includes(task.state)||reviewInstant(task.reviewAfterUtc)!==intent.reviewAfterUtc)throw new WorkspaceError('review_cue_changed',409);return task;
}
export async function reminderPushStatus(env:WorkspaceEnv,row:WorkspaceRow):Promise<ReminderPushStatus>{
 const response=await env.AUTH_PUSH.fetch('https://push.internal/status',{method:'POST',body:JSON.stringify({profileId:row.profile_id,connectionId:row.connection_id}),signal:AbortSignal.timeout(5000)});
 if(!response.ok)throw new WorkspaceError('push_status_unavailable',503);
 const value=await response.json<ReminderPushStatus>();
 if(value.profileId!==row.profile_id||value.connectionId!==row.connection_id||!Array.isArray(value.devices)||value.devices.length>64)throw new WorkspaceError('push_context_invalid',502);return value;
}
export function reminderAuthority(row:WorkspaceRow):boolean{return Boolean(row.active&&!row.retirement_fence&&(!row.authority_json||JSON.parse(row.authority_json).mode==='cloud'));}
function scheduleView(s:ReminderSchedule){return {scheduleId:s.schedule_id,revision:s.revision,taskId:s.task_id,state:s.state,reason:s.reason,intent:JSON.parse(s.intent_json),nextAtUtc:s.next_at_utc,nextCivilDate:s.next_civil_date};}
async function validateSet(env:WorkspaceEnv,row:WorkspaceRow,intent:ReminderIntent,expectedWorkspaceRevision:number){
 if(!env.PUSH_REMINDERS_ENABLED)throw new WorkspaceError('reminders_unavailable',503);
 if(!reminderAuthority(row))throw new WorkspaceError('workspace_authority_required',409);
 const document=workspaceDocument(JSON.parse(row.document_json));if(document.revision!==expectedWorkspaceRevision)throw new WorkspaceError('stale_revision',409);
 const task=reminderTask(document,intent),cue=intent.reviewAfterUtc!,at=Date.parse(cue),clock=Date.now();
 if(at+3600000<=clock||at>clock+366*86400000)throw new WorkspaceError('reminder_cue_outside_horizon',422);
 const first=nextReminder(intent,new Date(at-1).toISOString());if(first?.atUtc!==cue)throw new WorkspaceError('initial_review_cue_must_match_calendar',422);
 const push=await reminderPushStatus(env,row);if(!push.available||!push.dispatchAvailable)throw new WorkspaceError('push_dispatch_unavailable',503);if(!push.enabled||!push.consentEpoch)throw new WorkspaceError('separate_push_consent_required',403);
 for(const target of intent.targets)if(!push.devices.some(d=>d.deviceId===target.deviceId&&d.revision===target.revision&&d.subscriptionId===target.subscriptionId&&d.state==='active'&&(d.expiresAtMs===null||d.expiresAtMs>clock)))throw new WorkspaceError('push_device_changed',409);
 return {document,task,push,first};
}
export async function reminderApi(env:WorkspaceEnv,initial:WorkspaceRow,tool:ReminderTool,args:unknown){
 const input=reminderInput(tool,args);let row=await currentReminderWorkspace(env,initial);
 if(tool==='get_task_reminders'){
  const document=workspaceDocument(JSON.parse(row.document_json));if(!document.tasks.some(t=>t.id===input.taskId))throw new WorkspaceError('task_unavailable',404);
  const schedules=await env.WORKSPACE_DB.prepare('SELECT * FROM workspace_reminder_schedules WHERE profile_id=? AND task_id=? ORDER BY schedule_id LIMIT 65').bind(row.profile_id,input.taskId).all<ReminderSchedule>();
  const deliveries=await env.WORKSPACE_DB.prepare(`SELECT d.occurrence_id AS occurrenceId,d.schedule_id AS scheduleId,d.schedule_revision AS scheduleRevision,d.civil_date AS civilDate,d.at_utc AS atUtc,d.device_id AS deviceId,d.state,d.code,d.attempt,d.retry_after_ms AS retryAfterMs,d.http_status AS httpStatus,d.attempts_json AS attemptsJson
   FROM workspace_reminder_deliveries d JOIN workspace_reminder_schedules s ON s.profile_id=d.profile_id AND s.schedule_id=d.schedule_id WHERE s.profile_id=? AND s.task_id=? ORDER BY d.at_utc DESC,d.occurrence_id,d.device_id LIMIT 51 OFFSET ?`).bind(row.profile_id,input.taskId,input.offset).all();
  let push:ReminderPushStatus|null=null;try{push=await reminderPushStatus(env,row);}catch{/* Unavailable is explicit, never an empty device list. */}
  await currentReminderWorkspace(env,row);
  return {workspaceId:row.workspace_id,workspaceRevision:document.revision,taskId:input.taskId,available:Boolean(env.PUSH_REMINDERS_ENABLED)&&push?.dispatchAvailable===true,schedules:schedules.results.map(scheduleView),deliveries:deliveries.results.slice(0,50).map(d=>{const {attemptsJson,...view}=d;return {...view,attempts:JSON.parse(String(attemptsJson))};}),offset:input.offset,nextOffset:deliveries.results.length>50?Number(input.offset)+50:null,push,shown:null,read:null};
 }
 if(tool==='get_reminder_command'){
  const found=await command(env,row.profile_id,String(input.commandId));return found?{commandId:found.command_id,requestId:found.request_id,accepted:found.receipt_json!==null,receipt:found.receipt_json?JSON.parse(found.receipt_json):null,expiresAtUtc:found.expires_at_utc}:{commandId:input.commandId,accepted:null,outcomeUnknown:true};
 }
 if(tool==='prepare_reminder_command'){
  const intent=reminderIntent(input.intent),hash=await workspaceTokenHash(JSON.stringify({expectedWorkspaceRevision:input.expectedWorkspaceRevision,intent}));
  let found=await env.WORKSPACE_DB.prepare('SELECT * FROM workspace_reminder_commands WHERE profile_id=? AND request_id=?').bind(row.profile_id,input.requestId).first<Command>();
  if(found&&found.input_hash!==hash)throw new WorkspaceError('request_id_reused',409);
  if(found?.receipt_json)return {commandId:found.command_id,requestId:found.request_id,accepted:true,receipt:JSON.parse(found.receipt_json)};
  if(!found){
   if(intent.operation==='set')await validateSet(env,row,intent,Number(input.expectedWorkspaceRevision));
   else {const old=await env.WORKSPACE_DB.prepare('SELECT * FROM workspace_reminder_schedules WHERE profile_id=? AND schedule_id=? AND task_id=? AND revision=?').bind(row.profile_id,intent.scheduleId,intent.taskId,intent.expectedRevision).first();if(!old)throw new WorkspaceError('reminder_changed',409);}
   if(intent.operation==='set'&&intent.scheduleId!==null&&!await env.WORKSPACE_DB.prepare('SELECT 1 FROM workspace_reminder_schedules WHERE profile_id=? AND schedule_id=? AND task_id=? AND revision=?').bind(row.profile_id,intent.scheduleId,intent.taskId,intent.expectedRevision).first())throw new WorkspaceError('reminder_changed',409);
   const commandId=crypto.randomUUID(),expires=new Date(Date.now()+600000).toISOString();
   const body={intent,scheduleId:intent.scheduleId??crypto.randomUUID()};
   await env.WORKSPACE_DB.prepare(`INSERT OR IGNORE INTO workspace_reminder_commands(profile_id,request_id,command_id,connection_id,access_epoch,grant_epoch,input_hash,intent_json,expected_workspace_revision,expires_at_utc,created_at_utc)
    SELECT ?,?,?,?,?,?,?,?,?,?,? WHERE EXISTS(SELECT 1 FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1) AND (SELECT COUNT(*) FROM workspace_reminder_commands WHERE profile_id=?)<2048`)
    .bind(row.profile_id,input.requestId,commandId,row.connection_id,row.access_epoch,row.mcp_grant_epoch??'browser',hash,JSON.stringify(body),input.expectedWorkspaceRevision,expires,now(),row.profile_id,row.connection_id,row.access_epoch,row.profile_id).run();
   found=await env.WORKSPACE_DB.prepare('SELECT * FROM workspace_reminder_commands WHERE profile_id=? AND request_id=?').bind(row.profile_id,input.requestId).first<Command>();
   if(!found)throw new WorkspaceError('reminder_capacity_or_context',409);if(found.input_hash!==hash)throw new WorkspaceError('request_id_reused',409);
  }
  if(found.connection_id!==row.connection_id||found.access_epoch!==row.access_epoch||found.grant_epoch!==(row.mcp_grant_epoch??'browser')||found.expires_at_utc<=now())throw new WorkspaceError('preview_expired_or_changed',409);
  const token=workspaceSessionToken();await env.WORKSPACE_DB.prepare('UPDATE workspace_reminder_commands SET token_hash=? WHERE profile_id=? AND command_id=? AND receipt_json IS NULL').bind(await workspaceTokenHash(token),row.profile_id,found.command_id).run();
  return {commandId:found.command_id,requestId:found.request_id,accepted:false,inputHash:found.input_hash,acceptToken:token,expiresAtUtc:found.expires_at_utc,workspaceId:row.workspace_id,expectedWorkspaceRevision:found.expected_workspace_revision,...JSON.parse(found.intent_json),warning:'Schedules generic notifications only after separate push consent. Provider acceptance is not shown/read. Unknown delivery never retries. Review exact task, cue, timezone, repeat/DST policy and devices.'};
 }
 const found=await command(env,row.profile_id,String(input.commandId));if(!found)throw new WorkspaceError('preview_required',409);
 if(found.receipt_json)return {commandId:found.command_id,accepted:true,receipt:JSON.parse(found.receipt_json)};
 if(found.connection_id!==row.connection_id||found.access_epoch!==row.access_epoch||found.grant_epoch!==(row.mcp_grant_epoch??'browser')||found.expires_at_utc<=now()||found.token_hash!==await workspaceTokenHash(String(input.acceptToken))||found.input_hash!==input.expectedHash)throw new WorkspaceError('preview_expired_or_changed',409);
 const payload=workspaceObject(JSON.parse(found.intent_json)),intent=reminderIntent(payload.intent),id=uuid(payload.scheduleId),tag=crypto.randomUUID();
 const validated=intent.operation==='set'?await validateSet(env,row,intent,found.expected_workspace_revision):null;
 row=await currentReminderWorkspace(env,row);
 if(validated&&(!reminderAuthority(row)||workspaceDocument(JSON.parse(row.document_json)).revision!==found.expected_workspace_revision))throw new WorkspaceError('stale_revision',409);
 if(intent.operation==='set'&&await env.WORKSPACE_DB.prepare("SELECT 1 FROM workspace_reminder_schedules WHERE profile_id=? AND task_id=? AND state='active' AND schedule_id<>?").bind(row.profile_id,intent.taskId,id).first())throw new WorkspaceError('task_reminder_already_active',409);
 const revision=intent.expectedRevision+1,receiptValue={commandId:found.command_id,requestId:found.request_id,workspaceId:row.workspace_id,scheduleId:id,revision,taskId:intent.taskId,operation:intent.operation,state:intent.operation==='set'?'active':'cancelled',saved:true,scheduled:intent.operation==='set',dispatched:false,acceptedByPushService:false,shown:null,read:null,intent};
 const authority=`EXISTS(SELECT 1 FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1 AND document_json=?)`;
 const authorityArgs=[row.profile_id,row.connection_id,row.access_epoch,row.document_json];
 const guard=`${authority} AND EXISTS(SELECT 1 FROM workspace_reminder_commands WHERE profile_id=? AND command_id=? AND token_hash=? AND receipt_json IS NULL AND expires_at_utc>?)`;
 const guardArgs=[...authorityArgs,row.profile_id,found.command_id,found.token_hash,now()];
 const statements=[];
 if(intent.operation==='set'){
  statements.push(env.WORKSPACE_DB.prepare(`INSERT INTO workspace_reminder_schedules(profile_id,schedule_id,revision,connection_id,access_epoch,task_id,cue_utc,cue_revision,consent_epoch,state,reason,commit_tag,intent_json,next_at_utc,next_civil_date,updated_at_utc)
   SELECT ?,?,?,?,?,?,?,?,?,'active','scheduled',?,?,?, ?,? WHERE ${guard} AND COALESCE((SELECT revision FROM workspace_reminder_schedules WHERE profile_id=? AND schedule_id=?),0)=?
   AND ((SELECT COUNT(*) FROM workspace_reminder_schedules WHERE profile_id=?)<64 OR EXISTS(SELECT 1 FROM workspace_reminder_schedules WHERE profile_id=? AND schedule_id=?))
   ON CONFLICT(profile_id,schedule_id) DO UPDATE SET revision=excluded.revision,connection_id=excluded.connection_id,access_epoch=excluded.access_epoch,task_id=excluded.task_id,cue_utc=excluded.cue_utc,cue_revision=excluded.cue_revision,consent_epoch=excluded.consent_epoch,state=excluded.state,reason=excluded.reason,commit_tag=excluded.commit_tag,intent_json=excluded.intent_json,next_at_utc=excluded.next_at_utc,next_civil_date=excluded.next_civil_date,lease_token=NULL,lease_until_ms=NULL,updated_at_utc=excluded.updated_at_utc`)
   .bind(row.profile_id,id,revision,row.connection_id,row.access_epoch,intent.taskId,intent.reviewAfterUtc,reminderCueRevision(validated!.document,intent.taskId),validated!.push.consentEpoch,tag,JSON.stringify(intent),validated!.first!.atUtc,validated!.first!.civilDate,now(),...guardArgs,row.profile_id,id,intent.expectedRevision,row.profile_id,row.profile_id,id));
 } else statements.push(env.WORKSPACE_DB.prepare(`UPDATE workspace_reminder_schedules SET state='cancelled',reason='cancelled_by_user',commit_tag=?,revision=?,next_at_utc=NULL,next_civil_date=NULL,lease_token=NULL,lease_until_ms=NULL,updated_at_utc=? WHERE profile_id=? AND schedule_id=? AND task_id=? AND revision=? AND ${guard}`).bind(tag,revision,now(),row.profile_id,id,intent.taskId,intent.expectedRevision,...guardArgs));
 const committed=`EXISTS(SELECT 1 FROM workspace_reminder_schedules WHERE profile_id=? AND schedule_id=? AND revision=? AND commit_tag=?)`;
 statements.push(env.WORKSPACE_DB.prepare(`UPDATE workspace_reminder_commands SET receipt_json=?,token_hash=NULL WHERE profile_id=? AND command_id=? AND receipt_json IS NULL AND ${committed}`).bind(JSON.stringify(receiptValue),row.profile_id,found.command_id,row.profile_id,id,revision,tag));
 statements.push(env.WORKSPACE_DB.prepare(`UPDATE workspace_reminder_deliveries SET state='cancelled',code='schedule_changed',updated_at_utc=? WHERE profile_id=? AND schedule_id=? AND state IN ('scheduled','retryable') AND schedule_revision<>? AND ${committed}`).bind(now(),row.profile_id,id,revision,row.profile_id,id,revision,tag));
 try {const saved=await env.WORKSPACE_DB.batch(statements);if(!saved[0].meta.changes||!saved[1].meta.changes)throw new WorkspaceError('reminder_changed',409);}
 catch(error){const original=await command(env,row.profile_id,found.command_id);if(original?.receipt_json)return {commandId:found.command_id,accepted:true,receipt:JSON.parse(original.receipt_json)};if(intent.operation==='set'&&await env.WORKSPACE_DB.prepare("SELECT 1 FROM workspace_reminder_schedules WHERE profile_id=? AND task_id=? AND state='active' AND schedule_id<>?").bind(row.profile_id,intent.taskId,id).first())throw new WorkspaceError('task_reminder_already_active',409);throw error;}
 return {commandId:found.command_id,accepted:true,receipt:receiptValue};
}
