import { activeProfile, boundedJson, credentialKeys, VaultError } from "./vault";
import { decryptCredential } from "./cryptography";
import { workspaceJson, workspaceObject, uuid, workspaceSessionToken, workspaceTokenHash, WorkspaceError } from "../../shared/workspace-http";
import { selector, remotePatch, remoteTask, remotePatchMatches, type RemoteTask } from "../../shared/projection";
interface Context { profileId: string; connectionId: string }
interface Check { token_hash: string; selector: string; task_id: string | null; fingerprint: string | null; kind: string | null; completed: number | null; up: number | null; down: number | null; is_due: number | null; account_json: string | null }
interface Outcome { kind: "applied" | "refused" | "unknown"; code: string; retryAtUtc?: string; task?: { id: string; kind: string; fingerprint: string; alias: string | null } }
const headers = { "cache-control": "no-store" };
const json = (value: unknown, status = 200) => Response.json(value, { status, headers });
const fingerprint = (value: unknown): string => { if (typeof value !== "string" || !/^[a-f0-9]{64}$/.test(value)) throw new WorkspaceError("invalid_precondition",422); return value; };
function day(account: { timezoneOffset: number; dayStart: number }) { return new Date(Date.now() - account.timezoneOffset * 60000 - account.dayStart * 3600000).toISOString().slice(0,10); }
async function check(env: AuthEnv, who: Context, token: unknown): Promise<Check> {
 if (typeof token !== "string" || !/^[A-Za-z0-9_-]{43}$/.test(token)) throw new WorkspaceError("precondition_required",409);
 const row = await env.HABITICA_INTEGRATION_DB.prepare("SELECT * FROM integration_projection_checks WHERE token_hash=? AND profile_id=? AND connection_id=? AND expires_at_utc>?").bind(await workspaceTokenHash(token),who.profileId,who.connectionId,new Date().toISOString()).first<Check>();
 if (!row) throw new WorkspaceError("precondition_expired",409); return row;
}
async function rememberCheck(env: AuthEnv, who: Context, target: string, task: RemoteTask | null, account?: unknown) {
 const token = workspaceSessionToken(); const now = new Date().toISOString();
 await env.HABITICA_INTEGRATION_DB.prepare(`INSERT INTO integration_projection_checks(token_hash,profile_id,connection_id,selector,task_id,fingerprint,kind,completed,up,down,is_due,account_json,checked_at_utc,expires_at_utc)
 VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?)`).bind(await workspaceTokenHash(token),who.profileId,who.connectionId,target,task?.id ?? null,task?.fingerprint ?? null,task?.kind ?? null,task ? Number(task.completed) : null,task ? Number(task.up) : null,task ? Number(task.down) : null,task?.isDue === null || !task ? null : Number(task.isDue),account ? JSON.stringify(account) : null,now,new Date(Date.now()+90000).toISOString()).run();
 return token;
}
export async function rateState(env: AuthEnv, who: Context, response: Response, markCredentialFailure = true) {
 const retry = response.headers.get("retry-after"); const resetText = response.headers.get("x-ratelimit-reset");
 const retrySeconds = retry ? /^\d{1,9}$/.test(retry) ? Number(retry) : Math.ceil((Date.parse(retry)-Date.now())/1000) : 60;
 const reset = resetText && /^\d{1,12}$/.test(resetText) ? Number(resetText)*1000 : 0;
 const blocked = response.status === 429 || response.headers.get("x-ratelimit-remaining") === "0" ? new Date(Math.max(Date.now()+Math.max(Number.isFinite(retrySeconds)?retrySeconds:60,1)*1000,reset)).toISOString() : null;
 await env.HABITICA_INTEGRATION_DB.prepare(`INSERT INTO integration_read_state(profile_id,connection_id,blocked_until_utc,credential_repair_required) VALUES(?,?,?,?)
 ON CONFLICT(profile_id,connection_id) DO UPDATE SET blocked_until_utc=CASE WHEN integration_read_state.blocked_until_utc IS NULL OR excluded.blocked_until_utc>integration_read_state.blocked_until_utc THEN excluded.blocked_until_utc ELSE integration_read_state.blocked_until_utc END,
 credential_repair_required=MAX(integration_read_state.credential_repair_required,excluded.credential_repair_required)`).bind(who.profileId,who.connectionId,blocked,markCredentialFailure&&[401,403].includes(response.status)?1:0).run();
 return blocked;
}
export async function projectionOperation(request: Request, env: AuthEnv): Promise<Response> {
 let who: Context | undefined, lease: string | undefined, attemptId: string | undefined, dispatched = false;
 try {
  if (!Boolean(env.PROJECTION_ENABLED) || !Boolean(env.WORKSPACE_ENABLED) || request.method !== "POST") throw new WorkspaceError("unauthorized",401);
  const input = workspaceObject(await workspaceJson(request,262144),["profileId","connectionId","operation","arguments","attemptId","attemptAtUtc","automated"]);
  if (typeof input.profileId !== "string" || !/^prf_[A-Za-z0-9_-]{43}$/.test(input.profileId) || typeof input.automated !== "boolean") throw new WorkspaceError("invalid_request");
  who = { profileId: input.profileId, connectionId: uuid(input.connectionId) };
  const operation = input.operation; if (typeof operation !== "string" || !["inspect","inspectDaily","inspectAccount","create","update","score","reconcile","lookup"].includes(operation)) throw new WorkspaceError("invalid_operation");
  const writing = ["create","update","score"].includes(operation);
  const args = workspaceObject(input.arguments,["reconcile","lookup"].includes(operation) ? ["operation","arguments"] : writing ? operation === "create" ? ["selector","checkToken","patch"] : operation === "update" ? ["selector","checkToken","expectedFingerprint","patch"] : ["selector","checkToken","expectedFingerprint","direction","accountCheckToken","occurrenceDate"] : operation === "inspectAccount" ? [] : ["selector"]);
  const profile = await activeProfile(env,who.profileId,who.connectionId);
  // Operational attempt metadata expires after thirty days. Old attempt timestamps cannot authorize a new send.
  await env.HABITICA_INTEGRATION_DB.batch([
   env.HABITICA_INTEGRATION_DB.prepare("DELETE FROM integration_projection_attempts WHERE created_at_utc<?").bind(new Date(Date.now()-30*86400000).toISOString()),
   env.HABITICA_INTEGRATION_DB.prepare("DELETE FROM integration_projection_checks WHERE expires_at_utc<=?").bind(new Date().toISOString()),
  ]);
  if (["reconcile","lookup"].includes(operation)) return await reconcileAttempt(env,who,input,args,operation === "lookup");
  let payloadHash: string | undefined;
  if (writing) {
   attemptId = uuid(input.attemptId); payloadHash = await workspaceTokenHash(JSON.stringify({ operation, arguments: args, attemptAtUtc: input.attemptAtUtc }));
   const prior = await env.HABITICA_INTEGRATION_DB.prepare("SELECT payload_hash,state,outcome_json FROM integration_projection_attempts WHERE profile_id=? AND connection_id=? AND attempt_id=?").bind(who.profileId,who.connectionId,attemptId).first<{payload_hash:string;state:string;outcome_json:string|null}>();
   if (prior) { if (prior.payload_hash !== payloadHash) throw new WorkspaceError("attempt_id_reused",409); return json(prior.outcome_json ? JSON.parse(prior.outcome_json) : {kind:"unknown",code:"outcome_unknown"}); }
   const at = typeof input.attemptAtUtc === "string" ? Date.parse(input.attemptAtUtc) : NaN;
   if (!Number.isFinite(at) || at < Date.now()-120000 || at > Date.now()+5000) throw new WorkspaceError("attempt_expired",409);
   const unresolved = await env.HABITICA_INTEGRATION_DB.prepare("SELECT attempt_id FROM integration_projection_attempts WHERE profile_id=? AND connection_id=? AND state IN ('dispatched','unknown') LIMIT 1").bind(who.profileId,who.connectionId).first();
   if (unresolved) throw new WorkspaceError("unknown_outcome_pending",409);
  } else if (input.attemptId !== undefined || input.attemptAtUtc !== undefined) throw new WorkspaceError("invalid_request");
  const quota = await env.HABITICA_INTEGRATION_DB.prepare("SELECT blocked_until_utc,credential_repair_required FROM integration_read_state WHERE profile_id=? AND connection_id=?").bind(who.profileId,who.connectionId).first<{blocked_until_utc:string|null;credential_repair_required:number}>();
  if (quota?.credential_repair_required) return json({kind:"refused",code:"credential_repair_required"},401);
  if (quota?.blocked_until_utc && Date.parse(quota.blocked_until_utc)>Date.now()) return json({kind:"refused",code:"rate_limited",retryAtUtc:quota.blocked_until_utc},429);
  let precondition: Check | undefined; let target = operation === "inspectAccount" ? "account" : selector(args.selector);
  let patch;
  if (writing) {
   precondition = await check(env,who,args.checkToken);
   if (precondition.selector !== target && precondition.task_id !== target) throw new WorkspaceError("precondition_mismatch",409);
   if (operation === "create") {
    if (precondition.task_id || !/^companion_[0-9a-f]{32}_[0-9a-f]{32}$/.test(target)) throw new WorkspaceError("creation_not_absent",409);
    patch = remotePatch(args.patch,true);
   } else {
    target = uuid(target);
    if (precondition.task_id !== target || precondition.fingerprint !== fingerprint(args.expectedFingerprint)) throw new WorkspaceError("external_conflict",409);
    if (operation === "update") patch = remotePatch(args.patch);
    else {
     if (args.patch !== undefined || !["up","down"].includes(args.direction as string)) throw new WorkspaceError("invalid_direction",422);
     if (precondition.kind === "habit" ? !(args.direction === "up" ? precondition.up : precondition.down) : args.direction !== "up" || precondition.completed !== 0) throw new WorkspaceError("task_not_scorable",409);
     if (precondition.kind === "daily") {
      const account = await check(env,who,args.accountCheckToken);
      const state = account.account_json ? JSON.parse(account.account_json) : null;
      if (!state || state.needsCron !== false || precondition.is_due !== 1 || args.occurrenceDate !== state.day || day(state) !== state.day) throw new WorkspaceError("daily_precondition_unknown",409);
     } else if (args.accountCheckToken !== undefined || args.occurrenceDate !== undefined) throw new WorkspaceError("invalid_request");
    }
   }
  }
  if (profile.credential_schema_version !== 1 || !env.HABITICA_X_CLIENT_HEADER) throw new WorkspaceError("service_unavailable",503);
  const credential = await decryptCredential(who.profileId,{ciphertext:profile.credential_ciphertext!,iv:profile.credential_iv!,keyVersion:profile.credential_key_version!,schemaVersion:1},credentialKeys(env));
  lease = crypto.randomUUID(); const now = new Date().toISOString();
  const acquired = await env.HABITICA_INTEGRATION_DB.prepare(`INSERT INTO integration_projection_state(profile_id,connection_id,lease_token,lease_until_utc,next_automated_at_utc) VALUES(?,?,?,?,?)
   ON CONFLICT(profile_id,connection_id) DO UPDATE SET lease_token=excluded.lease_token,lease_until_utc=excluded.lease_until_utc,next_automated_at_utc=excluded.next_automated_at_utc
   WHERE (integration_projection_state.lease_until_utc IS NULL OR integration_projection_state.lease_until_utc<=?)
   AND (?=0 OR integration_projection_state.next_automated_at_utc IS NULL OR integration_projection_state.next_automated_at_utc<=?)`).bind(who.profileId,who.connectionId,lease,new Date(Date.now()+15000).toISOString(),new Date(Date.now()+30000).toISOString(),now,Number(input.automated),now).run();
  if (!acquired.meta.changes) return json({kind:"refused",code:"projection_busy",retryAtUtc:new Date(Date.now()+30000).toISOString()},429);
  if (writing) {
   const prepared = await env.HABITICA_INTEGRATION_DB.batch([
    env.HABITICA_INTEGRATION_DB.prepare("INSERT INTO integration_projection_attempts(profile_id,connection_id,attempt_id,payload_hash,operation,state,created_at_utc) SELECT ?,?,?,?,?,'dispatched',? FROM integration_projection_checks WHERE token_hash=? AND profile_id=? AND connection_id=? AND expires_at_utc>?").bind(who.profileId,who.connectionId,attemptId!,payloadHash!,operation,now,precondition!.token_hash,who.profileId,who.connectionId,now),
    env.HABITICA_INTEGRATION_DB.prepare("DELETE FROM integration_projection_checks WHERE token_hash=? AND EXISTS(SELECT 1 FROM integration_projection_attempts WHERE profile_id=? AND connection_id=? AND attempt_id=?)").bind(precondition!.token_hash,who.profileId,who.connectionId,attemptId!),
   ]);
   if (!prepared[0].meta.changes) throw new WorkspaceError("precondition_consumed",409); dispatched = true;
  }
  const path = operation === "inspectAccount" ? "user" : operation === "inspectDaily" ? "tasks/user?type=dailys&history=false&dueDate="+encodeURIComponent(now) : operation === "create" ? "tasks/user" : `tasks/${encodeURIComponent(target)}${operation === "score" ? `/score/${args.direction}` : ""}`;
  const method = operation === "create" || operation === "score" ? "POST" : operation === "update" ? "PUT" : "GET";
  await activeProfile(env,who.profileId,who.connectionId);
  const response = await fetch("https://habitica.com/api/v3/"+path,{method,redirect:"manual",headers:{"x-api-user":credential.habiticaUserId,"x-api-key":credential.apiToken,"x-client":env.HABITICA_X_CLIENT_HEADER,accept:"application/json","content-type":"application/json"},body:operation === "create" ? JSON.stringify({...patch,type:"todo",alias:target}) : operation === "update" ? JSON.stringify(patch) : undefined,signal:AbortSignal.timeout(10000)});
  const retryAtUtc = await rateState(env,who,response);
  if (!writing) {
   if (response.status === 404 && operation === "inspect") return json({task:null,checkToken:await rememberCheck(env,who,target,null)});
   if (!response.ok) return json({kind:"refused",code:[401,403].includes(response.status)?"credential_repair_required":response.status===429?"rate_limited":"habitica_unavailable",...(retryAtUtc?{retryAtUtc}:{})},response.status===429?429:response.status===401||response.status===403?401:502);
   const body = workspaceObject(await boundedJson(response,2097152)); if (body.success !== true) throw new WorkspaceError("invalid_upstream_response",502);
   if (operation === "inspectAccount") {
    const account = workspaceObject(body.data); const prefs = workspaceObject(account.preferences);
    if (typeof prefs.dayStart !== "number" || !Number.isInteger(prefs.dayStart) || prefs.dayStart<0 || prefs.dayStart>23 || typeof prefs.timezoneOffset !== "number" || !Number.isFinite(prefs.timezoneOffset) || Math.abs(prefs.timezoneOffset)>840 || typeof account.needsCron !== "boolean") throw new WorkspaceError("daily_precondition_unknown",409);
    const state = {dayStart:prefs.dayStart,timezoneOffset:prefs.timezoneOffset,needsCron:account.needsCron,day:day({dayStart:prefs.dayStart,timezoneOffset:prefs.timezoneOffset})};
    return json({...state,accountCheckToken:await rememberCheck(env,who,"account",null,state)});
   }
   if (operation === "inspectDaily" && !Array.isArray(body.data)) throw new WorkspaceError("invalid_upstream_response",502);
   const raw = operation === "inspectDaily" ? (body.data as Record<string,unknown>[]).find(task=>(task.id??task._id)===target) : body.data;
   if (raw === undefined) return json({task:null,checkToken:await rememberCheck(env,who,target,null)});
   const task = await remoteTask(raw,credential.habiticaUserId,operation === "inspectDaily");
   if (task.id !== target && task.alias !== target || operation === "inspectDaily" && task.kind !== "daily") throw new WorkspaceError("invalid_upstream_response",502);
   return json({task,checkToken:await rememberCheck(env,who,target,task)});
  }
  let outcome: Outcome;
  if ([400,401,403,404,409,422,429].includes(response.status)) outcome={kind:"refused",code:response.status===429?"rate_limited":[401,403].includes(response.status)?"credential_repair_required":response.status===404?"external_deleted":"habitica_refused",...(retryAtUtc?{retryAtUtc}:{})};
  else if (![200,201].includes(response.status)) outcome={kind:"unknown",code:"outcome_unknown"};
  else {
   const body = workspaceObject(await boundedJson(response,2097152));
   if (body.success !== true) throw new WorkspaceError("outcome_unknown",502);
   if (operation === "score") { const data=workspaceObject(body.data); if (typeof data.delta !== "number" || !Number.isFinite(data.delta)) throw new WorkspaceError("outcome_unknown",502); outcome={kind:"applied",code:"applied",task:{id:target,kind:precondition!.kind!,fingerprint:precondition!.fingerprint!,alias:null}}; }
   else { const task=await remoteTask(body.data,credential.habiticaUserId); if (operation === "create" ? task.kind!=="todo"||task.alias!==target : task.id!==target) throw new WorkspaceError("outcome_unknown",502); outcome={kind:"applied",code:"applied",task:{id:task.id,kind:task.kind,fingerprint:task.fingerprint,alias:task.alias}}; }
  }
  await env.HABITICA_INTEGRATION_DB.prepare("UPDATE integration_projection_attempts SET state=?,outcome_json=? WHERE profile_id=? AND connection_id=? AND attempt_id=?").bind(outcome.kind,JSON.stringify(outcome),who.profileId,who.connectionId,attemptId!).run();
  return json(outcome);
 } catch (error) {
  if (dispatched && who && attemptId) {
   try { await env.HABITICA_INTEGRATION_DB.prepare("UPDATE integration_projection_attempts SET state='unknown',outcome_json=? WHERE profile_id=? AND connection_id=? AND attempt_id=?").bind(JSON.stringify({kind:"unknown",code:"outcome_unknown"}),who.profileId,who.connectionId,attemptId).run(); } catch { /* The pre-send dispatched record still prevents replay. */ }
   return json({kind:"unknown",code:"outcome_unknown"});
  }
  return json({kind:"refused",code:error instanceof WorkspaceError?error.code:error instanceof VaultError?error.code:"projection_unavailable"},error instanceof WorkspaceError || error instanceof VaultError?error.status:503);
 } finally {
  if (lease && who) { try { await env.HABITICA_INTEGRATION_DB.prepare("UPDATE integration_projection_state SET lease_token=NULL,lease_until_utc=NULL WHERE profile_id=? AND connection_id=? AND lease_token=?").bind(who.profileId,who.connectionId,lease).run(); } catch { /* Expiry releases the bounded lease. */ } }
 }
}

