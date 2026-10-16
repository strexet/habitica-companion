import {mcpEpoch} from './mcp-epoch';
import {cronApi,cronHistory} from './cron-api';
import {cronToolScopes,type CronTool} from '../../shared/cron-tools';
import {spellApi,spellHistory} from './spell-api';
import {spellToolScopes,type SpellTool} from '../../shared/spell-tools';
import {consentParty,partySettings,ownContributions,planningState} from './party-planning';
import {partyApi,PartyApiFailure,fetchPartyProof} from './party-api';
import {partyToolScopes,publicPartyContext,type PartyTool} from '../../shared/party-tools';
import { purgeExpiredPreparations } from "./preparation-retention";
import { WorkerEntrypoint } from "cloudflare:workers";
import { taskApi } from "./task-api";
import { taskToolScopes, type TaskTool } from "../../shared/task-tools";
import { handoff } from "./handoff";
import { executeScheduled, cloudAuthority, type WorkspaceRow } from "./executor";
import { adoptProjection } from "./projection-adapter";
import { resourceConfiguration } from "../../shared/resources";
import { bearer, uuid, workspaceJson, workspaceObject, WorkspaceError, workspaceSessionToken, workspaceTokenHash } from "../../shared/workspace-http";
import { emptyWorkspace } from "../../shared/workspace-domain";
import { workspaceCommand, commandHash } from "../../shared/workspace-commands";
import { accept } from "./commands";
interface Identity { profileId: string; connectionId: string }
const headers = { "cache-control": "no-store", "x-content-type-options": "nosniff" };
const consentVersion = "workspace-v1";
const bytes = (value: string) => new TextEncoder().encode(value).length;
const json = (value: unknown, status = 200) => Response.json(value, { status, headers });
async function identity(env: WorkspaceEnv, path: string, authorization?: string, value?: Identity): Promise<Identity> {
 const response = await env.AUTH_IDENTITY.fetch("https://identity.internal/" + path, { method: "POST", headers: { "content-type": "application/json", ...(authorization ? { authorization } : {}) }, body: value ? JSON.stringify(value) : undefined });
 if (!response.ok) throw new WorkspaceError("unauthorized", 401);
 const result = await response.json<Identity>();
 if (!/^prf_[A-Za-z0-9_-]{43}$/.test(result.profileId)) throw new WorkspaceError("unauthorized", 401);
 uuid(result.connectionId); return result;
}
async function session(request: Request, env: WorkspaceEnv): Promise<WorkspaceRow> {
 const hash = await workspaceTokenHash(bearer(request));
 const row = await env.WORKSPACE_DB.prepare(`SELECT w.* FROM workspace_sessions s JOIN workspaces w ON w.profile_id=s.profile_id
 WHERE s.token_hash=? AND s.expires_at_utc>? AND s.connection_id=w.connection_id AND s.access_epoch=w.access_epoch AND w.active=1`).bind(hash, new Date().toISOString()).first<WorkspaceRow>();
 if (!row) throw new WorkspaceError("unauthorized", 401);
 await identity(env, "epoch", undefined, { profileId: row.profile_id, connectionId: row.connection_id });
 const current = await env.WORKSPACE_DB.prepare(`SELECT w.* FROM workspace_sessions s JOIN workspaces w ON w.profile_id=s.profile_id
 WHERE s.token_hash=? AND s.expires_at_utc>? AND s.access_epoch=? AND w.access_epoch=s.access_epoch AND w.active=1`).bind(hash,new Date().toISOString(),row.access_epoch).first<WorkspaceRow>();
 if (!current) throw new WorkspaceError("unauthorized", 401); return current;
}
async function connect(request: Request, env: WorkspaceEnv): Promise<Response> {
 const body = workspaceObject(await workspaceJson(request), ["consentVersion", "workspaceId"]);
 if (body.consentVersion !== consentVersion) throw new WorkspaceError("consent_required", 403);
 const who = await identity(env, "management", `Bearer ${bearer(request)}`);
 const workspaceId = body.workspaceId === undefined ? crypto.randomUUID() : uuid(body.workspaceId);
 const existing = await env.WORKSPACE_DB.prepare("SELECT workspace_id FROM workspaces WHERE profile_id=?").bind(who.profileId).first<{ workspace_id: string }>();
 if (body.workspaceId !== undefined && existing && existing.workspace_id !== workspaceId) throw new WorkspaceError("workspace_mismatch", 409);
 const now = new Date().toISOString(); const tag = crypto.randomUUID(); const documentJson = JSON.stringify(emptyWorkspace(workspaceId));
 // Consent never replaces retained canonical data. Reconnection revokes old session epochs.
 await env.WORKSPACE_DB.prepare(`INSERT INTO workspaces(profile_id,workspace_id,connection_id,access_epoch,consent_version,consented_at_utc,document_json,archive_bytes,commit_tag,created_at_utc,updated_at_utc)
 VALUES(?,?,?,?,?,?,?,?,?,?,?) ON CONFLICT(profile_id) DO UPDATE SET connection_id=excluded.connection_id,consent_version=excluded.consent_version,
 consented_at_utc=excluded.consented_at_utc,active=1,access_epoch=CASE WHEN workspaces.connection_id=excluded.connection_id AND workspaces.active=1 THEN workspaces.access_epoch ELSE excluded.access_epoch END,executor_enabled=CASE WHEN workspaces.connection_id=excluded.connection_id AND workspaces.active=1 THEN workspaces.executor_enabled ELSE 0 END,updated_at_utc=excluded.updated_at_utc`)
 .bind(who.profileId, workspaceId, who.connectionId, crypto.randomUUID(), consentVersion, now, documentJson, bytes(documentJson) + 512, tag, now, now).run();
 const row = await env.WORKSPACE_DB.prepare("SELECT * FROM workspaces WHERE profile_id=?").bind(who.profileId).first<WorkspaceRow>();
 if (!row || row.connection_id !== who.connectionId) throw new WorkspaceError("connection_changed", 409);
 if (body.workspaceId !== undefined && row.workspace_id !== workspaceId) throw new WorkspaceError("workspace_mismatch", 409);
 const token = workspaceSessionToken(); const hash = await workspaceTokenHash(token); const expires = new Date(Date.now() + 8 * 60 * 60 * 1000).toISOString();
 await env.WORKSPACE_DB.batch([
  env.WORKSPACE_DB.prepare(`INSERT INTO workspace_sessions(token_hash,profile_id,connection_id,access_epoch,created_at_utc,expires_at_utc)
   SELECT ?,profile_id,connection_id,access_epoch,?,? FROM workspaces WHERE profile_id=? AND active=1 AND connection_id=? AND access_epoch=?`).bind(hash, now, expires, who.profileId, who.connectionId, row.access_epoch),
  env.WORKSPACE_DB.prepare(`DELETE FROM workspace_sessions WHERE profile_id=? AND EXISTS(SELECT 1 FROM workspaces WHERE profile_id=? AND access_epoch=? AND active=1) AND (access_epoch<>? OR expires_at_utc<=? OR token_hash NOT IN
   (SELECT token_hash FROM workspace_sessions WHERE profile_id=? ORDER BY created_at_utc DESC,rowid DESC LIMIT 8))`).bind(who.profileId, who.profileId, row.access_epoch, row.access_epoch, now, who.profileId),
 ]);
 const check = await env.WORKSPACE_DB.prepare("SELECT token_hash FROM workspace_sessions WHERE token_hash=?").bind(hash).first();
 if (!check) throw new WorkspaceError("connection_changed", 409);
 return json({ workspaceId: row.workspace_id, sessionToken: token, expiresAtUtc: expires, consentVersion, executorEnabled: Boolean(row.executor_enabled), executorAvailable: Boolean(env.EXECUTOR_ENABLED) }, 201);
}
async function duplicate(env: WorkspaceEnv, row: WorkspaceRow, id: string, hash: string): Promise<Response | null> {
 const receipt = await env.WORKSPACE_DB.prepare("SELECT c.payload_hash,c.entry_json FROM workspace_commands c JOIN workspaces w ON w.profile_id=c.profile_id WHERE c.profile_id=? AND c.command_id=? AND w.access_epoch=? AND w.active=1").bind(row.profile_id, id, row.access_epoch).first<{ payload_hash: string; entry_json: string }>();
 if (!receipt) return null;
 if (receipt.payload_hash !== hash) throw new WorkspaceError("command_id_reused", 409);
 return json({ disposition: "duplicate", savedInCompanion: true, entry: JSON.parse(receipt.entry_json) });
}
async function command(request: Request, env: WorkspaceEnv, row: WorkspaceRow, prepared = false): Promise<Response> {
 let input; try { input = workspaceCommand(await workspaceJson(request, 65536)); } catch (error) { if (error instanceof WorkspaceError) throw error; throw new WorkspaceError("invalid_command", 422); }
 if (input.workspaceId !== row.workspace_id) throw new WorkspaceError("workspace_mismatch", 409);
 const hash = await commandHash(input); const replay = await duplicate(env, row, input.id, hash); if (replay) return replay;
 const accepted = await accept(JSON.parse(row.document_json), input, new Date().toISOString());
 const serialized = JSON.stringify(accepted.document);
 // D1 has a per-row bound. Reject before acceptance rather than silently dropping history.
 if (bytes(serialized) > 1024 * 1024) throw new WorkspaceError("workspace_capacity", 413);
 if (row.store_version >= Number.MAX_SAFE_INTEGER) throw new WorkspaceError("revision_exhausted", 409);
 const archiveBytes = row.archive_bytes - bytes(row.document_json) + bytes(serialized) + bytes(JSON.stringify(accepted.entry)) + bytes(JSON.stringify(accepted.event)) + 2;
 if (archiveBytes > 8 * 1024 * 1024) throw new WorkspaceError("workspace_capacity", 413);
 const version = row.store_version + 1; const tag = crypto.randomUUID();
 const result = await env.WORKSPACE_DB.batch([
  env.WORKSPACE_DB.prepare(`UPDATE workspaces SET document_json=?,store_version=?,commit_tag=?,updated_at_utc=?,archive_bytes=?
   WHERE profile_id=? AND store_version=? AND connection_id=? AND access_epoch=? AND active=1
   AND (?=0 OR EXISTS(SELECT 1 FROM workspace_preparations p WHERE p.profile_id=workspaces.profile_id AND p.command_id=? AND p.access_epoch=workspaces.access_epoch AND p.expires_at_utc>strftime('%Y-%m-%dT%H:%M:%fZ','now')))` ).bind(serialized, version, tag, accepted.entry.acceptedAtUtc, archiveBytes, row.profile_id, row.store_version, row.connection_id, row.access_epoch, Number(prepared), input.id),
  env.WORKSPACE_DB.prepare(`INSERT INTO workspace_commands(profile_id,command_id,sequence,payload_hash,entry_json)
   SELECT profile_id,?,?,?,? FROM workspaces WHERE profile_id=? AND commit_tag=?`).bind(input.id, version, hash, JSON.stringify(accepted.entry), row.profile_id, tag),
  env.WORKSPACE_DB.prepare(`INSERT INTO workspace_events(profile_id,event_id,sequence,event_json)
   SELECT profile_id,?,?,? FROM workspaces WHERE profile_id=? AND commit_tag=?`).bind(accepted.event.id, version, JSON.stringify(accepted.event), row.profile_id, tag),
 ]);
 if (!result[0].meta.changes) { const retry = await duplicate(env, row, input.id, hash); if (retry) return retry; throw new WorkspaceError("stale_revision", 409); }
 return json({ disposition: "accepted", savedInCompanion: true, entry: accepted.entry, revision: accepted.document.revision }, 201);
}
async function snapshot(env: WorkspaceEnv, row: WorkspaceRow): Promise<Response> {
 // Read the document and journal from one database transaction, including concurrent acceptance.
 const results = await env.WORKSPACE_DB.batch([
  env.WORKSPACE_DB.prepare("SELECT document_json,store_version,authority_json FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1").bind(row.profile_id,row.connection_id,row.access_epoch),
  env.WORKSPACE_DB.prepare("SELECT entry_json FROM workspace_commands WHERE profile_id=? ORDER BY sequence").bind(row.profile_id),
  env.WORKSPACE_DB.prepare("SELECT event_json FROM workspace_events WHERE profile_id=? ORDER BY sequence").bind(row.profile_id),
 ]);
 const current = results[0].results[0] as { document_json: string; store_version: number; authority_json: string|null } | undefined;
 if (!current) throw new WorkspaceError("unauthorized", 401);
 const value = JSON.stringify({ document: JSON.parse(current.document_json), storeVersion: current.store_version, authority: current.authority_json ? JSON.parse(current.authority_json) : cloudAuthority(row.workspace_id), commands: results[1].results.map(item => JSON.parse((item as { entry_json: string }).entry_json)), events: results[2].results.map(item => JSON.parse((item as { event_json: string }).event_json)) });
 if (new TextEncoder().encode(value).length > 16 * 1024 * 1024) throw new WorkspaceError("export_capacity", 413);
 return new Response(value, { headers: { ...headers, "content-type": "application/json" } });
}
async function handle(request: Request, env: WorkspaceEnv): Promise<Response> {
 if (new URL(request.url).pathname === "/health" && request.method === "GET") return json({ service: "workspace", enabled: Boolean(env.WORKSPACE_ENABLED) });
 if (!Boolean(env.WORKSPACE_ENABLED)) throw new WorkspaceError("workspace_unavailable", 503);
 const path = new URL(request.url).pathname;
 if (path === "/connect" && request.method === "POST") return connect(request, env);
 if(path==='/cron/history'&&request.method==='GET'){
  const who=await identity(env,'management',`Bearer ${bearer(request)}`);await identity(env,'epoch',undefined,who);
  return json(await cronHistory(env,who.profileId));
 }
 if(path==='/spells/history'&&request.method==='GET'){
  const who=await identity(env,'management',`Bearer ${bearer(request)}`);await identity(env,'epoch',undefined,who);
  return json(await spellHistory(env,who.profileId));
 }
 if(path==='/party/contributions'&&(request.method==='GET'||request.method==='POST')){
  if(!env.PARTY_ENABLED)throw new WorkspaceError('party_unavailable',503);
  const who=await identity(env,'management',`Bearer ${bearer(request)}`);
  if(request.method==='POST'){
   const input=workspaceObject(await workspaceJson(request),['confirm']);
   if(input.confirm!=='remove-my-party-contributions')throw new WorkspaceError('confirmation_required',409);
  }
  await identity(env,'epoch',undefined,who);
  return json(await ownContributions(env,who.profileId,request.method==='POST'));
 }
 const row = await session(request, env);
 if(path.startsWith('/party/')||path==='/party-api'){
  if(!env.PARTY_ENABLED)throw new WorkspaceError('party_unavailable',503);
  if(path==='/party-api'&&request.method==='POST'){
   const input=workspaceObject(await workspaceJson(request),['tool','arguments']);return json(await partyApi(env,row,input.tool as PartyTool,input.arguments));
  }
  const proof=await fetchPartyProof(env,{profileId:row.profile_id,connectionId:row.connection_id,accessEpoch:row.access_epoch},path==='/party/state'?'get_quest_planning_state':'get_party_context');
  if(path==='/party/context'&&request.method==='GET')return json(publicPartyContext(proof));
  if(path==='/party/state'&&request.method==='GET')return json(await planningState(env,row,proof));
  if(path==='/party/consent'&&request.method==='POST'){
   const input=workspaceObject(await workspaceJson(request),['consentVersion','expectedPartyId']);if(uuid(input.expectedPartyId)!==proof.party.id)throw new WorkspaceError('party_changed',409);return json(await consentParty(env,row,proof,input.consentVersion));
  }
  if(path==='/party/settings'&&request.method==='POST'){
   const input=workspaceObject(await workspaceJson(request),['expectedNamespaceId','expectedRevision','membersCanQueue']);return json(await partySettings(env,row,proof,input));
  }
  if(path==='/party/planning'&&request.method==='DELETE'){
   const input=workspaceObject(await workspaceJson(request),['expectedNamespaceId','expectedRevision','confirmNamespaceId']);if(input.confirmNamespaceId===undefined)throw new WorkspaceError('confirmation_required',409);return json(await partySettings(env,row,proof,input));
  }
  throw new WorkspaceError('not_found',404);
 }
 if (path === "/workspace" && request.method === "GET") return json({ document: JSON.parse(row.document_json), storeVersion: row.store_version, executorEnabled: Boolean(row.executor_enabled), executorAvailable: Boolean(env.EXECUTOR_ENABLED) });
 if (path === "/export" && request.method === "GET") return snapshot(env, row);
 if (path === "/task-api" && request.method === "POST") {
  const body=workspaceObject(await workspaceJson(request,65536),["tool","arguments"]);
  return json(await taskApi(env,row,body.tool as TaskTool,body.arguments,cmd=>command(new Request("https://workspace.internal/commands",{method:"POST",body:JSON.stringify(cmd)}),env,row,true)));
 }
 if (path === "/handoff" && request.method === "POST") return handoff(request,env,row);
 if (path === "/executor" && request.method === "POST") {
  const body=workspaceObject(await workspaceJson(request),["enabled","consentVersion"]);
  if (typeof body.enabled!=="boolean" || body.enabled && body.consentVersion!=="executor-v1") throw new WorkspaceError("executor_consent_required",403);
  if (body.enabled && !env.EXECUTOR_ENABLED) throw new WorkspaceError("executor_unavailable",503);
  const changed=await env.WORKSPACE_DB.prepare("UPDATE workspaces SET executor_enabled=? WHERE profile_id=? AND access_epoch=? AND active=1").bind(Number(body.enabled),row.profile_id,row.access_epoch).run();
  if (!changed.meta.changes) throw new WorkspaceError("connection_changed",409);
  return json({executorEnabled:body.enabled,executorAvailable:Boolean(env.EXECUTOR_ENABLED)});
 }
 if (path === "/commands" && request.method === "POST") return command(request, env, row);
 if (["/projection/preview","/projection/adopt"].includes(path) && request.method === "POST") {
  const body=workspaceObject(await workspaceJson(request),path.endsWith("preview")?["taskId","habiticaTaskId"]:["taskId","habiticaTaskId","expectedWorkspaceId","expectedAccessEpoch","expectedRevision","expectedFingerprint"]);
  const taskId=uuid(body.taskId); const externalId=uuid(body.habiticaTaskId); const document=JSON.parse(row.document_json);
  const task=document.tasks.find((task: {id:string})=>task.id===taskId); if (!task) throw new WorkspaceError("task_unavailable",422);
  const response=await env.AUTH_PROJECTION.fetch("https://projection.internal/operation",{method:"POST",headers:{"content-type":"application/json"},body:JSON.stringify({profileId:row.profile_id,connectionId:row.connection_id,operation:"inspect",arguments:{selector:externalId},automated:false})});
  if (!response.ok) throw new WorkspaceError("projection_preview_unavailable",response.status===401?401:503);
  const observed=await response.json<{task: import("../../shared/projection").RemoteTask|null}>();
  if (!observed.task) throw new WorkspaceError("external_deleted",409);
  const pending=await env.WORKSPACE_DB.prepare("SELECT entry_json FROM workspace_commands WHERE profile_id=?").bind(row.profile_id).all<{entry_json:string}>();
  const unresolved=pending.results.map(row=>JSON.parse(row.entry_json)).filter(entry=>entry.command.taskId===taskId && !["notRequested","confirmed","cancelled"].includes(entry.projectionState));
  if (path.endsWith("preview")) return json({workspaceId:row.workspace_id,accessEpoch:row.access_epoch,task,external:observed.task,expectedRevision:document.revision,pendingCommandCount:unresolved.length});
  if (unresolved.length || body.expectedWorkspaceId!==row.workspace_id || body.expectedAccessEpoch!==row.access_epoch || body.expectedRevision!==document.revision || typeof body.expectedFingerprint!=="string") throw new WorkspaceError("adoption_conflict",409);
  const adopted=adoptProjection(document,taskId,observed.task,body.expectedFingerprint); const serialized=JSON.stringify(adopted);
  const archiveBytes=row.archive_bytes-bytes(row.document_json)+bytes(serialized);
  if (bytes(serialized)>1024*1024 || archiveBytes>8*1024*1024 || row.store_version===Number.MAX_SAFE_INTEGER) throw new WorkspaceError("workspace_capacity",413);
  const updated=await env.WORKSPACE_DB.prepare("UPDATE workspaces SET document_json=?,archive_bytes=?,store_version=store_version+1,commit_tag=?,updated_at_utc=? WHERE profile_id=? AND access_epoch=? AND store_version=? AND active=1")
   .bind(serialized,archiveBytes,crypto.randomUUID(),new Date().toISOString(),row.profile_id,row.access_epoch,row.store_version).run();
  if (!updated.meta.changes) throw new WorkspaceError("stale_revision",409);
  return json({adopted:true,taskId,habiticaTaskId:observed.task.id,revision:adopted.revision});
 }
 if (path === "/disconnect" && request.method === "POST") {
  const stopped = await env.WORKSPACE_DB.batch([env.WORKSPACE_DB.prepare("UPDATE workspaces SET active=0,executor_enabled=0 WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1").bind(row.profile_id,row.connection_id,row.access_epoch),env.WORKSPACE_DB.prepare("DELETE FROM workspace_sessions WHERE profile_id=? AND access_epoch=?").bind(row.profile_id,row.access_epoch)]);
  if (!stopped[0].meta.changes) throw new WorkspaceError("unauthorized", 401);
  return json({ disconnected: true, retained: true });
 }
 if (path === "/workspace" && request.method === "DELETE") {
  const body = workspaceObject(await workspaceJson(request), ["confirmWorkspaceId"]);
  if (uuid(body.confirmWorkspaceId) !== row.workspace_id) throw new WorkspaceError("confirmation_required", 409);
  const deleted = await env.WORKSPACE_DB.prepare("DELETE FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1").bind(row.profile_id,row.connection_id,row.access_epoch).run();
  if (!deleted.meta.changes) throw new WorkspaceError("unauthorized", 401);
  return json({ deleted: true });
 }
 throw new WorkspaceError("not_found", 404);
}
export class WorkspaceCommandService extends WorkerEntrypoint<WorkspaceEnv> {
 async fetch(request:Request):Promise<Response>{
  try{
   if(!this.env.WORKSPACE_ENABLED||request.method!=="POST")throw new WorkspaceError("workspace_unavailable",503);
   const body=workspaceObject(await workspaceJson(request,65536),["tool","arguments"]);
   if(typeof body.tool!=="string"||!Object.hasOwn(taskToolScopes,body.tool)&&!Object.hasOwn(partyToolScopes,body.tool)&&!Object.hasOwn(spellToolScopes,body.tool)&&!Object.hasOwn(cronToolScopes,body.tool))throw new WorkspaceError("invalid_tool",422);
   const verified=await this.env.AUTH_IDENTITY.fetch("https://identity.internal/mcp",{method:"POST",headers:{authorization:request.headers.get("authorization")??""},signal:AbortSignal.timeout(5000)});
   if(!verified.ok)throw new WorkspaceError("unauthorized",401);
   const who=await verified.json<Identity&{scopes:string[];grantEpoch:string}>();
   const requiredScope=Object.hasOwn(partyToolScopes,body.tool)?partyToolScopes[body.tool as PartyTool]:taskToolScopes[body.tool as TaskTool];
   const required=Object.hasOwn(cronToolScopes,body.tool)?cronToolScopes[body.tool as CronTool]:Object.hasOwn(spellToolScopes,body.tool)?spellToolScopes[body.tool as SpellTool]:['profile:read',requiredScope];
   if(!Array.isArray(who.scopes)||required.some(scope=>!who.scopes.includes(scope)))throw new WorkspaceError("insufficient_scope",403);
   let row=await this.env.WORKSPACE_DB.prepare("SELECT * FROM workspaces WHERE profile_id=? AND connection_id=? AND active=1").bind(who.profileId,who.connectionId).first<WorkspaceRow>();
   if(!row)throw new WorkspaceError("workspace_consent_required",403);
   row=await mcpEpoch(this.env,row,who.grantEpoch);
   if(Object.hasOwn(cronToolScopes,body.tool))return json(await cronApi(this.env,row,body.tool as CronTool,body.arguments,request.headers.get('authorization')??''));
   if(Object.hasOwn(spellToolScopes,body.tool))return json(await spellApi(this.env,row,body.tool as SpellTool,body.arguments,request.headers.get('authorization')??''));
   if(Object.hasOwn(partyToolScopes,body.tool))return json(await partyApi(this.env,row,body.tool as PartyTool,body.arguments,request.headers.get("authorization")??undefined));
   return json(await taskApi(this.env,row,body.tool as TaskTool,body.arguments,cmd=>command(new Request("https://workspace.internal/commands",{method:"POST",body:JSON.stringify(cmd)}),this.env,row,true)));
  }catch(error){return json({error:error instanceof WorkspaceError?error.code:"command_outcome_unknown",...(error instanceof PartyApiFailure?{rate:error.rate}:{}),savedInCompanion:error instanceof WorkspaceError?false:null,outcomeUnknown:!(error instanceof WorkspaceError)},error instanceof WorkspaceError?error.status:503);}
 }
}
export default {
 async fetch(request: Request, env: WorkspaceEnv): Promise<Response> {
  const url = new URL(request.url); const config = resourceConfiguration(env.ENVIRONMENT); const origin = request.headers.get("origin"); let response: Response;
  try {
   if (url.protocol !== "https:" && !(env.ENVIRONMENT === "local" && ["localhost", "127.0.0.1", "[::1]"].includes(url.hostname))) throw new WorkspaceError("invalid_transport", 403);
   if (origin && origin !== config.pwaOrigin) throw new WorkspaceError("invalid_origin", 403);
   response = request.method === "OPTIONS" ? new Response(null, { status: 204, headers }) : await handle(request, env);
  } catch (error) { response = json({ error: error instanceof WorkspaceError ? error.code : "workspace_unavailable" }, error instanceof WorkspaceError ? error.status : 503); }
  if (origin === config.pwaOrigin) { response.headers.set("access-control-allow-origin", origin); response.headers.set("access-control-allow-methods", "GET, POST, DELETE, OPTIONS"); response.headers.set("access-control-allow-headers", "content-type, authorization"); response.headers.set("vary", "Origin"); }
  return response;
 },
 async scheduled(_event: ScheduledController,env: WorkspaceEnv): Promise<void> { await purgeExpiredPreparations(env); await executeScheduled(env); },
} satisfies ExportedHandler<WorkspaceEnv>;
