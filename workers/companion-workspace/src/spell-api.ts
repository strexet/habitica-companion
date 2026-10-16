import {spellInput,type SpellTool} from '../../shared/spell-tools';
import {supportedSpell,spellEligibility} from '../../shared/spells';
import {WorkspaceError,workspaceSessionToken,workspaceTokenHash,workspaceObject,uuid} from '../../shared/workspace-http';
import {requireCurrentPartyAccess} from './party-access';
import type {WorkspaceRow} from './executor';
interface Payload {spellId:'healAll';partyId:string;expectedPreconditionHash:string;count:number}
interface Entry {actionId:string;requestId:string;spellId:string;requestedCount:number;attemptedCount:number;confirmedCount:number;failurePosition:number|null;failure:string|null;retryAtUtc:string|null;refreshSucceeded:boolean|null;outcomeUnknown:boolean;mana:number|null;health:number|null;preview:Record<string,unknown>}
interface Action {profile_id:string;action_id:string;request_id:string;input_hash:string;connection_id:string;access_epoch:string;namespace_id:string;status:string;version:number;payload_json:string;entry_json:string;attempt_json:string|null;expires_at_utc:string;consumed_at_utc:string|null}
interface Attempt {profileId:string;connectionId:string;operation:string;arguments:{spellId:string;partyId:string;expectedPreconditionHash:string;remainingCount:number};attemptId:string;attemptAtUtc:string}
interface Outcome {kind:string;code:string;mana?:number;health?:number;nextPreconditionHash?:string;retryAtUtc?:string}
const now=()=>new Date().toISOString();
const current=(row:WorkspaceRow)=>({profileId:row.profile_id,connectionId:row.connection_id,accessEpoch:row.access_epoch});
const view=(a:Action)=>({...JSON.parse(a.entry_json) as Entry,status:a.status,accepted:a.consumed_at_utc!==null,expiresAtUtc:a.expires_at_utc});
const action=(env:WorkspaceEnv,profile:string,id:string)=>env.WORKSPACE_DB.prepare('SELECT * FROM prepared_spell_actions WHERE profile_id=? AND action_id=?').bind(profile,id).first<Action>();
async function gateway(env:WorkspaceEnv,row:WorkspaceRow,authorization:string,operation:string,args:unknown,attempt?:Attempt){
 const response=await env.AUTH_SPELL.fetch('https://spell.internal/operation',{method:'POST',headers:{authorization,'content-type':'application/json'},body:JSON.stringify(attempt?{...attempt,operation}:{profileId:row.profile_id,connectionId:row.connection_id,operation,arguments:args}),signal:AbortSignal.timeout(35000)});
 const data=await response.json<Record<string,unknown>>();
 if(operation==='context'&&!response.ok){
  if(response.status===404||data.code==='party_changed')await env.WORKSPACE_DB.batch([env.WORKSPACE_DB.prepare('DELETE FROM authenticated_party_consents WHERE profile_id=?').bind(row.profile_id),env.WORKSPACE_DB.prepare("DELETE FROM prepared_spell_actions WHERE profile_id=? AND status='prepared'").bind(row.profile_id)]);
  throw new WorkspaceError(typeof data.code==='string'?data.code:'spell_unavailable',response.status);
 }
 return data;
}
async function planning(env:WorkspaceEnv,row:WorkspaceRow,partyId:string,namespace?:string){
 const p=await env.WORKSPACE_DB.prepare(`SELECT p.namespace_id FROM authenticated_party_plans p JOIN authenticated_party_consents c ON c.namespace_id=p.namespace_id AND c.party_id=p.party_id JOIN workspaces w ON w.profile_id=c.profile_id WHERE c.profile_id=? AND c.connection_id=? AND c.access_epoch=? AND p.party_id=? AND w.active=1 AND w.connection_id=c.connection_id AND w.access_epoch=c.access_epoch`).bind(row.profile_id,row.connection_id,row.access_epoch,partyId).first<{namespace_id:string}>();
 if(!p||namespace&&p.namespace_id!==namespace)throw new WorkspaceError('party_consent_required',403);return p.namespace_id;
}
const taskFence=(env:WorkspaceEnv,profile:string)=>env.WORKSPACE_DB.prepare("SELECT 1 FROM workspace_commands WHERE profile_id=? AND json_extract(entry_json,'$.projectionState') IN ('executing','unknownOutcome','leased') LIMIT 1").bind(profile).first();
export async function spellHistory(env:WorkspaceEnv,profile:string){
 const rows=await env.WORKSPACE_DB.prepare('SELECT * FROM prepared_spell_actions WHERE profile_id=? ORDER BY created_at_utc,action_id LIMIT 1001').bind(profile).all<Action>();
 if(rows.results.length>1000)throw new WorkspaceError('export_capacity',413);
 // Explicit projection: token hashes, private dispatch payloads and original bearers never enter export.
 return {schemaVersion:1,spells:rows.results.map(view)};
}
export async function spellApi(env:WorkspaceEnv,row:WorkspaceRow,tool:SpellTool,args:unknown,authorization:string){
 const input=spellInput(tool,args);
 if(tool==='get_spell_command'){
  const found=await action(env,row.profile_id,String(input.actionId));
  if(!found)return {actionId:input.actionId,accepted:null,outcomeUnknown:true};
  if(['accepted','dispatching','unknownOutcome'].includes(found.status))return recover(env,row,found,authorization);
  return view(found);
 }
 if(!env.SPELLS_ENABLED||!env.PARTY_ENABLED)throw new WorkspaceError('spell_unavailable',503);
 if(tool==='get_spell_context'){
  const observed=await gateway(env,row,authorization,'context',{});await requireCurrentPartyAccess(env,current(row));return observed.context as Record<string,unknown>;
 }
 if(tool==='prepare_spell_cast'){
  const hash=await workspaceTokenHash(JSON.stringify([input.spellId,input.count]));
  let prepared=await env.WORKSPACE_DB.prepare('SELECT * FROM prepared_spell_actions WHERE profile_id=? AND request_id=?').bind(row.profile_id,input.requestId).first<Action>();
  if(prepared&&prepared.input_hash!==hash)throw new WorkspaceError('request_id_reused',409);
  if(!prepared){
   const observed=await gateway(env,row,authorization,'context',{});await requireCurrentPartyAccess(env,current(row));
   const context=workspaceObject(observed.context);const party=workspaceObject(context.party);const partyId=uuid(party.id);
   const reason=spellEligibility({className:String(context.className),level:Number(context.level),mana:Number(context.mana),health:Number(context.health),classesDisabled:context.classesDisabled===true},Number(input.count));
   if(reason)throw new WorkspaceError(reason,409);
   if(typeof observed.preconditionHash!=='string'||!/^[a-f0-9]{64}$/.test(observed.preconditionHash))throw new WorkspaceError('invalid_spell_context',502);
   const namespace=await planning(env,row,partyId);const id=crypto.randomUUID();const expires=new Date(Date.now()+120000).toISOString();
   const entry:Entry={actionId:id,requestId:String(input.requestId),spellId:supportedSpell.id,requestedCount:Number(input.count),attemptedCount:0,confirmedCount:0,failurePosition:null,failure:null,retryAtUtc:null,refreshSucceeded:null,outcomeUnknown:false,mana:null,health:null,preview:{...context,totalMana:supportedSpell.manaCost*Number(input.count),target:'verified-current-party',repetitions:Number(input.count)}};
   const payload:Payload={spellId:supportedSpell.id,partyId,expectedPreconditionHash:observed.preconditionHash,count:Number(input.count)};
   await env.WORKSPACE_DB.prepare(`INSERT OR IGNORE INTO prepared_spell_actions(profile_id,action_id,request_id,input_hash,connection_id,access_epoch,namespace_id,status,payload_json,entry_json,created_at_utc,expires_at_utc) SELECT profile_id,?,?,?,?,?,?,'prepared',?,?,?,? FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1 AND (SELECT COUNT(*) FROM prepared_spell_actions WHERE profile_id=?)<1000 AND EXISTS(SELECT 1 FROM authenticated_party_consents c WHERE c.profile_id=workspaces.profile_id AND c.namespace_id=? AND c.connection_id=workspaces.connection_id AND c.access_epoch=workspaces.access_epoch)`)
    .bind(id,input.requestId,hash,row.connection_id,row.access_epoch,namespace,JSON.stringify(payload),JSON.stringify(entry),now(),expires,row.profile_id,row.connection_id,row.access_epoch,row.profile_id,namespace).run();
   prepared=await env.WORKSPACE_DB.prepare('SELECT * FROM prepared_spell_actions WHERE profile_id=? AND request_id=?').bind(row.profile_id,input.requestId).first<Action>();
   if(!prepared)throw new WorkspaceError('spell_conflict_or_capacity',409);
   if(prepared.input_hash!==hash)throw new WorkspaceError('request_id_reused',409);
  }
  if(prepared.status!=='prepared')return view(prepared);
  if(prepared.connection_id!==row.connection_id||prepared.access_epoch!==row.access_epoch||prepared.expires_at_utc<=now())throw new WorkspaceError('preview_expired_or_changed',409);
  await planning(env,row,JSON.parse(prepared.payload_json).partyId,prepared.namespace_id);
  const token=workspaceSessionToken();
  const rotated=await env.WORKSPACE_DB.prepare("UPDATE prepared_spell_actions SET token_hash=?,version=version+1 WHERE profile_id=? AND action_id=? AND status='prepared' AND version=? AND expires_at_utc>? AND EXISTS(SELECT 1 FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1)").bind(await workspaceTokenHash(token),row.profile_id,prepared.action_id,prepared.version,now(),row.profile_id,row.connection_id,row.access_epoch).run();
  if(!rotated.meta.changes)throw new WorkspaceError('preview_changed',409);
  return {...view(prepared),executeToken:token,tokenNotice:'Only the latest returned preview token is valid. Review this exact action before execution.'};
 }
 const found=await action(env,row.profile_id,String(input.actionId));if(!found)throw new WorkspaceError('preview_required',409);
 if(found.status!=='prepared')return view(found);
 return execute(env,row,found,String(input.executeToken),authorization);
}

