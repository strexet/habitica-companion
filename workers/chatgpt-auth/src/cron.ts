import {upstreamBudget,upstreamFetch,observeUpstream,TrafficLimit,trafficFailure} from './traffic';
import {validatedToken} from './oauth';
import {activeProfile,boundedJson,VaultError} from './vault';
import {ReadFailure} from './reads';
import {rateState} from './projection';
import {cronObservation,cronProgress} from './cron-observation';
import {cronScopes} from '../../shared/cron';
import {resourceConfiguration} from '../../shared/resources';
import {workspaceJson,workspaceObject,workspaceTokenHash,WorkspaceError,uuid} from '../../shared/workspace-http';
import type {AuthProps} from '../../shared/contracts';
interface Outcome {kind:'applied'|'refused'|'unknown';code:string;retryAtUtc?:string}
const json=(value:unknown,status=200)=>{const retry=value&&typeof value==='object'?(value as {retryAtUtc?:string}).retryAtUtc:undefined;return Response.json(value,{status,headers:{...{'cache-control':'no-store'},...(status===429&&retry?{'retry-after':String(Math.max(1,Math.ceil((Date.parse(retry)-Date.now())/1000)))}:{})}});};
async function grant(env:AuthEnv,request:Request,who:AuthProps,operation:keyof typeof cronScopes){
 const token=request.headers.get('authorization')?.match(/^Bearer (\S+)$/)?.[1];const context=token?await validatedToken(env,resourceConfiguration(env.ENVIRONMENT, env).resource,token):null;
 if(!context||context.props.profileId!==who.profileId||context.props.connectionId!==who.connectionId)throw new WorkspaceError('unauthorized',401);
 if(cronScopes[operation].some(scope=>!context.scope?.includes(scope)))throw new WorkspaceError('insufficient_scope',403);
}
export async function cronOperation(request:Request,env:AuthEnv):Promise<Response>{
 let who:AuthProps|undefined,attemptId:string|undefined,hash:string|undefined,lease:string|undefined,owned=false,dispatched=false;
 async function prior(){
  const r=await env.HABITICA_INTEGRATION_DB.prepare('SELECT payload_hash,outcome_json FROM integration_projection_attempts WHERE profile_id=? AND connection_id=? AND attempt_id=?').bind(who!.profileId,who!.connectionId,attemptId!).first<{payload_hash:string;outcome_json:string|null}>();
  if(!r)return null;if(r.payload_hash!==hash)throw new WorkspaceError('attempt_id_reused',409);return r.outcome_json?JSON.parse(r.outcome_json):{kind:'unknown',code:'outcome_unknown'};
 }
 async function record(outcome:Outcome){
  if(dispatched)await env.HABITICA_INTEGRATION_DB.prepare("UPDATE integration_projection_attempts SET state=?,outcome_json=? WHERE profile_id=? AND connection_id=? AND attempt_id=? AND payload_hash=? AND state IN ('dispatched','unknown')").bind(outcome.kind,JSON.stringify(outcome),who!.profileId,who!.connectionId,attemptId!,hash!).run();
  else await env.HABITICA_INTEGRATION_DB.prepare("INSERT OR IGNORE INTO integration_projection_attempts(profile_id,connection_id,attempt_id,payload_hash,operation,state,outcome_json,created_at_utc) VALUES(?,?,?,?,'cron',?,?,?)").bind(who!.profileId,who!.connectionId,attemptId!,hash!,outcome.kind,JSON.stringify(outcome),new Date().toISOString()).run();
 }
 try{
  if(!env.WORKSPACE_ENABLED||request.method!=='POST')throw new WorkspaceError('cron_unavailable',503);
  const body=workspaceObject(await workspaceJson(request),['profileId','connectionId','operation','arguments','attemptId','attemptAtUtc']);
  if(typeof body.profileId!=='string'||!/^prf_[A-Za-z0-9_-]{43}$/.test(body.profileId)||typeof body.operation!=='string'||!Object.hasOwn(cronScopes,body.operation))throw new WorkspaceError('invalid_request',400);
  who={profileId:body.profileId,connectionId:uuid(body.connectionId)};const operation=body.operation as keyof typeof cronScopes;await grant(env,request,who,operation);
  if(operation==='status'||operation==='progress'){
   if(body.attemptId!==undefined||body.attemptAtUtc!==undefined)throw new WorkspaceError('invalid_request',400);
   const args=workspaceObject(body.arguments,operation==='status'?[]:['previousLastCron']);
   if(operation==='status'){
    if(!env.CRON_ENABLED)throw new WorkspaceError('cron_unavailable',503);
    const observed=await cronObservation(env,who);await grant(env,request,who,'status');return json({context:observed.context,preconditionHash:observed.preconditionHash});
   }
   if(typeof args.previousLastCron!=='string'||!Number.isFinite(Date.parse(args.previousLastCron)))throw new WorkspaceError('invalid_request',400);
   const progress=await cronProgress(env,who,args.previousLastCron);await grant(env,request,who,'progress');return json(progress);
  }
  const args=workspaceObject(body.arguments,['expectedPreconditionHash','expectedDay','expectedLastCron']);
  if(typeof args.expectedPreconditionHash!=='string'||!/^[a-f0-9]{64}$/.test(args.expectedPreconditionHash)||typeof args.expectedDay!=='string'||!/^\d{4}-\d{2}-\d{2}$/.test(args.expectedDay)||typeof args.expectedLastCron!=='string'||!Number.isFinite(Date.parse(args.expectedLastCron)))throw new WorkspaceError('invalid_precondition',422);
  attemptId=uuid(body.attemptId);if(typeof body.attemptAtUtc!=='string')throw new WorkspaceError('invalid_request',400);
  hash=await workspaceTokenHash(JSON.stringify({operation:'cron',arguments:args,attemptAtUtc:body.attemptAtUtc}));const existing=await prior();if(existing)return json(existing);
  if(operation==='lookup')return json({kind:'unknown',code:'attempt_not_found'});
  if(!env.CRON_ENABLED)throw new WorkspaceError('cron_unavailable',503);
  const at=Date.parse(body.attemptAtUtc);if(!Number.isFinite(at)||at<Date.now()-120000||at>Date.now()+5000)throw new WorkspaceError('attempt_expired',409);
  if(await env.HABITICA_INTEGRATION_DB.prepare("SELECT 1 FROM integration_projection_attempts WHERE profile_id=? AND connection_id=? AND state IN ('dispatched','unknown') LIMIT 1").bind(who.profileId,who.connectionId).first())throw new WorkspaceError('unknown_outcome_pending',409);
  lease=crypto.randomUUID()+':cron:'+attemptId;
  const acquired=await env.HABITICA_INTEGRATION_DB.prepare(`INSERT INTO integration_projection_state(profile_id,connection_id,lease_token,lease_until_utc) VALUES(?,?,?,?) ON CONFLICT(profile_id,connection_id) DO UPDATE SET lease_token=excluded.lease_token,lease_until_utc=excluded.lease_until_utc WHERE integration_projection_state.lease_until_utc IS NULL OR integration_projection_state.lease_until_utc<=?`).bind(who.profileId,who.connectionId,lease,new Date(Date.now()+60000).toISOString(),new Date().toISOString()).run();
  if(!acquired.meta.changes){const active=await env.HABITICA_INTEGRATION_DB.prepare('SELECT lease_token FROM integration_projection_state WHERE profile_id=? AND connection_id=?').bind(who.profileId,who.connectionId).first<{lease_token:string|null}>();if(active?.lease_token?.endsWith(':'+attemptId))return json({kind:'unknown',code:'attempt_in_flight'});throw new WorkspaceError('gameplay_busy',409);}
  owned=true;const completed=await prior();if(completed)return json(completed);
  const observed=await cronObservation(env,who);
  if(!observed.context.canPrepare)throw new WorkspaceError(observed.context.unavailableReason??'cron_precondition_unknown',409);
  if(observed.preconditionHash!==args.expectedPreconditionHash||observed.context.day!==args.expectedDay||observed.context.lastCron!==args.expectedLastCron)throw new WorkspaceError('cron_precondition_changed',409);
  await grant(env,request,who,'execute');await activeProfile(env,who.profileId,who.connectionId);
  const rate=await env.HABITICA_INTEGRATION_DB.prepare('SELECT blocked_until_utc,credential_repair_required FROM integration_read_state WHERE profile_id=? AND connection_id=?').bind(who.profileId,who.connectionId).first<{blocked_until_utc:string|null;credential_repair_required:number}>();
  if(rate?.credential_repair_required)throw new WorkspaceError('credential_repair_required',401);
  if(rate?.blocked_until_utc&&Date.parse(rate.blocked_until_utc)>Date.now())throw new ReadFailure('rate_limited',429,{retryAfterSeconds:Math.ceil((Date.parse(rate.blocked_until_utc)-Date.now())/1000)});
  await upstreamBudget(env,who.profileId);
  const reserved=await env.HABITICA_INTEGRATION_DB.prepare(`INSERT OR IGNORE INTO integration_projection_attempts(profile_id,connection_id,attempt_id,payload_hash,operation,state,created_at_utc) SELECT ?,?,?,?,'cron','dispatched',? FROM integration_projection_state WHERE profile_id=? AND connection_id=? AND lease_token=? AND lease_until_utc>? AND NOT EXISTS(SELECT 1 FROM integration_projection_attempts WHERE profile_id=? AND connection_id=? AND state IN ('dispatched','unknown'))`).bind(who.profileId,who.connectionId,attemptId,hash,new Date().toISOString(),who.profileId,who.connectionId,lease,new Date(Date.now()+11000).toISOString(),who.profileId,who.connectionId).run();
  if(!reserved.meta.changes){const found=await prior();if(found)return json(found);throw new WorkspaceError('cron_busy_or_stale',409);}dispatched=true;
  const response=await upstreamFetch(env,'https://habitica.com/api/v3/cron',{method:'POST',redirect:'manual',headers:{'x-api-user':observed.credential.habiticaUserId,'x-api-key':observed.credential.apiToken,'x-client':env.HABITICA_X_CLIENT_HEADER,'content-type':'application/json',accept:'application/json'},body:'{}',signal:AbortSignal.timeout(10000)},who!.profileId);
  await observeUpstream(env,response,who!.profileId);
  const retryAtUtc=await rateState(env,who,response,false);let outcome:Outcome={kind:'unknown',code:'outcome_unknown',...(retryAtUtc?{retryAtUtc}:{})};
  if(response.status===200){const result=workspaceObject(await boundedJson(response,2097152));const data=workspaceObject(result.data);if(result.success===true&&Object.keys(data).length===0)outcome={kind:'applied',code:'applied'};}
  await record(outcome);return json(await prior()??outcome);
 }catch(error){
  if(dispatched){const outcome:Outcome={kind:'unknown',code:'outcome_unknown'};try{await record(outcome);}catch{/* The durable dispatch still fences gameplay. */}return json(outcome);}
  const known=error instanceof WorkspaceError||error instanceof VaultError||error instanceof ReadFailure;
  const outcome:Outcome={kind:'refused',code:known?error.code:'cron_unavailable',...(error instanceof TrafficLimit?{retryAtUtc:error.retryAtUtc}:error instanceof ReadFailure&&typeof error.rate.retryAfterSeconds==='number'?{retryAtUtc:new Date(Date.now()+error.rate.retryAfterSeconds*1000).toISOString()}:{})};
  if(owned&&who&&attemptId&&hash&&outcome.code!=='unauthorized'&&outcome.code!=='insufficient_scope'&&!(error instanceof VaultError)){
   try{const held=await env.HABITICA_INTEGRATION_DB.prepare('SELECT 1 FROM integration_projection_state WHERE profile_id=? AND connection_id=? AND lease_token=? AND lease_until_utc>?').bind(who.profileId,who.connectionId,lease!,new Date().toISOString()).first();if(held){await record(outcome);return json(await prior()??outcome,known?error.status:503);}}catch{return json({kind:'refused',code:'cron_unavailable'},503);}
  }
  return json(outcome,known?error.status:503);
 }finally{if(owned&&who&&lease){try{await env.HABITICA_INTEGRATION_DB.prepare('UPDATE integration_projection_state SET lease_token=NULL,lease_until_utc=NULL WHERE profile_id=? AND connection_id=? AND lease_token=?').bind(who.profileId,who.connectionId,lease).run();}catch{/* Expiry bounds ownership. */}}}
}
