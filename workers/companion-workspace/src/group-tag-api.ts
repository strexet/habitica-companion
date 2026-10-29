import {workspaceDocument} from '../../shared/workspace-domain.ts';
import {groupTagStatus,type TagStatusAuthority} from '../../shared/group-tag-status.ts';
import {groupTagInput,type GroupTagTool} from '../../shared/group-tag-tools.ts';
import {workspaceTokenHash,WorkspaceError} from '../../shared/workspace-http.ts';
import {tagAuthority,tagAuthorityGap,type TagOrigin,type TagAuthority} from './tag-authority.ts';
import {executeTagWork,type TagExecutorControl} from './tag-executor.ts';
import type {WorkspaceRow} from './executor.ts';
export interface TagStoredWork {work_id:string;work_json:string;prepared_request_json:string|null;state:string;error_code:string|null;command_id:string;source_revision:number;access_epoch:string;connection_id:string}
const now=()=>new Date().toISOString();
export async function currentTagRow(env:WorkspaceEnv,row:WorkspaceRow):Promise<WorkspaceRow>{
 const found=await env.WORKSPACE_DB.prepare('SELECT * FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1').bind(row.profile_id,row.connection_id,row.access_epoch).first<WorkspaceRow>();
 if(!found)throw new WorkspaceError('unauthorized',401);return found;
}
export function publicTagWork(stored:TagStoredWork){const work=JSON.parse(stored.work_json);return {workId:stored.work_id,commandId:stored.command_id,sourceRevision:stored.source_revision,state:stored.state,error:stored.error_code,entityId:work.context.entityId,kind:work.context.kind,operation:work.operation,originalAttemptId:stored.prepared_request_json?JSON.parse(stored.prepared_request_json).attemptId:null};}
export async function requiredTagAuthority(env:WorkspaceEnv,row:WorkspaceRow,origin:TagOrigin):Promise<TagAuthority>{
 const authority=await tagAuthority(env,row,origin),gap=tagAuthorityGap(authority);if(gap)throw new WorkspaceError(gap,403);return authority;
}
export async function groupTagGateway(env:WorkspaceEnv,row:WorkspaceRow,authority:TagAuthority,context:unknown,operation:string,args:unknown,extra:Record<string,unknown>={}){
 const response=await env.AUTH_PROJECTION.fetch('https://projection.internal/tags',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({profileId:row.profile_id,connectionId:row.connection_id,context,operation,arguments:args,automated:false,expectedConsentEpoch:authority.consentEpoch,...(authority.source==='mcp'?{requiredGrantEpoch:authority.grantEpoch}:{}),...extra}),signal:AbortSignal.timeout(15000)});
 const value=await response.json<Record<string,unknown>>();if(!response.ok)throw new WorkspaceError(typeof value.code==='string'?value.code:'tag_gateway_unavailable',response.status);return value;
}
export async function tagReviewLease<T>(env:WorkspaceEnv,row:WorkspaceRow,action:(control:TagExecutorControl)=>Promise<T>):Promise<T>{
 const token=crypto.randomUUID(),deadline=Date.now()+60000,until=new Date(Date.now()+90000).toISOString();
 const lock=await env.WORKSPACE_DB.prepare(`INSERT INTO workspace_runtime(profile_id,access_epoch,lease_token,lease_until_utc,last_wake_at_utc) SELECT profile_id,access_epoch,?,?,? FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1 ON CONFLICT(profile_id) DO UPDATE SET access_epoch=excluded.access_epoch,lease_token=excluded.lease_token,lease_until_utc=excluded.lease_until_utc WHERE workspace_runtime.lease_until_utc IS NULL OR workspace_runtime.lease_until_utc<=?`).bind(token,until,now(),row.profile_id,row.connection_id,row.access_epoch,now()).run();if(!lock.meta.changes)throw new WorkspaceError('projection_busy',409);
 const current=()=>currentTagRow(env,row);
 const runtime=()=>env.WORKSPACE_DB.prepare('SELECT next_network_at_utc FROM workspace_runtime WHERE profile_id=? AND access_epoch=? AND lease_token=? AND lease_until_utc>?').bind(row.profile_id,row.access_epoch,token,now()).first<{next_network_at_utc:string|null}>();
 try{return await action({runtime,row:current,permitted:async()=>{await current();return Date.now()<deadline&&Boolean(await runtime());},wait:async()=>{throw new WorkspaceError('manual_pacing_unavailable',409);},deadline,token,epoch:row.access_epoch});}
 finally{await env.WORKSPACE_DB.prepare('UPDATE workspace_runtime SET lease_token=NULL,lease_until_utc=NULL WHERE profile_id=? AND access_epoch=? AND lease_token=?').bind(row.profile_id,row.access_epoch,token).run();}
}
export async function recoverGroupTagReceipt(env:WorkspaceEnv,row:WorkspaceRow,workId:string){
 let stored=await env.WORKSPACE_DB.prepare('SELECT * FROM workspace_tag_work WHERE profile_id=? AND work_id=?').bind(row.profile_id,workId).first<TagStoredWork>();
 if(!stored)throw new WorkspaceError('tag_work_unavailable',404);
 if(['executing','unknownOutcome'].includes(stored.state)){
  await tagReviewLease(env,row,control=>executeTagWork(env,row.profile_id,control,true,workId));
  stored=await env.WORKSPACE_DB.prepare('SELECT * FROM workspace_tag_work WHERE profile_id=? AND work_id=?').bind(row.profile_id,workId).first<TagStoredWork>();
 }
 await currentTagRow(env,row);return {provenance:'original-tag-work',...publicTagWork(stored!),outcomeUnknown:stored!.state==='executing'||stored!.state==='unknownOutcome',gameEffects:0};
}
export async function scopedTagStatusAuthority(env:WorkspaceEnv,row:WorkspaceRow,authority:TagAuthority):Promise<TagStatusAuthority>{
 const works=await env.WORKSPACE_DB.prepare("SELECT intent_id FROM workspace_tag_work WHERE profile_id=? AND connection_id=? AND access_epoch=? AND state='confirmed'").bind(row.profile_id,row.connection_id,row.access_epoch).all<{intent_id:string}>();
 return {...authority,trustedIntentIds:new Set(works.results.map(work=>work.intent_id))};
}
export async function groupTagReadApi(env:WorkspaceEnv,initial:WorkspaceRow,name:GroupTagTool,args:unknown,origin:TagOrigin):Promise<Record<string,unknown>|null>{
 const input=groupTagInput(name,args),row=await currentTagRow(env,initial);
 if(name==='get_group_tag_status'){
  let authority:TagAuthority|null=null;try{authority=await tagAuthority(env,row,origin);}catch{/* Stored audit remains explicitly account-unverified. */}
  const result=groupTagStatus(JSON.parse(row.document_json),authority?await scopedTagStatusAuthority(env,row,authority):null,input);
  const uncertain=await env.WORKSPACE_DB.prepare("SELECT work_id,work_json FROM workspace_tag_work WHERE profile_id=? AND state IN ('executing','unknownOutcome')").bind(row.profile_id).all<{work_id:string;work_json:string}>();
  await currentTagRow(env,row);return {...result,groups:result.groups.map(group=>({...group,unknownWorkIds:uncertain.results.filter(work=>JSON.parse(work.work_json).context.entityId===group.entityId).map(work=>work.work_id)}))};
 }
 if(name==='recover_group_tag_receipt')return recoverGroupTagReceipt(env,row,String(input.workId));
 if(name==='inspect_group_tag_candidates'){
  const document=workspaceDocument(JSON.parse(row.document_json)),group=groupTagStatus(document,await requiredTagAuthority(env,row,origin),{kind:input.kind,entityId:input.entityId}).groups[0];if(!group)throw new WorkspaceError('group_unavailable',404);
  const authority=await requiredTagAuthority(env,row,origin),context={workspaceId:row.workspace_id,entityId:input.entityId,kind:input.kind},observed=await groupTagGateway(env,row,authority,context,'inspectTags',{});
  if(observed.accountId!==authority.accountId||!Array.isArray(observed.tags)||typeof observed.fingerprint!=='string')throw new WorkspaceError('tag_invalid_observation',502);
  if(input.expectedFingerprint!==undefined&&input.expectedFingerprint!==observed.fingerprint)throw new WorkspaceError('tag_candidates_changed',409);
  const tags=(observed.tags as {id:string;name:string;native:boolean}[]).filter(tag=>tag.native),offset=Number(input.offset??0),limit=Number(input.limit??20);await currentTagRow(env,row);
  return {workspaceId:row.workspace_id,revision:document.revision,entityId:input.entityId,kind:input.kind,name:group.name,fingerprint:observed.fingerprint,candidates:tags.slice(offset,offset+limit).map(tag=>({id:tag.id,name:tag.name,registeredForGroup:tag.id===observed.mappedTagId})),matchedCount:tags.length,nextOffset:offset+limit<tags.length?offset+limit:null};
 }
 return null;
}
