import {upstreamBudget,observeUpstream,TrafficLimit,trafficFailure} from './traffic';
import {activeProfile,credentialKeys,boundedJson,VaultError} from './vault';
import {decryptCredential} from './cryptography';
import {validatedToken} from './oauth';
import {upstream,ReadFailure} from './reads';
import {verifyPartyUser} from './party';
import {rateState} from './projection';
import {resourceConfiguration} from '../../shared/resources';
import {spellScopes,supportedSpell,spellEligibility,spellStats,spellStateHash} from '../../shared/spells';
import {workspaceObject,workspaceJson,workspaceTokenHash,WorkspaceError,uuid} from '../../shared/workspace-http';
import type {AuthProps} from '../../shared/contracts';
const headers={'cache-control':'no-store'};
const json=(value:unknown,status=200)=>{const retry=value&&typeof value==='object'?(value as {retryAtUtc?:string}).retryAtUtc:undefined;return Response.json(value,{status,headers:{...headers,...(status===429&&retry?{'retry-after':String(Math.max(1,Math.ceil((Date.parse(retry)-Date.now())/1000)))}:{})}});};
interface Critical {actorId:string;stats:Record<string,unknown>;equipped:Record<string,unknown>;sleep:boolean|null;classesDisabled:boolean|null;needsCron:boolean|null;lastCron:string|null;party:unknown}
interface SpellOutcome {kind:'applied'|'refused'|'unknown';code:string;mana?:number;health?:number;nextPreconditionHash?:string;retryAtUtc?:string}
function accountCritical(raw:unknown,actorId:string,party:unknown,previous?:Critical):Critical{
 const user=workspaceObject(raw);if(uuid(user._id??user.id)!==actorId)throw new WorkspaceError('identity_mismatch',502);
 const flags=workspaceObject(user.flags??{}),prefs=workspaceObject(user.preferences??{}),items=workspaceObject(user.items??{}),gear=workspaceObject(items.gear??{});
 if(!previous&&typeof prefs.disableClasses!=='boolean')throw new WorkspaceError('invalid_spell_snapshot',502);
 const bool=(v:unknown,old:boolean|null|undefined)=>typeof v==='boolean'?v:old??null;
 return {actorId,stats:spellStats(user.stats),equipped:gear.equipped===undefined?previous?.equipped??{}:workspaceObject(gear.equipped),sleep:bool(prefs.sleep,previous?.sleep),classesDisabled:bool(prefs.disableClasses,previous?.classesDisabled),needsCron:bool(flags.needsCron,previous?.needsCron),lastCron:typeof user.lastCron==='string'&&user.lastCron.length<=128?user.lastCron:previous?.lastCron??null,party};
}
export async function spellObservation(env:AuthEnv,who:AuthProps){
 const profile=await activeProfile(env,who.profileId,who.connectionId);
 if(profile.credential_schema_version!==1)throw new WorkspaceError('spell_unavailable',503);
 const credential=await decryptCredential(who.profileId,{ciphertext:profile.credential_ciphertext!,iv:profile.credential_iv!,keyVersion:profile.credential_key_version!,schemaVersion:1},credentialKeys(env));
 const raw=(await upstream(env,who.profileId,who.connectionId,'user?userFields=_id,profile,stats,party,flags,preferences,lastCron,items.gear.equipped')).data;
 const proof=await verifyPartyUser(env,who.profileId,who.connectionId,raw,credential.habiticaUserId);
 const critical=accountCritical(raw,proof.actorUserId,proof.party);
 const state={className:String(critical.stats.class),level:Number(critical.stats.lvl),mana:Number(critical.stats.mp),health:Number(critical.stats.hp),classesDisabled:critical.classesDisabled===true};
 const reason=spellEligibility(state);
 return {credential,proof,critical,state,preconditionHash:await spellStateHash(critical),context:{...state,needsCron:critical.needsCron,party:{id:proof.party.id,name:proof.party.name,memberCount:proof.party.memberCount,quest:proof.party.quest},spell:supportedSpell,canPrepare:reason===null,unavailableReason:reason,retrievedAtUtc:proof.observedAtUtc,warnings:['Actual per-member healing is unknown; no exact HP or reward prediction is supplied.','No new day is started and no temporary buff is changed by this plan.','Habitica may post an automatic spell notice in party chat.']}};
}
async function grant(env:AuthEnv,request:Request,who:AuthProps,operation:keyof typeof spellScopes){
 const token=request.headers.get('authorization')?.match(/^Bearer (\S+)$/)?.[1];
 const identity=token?await validatedToken(env,resourceConfiguration(env.ENVIRONMENT, env).resource,token):null;
 if(!identity||identity.props.profileId!==who.profileId||identity.props.connectionId!==who.connectionId)throw new WorkspaceError('unauthorized',401);
 if(spellScopes[operation].some(scope=>!identity.scope?.includes(scope)))throw new WorkspaceError('insufficient_scope',403);
}
export async function spellOperation(request:Request,env:AuthEnv):Promise<Response>{
 let who:AuthProps|undefined,lease:string|undefined,attemptId:string|undefined,hash:string|undefined,dispatched=false,leaseOwned=false;
 const readPrior=async()=>env.HABITICA_INTEGRATION_DB.prepare('SELECT payload_hash,outcome_json FROM integration_projection_attempts WHERE profile_id=? AND connection_id=? AND attempt_id=?').bind(who!.profileId,who!.connectionId,attemptId!).first<{payload_hash:string;outcome_json:string|null}>();
 async function record(outcome:SpellOutcome){
  if(!who||!attemptId||!hash)return;
  if(dispatched){
   // Terminal receipts are immutable, including when an older lease owner resumes late.
   await env.HABITICA_INTEGRATION_DB.prepare(`UPDATE integration_projection_attempts SET state=?,outcome_json=? WHERE profile_id=? AND connection_id=? AND attempt_id=? AND payload_hash=? AND state IN ('dispatched','unknown')`)
    .bind(outcome.kind,JSON.stringify(outcome),who.profileId,who.connectionId,attemptId,hash).run();
  }else{
   await env.HABITICA_INTEGRATION_DB.prepare(`INSERT OR IGNORE INTO integration_projection_attempts(profile_id,connection_id,attempt_id,payload_hash,operation,state,outcome_json,created_at_utc) VALUES(?,?,?,?,'spell',?,?,?)`)
    .bind(who.profileId,who.connectionId,attemptId,hash,outcome.kind,JSON.stringify(outcome),new Date().toISOString()).run();
  }
 }
 async function existing(){
  const prior=await readPrior();if(!prior)return null;
  if(prior.payload_hash!==hash)throw new WorkspaceError('attempt_id_reused',409);
  return prior.outcome_json?JSON.parse(prior.outcome_json):{kind:'unknown',code:'outcome_unknown'};
 }

 try{
  if(!env.WORKSPACE_ENABLED||request.method!=='POST')throw new WorkspaceError('spell_unavailable',503);
  const input=workspaceObject(await workspaceJson(request),['profileId','connectionId','operation','arguments','attemptId','attemptAtUtc']);
  if(typeof input.profileId!=='string'||!/^prf_[A-Za-z0-9_-]{43}$/.test(input.profileId)||!['context','cast','lookup'].includes(String(input.operation)))throw new WorkspaceError('invalid_request',400);
  who={profileId:input.profileId,connectionId:uuid(input.connectionId)};const operation=input.operation as keyof typeof spellScopes;
  await grant(env,request,who,operation);
  if(operation==='context'){
   if(!env.SPELLS_ENABLED||!env.PARTY_ENABLED)throw new WorkspaceError('spell_unavailable',503);
   workspaceObject(input.arguments,[]);if(input.attemptId!==undefined||input.attemptAtUtc!==undefined)throw new WorkspaceError('invalid_request',400);
   const observed=await spellObservation(env,who);await grant(env,request,who,'context');return json({context:observed.context,preconditionHash:observed.preconditionHash});
  }
  const args=workspaceObject(input.arguments,['spellId','partyId','expectedPreconditionHash','remainingCount']);
  if(args.spellId!=='healAll'||typeof args.expectedPreconditionHash!=='string'||!/^[a-f0-9]{64}$/.test(args.expectedPreconditionHash)||!Number.isInteger(args.remainingCount)||Number(args.remainingCount)<1||Number(args.remainingCount)>3)throw new WorkspaceError('invalid_spell',422);
  args.partyId=uuid(args.partyId);attemptId=uuid(input.attemptId);
  if(typeof input.attemptAtUtc!=='string')throw new WorkspaceError('invalid_request',400);
  hash=await workspaceTokenHash(JSON.stringify({operation:'spell',arguments:args,attemptAtUtc:input.attemptAtUtc}));
  const prior=await readPrior();
  if(prior){if(prior.payload_hash!==hash)throw new WorkspaceError('attempt_id_reused',409);return json(prior.outcome_json?JSON.parse(prior.outcome_json):{kind:'unknown',code:'outcome_unknown'});}
  if(operation==='lookup')return json({kind:'unknown',code:'attempt_not_found'});
  if(!env.SPELLS_ENABLED||!env.PARTY_ENABLED)throw new WorkspaceError('spell_unavailable',503);
  const at=Date.parse(input.attemptAtUtc);if(!Number.isFinite(at)||at<Date.now()-120000||at>Date.now()+5000)throw new WorkspaceError('attempt_expired',409);
  if(await env.HABITICA_INTEGRATION_DB.prepare("SELECT 1 FROM integration_projection_attempts WHERE profile_id=? AND connection_id=? AND state IN ('unknown','dispatched') LIMIT 1").bind(who.profileId,who.connectionId).first())throw new WorkspaceError('unknown_outcome_pending',409);
  lease=crypto.randomUUID()+':'+attemptId;const now=new Date().toISOString();
  const locked=await env.HABITICA_INTEGRATION_DB.prepare(`INSERT INTO integration_projection_state(profile_id,connection_id,lease_token,lease_until_utc) VALUES(?,?,?,?) ON CONFLICT(profile_id,connection_id) DO UPDATE SET lease_token=excluded.lease_token,lease_until_utc=excluded.lease_until_utc WHERE integration_projection_state.lease_until_utc IS NULL OR integration_projection_state.lease_until_utc<=?`)
   .bind(who.profileId,who.connectionId,lease,new Date(Date.now()+45000).toISOString(),now).run();
  if(!locked.meta.changes){
   const held=await env.HABITICA_INTEGRATION_DB.prepare('SELECT lease_token FROM integration_projection_state WHERE profile_id=? AND connection_id=?').bind(who.profileId,who.connectionId).first<{lease_token:string|null}>();
   if(held?.lease_token?.endsWith(':'+attemptId))return json({kind:'unknown',code:'attempt_in_flight'});
   throw new WorkspaceError('gameplay_busy',409);
  }
  leaseOwned=true;
  const completed=await existing();if(completed)return json(completed);
  const observed=await spellObservation(env,who);
  const reason=spellEligibility(observed.state,Number(args.remainingCount));
  if(reason)throw new WorkspaceError(reason,409);
  if(observed.proof.party.id!==args.partyId||observed.preconditionHash!==args.expectedPreconditionHash)throw new WorkspaceError('spell_precondition_changed',409);
  await grant(env,request,who,'cast');await activeProfile(env,who.profileId,who.connectionId);
  const rate=await env.HABITICA_INTEGRATION_DB.prepare('SELECT blocked_until_utc,credential_repair_required FROM integration_read_state WHERE profile_id=? AND connection_id=?').bind(who.profileId,who.connectionId).first<{blocked_until_utc:string|null;credential_repair_required:number}>();
  if(rate?.credential_repair_required)throw new WorkspaceError('credential_repair_required',401);
  if(rate?.blocked_until_utc&&Date.parse(rate.blocked_until_utc)>Date.now())throw new ReadFailure('rate_limited',429,{retryAfterSeconds:Math.ceil((Date.parse(rate.blocked_until_utc)-Date.now())/1000)});
  await upstreamBudget(env,who.profileId);
  const reserved=await env.HABITICA_INTEGRATION_DB.prepare(`INSERT OR IGNORE INTO integration_projection_attempts(profile_id,connection_id,attempt_id,payload_hash,operation,state,created_at_utc) SELECT ?,?,?,?,'spell','dispatched',? FROM integration_projection_state WHERE profile_id=? AND connection_id=? AND lease_token=? AND lease_until_utc>? AND NOT EXISTS(SELECT 1 FROM integration_projection_attempts WHERE profile_id=? AND connection_id=? AND state IN ('unknown','dispatched'))`)
   .bind(who.profileId,who.connectionId,attemptId,hash,new Date().toISOString(),who.profileId,who.connectionId,lease,new Date(Date.now()+11000).toISOString(),who.profileId,who.connectionId).run();
  if(!reserved.meta.changes){const prior=await existing();if(prior)return json(prior);throw new WorkspaceError('spell_busy_or_stale',409);}dispatched=true;
  const response=await fetch('https://habitica.com/api/v3/user/class/cast/healAll',{method:'POST',redirect:'manual',headers:{'x-api-user':observed.credential.habiticaUserId,'x-api-key':observed.credential.apiToken,'x-client':env.HABITICA_X_CLIENT_HEADER,'content-type':'application/json',accept:'application/json'},body:JSON.stringify({quantity:1}),signal:AbortSignal.timeout(10000)});
  await observeUpstream(env,response,who!.profileId);
  const retryAtUtc=await rateState(env,who,response,false);
  let outcome:SpellOutcome;
  if([400,401,403,404,409,422,429].includes(response.status))outcome={kind:'refused',code:response.status===429?'rate_limited':'spell_refused',...(retryAtUtc?{retryAtUtc}:{})};
  else if(response.status!==200)outcome={kind:'unknown',code:'outcome_unknown'};
  else{
   const body=workspaceObject(await boundedJson(response,2097152));if(body.success!==true)throw new WorkspaceError('outcome_unknown',502);
   const data=workspaceObject(body.data);const next=accountCritical(data.user,observed.proof.actorUserId,observed.proof.party,observed.critical);
   const party=workspaceObject(workspaceObject(data.user).party);if(uuid(party._id)!==observed.proof.party.id)throw new WorkspaceError('outcome_unknown',502);
   outcome={kind:'applied',code:'applied',mana:Number(next.stats.mp),health:Number(next.stats.hp),nextPreconditionHash:await spellStateHash(next)};
  }
  await record(outcome);return json(await existing()??outcome);
 }catch(error){
  if(dispatched){const outcome:SpellOutcome={kind:'unknown',code:'outcome_unknown'};try{await record(outcome);}catch{/* The durable dispatched attempt continues to fence gameplay. */}return json(outcome);}
  const known=error instanceof WorkspaceError||error instanceof VaultError||error instanceof ReadFailure;
  const outcome:SpellOutcome={kind:'refused',code:known?error.code:'spell_unavailable',...(error instanceof TrafficLimit?{retryAtUtc:error.retryAtUtc}:error instanceof ReadFailure&&typeof error.rate.retryAfterSeconds==='number'?{retryAtUtc:new Date(Date.now()+error.rate.retryAfterSeconds*1000).toISOString()}:{})};
  // Only the lease owner records a known pre-dispatch refusal; a competing in-flight attempt stays authoritative.
  if(leaseOwned&&lease&&who&&attemptId&&hash&&outcome.code!=='unauthorized'&&outcome.code!=='insufficient_scope'&&!(error instanceof VaultError)){const held=await env.HABITICA_INTEGRATION_DB.prepare('SELECT 1 FROM integration_projection_state WHERE profile_id=? AND connection_id=? AND lease_token=?').bind(who.profileId,who.connectionId,lease).first();if(held){try{await record(outcome);return json(await existing()??outcome,known?error.status:503);}catch{return json({kind:'refused',code:'spell_unavailable'},503);}}}
  return json(outcome,known?error.status:503);
 }finally{if(leaseOwned&&lease&&who){try{await env.HABITICA_INTEGRATION_DB.prepare('UPDATE integration_projection_state SET lease_token=NULL,lease_until_utc=NULL WHERE profile_id=? AND connection_id=? AND lease_token=?').bind(who.profileId,who.connectionId,lease).run();}catch{/* Lease expiry bounds recovery. */}}}
}
