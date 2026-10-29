import {linkedRecoveryInput,type LinkedRecoveryTool} from '../../shared/linked-recovery-tools.ts';
import {applyLinkedRecovery,linkedRecoveryPlan,type LinkedRecoveryPlan} from '../../shared/linked-recovery.ts';
import {sameCompletionScoreSource} from '../../shared/completion-score-source.ts';
import type {RecoveryReview} from '../../shared/linked-task-recovery.ts';
import {workspaceTokenHash,uuid,WorkspaceError} from '../../shared/workspace-http.ts';
import {journal,type Journal} from './journal.ts';
import {cloudAuthority,type WorkspaceRow} from './executor.ts';
import {tagReviewLease} from './group-tag-api.ts';
interface Stored {
 command_id:string;request_id:string;input_hash:string;connection_id:string;access_epoch:string;grant_epoch:string;account_id:string;
 plan_json:string;review_json:string;check_token:string;state:string;prepared_request_json:string|null;native_outcome_json:string|null;receipt_json:string|null;error_code:string|null;expires_at_utc:string;
}
interface Outcome {kind?:string;code?:string;verified?:boolean;attemptId?:string;sourceContext?:unknown;checkToken?:string;review?:RecoveryReview;recovery?:RecoveryReview;task?:{id:string;kind:string;fingerprint:string}}
const now=()=>new Date().toISOString(),bytes=(s:string)=>new TextEncoder().encode(s).length;
const disclosure='Uncheck this exact owned Habitica task once, then reopen its retained Companion completion. HP, MP, GP, XP and streak changes are unknown; no exact refund is promised. Daily recovery is limited to the current verified account day. Shared/challenge tasks, Cron, party management and schedule changes are excluded. External edits can race the final Habitica read. An unknown outcome never authorizes another score.';
async function load(env:WorkspaceEnv,initial:WorkspaceRow){
 const results=await env.WORKSPACE_DB.batch([
  env.WORKSPACE_DB.prepare('SELECT * FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1').bind(initial.profile_id,initial.connection_id,initial.access_epoch),
  env.WORKSPACE_DB.prepare('SELECT entry_json FROM workspace_commands WHERE profile_id=? ORDER BY sequence').bind(initial.profile_id),
  env.WORKSPACE_DB.prepare('SELECT event_json FROM workspace_events WHERE profile_id=? ORDER BY sequence').bind(initial.profile_id)]);
 const row=results[0].results[0] as WorkspaceRow|undefined;if(!row)throw new WorkspaceError('unauthorized',401);
 return {row,state:await journal({document:JSON.parse(row.document_json),storeVersion:row.store_version,authority:row.authority_json?JSON.parse(row.authority_json):cloudAuthority(row.workspace_id),commands:results[1].results.map(x=>JSON.parse(String((x as {entry_json:string}).entry_json))),events:results[2].results.map(x=>JSON.parse(String((x as {event_json:string}).event_json)))})};
}
async function authority(env:WorkspaceEnv,row:WorkspaceRow){
 const response=await env.AUTH_IDENTITY.fetch('https://identity.internal/task-recovery-authority',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({profileId:row.profile_id,connectionId:row.connection_id}),signal:AbortSignal.timeout(5000)});
 if(!response.ok)throw new WorkspaceError('credential_repair_required',401);
 const value=await response.json<{profileId:string;connectionId:string;accountId:string;grantEpoch:string}>();
 if(value.profileId!==row.profile_id||value.connectionId!==row.connection_id||typeof value.grantEpoch!=='string')throw new WorkspaceError('recovery_authority_unavailable',502);
 return {...value,accountId:uuid(value.accountId)};
}
async function gateway(env:WorkspaceEnv,row:WorkspaceRow,operation:string,args:unknown,extra:Record<string,unknown>={}):Promise<Outcome>{
 const response=await env.AUTH_PROJECTION.fetch('https://projection.internal/operation',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({profileId:row.profile_id,connectionId:row.connection_id,operation,arguments:args,automated:false,...extra}),signal:AbortSignal.timeout(operation==='recoverTask'?35000:30000)});
 const value=await response.json<Outcome>();
 if(!response.ok&&operation!=='recoverTask'&&operation!=='lookup')throw new WorkspaceError(value.code??'recovery_gateway_unavailable',response.status);
 if(operation==='lookup'&&!response.ok)return {kind:'unknown',code:'receipt_unavailable'};
 return value;
}
async function prerequisites(env:WorkspaceEnv,row:WorkspaceRow,state:Journal,taskId:string,completionId:string,expectedRevision?:number){
 if((state.document.linkedTaskRecoveries?.length??0)>=1000||state.document.revision>=Number.MAX_SAFE_INTEGER||row.store_version>Number.MAX_SAFE_INTEGER-3)throw new WorkspaceError('workspace_capacity',413);
 if(!env.EXECUTOR_ENABLED||!row.executor_enabled)throw new WorkspaceError('executor_consent_required',403);
 if(row.retirement_fence||row.linked_recovery_fence)throw new WorkspaceError('unresolved_gameplay_pending',409);
 const busy=await env.WORKSPACE_DB.prepare("SELECT 1 FROM workspace_tag_work WHERE profile_id=?1 AND state IN ('pending','executing','unknownOutcome') UNION ALL SELECT 1 FROM prepared_cron_actions WHERE profile_id=?1 AND status IN ('accepted','dispatching','unknownOutcome') UNION ALL SELECT 1 FROM prepared_spell_actions WHERE profile_id=?1 AND status IN ('accepted','dispatching','unknownOutcome') LIMIT 1").bind(row.profile_id).first();
 if(busy)throw new WorkspaceError('unresolved_gameplay_pending',409);
 const who=await authority(env,row),plan=await linkedRecoveryPlan(state,taskId,completionId,who.accountId,expectedRevision);
 const proof=await gateway(env,row,'scoreReceipt',{attemptId:plan.sourceAttemptId,selector:plan.mapping.habiticaTaskId,taskKind:plan.task.kind,expectedFingerprint:plan.mapping.fingerprint,sourceContext:plan.sourceContext});
 if(proof.verified!==true||proof.attemptId!==plan.sourceAttemptId||proof.task?.id!==plan.mapping.habiticaTaskId||proof.task?.fingerprint!==plan.mapping.fingerprint||proof.task?.kind!==plan.task.kind||!sameCompletionScoreSource(proof.sourceContext,plan.sourceContext))throw new WorkspaceError('original_score_proof_unavailable',409);
 return {plan,who};
}
function validReview(review:RecoveryReview|undefined,plan:LinkedRecoveryPlan):review is RecoveryReview{
 return Boolean(review&&review.contract==='own-task-recovery-v1'&&review.contextHash===plan.contextHash&&review.taskId===plan.mapping.habiticaTaskId&&review.taskKind===plan.task.kind&&review.fingerprint===plan.mapping.fingerprint&&review.occurrenceDate===plan.sourceContext.occurrenceDate&&/^[a-f0-9]{64}$/.test(review.reviewHash)&&/^\d{4}-\d{2}-\d{2}$/.test(review.accountDay)&&Object.entries(review.resourceChanges??{}).length===5&&['hp','mp','gp','xp','streak'].every(key=>review.resourceChanges[key as keyof RecoveryReview['resourceChanges']]==='unknown'));
}
function view(stored:Stored){
 const plan=JSON.parse(stored.plan_json) as LinkedRecoveryPlan;
 return {commandId:stored.command_id,requestId:stored.request_id,workspaceId:plan.workspaceId,revision:plan.reviewedRevision,task:plan.task,mapping:plan.mapping,completion:plan.completion,sourceContext:plan.sourceContext,sourceAttemptId:plan.sourceAttemptId,accountId:plan.accountId,contextHash:plan.contextHash,review:JSON.parse(stored.review_json) as RecoveryReview,state:stored.state,expiresAtUtc:stored.expires_at_utc,expired:stored.expires_at_utc<=now(),outcomeUnknown:['executing','unknownOutcome','nativeConfirmed'].includes(stored.state),savedInCompanion:stored.receipt_json?true:null,receipt:stored.receipt_json?JSON.parse(stored.receipt_json):null,error:stored.error_code,disclosure};
}
async function finalize(env:WorkspaceEnv,row:WorkspaceRow,stored:Stored){
 const current=await load(env,row),plan=JSON.parse(stored.plan_json) as LinkedRecoveryPlan,review=JSON.parse(stored.review_json) as RecoveryReview;
 if(current.state.document.linkedTaskRecoveries?.some(x=>x.id===stored.command_id))return;
 if(current.row.linked_recovery_fence!==stored.command_id||!stored.native_outcome_json)throw new WorkspaceError('recovery_save_requires_receipt',409);
 const document=await applyLinkedRecovery(current.state,plan,stored.command_id,review.reviewHash,now());
 await journal({...current.state,document,storeVersion:current.row.store_version+1});
 const receipt=document.linkedTaskRecoveries!.at(-1)!,encoded=JSON.stringify(document),archive=current.row.archive_bytes-bytes(current.row.document_json)+bytes(encoded)+bytes(JSON.stringify(receipt));
 if(bytes(encoded)>1024*1024||archive>8*1024*1024||current.row.store_version>=Number.MAX_SAFE_INTEGER)throw new WorkspaceError('workspace_capacity',413);
 const result=await env.WORKSPACE_DB.batch([
  env.WORKSPACE_DB.prepare('UPDATE workspaces SET document_json=?,store_version=store_version+1,archive_bytes=?,commit_tag=?,updated_at_utc=?,linked_recovery_fence=NULL WHERE profile_id=? AND connection_id=? AND access_epoch=? AND store_version=? AND linked_recovery_fence=? AND active=1').bind(encoded,archive,stored.command_id,now(),row.profile_id,row.connection_id,row.access_epoch,current.row.store_version,stored.command_id),
  env.WORKSPACE_DB.prepare("UPDATE workspace_linked_recoveries SET state='confirmed',receipt_json=?,error_code=NULL,check_token='' WHERE profile_id=? AND command_id=? AND EXISTS(SELECT 1 FROM workspaces WHERE profile_id=? AND commit_tag=? AND linked_recovery_fence IS NULL)").bind(JSON.stringify(receipt),row.profile_id,stored.command_id,row.profile_id,stored.command_id)]);
 if(!result[0].meta.changes||!result[1].meta.changes)throw new WorkspaceError('recovery_save_requires_recovery',409);
}
export async function linkedRecoveryApi(env:WorkspaceEnv,initial:WorkspaceRow,name:LinkedRecoveryTool,args:unknown):Promise<Record<string,unknown>>{
 const input=linkedRecoveryInput(name,args);let {row,state}=await load(env,initial);
 const read=(key:'request_id'|'command_id',id:string)=>env.WORKSPACE_DB.prepare('SELECT * FROM workspace_linked_recoveries WHERE profile_id=? AND '+key+'=?').bind(row.profile_id,id).first<Stored>();
 if(name==='get_linked_task_recovery'){
  const original=await env.WORKSPACE_DB.prepare("SELECT * FROM workspace_linked_recoveries WHERE profile_id=? AND json_extract(plan_json,'$.task.id')=? AND json_extract(plan_json,'$.completion.id')=? AND (state IN ('executing','unknownOutcome','nativeConfirmed') OR state='ready' AND expires_at_utc>?) ORDER BY created_at_utc DESC,command_id DESC LIMIT 1").bind(row.profile_id,input.taskId,input.completionId,now()).first<Stored>();
  if(original)return {workspaceId:row.workspace_id,revision:state.document.revision,taskId:input.taskId,completionId:input.completionId,available:false,reason:'original_recovery_pending',originalCommand:view(original),disclosure};
  try{return await tagReviewLease(env,row,async()=>{({row,state}=await load(env,row));const {plan}=await prerequisites(env,row,state,String(input.taskId),String(input.completionId));return {workspaceId:row.workspace_id,revision:state.document.revision,taskId:input.taskId,completionId:input.completionId,available:true,reason:null,plan,disclosure:disclosure+' Availability covers canonical state and the original score receipt; prepare a fresh review to verify current native eligibility.'};});}
  catch(error){if(!(error instanceof WorkspaceError))throw error;return {workspaceId:row.workspace_id,revision:state.document.revision,taskId:input.taskId,completionId:input.completionId,available:false,reason:error.code,disclosure};}
 }
 if(name==='prepare_linked_task_recovery'){
  const hash=await workspaceTokenHash(JSON.stringify(input)),existing=await read('request_id',String(input.requestId));
  if(existing){if(existing.input_hash!==hash)throw new WorkspaceError('request_id_reused',409);return view(existing);}
  return tagReviewLease(env,row,async()=>{
   ({row,state}=await load(env,row));const duplicate=await read('request_id',String(input.requestId));if(duplicate){if(duplicate.input_hash!==hash)throw new WorkspaceError('request_id_reused',409);return view(duplicate);}
   const {plan,who}=await prerequisites(env,row,state,String(input.taskId),String(input.completionId),Number(input.expectedRevision));
   const observed=await gateway(env,row,'inspectRecovery',{selector:plan.mapping.habiticaTaskId,taskKind:plan.task.kind,occurrenceDate:plan.sourceContext.occurrenceDate,contextHash:plan.contextHash},{requiredGrantEpoch:who.grantEpoch});
   if(!validReview(observed.review,plan)||typeof observed.checkToken!=='string'||observed.task?.id!==plan.mapping.habiticaTaskId||observed.task?.fingerprint!==plan.mapping.fingerprint)throw new WorkspaceError('invalid_recovery_review',502);
   const encoded=JSON.stringify(plan),review=JSON.stringify(observed.review),size=bytes(encoded)+bytes(review)+16384,id=crypto.randomUUID(),commit=crypto.randomUUID();
   if(row.archive_bytes+size>8*1024*1024||bytes(row.document_json)+16384>1024*1024||row.store_version>=Number.MAX_SAFE_INTEGER)throw new WorkspaceError('workspace_capacity',413);
   const result=await env.WORKSPACE_DB.batch([
    env.WORKSPACE_DB.prepare('UPDATE workspaces SET archive_bytes=archive_bytes+?,store_version=store_version+1,commit_tag=? WHERE profile_id=? AND connection_id=? AND access_epoch=? AND store_version=? AND retirement_fence IS NULL AND linked_recovery_fence IS NULL AND executor_enabled=1 AND active=1').bind(size,commit,row.profile_id,row.connection_id,row.access_epoch,row.store_version),
    env.WORKSPACE_DB.prepare("INSERT INTO workspace_linked_recoveries(profile_id,command_id,request_id,input_hash,connection_id,access_epoch,grant_epoch,account_id,plan_json,review_json,check_token,state,created_at_utc,expires_at_utc) SELECT profile_id,?,?,?,?,?,?,?,?,?,?,'ready',?,? FROM workspaces WHERE profile_id=? AND commit_tag=?").bind(id,input.requestId,hash,row.connection_id,row.access_epoch,who.grantEpoch,who.accountId,encoded,review,observed.checkToken,now(),new Date(Date.now()+60000).toISOString(),row.profile_id,commit)]);
   if(!result[0].meta.changes||!result[1].meta.changes)throw new WorkspaceError('workspace_changed',409);return view((await read('command_id',id))!);
  });
 }
 let stored=await read('command_id',String(input.commandId));if(!stored)throw new WorkspaceError('recovery_command_unavailable',404);
 if(stored.receipt_json)return view(stored);
 // A saved original attempt is never sent again, including after lease expiry, reconnect or lost acknowledgement.
 if(name==='get_linked_task_recovery_command'||stored.state!=='ready'){
  if(!['executing','unknownOutcome','nativeConfirmed'].includes(stored.state))return view(stored);
  return tagReviewLease(env,row,async()=>{
   stored=(await read('command_id',String(input.commandId)))!;if(stored.receipt_json)return view(stored);
   if(stored.connection_id!==row.connection_id||stored.access_epoch!==row.access_epoch)return view(stored);
   const who=await authority(env,row);if(who.accountId!==stored.account_id)return view(stored);
   if(stored.native_outcome_json){await finalize(env,row,stored);return view((await read('command_id',stored.command_id))!);}
   if(!stored.prepared_request_json)return view(stored);
   const original=JSON.parse(stored.prepared_request_json);let outcome:Outcome;
   try{outcome=await gateway(env,row,'lookup',{operation:'recoverTask',arguments:original.arguments},{attemptId:original.attemptId,attemptAtUtc:original.attemptAtUtc});}catch{return view(stored);}
   return finish(stored,outcome);
  });
 }
 return tagReviewLease(env,row,async()=>{
  ({row,state}=await load(env,row));stored=(await read('command_id',String(input.commandId)))!;
  if(stored.state!=='ready')return view(stored);
  const plan=JSON.parse(stored.plan_json) as LinkedRecoveryPlan,review=JSON.parse(stored.review_json) as RecoveryReview;
  if(stored.connection_id!==row.connection_id||stored.access_epoch!==row.access_epoch||stored.expires_at_utc<=now())throw new WorkspaceError('recovery_review_expired_or_changed',409);
  if(input.contextHash!==plan.contextHash||input.reviewHash!==review.reviewHash)throw new WorkspaceError('exact_recovery_review_required',409);
  const fresh=await prerequisites(env,row,state,plan.task.id,plan.completion.id,plan.reviewedRevision);
  if(fresh.plan.contextHash!==plan.contextHash||fresh.who.accountId!==stored.account_id||fresh.who.grantEpoch!==stored.grant_epoch)throw new WorkspaceError('recovery_review_expired_or_changed',409);
  const prepared={attemptId:stored.command_id,attemptAtUtc:now(),requiredGrantEpoch:stored.grant_epoch,arguments:{selector:plan.mapping.habiticaTaskId,checkToken:stored.check_token,expectedFingerprint:plan.mapping.fingerprint,taskKind:plan.task.kind,occurrenceDate:plan.sourceContext.occurrenceDate,contextHash:plan.contextHash}};
  const reservedAt=now();
  const result=await env.WORKSPACE_DB.batch([
   env.WORKSPACE_DB.prepare(`UPDATE workspaces SET linked_recovery_fence=?,store_version=store_version+1 WHERE profile_id=? AND connection_id=? AND access_epoch=? AND store_version=? AND linked_recovery_fence IS NULL AND retirement_fence IS NULL AND executor_enabled=1 AND active=1 AND EXISTS(SELECT 1 FROM workspace_linked_recoveries WHERE profile_id=? AND command_id=? AND state='ready' AND expires_at_utc>?)`).bind(stored.command_id,row.profile_id,row.connection_id,row.access_epoch,row.store_version,row.profile_id,stored.command_id,reservedAt),
   env.WORKSPACE_DB.prepare("UPDATE workspace_linked_recoveries SET state='executing',prepared_request_json=? WHERE profile_id=? AND command_id=? AND state='ready' AND expires_at_utc>? AND EXISTS(SELECT 1 FROM workspaces WHERE profile_id=? AND linked_recovery_fence=?)").bind(JSON.stringify(prepared),row.profile_id,stored.command_id,reservedAt,row.profile_id,stored.command_id)]);
  if(!result[0].meta.changes||!result[1].meta.changes)throw new WorkspaceError('workspace_changed',409);
  stored=(await read('command_id',stored.command_id))!;
  const dispatchGate=await env.WORKSPACE_DB.prepare('SELECT executor_enabled FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1 AND linked_recovery_fence=?').bind(row.profile_id,row.connection_id,row.access_epoch,stored.command_id).first<{executor_enabled:number}>();
  if(!dispatchGate?.executor_enabled)return finish(stored,{kind:'refused',code:'executor_consent_required'});
  let outcome:Outcome;try{outcome=await gateway(env,row,'recoverTask',prepared.arguments,{attemptId:prepared.attemptId,attemptAtUtc:prepared.attemptAtUtc,requiredGrantEpoch:prepared.requiredGrantEpoch});}catch{outcome={kind:'unknown',code:'transport_unknown'};}
  return finish(stored,outcome);
 });
 async function finish(work:Stored,outcome:Outcome):Promise<Record<string,unknown>>{
  const plan=JSON.parse(work.plan_json) as LinkedRecoveryPlan,review=JSON.parse(work.review_json) as RecoveryReview;
  if(outcome.kind==='refused'){
   await env.WORKSPACE_DB.batch([
    env.WORKSPACE_DB.prepare("UPDATE workspace_linked_recoveries SET state='refused',error_code=?,check_token='' WHERE profile_id=? AND command_id=? AND state IN ('executing','unknownOutcome')").bind(outcome.code??'recovery_refused',row.profile_id,work.command_id),
    env.WORKSPACE_DB.prepare("UPDATE workspaces SET linked_recovery_fence=NULL WHERE profile_id=? AND linked_recovery_fence=? AND EXISTS(SELECT 1 FROM workspace_linked_recoveries WHERE profile_id=? AND command_id=? AND state='refused')").bind(row.profile_id,work.command_id,row.profile_id,work.command_id)]);
  }else if(outcome.kind==='applied'&&outcome.code==='task_recovered'&&outcome.task?.id===plan.mapping.habiticaTaskId&&outcome.task?.kind===plan.task.kind&&outcome.task?.fingerprint===plan.mapping.fingerprint&&validReview(outcome.recovery,plan)&&outcome.recovery.reviewHash===review.reviewHash){
   await env.WORKSPACE_DB.prepare("UPDATE workspace_linked_recoveries SET state='nativeConfirmed',native_outcome_json=?,error_code=NULL WHERE profile_id=? AND command_id=? AND state IN ('executing','unknownOutcome')").bind(JSON.stringify(outcome),row.profile_id,work.command_id).run();
   const saved=(await read('command_id',work.command_id))!;await finalize(env,row,saved);
  }else await env.WORKSPACE_DB.prepare("UPDATE workspace_linked_recoveries SET state='unknownOutcome',error_code=? WHERE profile_id=? AND command_id=? AND state='executing'").bind(outcome.code??'outcome_unknown',row.profile_id,work.command_id).run();
  return view((await read('command_id',work.command_id))!);
 }
}
