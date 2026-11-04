import {eventName,type EventPrincipal,type EventSubscription} from '../../shared/events.ts';
import {workspaceJson,workspaceObject,uuid,WorkspaceError} from '../../shared/workspace-http.ts';
import {activeProfile,VaultError} from './vault.ts';
import {eventsConfiguration,eventsWorkspace,sendEventBytes,eventCallbackMaterial,type EventsAuthEnv} from './events.ts';
import {egressHash} from './callback-egress-protocol.ts';

interface Dispatch {intent_hash:string;body_hash:string;request_id:string;state:string;receipt_json:string|null}
const headers={'cache-control':'no-store'};
async function principal(env:EventsAuthEnv,value:unknown):Promise<EventPrincipal>{
 const p=workspaceObject(value,['profileId','connectionId','grantEpoch']);if(typeof p.profileId!=='string'||typeof p.grantEpoch!=='string')throw new VaultError('invalid_principal',422);
 const who={profileId:p.profileId,connectionId:uuid(p.connectionId),grantEpoch:p.grantEpoch},profile=await activeProfile(env,who.profileId,who.connectionId);if(profile.grant_epoch!==who.grantEpoch)throw new VaultError('unauthorized',401);return who;
}
function receipt(row:Dispatch,eventId:string,attempt:number){return row.receipt_json?JSON.parse(row.receipt_json):{eventId,attempt,requestId:row.request_id,state:'unknown',code:'original_dispatch_pending',acceptedByHost:null,chatResponse:null};}
export async function eventDispatchService(request:Request,env:EventsAuthEnv):Promise<Response>{
 try{
  if(request.method!=='POST')throw new VaultError('invalid_request',400);
  const v=workspaceObject(await workspaceJson(request,8192)),who=await principal(env,v.principal),eventId=uuid(v.eventId),attempt=Number(v.attempt),db=env.HABITICA_INTEGRATION_DB;
  if(!Number.isSafeInteger(attempt)||attempt<1||attempt>3)throw new VaultError('invalid_attempt',422);
  const old=await db.prepare('SELECT * FROM integration_event_dispatches WHERE profile_id=? AND event_id=? AND attempt=?').bind(who.profileId,eventId,attempt).first<Dispatch>();
  if(new URL(request.url).pathname==='/receipt')return Response.json(old?receipt(old,eventId,attempt):{eventId,attempt,state:'unknown',code:'original_receipt_unavailable',acceptedByHost:null,chatResponse:null},{headers});
  if(new URL(request.url).pathname!=='/dispatch')throw new VaultError('invalid_request',400);
  workspaceObject(v,['principal','eventId','attempt','subscriptionId','revision','consentEpoch','atUtc']);eventsConfiguration(env);
  if(typeof v.subscriptionId!=='string'||!/^[a-f0-9]{64}$/.test(v.subscriptionId)||typeof v.atUtc!=='string')throw new VaultError('invalid_event',422);
  const subscription=await eventsWorkspace<EventSubscription>(env,who,'/status',{subscriptionId:v.subscriptionId});
  const due=Date.parse(v.atUtc),clock=Date.now();
  if(!subscription||subscription.state!=='active'||subscription.expiresAt<=clock||subscription.revision!==v.revision||subscription.consentEpoch!==v.consentEpoch||subscription.cue!==v.atUtc||!Number.isFinite(due)||due>clock||due+3600000<=clock)throw new VaultError('event_authority_changed',409);
  const body=JSON.stringify({eventId,name:eventName,timestamp:new Date(due).toISOString(),data:{taskId:subscription.taskId},cursor:null}),bodyHash=egressHash(body);
  const intentHash=egressHash(JSON.stringify([who,subscription.subscriptionId,subscription.consentEpoch,subscription.cue,subscription.cueRevision,bodyHash]));
  if(old){if(old.intent_hash!==intentHash)throw new VaultError('event_identity_reused',409);return Response.json(receipt(old,eventId,attempt),{headers});}
  const previous=attempt>1?await db.prepare('SELECT * FROM integration_event_dispatches WHERE profile_id=? AND event_id=? AND attempt=?').bind(who.profileId,eventId,attempt-1).first<Dispatch>():null;
  if(attempt>1&&(!previous||previous.intent_hash!==intentHash||previous.state!=='retryable'||!previous.receipt_json))throw new VaultError('original_retry_not_authorized',409);
  const material=await eventCallbackMaterial(env,who,subscription),requestId=crypto.randomUUID();
  const inserted=await db.prepare(`INSERT OR IGNORE INTO integration_event_dispatches SELECT ?,?,?,?,?,?,?,?,?,?,NULL,? WHERE (SELECT COUNT(*) FROM integration_event_dispatches WHERE profile_id=?)<4096 AND (SELECT COUNT(*) FROM integration_event_dispatches)<20000 AND NOT EXISTS(SELECT 1 FROM integration_event_dispatches WHERE profile_id=? AND event_id=? AND attempt>=?)`).bind(who.profileId,eventId,attempt,subscription.subscriptionId,who.connectionId,who.grantEpoch,intentHash,bodyHash,requestId,'dispatching',clock,who.profileId,who.profileId,eventId,attempt).run();
  if(!inserted.meta.changes){const original=await db.prepare('SELECT * FROM integration_event_dispatches WHERE profile_id=? AND event_id=? AND attempt=?').bind(who.profileId,eventId,attempt).first<Dispatch>();if(!original)throw new VaultError('event_capacity_or_race',409);if(original.intent_hash!==intentHash)throw new VaultError('event_identity_reused',409);return Response.json(receipt(original,eventId,attempt),{headers});}
  let state='unknown',code='transport_uncertain',httpStatus:number|null=null;
  try{
   // Revalidate immediately before minting the short service grant. No queued token or callback authority is trusted.
   await principal(env,who);const current=await eventsWorkspace<EventSubscription>(env,who,'/status',{subscriptionId:subscription.subscriptionId});
   if(!current||current.state!=='active'||current.revision!==subscription.revision||current.consentEpoch!==subscription.consentEpoch||current.expiresAt<=Date.now())throw new VaultError('event_authority_changed',409);
   const response=await sendEventBytes(env,material,subscription.subscriptionId,body,eventId,requestId,'event');
   if(response.state==='response'&&Number.isInteger(response.status)){
    httpStatus=Number(response.status);state=httpStatus>=200&&httpStatus<300?'accepted':httpStatus===429||httpStatus>=500?'retryable':'refused';code=state==='accepted'?'host_acknowledged':state==='retryable'?'known_transient_response':'permanent_response';
   }else{state='refused';code='protected_transport_refused';}
  }catch(e){if(e instanceof VaultError&&e.code==='event_authority_changed'){state='cancelled';code=e.code;}}
  if(state==='retryable'&&attempt===3){state='refused';code='retry_exhausted';}
  const result={eventId,attempt,requestId,state,code,httpStatus,bodyHash,acceptedByHost:state==='accepted'?true:state==='unknown'?null:false,chatResponse:null,retryAfterMs:state==='retryable'?1000*2**(attempt-1):null};
  await db.prepare('UPDATE integration_event_dispatches SET state=?,receipt_json=? WHERE profile_id=? AND event_id=? AND attempt=? AND request_id=?').bind(state,JSON.stringify(result),who.profileId,eventId,attempt,requestId).run();
  if(httpStatus===410||httpStatus===413)await eventsWorkspace(env,who,'/terminate',{subscriptionId:subscription.subscriptionId,revision:subscription.revision});
  return Response.json(result,{headers});
 }catch(e){const known=e instanceof VaultError||e instanceof WorkspaceError;return Response.json({error:known?e.code:'events_dispatch_unavailable'},{status:known?e.status:503,headers});}
}
