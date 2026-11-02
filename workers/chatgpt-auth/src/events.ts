import {createHmac} from 'node:crypto';
import {encode,decode} from './cryptography.ts';
import {activeProfile,VaultError} from './vault.ts';
import {validatedToken} from './oauth.ts';
import {resourceConfiguration} from '../../shared/resources.ts';
import {eventRequest,eventDefinition,eventToolNames,eventsScope,type EventPrincipal,type EventSubscription,type EventContext} from '../../shared/events.ts';
import {workspaceJson,workspaceObject,WorkspaceError} from '../../shared/workspace-http.ts';
import {egressVersion,egressHash,egressMac,egressMatches,egressKeys,serviceUrl,type EgressGrant} from './callback-egress-protocol.ts';
import type {CallbackEgressEnv} from './callback-egress.ts';

export interface EventsAuthEnv extends AuthEnv {CALLBACK_EGRESS_GRANT_KEY?:string;CALLBACK_EGRESS_CLAIM_KEY?:string;CALLBACK_VAULT_KEY_V1?:string;CALLBACK_VAULT_KEY_V2?:string}
interface Material {url:string;secret:string;consentEpoch?:string;previousSecret?:string;previousUntil?:number}
interface VaultRow {profile_id:string;task_id:string;subscription_id:string;revision:number;callback_id:string;connection_id:string;grant_epoch:string;consent_epoch:string;operation_id:string;ciphertext:string;iv:string;key_version:string;expires_at:number;request_id:string;state:string;receipt_json:string|null}
class EventLifecycleError extends Error {
 readonly code:string;readonly status=409;
 readonly original:{subscriptionId:string;operationId:string;requestId:string|null;revision:number};
 constructor(code:string,original:EventLifecycleError['original']){super(code);this.code=code;this.original=original;}
}
function keyBytes(value:unknown){if(typeof value!=='string'||!/^[A-Za-z0-9_-]{43}$/.test(value))throw new VaultError('events_configuration_unavailable',503);const bytes=decode(value);if(bytes.length!==32||encode(bytes)!==value)throw new VaultError('events_configuration_unavailable',503);return bytes;}
export function eventsConfiguration(env:EventsAuthEnv){
 if(!Boolean(env.EVENTS_ENABLED)||!Boolean(env.CALLBACK_EGRESS_ENABLED)||!env.EVENTS_WORKSPACE)throw new VaultError('events_unavailable',503);
 const version=env.CALLBACK_VAULT_CURRENT_KEY_VERSION??'1',keys={'1':env.CALLBACK_VAULT_KEY_V1,'2':env.CALLBACK_VAULT_KEY_V2};
 if(version!=='1'&&version!=='2')throw new VaultError('events_configuration_unavailable',503);
 keyBytes(keys[version]);const all=Object.values(keys).filter((x):x is string=>Boolean(x));all.forEach(keyBytes);
 const other=[env.PROFILE_ID_KEY,env.CREDENTIAL_KEY_V1,env.CREDENTIAL_KEY_V2,env.CALLBACK_EGRESS_GRANT_KEY,env.CALLBACK_EGRESS_CLAIM_KEY,(env as AuthEnv&{PUSH_VAULT_KEY_V1?:string}).PUSH_VAULT_KEY_V1,(env as AuthEnv&{PUSH_VAULT_KEY_V2?:string}).PUSH_VAULT_KEY_V2].filter(Boolean);
 if(new Set([...all,...other]).size!==all.length+other.length)throw new VaultError('events_configuration_unavailable',503);
 egressKeys(env.CALLBACK_EGRESS_GRANT_KEY!,env.CALLBACK_EGRESS_CLAIM_KEY!);serviceUrl(env.CALLBACK_EGRESS_AUDIENCE);
 return {version,key:keys[version]!,keys};
}
function aad(row:Pick<VaultRow,'profile_id'|'subscription_id'|'revision'|'connection_id'|'grant_epoch'>,version:string){return new TextEncoder().encode(`companion-events-v1:${row.profile_id}:${row.subscription_id}:${row.revision}:${row.connection_id}:${row.grant_epoch}:${version}`);}
async function encrypt(env:EventsAuthEnv,row:Parameters<typeof aad>[0],material:Material){
 const {version,key}=eventsConfiguration(env),iv=crypto.getRandomValues(new Uint8Array(12)),k=await crypto.subtle.importKey('raw',keyBytes(key),'AES-GCM',false,['encrypt']);
 const ciphertext=await crypto.subtle.encrypt({name:'AES-GCM',iv,additionalData:aad(row,version)},k,new TextEncoder().encode(JSON.stringify(material)));return {ciphertext:encode(new Uint8Array(ciphertext)),iv:encode(iv),version};
}
async function decrypt(env:EventsAuthEnv,row:VaultRow):Promise<Material>{
 const {keys}=eventsConfiguration(env),key=keys[row.key_version as '1'|'2'];const k=await crypto.subtle.importKey('raw',keyBytes(key),'AES-GCM',false,['decrypt']);
 const raw=await crypto.subtle.decrypt({name:'AES-GCM',iv:decode(row.iv),additionalData:aad(row,row.key_version)},k,decode(row.ciphertext));return JSON.parse(new TextDecoder().decode(raw));
}
export async function eventsWorkspace<T>(env:EventsAuthEnv,who:EventPrincipal,path:string,input:unknown):Promise<T>{
 const r=await env.EVENTS_WORKSPACE!.fetch('https://events.internal'+path,{method:'POST',body:JSON.stringify({principal:who,input}),signal:AbortSignal.timeout(5000)});
 if(!r.ok){const v=await r.json<{error?:string}>();throw new VaultError(v.error??'events_resource_unavailable',r.status);}return r.json<T>();
}
function signature(secret:string,id:string,timestamp:string,body:string){return 'v1,'+createHmac('sha256',Buffer.from(secret.slice(6),'base64')).update(`${id}.${timestamp}.${body}`).digest('base64');}
export async function sendEventBytes(env:EventsAuthEnv,material:Material,subscriptionId:string,body:string,eventId:string,requestId:string,operation:'challenge'|'event'){
 eventsConfiguration(env);const stamp=String(Math.floor(Date.now()/1000));let sig=signature(material.secret,eventId,stamp,body);
 if(material.previousSecret&&Number(material.previousUntil)>Date.now())sig+=' '+signature(material.previousSecret,eventId,stamp,body);
 const payload={url:material.url,body:Buffer.from(body).toString('base64'),headers:{'content-type':'application/json','webhook-id':eventId,'webhook-timestamp':stamp,'webhook-signature':sig,'x-mcp-subscription-id':subscriptionId}};
 const grant:EgressGrant={version:egressVersion,requestId,issuer:resourceConfiguration(env.ENVIRONMENT,env).issuer,audience:env.CALLBACK_EGRESS_AUDIENCE!,operation,payloadHash:egressHash(JSON.stringify(payload)),issuedAt:Number(stamp),expiresAt:Number(stamp)+30};
 // A private Auth service sends only to the fixed configured adapter; it never fetches a callback itself.
 const r=await fetch(grant.audience,{method:'POST',redirect:'manual',headers:{'content-type':'application/json'},body:JSON.stringify({grant,mac:egressMac(env.CALLBACK_EGRESS_GRANT_KEY!,'grant',grant),payload}),signal:AbortSignal.timeout(12000)});
 if(!r.ok)throw new VaultError('callback_outcome_unknown',503);
 const result=workspaceObject(await workspaceJson(new Request('https://adapter.internal/result',{method:'POST',body:r.body}),16384),['requestId','state','status','responseBody','code']);
 if(result.requestId!==requestId)throw new VaultError('callback_outcome_unknown',503);return result;
}
async function currentPrincipal(env:EventsAuthEnv,who:EventPrincipal){const p=await activeProfile(env,who.profileId,who.connectionId);if(p.grant_epoch!==who.grantEpoch)throw new VaultError('unauthorized',401);}
export async function eventProtocol(env:EventsAuthEnv,who:EventPrincipal,method:string,params:unknown){
 eventsConfiguration(env);await currentPrincipal(env,who);
 if(method==='events/list'){workspaceObject(params??{},[]);return {events:[eventDefinition]};}
 const subscribe=method==='events/subscribe',input=eventRequest(params,subscribe),id=egressHash(JSON.stringify([who.profileId,input.url,input.name,input.arguments]));
 if(!subscribe){
  const receipt=await eventsWorkspace(env,who,'/unsubscribe',{subscriptionId:id});
  await env.HABITICA_INTEGRATION_DB.prepare('DELETE FROM integration_event_callbacks WHERE profile_id=? AND subscription_id=?').bind(who.profileId,id).run();return {};
 }
 const context=await eventsWorkspace<EventContext>(env,who,'/context',{taskId:input.arguments.taskId});
 const old=await env.HABITICA_INTEGRATION_DB.prepare('SELECT * FROM integration_event_callbacks WHERE profile_id=? AND subscription_id=? ORDER BY revision DESC LIMIT 1').bind(who.profileId,id).first<VaultRow>();
 if(old?.state==='verified'&&old.consent_epoch===context.consentEpoch&&old.expires_at<=Date.now()+input.ttlMs&&old.expires_at>Date.now()+Math.max(30000,input.ttlMs-60000)&&old.connection_id===who.connectionId&&old.grant_epoch===who.grantEpoch){
  const material=await decrypt(env,old),current=await eventsWorkspace<EventSubscription>(env,who,'/status',{subscriptionId:id});
  if(material.secret===input.secret&&current?.state==='active'&&current.expiresAt>Date.now())return {id,refreshBefore:new Date(current.expiresAt).toISOString(),cursor:null,truncated:false};
 }
 const operationId=crypto.randomUUID(),callbackId=old?.callback_id??crypto.randomUUID();
 const reserved=await eventsWorkspace<EventSubscription>(env,who,'/reserve',{subscriptionId:id,callbackId,taskId:input.arguments.taskId,operationId,expiresAt:Date.now()+input.ttlMs});
 if(reserved.operationId!==operationId)throw new EventLifecycleError('subscription_original_operation_pending',{subscriptionId:id,operationId:reserved.operationId,requestId:old?.operation_id===reserved.operationId?old.request_id:null,revision:reserved.revision});
 let material:Material={url:input.url,secret:input.secret,consentEpoch:reserved.consentEpoch};
 if(old?.state==='verified'&&old.connection_id===who.connectionId&&old.grant_epoch===who.grantEpoch){const previous=await decrypt(env,old);if(previous.consentEpoch===reserved.consentEpoch&&previous.secret!==material.secret)material={...material,previousSecret:previous.secret,previousUntil:Math.min(Date.now()+300000,reserved.expiresAt)};}
 const row={profile_id:who.profileId,subscription_id:id,revision:reserved.revision,connection_id:who.connectionId,grant_epoch:who.grantEpoch},encrypted=await encrypt(env,row,material),requestId=crypto.randomUUID(),challenge=encode(crypto.getRandomValues(new Uint8Array(32))),body=JSON.stringify({type:'verification',challenge}),eventId=crypto.randomUUID();
 const inserted=await env.HABITICA_INTEGRATION_DB.prepare('INSERT OR IGNORE INTO integration_event_callbacks SELECT ?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,NULL WHERE (SELECT COUNT(*) FROM integration_event_callbacks WHERE profile_id=?)<4096 AND (SELECT COUNT(*) FROM integration_event_callbacks)<20000').bind(who.profileId,input.arguments.taskId,id,reserved.revision,reserved.callbackId,who.connectionId,who.grantEpoch,reserved.consentEpoch,operationId,encrypted.ciphertext,encrypted.iv,encrypted.version,reserved.expiresAt,requestId,'pending',who.profileId).run();
 if(!inserted.meta.changes)throw new VaultError('subscription_original_operation_pending',409);
 let verified=false,failureCode:string|null=null;try{
  await currentPrincipal(env,who);await eventsWorkspace(env,who,'/context',{taskId:input.arguments.taskId});
  const response=await sendEventBytes(env,material,id,body,eventId,requestId,'challenge');
  if(response.state==='response'&&Number(response.status)>=200&&Number(response.status)<300&&typeof response.responseBody==='string'){
   const echo=workspaceObject(JSON.parse(Buffer.from(response.responseBody,'base64').toString('utf8')),['challenge']);
   verified=typeof echo.challenge==='string'&&egressMatches(egressHash(challenge),egressHash(echo.challenge));
  }
 }catch(e){failureCode=e instanceof VaultError?e.code:e instanceof Error?e.name:'transport_unknown';/* Preserve uncertainty without persisting credential-bearing exception text. */}
 const receipt={subscriptionId:id,revision:reserved.revision,operationId,requestId,state:verified?'verified':'unknown',challengeVerified:verified,failureCode,chatResponse:null};
 await env.HABITICA_INTEGRATION_DB.prepare('UPDATE integration_event_callbacks SET state=?,receipt_json=? WHERE profile_id=? AND subscription_id=? AND revision=? AND operation_id=?').bind(receipt.state,JSON.stringify(receipt),who.profileId,id,reserved.revision,operationId).run();
 await currentPrincipal(env,who);
 const final=await eventsWorkspace<EventSubscription>(env,who,verified?'/activate':'/unknown',{subscriptionId:id,revision:reserved.revision,operationId});
 if(!verified||final?.state!=='active')throw new EventLifecycleError('callback_challenge_failed',{subscriptionId:id,operationId,requestId,revision:reserved.revision});
 return {id,refreshBefore:new Date(final.expiresAt).toISOString(),cursor:null,truncated:false};
}
export async function eventsOperation(request:Request,env:EventsAuthEnv):Promise<Response>{
 try{
  eventsConfiguration(env);const token=request.headers.get('authorization')?.match(/^Bearer ([^ ]+)$/)?.[1];if(!token)throw new VaultError('unauthorized',401);
  const validated=await validatedToken(env,resourceConfiguration(env.ENVIRONMENT,env).resource,token);
  if(!validated||!validated.props.grantEpoch)throw new VaultError('unauthorized',401);
  if(!['profile:read','tasks:read',eventsScope].every(s=>validated.scope.includes(s)))throw new VaultError('insufficient_scope',403);
  const who:EventPrincipal={...validated.props,grantEpoch:validated.props.grantEpoch},v=workspaceObject(await workspaceJson(request,16384),['method','params']);
  let result;
  if(v.method==='get_event_delivery')result=await eventsWorkspace(env,who,'/delivery-receipt',v.params);
  else if(v.method==='get_event_subscription'){
   const args=workspaceObject(v.params,['subscriptionId']);if(typeof args.subscriptionId!=='string'||!/^[a-f0-9]{64}$/.test(args.subscriptionId))throw new VaultError('invalid_subscription',422);
   const original=await env.HABITICA_INTEGRATION_DB.prepare('SELECT receipt_json FROM integration_event_callbacks WHERE profile_id=? AND connection_id=? AND grant_epoch=? AND subscription_id=? ORDER BY revision DESC LIMIT 1').bind(who.profileId,who.connectionId,who.grantEpoch,args.subscriptionId).first<{receipt_json:string|null}>();
   result={subscription:await eventsWorkspace(env,who,'/subscription-status',args),receipt:original?.receipt_json?JSON.parse(original.receipt_json):null,chatResponse:null};
  }else if((eventToolNames as readonly string[]).includes(String(v.method)))result=await eventsWorkspace(env,who,'/consent',{tool:v.method,arguments:v.params});
  else if(['events/list','events/subscribe','events/unsubscribe'].includes(String(v.method)))result=await eventProtocol(env,who,String(v.method),v.params);
  else throw new VaultError('invalid_event_method',422);
  if(v.method==='accept_event_consent'){const r=result as {receipt?:{retiredConsentEpoch?:string;taskId?:string}};if(r.receipt?.retiredConsentEpoch)await env.HABITICA_INTEGRATION_DB.prepare('DELETE FROM integration_event_callbacks WHERE profile_id=? AND task_id=? AND consent_epoch=?').bind(who.profileId,r.receipt.taskId,r.receipt.retiredConsentEpoch).run();}
  return Response.json(result,{headers:{'cache-control':'no-store'}});
 }catch(e){const known=e instanceof VaultError||e instanceof WorkspaceError||e instanceof EventLifecycleError;return Response.json({error:known?e.code:'events_unavailable',...(e instanceof EventLifecycleError?{original:e.original}:{})},{status:known?e.status:503,headers:{'cache-control':'no-store'}});}
}
export async function clearEvents(env:AuthEnv,profileId:string,connectionId:string){await env.HABITICA_INTEGRATION_DB.prepare('DELETE FROM integration_event_callbacks WHERE profile_id=? AND connection_id=?').bind(profileId,connectionId).run();}
export async function eventCallbackMaterial(env:EventsAuthEnv,who:EventPrincipal,subscription:EventSubscription):Promise<Material>{
 await currentPrincipal(env,who);
 const row=await env.HABITICA_INTEGRATION_DB.prepare('SELECT * FROM integration_event_callbacks WHERE profile_id=? AND subscription_id=? AND revision=? AND callback_id=? AND connection_id=? AND grant_epoch=? AND consent_epoch=? AND state=\'verified\' AND expires_at>?').bind(who.profileId,subscription.subscriptionId,subscription.revision,subscription.callbackId,who.connectionId,who.grantEpoch,subscription.consentEpoch,Date.now()).first<VaultRow>();
 if(!row)throw new VaultError('callback_authority_unavailable',409);return decrypt(env,row);
}
