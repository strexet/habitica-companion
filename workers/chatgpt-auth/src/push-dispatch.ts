import {activeProfile,VaultError} from './vault';
import {decryptPush,pushConfiguration,pushHash,type PushEnv} from './push-cryptography';
import {pushMessage} from './push-message';
import {uuid,workspaceObject,workspaceJson,WorkspaceError} from '../../shared/workspace-http';
interface Dispatch {profile_id:string;dispatch_id:string;connection_id:string;device_id:string;subscription_id:string;device_revision:number;consent_epoch:string;payload_hash:string;expires_at_ms:number;state:string;code:string;http_status:number|null;retry_after_ms:number|null;created_at_ms:number;updated_at_ms:number}
interface Device {connection_id:string;revision:number;subscription_id:string;state:string;consent_epoch:string;expires_at_ms:number|null;ciphertext:string|null;iv:string|null;key_version:string|null;vapid_key_hash:string|null}
const db=(env:PushEnv)=>env.HABITICA_INTEGRATION_DB;
const receipt=(env:PushEnv,profile:string,id:string)=>db(env).prepare('SELECT * FROM integration_push_dispatches WHERE profile_id=? AND dispatch_id=?').bind(profile,id).first<Dispatch>();
const view=(d:Dispatch)=>({requestId:d.dispatch_id,state:['admitted','dispatched'].includes(d.state)?'unknown':d.state,code:['admitted','dispatched'].includes(d.state)?'attempt_incomplete':d.code,httpStatus:d.http_status,retryAfterMs:d.retry_after_ms,attempted:['dispatched','accepted','unknown','retryable'].includes(d.state)||d.http_status!==null,acceptedByPushService:d.state==='accepted',shown:null,read:null});
const validDevice=`EXISTS(SELECT 1 FROM integration_push_devices d JOIN integration_push_consents c ON c.profile_id=d.profile_id
 WHERE d.profile_id=? AND d.device_id=? AND d.connection_id=? AND d.subscription_id=? AND d.revision=? AND d.state='active' AND d.consent_epoch=? AND (d.expires_at_ms IS NULL OR d.expires_at_ms>?)
 AND c.connection_id=d.connection_id AND c.consent_epoch=d.consent_epoch AND c.enabled=1
 AND EXISTS(SELECT 1 FROM integration_profiles p WHERE p.profile_id=d.profile_id AND p.connection_id=d.connection_id AND p.revoked_at_utc IS NULL AND p.credential_ciphertext IS NOT NULL))`;
