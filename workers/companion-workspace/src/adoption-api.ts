import {adoptionInput,type AdoptionTool} from '../../shared/adoption-tools.ts';
import {workspaceDocument} from '../../shared/workspace-domain.ts';
import {workspaceTokenHash,WorkspaceError} from '../../shared/workspace-http.ts';
import type {RemoteTask} from '../../shared/projection.ts';
import {adoptProjection} from './projection-adapter.ts';
import {currentTagRow,tagReviewLease} from './group-tag-api.ts';
import type {WorkspaceRow} from './executor.ts';
interface Review {command_id:string;request_id:string;input_hash:string;workspace_id:string;connection_id:string;access_epoch:string;grant_epoch:string;origin:string;expected_revision:number;review_hash:string;review_json:string;state:'prepared'|'accepted'|'refused';receipt_json:string|null;error_code:string|null;expires_at_utc:string}
export interface AdoptionOrigin {source:'browser'|'mcp';grantEpoch?:string}
const now=()=>new Date().toISOString(),bytes=(value:string)=>new TextEncoder().encode(value).length;
const disclosure='Link only these exact records. Companion and Habitica fields, schedules, completion history and rewards remain unchanged. Zero native writes and zero game effects. Future accepted commands may use this correspondence.';
export async function adoptionApi(env:WorkspaceEnv,initial:WorkspaceRow,name:AdoptionTool,args:unknown,origin:AdoptionOrigin={source:'browser'}):Promise<Record<string,unknown>>{
 const input=adoptionInput(name,args);let row=await currentTagRow(env,initial);
 const find=(field:'request_id'|'command_id',id:unknown)=>env.WORKSPACE_DB.prepare(`SELECT * FROM workspace_task_adoptions WHERE profile_id=? AND ${field}=?`).bind(row.profile_id,id).first<Review>();
 const scope=(review:Review)=>review.workspace_id===row.workspace_id&&review.connection_id===row.connection_id&&review.access_epoch===row.access_epoch;
 const authority=(review:Review)=>scope(review)&&review.origin===origin.source&&review.grant_epoch===(origin.grantEpoch??'browser')&&(origin.source!=='mcp'||row.mcp_grant_epoch===origin.grantEpoch);
 const result=(review:Review):Record<string,unknown>=>({commandId:review.command_id,preparedCommandId:review.command_id,requestId:review.request_id,workspaceId:review.workspace_id,reviewHash:review.review_hash,state:review.state,savedInCompanion:review.state==='accepted',outcomeUnknown:false,gameEffects:0,nativeWrites:0,...(review.receipt_json?{receipt:JSON.parse(review.receipt_json)}:{error:review.error_code})});
 const terminal=async(review:Review,code:string)=>{
  await env.WORKSPACE_DB.prepare("UPDATE workspace_task_adoptions SET state='refused',error_code=? WHERE profile_id=? AND command_id=? AND state='prepared'").bind(code,row.profile_id,review.command_id).run();
  return result((await find('command_id',review.command_id))!);
 };
 if(name==='prepare_task_adoption'){
  const hash=await workspaceTokenHash(JSON.stringify(input));let stored=await find('request_id',input.requestId);
  if(stored&&stored.input_hash!==hash)throw new WorkspaceError('request_id_reused',409);
  if(stored){
   if(!scope(stored))throw new WorkspaceError('adoption_identity_changed',409);
   if(stored.state!=='prepared')return result(stored);
   if(!authority(stored)||stored.expires_at_utc<=now())return tagReviewLease(env,row,async()=>{row=await currentTagRow(env,row);return terminal(stored!,'preview_expired_or_revoked');});
   return {...JSON.parse(stored.review_json),...result(stored),expiresAtUtc:stored.expires_at_utc};
  }
  return tagReviewLease(env,row,async()=>{
   row=await currentTagRow(env,row);const document=workspaceDocument(JSON.parse(row.document_json));
   if(input.expectedRevision!==undefined&&input.expectedRevision!==document.revision)throw new WorkspaceError('stale_revision',409);
   if(row.retirement_fence||row.linked_recovery_fence)throw new WorkspaceError('workspace_recovery_pending',409);
   await noEffects(env,row,String(input.taskId));
   const task=document.tasks.find(task=>task.id===input.taskId);if(!task)throw new WorkspaceError('task_unavailable',404);
   const remote=await inspect(env,row,String(input.habiticaTaskId),origin);const after=adoptProjection(document,task.id,remote,remote.fingerprint);
   row=await currentTagRow(env,row);if(workspaceDocument(JSON.parse(row.document_json)).revision!==document.revision)throw new WorkspaceError('stale_revision',409);
   const commandId=crypto.randomUUID(),expiresAtUtc=new Date(Date.now()+600000).toISOString();
   const review={workspaceId:row.workspace_id,accessEpoch:row.access_epoch,connectionId:row.connection_id,task,external:remote,expectedRevision:document.revision,pendingCommandCount:0,mappingBefore:document.projections.find(mapping=>mapping.taskId===task.id)??null,mappingAfter:after.projections.find(mapping=>mapping.taskId===task.id),gameEffects:0,nativeWrites:0,disclosure};
   const encoded=JSON.stringify(review),reviewHash=await workspaceTokenHash(encoded),cost=bytes(encoded)+1024;
   const inserted=await env.WORKSPACE_DB.prepare(`INSERT INTO workspace_task_adoptions(profile_id,command_id,request_id,input_hash,workspace_id,connection_id,access_epoch,grant_epoch,origin,expected_revision,review_hash,review_json,state,created_at_utc,expires_at_utc)
    SELECT profile_id,?,?,?,?,?,?,?,?,?,?,?,'prepared',?,? FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1 AND store_version=? AND store_version<9007199254740991
    AND (?='browser' OR mcp_grant_epoch=?) AND archive_bytes+?<=8388608
    AND (SELECT count(*) FROM workspace_task_adoptions WHERE profile_id=?)<8192
    AND (SELECT count(*) FROM workspace_task_adoptions WHERE profile_id=? AND state='prepared' AND expires_at_utc>?)<8
    ON CONFLICT(profile_id,request_id) DO NOTHING`).bind(commandId,input.requestId,hash,row.workspace_id,row.connection_id,row.access_epoch,origin.grantEpoch??'browser',origin.source,document.revision,reviewHash,encoded,now(),expiresAtUtc,row.profile_id,row.connection_id,row.access_epoch,row.store_version,origin.source,origin.grantEpoch??'browser',cost,row.profile_id,row.profile_id,now()).run();
   if(!inserted.meta.changes){stored=await find('request_id',input.requestId);if(!stored||stored.input_hash!==hash)throw new WorkspaceError('adoption_capacity_or_identity_changed',409);return {...JSON.parse(stored.review_json),...result(stored),expiresAtUtc:stored.expires_at_utc};}
   stored=(await find('command_id',commandId))!;return {...review,...result(stored),expiresAtUtc};
  });
 }
 const stored=await find('command_id',input.commandId);
 if(!stored||!scope(stored))return {commandId:input.commandId,savedInCompanion:null,outcomeUnknown:true,state:'unavailable',gameEffects:0,nativeWrites:0};
 // Immutable receipts are recoverable after later revisions and grant renewal, within the original active connection/access epoch.
 if(stored.state!=='prepared')return result(stored);
 if(name==='accept_task_adoption'&&input.reviewHash!==stored.review_hash)throw new WorkspaceError('exact_adoption_review_required',409);
 return tagReviewLease(env,row,async control=>{
  row=await currentTagRow(env,row);const fresh=(await find('command_id',stored.command_id))!;if(fresh.state!=='prepared')return result(fresh);
  if(fresh.expires_at_utc<=now())return terminal(fresh,'preview_expired_or_revoked');
  if(name==='get_task_adoption_command')return result(fresh);
  if(!authority(fresh))return terminal(fresh,'preview_expired_or_revoked');
  try{
   if(await workspaceTokenHash(fresh.review_json)!==fresh.review_hash)throw new WorkspaceError('adoption_review_corrupt',409);
   const document=workspaceDocument(JSON.parse(row.document_json)),review=JSON.parse(fresh.review_json);
   if(document.revision!==fresh.expected_revision)throw new WorkspaceError('stale_revision',409);
   if(row.retirement_fence||row.linked_recovery_fence)throw new WorkspaceError('workspace_recovery_pending',409);
   await noEffects(env,row,review.task.id);
   const remote=await inspect(env,row,review.external.id,origin);
   if(remote.fingerprint!==review.external.fingerprint)throw new WorkspaceError('external_edit_review_required',409);
   const adopted=adoptProjection(document,review.task.id,remote,review.external.fingerprint);
   // Use the server-issued mapping identity shown in the review, never a new mapping ID on acceptance.
   const mapping=adopted.projections.find(mapping=>mapping.taskId===review.task.id)!;mapping.id=review.mappingAfter.id;
   const serialized=JSON.stringify(workspaceDocument(adopted)),receipt={commandId:fresh.command_id,requestId:fresh.request_id,workspaceId:row.workspace_id,accessEpoch:row.access_epoch,connectionId:row.connection_id,taskId:review.task.id,habiticaTaskId:remote.id,mappingId:mapping.id,reviewHash:fresh.review_hash,revision:adopted.revision,adopted:true,acceptedAtUtc:now(),gameEffects:0,nativeWrites:0};
   const encoded=JSON.stringify(receipt),size=row.archive_bytes-bytes(row.document_json)+bytes(serialized)+bytes(encoded),tag=crypto.randomUUID();
   if(bytes(serialized)>1048576||size>8388608||row.store_version>=Number.MAX_SAFE_INTEGER)throw new WorkspaceError('workspace_capacity',413);
   const saved=await env.WORKSPACE_DB.batch([
    env.WORKSPACE_DB.prepare(`UPDATE workspaces SET document_json=?,archive_bytes=?,store_version=store_version+1,commit_tag=?,updated_at_utc=? WHERE profile_id=? AND workspace_id=? AND connection_id=? AND access_epoch=? AND store_version=? AND active=1 AND (?='browser' OR mcp_grant_epoch=?) AND retirement_fence IS NULL AND linked_recovery_fence IS NULL AND EXISTS(SELECT 1 FROM workspace_task_adoptions a WHERE a.profile_id=workspaces.profile_id AND a.command_id=? AND a.state='prepared' AND a.expires_at_utc>?) AND EXISTS(SELECT 1 FROM workspace_runtime WHERE profile_id=? AND access_epoch=? AND lease_token=? AND lease_until_utc>?)`).bind(serialized,size,tag,now(),row.profile_id,row.workspace_id,row.connection_id,row.access_epoch,row.store_version,origin.source,origin.grantEpoch??'browser',fresh.command_id,now(),row.profile_id,row.access_epoch,control.token,now()),
    env.WORKSPACE_DB.prepare("UPDATE workspace_task_adoptions SET state='accepted',receipt_json=? WHERE profile_id=? AND command_id=? AND state='prepared' AND EXISTS(SELECT 1 FROM workspaces WHERE profile_id=? AND commit_tag=?)").bind(encoded,row.profile_id,fresh.command_id,row.profile_id,tag)
   ]);
   if(!saved[0].meta.changes)throw new WorkspaceError('stale_revision',409);
   return result((await find('command_id',fresh.command_id))!);
  }catch(error){if(error instanceof WorkspaceError&&[404,409,422].includes(error.status))return terminal(fresh,error.code);throw error;}
 });
}
async function noEffects(env:WorkspaceEnv,row:WorkspaceRow,taskId:string){
 const pending=await env.WORKSPACE_DB.prepare('SELECT entry_json FROM workspace_commands WHERE profile_id=?').bind(row.profile_id).all<{entry_json:string}>();
 if(pending.results.some(value=>{const entry=JSON.parse(value.entry_json);return ['executing','unknownOutcome','leased'].includes(entry.projectionState)||entry.command.taskId===taskId&&!['notRequested','confirmed','cancelled'].includes(entry.projectionState);}))throw new WorkspaceError('unresolved_projection_effects',409);
 if(await env.WORKSPACE_DB.prepare("SELECT 1 FROM workspace_tag_work WHERE profile_id=? AND state IN ('executing','unknownOutcome') LIMIT 1").bind(row.profile_id).first())throw new WorkspaceError('unresolved_projection_effects',409);
}
async function inspect(env:WorkspaceEnv,row:WorkspaceRow,id:string,origin:AdoptionOrigin):Promise<RemoteTask>{
 const response=await env.AUTH_PROJECTION.fetch('https://projection.internal/operation',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({profileId:row.profile_id,connectionId:row.connection_id,operation:'inspectAdoption',arguments:{selector:id},automated:false,...(origin.source==='mcp'?{requiredGrantEpoch:origin.grantEpoch}:{})}),signal:AbortSignal.timeout(15000)});
 const observed=await response.json<{task:RemoteTask|null;code?:string}>();if(!response.ok)throw new WorkspaceError(observed.code??'projection_preview_unavailable',response.status);
 if(!observed.task)throw new WorkspaceError('external_deleted',409);if(observed.task.id!==id)throw new WorkspaceError('projection_identity_conflict',409);return observed.task;
}
