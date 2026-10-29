import {managedProfile,activeProfile,VaultError,type IntegrationProfile} from './vault';
import {workspaceJson,workspaceObject,uuid,WorkspaceError} from '../../shared/workspace-http';
import {trafficFailure} from './traffic';
import {pushConfiguration,pushMaterial,pushHash,encryptPush,type PushEnv} from './push-cryptography';

const headers={'cache-control':'no-store','x-content-type-options':'nosniff'};
const consentVersion='web-push-v1';
const maximumDevices=64,maximumReceipts=1000;
interface Consent {connection_id:string;consent_epoch:string;enabled:number}
interface Device {device_id:string;connection_id:string;revision:number;subscription_id:string;state:string;expires_at_ms:number|null;endpoint_hash:string|null}
interface Receipt {payload_hash:string;result_json:string}
const authority=`EXISTS(SELECT 1 FROM integration_profiles WHERE profile_id=? AND connection_id=? AND management_token_hash=? AND revoked_at_utc IS NULL AND credential_ciphertext IS NOT NULL) AND (SELECT COUNT(*) FROM integration_push_commands WHERE profile_id=?)<${maximumReceipts}`;
const authorityArgs=(p:IntegrationProfile)=>[p.profile_id,p.connection_id,p.management_token_hash,p.profile_id];
const reply=(body:unknown,status=200)=>Response.json(body,{status,headers});
async function consent(env:AuthEnv,profileId:string):Promise<Consent|null>{return env.HABITICA_INTEGRATION_DB.prepare('SELECT connection_id,consent_epoch,enabled FROM integration_push_consents WHERE profile_id=?').bind(profileId).first<Consent>();}
function currentEpoch(row:Consent|null,p:IntegrationProfile){return row?.connection_id===p.connection_id?row.consent_epoch:null;}
function epoch(value:unknown):string|null{return value===null?null:uuid(value);}
function revision(value:unknown):number{if(typeof value!=='number'||!Number.isSafeInteger(value)||value<0||value>=Number.MAX_SAFE_INTEGER)throw new VaultError('invalid_push_revision',422);return value;}
async function original(env:AuthEnv,profileId:string,requestId:string,hash?:string){
 const row=await env.HABITICA_INTEGRATION_DB.prepare('SELECT payload_hash,result_json FROM integration_push_commands WHERE profile_id=? AND request_id=? AND expires_at_utc>?').bind(profileId,requestId,new Date().toISOString()).first<Receipt>();
 if(row&&hash!==undefined&&row.payload_hash!==hash)throw new VaultError('push_request_conflict',409);
 return row?JSON.parse(row.result_json):null;
}
function receipt(env:AuthEnv,p:IntegrationProfile,requestId:string,hash:string,result:unknown,guard:string,values:unknown[]){
 const now=new Date(),expires=new Date(now.getTime()+30*86400000);
 return env.HABITICA_INTEGRATION_DB.prepare(`INSERT INTO integration_push_commands(profile_id,request_id,connection_id,payload_hash,result_json,created_at_utc,expires_at_utc)
 SELECT ?,?,?,?,?,?,? WHERE ${guard}`).bind(p.profile_id,requestId,p.connection_id,hash,JSON.stringify(result),now.toISOString(),expires.toISOString(),...values);
}
async function capacity(env:AuthEnv,p:IntegrationProfile){
 const row=await env.HABITICA_INTEGRATION_DB.prepare('SELECT COUNT(*) AS n FROM integration_push_commands WHERE profile_id=?').bind(p.profile_id).first<{n:number}>();
 if(!row||row.n>=maximumReceipts)throw new VaultError('push_receipt_capacity',413);
}
export async function expirePush(env:AuthEnv,profileId?:string):Promise<void>{
 const now=new Date().toISOString(),scope=profileId?' AND profile_id=?':'',parameters=profileId?[profileId]:[];
 await env.HABITICA_INTEGRATION_DB.batch([
  env.HABITICA_INTEGRATION_DB.prepare(`UPDATE integration_push_devices SET state='expired',revision=MIN(revision+1,9007199254740991),endpoint_hash=NULL,ciphertext=NULL,iv=NULL,key_version=NULL,vapid_key_hash=NULL,updated_at_utc=?
   WHERE rowid IN(SELECT rowid FROM integration_push_devices WHERE state='active' AND expires_at_ms<=?${scope} LIMIT 128)`).bind(now,Date.now(),...parameters),
  env.HABITICA_INTEGRATION_DB.prepare(`DELETE FROM integration_push_commands WHERE rowid IN(SELECT rowid FROM integration_push_commands WHERE expires_at_utc<=?${scope} LIMIT 128)`).bind(now,...parameters),
 ]);
}
export async function pushStatus(env:PushEnv,p:IntegrationProfile){
 await expirePush(env,p.profile_id);const c=await consent(env,p.profile_id),consentEpoch=currentEpoch(c,p);
 let publicKey:string|null=null;try{publicKey=await pushConfiguration(env);}catch{/* Status and revocation remain available with missing/offline signing configuration. */}
 const devices=await env.HABITICA_INTEGRATION_DB.prepare('SELECT device_id,connection_id,revision,subscription_id,state,expires_at_ms,endpoint_hash FROM integration_push_devices WHERE profile_id=? ORDER BY device_id LIMIT 65').bind(p.profile_id).all<Device>();
 if(devices.results.length>maximumDevices)throw new VaultError('push_device_capacity',413);
 return {profileId:p.profile_id,connectionId:p.connection_id,available:publicKey!==null,consentVersion,consentEpoch,enabled:consentEpoch!==null&&c?.enabled===1,applicationServerKey:publicKey,
  devices:devices.results.map(d=>({deviceId:d.device_id,revision:d.revision,subscriptionId:d.subscription_id,state:d.connection_id===p.connection_id?d.state:'revoked',expiresAtMs:d.expires_at_ms}))};
}
async function consentCommand(request:Request,env:PushEnv,p:IntegrationProfile){
 const body=workspaceObject(await workspaceJson(request,2048),['requestId','connectionId','expectedConsentEpoch','consentVersion','enabled']);
 const requestId=uuid(body.requestId),connectionId=uuid(body.connectionId),expected=epoch(body.expectedConsentEpoch);
 if(body.consentVersion!==consentVersion||typeof body.enabled!=='boolean')throw new VaultError('push_consent_required',403);
 const payload={operation:'consent',requestId,connectionId,expectedConsentEpoch:expected,consentVersion,enabled:body.enabled},hash=await pushHash(JSON.stringify(payload));
 const previous=await original(env,p.profile_id,requestId,hash);if(previous)return reply(previous);
 if(connectionId!==p.connection_id)throw new VaultError('connection_changed',409);
 if(body.enabled)await pushConfiguration(env);
 await expirePush(env,p.profile_id);await capacity(env,p);const old=await consent(env,p.profile_id);if(currentEpoch(old,p)!==expected)throw new VaultError('push_consent_changed',409);
 const nextEpoch=expected!==null&&old?.enabled===Number(body.enabled)?expected:crypto.randomUUID(),now=new Date().toISOString();
 const result={profileId:p.profile_id,connectionId,requestId,operation:'consent',consentEpoch:nextEpoch,enabled:body.enabled,saved:true};
 let changed;try{changed=await env.HABITICA_INTEGRATION_DB.batch([
  env.HABITICA_INTEGRATION_DB.prepare(`INSERT INTO integration_push_consents(profile_id,connection_id,consent_epoch,enabled,consent_version,updated_at_utc)
   SELECT ?,?,?,?,?,? WHERE ${authority} AND COALESCE((SELECT consent_epoch FROM integration_push_consents WHERE profile_id=? AND connection_id=?),'')=?
   ON CONFLICT(profile_id) DO UPDATE SET connection_id=excluded.connection_id,consent_epoch=excluded.consent_epoch,enabled=excluded.enabled,consent_version=excluded.consent_version,updated_at_utc=excluded.updated_at_utc`)
   .bind(p.profile_id,connectionId,nextEpoch,Number(body.enabled),consentVersion,now,...authorityArgs(p),p.profile_id,connectionId,expected??''),
  env.HABITICA_INTEGRATION_DB.prepare(`UPDATE integration_push_devices SET state='revoked',revision=MIN(revision+1,9007199254740991),endpoint_hash=NULL,ciphertext=NULL,iv=NULL,key_version=NULL,vapid_key_hash=NULL,updated_at_utc=?
   WHERE profile_id=? AND state='active' AND ?=0 AND ${authority} AND EXISTS(SELECT 1 FROM integration_push_consents WHERE profile_id=? AND connection_id=? AND consent_epoch=? AND enabled=0)`)
   .bind(now,p.profile_id,Number(body.enabled),...authorityArgs(p),p.profile_id,connectionId,nextEpoch),
  receipt(env,p,requestId,hash,result,`${authority} AND EXISTS(SELECT 1 FROM integration_push_consents WHERE profile_id=? AND connection_id=? AND consent_epoch=? AND enabled=?)`,[...authorityArgs(p),p.profile_id,connectionId,nextEpoch,Number(body.enabled)]),
 ]);}catch(error){const recovered=await original(env,p.profile_id,requestId,hash);if(recovered)return reply(recovered);throw error;}
 if(!changed[0].meta.changes||!changed[2].meta.changes)throw new VaultError('push_consent_changed',409);
 await activeProfile(env,p.profile_id,connectionId);return reply(result);
}
async function deviceCommand(request:Request,env:PushEnv,p:IntegrationProfile,unsubscribe:boolean){
 const body=workspaceObject(await workspaceJson(request,8192),['requestId','connectionId','deviceId','expectedRevision','consentEpoch',...(unsubscribe?['subscriptionId']:['subscription','userVisibleOnly'])]);
 const requestId=uuid(body.requestId),connectionId=uuid(body.connectionId),deviceId=uuid(body.deviceId),expectedRevision=revision(body.expectedRevision),consentEpoch=uuid(body.consentEpoch);
 const material=unsubscribe?null:await pushMaterial(body.subscription,true),subscriptionId=unsubscribe?uuid(body.subscriptionId):null;
 if(!unsubscribe&&body.userVisibleOnly!==true)throw new VaultError('user_visible_only_required',422);
 const hash=await pushHash(JSON.stringify({operation:unsubscribe?'unsubscribe':'subscribe',requestId,connectionId,deviceId,expectedRevision,consentEpoch,material,subscriptionId}));
 const previous=await original(env,p.profile_id,requestId,hash);if(previous)return reply(previous);
 if(connectionId!==p.connection_id)throw new VaultError('connection_changed',409);
 if(material?.expirationTime!==null&&material?.expirationTime!==undefined&&material.expirationTime<=Date.now())throw new VaultError('invalid_push_expiry',422);
 const publicKey=unsubscribe?null:await pushConfiguration(env);await expirePush(env,p.profile_id);await capacity(env,p);
 const c=await consent(env,p.profile_id);if(currentEpoch(c,p)!==consentEpoch||!unsubscribe&&c?.enabled!==1)throw new VaultError('push_consent_required',403);
 const device=await env.HABITICA_INTEGRATION_DB.prepare('SELECT device_id,connection_id,revision,subscription_id,state,expires_at_ms,endpoint_hash FROM integration_push_devices WHERE profile_id=? AND device_id=?').bind(p.profile_id,deviceId).first<Device>();
 if((device?.revision??0)!==expectedRevision||unsubscribe&&(!device||device.subscription_id!==subscriptionId))throw new VaultError('push_device_changed',409);
 if(!device){const count=await env.HABITICA_INTEGRATION_DB.prepare('SELECT COUNT(*) AS n FROM integration_push_devices WHERE profile_id=?').bind(p.profile_id).first<{n:number}>();if(!count||count.n>=maximumDevices)throw new VaultError('push_device_capacity',413);}
 const endpointHash=material?await pushHash(material.endpoint):null;
 if(endpointHash){const owner=await env.HABITICA_INTEGRATION_DB.prepare('SELECT profile_id,device_id FROM integration_push_devices WHERE endpoint_hash=?').bind(endpointHash).first<{profile_id:string;device_id:string}>();if(owner&&(owner.profile_id!==p.profile_id||owner.device_id!==deviceId))throw new VaultError('push_endpoint_already_bound',409);}
 const nextRevision=expectedRevision+1,id=subscriptionId??(device?.state==='active'&&device.endpoint_hash===endpointHash?device.subscription_id:crypto.randomUUID()),state=unsubscribe?'unsubscribed':'active',tag=crypto.randomUUID(),now=new Date().toISOString();
 const envelope=material?await encryptPush(env,p.profile_id,deviceId,nextRevision,material):null;
 const result={profileId:p.profile_id,connectionId,requestId,operation:unsubscribe?'unsubscribe':'subscribe',deviceId,subscriptionId:id,revision:nextRevision,state,saved:true};
 const guard=`${authority} AND EXISTS(SELECT 1 FROM integration_push_consents WHERE profile_id=? AND connection_id=? AND consent_epoch=? ${unsubscribe?'':'AND enabled=1'})`;
 const values=[...authorityArgs(p),p.profile_id,connectionId,consentEpoch];
 let changed;try{changed=await env.HABITICA_INTEGRATION_DB.batch([
  env.HABITICA_INTEGRATION_DB.prepare(`INSERT INTO integration_push_devices(profile_id,device_id,connection_id,revision,subscription_id,state,consent_epoch,endpoint_hash,ciphertext,iv,key_version,expires_at_ms,vapid_key_hash,commit_tag,updated_at_utc)
   SELECT ?,?,?,?,?,?,?,?,?,?,?,?,?,?,? WHERE ${guard} AND COALESCE((SELECT revision FROM integration_push_devices WHERE profile_id=? AND device_id=?),0)=?
   AND ((SELECT COUNT(*) FROM integration_push_devices WHERE profile_id=?)<${maximumDevices} OR EXISTS(SELECT 1 FROM integration_push_devices WHERE profile_id=? AND device_id=?))
   ON CONFLICT(profile_id,device_id) DO UPDATE SET connection_id=excluded.connection_id,revision=excluded.revision,subscription_id=excluded.subscription_id,state=excluded.state,consent_epoch=excluded.consent_epoch,endpoint_hash=excluded.endpoint_hash,ciphertext=excluded.ciphertext,iv=excluded.iv,key_version=excluded.key_version,expires_at_ms=excluded.expires_at_ms,vapid_key_hash=excluded.vapid_key_hash,commit_tag=excluded.commit_tag,updated_at_utc=excluded.updated_at_utc`)
   .bind(p.profile_id,deviceId,connectionId,nextRevision,id,state,consentEpoch,endpointHash,envelope?.ciphertext??null,envelope?.iv??null,envelope?.keyVersion??null,material?.expirationTime??null,publicKey?await pushHash(publicKey):null,tag,now,...values,p.profile_id,deviceId,expectedRevision,p.profile_id,p.profile_id,deviceId),
  receipt(env,p,requestId,hash,result,`${guard} AND EXISTS(SELECT 1 FROM integration_push_devices WHERE profile_id=? AND device_id=? AND commit_tag=?)`,[...values,p.profile_id,deviceId,tag]),
 ]);}catch(error){const recovered=await original(env,p.profile_id,requestId,hash);if(recovered)return reply(recovered);throw error;}
 if(!changed[0].meta.changes||!changed[1].meta.changes)throw new VaultError('push_device_changed',409);
 await activeProfile(env,p.profile_id,connectionId);return reply(result);
}
export async function pushRoute(request:Request,env:PushEnv):Promise<Response|null>{
 const match=new URL(request.url).pathname.match(/^\/profiles\/(prf_[A-Za-z0-9_-]{43})\/push(?:\/(consent|subscribe|unsubscribe|commands\/([0-9a-f-]{36})))?$/i);if(!match)return null;
 try{
  const p=await managedProfile(request,env,match[1]);const action=match[2];
  if(request.method==='GET'&&!action)return reply(await pushStatus(env,p));
  if(request.method==='GET'&&match[3]){await expirePush(env,p.profile_id);return reply({profileId:p.profile_id,requestId:uuid(match[3]),receipt:await original(env,p.profile_id,uuid(match[3]))});}
  if(request.method!=='POST')throw new VaultError('invalid_request',405);
  if(action==='consent')return await consentCommand(request,env,p);
  if(action==='subscribe'||action==='unsubscribe')return await deviceCommand(request,env,p,action==='unsubscribe');
  throw new VaultError('invalid_request',404);
 }catch(error){return trafficFailure(error)??reply({error:error instanceof VaultError||error instanceof WorkspaceError?error.code:'push_unavailable'},error instanceof VaultError||error instanceof WorkspaceError?error.status:503);}
}
export async function clearPushContext(env:AuthEnv,profileId:string,connectionId:string,deleteProfile:boolean):Promise<void>{
 if(deleteProfile){await env.HABITICA_INTEGRATION_DB.batch(['integration_push_devices','integration_push_consents','integration_push_commands','integration_push_dispatches'].map(table=>env.HABITICA_INTEGRATION_DB.prepare(`DELETE FROM ${table} WHERE profile_id=?`).bind(profileId)));return;}
 const now=new Date().toISOString();await env.HABITICA_INTEGRATION_DB.batch([
  env.HABITICA_INTEGRATION_DB.prepare('UPDATE integration_push_consents SET enabled=0,consent_epoch=?,updated_at_utc=? WHERE profile_id=? AND connection_id=?').bind(crypto.randomUUID(),now,profileId,connectionId),
  env.HABITICA_INTEGRATION_DB.prepare(`UPDATE integration_push_devices SET state='revoked',revision=MIN(revision+1,9007199254740991),endpoint_hash=NULL,ciphertext=NULL,iv=NULL,key_version=NULL,vapid_key_hash=NULL,updated_at_utc=? WHERE profile_id=? AND connection_id=? AND state='active'`).bind(now,profileId,connectionId),
 ]);
}
