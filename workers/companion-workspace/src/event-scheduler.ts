import {eventWorkspace,eventSubscription,type EventsWorkspaceEnv} from './events.ts';
import {workspaceObject,uuid,WorkspaceError} from '../../shared/workspace-http.ts';
import type {EventPrincipal} from '../../shared/events.ts';
interface Outbox {profile_id:string;event_id:string;subscription_id:string;task_id:string;connection_id:string;access_epoch:string;grant_epoch:string;consent_epoch:string;cue:string;cue_revision:number;expires_at:number;state:string;attempt:number;next_at:number;lease_token:string|null;lease_until:number|null;receipt_json:string|null}
type Receipt={eventId:string;attempt:number;state:string;retryAfterMs?:number|null;acceptedByHost:boolean|null;chatResponse:null;[key:string]:unknown};
const who=(r:Outbox):EventPrincipal=>({profileId:r.profile_id,connectionId:r.connection_id,grantEpoch:r.grant_epoch});
async function authReceipt(env:EventsWorkspaceEnv,row:Outbox):Promise<Receipt>{
 const r=await env.AUTH_EVENTS.fetch('https://events.internal/receipt',{method:'POST',body:JSON.stringify({principal:who(row),eventId:row.event_id,attempt:row.attempt}),signal:AbortSignal.timeout(5000)});
 if(!r.ok)throw new WorkspaceError('original_event_receipt_unavailable',503);const v=await r.json<Receipt>();if(v.eventId!==row.event_id||v.attempt!==row.attempt)throw new WorkspaceError('original_event_receipt_invalid',502);return v;
}
function unknown(row:Outbox):Receipt{return {eventId:row.event_id,attempt:row.attempt,state:'unknown',code:'original_event_outcome_unknown',acceptedByHost:null,chatResponse:null};}
async function save(env:EventsWorkspaceEnv,row:Outbox,token:string,value:Receipt,clock:number){
 const state=['accepted','refused','cancelled','expired','unknown','retryable'].includes(value.state)?value.state:'unknown';
 const delay=state==='retryable'&&row.attempt<3?Math.max(1000,Math.min(60000,Number(value.retryAfterMs)||1000)):0;
 await env.WORKSPACE_DB.prepare('UPDATE workspace_event_outbox SET state=?,receipt_json=?,next_at=?,lease_token=NULL,lease_until=NULL WHERE profile_id=? AND event_id=? AND lease_token=?').bind(state,JSON.stringify({...value,state,chatResponse:null}),clock+delay,row.profile_id,row.event_id,token).run();
}
export async function scheduledEvents(env:EventsWorkspaceEnv,options:{now?:()=>number}={}){
 if(!Boolean(env.EVENTS_ENABLED)||!env.WORKSPACE_ENABLED)return;
 const now=options.now??Date.now,start=Date.now(),deadline=start+25000,db=env.WORKSPACE_DB,clock=now();
 // Exclude already materialized occurrences before the limit, so earlier tasks cannot starve later subscriptions.
 const candidates=await db.prepare(`SELECT s.profile_id,s.subscription_id,s.connection_id,s.grant_epoch,s.task_id,c.cue,c.cue_revision FROM workspace_event_subscriptions s JOIN workspace_event_consents c ON c.profile_id=s.profile_id AND c.task_id=s.task_id AND c.consent_epoch=s.consent_epoch JOIN workspaces w ON w.profile_id=s.profile_id, json_each(w.document_json,'$.tasks') t WHERE s.state='active' AND s.expires_at>? AND c.enabled=1 AND w.active=1 AND json_extract(t.value,'$.id')=s.task_id AND json_extract(t.value,'$.reviewAfterUtc')=c.cue AND julianday(c.cue)<=julianday(?) AND NOT EXISTS(SELECT 1 FROM workspace_event_outbox o WHERE o.profile_id=s.profile_id AND o.subscription_id=s.subscription_id AND o.consent_epoch=s.consent_epoch AND o.cue=c.cue AND o.cue_revision=c.cue_revision) ORDER BY c.cue,s.subscription_id LIMIT 20`).bind(clock,new Date(clock).toISOString()).all<{profile_id:string;subscription_id:string;connection_id:string;grant_epoch:string;task_id:string;cue:string;cue_revision:number}>();
 for(const candidate of candidates.results){
  if(Date.now()+5000>=deadline)break;
  try{
   const principal={profileId:candidate.profile_id,connectionId:candidate.connection_id,grantEpoch:candidate.grant_epoch},s=await eventSubscription(env,principal,candidate.subscription_id);if(!s||s.state!=='active'||!s.cue||Date.parse(s.cue)>clock)continue;
   const expires=Math.min(s.expiresAt,Date.parse(s.cue)+3600000),id=crypto.randomUUID();
   await db.prepare(`INSERT OR IGNORE INTO workspace_event_outbox SELECT ?,?,?,?,?,?,?,?,?,?,?,?,?,?,NULL,NULL,NULL,? WHERE (SELECT COUNT(*) FROM workspace_event_outbox WHERE profile_id=?)<2048 AND (SELECT COUNT(*) FROM workspace_event_outbox)<20000 AND EXISTS(SELECT 1 FROM workspace_event_subscriptions WHERE profile_id=? AND subscription_id=? AND revision=? AND state='active')`).bind(s.profileId,id,s.subscriptionId,s.taskId,s.connectionId,s.accessEpoch,s.grantEpoch,s.consentEpoch,s.cue,s.cueRevision,expires,expires<=clock?'expired':'scheduled',0,clock,clock,s.profileId,s.profileId,s.subscriptionId,s.revision).run();
  }catch{/* Changed/denied resource creates no application event. */}
 }
 const ready=await db.prepare("SELECT * FROM workspace_event_outbox WHERE state IN ('scheduled','retryable','dispatching') AND next_at<=? AND (lease_until IS NULL OR lease_until<=?) ORDER BY next_at,event_id LIMIT 20").bind(clock,clock).all<Outbox>();
 for(let row of ready.results){
  if(Date.now()+23000>=deadline)break;
  const token=crypto.randomUUID(),leased=await db.prepare('UPDATE workspace_event_outbox SET lease_token=?,lease_until=? WHERE profile_id=? AND event_id=? AND state=? AND attempt=? AND (lease_until IS NULL OR lease_until<=?)').bind(token,clock+45000,row.profile_id,row.event_id,row.state,row.attempt,clock).run();if(!leased.meta.changes)continue;
  if(row.state==='dispatching'){
   let receipt;try{receipt=await authReceipt(env,row);}catch{receipt=unknown(row);}await save(env,row,token,receipt,clock);continue;
  }
  try{
   const s=await eventSubscription(env,who(row),row.subscription_id);
   if(!s||s.state!=='active'||s.accessEpoch!==row.access_epoch||s.consentEpoch!==row.consent_epoch||s.cue!==row.cue||s.cueRevision!==row.cue_revision)throw new WorkspaceError('event_authority_changed',409);
   if(row.expires_at<=clock||s.expiresAt<=clock){await save(env,row,token,{...unknown(row),state:'expired',acceptedByHost:false},clock);continue;}
   const attempt=row.attempt+1;if(attempt>3){await save(env,row,token,{...unknown(row),state:'refused',code:'retry_exhausted',acceptedByHost:false},clock);continue;}
   const saved=await db.prepare("UPDATE workspace_event_outbox SET state='dispatching',attempt=? WHERE profile_id=? AND event_id=? AND lease_token=? AND state=? AND attempt=?").bind(attempt,row.profile_id,row.event_id,token,row.state,row.attempt).run();if(!saved.meta.changes)continue;
   row={...row,state:'dispatching',attempt};
   let value:Receipt;
   try{
    const response=await env.AUTH_EVENTS.fetch('https://events.internal/dispatch',{method:'POST',body:JSON.stringify({principal:who(row),eventId:row.event_id,attempt,subscriptionId:s.subscriptionId,revision:s.revision,consentEpoch:s.consentEpoch,atUtc:s.cue}),signal:AbortSignal.timeout(18000)});
    if(!response.ok){if([401,403,409].includes(response.status))value={...unknown(row),state:'cancelled',code:'current_authority_refused',acceptedByHost:false};else throw new Error();}
    else{value=await response.json<Receipt>();if(value.eventId!==row.event_id||value.attempt!==attempt)throw new Error();}
   }catch{try{value=await authReceipt(env,row);}catch{value=unknown(row);}}
   await save(env,row,token,value,clock);
  }catch{await save(env,row,token,{...unknown(row),state:'cancelled',code:'event_authority_changed',acceptedByHost:false},clock);}
 }
 // Retain original outcomes for30days; bounded cleanup never deletes an in-flight receipt.
 await db.prepare("DELETE FROM workspace_event_outbox WHERE rowid IN (SELECT rowid FROM workspace_event_outbox WHERE created_at<? AND state NOT IN ('dispatching','retryable','scheduled') LIMIT 128)").bind(clock-30*86400000).run();
}
export async function eventDeliveryReceipt(env:EventsWorkspaceEnv,principal:EventPrincipal,value:unknown){
 const v=workspaceObject(value,['eventId']),id=uuid(v.eventId),{document}=await eventWorkspace(env,principal,undefined,false),row=await env.WORKSPACE_DB.prepare('SELECT * FROM workspace_event_outbox WHERE profile_id=? AND event_id=?').bind(principal.profileId,id).first<Outbox>();
 if(!row)return {eventId:id,outcomeUnknown:true,receipt:null,chatResponse:null};
 if(!document.tasks.some(t=>t.id===row.task_id))throw new WorkspaceError('resource_access_denied',403);
 let receipt:Receipt|null=row.receipt_json?JSON.parse(row.receipt_json):null;
 if(row.state==='dispatching'||row.state==='unknown'&&row.attempt>0)try{receipt=await authReceipt(env,row);}catch{receipt=unknown(row);}
 return {eventId:id,taskId:row.task_id,cue:row.cue,state:receipt?.state??row.state,attempt:row.attempt,receipt,acceptedByHost:receipt?.acceptedByHost??(row.attempt===0?false:null),chatResponse:null};
}
