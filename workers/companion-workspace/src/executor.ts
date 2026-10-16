import { event, type Authority } from './journal.ts';
import type { CommandEntry, JournalEvent } from './commands.ts';
import { projectionStep, confirmMapping, type Observation, type AccountObservation } from './projection-adapter.ts';
import type { WorkspaceDocument } from '../../shared/workspace-domain.ts';
export interface WorkspaceRow {profile_id:string;workspace_id:string;connection_id:string;access_epoch:string;active:number;executor_enabled:number;document_json:string;store_version:number;archive_bytes:number;commit_tag:string;authority_json:string|null}
interface Runtime {access_epoch:string;lease_token:string|null;lease_until_utc:string|null;next_network_at_utc:string|null;command_id:string|null;prepared_request_json:string|null}
interface Prepared {profileId:string;connectionId:string;operation:string;arguments:Record<string,unknown>;automated:boolean;attemptId:string;attemptAtUtc:string}
interface Outcome {kind?:string;code?:string;retryAtUtc?:string;task?:{id:string;fingerprint:string;alias:string|null}}
export type ExecutorEnv=Pick<WorkspaceEnv,'WORKSPACE_DB'|'AUTH_IDENTITY'|'AUTH_PROJECTION'> & {EXECUTOR_ENABLED:unknown;WORKSPACE_ENABLED:unknown};
const bytes=(v:string)=>new TextEncoder().encode(v).length;
const now=()=>new Date().toISOString();
export const cloudAuthority=(id:string):Authority=>({mode:'cloud',transferId:null,cloudWorkspaceId:id});
export async function executeProfile(env:ExecutorEnv,profileId:string,options:{wait?:(ms:number)=>Promise<void>}={}):Promise<string>{
 if(!env.EXECUTOR_ENABLED||!env.WORKSPACE_ENABLED)return 'disabled';
 const initial=await env.WORKSPACE_DB.prepare('SELECT * FROM workspaces WHERE profile_id=? AND active=1 AND executor_enabled=1').bind(profileId).first<WorkspaceRow>();
 if(!initial)return 'disabled';
 const authority:Authority=initial.authority_json?JSON.parse(initial.authority_json):cloudAuthority(initial.workspace_id);if(authority.mode!=='cloud')return 'paused';
 const token=crypto.randomUUID();const until=new Date(Date.now()+240000).toISOString();
 const acquired=await env.WORKSPACE_DB.prepare(`INSERT INTO workspace_runtime(profile_id,access_epoch,lease_token,lease_until_utc,last_wake_at_utc)
 SELECT profile_id,access_epoch,?,?,? FROM workspaces WHERE profile_id=? AND active=1 AND executor_enabled=1 AND access_epoch=?
 ON CONFLICT(profile_id) DO UPDATE SET access_epoch=excluded.access_epoch,lease_token=excluded.lease_token,lease_until_utc=excluded.lease_until_utc,last_wake_at_utc=excluded.last_wake_at_utc
 WHERE workspace_runtime.lease_until_utc IS NULL OR workspace_runtime.lease_until_utc<=?`).bind(token,until,now(),profileId,initial.access_epoch,now()).run();
 if(!acquired.meta.changes)return 'busy';
 const epoch=initial.access_epoch;const deadline=Date.now()+120000;const wait=options.wait??(ms=>new Promise(resolve=>setTimeout(resolve,ms)));
 const runtime=()=>env.WORKSPACE_DB.prepare('SELECT * FROM workspace_runtime WHERE profile_id=? AND access_epoch=? AND lease_token=? AND lease_until_utc>?').bind(profileId,epoch,token,now()).first<Runtime>();
 const row=()=>env.WORKSPACE_DB.prepare('SELECT * FROM workspaces WHERE profile_id=? AND access_epoch=?').bind(profileId,epoch).first<WorkspaceRow>();
 const currentEntry=(id:string)=>env.WORKSPACE_DB.prepare('SELECT entry_json FROM workspace_commands WHERE profile_id=? AND command_id=?').bind(profileId,id).first<{entry_json:string}>();
 async function permitted(){const [w,r]=await Promise.all([row(),runtime()]);return Boolean(w?.active&&w.executor_enabled&&r&&Date.now()<deadline);}
 async function transition(id:string,change:(entry:CommandEntry,document:WorkspaceDocument)=>{entry:CommandEntry;document?:WorkspaceDocument;kind:string;evidence?:string},prepared?:Prepared|null):Promise<boolean>{
  for(let i=0;i<3;i++){
   const w=await row();const r=await runtime();const c=await currentEntry(id);if(!w||!r||!c)return false;
   const old=JSON.parse(c.entry_json) as CommandEntry;const doc=JSON.parse(w.document_json) as WorkspaceDocument;const next=change(structuredClone(old),doc);const entryJson=JSON.stringify(next.entry);const docJson=JSON.stringify(next.document??doc);const e:JournalEvent=event(next.entry,next.kind,next.evidence??null);const eventJson=JSON.stringify(e);const size=w.archive_bytes-bytes(c.entry_json)-bytes(w.document_json)+bytes(entryJson)+bytes(docJson)+bytes(eventJson)+1;
   if(size>8*1024*1024||bytes(docJson)>1024*1024||w.store_version>=Number.MAX_SAFE_INTEGER)return false;
   const tag=crypto.randomUUID();const version=w.store_version+1;
   const statements=[env.WORKSPACE_DB.prepare(`UPDATE workspaces SET document_json=?,store_version=?,archive_bytes=?,commit_tag=?,updated_at_utc=? WHERE profile_id=? AND access_epoch=? AND store_version=?
    AND EXISTS(SELECT 1 FROM workspace_runtime WHERE profile_id=? AND access_epoch=? AND lease_token=? AND lease_until_utc>?)`).bind(docJson,version,size,tag,now(),profileId,epoch,w.store_version,profileId,epoch,token,now()),
    env.WORKSPACE_DB.prepare('UPDATE workspace_commands SET entry_json=? WHERE profile_id=? AND command_id=? AND EXISTS(SELECT 1 FROM workspaces WHERE profile_id=? AND commit_tag=?)').bind(entryJson,profileId,id,profileId,tag),
    env.WORKSPACE_DB.prepare('INSERT INTO workspace_events(profile_id,event_id,sequence,event_json) SELECT profile_id,?,?,? FROM workspaces WHERE profile_id=? AND commit_tag=?').bind(e.id,version,eventJson,profileId,tag)];
   if(prepared!==undefined)statements.push(env.WORKSPACE_DB.prepare('UPDATE workspace_runtime SET command_id=?,prepared_request_json=? WHERE profile_id=? AND lease_token=? AND EXISTS(SELECT 1 FROM workspaces WHERE profile_id=? AND commit_tag=?)').bind(prepared?id:null,prepared?JSON.stringify(prepared):null,profileId,token,profileId,tag));
   const result=await env.WORKSPACE_DB.batch(statements);if(result[0].meta.changes)return true;
  }return false;
 }
 async function gateway(operation:string,args:Record<string,unknown>,prepared?:Prepared,receiptOnly=false):Promise<Outcome&Partial<Observation>&Partial<AccountObservation>>{
  if(!await permitted())throw new Error('executor_paused');
  if(!receiptOnly){
   const r=await runtime();const delay=Math.max(0,Date.parse(r?.next_network_at_utc??'')-Date.now());
   if(Number.isFinite(delay)&&delay>0){if(Date.now()+delay+15000>=deadline)throw new Error('activation_deadline');await wait(delay);}
   if(!await permitted())throw new Error('executor_paused');
   const epochCheck=await env.AUTH_IDENTITY.fetch('https://identity.internal/epoch',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({profileId,connectionId:initial!.connection_id}),signal:AbortSignal.timeout(15000)});
   if(!epochCheck.ok)return {kind:'refused',code:'credential_repair_required'};
   if(!await permitted())throw new Error('executor_paused');
   const reserved=await env.WORKSPACE_DB.prepare('UPDATE workspace_runtime SET next_network_at_utc=? WHERE profile_id=? AND access_epoch=? AND lease_token=? AND lease_until_utc>?').bind(new Date(Date.now()+30000).toISOString(),profileId,epoch,token,now()).run();if(!reserved.meta.changes)throw new Error('executor_paused');
  }
  const payload=prepared??{profileId,connectionId:initial!.connection_id,operation,arguments:args,automated:true};
  const response=await env.AUTH_PROJECTION.fetch('https://projection.internal/operation',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify(payload),signal:AbortSignal.timeout(15000)});
  const result=await response.json<Outcome&Partial<Observation>&Partial<AccountObservation>>();
  if(receiptOnly&&!response.ok)return {kind:'unknown',code:'receipt_unavailable'};
  if(!response.ok&&!result.kind)return {kind:'refused',code:'projection_unavailable'};return result;
 }
 async function result(id:string,outcome:Outcome,recovered=false):Promise<boolean>{
  return transition(id,(entry,document)=>{
   if(!['executing','unknownOutcome','leased'].includes(entry.projectionState))throw new Error('attempt_changed');
   if(outcome.kind==='applied'&&outcome.task){
    const doc=confirmMapping(document,entry.command.taskId,outcome.task);const count=entry.confirmedEffects+1;
    return {entry:{...entry,confirmedEffects:count,projectionState:count===entry.effects.length?'confirmed':'pending',failure:'none',retryAtUtc:null,lease:null,dispatchedAtUtc:null},document:doc,kind:recovered?'reconciled':'resultKnown',evidence:recovered?'auth-attempt-receipt':undefined};
   }
   if(outcome.kind!=='refused'){
    const revision=document.revision+1;const doc={...document,revision,projections:document.projections.map(p=>p.taskId===entry.command.taskId?{...p,state:'unknownOutcome' as const,revision}:p)};
    return {entry:{...entry,projectionState:'unknownOutcome',failure:'ambiguous'},document:doc,kind:'unknownDetected'};
   }
   const retry=outcome.retryAtUtc&&Number.isFinite(Date.parse(outcome.retryAtUtc))&&Date.parse(outcome.retryAtUtc)>Date.now()?outcome.retryAtUtc:null;
   const limited=['rate_limited','projection_busy'].includes(outcome.code??'');const credential=['credential_repair_required','unauthorized','profile_unavailable'].includes(outcome.code??'');
   const failure=limited?'rateLimited':credential?'credentialRepair':'precondition';
   let doc=document;if(['external_deleted','external_edit_review_required','projection_identity_conflict'].includes(outcome.code??'')){const revision=document.revision+1;doc={...document,revision,projections:document.projections.map(p=>p.taskId===entry.command.taskId?{...p,state:'externalConflict' as const,revision}:p)};}
   return {entry:{...entry,projectionState:limited&&retry?'pending':'blocked',failure,retryAtUtc:limited?retry:null,lease:null,dispatchedAtUtc:null},document:doc,kind:'resultKnown'};
  },outcome.kind==='unknown'||!['applied','refused'].includes(outcome.kind??'')?undefined:null);
 }
 try{
  if(await env.WORKSPACE_DB.prepare("SELECT 1 FROM prepared_cron_actions WHERE profile_id=? AND status IN ('accepted','dispatching','unknownOutcome') LIMIT 1").bind(profileId).first())return 'cron_fenced';
  if(await env.WORKSPACE_DB.prepare("SELECT 1 FROM prepared_spell_actions WHERE profile_id=? AND status IN ('accepted','dispatching','unknownOutcome') LIMIT 1").bind(profileId).first())return 'spell_fenced';
  const r=await runtime();if(!r)return 'lost_lease';
  const entries=(await env.WORKSPACE_DB.prepare('SELECT entry_json FROM workspace_commands WHERE profile_id=? ORDER BY sequence').bind(profileId).all<{entry_json:string}>()).results.map(c=>JSON.parse(c.entry_json) as CommandEntry);
  // A saved dispatch is never sent again. A receipt lookup does not touch Habitica.
  const uncertain=entries.find(c=>['executing','unknownOutcome'].includes(c.projectionState));
  if(uncertain){
   if(uncertain.lease&&Date.parse(uncertain.lease.expiresAtUtc)>Date.now()&&uncertain.projectionState==='executing')return 'in_flight';
   let outcome:Outcome={kind:'unknown',code:'receipt_unavailable'};
   if(r.command_id===uncertain.command.id&&r.prepared_request_json){const prepared=JSON.parse(r.prepared_request_json) as Prepared;outcome=await gateway('lookup',{}, {...prepared,operation:'lookup',arguments:{operation:prepared.operation,arguments:prepared.arguments}},true);if(outcome.kind!=='applied'&&outcome.kind!=='refused')outcome={kind:'unknown',code:outcome.code};}
   else if(uncertain.projectionState==='unknownOutcome')return 'unknown';
   if(uncertain.projectionState==='unknownOutcome'&&outcome.kind==='unknown')return 'unknown';
   await result(uncertain.command.id,outcome,true);return outcome.kind==='applied'?'recovered':'unknown';
  }
  for(const entry of entries.filter(c=>c.projectionState==='leased')){
   if(Date.parse(entry.lease!.expiresAtUtc)>Date.now())return 'in_flight';
   await transition(entry.command.id,e=>({entry:{...e,projectionState:'pending',lease:null},kind:'leaseExpired'}));return 'lease_recovered';
  }
  if(entries.some(c=>c.failure==='credentialRepair'&&c.projectionState==='blocked'||c.failure==='rateLimited'&&Date.parse(c.retryAtUtc??'')>Date.now()))return 'fenced';
  const candidate=entries.find((c,i)=>c.projectionState==='pending'&&!(Date.parse(c.retryAtUtc??'')>Date.now())&&!entries.slice(0,i).some(p=>p.command.taskId===c.command.taskId&&!['confirmed','notRequested','cancelled'].includes(p.projectionState)));
  if(!candidate)return 'idle';
  const w=await row();if(!w||w.archive_bytes>8*1024*1024-32768||w.store_version>Number.MAX_SAFE_INTEGER-8)return 'capacity';
  const lease={owner:'workspace-worker',token:crypto.randomUUID(),expiresAtUtc:until};
  if(!await transition(candidate.command.id,e=>({entry:{...e,projectionState:'leased',lease},kind:'leaseAcquired'})))return 'conflict';
  let document=JSON.parse((await row())!.document_json) as WorkspaceDocument;let observation:Observation|undefined;let account:AccountObservation|undefined;
  for(let stepCount=0;stepCount<4;stepCount++){
   const step=projectionStep(document,candidate,observation,account);
   if(step.kind==='blocked'){await result(candidate.command.id,{kind:'refused',code:step.code});return 'blocked';}
   if(step.kind==='account'){
    const observed=await gateway('inspectAccount',{});if(observed.kind==='refused'){await result(candidate.command.id,observed);return 'refused';}if(typeof observed.day!=='string'||typeof observed.needsCron!=='boolean'||typeof observed.accountCheckToken!=='string')throw new Error('invalid_observation');account=observed as AccountObservation;continue;
   }
   if(step.kind==='inspect'){
    const observed=await gateway(step.operation,{selector:step.selector});if(observed.kind==='refused'){await result(candidate.command.id,observed);return 'refused';}if(!('task' in observed)||typeof observed.checkToken!=='string')throw new Error('invalid_observation');observation=observed as Observation;continue;
   }
   if(step.kind==='observedApplied'){await result(candidate.command.id,{kind:'applied',task:step.task});return 'confirmed';}
   // Reserve the request only after pacing; attempt timestamps and check receipts remain fresh.
   const rt=await runtime();const delay=Math.max(0,Date.parse(rt?.next_network_at_utc??'')-Date.now());if(Number.isFinite(delay)&&delay>0)await wait(delay);
   if(!await permitted())return 'paused';
   const prepared:Prepared={profileId,connectionId:initial.connection_id,operation:step.operation,arguments:step.arguments,automated:true,attemptId:lease.token,attemptAtUtc:now()};
   if(!await transition(candidate.command.id,e=>({entry:{...e,projectionState:'executing',attempts:e.attempts+1,dispatchedAtUtc:prepared.attemptAtUtc,failure:'none',retryAtUtc:null},kind:'dispatched'}),prepared))return 'conflict';
   let outcome:Outcome;try{outcome=await gateway(step.operation,step.arguments,prepared);}catch{outcome={kind:'unknown',code:'transport_unknown'};}
   await result(candidate.command.id,outcome);return outcome.kind??'unknown';
  }return 'bounded';
 }catch{
  // Durable leases and dispatch records remain recoverable. Never turn transport uncertainty into permission to replay.
  return 'interrupted';
 }finally{await env.WORKSPACE_DB.prepare('UPDATE workspace_runtime SET lease_token=NULL,lease_until_utc=NULL WHERE profile_id=? AND access_epoch=? AND lease_token=?').bind(profileId,epoch,token).run();}
}
export async function executeScheduled(env:ExecutorEnv):Promise<void>{
 if(!env.WORKSPACE_ENABLED||!env.EXECUTOR_ENABLED)return;
 const next=await env.WORKSPACE_DB.prepare(`SELECT w.profile_id FROM workspaces w LEFT JOIN workspace_runtime r ON r.profile_id=w.profile_id WHERE w.active=1 AND w.executor_enabled=1 ORDER BY COALESCE(r.last_wake_at_utc,''),w.profile_id LIMIT 1`).first<{profile_id:string}>();
 if(next)await executeProfile(env,next.profile_id);
}