async function execute(env:WorkspaceEnv,row:WorkspaceRow,initial:Action,token:string,authorization:string){
 const payload=JSON.parse(initial.payload_json) as Payload;
 if(initial.access_epoch!==row.access_epoch||initial.connection_id!==row.connection_id||initial.expires_at_utc<=now())throw new WorkspaceError('preview_expired_or_changed',409);
 await planning(env,row,payload.partyId,initial.namespace_id);
 if(row.authority_json&&JSON.parse(row.authority_json).mode!=='cloud')throw new WorkspaceError('workspace_authority_required',409);
 const lease=crypto.randomUUID();const until=new Date(Date.now()+90000).toISOString();
 const acquired=await env.WORKSPACE_DB.prepare(`INSERT INTO workspace_runtime(profile_id,access_epoch,lease_token,lease_until_utc,last_wake_at_utc) SELECT profile_id,access_epoch,?,?,? FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1 ON CONFLICT(profile_id) DO UPDATE SET access_epoch=excluded.access_epoch,lease_token=excluded.lease_token,lease_until_utc=excluded.lease_until_utc,last_wake_at_utc=excluded.last_wake_at_utc WHERE workspace_runtime.lease_until_utc IS NULL OR workspace_runtime.lease_until_utc<=?`).bind(lease,until,now(),row.profile_id,row.connection_id,row.access_epoch,now()).run();
 if(!acquired.meta.changes){const duplicate=await action(env,row.profile_id,initial.action_id);if(duplicate?.status!=='prepared'&&duplicate)return view(duplicate);throw new WorkspaceError('gameplay_busy',409);}
 let a=initial;let e=JSON.parse(a.entry_json) as Entry;let dispatched=false;const deadline=Date.now()+55000;
 async function save(status:string,attempt:Attempt|null,gate=false){
  const result=await env.WORKSPACE_DB.prepare(`UPDATE prepared_spell_actions SET status=?,entry_json=?,attempt_json=?,version=version+1 WHERE profile_id=? AND action_id=? AND version=? AND EXISTS(SELECT 1 FROM workspace_runtime r WHERE r.profile_id=? AND r.access_epoch=? AND r.lease_token=? AND r.lease_until_utc>?) ${gate?"AND EXISTS(SELECT 1 FROM workspaces w JOIN authenticated_party_consents c ON c.profile_id=w.profile_id JOIN authenticated_party_plans p ON p.namespace_id=c.namespace_id WHERE w.profile_id=? AND w.active=1 AND w.connection_id=? AND w.access_epoch=? AND c.connection_id=w.connection_id AND c.access_epoch=w.access_epoch AND c.namespace_id=?)":''}`)
   .bind(status,JSON.stringify(e),attempt?JSON.stringify(attempt):null,row.profile_id,a.action_id,a.version,row.profile_id,row.access_epoch,lease,now(),...(gate?[row.profile_id,row.connection_id,row.access_epoch,a.namespace_id]:[])).run();
  if(!result.meta.changes)throw new WorkspaceError('spell_execution_interrupted',409);a={...a,status,entry_json:JSON.stringify(e),attempt_json:attempt?JSON.stringify(attempt):null,version:a.version+1};
 }
 try{
  if(await env.WORKSPACE_DB.prepare("SELECT 1 FROM prepared_cron_actions WHERE profile_id=? AND status IN ('accepted','dispatching','unknownOutcome') LIMIT 1").bind(row.profile_id).first()||await taskFence(env,row.profile_id)||await env.WORKSPACE_DB.prepare("SELECT 1 FROM prepared_spell_actions WHERE profile_id=? AND status IN ('accepted','dispatching','unknownOutcome') LIMIT 1").bind(row.profile_id).first())throw new WorkspaceError('unknown_gameplay_pending',409);
  const accepted=await env.WORKSPACE_DB.prepare(`UPDATE prepared_spell_actions SET status='accepted',token_hash=NULL,consumed_at_utc=?,version=version+1 WHERE profile_id=? AND action_id=? AND version=? AND status='prepared' AND token_hash=? AND expires_at_utc>? AND EXISTS(SELECT 1 FROM workspaces w JOIN authenticated_party_consents c ON c.profile_id=w.profile_id JOIN authenticated_party_plans p ON p.namespace_id=c.namespace_id WHERE w.profile_id=? AND w.active=1 AND w.connection_id=? AND w.access_epoch=? AND c.connection_id=w.connection_id AND c.access_epoch=w.access_epoch AND c.namespace_id=?)`)
   .bind(now(),row.profile_id,a.action_id,a.version,await workspaceTokenHash(token),now(),row.profile_id,row.connection_id,row.access_epoch,a.namespace_id).run();
  if(!accepted.meta.changes){const duplicate=await action(env,row.profile_id,a.action_id);if(duplicate&&duplicate.status!=='prepared')return view(duplicate);throw new WorkspaceError('preview_token_invalid_or_changed',409);}a=(await action(env,row.profile_id,a.action_id))!;
  let hash=payload.expectedPreconditionHash;
  for(let i=0;i<payload.count;i++){
   if(Date.now()+35000>deadline){e.failure='foreground_deadline';e.failurePosition=i+1;await save('stopped',null);break;}
   await requireCurrentPartyAccess(env,current(row));await planning(env,row,payload.partyId,a.namespace_id);
   const attempt:Attempt={profileId:row.profile_id,connectionId:row.connection_id,operation:'cast',arguments:{spellId:payload.spellId,partyId:payload.partyId,expectedPreconditionHash:hash,remainingCount:payload.count-i},attemptId:crypto.randomUUID(),attemptAtUtc:now()};
   e.attemptedCount++;e.outcomeUnknown=true;await save('dispatching',attempt,true);dispatched=true;
   let result:Outcome;try{result=await gateway(env,row,authorization,'cast',{},attempt) as unknown as Outcome;}catch{result={kind:'unknown',code:'transport_unknown'};}
   if(result.kind==='applied'&&typeof result.mana==='number'&&Number.isFinite(result.mana)&&typeof result.health==='number'&&Number.isFinite(result.health)&&typeof result.nextPreconditionHash==='string'&&/^[a-f0-9]{64}$/.test(result.nextPreconditionHash)){
    e.confirmedCount++;e.mana=result.mana;e.health=result.health;e.outcomeUnknown=false;hash=result.nextPreconditionHash;
    await save(i+1===payload.count?'confirmed':'accepted',null);dispatched=false;
   }else{
    const known=result.kind==='refused';e.outcomeUnknown=!known;e.failure=typeof result.code==='string'?result.code:'outcome_unknown';e.failurePosition=i+1;e.retryAtUtc=result.retryAtUtc??null;
    await save(known?'stopped':'unknownOutcome',known?null:attempt);dispatched=false;break;
   }
  }
  if(a.status==='confirmed'||e.confirmedCount>0&&!e.outcomeUnknown){
   try{if(Date.now()+35000>deadline)throw new Error('refresh_deadline');await requireCurrentPartyAccess(env,current(row));await gateway(env,row,authorization,'context',{});await requireCurrentPartyAccess(env,current(row));e.refreshSucceeded=true;}catch{e.refreshSucceeded=false;}
   await save(a.status,a.attempt_json?JSON.parse(a.attempt_json):null);
  }
  return view(a);
 }catch(error){
  if(a.status!=='prepared'){
   if(['confirmed','stopped','unknownOutcome'].includes(a.status))return view(a);
   e=JSON.parse(a.entry_json) as Entry;
   e.outcomeUnknown=dispatched;e.failure=dispatched?'outcome_unknown':'execution_stopped';e.failurePosition=e.confirmedCount+1;
   try{await save(dispatched?'unknownOutcome':'stopped',a.attempt_json?JSON.parse(a.attempt_json):null);}catch{/* Saved accepted/dispatching state still fences all future effects. */}
   return view(a);
  }
  throw error;
 }finally{await env.WORKSPACE_DB.prepare('UPDATE workspace_runtime SET lease_token=NULL,lease_until_utc=NULL WHERE profile_id=? AND access_epoch=? AND lease_token=?').bind(row.profile_id,row.access_epoch,lease).run();}
}

