import {retirementInput,type RetirementTool} from '../../shared/retirement-tools.ts';
import {workspaceTokenHash,WorkspaceError} from '../../shared/workspace-http.ts';
import {remotePatchMatches,type RemoteTask} from '../../shared/projection.ts';
import {desiredTagMembership} from '../../shared/tag-membership.ts';
import {groupTagStatus} from '../../shared/group-tag-status.ts';
import {inspectRetirement,type RetirementPlan} from './retirement-policy.ts';
import {journal,type Journal} from './journal.ts';
import {cloudAuthority,type WorkspaceRow} from './executor.ts';
import {scopedTagStatusAuthority,currentTagRow,requiredTagAuthority,groupTagGateway,tagReviewLease} from './group-tag-api.ts';
import {browserTagOrigin} from './tag-reconciliation.ts';
import type {TagOrigin,TagAuthority} from './tag-authority.ts';
import type {SourceRetirement} from '../../shared/source-retirements.ts';
interface Verification {cursor:number;firstAt:string|null;sourceAbsent:boolean;sourceToken:string|null;verifiedAt:string|null;tagIds:Record<string,string[]>}
interface Stored {command_id:string;request_id:string;input_hash:string;connection_id:string;access_epoch:string;grant_epoch:string;authority_json:string;plan_json:string;verification_json:string;state:string;prepared_request_json:string|null;native_outcome_json:string|null;receipt_json:string|null;error_code:string|null;expires_at_utc:string}
interface Outcome {kind?:string;code?:string;task?:RemoteTask&{tags?:string[]};checkToken?:string;retryAtUtc?:string}
const now=()=>new Date().toISOString(),bytes=(s:string)=>new TextEncoder().encode(s).length;
const stable=(v:unknown):unknown=>Array.isArray(v)?v.map(stable):v&&typeof v==='object'?Object.fromEntries(Object.entries(v).sort(([a],[b])=>a.localeCompare(b)).map(([k,x])=>[k,stable(x)])):v;
const same=(a:unknown,b:unknown)=>JSON.stringify(stable(a))===JSON.stringify(stable(b));
const disclosure='Only the obsolete source Todo will be permanently deleted in Habitica. Its full source and conversion history stay archived in Companion. Replacement tasks, notes, completion history and tags are retained. No score or reward occurs. Habitica provides no conditional DELETE: external edits can race the final source read. Verification describes observations, not a cross-service transaction.';
async function load(env:WorkspaceEnv,initial:WorkspaceRow){
 const rows=await env.WORKSPACE_DB.batch([env.WORKSPACE_DB.prepare('SELECT * FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1').bind(initial.profile_id,initial.connection_id,initial.access_epoch),env.WORKSPACE_DB.prepare('SELECT entry_json FROM workspace_commands WHERE profile_id=? ORDER BY sequence').bind(initial.profile_id),env.WORKSPACE_DB.prepare('SELECT event_json FROM workspace_events WHERE profile_id=? ORDER BY sequence').bind(initial.profile_id)]),row=rows[0].results[0] as WorkspaceRow|undefined;
 if(!row)throw new WorkspaceError('unauthorized',401);return {row,state:await journal({document:JSON.parse(row.document_json),storeVersion:row.store_version,authority:row.authority_json?JSON.parse(row.authority_json):cloudAuthority(row.workspace_id),commands:rows[1].results.map(x=>JSON.parse(String((x as {entry_json:string}).entry_json))),events:rows[2].results.map(x=>JSON.parse(String((x as {event_json:string}).event_json)))})};
}
async function gateway(env:WorkspaceEnv,row:WorkspaceRow,operation:string,args:unknown,extra:Record<string,unknown>={}):Promise<Outcome>{
 const response=await env.AUTH_PROJECTION.fetch('https://projection.internal/operation',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({profileId:row.profile_id,connectionId:row.connection_id,operation,arguments:args,automated:false,...extra}),signal:AbortSignal.timeout(operation==='retireSource'?25000:15000)}),value=await response.json<Outcome>();
 if(!response.ok)throw new WorkspaceError(value.code??'retirement_gateway_unavailable',response.status);if(value.kind==='refused'&&!['lookup','reconcile'].includes(operation))throw new WorkspaceError(value.code??'retirement_refused',409);return value;
}
function view(stored:Stored){const plan=JSON.parse(stored.plan_json) as RetirementPlan,v=JSON.parse(stored.verification_json) as Verification;return {commandId:stored.command_id,requestId:stored.request_id,workspaceId:plan.workspaceId,conversionId:plan.conversionId,contextHash:plan.contextHash,revision:plan.revision,source:plan.source,sourceMapping:plan.sourceMapping,habiticaProjectTagId:plan.habiticaProjectTagId,habiticaTaskId:plan.sourceMapping.habiticaTaskId,project:plan.project,targets:plan.targets,verifiedTargets:Math.min(Math.max(0,v.cursor-1),plan.targets.length),verificationSteps:plan.targets.length+2,completedSteps:v.cursor,state:stored.state,sourceAlreadyAbsent:v.sourceAbsent,verifiedAtUtc:v.verifiedAt,expiresAtUtc:stored.expires_at_utc,expired:stored.expires_at_utc<=now()||Boolean(v.firstAt&&Date.parse(v.firstAt)+300000<Date.now()),outcomeUnknown:['executing','unknownOutcome'].includes(stored.state),savedInCompanion:stored.receipt_json?true:null,receipt:stored.receipt_json?JSON.parse(stored.receipt_json):null,error:stored.error_code,gameScores:0,disclosure};}
async function prerequisites(env:WorkspaceEnv,row:WorkspaceRow,state:Journal,conversionId:string,origin:TagOrigin){
 if(row.linked_recovery_fence)throw new WorkspaceError('linked_recovery_pending',409);
 if(state.authority?.mode!=='cloud'||state.authority.cloudWorkspaceId!==row.workspace_id)throw new WorkspaceError('workspace_owned_elsewhere',409);
 const authority=await requiredTagAuthority(env,row,origin),readiness=await inspectRetirement(state,conversionId,authority.accountId);if(!readiness.plan)throw new WorkspaceError(readiness.blockers[0]??'retirement_not_ready',409);
 const pending=await env.WORKSPACE_DB.prepare("SELECT work_id FROM workspace_tag_work WHERE profile_id=? AND state IN ('pending','executing','unknownOutcome') LIMIT 1").bind(row.profile_id).first();if(pending)throw new WorkspaceError('unresolved_tag_effects',409);
 const plan=readiness.plan,targetIds=new Set(plan.targets.map(x=>x.task.id)),groups=desiredTagMembership(state.document).groups.filter(group=>group.taskIds.some(id=>targetIds.has(id)));
 // Full desired groups are checked, independently of the public status page's pagination.
 const statusAuthority=await scopedTagStatusAuthority(env,row,authority);
 for(const group of groups){const status=groupTagStatus(state.document,statusAuthority,{kind:group.kind,entityId:group.entityId}).groups[0];if(!status||!['confirmed','retained'].includes(status.membershipState)||!status.habiticaTagId)throw new WorkspaceError('required_tag_membership_requires_review',409);}
 return {plan,authority,groups};
}
function matches(remote:RemoteTask,task:RetirementPlan['source'],fingerprint:string){return remote.kind==='todo'&&remote.fingerprint===fingerprint&&remote.completed===(task.state==='completed')&&remotePatchMatches(remote,{text:task.text,notes:task.notes??'',date:task.deadline??null,priority:task.habiticaDetails?.difficulty??1,attribute:task.habiticaDetails?.attribute??'str',checklist:task.habiticaDetails?.checklist??[]});}
async function finalize(env:WorkspaceEnv,row:WorkspaceRow,stored:Stored,outcome:SourceRetirement['outcome']){
 const {row:current,state}=await load(env,row),plan=JSON.parse(stored.plan_json) as RetirementPlan,v=JSON.parse(stored.verification_json) as Verification,at=now(),revision=state.document.revision+1;
 if(state.document.sourceRetirements?.some(x=>x.id===stored.command_id))return;
 if(state.document.revision!==plan.revision||revision>Number.MAX_SAFE_INTEGER||current.store_version>=Number.MAX_SAFE_INTEGER)throw new WorkspaceError('retirement_save_requires_recovery',409);
 const original=stored.prepared_request_json?JSON.parse(stored.prepared_request_json):null;
 const receipt:SourceRetirement={id:stored.command_id,revision,isArchived:false,conversionId:plan.conversionId,sourceTaskId:plan.source.id,sourceRevision:plan.source.revision,reviewedRevision:plan.revision,accountId:(JSON.parse(stored.authority_json) as TagAuthority).accountId,habiticaTaskId:plan.sourceMapping.habiticaTaskId!,contextHash:plan.contextHash,outcome,nativeAttemptId:outcome==='alreadyAbsent'?null:original.attemptId,verifiedAtUtc:v.verifiedAt!,retiredAtUtc:at};
 const document={...state.document,revision,tasks:state.document.tasks.map(task=>task.id===plan.source.id?{...task,revision,isArchived:true,updatedAtUtc:at}:task),projections:state.document.projections.map(mapping=>mapping.taskId===plan.source.id?{...mapping,revision,isArchived:true}:mapping),sourceRetirements:[...(state.document.sourceRetirements??[]),receipt]};
 await journal({...state,document,storeVersion:current.store_version+1});const encoded=JSON.stringify(document),archive=current.archive_bytes-bytes(current.document_json)+bytes(encoded)+bytes(JSON.stringify(receipt));if(bytes(encoded)>1024*1024||archive>8*1024*1024)throw new WorkspaceError('workspace_capacity',413);
 const changes=await env.WORKSPACE_DB.batch([env.WORKSPACE_DB.prepare('UPDATE workspaces SET document_json=?,store_version=store_version+1,archive_bytes=?,commit_tag=?,updated_at_utc=?,retirement_fence=NULL WHERE profile_id=? AND connection_id=? AND access_epoch=? AND store_version=? AND retirement_fence=? AND active=1').bind(encoded,archive,stored.command_id,at,row.profile_id,row.connection_id,row.access_epoch,current.store_version,stored.command_id),env.WORKSPACE_DB.prepare("UPDATE workspace_source_retirements SET state='confirmed',receipt_json=?,error_code=NULL WHERE profile_id=? AND command_id=? AND EXISTS(SELECT 1 FROM workspaces w WHERE w.profile_id=? AND w.commit_tag=? AND w.retirement_fence IS NULL)").bind(JSON.stringify(receipt),row.profile_id,stored.command_id,row.profile_id,stored.command_id)]);
 if(!changes[0].meta.changes)throw new WorkspaceError('retirement_save_requires_recovery',409);
}
export async function sourceRetirementApi(env:WorkspaceEnv,initial:WorkspaceRow,name:RetirementTool,args:unknown,origin:TagOrigin=browserTagOrigin):Promise<Record<string,unknown>>{
 const input=retirementInput(name,args);let {row,state}=await load(env,initial);
 const read=(key:'command_id'|'request_id',id:string)=>env.WORKSPACE_DB.prepare('SELECT * FROM workspace_source_retirements WHERE profile_id=? AND '+key+'=?').bind(row.profile_id,id).first<Stored>();
 let stored:Stored|null;
 if(name==='prepare_source_retirement'){
  const hash=await workspaceTokenHash(JSON.stringify(input));stored=await read('request_id',String(input.requestId));if(stored){if(stored.input_hash!==hash)throw new WorkspaceError('request_id_reused',409);return view(stored);}
  if(input.expectedRevision!==state.document.revision)throw new WorkspaceError('stale_revision',409);
  const {plan,authority}=await prerequisites(env,row,state,String(input.conversionId),origin),encoded=JSON.stringify(plan),size=bytes(encoded)+16384,id=crypto.randomUUID(),commit=crypto.randomUUID();
  if(row.archive_bytes+size>8*1024*1024||bytes(row.document_json)+8192>1024*1024||row.store_version>=Number.MAX_SAFE_INTEGER)throw new WorkspaceError('workspace_capacity',413);
  const changes=await env.WORKSPACE_DB.batch([env.WORKSPACE_DB.prepare('UPDATE workspaces SET archive_bytes=archive_bytes+?,store_version=store_version+1,commit_tag=? WHERE profile_id=? AND connection_id=? AND access_epoch=? AND store_version=? AND retirement_fence IS NULL AND active=1').bind(size,commit,row.profile_id,row.connection_id,row.access_epoch,row.store_version),env.WORKSPACE_DB.prepare("INSERT INTO workspace_source_retirements(profile_id,command_id,request_id,input_hash,connection_id,access_epoch,grant_epoch,authority_json,plan_json,verification_json,state,created_at_utc,expires_at_utc) SELECT profile_id,?,?,?,?,?,?,?,?,?,'verifying',?,? FROM workspaces WHERE profile_id=? AND commit_tag=?").bind(id,input.requestId,hash,row.connection_id,row.access_epoch,row.mcp_grant_epoch??'legacy',JSON.stringify(authority),encoded,JSON.stringify({cursor:0,firstAt:null,sourceAbsent:false,sourceToken:null,verifiedAt:null,tagIds:{}}),now(),new Date(Date.now()+600000).toISOString(),row.profile_id,commit)]);
  if(!changes[0].meta.changes)throw new WorkspaceError('workspace_changed',409);stored=await read('request_id',String(input.requestId));if(!stored)throw new WorkspaceError('preparation_unavailable',503);return view(stored);
 }
 stored=await read('command_id',String(input.commandId));if(!stored)throw new WorkspaceError('retirement_command_unavailable',404);
 if(stored.receipt_json)return view(stored);
 const plan=JSON.parse(stored.plan_json) as RetirementPlan,v=JSON.parse(stored.verification_json) as Verification;
 if(name==='get_source_retirement_command'||name==='reconcile_source_retirement'){
  if(['executing','unknownOutcome','nativeConfirmed'].includes(stored.state))return tagReviewLease(env,row,async()=>{
   stored=(await read('command_id',String(input.commandId)))!;
   if(stored.receipt_json)return view(stored);
   if(stored.native_outcome_json){const outcome=JSON.parse(stored.native_outcome_json);await finalize(env,row,stored,outcome);return view((await read('command_id',stored.command_id))!);}
   if(!stored.prepared_request_json){if(name==='reconcile_source_retirement'){await requiredTagAuthority(env,row,origin);const observed=await gateway(env,row,'inspectRetirement',{selector:plan.sourceMapping.habiticaTaskId});if(!observed.task){await saveNative(stored,'alreadyAbsent');await finalize(env,row,stored,'alreadyAbsent');return view((await read('command_id',stored.command_id))!);}}return view(stored);}
   const original=JSON.parse(stored.prepared_request_json);let outcome:Outcome;
   if(name==='reconcile_source_retirement')await requiredTagAuthority(env,row,origin);
   try{outcome=await gateway(env,row,name==='reconcile_source_retirement'?'reconcile':'lookup',{operation:'retireSource',arguments:original.arguments},{attemptId:original.attemptId,attemptAtUtc:original.attemptAtUtc});}catch{return view(stored);}
   return finish(outcome,stored);
  });return view(stored);
 }
 if(['executing','unknownOutcome','nativeConfirmed','refused'].includes(stored.state))return view(stored);
 if(stored.connection_id!==row.connection_id||stored.access_epoch!==row.access_epoch||stored.grant_epoch!==(row.mcp_grant_epoch??'legacy')||stored.expires_at_utc<=now()||v.firstAt&&Date.parse(v.firstAt)+300000<Date.now())throw new WorkspaceError('retirement_review_expired_or_changed',409);
 return tagReviewLease(env,row,async()=>{
  const current=await load(env,row);row=current.row;state=current.state;
  if(stored!.grant_epoch!==(row.mcp_grant_epoch??'legacy'))throw new WorkspaceError('retirement_review_expired_or_changed',409);
  const fresh=await prerequisites(env,row,state,plan.conversionId,origin);if(!same(fresh.authority,JSON.parse(stored!.authority_json))||fresh.plan.contextHash!==plan.contextHash)throw new WorkspaceError('retirement_source_changed',409);
  if(name==='verify_source_retirement'){
   if(stored!.state==='ready')return view(stored!);
   const before=stored!.verification_json;
   if(v.cursor===0){
    const observed=await groupTagGateway(env,row,fresh.authority,{workspaceId:row.workspace_id,entityId:plan.project.id,kind:'project'},'inspectTags',{});
    if(observed.accountId!==fresh.authority.accountId||!Array.isArray(observed.tags))throw new WorkspaceError('tag_invalid_observation',502);
    for(const group of fresh.groups){const mapping=state.document.tagLedger!.mappings.find(x=>x.kind===group.kind&&x.entityId===group.entityId)!,native=(observed.tags as {id:string;name:string;native:boolean}[]).find(x=>x.id===mapping.habiticaTagId);if(!native?.native||native.name!==group.name)throw new WorkspaceError('required_native_tag_changed',409);for(const taskId of group.taskIds)if(plan.targets.some(x=>x.task.id===taskId))(v.tagIds[taskId]??=[]).push(native.id);}
    v.firstAt=now();
   }else{
    const target=v.cursor<=plan.targets.length?plan.targets[v.cursor-1]:null,task=target?.task??plan.source,mapping=target?.mapping??plan.sourceMapping,observed=await gateway(env,row,'inspectRetirement',{selector:mapping.habiticaTaskId});
    if(target){if(!observed.task||observed.task.id!==mapping.habiticaTaskId||!matches(observed.task,task,mapping.fingerprint!)||!Array.isArray(observed.task.tags)||!(v.tagIds[task.id]??[]).every(id=>observed.task!.tags!.includes(id)))throw new WorkspaceError('replacement_native_mismatch',409);}
    else {if(observed.task&&(!matches(observed.task,task,mapping.fingerprint!)||observed.task.id!==mapping.habiticaTaskId))throw new WorkspaceError('retirement_source_changed',409);v.sourceAbsent=!observed.task;v.sourceToken=observed.checkToken??null;v.verifiedAt=now();if(!v.sourceToken)throw new WorkspaceError('invalid_source_observation',502);}
   }
   v.cursor++;await currentTagRow(env,row);
   const changed=await env.WORKSPACE_DB.prepare("UPDATE workspace_source_retirements SET verification_json=?,state=? WHERE profile_id=? AND command_id=? AND state='verifying' AND verification_json=?").bind(JSON.stringify(v),v.cursor===plan.targets.length+2?'ready':'verifying',row.profile_id,stored!.command_id,before).run();if(!changed.meta.changes)throw new WorkspaceError('retirement_review_changed',409);return view((await read('command_id',stored!.command_id))!);
  }
  if(stored!.state!=='ready'||input.contextHash!==plan.contextHash||input.irreversibleApproved!==true||!v.verifiedAt)throw new WorkspaceError('fresh_exact_retirement_review_required',409);
  const latestSource=await gateway(env,row,'inspectRetirement',{selector:plan.sourceMapping.habiticaTaskId});
  if(v.sourceAbsent?Boolean(latestSource.task):!latestSource.task||latestSource.task.id!==plan.sourceMapping.habiticaTaskId||!matches(latestSource.task,plan.source,plan.sourceMapping.fingerprint!))throw new WorkspaceError('retirement_source_changed',409);
  if(!latestSource.checkToken)throw new WorkspaceError('invalid_source_observation',502);
  v.sourceToken=latestSource.checkToken;
  const prepared={attemptId:crypto.randomUUID(),attemptAtUtc:now(),expectedConsentEpoch:fresh.authority.consentEpoch,...(fresh.authority.source==='mcp'?{requiredGrantEpoch:fresh.authority.grantEpoch}:{}),arguments:{selector:plan.sourceMapping.habiticaTaskId,checkToken:v.sourceToken,expectedFingerprint:plan.sourceMapping.fingerprint,expectedCompleted:plan.source.state==='completed',workspaceId:plan.workspaceId,conversionId:plan.conversionId,sourceTaskId:plan.source.id,contextHash:plan.contextHash,irreversibleApproved:true}};
  const changes=await env.WORKSPACE_DB.batch([env.WORKSPACE_DB.prepare('UPDATE workspaces SET retirement_fence=?,store_version=store_version+1 WHERE profile_id=? AND connection_id=? AND access_epoch=? AND store_version=? AND retirement_fence IS NULL AND active=1').bind(stored!.command_id,row.profile_id,row.connection_id,row.access_epoch,row.store_version),env.WORKSPACE_DB.prepare("UPDATE workspace_source_retirements SET state='executing',prepared_request_json=? WHERE profile_id=? AND command_id=? AND state='ready' AND EXISTS(SELECT 1 FROM workspaces w WHERE w.profile_id=? AND w.retirement_fence=?)").bind(v.sourceAbsent?null:JSON.stringify(prepared),row.profile_id,stored!.command_id,row.profile_id,stored!.command_id)]);
  if(!changes[0].meta.changes||!changes[1].meta.changes)throw new WorkspaceError('workspace_changed',409);stored=(await read('command_id',stored!.command_id))!;
  if(v.sourceAbsent){try{const observed=await gateway(env,row,'inspectRetirement',{selector:plan.sourceMapping.habiticaTaskId});if(observed.task)return finish({kind:'refused',code:'source_reappeared'},stored);}catch(error){await release(stored,'source_absence_recheck_failed');throw error;}await saveNative(stored,'alreadyAbsent');await finalize(env,row,stored,'alreadyAbsent');return view((await read('command_id',stored.command_id))!);}
  let outcome:Outcome;try{outcome=await gateway(env,row,'retireSource',prepared.arguments,{attemptId:prepared.attemptId,attemptAtUtc:prepared.attemptAtUtc,expectedConsentEpoch:prepared.expectedConsentEpoch,...(prepared.requiredGrantEpoch?{requiredGrantEpoch:prepared.requiredGrantEpoch}:{})});}catch(error){if(error instanceof WorkspaceError&&error.status<500)outcome={kind:'refused',code:error.code};else outcome={kind:'unknown',code:'retirement_transport_unknown'};}
  return finish(outcome,stored);
 });
 async function release(work:Stored,code:string){await env.WORKSPACE_DB.batch([env.WORKSPACE_DB.prepare("UPDATE workspace_source_retirements SET state='refused',error_code=? WHERE profile_id=? AND command_id=?").bind(code,row.profile_id,work.command_id),env.WORKSPACE_DB.prepare('UPDATE workspaces SET retirement_fence=NULL WHERE profile_id=? AND retirement_fence=?').bind(row.profile_id,work.command_id)]);}
 async function saveNative(work:Stored,outcome:SourceRetirement['outcome']){await env.WORKSPACE_DB.prepare("UPDATE workspace_source_retirements SET state='nativeConfirmed',native_outcome_json=?,error_code=NULL WHERE profile_id=? AND command_id=?").bind(JSON.stringify(outcome),row.profile_id,work.command_id).run();}
 async function finish(outcome:Outcome,work:Stored){
  if(outcome.kind==='refused'){await release(work,outcome.code??'retirement_refused');return view((await read('command_id',work.command_id))!);}
  if(outcome.kind==='applied'&&['source_retired','retirement_absence_observed'].includes(outcome.code??'')&&outcome.task?.id===plan.sourceMapping.habiticaTaskId&&outcome.task.kind==='todo'&&outcome.task.fingerprint===plan.sourceMapping.fingerprint){const result=outcome.code==='source_retired'?'originalReceipt':'observedAbsentAfterUnknown';await saveNative(work,result);await finalize(env,row,work,result);return view((await read('command_id',work.command_id))!);}
  await env.WORKSPACE_DB.prepare("UPDATE workspace_source_retirements SET state='unknownOutcome',error_code='retirement_outcome_unknown' WHERE profile_id=? AND command_id=?").bind(row.profile_id,work.command_id).run();return view((await read('command_id',work.command_id))!);
 }
}
