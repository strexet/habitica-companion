import {cronInput,type CronTool} from '../../shared/cron-tools';
import {WorkspaceError,workspaceSessionToken,workspaceTokenHash,workspaceObject} from '../../shared/workspace-http';
import type {WorkspaceRow} from './executor';
interface Payload {expectedPreconditionHash:string;expectedDay:string;expectedLastCron:string}
interface Entry {actionId:string;requestId:string;preview:Record<string,unknown>;attempted:boolean;confirmed:boolean;outcomeUnknown:boolean;failure:string|null;refreshSucceeded:boolean|null;observedProgress:Record<string,unknown>|null}
interface Action {grant_epoch:string;profile_id:string;action_id:string;request_id:string;connection_id:string;access_epoch:string;status:string;version:number;payload_json:string;entry_json:string;attempt_json:string|null;expires_at_utc:string;consumed_at_utc:string|null}
interface Attempt {profileId:string;connectionId:string;operation:string;arguments:Payload;attemptId:string;attemptAtUtc:string}
const now=()=>new Date().toISOString();
const view=(a:Action)=>({...JSON.parse(a.entry_json) as Entry,status:a.status,accepted:a.consumed_at_utc!==null,expiresAtUtc:a.expires_at_utc});
const action=(env:WorkspaceEnv,profile:string,id:string)=>env.WORKSPACE_DB.prepare('SELECT * FROM prepared_cron_actions WHERE profile_id=? AND action_id=?').bind(profile,id).first<Action>();
async function current(env:WorkspaceEnv,row:WorkspaceRow){
 if(!await env.WORKSPACE_DB.prepare('SELECT 1 FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1').bind(row.profile_id,row.connection_id,row.access_epoch).first())throw new WorkspaceError('unauthorized',401);
}
async function gateway(env:WorkspaceEnv,row:WorkspaceRow,authorization:string,operation:string,args:unknown,attempt?:Attempt,timeout=55000){
 const response=await env.AUTH_CRON.fetch('https://cron.internal/operation',{method:'POST',headers:{authorization,'content-type':'application/json'},body:JSON.stringify(attempt?{...attempt,operation}:{profileId:row.profile_id,connectionId:row.connection_id,operation,arguments:args}),signal:AbortSignal.timeout(timeout)});
 const data=workspaceObject(await response.json());
 if(operation==='lookup'&&!response.ok)return {kind:'unknown',code:'receipt_unavailable'};
 if((operation==='status'||operation==='progress')&&!response.ok)throw new WorkspaceError(typeof data.code==='string'?data.code:'cron_unavailable',response.status);
 return data;
}
export async function cronHistory(env:WorkspaceEnv,profile:string){
 const rows=await env.WORKSPACE_DB.prepare('SELECT * FROM prepared_cron_actions WHERE profile_id=? ORDER BY created_at_utc,action_id LIMIT 1001').bind(profile).all<Action>();
 if(rows.results.length>1000)throw new WorkspaceError('export_capacity',413);
 return {schemaVersion:1,cron:rows.results.map(view)};
}
export async function cronApi(env:WorkspaceEnv,row:WorkspaceRow,tool:CronTool,args:unknown,authorization:string){
 const input=cronInput(tool,args);
 if(tool==='get_cron_command'){
  const found=await action(env,row.profile_id,String(input.actionId));await current(env,row);
  if(!found)return {actionId:input.actionId,accepted:null,outcomeUnknown:true};
  if(['accepted','dispatching','unknownOutcome'].includes(found.status))return recover(env,row,found,authorization);
  return view(found);
 }
 if(!env.CRON_ENABLED)throw new WorkspaceError('cron_unavailable',503);
 if(tool==='get_cron_status'){
  const observed=await gateway(env,row,authorization,'status',{});await current(env,row);return workspaceObject(observed.context);
 }
 if(tool==='prepare_start_new_day'){
  let a=await env.WORKSPACE_DB.prepare('SELECT * FROM prepared_cron_actions WHERE profile_id=? AND request_id=?').bind(row.profile_id,input.requestId).first<Action>();
  if(!a){
   const observed=await gateway(env,row,authorization,'status',{});await current(env,row);
   const context=workspaceObject(observed.context);
   if(context.canPrepare!==true)throw new WorkspaceError(typeof context.unavailableReason==='string'?context.unavailableReason:'cron_precondition_unknown',409);
   if(typeof observed.preconditionHash!=='string'||!/^[a-f0-9]{64}$/.test(observed.preconditionHash)||typeof context.day!=='string'||typeof context.lastCron!=='string')throw new WorkspaceError('invalid_cron_context',502);
   const id=crypto.randomUUID(),expires=new Date(Date.now()+120000).toISOString();
   const e:Entry={actionId:id,requestId:String(input.requestId),preview:context,attempted:false,confirmed:false,outcomeUnknown:false,failure:null,refreshSucceeded:null,observedProgress:null};
   const payload:Payload={expectedPreconditionHash:observed.preconditionHash,expectedDay:context.day,expectedLastCron:context.lastCron};
   await env.WORKSPACE_DB.prepare(`INSERT OR IGNORE INTO prepared_cron_actions(profile_id,action_id,request_id,input_hash,connection_id,access_epoch,status,payload_json,entry_json,created_at_utc,expires_at_utc,grant_epoch) SELECT profile_id,?,?,?,?,?,'prepared',?,?,?,?,? FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1 AND (SELECT COUNT(*) FROM prepared_cron_actions WHERE profile_id=?)<1000`)
    .bind(id,input.requestId,await workspaceTokenHash('start-new-day-v1'),row.connection_id,row.access_epoch,JSON.stringify(payload),JSON.stringify(e),now(),expires,row.mcp_grant_epoch??'legacy',row.profile_id,row.connection_id,row.access_epoch,row.profile_id).run();
   a=await env.WORKSPACE_DB.prepare('SELECT * FROM prepared_cron_actions WHERE profile_id=? AND request_id=?').bind(row.profile_id,input.requestId).first<Action>();
   if(!a)throw new WorkspaceError('cron_conflict_or_capacity',409);
  }
  await current(env,row);
  if(a.status!=='prepared')return view(a);
  if(a.grant_epoch!==(row.mcp_grant_epoch??'legacy')||a.connection_id!==row.connection_id||a.access_epoch!==row.access_epoch||a.expires_at_utc<=now())throw new WorkspaceError('preview_expired_or_changed',409);
  const token=workspaceSessionToken();
  const rotated=await env.WORKSPACE_DB.prepare("UPDATE prepared_cron_actions SET token_hash=?,version=version+1 WHERE profile_id=? AND action_id=? AND status='prepared' AND version=? AND expires_at_utc>? AND EXISTS(SELECT 1 FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1)").bind(await workspaceTokenHash(token),row.profile_id,a.action_id,a.version,now(),row.profile_id,row.connection_id,row.access_epoch).run();
  if(!rotated.meta.changes)throw new WorkspaceError('preview_changed',409);
  return {...view(a),executeToken:token,tokenNotice:'Review this exact day, unfinished Dailies, expiry and warnings. Only the latest token is valid. Explicit approval is required.'};
 }
 const found=await action(env,row.profile_id,String(input.actionId));await current(env,row);
 if(!found)throw new WorkspaceError('preview_required',409);
 if(found.status!=='prepared')return view(found);
 return execute(env,row,found,String(input.executeToken),authorization);
}
async function execute(env:WorkspaceEnv,row:WorkspaceRow,initial:Action,token:string,authorization:string){
 if(initial.grant_epoch!==(row.mcp_grant_epoch??'legacy')||initial.connection_id!==row.connection_id||initial.access_epoch!==row.access_epoch||initial.expires_at_utc<=now())throw new WorkspaceError('preview_expired_or_changed',409);
 if(row.authority_json&&JSON.parse(row.authority_json).mode!=='cloud')throw new WorkspaceError('workspace_authority_required',409);
 const lease=crypto.randomUUID(),deadline=Date.now()+60000;
 const acquired=await env.WORKSPACE_DB.prepare(`INSERT INTO workspace_runtime(profile_id,access_epoch,lease_token,lease_until_utc,last_wake_at_utc) SELECT profile_id,access_epoch,?,?,? FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1 ON CONFLICT(profile_id) DO UPDATE SET access_epoch=excluded.access_epoch,lease_token=excluded.lease_token,lease_until_utc=excluded.lease_until_utc,last_wake_at_utc=excluded.last_wake_at_utc WHERE workspace_runtime.lease_until_utc IS NULL OR workspace_runtime.lease_until_utc<=?`)
  .bind(lease,new Date(Date.now()+90000).toISOString(),now(),row.profile_id,row.connection_id,row.access_epoch,now()).run();
 if(!acquired.meta.changes){const duplicate=await action(env,row.profile_id,initial.action_id);if(duplicate&&duplicate.status!=='prepared')return view(duplicate);throw new WorkspaceError('gameplay_busy',409);}
 let a=initial;let e=JSON.parse(a.entry_json) as Entry;
 const payload=JSON.parse(a.payload_json) as Payload;
 const persist=async(status:string,attempt:Attempt|null)=>{
  const result=await env.WORKSPACE_DB.prepare(`UPDATE prepared_cron_actions SET status=?,entry_json=?,attempt_json=?,version=version+1 WHERE profile_id=? AND action_id=? AND version=? AND EXISTS(SELECT 1 FROM workspace_runtime WHERE profile_id=? AND access_epoch=? AND lease_token=? AND lease_until_utc>?) AND EXISTS(SELECT 1 FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1)`)
   .bind(status,JSON.stringify(e),attempt?JSON.stringify(attempt):null,row.profile_id,a.action_id,a.version,row.profile_id,row.access_epoch,lease,now(),row.profile_id,row.connection_id,row.access_epoch).run();
  if(!result.meta.changes)throw new WorkspaceError('cron_execution_interrupted',409);
  a={...a,status,entry_json:JSON.stringify(e),attempt_json:attempt?JSON.stringify(attempt):null,version:a.version+1};
 };
 try{
  for(const table of ['prepared_spell_actions','prepared_cron_actions'])if(await env.WORKSPACE_DB.prepare(`SELECT 1 FROM ${table} WHERE profile_id=? AND status IN ('accepted','dispatching','unknownOutcome') LIMIT 1`).bind(row.profile_id).first())throw new WorkspaceError('unknown_gameplay_pending',409);
  if(await env.WORKSPACE_DB.prepare("SELECT 1 FROM workspace_commands WHERE profile_id=? AND json_extract(entry_json,'$.projectionState') IN ('executing','unknownOutcome','leased') LIMIT 1").bind(row.profile_id).first())throw new WorkspaceError('unknown_gameplay_pending',409);
  const accepted=await env.WORKSPACE_DB.prepare(`UPDATE prepared_cron_actions SET status='accepted',token_hash=NULL,consumed_at_utc=?,version=version+1 WHERE profile_id=? AND action_id=? AND version=? AND status='prepared' AND token_hash=? AND expires_at_utc>? AND EXISTS(SELECT 1 FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1)`)
   .bind(now(),row.profile_id,a.action_id,a.version,await workspaceTokenHash(token),now(),row.profile_id,row.connection_id,row.access_epoch).run();
  if(!accepted.meta.changes){const duplicate=await action(env,row.profile_id,a.action_id);if(duplicate&&duplicate.status!=='prepared')return view(duplicate);throw new WorkspaceError('preview_token_invalid_or_changed',409);}
  a=(await action(env,row.profile_id,a.action_id))!;
  const attempt:Attempt={profileId:row.profile_id,connectionId:row.connection_id,operation:'execute',arguments:payload,attemptId:crypto.randomUUID(),attemptAtUtc:now()};
  e.attempted=true;e.outcomeUnknown=true;await persist('dispatching',attempt);
  let outcome:Record<string,unknown>;try{await current(env,row);outcome=await gateway(env,row,authorization,'execute',{},attempt,50000);}catch{outcome={kind:'unknown',code:'transport_unknown'};}
  e.confirmed=outcome.kind==='applied'&&outcome.code==='applied';e.outcomeUnknown=!e.confirmed&&outcome.kind!=='refused';e.failure=e.confirmed?null:typeof outcome.code==='string'?outcome.code:'outcome_unknown';
  await persist(e.confirmed?'confirmed':e.outcomeUnknown?'unknownOutcome':'stopped',e.outcomeUnknown?attempt:null);
  if(e.confirmed){
   // Observation refresh is separate from the immutable original completion receipt.
   try{const remaining=deadline-Date.now();if(remaining<1000)throw new Error('refresh_deadline');await gateway(env,row,authorization,'status',{},undefined,Math.min(30000,remaining));e.refreshSucceeded=true;}catch{e.refreshSucceeded=false;}
   await persist('confirmed',null);
  }
  await current(env,row);return view(a);
 }catch(error){
  if(a.status==='prepared')throw error;
  if(a.status==='accepted'){
   e.outcomeUnknown=false;e.failure='execution_stopped';try{await persist('stopped',null);}catch{/* Accepted state remains a durable fence until recovery. */}
  }
  await current(env,row);return view(a);
 }finally{
  try{await env.WORKSPACE_DB.prepare('UPDATE workspace_runtime SET lease_token=NULL,lease_until_utc=NULL WHERE profile_id=? AND access_epoch=? AND lease_token=?').bind(row.profile_id,row.access_epoch,lease).run();}catch{/* Expiry bounds ownership. */}
 }
}
async function recover(env:WorkspaceEnv,row:WorkspaceRow,a:Action,authorization:string){
 const runtime=await env.WORKSPACE_DB.prepare('SELECT lease_until_utc FROM workspace_runtime WHERE profile_id=?').bind(row.profile_id).first<{lease_until_utc:string|null}>();
 if(runtime?.lease_until_utc&&runtime.lease_until_utc>now())return {...view(a),outcomeUnknown:true};
 const e=JSON.parse(a.entry_json) as Entry;
 if(a.attempt_json){
  let outcome:Record<string,unknown>={kind:'unknown',code:'receipt_unavailable'};
  try{outcome=await gateway(env,row,authorization,'lookup',{},JSON.parse(a.attempt_json));}catch{}
  e.confirmed=outcome.kind==='applied'&&outcome.code==='applied';e.outcomeUnknown=!e.confirmed&&outcome.kind!=='refused';e.failure=e.confirmed?null:typeof outcome.code==='string'?outcome.code:'outcome_unknown';
  if(e.outcomeUnknown)try{e.observedProgress=await gateway(env,row,authorization,'progress',{previousLastCron:JSON.parse(a.payload_json).expectedLastCron},undefined,15000);}catch{e.observedProgress=null;}
 }else{e.outcomeUnknown=false;e.failure='execution_stopped';}
 if(e.confirmed)e.refreshSucceeded=false;
 await current(env,row);
 const status=e.confirmed?'confirmed':e.outcomeUnknown?'unknownOutcome':'stopped';
 await env.WORKSPACE_DB.prepare(`UPDATE prepared_cron_actions SET status=?,entry_json=?,version=version+1 WHERE profile_id=? AND action_id=? AND version=? AND NOT EXISTS(SELECT 1 FROM workspace_runtime WHERE profile_id=? AND lease_until_utc>?) AND EXISTS(SELECT 1 FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1)`)
  .bind(status,JSON.stringify(e),row.profile_id,a.action_id,a.version,row.profile_id,now(),row.profile_id,row.connection_id,row.access_epoch).run();
 await current(env,row);return view((await action(env,row.profile_id,a.action_id))!);
}