export async function clearProjectionContext(env: AuthEnv, profileId: string, connectionId: string): Promise<void> {
 await env.HABITICA_INTEGRATION_DB.batch(["integration_projection_checks","integration_projection_attempts","integration_projection_state"].map(table=>env.HABITICA_INTEGRATION_DB.prepare(`DELETE FROM ${table} WHERE profile_id=? AND connection_id=?`).bind(profileId,connectionId)));
}
export async function purgeProjectionMetadata(env: AuthEnv): Promise<void> {
 const now=new Date().toISOString(); const cutoff=new Date(Date.now()-30*86400000).toISOString();
 await env.HABITICA_INTEGRATION_DB.batch([
  env.HABITICA_INTEGRATION_DB.prepare("DELETE FROM integration_projection_attempts WHERE created_at_utc<?").bind(cutoff),
  env.HABITICA_INTEGRATION_DB.prepare("DELETE FROM integration_projection_checks WHERE expires_at_utc<=?").bind(now),
  env.HABITICA_INTEGRATION_DB.prepare("DELETE FROM integration_projection_state WHERE next_automated_at_utc<? AND (lease_until_utc IS NULL OR lease_until_utc<=?)").bind(cutoff,now),
 ]);
}

async function reconcileAttempt(env: AuthEnv, who: Context, input: Record<string,unknown>, args: Record<string,unknown>, lookupOnly = false): Promise<Response> {
 const attemptId=uuid(input.attemptId); const operation=args.operation;
 if (typeof operation!=="string" || !["create","update","score"].includes(operation)) throw new WorkspaceError("invalid_operation");
 const original=workspaceObject(args.arguments); const hash=await workspaceTokenHash(JSON.stringify({operation,arguments:original,attemptAtUtc:input.attemptAtUtc}));
 const prior=await env.HABITICA_INTEGRATION_DB.prepare("SELECT payload_hash,state,outcome_json FROM integration_projection_attempts WHERE profile_id=? AND connection_id=? AND attempt_id=?").bind(who.profileId,who.connectionId,attemptId).first<{payload_hash:string;state:string;outcome_json:string|null}>();
 if (!prior) return json({kind:"unknown",code:"attempt_not_found"});
 if (prior.payload_hash!==hash) throw new WorkspaceError("attempt_id_reused",409);
 if (!["dispatched","unknown"].includes(prior.state)) return json(prior.outcome_json?JSON.parse(prior.outcome_json):{kind:"unknown",code:"outcome_unknown"});
 if (lookupOnly) return json({kind:"unknown",code:"outcome_unknown"});
 // Habit scores have no conclusive per-command upstream receipt. Caller assertions never resolve them.
 if (operation==="score") return json({kind:"unknown",code:"score_outcome_requires_review"});
 const target=selector(original.selector);
 const read=await projectionOperation(new Request("https://projection.internal/operation",{method:"POST",headers:{"content-type":"application/json"},body:JSON.stringify({...who,operation:"inspect",arguments:{selector:target},automated:input.automated})}),env);
 if (!read.ok) return read;
 const observed=await read.json<{task:RemoteTask|null}>(); const task=observed.task; const patch=remotePatch(original.patch,operation==="create");
 // Absence after a timeout does not prove that an earlier request will never finish.
 if (!task || operation==="create" && (task.alias!==target || task.kind!=="todo") || operation==="update" && task.id!==target || !remotePatchMatches(task,patch)) return json({kind:"unknown",code:"outcome_unknown"});
 const outcome: Outcome={kind:"applied",code:"reconciled_applied",task:{id:task.id,kind:task.kind,fingerprint:task.fingerprint,alias:task.alias}};
 const updated=await env.HABITICA_INTEGRATION_DB.prepare("UPDATE integration_projection_attempts SET state='applied',outcome_json=? WHERE profile_id=? AND connection_id=? AND attempt_id=? AND payload_hash=? AND state IN ('unknown','dispatched')").bind(JSON.stringify(outcome),who.profileId,who.connectionId,attemptId,hash).run();
 return json(updated.meta.changes?outcome:{kind:"unknown",code:"outcome_unknown"});
}
