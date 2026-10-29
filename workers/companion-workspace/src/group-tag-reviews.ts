import {groupTagInput,type GroupTagTool} from '../../shared/group-tag-tools.ts';
import {desiredTagMembership} from '../../shared/tag-membership.ts';
import {workspaceDocument,type WorkspaceDocument} from '../../shared/workspace-domain.ts';
import {tagAlias,type TagLedger,type TagMapping,type TagIntent} from '../../shared/tag-projection.ts';
import {workspaceTokenHash,WorkspaceError} from '../../shared/workspace-http.ts';
import type {WorkspaceRow} from './executor.ts';
import {planTags,type TagWorkPlan} from './tag-planner.ts';
import {tagStatements,tagWorkBytes} from './tag-reconciliation.ts';
import {tagAuthority,type TagOrigin,type TagAuthority} from './tag-authority.ts';
import {closeTagReview,type TagReview as Review,tagReviewOutcome} from './group-tag-outcomes.ts';
import {groupTagReadApi,currentTagRow,requiredTagAuthority,groupTagGateway,tagReviewLease,recoverGroupTagReceipt,publicTagWork,type TagStoredWork} from './group-tag-api.ts';
import {executeTagWork} from './tag-executor.ts';
interface Payload {ledger?:TagLedger;work?:TagWorkPlan[];workId?:string;gateway?:{context:unknown;operation:string;arguments:Record<string,unknown>;attemptId?:string;attemptAtUtc?:string}}
const bytes=(value:string)=>new TextEncoder().encode(value).length,now=()=>new Date().toISOString();
const stable=(value:unknown):unknown=>Array.isArray(value)?value.map(stable):value&&typeof value==='object'?Object.fromEntries(Object.entries(value).sort(([a],[b])=>a.localeCompare(b)).map(([key,item])=>[key,stable(item)])):value;
const ledgerHash=(document:WorkspaceDocument)=>workspaceTokenHash(JSON.stringify(document.tagLedger??null));
function display(review:Review){const receipt=review.receipt_json?JSON.parse(review.receipt_json):null;return {...JSON.parse(review.preview_json),expired:!receipt&&review.expires_at_utc<=now(),savedInCompanion:receipt?.savedInCompanion===true,reviewOutcome:receipt?tagReviewOutcome(receipt):'prepared',...(receipt?{receipt}:{})};}
const loadReview=(env:WorkspaceEnv,row:WorkspaceRow,column:'command_id'|'request_id',id:string)=>env.WORKSPACE_DB.prepare('SELECT * FROM workspace_tag_reviews WHERE profile_id=? AND '+column+'=?').bind(row.profile_id,id).first<Review>();
async function workRow(env:WorkspaceEnv,row:WorkspaceRow,id:string){const work=await env.WORKSPACE_DB.prepare('SELECT * FROM workspace_tag_work WHERE profile_id=? AND work_id=?').bind(row.profile_id,id).first<TagStoredWork>();if(!work)throw new WorkspaceError('tag_work_unavailable',404);return work;}
async function staleReason(env:WorkspaceEnv,row:WorkspaceRow,review:Review):Promise<string|null>{
 if(review.connection_id!==row.connection_id||review.access_epoch!==row.access_epoch)return 'tag_connection_changed';
 if(review.expires_at_utc<=now())return 'tag_preview_expired';
 const document=workspaceDocument(JSON.parse(row.document_json));
 if(review.expected_revision!==document.revision||review.ledger_hash!==await ledgerHash(document))return 'tag_preview_changed';
 const saved=JSON.parse(review.authority_json) as TagAuthority;
 const current=await tagAuthority(env,row,{source:saved.source,tagWriteAllowed:saved.scopeAllowed,...(saved.grantEpoch?{grantEpoch:saved.grantEpoch}:{})});
 return JSON.stringify(saved)!==JSON.stringify(current)?'tag_authority_changed':null;
}
async function original(env:WorkspaceEnv,row:WorkspaceRow,review:Review){
 row=await currentTagRow(env,row);
 if(!review.receipt_json){
  let reason:string|null=null;try{reason=await staleReason(env,row,review);}catch{/* Unavailable authority cannot prove non-acceptance. */}
  if(reason)review=await closeTagReview(env,row,review,reason);
  else review=await loadReview(env,row,'command_id',review.command_id)??review;
 }
 if(!review.receipt_json)return {commandId:review.command_id,requestId:review.request_id,workspaceId:review.workspace_id,operation:review.operation,gameEffects:0,savedInCompanion:null,outcomeUnknown:true,prepared:true,reviewOutcome:'prepared',workIds:[]};
 const receipt=JSON.parse(review.receipt_json),payload=JSON.parse(review.payload_json) as Payload;
 if(receipt.savedInCompanion!==true)return receipt;
 if(payload.workId){const original=await recoverGroupTagReceipt(env,row,payload.workId),outcomeUnknown=original.outcomeUnknown;return {...receipt,original,outcomeUnknown,reviewOutcome:outcomeUnknown?'accepted-unknown':'accepted'};}
 return {...receipt,reviewOutcome:tagReviewOutcome(receipt)};
}
export async function groupTagApi(env:WorkspaceEnv,initial:WorkspaceRow,name:GroupTagTool,args:unknown,origin:TagOrigin):Promise<Record<string,unknown>>{
 const input=groupTagInput(name,args),read=await groupTagReadApi(env,initial,name,input,origin);if(read)return read;
 let row=await currentTagRow(env,initial);
 if(name==='get_group_tag_command'){
  const review=await loadReview(env,row,'command_id',String(input.commandId));return review?original(env,row,review):{commandId:input.commandId,savedInCompanion:null,outcomeUnknown:true,reviewOutcome:'accepted-unknown'};
 }
 if(name==='prepare_group_tag_command'){
  const hash=await workspaceTokenHash(JSON.stringify(stable(input))),existing=await loadReview(env,row,'request_id',String(input.requestId));
  if(existing){if(existing.input_hash!==hash)throw new WorkspaceError('request_id_reused',409);await original(env,row,existing);return display((await loadReview(env,row,'request_id',String(input.requestId)))!);}
  const authority=await requiredTagAuthority(env,row,origin),document=workspaceDocument(JSON.parse(row.document_json)),commandId=crypto.randomUUID();
  if(document.tagLedger&&document.tagLedger.accountId!==authority.accountId)throw new WorkspaceError('tag_account_mismatch',409);
  const payload:Payload={},operation=String(input.operation);let details:Record<string,unknown>,expires=new Date(Date.now()+600000).toISOString();
  if(operation==='recover'){
   const stored=await workRow(env,row,String(input.workId));if(!['executing','unknownOutcome'].includes(stored.state)||!stored.prepared_request_json)throw new WorkspaceError('tag_unknown_receipt_required',409);
   if(stored.connection_id!==row.connection_id||stored.access_epoch!==row.access_epoch)throw new WorkspaceError('tag_connection_changed',409);
   const prepared=JSON.parse(stored.prepared_request_json),work=JSON.parse(stored.work_json) as TagWorkPlan;
   const observed=await groupTagGateway(env,row,authority,prepared.context,'prepareTagRecovery',{operation:prepared.operation,arguments:prepared.arguments},{attemptId:prepared.attemptId,attemptAtUtc:prepared.attemptAtUtc});
   if(observed.kind!=='review')return {commandId:null,canAccept:false,reason:'original_receipt_available',original:await recoverGroupTagReceipt(env,row,stored.work_id),savedInCompanion:false};
   payload.workId=stored.work_id;payload.gateway={context:prepared.context,operation:'acceptTagRecovery',arguments:{operation:prepared.operation,arguments:prepared.arguments,checkToken:observed.checkToken,expectedFingerprint:observed.fingerprint},attemptId:prepared.attemptId,attemptAtUtc:prepared.attemptAtUtc};
   details={entityId:work.context.entityId,kind:work.context.kind,name:work.name,habiticaTagId:work.plannedTagId,taskChanges:[],originalWorkId:work.id,originalAttemptId:prepared.attemptId,observedTag:observed.tag??null,observedTask:observed.task??null,disclosure:observed.disclosure};expires=new Date(Date.now()+60000).toISOString();
  }else{
   if(await env.WORKSPACE_DB.prepare("SELECT 1 FROM workspace_tag_work WHERE profile_id=? AND state IN ('executing','unknownOutcome') LIMIT 1").bind(row.profile_id).first())throw new WorkspaceError('tag_unknown_receipt_required',409);
   const group=desiredTagMembership(document).groups.find(group=>group.entityId===input.entityId&&group.kind===input.kind);if(!group||group.retainMembership)throw new WorkspaceError('group_unavailable_or_archived',409);
   const source=workspaceDocument({...document,revision:document.revision+1});
   if(operation==='reconcile'){
    const plan=await planTags(source,authority,commandId,undefined,group.entityId);if(plan.gap||!plan.document.tagLedger)throw new WorkspaceError(plan.gap??'tag_plan_unavailable',409);
    payload.ledger=plan.document.tagLedger;payload.work=plan.work;
    const members=plan.work.find(work=>work.operation==='setMembership')?.members??[];
    details={entityId:group.entityId,kind:group.kind,name:group.name,habiticaTagId:payload.ledger.mappings.find(mapping=>mapping.entityId===group.entityId)?.habiticaTagId??null,desiredName:group.name,taskChanges:members.map(member=>({...member,text:document.tasks.find(task=>task.id===member.taskId)!.text})),retainsHistoricalTags:true,disclosure:'Ensure this exact group tag and the complete listed native task memberships. Missing counterparts and external conflicts require review. No task is created or scored; unrelated tags and native tag records remain.'};
   }else{
    const context={workspaceId:row.workspace_id,entityId:group.entityId,kind:group.kind},observed=await groupTagGateway(env,row,authority,context,'inspectTags',{});
    if(observed.accountId!==authority.accountId||!Array.isArray(observed.tags))throw new WorkspaceError('tag_invalid_observation',502);
    const candidate=(observed.tags as {id:string;name:string;native:boolean}[]).find(tag=>tag.id===input.habiticaTagId&&tag.native);if(!candidate)throw new WorkspaceError('tag_candidate_unavailable',409);
    const ledger=structuredClone(document.tagLedger??{accountId:authority.accountId,mappings:[],intents:[]}) as TagLedger;
    let mapping=ledger.mappings.find(mapping=>mapping.entityId===group.entityId);
    if(mapping?.habiticaTagId&&mapping.habiticaTagId!==candidate.id)throw new WorkspaceError('tag_identity_requires_review',409);
    if(!mapping){mapping={id:crypto.randomUUID(),revision:source.revision,isArchived:group.archived,entityId:group.entityId,kind:group.kind,state:'pending',desiredRevision:source.revision,correlationId:commandId,correlationAlias:tagAlias(source.id,group.kind,group.entityId),desiredName:group.name,habiticaTagId:null,evidence:null};ledger.mappings.push(mapping);}
    Object.assign(mapping,{revision:source.revision,state:'pending',desiredRevision:source.revision,correlationId:commandId,desiredName:group.name});
    const intent:TagIntent={id:crypto.randomUUID(),revision:source.revision,isArchived:group.archived,mappingId:mapping.id,commandId,correlationId:crypto.randomUUID(),desiredRevision:source.revision,operation:'ensureTag',state:'pending',habiticaTagId:candidate.id,desiredName:group.name,members:null,evidence:null};ledger.intents.push(intent);
    const work:TagWorkPlan={id:crypto.randomUUID(),intentId:intent.id,commandId,sourceRevision:source.revision,desiredHash:await workspaceTokenHash(JSON.stringify({context,name:group.name,members:group.taskIds,removed:group.removed})),context,accountId:authority.accountId,consentEpoch:authority.consentEpoch!,origin:authority,operation:'ensureTag',mappingId:mapping.id,plannedTagId:candidate.id,name:candidate.name,members:[],state:'pending',error:null,manualReview:true};
    payload.ledger=ledger;payload.work=[work];payload.workId=work.id;payload.gateway={context,operation:'adoptTag',arguments:{tagId:candidate.id,checkToken:observed.checkToken,expectedFingerprint:observed.fingerprint}};
    details={entityId:group.entityId,kind:group.kind,name:group.name,habiticaTagId:candidate.id,observedTag:candidate,desiredName:group.name,taskChanges:[],disclosure:'Associate only this exact existing native tag ID with this group. Matching names do not prove identity. Adoption sends no native mutation and gives no game rewards. Different native names and membership still need a separate reconciliation review.'};expires=new Date(Date.now()+60000).toISOString();
   }
  }
  const preview={commandId,requestId:input.requestId,workspaceId:row.workspace_id,operation,expectedRevision:document.revision,expiresAtUtc:expires,canAccept:true,gameEffects:0,details},payloadJson=JSON.stringify(payload),previewJson=JSON.stringify(preview),size=bytes(payloadJson)+bytes(previewJson)+512;
  if(bytes(payloadJson)+bytes(previewJson)>1024*1024||row.archive_bytes+size>8*1024*1024-32768)throw new WorkspaceError('tag_review_capacity',413);
  const tag=crypto.randomUUID(),result=await env.WORKSPACE_DB.batch([
   env.WORKSPACE_DB.prepare('UPDATE workspaces SET archive_bytes=archive_bytes+?,store_version=store_version+1,commit_tag=? WHERE profile_id=? AND connection_id=? AND access_epoch=? AND store_version=? AND active=1').bind(size,tag,row.profile_id,row.connection_id,row.access_epoch,row.store_version),
   env.WORKSPACE_DB.prepare(`INSERT INTO workspace_tag_reviews(profile_id,command_id,request_id,input_hash,workspace_id,connection_id,access_epoch,authority_json,expected_revision,ledger_hash,operation,payload_json,preview_json,created_at_utc,expires_at_utc) SELECT profile_id,?,?,?,workspace_id,connection_id,access_epoch,?,?,?,?,?,?,?,? FROM workspaces WHERE profile_id=? AND commit_tag=?`).bind(commandId,input.requestId,hash,JSON.stringify(authority),document.revision,await ledgerHash(document),operation,payloadJson,previewJson,now(),expires,row.profile_id,tag)
  ]);
  if(!result[0].meta.changes)throw new WorkspaceError('workspace_changed',409);const saved=await loadReview(env,row,'request_id',String(input.requestId));if(!saved||saved.input_hash!==hash)throw new WorkspaceError('request_id_reused',409);return display(saved);
 }
 const review=await loadReview(env,row,'command_id',String(input.commandId));if(!review)throw new WorkspaceError('tag_review_required',409);
 if(review.receipt_json)return original(env,row,review);
 const stale=await staleReason(env,row,review);if(stale)return original(env,row,await closeTagReview(env,row,review,stale));
 const authority=await requiredTagAuthority(env,row,origin);
 if(review.authority_json!==JSON.stringify(authority))throw new WorkspaceError('tag_preview_changed',409);
 const payload=JSON.parse(review.payload_json) as Payload,receipt={commandId:review.command_id,requestId:review.request_id,workspaceId:row.workspace_id,operation:review.operation,savedInCompanion:true,gameEffects:0,outcomeUnknown:review.operation!=='reconcile',reviewOutcome:review.operation==='reconcile'?'accepted':'accepted-unknown',workIds:payload.work?.map(work=>work.id)??(payload.workId?[payload.workId]:[])};
 return tagReviewLease(env,row,async control=>{
  const originalReview=await loadReview(env,row,'command_id',review.command_id);
  if(originalReview?.receipt_json){const saved=JSON.parse(originalReview.receipt_json);return {...saved,reviewOutcome:tagReviewOutcome(saved)};}
  if(!originalReview)throw new WorkspaceError('tag_preview_expired_or_changed',409);
  row=await currentTagRow(env,row);const changed=await staleReason(env,row,originalReview);if(changed)return original(env,row,await closeTagReview(env,row,originalReview,changed));const current=workspaceDocument(JSON.parse(row.document_json));if(row.store_version>=Number.MAX_SAFE_INTEGER)throw new WorkspaceError('tag_preview_changed',409);
  const next=review.operation==='recover'?current:workspaceDocument({...current,revision:current.revision+1,tagLedger:payload.ledger}),serialized=JSON.stringify(next),commit=crypto.randomUUID();
  let prepared:Record<string,unknown>|null=null;
  if(review.operation==='adopt')prepared={profileId:row.profile_id,connectionId:row.connection_id,...payload.gateway,automated:false,attemptId:crypto.randomUUID(),attemptAtUtc:now(),expectedConsentEpoch:authority.consentEpoch,...(authority.source==='mcp'?{requiredGrantEpoch:authority.grantEpoch}:{})};
  const plan={document:next,work:payload.work??[],gap:null},size=row.archive_bytes-bytes(row.document_json)+bytes(serialized)+tagWorkBytes(plan)+bytes(prepared?JSON.stringify(prepared):'')+bytes(JSON.stringify(receipt));if(size>8*1024*1024||bytes(serialized)>1024*1024)throw new WorkspaceError('workspace_capacity',413);
  const statements=[env.WORKSPACE_DB.prepare(`UPDATE workspaces SET document_json=?,archive_bytes=?,store_version=store_version+1,commit_tag=?,updated_at_utc=? WHERE profile_id=? AND access_epoch=? AND store_version=? AND active=1 AND EXISTS(SELECT 1 FROM workspace_tag_reviews WHERE profile_id=workspaces.profile_id AND command_id=? AND receipt_json IS NULL AND expires_at_utc>?) AND NOT EXISTS(SELECT 1 FROM workspace_tag_work w WHERE w.profile_id=workspaces.profile_id AND w.command_id=?) AND EXISTS(SELECT 1 FROM workspace_runtime WHERE profile_id=? AND access_epoch=? AND lease_token=? AND lease_until_utc>?)`).bind(serialized,size,commit,now(),row.profile_id,row.access_epoch,row.store_version,review.command_id,now(),review.command_id,row.profile_id,row.access_epoch,control.token,now()),
   ...tagStatements(env,row,plan,commit),
   env.WORKSPACE_DB.prepare('UPDATE workspace_tag_reviews SET receipt_json=? WHERE profile_id=? AND command_id=? AND receipt_json IS NULL AND EXISTS(SELECT 1 FROM workspaces WHERE profile_id=? AND commit_tag=?)').bind(JSON.stringify(receipt),row.profile_id,review.command_id,row.profile_id,commit)];
  if(prepared)statements.push(env.WORKSPACE_DB.prepare("UPDATE workspace_tag_work SET state='executing',prepared_request_json=? WHERE profile_id=? AND work_id=? AND EXISTS(SELECT 1 FROM workspaces WHERE profile_id=? AND commit_tag=?)").bind(JSON.stringify(prepared),row.profile_id,payload.workId,row.profile_id,commit));
  const committed=await env.WORKSPACE_DB.batch(statements);if(!committed[0].meta.changes){const latest=await loadReview(env,row,'command_id',review.command_id);if(latest?.receipt_json)return original(env,row,latest);throw new WorkspaceError('tag_preview_changed',409);}
  if(review.operation==='reconcile')return receipt;
  try{
   const gateway=payload.gateway!;
   const response=await env.AUTH_PROJECTION.fetch('https://projection.internal/tags',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify(prepared??{profileId:row.profile_id,connectionId:row.connection_id,...gateway,automated:false,expectedConsentEpoch:authority.consentEpoch,...(authority.source==='mcp'?{requiredGrantEpoch:authority.grantEpoch}:{})}),signal:AbortSignal.timeout(15000)});
   const outcome=await response.json<{kind?:string;code?:string}>();
   if(review.operation==='adopt'&&outcome.kind==='refused'){
    const fresh=await currentTagRow(env,row),d=workspaceDocument(JSON.parse(fresh.document_json)),work=payload.work![0],intent=d.tagLedger!.intents.find(intent=>intent.id===work.intentId)!;intent.state='needsReview';d.tagLedger!.mappings.find(mapping=>mapping.id===work.mappingId)!.state='needsReview';const encoded=JSON.stringify(d),t=crypto.randomUUID();
    await env.WORKSPACE_DB.batch([env.WORKSPACE_DB.prepare('UPDATE workspaces SET document_json=?,archive_bytes=archive_bytes+?,store_version=store_version+1,commit_tag=? WHERE profile_id=? AND access_epoch=? AND store_version=? AND active=1').bind(encoded,bytes(encoded)-bytes(fresh.document_json),t,row.profile_id,row.access_epoch,fresh.store_version),env.WORKSPACE_DB.prepare("UPDATE workspace_tag_work SET state='needsReview',error_code=? WHERE profile_id=? AND work_id=? AND EXISTS(SELECT 1 FROM workspaces WHERE profile_id=? AND commit_tag=?)").bind(outcome.code??'tag_adoption_refused',row.profile_id,work.id,row.profile_id,t)]);
   }else await executeTagWork(env,row.profile_id,control,true,payload.workId);
  }catch{/* Original persisted request remains uncertain; a duplicate acceptance never resends. */}
  const work=await workRow(env,row,payload.workId!);const outcomeUnknown=['executing','unknownOutcome'].includes(work.state);return {...receipt,original:publicTagWork(work),outcomeUnknown,reviewOutcome:outcomeUnknown?'accepted-unknown':'accepted'};
 });
}