async function recover(env:WorkspaceEnv,row:WorkspaceRow,a:Action,authorization:string){
 const runtime=await env.WORKSPACE_DB.prepare('SELECT lease_until_utc FROM workspace_runtime WHERE profile_id=?').bind(row.profile_id).first<{lease_until_utc:string|null}>();
 if(runtime?.lease_until_utc&&runtime.lease_until_utc>now())return {...view(a),outcomeUnknown:true};
 const e=JSON.parse(a.entry_json) as Entry;
 if(['dispatching','unknownOutcome'].includes(a.status)&&a.attempt_json){
  let result:Outcome={kind:'unknown',code:'receipt_unavailable'};
  try{result=await gateway(env,row,authorization,'lookup',{},JSON.parse(a.attempt_json)) as unknown as Outcome;}catch{}
  if(result.kind==='applied'&&typeof result.mana==='number'&&Number.isFinite(result.mana)&&typeof result.health==='number'&&Number.isFinite(result.health)){
   e.confirmedCount++;e.mana=typeof result.mana==='number'?result.mana:null;e.health=typeof result.health==='number'?result.health:null;e.outcomeUnknown=false;e.failure=e.confirmedCount<e.requestedCount?'interrupted_remainder':null;e.failurePosition=e.failure?e.confirmedCount+1:null;
  }else{e.outcomeUnknown=result.kind!=='refused';e.failure=result.code??'outcome_unknown';e.failurePosition=e.confirmedCount+1;e.retryAtUtc=result.retryAtUtc??null;}
 }else{e.outcomeUnknown=false;e.failure='interrupted_remainder';e.failurePosition=e.confirmedCount+1;}
 const status=e.outcomeUnknown?'unknownOutcome':e.confirmedCount===e.requestedCount?'confirmed':'stopped';e.refreshSucceeded=e.confirmedCount>0?false:null;
 await env.WORKSPACE_DB.prepare("UPDATE prepared_spell_actions SET status=?,entry_json=?,version=version+1 WHERE profile_id=? AND action_id=? AND version=? AND NOT EXISTS(SELECT 1 FROM workspace_runtime WHERE profile_id=? AND lease_until_utc>?)").bind(status,JSON.stringify(e),row.profile_id,a.action_id,a.version,row.profile_id,now()).run();
 return view((await action(env,row.profile_id,a.action_id))!);
}
