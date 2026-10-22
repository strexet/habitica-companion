import {activeProfile,credentialKeys,boundedJson,VaultError} from './vault';
import {decryptCredential} from './cryptography';
import {upstreamBudget,upstreamFetch,observeUpstream,TrafficLimit} from './traffic';
import {rateState} from './projection';
import {requireTagConsent} from './tag-consent';
import {tagContext,tagName,nativeTags,tagsFingerprint,taggedTask,tagAttemptHash,type TagGatewayContext,type NativeTag} from '../../shared/tag-gateway';
import {workspaceObject as object,workspaceJson,uuid,workspaceTokenHash,workspaceSessionToken,WorkspaceError} from '../../shared/workspace-http';
interface Who {profileId:string;connectionId:string}
interface TagCheck {token_hash:string;payload_json:string}
interface TagOutcome {kind:'applied'|'refused'|'unknown';code:string;tag?:{id:string;name:string;fingerprint:string};task?:{id:string;kind:string;fingerprint:string};retryAtUtc?:string;receiptId?:string;confirmedAtUtc?:string}
interface OwnedTag {tag_id:string;receipt_id:string}
const headers={'cache-control':'no-store'};
const json=(value:unknown,status=200)=>Response.json(value,{status,headers});
const writes=['createTag','adoptTag','renameTag','setMembership'];
function fingerprint(value:unknown):string{if(typeof value!=='string'||!/^[a-f0-9]{64}$/.test(value))throw new WorkspaceError('invalid_precondition',422);return value;}
async function owned(env:AuthEnv,who:Who,context:TagGatewayContext,tagId:string):Promise<OwnedTag>{
 const row=await env.HABITICA_INTEGRATION_DB.prepare('SELECT tag_id,receipt_id FROM integration_owned_tags WHERE profile_id=? AND connection_id=? AND workspace_id=? AND entity_id=? AND kind=? AND tag_id=?').bind(who.profileId,who.connectionId,context.workspaceId,context.entityId,context.kind,tagId).first<OwnedTag>();
 if(!row)throw new WorkspaceError('tag_not_adopted',409);return row;
}
async function readCheck(env:AuthEnv,who:Who,epoch:string,contextHash:string,token:unknown):Promise<TagCheck>{
 if(typeof token!=='string'||!/^[A-Za-z0-9_-]{43}$/.test(token))throw new WorkspaceError('precondition_required',409);
 const row=await env.HABITICA_INTEGRATION_DB.prepare('SELECT token_hash,payload_json FROM integration_tag_checks WHERE token_hash=? AND profile_id=? AND connection_id=? AND consent_epoch=? AND context_hash=? AND expires_at_utc>?').bind(await workspaceTokenHash(token),who.profileId,who.connectionId,epoch,contextHash,new Date().toISOString()).first<TagCheck>();
 if(!row)throw new WorkspaceError('precondition_expired',409);return row;
}
async function remember(env:AuthEnv,who:Who,epoch:string,contextHash:string,payload:unknown){
 const encoded=JSON.stringify(payload);if(new TextEncoder().encode(encoded).length>262144)throw new WorkspaceError('tag_check_capacity',413);
 const token=workspaceSessionToken();await env.HABITICA_INTEGRATION_DB.prepare('INSERT INTO integration_tag_checks(token_hash,profile_id,connection_id,consent_epoch,context_hash,payload_json,expires_at_utc) VALUES(?,?,?,?,?,?,?)').bind(await workspaceTokenHash(token),who.profileId,who.connectionId,epoch,contextHash,encoded,new Date(Date.now()+90000).toISOString()).run();return token;
}
async function register(env:AuthEnv,who:Who,context:TagGatewayContext,tag:{id:string;fingerprint:string},receiptId:string,consentEpoch:string){
 const result=await env.HABITICA_INTEGRATION_DB.prepare(`INSERT INTO integration_owned_tags(profile_id,connection_id,workspace_id,entity_id,kind,tag_id,receipt_id,fingerprint,verified_at_utc)
 SELECT ?,?,?,?,?,?,?,?,? WHERE EXISTS(SELECT 1 FROM integration_tag_consents WHERE profile_id=? AND connection_id=? AND consent_epoch=? AND revoked_at_utc IS NULL)
 AND EXISTS(SELECT 1 FROM integration_profiles WHERE profile_id=? AND connection_id=? AND revoked_at_utc IS NULL)
 ON CONFLICT(profile_id,connection_id,workspace_id,entity_id,kind) DO UPDATE SET receipt_id=excluded.receipt_id,fingerprint=excluded.fingerprint,verified_at_utc=excluded.verified_at_utc WHERE integration_owned_tags.tag_id=excluded.tag_id`).bind(who.profileId,who.connectionId,context.workspaceId,context.entityId,context.kind,tag.id,receiptId,tag.fingerprint,new Date().toISOString(),who.profileId,who.connectionId,consentEpoch,who.profileId,who.connectionId).run();
 if(!result.meta.changes)throw new WorkspaceError('tag_identity_conflict',409);
}
export async function tagOperation(request:Request,env:AuthEnv):Promise<Response>{
 let who:Who|undefined,attemptId:string|undefined,lease:string|undefined,reserved=false;
 try{
  if(!env.WORKSPACE_ENABLED||!env.PROJECTION_ENABLED||request.method!=='POST')throw new WorkspaceError('unauthorized',401);
  const input=object(await workspaceJson(request,65536),['profileId','connectionId','context','operation','arguments','attemptId','attemptAtUtc','automated','expectedConsentEpoch','requiredGrantEpoch']);
  if(typeof input.profileId!=='string'||!/^prf_[A-Za-z0-9_-]{43}$/.test(input.profileId)||typeof input.automated!=='boolean')throw new WorkspaceError('invalid_request',422);
  who={profileId:input.profileId,connectionId:uuid(input.connectionId)};const profile=await activeProfile(env,who.profileId,who.connectionId);
  const context=tagContext(input.context),contextHash=await workspaceTokenHash(JSON.stringify(context)),operation=input.operation;
  if(typeof operation!=='string'||!['inspectTags','inspectMembership','lookupTagAttempt','prepareTagRecovery','acceptTagRecovery',...writes].includes(operation))throw new WorkspaceError('invalid_operation',422);
  if(operation==='prepareTagRecovery'||operation==='acceptTagRecovery')return await recoverTagAttempt(env,who,context,contextHash,input,operation);
  const args=object(input.arguments,operation==='inspectTags'?[]:operation==='inspectMembership'?['tagId','taskId','tagCheckToken']:operation==='lookupTagAttempt'?['operation','arguments']:operation==='createTag'||operation==='renameTag'?['tagId','name','checkToken','expectedFingerprint']:operation==='adoptTag'?['tagId','checkToken','expectedFingerprint']:['tagId','taskId','present','checkToken','expectedFingerprint']);
  const writing=writes.includes(operation);let payloadHash:string|undefined;
  if(writing||operation==='lookupTagAttempt'){
   attemptId=uuid(input.attemptId);const originalOperation=operation==='lookupTagAttempt'?String(args.operation):operation;
   if(!writes.includes(originalOperation))throw new WorkspaceError('invalid_operation',422);
   payloadHash=await tagAttemptHash('tags.'+originalOperation,{context,arguments:operation==='lookupTagAttempt'?object(args.arguments):args},input.attemptAtUtc);
   const prior=await env.HABITICA_INTEGRATION_DB.prepare('SELECT payload_hash,outcome_json FROM integration_projection_attempts WHERE profile_id=? AND connection_id=? AND attempt_id=?').bind(who.profileId,who.connectionId,attemptId).first<{payload_hash:string;outcome_json:string|null}>();
   if(prior){if(prior.payload_hash!==payloadHash)throw new WorkspaceError('attempt_id_reused',409);return json(prior.outcome_json?JSON.parse(prior.outcome_json):{kind:'unknown',code:'outcome_unknown'});}
   if(operation==='lookupTagAttempt')return json({kind:'unknown',code:'attempt_not_found'});
   const at=typeof input.attemptAtUtc==='string'?Date.parse(input.attemptAtUtc):NaN;
   if(!Number.isFinite(at)||at<Date.now()-120000||at>Date.now()+5000)throw new WorkspaceError('attempt_expired',409);
   const unresolved=await env.HABITICA_INTEGRATION_DB.prepare("SELECT attempt_id FROM integration_projection_attempts WHERE profile_id=? AND connection_id=? AND state IN ('dispatched','unknown') LIMIT 1").bind(who.profileId,who.connectionId).first();
   if(unresolved)throw new WorkspaceError('unknown_outcome_pending',409);
  }else if(input.attemptId!==undefined||input.attemptAtUtc!==undefined)throw new WorkspaceError('invalid_request',422);
  const consent=await requireTagConsent(env,who.profileId,who.connectionId);
  if(input.expectedConsentEpoch!==undefined&&input.expectedConsentEpoch!==consent.consent_epoch||input.requiredGrantEpoch!==undefined&&input.requiredGrantEpoch!==profile.grant_epoch)throw new WorkspaceError('tag_authority_changed',409);
  const quota=await env.HABITICA_INTEGRATION_DB.prepare('SELECT blocked_until_utc,credential_repair_required FROM integration_read_state WHERE profile_id=? AND connection_id=?').bind(who.profileId,who.connectionId).first<{blocked_until_utc:string|null;credential_repair_required:number}>();
  if(quota?.credential_repair_required)throw new WorkspaceError('credential_repair_required',401);
  if(quota?.blocked_until_utc&&Date.parse(quota.blocked_until_utc)>Date.now())return json({kind:'refused',code:'rate_limited',retryAtUtc:quota.blocked_until_utc},429);
  if(profile.credential_schema_version!==1||!env.HABITICA_X_CLIENT_HEADER)throw new WorkspaceError('service_unavailable',503);
  const credential=await decryptCredential(who.profileId,{ciphertext:profile.credential_ciphertext!,iv:profile.credential_iv!,keyVersion:profile.credential_key_version!,schemaVersion:1},credentialKeys(env));
  let tagId:string|undefined,taskId:string|undefined,checked:TagCheck|undefined,observed:Record<string,unknown>|undefined,tagNameValue:string|undefined;
  if(operation!=='inspectTags')tagId=uuid(args.tagId);
  if(operation==='inspectMembership'||operation==='setMembership')taskId=uuid(args.taskId);
  if(operation==='createTag'||operation==='renameTag')tagNameValue=tagName(args.name);
  if(operation==='inspectMembership'||operation==='renameTag'||operation==='setMembership')await owned(env,who,context,tagId!);
  if(operation==='inspectMembership'){
   const tagCheck=await readCheck(env,who,consent.consent_epoch,contextHash,args.tagCheckToken),payload=object(JSON.parse(tagCheck.payload_json));
   if(payload.type!=='tags'||!Array.isArray(payload.tags)||!payload.tags.some((tag:NativeTag)=>tag.id===tagId&&tag.native))throw new WorkspaceError('tag_not_owned',409);
  }
  if(writing){
   checked=await readCheck(env,who,consent.consent_epoch,contextHash,args.checkToken);observed=object(JSON.parse(checked.payload_json));
   if(observed.fingerprint!==fingerprint(args.expectedFingerprint))throw new WorkspaceError('external_conflict',409);
   if(operation==='setMembership'){
    if(observed.type!=='membership'||observed.tagId!==tagId||observed.taskId!==taskId||typeof args.present!=='boolean')throw new WorkspaceError('precondition_mismatch',409);
   }else{
    if(observed.type!=='tags'||!Array.isArray(observed.tags))throw new WorkspaceError('precondition_mismatch',409);
    const tag=(observed.tags as NativeTag[]).find(tag=>tag.id===tagId);
    if(operation==='createTag'?Boolean(tag):!tag?.native)throw new WorkspaceError('tag_not_owned',409);

   }
   lease=crypto.randomUUID();const now=new Date().toISOString();
   const acquired=await env.HABITICA_INTEGRATION_DB.prepare(`INSERT INTO integration_projection_state(profile_id,connection_id,lease_token,lease_until_utc,next_automated_at_utc) VALUES(?,?,?,?,?)
    ON CONFLICT(profile_id,connection_id) DO UPDATE SET lease_token=excluded.lease_token,lease_until_utc=excluded.lease_until_utc,next_automated_at_utc=excluded.next_automated_at_utc
    WHERE (integration_projection_state.lease_until_utc IS NULL OR integration_projection_state.lease_until_utc<=?) AND (?=0 OR integration_projection_state.next_automated_at_utc IS NULL OR integration_projection_state.next_automated_at_utc<=?)`).bind(who.profileId,who.connectionId,lease,new Date(Date.now()+15000).toISOString(),new Date(Date.now()+30000).toISOString(),now,Number(input.automated),now).run();
   if(!acquired.meta.changes)throw new WorkspaceError('projection_busy',429);
   if(operation!=='setMembership'){
    const conflict=await env.HABITICA_INTEGRATION_DB.prepare('SELECT tag_id FROM integration_owned_tags WHERE profile_id=? AND connection_id=? AND ((workspace_id=? AND entity_id=? AND kind=? AND tag_id<>?) OR (tag_id=? AND (workspace_id<>? OR entity_id<>? OR kind<>?)))').bind(who.profileId,who.connectionId,context.workspaceId,context.entityId,context.kind,tagId!,tagId!,context.workspaceId,context.entityId,context.kind).first();
    if(conflict)throw new WorkspaceError('tag_identity_conflict',409);
   }
   if(operation==='renameTag'||operation==='setMembership')await owned(env,who,context,tagId!);
   if(operation!=='adoptTag')await upstreamBudget(env,who.profileId);
   const batch=await env.HABITICA_INTEGRATION_DB.batch([
    env.HABITICA_INTEGRATION_DB.prepare(`INSERT INTO integration_projection_attempts(profile_id,connection_id,attempt_id,payload_hash,operation,state,created_at_utc)
     SELECT ?,?,?,?,?,'dispatched',? FROM integration_tag_checks c JOIN integration_tag_consents s ON s.profile_id=c.profile_id AND s.connection_id=c.connection_id AND s.consent_epoch=c.consent_epoch
     WHERE c.token_hash=? AND c.profile_id=? AND c.connection_id=? AND c.consent_epoch=? AND c.context_hash=? AND c.expires_at_utc>? AND s.revoked_at_utc IS NULL`).bind(who.profileId,who.connectionId,attemptId!,payloadHash!,'tags.'+operation,now,checked!.token_hash,who.profileId,who.connectionId,consent.consent_epoch,contextHash,now),
    env.HABITICA_INTEGRATION_DB.prepare(`INSERT INTO integration_tag_attempt_details(profile_id,connection_id,attempt_id,observation_json,created_at_utc) SELECT ?,?,?,payload_json,? FROM integration_tag_checks WHERE token_hash=? AND EXISTS(SELECT 1 FROM integration_projection_attempts WHERE profile_id=? AND connection_id=? AND attempt_id=?)`).bind(who.profileId,who.connectionId,attemptId!,now,checked!.token_hash,who.profileId,who.connectionId,attemptId!),
    env.HABITICA_INTEGRATION_DB.prepare('DELETE FROM integration_tag_checks WHERE token_hash=? AND EXISTS(SELECT 1 FROM integration_projection_attempts WHERE profile_id=? AND connection_id=? AND attempt_id=?)').bind(checked!.token_hash,who.profileId,who.connectionId,attemptId!),
   ]);
   if(!batch[0].meta.changes)throw new WorkspaceError('precondition_consumed',409);reserved=true;
  }else await upstreamBudget(env,who.profileId);
  await activeProfile(env,who.profileId,who.connectionId);
  const finalConsent=await requireTagConsent(env,who.profileId,who.connectionId);if(finalConsent.consent_epoch!==consent.consent_epoch)throw new WorkspaceError('tag_consent_changed',409);
  let outcome:TagOutcome;
  if(operation==='setMembership'&&(observed!.tags as string[]).includes(tagId!)===args.present){outcome={kind:'applied',code:'already_matches',task:{id:taskId!,kind:String(observed!.kind),fingerprint:String(observed!.fingerprint)},receiptId:attemptId};}
  else if(operation==='adoptTag'){
   const tag=(observed!.tags as NativeTag[]).find(tag=>tag.id===tagId)!;outcome={kind:'applied',code:'adopted',tag:{id:tag.id,name:tag.name,fingerprint:await tagsFingerprint([tag])},receiptId:attemptId};
  }else{
   const currentProfile=await activeProfile(env,who.profileId,who.connectionId);if(input.requiredGrantEpoch!==undefined&&input.requiredGrantEpoch!==currentProfile.grant_epoch)throw new WorkspaceError('tag_authority_changed',409);const currentConsent=await requireTagConsent(env,who.profileId,who.connectionId);if(currentConsent.consent_epoch!==consent.consent_epoch)throw new WorkspaceError('tag_consent_changed',409);
   const path=operation==='inspectTags'||operation==='createTag'?'tags':operation==='renameTag'?'tags/'+tagId:'tasks/'+taskId+(operation==='setMembership'?'/tags/'+tagId:'');
   const method=operation==='createTag'?'POST':operation==='renameTag'?'PUT':operation==='setMembership'?args.present?'POST':'DELETE':'GET';
   const response=await upstreamFetch(env,'https://habitica.com/api/v3/'+path,{method,redirect:'manual',headers:{'x-api-user':credential.habiticaUserId,'x-api-key':credential.apiToken,'x-client':env.HABITICA_X_CLIENT_HEADER,accept:'application/json','content-type':'application/json'},...(operation==='createTag'||operation==='renameTag'?{body:JSON.stringify({...(operation==='createTag'?{id:tagId}:{}),name:tagNameValue})}:{}),signal:AbortSignal.timeout(10000)},who.profileId);
   await observeUpstream(env,response,who.profileId);const retryAtUtc=await rateState(env,who,response);
   if(!writing){
    if(!response.ok)return json({kind:'refused',code:response.status===429?'rate_limited':[401,403].includes(response.status)?'credential_repair_required':'habitica_unavailable',...(retryAtUtc?{retryAtUtc}:{})},response.status===429?429:[401,403].includes(response.status)?401:502);
    const body=object(await boundedJson(response,2097152));if(body.success!==true)throw new WorkspaceError('invalid_upstream_response',502);
    if(operation==='inspectTags'){
     const tags=nativeTags(body.data),hash=await tagsFingerprint(tags);
     const mapped=await env.HABITICA_INTEGRATION_DB.prepare('SELECT tag_id,fingerprint FROM integration_owned_tags WHERE profile_id=? AND connection_id=? AND workspace_id=? AND entity_id=? AND kind=?').bind(who.profileId,who.connectionId,context.workspaceId,context.entityId,context.kind).first<{tag_id:string;fingerprint:string}>();
     return json({accountId:credential.habiticaUserId,tags,mappedTagId:mapped?.tag_id??null,mappedTagFingerprint:mapped?.fingerprint??null,fingerprint:hash,checkToken:await remember(env,who,consent.consent_epoch,contextHash,{type:'tags',tags,fingerprint:hash})});
    }
    const task=await taggedTask(body.data,credential.habiticaUserId);if(task.task.id!==taskId)throw new WorkspaceError('invalid_upstream_response',502);
    return json({accountId:credential.habiticaUserId,task:{id:task.task.id,kind:task.task.kind,tags:task.tags},fingerprint:task.fingerprint,checkToken:await remember(env,who,consent.consent_epoch,contextHash,{type:'membership',tagId,taskId,kind:task.task.kind,tags:task.tags,fingerprint:task.fingerprint})});
   }
   if([400,401,403,404,409,422,429].includes(response.status))outcome={kind:'refused',code:response.status===429?'rate_limited':[401,403].includes(response.status)?'credential_repair_required':'habitica_refused',...(retryAtUtc?{retryAtUtc}:{})};
   else if(![200,201].includes(response.status))outcome={kind:'unknown',code:'outcome_unknown'};
   else{
    const body=object(await boundedJson(response,2097152));if(body.success!==true)throw new WorkspaceError('invalid_upstream_response',502);
    if(operation==='setMembership'){
     const after=await taggedTask(body.data,credential.habiticaUserId);if(after.task.id!==taskId||after.tags.includes(tagId!)!==args.present||(observed!.tags as string[]).some(id=>id!==tagId&&!after.tags.includes(id)))throw new WorkspaceError('outcome_unknown',502);
     outcome={kind:'applied',code:'applied',task:{id:after.task.id,kind:after.task.kind,fingerprint:after.fingerprint},receiptId:attemptId};
    }else{
     const tag=nativeTags([body.data])[0];if(!tag.native||tag.id!==tagId||tag.name!==tagNameValue)throw new WorkspaceError('outcome_unknown',502);
     outcome={kind:'applied',code:'applied',tag:{id:tag.id,name:tag.name,fingerprint:await tagsFingerprint([tag])},receiptId:attemptId};
    }
   }
  }
  if(outcome.kind==='applied')outcome.confirmedAtUtc=new Date().toISOString();
  if(outcome.kind==='applied'&&outcome.tag)await register(env,who,context,outcome.tag,attemptId!,consent.consent_epoch);
  await env.HABITICA_INTEGRATION_DB.prepare('UPDATE integration_projection_attempts SET state=?,outcome_json=? WHERE profile_id=? AND connection_id=? AND attempt_id=?').bind(outcome.kind,JSON.stringify(outcome),who.profileId,who.connectionId,attemptId!).run();return json(outcome);
 }catch(error){
  if(reserved&&who&&attemptId){try{await env.HABITICA_INTEGRATION_DB.prepare("UPDATE integration_projection_attempts SET state='unknown',outcome_json=? WHERE profile_id=? AND connection_id=? AND attempt_id=?").bind(JSON.stringify({kind:'unknown',code:'outcome_unknown'}),who.profileId,who.connectionId,attemptId).run();}catch{/* Durable dispatched record preserves the original fence. */}return json({kind:'unknown',code:'outcome_unknown'});}
  if(error instanceof TrafficLimit)return json({kind:'refused',code:'rate_limited',retryAtUtc:error.retryAtUtc},429);
  return json({kind:'refused',code:error instanceof WorkspaceError||error instanceof VaultError?error.code:'tag_gateway_unavailable'},error instanceof WorkspaceError||error instanceof VaultError?error.status:503);
 }finally{if(lease&&who){try{await env.HABITICA_INTEGRATION_DB.prepare('UPDATE integration_projection_state SET lease_token=NULL,lease_until_utc=NULL WHERE profile_id=? AND connection_id=? AND lease_token=?').bind(who.profileId,who.connectionId,lease).run();}catch{/* Bounded lease expiry preserves serialization. */}}}
}

