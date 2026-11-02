import {eventDeliveryReceipt} from './event-scheduler.ts';
import {eventName,type EventPrincipal,type EventContext,type EventSubscription} from '../../shared/events.ts';
import {workspaceObject,workspaceJson,workspaceTokenHash,workspaceSessionToken,uuid,WorkspaceError} from '../../shared/workspace-http.ts';
import {workspaceDocument} from '../../shared/workspace-domain.ts';
import {reminderAuthority,reminderCueRevision} from './reminder-api.ts';
import type {WorkspaceRow} from './executor.ts';
import {mcpEpoch} from './mcp-epoch.ts';

export interface EventsWorkspaceEnv extends WorkspaceEnv {}
interface Consent {consent_epoch:string;enabled:number;cue:string|null;cue_revision:number}
interface SubscriptionRow {profile_id:string;subscription_id:string;callback_id:string;task_id:string;connection_id:string;access_epoch:string;grant_epoch:string;consent_epoch:string;revision:number;state:string;expires_at:number;operation_id:string}
export async function eventWorkspace(env:EventsWorkspaceEnv,who:EventPrincipal,taskId?:string,consented=true){
 if(!Boolean(env.EVENTS_ENABLED)||!env.WORKSPACE_ENABLED)throw new WorkspaceError('events_unavailable',503);
 const source=await env.WORKSPACE_DB.prepare('SELECT * FROM workspaces WHERE profile_id=? AND connection_id=? AND active=1').bind(who.profileId,who.connectionId).first<WorkspaceRow>();
 const row=source?await mcpEpoch(env,source,who.grantEpoch):null;
 if(!row||!reminderAuthority(row)||row.linked_recovery_fence)throw new WorkspaceError('resource_access_denied',403);
 const document=workspaceDocument(JSON.parse(row.document_json));
 if(!taskId)return {row,document,context:null};
 const task=document.tasks.find(t=>t.id===taskId);if(!task||task.isArchived||['completed','cancelled'].includes(task.state))throw new WorkspaceError('resource_access_denied',403);
 const consent=await env.WORKSPACE_DB.prepare('SELECT * FROM workspace_event_consents WHERE profile_id=? AND task_id=? AND connection_id=? AND access_epoch=? AND grant_epoch=?').bind(who.profileId,taskId,who.connectionId,row.access_epoch,who.grantEpoch).first<Consent>();
 const cue=task.reviewAfterUtc??null,cueRevision=reminderCueRevision(document,taskId);
 if(consented&&(!consent?.enabled||consent.cue!==cue||consent.cue_revision!==cueRevision))throw new WorkspaceError('separate_events_consent_required',403);
 const context:EventContext={...who,accessEpoch:row.access_epoch,taskId,consentEpoch:consent?.consent_epoch??'',workspaceRevision:document.revision,cue,cueRevision};
 return {row,document,context};
}
export async function eventSubscription(env:EventsWorkspaceEnv,who:EventPrincipal,id:string):Promise<EventSubscription|null>{
 const s=await env.WORKSPACE_DB.prepare('SELECT * FROM workspace_event_subscriptions WHERE profile_id=? AND subscription_id=?').bind(who.profileId,id).first<SubscriptionRow>();if(!s)return null;
 const {context}=await eventWorkspace(env,who,s.task_id);if(!context||s.connection_id!==who.connectionId||s.access_epoch!==context.accessEpoch||s.grant_epoch!==who.grantEpoch||s.consent_epoch!==context.consentEpoch)throw new WorkspaceError('subscription_context_changed',409);
 return {...context,subscriptionId:id,callbackId:s.callback_id,revision:s.revision,state:s.state,expiresAt:s.expires_at,operationId:s.operation_id};
}
export async function eventConsent(env:EventsWorkspaceEnv,who:EventPrincipal,tool:string,args:unknown){
 const v=workspaceObject(args),db=env.WORKSPACE_DB;
 if(tool==='get_event_consent'){
  const taskId=uuid(v.taskId),{context}=await eventWorkspace(env,who,taskId,false);
  const command=v.commandId?await db.prepare('SELECT command_id AS commandId,receipt_json AS receipt FROM workspace_event_commands WHERE profile_id=? AND task_id=? AND command_id=? AND connection_id=? AND access_epoch=? AND grant_epoch=?').bind(who.profileId,taskId,uuid(v.commandId),who.connectionId,context!.accessEpoch,who.grantEpoch).first<{commandId:string;receipt:string|null}>():null;
  let enabled=false;try{await eventWorkspace(env,who,taskId);enabled=true;}catch(e){if(!(e instanceof WorkspaceError)||e.code!=='separate_events_consent_required')throw e;}
  return {taskId,enabled,commandId:command?.commandId??null,receipt:command?.receipt?JSON.parse(command.receipt):null,outcomeUnknown:Boolean(v.commandId&&!command)};
 }
 if(tool==='prepare_event_consent'){
  workspaceObject(v,['requestId','taskId','enabled','expectedWorkspaceRevision']);const taskId=uuid(v.taskId),requestId=uuid(v.requestId);
  if(typeof v.enabled!=='boolean'||!Number.isSafeInteger(v.expectedWorkspaceRevision))throw new WorkspaceError('invalid_consent',422);
  const {row,context}=await eventWorkspace(env,who,taskId,false);if(context!.workspaceRevision!==v.expectedWorkspaceRevision)throw new WorkspaceError('stale_revision',409);
  const input={taskId,enabled:v.enabled,expectedWorkspaceRevision:v.expectedWorkspaceRevision,cue:context!.cue,cueRevision:context!.cueRevision},hash=await workspaceTokenHash(JSON.stringify(input));
  const token=workspaceSessionToken(),id=crypto.randomUUID(),expires=Date.now()+600000;
  await db.prepare(`INSERT OR IGNORE INTO workspace_event_commands SELECT ?,?,?,?,?,?,?,?,?,?,?,NULL WHERE (SELECT COUNT(*) FROM workspace_event_commands WHERE profile_id=?)<1024`).bind(who.profileId,requestId,id,taskId,who.connectionId,row.access_epoch,who.grantEpoch,hash,JSON.stringify(input),await workspaceTokenHash(token),expires,who.profileId).run();
  const found=await db.prepare('SELECT * FROM workspace_event_commands WHERE profile_id=? AND request_id=?').bind(who.profileId,requestId).first<Record<string,unknown>>();
  if(!found||found.input_hash!==hash||found.connection_id!==who.connectionId||found.access_epoch!==row.access_epoch||found.grant_epoch!==who.grantEpoch)throw new WorkspaceError('request_id_reused_or_capacity',409);
  if(found.receipt_json)return {commandId:found.command_id,receipt:JSON.parse(String(found.receipt_json)),accepted:true};
  if(Number(found.expires_at)<=Date.now())throw new WorkspaceError('preview_expired',409);
  await db.prepare('UPDATE workspace_event_commands SET token_hash=? WHERE profile_id=? AND command_id=? AND receipt_json IS NULL').bind(await workspaceTokenHash(token),who.profileId,found.command_id).run();
  return {commandId:found.command_id,requestId,inputHash:hash,acceptToken:token,expiresAt:found.expires_at,accepted:false,...input,warning:'Separate Events consent for this exact task and review cue. No callback or chat is subscribed by this command. Review before accepting.'};
 }
 workspaceObject(v,['commandId','acceptToken','expectedHash']);
 const command=await db.prepare('SELECT * FROM workspace_event_commands WHERE profile_id=? AND command_id=?').bind(who.profileId,uuid(v.commandId)).first<Record<string,unknown>>();if(!command)throw new WorkspaceError('preview_required',409);
 const {row,context}=await eventWorkspace(env,who,String(command.task_id),false);
 if(command.connection_id!==who.connectionId||command.access_epoch!==row.access_epoch||command.grant_epoch!==who.grantEpoch)throw new WorkspaceError('preview_context_changed',409);
 if(command.receipt_json)return {commandId:command.command_id,accepted:true,receipt:JSON.parse(String(command.receipt_json))};
 const input=JSON.parse(String(command.input_json));
 if(Number(command.expires_at)<=Date.now()||command.token_hash!==await workspaceTokenHash(String(v.acceptToken))||v.expectedHash!==command.input_hash||input.expectedWorkspaceRevision!==context!.workspaceRevision||input.cue!==context!.cue||input.cueRevision!==context!.cueRevision)throw new WorkspaceError('preview_expired_or_changed',409);
 const epoch=crypto.randomUUID(),receipt={commandId:command.command_id,taskId:command.task_id,enabled:input.enabled,consentEpoch:epoch,retiredConsentEpoch:context!.consentEpoch,subscribed:false,shown:null};
 const guard='EXISTS(SELECT 1 FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND mcp_grant_epoch=? AND document_json=? AND active=1) AND EXISTS(SELECT 1 FROM workspace_event_commands WHERE profile_id=? AND command_id=? AND receipt_json IS NULL AND token_hash=?)';
 const binds=[who.profileId,who.connectionId,row.access_epoch,who.grantEpoch,row.document_json,who.profileId,command.command_id,command.token_hash];
 await db.batch([
  db.prepare(`INSERT INTO workspace_event_consents SELECT ?,?,?,?,?,?,?,?,? WHERE ${guard} ON CONFLICT(profile_id,task_id) DO UPDATE SET connection_id=excluded.connection_id,access_epoch=excluded.access_epoch,grant_epoch=excluded.grant_epoch,consent_epoch=excluded.consent_epoch,enabled=excluded.enabled,cue=excluded.cue,cue_revision=excluded.cue_revision`).bind(who.profileId,command.task_id,who.connectionId,row.access_epoch,who.grantEpoch,epoch,input.enabled?1:0,context!.cue,context!.cueRevision,...binds),
  db.prepare(`UPDATE workspace_event_subscriptions SET state='revoked',revision=revision+1 WHERE profile_id=? AND task_id=? AND ${guard}`).bind(who.profileId,command.task_id,...binds),
  db.prepare(`UPDATE workspace_event_commands SET receipt_json=?,token_hash='' WHERE profile_id=? AND command_id=? AND ${guard}`).bind(JSON.stringify(receipt),who.profileId,command.command_id,...binds)
 ]);
 const result=await db.prepare('SELECT receipt_json FROM workspace_event_commands WHERE profile_id=? AND command_id=?').bind(who.profileId,command.command_id).first<{receipt_json:string|null}>();if(!result?.receipt_json)throw new WorkspaceError('consent_context_changed',409);
 return {commandId:command.command_id,accepted:true,receipt:JSON.parse(result.receipt_json)};
}
export async function eventLifecycle(env:EventsWorkspaceEnv,who:EventPrincipal,path:string,value:unknown){
 const v=workspaceObject(value),db=env.WORKSPACE_DB;
 if(path==='/delivery-receipt')return eventDeliveryReceipt(env,who,value);
 if(path==='/consent')return eventConsent(env,who,String(v.tool),v.arguments);
 if(path==='/context')return (await eventWorkspace(env,who,uuid(v.taskId))).context;
 const id=String(v.subscriptionId);if(!/^[a-f0-9]{64}$/.test(id))throw new WorkspaceError('invalid_subscription',422);
 if(path==='/unsubscribe'){
  await eventWorkspace(env,who,undefined,false);
  await db.prepare("UPDATE workspace_event_subscriptions SET state='unsubscribed',revision=revision+1 WHERE profile_id=? AND subscription_id=?").bind(who.profileId,id).run();
  return {subscriptionId:id,unsubscribed:true};
 }
 if(path==='/reserve'){
  const {row,context}=await eventWorkspace(env,who,uuid(v.taskId));
  const expires=Number(v.expiresAt);if(!Number.isSafeInteger(expires)||expires<=Date.now()||expires>Date.now()+86400000)throw new WorkspaceError('invalid_expiry',422);
  const operation=uuid(v.operationId),callback=uuid(v.callbackId);
  await db.prepare(`INSERT OR IGNORE INTO workspace_event_subscriptions SELECT ?,?,?,?,?,?,?,?,?,?,?,? WHERE (SELECT COUNT(*) FROM workspace_event_subscriptions WHERE profile_id=?)<64 AND EXISTS(SELECT 1 FROM workspaces WHERE profile_id=? AND access_epoch=? AND document_json=? AND active=1)`).bind(who.profileId,id,callback,context!.taskId,who.connectionId,row.access_epoch,who.grantEpoch,context!.consentEpoch,1,'pending',expires,operation,who.profileId,who.profileId,row.access_epoch,row.document_json).run();
  // A new reviewed consent can create a new authority generation under the same deterministic identity.
  // Expiry, unsubscribe or an old connection alone never supplies that authority.
  const prior=await db.prepare('SELECT * FROM workspace_event_subscriptions WHERE profile_id=? AND subscription_id=?').bind(who.profileId,id).first<SubscriptionRow>();
  if(prior?.state==='revoked'&&prior.consent_epoch!==context!.consentEpoch){
   await db.prepare("UPDATE workspace_event_subscriptions SET callback_id=?,connection_id=?,access_epoch=?,grant_epoch=?,consent_epoch=?,revision=revision+1,state='pending',expires_at=?,operation_id=? WHERE profile_id=? AND subscription_id=? AND state='revoked' AND revision=?").bind(callback,who.connectionId,row.access_epoch,who.grantEpoch,context!.consentEpoch,expires,operation,who.profileId,id,prior.revision).run();
  }
  const old=await eventSubscription(env,who,id);if(!old)throw new WorkspaceError('subscription_capacity',409);
  if(old.state==='pending'||old.state==='unknown')return old;
  if(old.state!=='active'||old.expiresAt<=Date.now())throw new WorkspaceError('subscription_authority_expired',409);
  const updated=await db.prepare("UPDATE workspace_event_subscriptions SET state='pending',revision=revision+1,operation_id=?,expires_at=? WHERE profile_id=? AND subscription_id=? AND revision=? AND state='active'").bind(operation,expires,who.profileId,id,old.revision).run();
  if(!updated.meta.changes)throw new WorkspaceError('subscription_busy',409);return eventSubscription(env,who,id);
 }
 if(path==='/terminate'){
  await eventSubscription(env,who,id);await db.prepare("UPDATE workspace_event_subscriptions SET state='ended',revision=revision+1 WHERE profile_id=? AND subscription_id=? AND revision=? AND state='active'").bind(who.profileId,id,Number(v.revision)).run();
 }
 if(path==='/activate'||path==='/unknown'){
  const old=await eventSubscription(env,who,id);if(!old)throw new WorkspaceError('subscription_unavailable',404);
  const {row}=await eventWorkspace(env,who,old.taskId);
  await db.prepare(`UPDATE workspace_event_subscriptions SET state=? WHERE profile_id=? AND subscription_id=? AND revision=? AND operation_id=? AND state='pending' AND expires_at>? AND EXISTS(SELECT 1 FROM workspaces w WHERE w.profile_id=workspace_event_subscriptions.profile_id AND w.connection_id=? AND w.access_epoch=? AND w.mcp_grant_epoch=? AND w.active=1 AND w.document_json=?) AND EXISTS(SELECT 1 FROM workspace_event_consents c WHERE c.profile_id=workspace_event_subscriptions.profile_id AND c.task_id=workspace_event_subscriptions.task_id AND c.enabled=1 AND c.consent_epoch=workspace_event_subscriptions.consent_epoch)`).bind(path==='/activate'?'active':'unknown',who.profileId,id,Number(v.revision),uuid(v.operationId),Date.now(),who.connectionId,old.accessEpoch,who.grantEpoch,row.document_json).run();
 }
 return eventSubscription(env,who,id);
}
export async function eventService(request:Request,env:EventsWorkspaceEnv):Promise<Response>{
 try{
  if(request.method!=='POST')throw new WorkspaceError('invalid_request',400);
  const body=workspaceObject(await workspaceJson(request,16384),['principal','input']),p=workspaceObject(body.principal,['profileId','connectionId','grantEpoch']);
  if(typeof p.profileId!=='string'||typeof p.connectionId!=='string'||typeof p.grantEpoch!=='string')throw new WorkspaceError('invalid_principal',422);
  const result=await eventLifecycle(env,p as unknown as EventPrincipal,new URL(request.url).pathname,body.input);return Response.json(result,{headers:{'cache-control':'no-store'}});
 }catch(e){return Response.json({error:e instanceof WorkspaceError?e.code:'events_unavailable'},{status:e instanceof WorkspaceError?e.status:503});}
}