function retryAfter(value:string|null,now:number):number {
 if(value&&/^\d{1,10}$/.test(value))return Math.min(Number.MAX_SAFE_INTEGER,now+Math.max(1,Number(value))*1000);
 const parsed=value?Date.parse(value):NaN;return Number.isFinite(parsed)&&parsed>now?parsed:now+60000;
}
export async function pushDispatchRoute(request:Request,env:PushEnv):Promise<Response>{
 const headers={'cache-control':'no-store','x-content-type-options':'nosniff'};
 try {
  if(request.method!=='POST')throw new VaultError('invalid_request',405);
  const path=new URL(request.url).pathname,lookup=path==='/receipt';if(!lookup&&path!=='/dispatch')throw new VaultError('not_found',404);
  const input=workspaceObject(await workspaceJson(request,4096),lookup?['profileId','connectionId','requestId']:['profileId','connectionId','requestId','deviceId','subscriptionId','deviceRevision','consentEpoch','expiresAtMs','workspaceId','taskId','scheduleId','scheduleRevision','occurrenceId']);
  if(typeof input.profileId!=='string'||!/^prf_[A-Za-z0-9_-]{43}$/.test(input.profileId))throw new VaultError('invalid_profile',422);
  const profile=input.profileId,connection=uuid(input.connectionId),id=uuid(input.requestId);await activeProfile(env,profile,connection);
  let existing=await receipt(env,profile,id);
  if(lookup)return Response.json({receipt:existing?.connection_id===connection?view(existing):null},{headers});
  if(!env.PUSH_DISPATCH_ENABLED)throw new VaultError('push_dispatch_unavailable',503);
  const deviceId=uuid(input.deviceId),subscriptionId=uuid(input.subscriptionId),consentEpoch=uuid(input.consentEpoch),workspaceId=uuid(input.workspaceId),taskId=uuid(input.taskId),scheduleId=uuid(input.scheduleId),occurrenceId=uuid(input.occurrenceId);
  if(!Number.isSafeInteger(input.deviceRevision)||Number(input.deviceRevision)<1||!Number.isSafeInteger(input.scheduleRevision)||Number(input.scheduleRevision)<1||!Number.isSafeInteger(input.expiresAtMs))throw new VaultError('invalid_dispatch',422);
  const revision=Number(input.deviceRevision),expires=Number(input.expiresAtMs),hash=await pushHash(JSON.stringify({connection,deviceId,subscriptionId,revision,consentEpoch,expires,workspaceId,taskId,scheduleId,scheduleRevision:input.scheduleRevision,occurrenceId}));
  if(existing){if(existing.payload_hash!==hash||existing.connection_id!==connection)throw new VaultError('push_dispatch_conflict',409);return Response.json(view(existing),{headers});}
  const now=Date.now();if(expires<=now||expires>now+3600000)throw new VaultError('push_expired',409);
  const publicKey=await pushConfiguration(env);
  const device=await db(env).prepare('SELECT * FROM integration_push_devices WHERE profile_id=? AND device_id=?').bind(profile,deviceId).first<Device>();
  const args=[profile,deviceId,connection,subscriptionId,revision,consentEpoch,now];
  if(!device||device.vapid_key_hash!==await pushHash(publicKey)||!device.ciphertext||!device.iv||!device.key_version||!await db(env).prepare('SELECT 1 WHERE '+validDevice).bind(...args).first())throw new VaultError('push_target_unavailable',409);
  const material=await decryptPush(env,profile,deviceId,revision,{ciphertext:device.ciphertext,iv:device.iv,keyVersion:device.key_version});
  const payload=JSON.stringify({version:1,type:'task-review',title:'Companion review cue',body:'Open Companion to review a saved cue. Reviewing is optional.',workspaceId,taskId,scheduleId,scheduleRevision:input.scheduleRevision,occurrenceId,expiresAtMs:expires,url:'/todo/today'});
  const message=await pushMessage(env,material,payload,expires,now);
  const reserved=await db(env).prepare(`INSERT OR IGNORE INTO integration_push_dispatches(profile_id,dispatch_id,connection_id,device_id,subscription_id,device_revision,consent_epoch,payload_hash,expires_at_ms,state,code,created_at_ms,updated_at_ms)
   SELECT ?,?,?,?,?,?,?,?,?,'admitted','reserved',?,? WHERE ${validDevice}
   AND (SELECT COUNT(*) FROM integration_push_dispatches WHERE profile_id=?)<8192
   AND (SELECT COUNT(*) FROM integration_push_dispatches WHERE profile_id=? AND created_at_ms>?)<20
   AND NOT EXISTS(SELECT 1 FROM integration_push_dispatches WHERE profile_id=? AND retry_after_ms>?)`)
   .bind(profile,id,connection,deviceId,subscriptionId,revision,consentEpoch,hash,expires,now,now,...args,profile,profile,now-3600000,profile,now).run();
  if(!reserved.meta.changes){existing=await receipt(env,profile,id);if(existing){if(existing.payload_hash!==hash)throw new VaultError('push_dispatch_conflict',409);return Response.json(view(existing),{headers});}throw new VaultError('push_quota_or_context_changed',429);}
  // An admission or acknowledgement failure is recovered by the original receipt, never another send.
  const begun=await db(env).prepare(`UPDATE integration_push_dispatches SET state='dispatched',code='attempted',updated_at_ms=? WHERE profile_id=? AND dispatch_id=? AND state='admitted' AND expires_at_ms>? AND ${validDevice}`).bind(Date.now(),profile,id,Date.now(),profile,deviceId,connection,subscriptionId,revision,consentEpoch,Date.now()).run();
  if(!begun.meta.changes){await db(env).prepare("UPDATE integration_push_dispatches SET state='refused',code='context_changed',updated_at_ms=? WHERE profile_id=? AND dispatch_id=? AND state='admitted'").bind(Date.now(),profile,id).run();return Response.json(view((await receipt(env,profile,id))!),{headers});}
  let state='unknown',code='transport_unknown',status:number|null=null,retry:number|null=null;
  try {
   // Workers rejects Fetch's error mode at construction. Manual mode plus explicit3xx refusal never follows Location.
   const response=await fetch(material.endpoint,{method:'POST',headers:message.headers,body:message.body,redirect:'manual',signal:AbortSignal.timeout(5000)});status=response.status;
   if(response.status===201){state='accepted';code='accepted_by_push_service';}
   else if(response.status===429){state='retryable';code='provider_rate_limited';retry=retryAfter(response.headers.get('retry-after'),Date.now());}
   else if(response.status===404||response.status===410){state='refused';code='subscription_gone';}
   else if(response.status>=300&&response.status<400){state='refused';code='provider_redirect_refused';}
   else if(response.status>=400&&response.status<500){state='refused';code='provider_refused';}
   else {state='unknown';code='provider_outcome_unknown';}
   await response.body?.cancel();
  } catch { /* The durable original attempt remains uncertain. No transport exception text is retained. */ }
  const statements=[db(env).prepare("UPDATE integration_push_dispatches SET state=?,code=?,http_status=?,retry_after_ms=?,updated_at_ms=? WHERE profile_id=? AND dispatch_id=? AND state='dispatched'").bind(state,code,status,retry,Date.now(),profile,id)];
  if(code==='subscription_gone')statements.push(db(env).prepare("UPDATE integration_push_devices SET state='expired',revision=MIN(revision+1,9007199254740991),endpoint_hash=NULL,ciphertext=NULL,iv=NULL,key_version=NULL,vapid_key_hash=NULL,updated_at_utc=? WHERE profile_id=? AND device_id=? AND revision=? AND subscription_id=?").bind(new Date().toISOString(),profile,deviceId,revision,subscriptionId));
  await db(env).batch(statements);
  return Response.json(view((await receipt(env,profile,id))!),{headers});
 } catch(error){const known=error instanceof VaultError||error instanceof WorkspaceError;return Response.json({error:known?error.code:'push_outcome_unknown',outcomeUnknown:!known}, {status:known?error.status:503,headers});}
}
