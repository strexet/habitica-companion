import {upstreamBudget,observeUpstream,TrafficLimit,trafficFailure} from './traffic';
import { activeProfile, boundedJson, credentialKeys, VaultError } from "./vault";
import { decryptCredential } from "./cryptography";
import { validatedToken } from "./oauth";
import { readInput, readScopes, type ReadTool, type ReadInput } from "../../shared/read-tools";
import { resourceConfiguration } from "../../shared/resources";

type ObjectValue = Record<string, unknown>;
const object = (value: unknown): ObjectValue => value && typeof value === "object" && !Array.isArray(value) ? value as ObjectValue : {};
const text = (value: unknown, length = 256) => typeof value === "string" && value.trim() ? value.slice(0, length) : undefined;
const number = (value: unknown) => typeof value === "number" && Number.isFinite(value) ? value : undefined;
const boolean = (value: unknown) => typeof value === "boolean" ? value : undefined;
const headers = { "cache-control": "no-store" };
export class ReadFailure extends Error {
  constructor(public code: string, public status: number, public rate: Record<string, unknown> = {}) { super(code); }
}
function rateMetadata(response: Response) {
  const rate: Record<string, unknown> = {};
  for (const [header, key] of [["x-ratelimit-limit", "limit"], ["x-ratelimit-remaining", "remaining"], ["x-ratelimit-reset", "resetUnixSeconds"]]) {
    const value = response.headers.get(header);
    if (value && /^\d{1,12}$/.test(value)) rate[key] = Number(value);
  }
  const retry = response.headers.get("retry-after");
  if (retry) {
    const seconds = /^\d{1,9}$/.test(retry) ? Number(retry) : Math.ceil((Date.parse(retry) - Date.now()) / 1000);
    if (Number.isFinite(seconds) && seconds >= 0) rate.retryAfterSeconds = seconds;
  }
  return rate;
}
export async function upstream(env: AuthEnv, profileId: string, connectionId: string, path: string) {
  const state = await env.HABITICA_INTEGRATION_DB.prepare("SELECT blocked_until_utc,credential_repair_required FROM integration_read_state WHERE profile_id=? AND connection_id=?")
    .bind(profileId, connectionId).first<{ blocked_until_utc: string | null; credential_repair_required: number }>();
  if (state?.credential_repair_required) throw new ReadFailure("credential_repair_required", 401);
  if (state?.blocked_until_utc && Date.parse(state.blocked_until_utc) > Date.now()) throw new ReadFailure("rate_limited", 429, { retryAfterSeconds: Math.ceil((Date.parse(state.blocked_until_utc) - Date.now()) / 1000) });
  const profile = await activeProfile(env, profileId, connectionId);
  if (profile.credential_schema_version !== 1) throw new ReadFailure("service_unavailable", 503);
  const credential = await decryptCredential(profileId, { ciphertext: profile.credential_ciphertext!, iv: profile.credential_iv!, keyVersion: profile.credential_key_version!, schemaVersion: 1 }, credentialKeys(env));
  if (!env.HABITICA_X_CLIENT_HEADER) throw new ReadFailure("service_unavailable", 503);
  try{await upstreamBudget(env,profileId);}catch(error){if(error instanceof TrafficLimit)throw new ReadFailure("rate_limited",429,{retryAfterSeconds:error.retryAfterSeconds});throw error;}
  const response = await fetch("https://habitica.com/api/v3/" + path, { method: "GET", redirect: "manual", headers: { "x-api-user": credential.habiticaUserId, "x-api-key": credential.apiToken, "x-client": env.HABITICA_X_CLIENT_HEADER, accept: "application/json" }, signal: AbortSignal.timeout(10000) });
  await observeUpstream(env,response,profileId);
  const rate = rateMetadata(response);
  const retry = Number(rate.retryAfterSeconds);
  const reset = Number(rate.resetUnixSeconds) * 1000;
  let blocked: string | null = null;
  if (response.status === 429 || rate.remaining === 0) {
    const end = Math.max(Date.now() + (Number.isFinite(retry) ? retry * 1000 : 60000), Number.isFinite(reset) ? reset : 0);
    blocked = new Date(end).toISOString();
  }
  await env.HABITICA_INTEGRATION_DB.prepare(`INSERT INTO integration_read_state(profile_id,connection_id,blocked_until_utc,credential_repair_required) VALUES(?,?,?,?)
    ON CONFLICT(profile_id,connection_id) DO UPDATE SET blocked_until_utc=CASE WHEN integration_read_state.blocked_until_utc IS NULL OR excluded.blocked_until_utc > integration_read_state.blocked_until_utc THEN excluded.blocked_until_utc ELSE integration_read_state.blocked_until_utc END,
    credential_repair_required=MAX(integration_read_state.credential_repair_required,excluded.credential_repair_required)`)
    .bind(profileId, connectionId, blocked, response.status === 401 || response.status === 403 ? 1 : 0).run();
  if (response.status === 401 || response.status === 403) throw new ReadFailure("credential_repair_required", 401, rate);
  if (response.status === 429) throw new ReadFailure("rate_limited", 429, rate);
  if (!response.ok) throw new ReadFailure("habitica_unavailable", 502, rate);
  const document = object(await boundedJson(response, 2097152));
  if (document.success !== true || document.data === undefined) throw new ReadFailure("invalid_upstream_response", 502, rate);
  return { data: document.data, rate };
}
function task(value: unknown) {
  const item = object(value); const id = text(item.id ?? item._id, 128); const type = text(item.type);
  if (!id || !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id) || !["habit", "daily", "todo", "reward"].includes(type ?? "")) return null;
  return { id, type: type!, text: typeof item.text === "string" ? item.text.slice(0, 2048) : "", completed: boolean(item.completed) ?? null, isDue: type === "daily" ? boolean(item.isDue) ?? null : null, provenance: "habitica-legacy-bootstrap" };
}
async function tasks(env: AuthEnv, profileId: string, connectionId: string, completed = false) {
  const result = await upstream(env, profileId, connectionId, "tasks/user?dueDate=" + encodeURIComponent(new Date().toISOString()) + (completed ? "&type=completedTodos" : ""));
  if (!Array.isArray(result.data)) throw new ReadFailure("invalid_upstream_response", 502, result.rate);
  return { tasks: result.data.map(task).filter(value => value !== null), rate: result.rate };
}
async function execute(env: AuthEnv, profileId: string, connectionId: string, tool: ReadTool, input: ReadInput) {
  const profile = await activeProfile(env, profileId, connectionId);
  if (tool === "get_profile") {
    const name = text(profile.display_name_cache);
    return { data: { id: profileId, ...(name ? { name, nickname: "Habitica — " + name } : {}) }, rate: {} };
  }
  if (tool === "get_account_status") {
    const result = await upstream(env, profileId, connectionId, "user");
    const user = object(result.data); const stats = object(user.stats); const party = object(user.party);
    const status: ObjectValue = { retrievedAtUtc: new Date().toISOString(), needsCron: boolean(user.needsCron) ?? null };
    const klass = text(stats.class, 32); if (klass) status.class = klass;
    for (const [source, target] of [["lvl", "level"], ["hp", "hp"], ["maxHealth", "maxHp"], ["mp", "mp"], ["maxMP", "maxMp"], ["exp", "xp"], ["toNextLevel", "nextLevelTarget"], ["gp", "gp"]]) {
      const value = number(stats[source]); if (value !== undefined) status[target] = value;
    }
    const partyId = text(party._id ?? party.id, 128); if (partyId) status.partyId = partyId;
    return { data: status, rate: result.rate };
  }
  const active = await tasks(env, profileId, connectionId);
  let items = active.tasks; let rate = active.rate; let completedCoverage = "excluded";
  if ((tool === "get_task" && !items.some(item => item.id.toLowerCase() === input.taskId!.toLowerCase())) || (tool === "list_tasks" && input.completed === true && (!input.type || input.type === "todo"))) {
    const recent = await tasks(env, profileId, connectionId, true);
    items = Array.from(new Map([...items, ...recent.tasks].map(item => [item.id, item])).values()); rate = recent.rate; completedCoverage = "30-most-recent-todos";
  }
  if (tool === "get_task") return { data: { task: items.find(item => item.id.toLowerCase() === input.taskId!.toLowerCase()) ?? null, provenance: "habitica-legacy-bootstrap", completedTodoCoverage: completedCoverage, retrievedAtUtc: new Date().toISOString() }, rate };
  const unknownDueCount = items.filter(item => item.type === "daily" && item.isDue === null).length;
  items = items.filter(item => (!input.type || item.type === input.type) && (input.completed === undefined || item.completed === input.completed)
    && (!input.dueOnly || (item.type === "daily" && item.isDue === true)) && (!input.query || item.text.toLowerCase().includes(input.query.toLowerCase())));
  const matchedCount = items.length;
  return { data: { tasks: items.slice(0, input.limit ?? 50), matchedCount, truncated: matchedCount > (input.limit ?? 50), unknownDueCount, completedTodoCoverage: completedCoverage, provenance: "habitica-legacy-bootstrap", retrievedAtUtc: new Date().toISOString() }, rate };
}
export async function readOperation(request: Request, env: AuthEnv): Promise<Response> {
  const correlationId = crypto.randomUUID(); const started = Date.now();
  let tool: ReadTool | undefined;
  try {
    const body = object(await boundedJson(request, 16384));
    if (Object.keys(body).some(key => !["tool", "arguments"].includes(key))) throw new ReadFailure("invalid_request", 400);
    const input = readInput(String(body.tool), body.arguments); tool = body.tool as ReadTool;
    const token = request.headers.get("authorization")?.match(/^Bearer (\S+)$/)?.[1];
    const context = token ? await validatedToken(env, resourceConfiguration(env.ENVIRONMENT, env).resource, token) : null;
    if (!context) throw new ReadFailure("unauthorized", 401);
    if (!context.scope?.includes(readScopes[tool])) throw new ReadFailure("insufficient_scope", 403);
    const result = await execute(env, context.props.profileId, context.props.connectionId, tool, input);
    // No task text, credentials, tokens or upstream bodies enter diagnostics.
    console.info(JSON.stringify({ correlationId, tool, route: tool === "get_profile" ? "profile-cache" : tool === "get_account_status" ? "account" : "tasks", durationMs: Date.now() - started, status: 200, rate: result.rate }));
    return Response.json({ data: result.data, correlationId, rate: result.rate }, { headers });
  } catch (error) {
    const failure = error instanceof ReadFailure ? error : new ReadFailure(error instanceof VaultError && error.status === 401 ? "unauthorized" : "service_unavailable", error instanceof VaultError ? error.status : 503);
    console.info(JSON.stringify({ correlationId, tool, durationMs: Date.now() - started, status: failure.status, rate: failure.rate }));
    return Response.json({ error: failure.code, correlationId, rate: failure.rate }, { status: failure.status, headers: { ...headers, ...(failure.status===429&&typeof failure.rate.retryAfterSeconds==="number"?{"retry-after":String(failure.rate.retryAfterSeconds)}:{}) } });
  }
}