// Repair reads and an explicit single-use observation review never send a second mutation.
async function recoverTagAttempt(env:AuthEnv,who:Who,context:TagGatewayContext,contextHash:string,input:Record<string,unknown>,operation:string):Promise<Response>{
 const args=object(input.arguments,operation==='prepareTagRecovery'?['operation','arguments']:['operation','arguments','checkToken','expectedFingerprint']),originalOperation=String(args.operation),original=object(args.arguments),attemptId=uuid(input.attemptId);
 if(!writes.includes(originalOperation))throw new WorkspaceError('invalid_operation',422);
 const hash=await tagAttemptHash('tags.'+originalOperation,{context,arguments:original},input.attemptAtUtc);
 const prior=await env.HABITICA_INTEGRATION_DB.prepare('SELECT payload_hash,state,outcome_json FROM integration_projection_attempts WHERE profile_id=? AND connection_id=? AND attempt_id=?').bind(who.profileId,who.connectionId,attemptId).first<{payload_hash:string;state:string;outcome_json:string|null}>();
 if(!prior)return json({kind:'unknown',code:'attempt_not_found'});if(prior.payload_hash!==hash)throw new WorkspaceError('attempt_id_reused',409);
 if(!['unknown','dispatched'].includes(prior.state))return json(prior.outcome_json?JSON.parse(prior.outcome_json):{kind:'unknown',code:'outcome_unknown'});
 const consent=await requireTagConsent(env,who.profileId,who.connectionId);
 if(operation==='acceptTagRecovery'){
  const checked=await readCheck(env,who,consent.consent_epoch,contextHash,args.checkToken),proof=object(JSON.parse(checked.payload_json));
  if(proof.type!=='recovery'||proof.attemptId!==attemptId||proof.payloadHash!==hash||proof.fingerprint!==fingerprint(args.expectedFingerprint))throw new WorkspaceError('precondition_mismatch',409);
  const lease=crypto.randomUUID(),now=new Date().toISOString();
  const acquired=await env.HABITICA_INTEGRATION_DB.prepare(`INSERT INTO integration_projection_state(profile_id,connection_id,lease_token,lease_until_utc) VALUES(?,?,?,?)
   ON CONFLICT(profile_id,connection_id) DO UPDATE SET lease_token=excluded.lease_token,lease_until_utc=excluded.lease_until_utc WHERE integration_projection_state.lease_until_utc IS NULL OR integration_projection_state.lease_until_utc<=?`).bind(who.profileId,who.connectionId,lease,new Date(Date.now()+15000).toISOString(),now).run();
  if(!acquired.meta.changes)throw new WorkspaceError('projection_busy',429);
  try{
   await activeProfile(env,who.profileId,who.connectionId);
   const tag=proof.tag as TagOutcome['tag'],task=proof.task as TagOutcome['task'];
   if(tag)await register(env,who,context,tag,attemptId,consent.consent_epoch);
   const outcome:TagOutcome={kind:'applied',code:'reviewed_observation',...(tag?{tag}:{}),...(task?{task}:{}),receiptId:attemptId,confirmedAtUtc:now};
   const updated=await env.HABITICA_INTEGRATION_DB.batch([
    env.HABITICA_INTEGRATION_DB.prepare(`UPDATE integration_projection_attempts SET state='applied',outcome_json=? WHERE profile_id=? AND connection_id=? AND attempt_id=? AND payload_hash=? AND state IN ('dispatched','unknown')
     AND EXISTS(SELECT 1 FROM integration_tag_checks c JOIN integration_tag_consents s ON s.profile_id=c.profile_id AND s.connection_id=c.connection_id AND s.consent_epoch=c.consent_epoch WHERE c.token_hash=? AND c.consent_epoch=? AND s.revoked_at_utc IS NULL AND c.expires_at_utc>?)`).bind(JSON.stringify(outcome),who.profileId,who.connectionId,attemptId,hash,checked.token_hash,consent.consent_epoch,now),
    env.HABITICA_INTEGRATION_DB.prepare('DELETE FROM integration_tag_checks WHERE token_hash=?').bind(checked.token_hash),
   ]);
   return json(updated[0].meta.changes?outcome:{kind:'unknown',code:'recovery_changed'});
  }finally{await env.HABITICA_INTEGRATION_DB.prepare('UPDATE integration_projection_state SET lease_token=NULL,lease_until_utc=NULL WHERE profile_id=? AND connection_id=? AND lease_token=?').bind(who.profileId,who.connectionId,lease).run();}
 }
 const details=await env.HABITICA_INTEGRATION_DB.prepare('SELECT observation_json FROM integration_tag_attempt_details WHERE profile_id=? AND connection_id=? AND attempt_id=?').bind(who.profileId,who.connectionId,attemptId).first<{observation_json:string}>();
 if(!details)return json({kind:'unknown',code:'original_observation_missing'});
 const before=object(JSON.parse(details.observation_json)),tagId=uuid(original.tagId),profile=await activeProfile(env,who.profileId,who.connectionId);
 if(profile.credential_schema_version!==1||!env.HABITICA_X_CLIENT_HEADER)throw new WorkspaceError('service_unavailable',503);
 if(originalOperation==='setMembership'||originalOperation==='renameTag')await owned(env,who,context,tagId);
 const taskId=originalOperation==='setMembership'?uuid(original.taskId):null;
 const credential=await decryptCredential(who.profileId,{ciphertext:profile.credential_ciphertext!,iv:profile.credential_iv!,keyVersion:profile.credential_key_version!,schemaVersion:1},credentialKeys(env));
 await upstreamBudget(env,who.profileId);await activeProfile(env,who.profileId,who.connectionId);
 const beforeReadConsent=await requireTagConsent(env,who.profileId,who.connectionId);if(beforeReadConsent.consent_epoch!==consent.consent_epoch)throw new WorkspaceError('tag_consent_changed',409);
 const response=await upstreamFetch(env,'https://habitica.com/api/v3/'+(taskId?'tasks/'+taskId:'tags'),{method:'GET',redirect:'manual',headers:{'x-api-user':credential.habiticaUserId,'x-api-key':credential.apiToken,'x-client':env.HABITICA_X_CLIENT_HEADER,accept:'application/json'},signal:AbortSignal.timeout(10000)},who.profileId);
 await observeUpstream(env,response,who.profileId);const retryAtUtc=await rateState(env,who,response);
 if(!response.ok)return json({kind:'unknown',code:'recovery_unavailable',...(retryAtUtc?{retryAtUtc}:{})},response.status===429?429:502);
 const body=object(await boundedJson(response,2097152));if(body.success!==true)throw new WorkspaceError('invalid_upstream_response',502);
 let observed:{tag?:TagOutcome['tag'];task?:TagOutcome['task']};
 if(taskId){
  const after=await taggedTask(body.data,credential.habiticaUserId);
  if(after.task.id!==taskId||after.tags.includes(tagId)!==original.present||!Array.isArray(before.tags)||(before.tags as string[]).some(id=>id!==tagId&&!after.tags.includes(id)))return json({kind:'unknown',code:'observation_requires_review'});
  observed={task:{id:taskId,kind:after.task.kind,fingerprint:after.fingerprint}};
 }else{
  const tags=nativeTags(body.data),tag=tags.find(tag=>tag.id===tagId),name=originalOperation==='adoptTag'?(before.tags as NativeTag[]).find(tag=>tag.id===tagId)?.name:tagName(original.name);
  if(!tag?.native||tag.name!==name)return json({kind:'unknown',code:'observation_requires_review'});
  observed={tag:{id:tag.id,name:tag.name,fingerprint:await tagsFingerprint([tag])}};
 }
 const hashOfObservation=await workspaceTokenHash(JSON.stringify(observed));
 const current=await requireTagConsent(env,who.profileId,who.connectionId);if(current.consent_epoch!==consent.consent_epoch)throw new WorkspaceError('tag_consent_changed',409);
 return json({kind:'review',code:'original_send_unknown',...observed,originalAttemptId:attemptId,fingerprint:hashOfObservation,checkToken:await remember(env,who,consent.consent_epoch,contextHash,{type:'recovery',attemptId,payloadHash:hash,...observed,fingerprint:hashOfObservation}),disclosure:'The current exact metadata matches the original request. The original send remains uncertain. Approving records this observed state without another Habitica mutation.'});
}
