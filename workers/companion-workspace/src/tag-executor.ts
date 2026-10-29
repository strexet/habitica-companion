import {tagsFingerprint} from '../../shared/tag-gateway.ts';
import {workspaceDocument,type WorkspaceDocument} from '../../shared/workspace-domain.ts';
import {desiredTagMembership} from '../../shared/tag-membership.ts';
import {workspaceTokenHash} from '../../shared/workspace-http.ts';
import type {TagEvidence} from '../../shared/tag-projection.ts';
import {tagAuthority,tagAuthorityGap} from './tag-authority.ts';
import type {TagWorkPlan} from './tag-planner.ts';
import type {WorkspaceRow,ExecutorEnv} from './executor.ts';
interface WorkRow {work_id:string;work_json:string;state:string;prepared_request_json:string|null;connection_id:string;access_epoch:string;retry_at_utc:string|null}
interface Prepared {profileId:string;connectionId:string;context:TagWorkPlan['context'];operation:string;arguments:Record<string,unknown>;automated:boolean;attemptId:string;attemptAtUtc:string;memberTaskId?:string;expectedConsentEpoch?:string;requiredGrantEpoch?:string}
interface Outcome {kind?:string;code?:string;retryAtUtc?:string;accountId?:string;checkToken?:string;fingerprint?:string;mappedTagId?:string|null;mappedTagFingerprint?:string|null;tags?:{id:string;name:string;native:boolean}[];receiptId?:string;confirmedAtUtc?:string;tag?:{id:string;name:string;fingerprint:string};task?:{id:string;kind:string;fingerprint:string;tags?:string[]}}
export interface TagExecutorControl {runtime:()=>Promise<{next_network_at_utc:string|null}|null>;row:()=>Promise<WorkspaceRow|null>;permitted:()=>Promise<boolean>;wait:(ms:number)=>Promise<void>;deadline:number;token:string;epoch:string}
const bytes=(value:string)=>new TextEncoder().encode(value).length;
// Called under the same durable profile lease as game task projection. One metadata effect per wake.
export async function executeTagWork(env:ExecutorEnv,profileId:string,control:TagExecutorControl,uncertainOnly=false,workId?:string):Promise<string|null>{
 const stored=await env.WORKSPACE_DB.prepare(`SELECT * FROM workspace_tag_work WHERE profile_id=? AND state IN ('executing','unknownOutcome'${uncertainOnly?'':",'pending'"}) AND (? IS NULL OR work_id=?) ORDER BY CASE WHEN state IN ('executing','unknownOutcome') THEN 0 ELSE 1 END,created_at_utc,CASE json_extract(work_json,'$.operation') WHEN 'ensureTag' THEN 0 ELSE 1 END,work_id LIMIT 1`).bind(profileId,workId??null,workId??null).first<WorkRow>();
 if(!stored)return null;
 let work=JSON.parse(stored.work_json) as TagWorkPlan;
 async function save(state:string,error:string|null,update:(document:WorkspaceDocument)=>void=()=>{},prepared?:Prepared|null,retry:string|null=null,expectedVersion?:number):Promise<boolean>{
  for(let n=0;n<3;n++){
   const row=await control.row();if(!row||!await control.runtime()||expectedVersion!==undefined&&row.store_version!==expectedVersion)return false;
   const document=workspaceDocument(JSON.parse(row.document_json));update(document);
   const intent=document.tagLedger?.intents.find(item=>item.id===work.intentId);if(intent&&['pending','needsReview','unknownOutcome','confirmed'].includes(state))intent.state=state as typeof intent.state;
   const serialized=JSON.stringify(workspaceDocument(document));
   const oldWork=await env.WORKSPACE_DB.prepare('SELECT work_json,prepared_request_json FROM workspace_tag_work WHERE profile_id=? AND work_id=?').bind(profileId,work.id).first<{work_json:string;prepared_request_json:string|null}>();if(!oldWork)return false;
   const encodedWork=JSON.stringify(work),encodedPrepared=prepared===undefined?oldWork.prepared_request_json:prepared?JSON.stringify(prepared):null;
   const size=row.archive_bytes-bytes(row.document_json)+bytes(serialized)-bytes(oldWork.work_json)+bytes(encodedWork)-bytes(oldWork.prepared_request_json??'')+bytes(encodedPrepared??'');
   if(size>8*1024*1024||bytes(serialized)>1024*1024||row.store_version>=Number.MAX_SAFE_INTEGER)return false;
   const tag=crypto.randomUUID(),now=new Date().toISOString();
   const result=await env.WORKSPACE_DB.batch([
    env.WORKSPACE_DB.prepare(`UPDATE workspaces SET document_json=?,archive_bytes=?,store_version=store_version+1,commit_tag=?,updated_at_utc=? WHERE profile_id=? AND access_epoch=? AND store_version=? AND active=1 AND EXISTS(SELECT 1 FROM workspace_runtime WHERE profile_id=? AND access_epoch=? AND lease_token=? AND lease_until_utc>?)`).bind(serialized,size,tag,now,profileId,control.epoch,row.store_version,profileId,control.epoch,control.token,now),
    env.WORKSPACE_DB.prepare(`UPDATE workspace_tag_work SET work_json=?,state=?,error_code=?,retry_at_utc=?,updated_at_utc=?,prepared_request_json=CASE WHEN ?=1 THEN ? ELSE prepared_request_json END WHERE profile_id=? AND work_id=? AND EXISTS(SELECT 1 FROM workspaces WHERE profile_id=? AND commit_tag=?)`).bind(encodedWork,state,error,retry,now,Number(prepared!==undefined),encodedPrepared,profileId,work.id,profileId,tag)
   ]);if(result[0].meta.changes)return true;
  }return false;
 }
 const review=async(code:string)=>{await save('needsReview',code,document=>{if(work.operation==='ensureTag'){const mapping=document.tagLedger?.mappings.find(item=>item.id===work.mappingId);if(mapping)mapping.state='needsReview';}});return 'tag_review';};
 async function paced(){
  const runtime=await control.runtime(),delay=Math.max(0,Date.parse(runtime?.next_network_at_utc??'')-Date.now());
  if(Number.isFinite(delay)&&delay>0){if(Date.now()+delay+15000>=control.deadline)throw new Error('activation_deadline');await control.wait(delay);}
  if(!await control.permitted()||(await control.row())?.linked_recovery_fence)throw new Error('executor_paused');
  const result=await env.WORKSPACE_DB.prepare('UPDATE workspace_runtime SET next_network_at_utc=? WHERE profile_id=? AND access_epoch=? AND lease_token=? AND lease_until_utc>?').bind(new Date(Date.now()+30000).toISOString(),profileId,control.epoch,control.token,new Date().toISOString()).run();if(!result.meta.changes)throw new Error('executor_paused');
 }
 async function gateway(operation:string,args:Record<string,unknown>,prepared?:Prepared,lookup=false):Promise<Outcome>{
  if(!lookup)await paced();else if(!await control.permitted()||(await control.row())?.linked_recovery_fence)throw new Error('executor_paused');
  const payload=prepared?{...prepared}:{profileId,connectionId:stored!.connection_id,context:work.context,operation,arguments:args,automated:true,expectedConsentEpoch:work.consentEpoch,...(work.origin.source==='mcp'?{requiredGrantEpoch:work.origin.grantEpoch}: {})};delete (payload as Partial<Prepared>).memberTaskId;
  const response=await env.AUTH_PROJECTION.fetch('https://projection.internal/tags',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify(payload),signal:AbortSignal.timeout(15000)});
  const outcome=await response.json<Outcome>();if(lookup&&!response.ok)return {kind:'unknown',code:'tag_receipt_unavailable'};return !response.ok&&!outcome.kind?{kind:lookup?'unknown':'refused',code:'tag_gateway_unavailable'}:outcome;
 }
 async function finish(outcome:Outcome,prepared:Prepared):Promise<string>{
  if(outcome.kind!=='applied'){
   if(outcome.kind==='refused'){
    const retry=!work.manualReview&&['projection_busy','rate_limited'].includes(outcome.code??'')&&outcome.retryAtUtc&&Date.parse(outcome.retryAtUtc)>Date.now()?outcome.retryAtUtc:null;
    await save(retry?'pending':'needsReview',outcome.code??'tag_refused',()=>{},null,retry);return retry?'tag_limited':'tag_review';
   }
   await save('unknownOutcome',outcome.code??'tag_transport_unknown',document=>{if(work.operation==='ensureTag'){const mapping=document.tagLedger?.mappings.find(item=>item.id===work.mappingId);if(mapping)mapping.state='unknownOutcome';}});return 'tag_unknown';
  }
  const fingerprint=outcome.tag?.fingerprint??outcome.task?.fingerprint;
  if(typeof outcome.receiptId!=='string'||typeof outcome.confirmedAtUtc!=='string'||!fingerprint)return 'tag_invalid_receipt';
  const proof:TagEvidence={receiptId:outcome.receiptId,confirmedAtUtc:outcome.confirmedAtUtc,fingerprint};
  const confirmed=work.operation==='ensureTag'||work.members.every(member=>member.state==='confirmed'||member.taskId===prepared.memberTaskId);
  if(prepared.memberTaskId){const member=work.members.find(item=>item.taskId===prepared.memberTaskId);if(!member||outcome.task?.id!==member.habiticaTaskId)return 'tag_invalid_receipt';Object.assign(member,{state:'confirmed',evidence:proof});}
  else if(outcome.tag?.id!==work.plannedTagId||outcome.tag.name!==work.name)return 'tag_invalid_receipt';
  const saved=await save(confirmed?'confirmed':'pending',null,document=>{
   const ledger=document.tagLedger!,mapping=ledger.mappings.find(item=>item.id===work.mappingId)!,intent=ledger.intents.find(item=>item.id===work.intentId)!;
   if(work.operation==='ensureTag'){Object.assign(mapping,{state:mapping.desiredName===work.name?'confirmed':'needsReview',habiticaTagId:work.plannedTagId,evidence:proof});Object.assign(intent,{habiticaTagId:work.plannedTagId,evidence:proof});}
   else {intent.members=work.members;if(confirmed)intent.evidence=proof;}
  },null);
  return saved?'tag_applied':'tag_conflict';
 }
 // Original receipt lookup precedes identity/scope checks and stale-source rejection. Never resend.
 if(['executing','unknownOutcome'].includes(stored.state)){
  if(!stored.prepared_request_json)return 'tag_unknown';const prepared=JSON.parse(stored.prepared_request_json) as Prepared;
  let outcome:Outcome;try{outcome=await gateway('lookupTagAttempt',{}, {...prepared,operation:'lookupTagAttempt',arguments:{operation:prepared.operation,arguments:prepared.arguments}},true);}catch{return 'tag_unknown';}
  if(stored.state==='unknownOutcome'&&outcome.kind!=='applied'&&outcome.kind!=='refused')return 'tag_unknown';
  return finish(outcome,prepared);
 }
 if(stored.retry_at_utc&&Date.parse(stored.retry_at_utc)>Date.now())return 'tag_limited';
 const row=await control.row();if(!row||row.retirement_fence)return 'tag_paused';
 if(stored.access_epoch!==row.access_epoch||stored.connection_id!==row.connection_id)return review('tag_connection_changed');
 let authority;try{authority=await tagAuthority(env,row,{source:work.origin.source,tagWriteAllowed:work.origin.scopeAllowed,grantEpoch:work.origin.grantEpoch??undefined});}catch{return review('tag_authority_unavailable');}
 const gap=tagAuthorityGap(authority);if(gap)return review(gap);
 if(authority.accountId!==work.accountId||authority.consentEpoch!==work.consentEpoch)return review('tag_authority_changed');
 let document=workspaceDocument(JSON.parse(row.document_json));const group=desiredTagMembership(document).groups.find(item=>item.entityId===work.context.entityId&&item.kind===work.context.kind);
 if(!group||group.retainMembership||await workspaceTokenHash(JSON.stringify({context:work.context,name:group.name,members:group.taskIds,removed:group.removed}))!==work.desiredHash)return review('tag_source_changed');
 const mapping=document.tagLedger?.mappings.find(item=>item.id===work.mappingId),intent=document.tagLedger?.intents.find(item=>item.id===work.intentId);
 if(!mapping||!intent||document.tagLedger?.accountId!==work.accountId)return review('tag_ledger_changed');
 const tags=await gateway('inspectTags',{});if(tags.kind==='refused')return review(tags.code??'tag_inspect_failed');
 if(tags.accountId!==work.accountId||!tags.tags||!tags.checkToken||!tags.fingerprint)return review('tag_invalid_observation');
 const remote=tags.tags.find(item=>item.id===work.plannedTagId&&item.native);
 if(remote&&tags.mappedTagId===remote.id&&await tagsFingerprint([remote])!==tags.mappedTagFingerprint)return review('tag_external_edit_requires_review');
 let operation:string,args:Record<string,unknown>,memberTaskId:string|undefined;
 if(work.operation==='ensureTag'){
  if(mapping.habiticaTagId&&tags.mappedTagId!==mapping.habiticaTagId)return review('tag_adoption_required');
  if(remote&&!mapping.habiticaTagId)return review('tag_adoption_required');
  if(mapping.habiticaTagId&&!remote)return review('tag_external_deleted');
  // A confirmed registry identity may be rechecked without a mutation, using a durable observation receipt.
  operation=remote?(remote.name===work.name?'adoptTag':'renameTag'):'createTag';
  args={tagId:work.plannedTagId,...(operation==='adoptTag'?{}:{name:work.name}),checkToken:tags.checkToken,expectedFingerprint:tags.fingerprint};
 }else{
  if(!remote||tags.mappedTagId!==work.plannedTagId||mapping.state!=='confirmed')return review('tag_identity_requires_review');
  const member=work.members.find(item=>item.state!=='confirmed');
  if(!member){await save('confirmed',null,doc=>{const current=doc.tagLedger!.intents.find(item=>item.id===work.intentId)!;current.members=work.members;current.evidence=mapping.evidence;});return 'tag_confirmed';}
  const taskMapping=document.projections.find(item=>item.taskId===member.taskId&&!item.isArchived&&item.state==='linked');
  if(!taskMapping?.habiticaTaskId)return review('tag_task_counterpart_required');
  if(member.habiticaTaskId&&member.habiticaTaskId!==taskMapping.habiticaTaskId)return review('tag_task_identity_changed');
  member.habiticaTaskId=taskMapping.habiticaTaskId;
  const observed=await gateway('inspectMembership',{tagId:work.plannedTagId,taskId:member.habiticaTaskId,tagCheckToken:tags.checkToken});
  if(observed.kind==='refused')return review(observed.code??'tag_inspect_failed');
  if(observed.accountId!==work.accountId||observed.task?.id!==member.habiticaTaskId||!observed.checkToken||!observed.fingerprint)return review('tag_invalid_observation');
  operation='setMembership';memberTaskId=member.taskId;args={tagId:work.plannedTagId,taskId:member.habiticaTaskId,present:member.present,checkToken:observed.checkToken,expectedFingerprint:observed.fingerprint};
 }
 // Pace before creating the attempt, then recheck the exact source after any awaited inspection.
 await paced();const latest=await control.row();if(!latest||latest.store_version!==row.store_version)return review('tag_source_changed');
 const prepared:Prepared={profileId,connectionId:row.connection_id,context:work.context,operation,arguments:args,automated:true,attemptId:crypto.randomUUID(),attemptAtUtc:new Date().toISOString(),expectedConsentEpoch:work.consentEpoch,...(work.origin.source==='mcp'&&work.origin.grantEpoch?{requiredGrantEpoch:work.origin.grantEpoch}:{}),...(memberTaskId?{memberTaskId}:{})};
 if(!await save('executing',null,()=>{},prepared,null,latest.store_version))return 'tag_conflict';
 // Pacing was already reserved. Persisted request is sent once, with its original timestamp and token.
 let outcome:Outcome;try{
  if(!await control.permitted())return 'tag_paused';
  const payload={...prepared};delete payload.memberTaskId;
  const response=await env.AUTH_PROJECTION.fetch('https://projection.internal/tags',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify(payload),signal:AbortSignal.timeout(15000)});outcome=await response.json<Outcome>();
 }catch{outcome={kind:'unknown',code:'tag_transport_unknown'};}
 return finish(outcome,prepared);
}
