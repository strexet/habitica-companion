import {upstreamBudget,upstreamFetch,observeUpstream,TrafficLimit,trafficFailure,charge} from './traffic';
import { deriveProfileId, encryptCredential, decryptCredential, tokenHash, newManagementToken, tokenMatches, validateKeySeparation, type HabiticaCredential } from "./cryptography";
import { resourceConfiguration } from "../../shared/resources";
import {clearPushContext} from './push-subscriptions';

export const consentVersion = "integration-v1";
const headers = { "cache-control": "no-store", "x-content-type-options": "nosniff" };
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export interface IntegrationProfile {
  profile_id: string;
  credential_ciphertext: string | null;
  credential_iv: string | null;
  credential_key_version: string | null;
  credential_schema_version: number | null;
  display_name_cache: string | null;
  revoked_at_utc: string | null;
  management_token_hash: string | null;
  generation: number;
  grant_epoch:string;
  last_used_at_utc:string|null;
  last_verified_at_utc:string;
  connection_id: string;
}
export class VaultError extends Error {
  constructor(public readonly code: string, public readonly status: number) { super(code); }
}
export async function boundedText(input: Request | Response, limit: number): Promise<string> {
  const reader = input.body?.getReader();
  if (!reader) throw new VaultError("invalid_request", 400);
  const chunks: Uint8Array[] = [];
  let size = 0;
  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      size += value.byteLength;
      if (size > limit) { await reader.cancel(); throw new VaultError("payload_too_large", 413); }
      chunks.push(value);
    }
  } finally { reader.releaseLock(); }
  const bytes = new Uint8Array(size);
  let offset = 0;
  for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.length; }
  return new TextDecoder().decode(bytes);
}
export async function boundedJson(input: Request | Response, limit: number): Promise<unknown> {
  try { return JSON.parse(await boundedText(input, limit)); }
  catch (error) { if (error instanceof VaultError) throw error; throw new VaultError("invalid_request", 400); }
}
function credentialKeys(env: AuthEnv) {
  return { "1": env.CREDENTIAL_KEY_V1, "2": env.CREDENTIAL_KEY_V2 };
}
export { credentialKeys };
export async function loadProfile(env: AuthEnv, id: string): Promise<IntegrationProfile | null> {
  return env.HABITICA_INTEGRATION_DB.prepare("SELECT * FROM integration_profiles WHERE profile_id = ?").bind(id).first<IntegrationProfile>();
}
export async function activeProfile(env: AuthEnv, id: string, connectionId?: string): Promise<IntegrationProfile> {
  const profile = await loadProfile(env, id);
  if (!profile || profile.revoked_at_utc || !profile.credential_ciphertext || (connectionId !== undefined && profile.connection_id !== connectionId)) {
    throw new VaultError("unauthorized", 401);
  }
  return profile;
}
async function verifyHabitica(credential: HabiticaCredential, env: AuthEnv): Promise<string | null> {
  if (!env.HABITICA_X_CLIENT_HEADER) throw new VaultError("integration_unavailable", 503);
  await upstreamBudget(env);
  const response = await upstreamFetch(env,"https://habitica.com/api/v3/user?userFields=_id,profile", {
    headers: { "x-api-user": credential.habiticaUserId, "x-api-key": credential.apiToken, "x-client": env.HABITICA_X_CLIENT_HEADER, accept: "application/json" },
    signal: AbortSignal.timeout(10000),
  });
  await observeUpstream(env,response);
  if(response.status===429)await upstreamBudget(env);
  if (response.status === 401 || response.status === 403) throw new VaultError("invalid_credentials", 401);
  if (!response.ok) throw new VaultError("habitica_unavailable", 502);
  const document = await boundedJson(response, 1048576) as { success?: boolean; data?: { _id?: string; id?: string; profile?: { name?: string } } };
  const id = document.data?._id ?? document.data?.id;
  if (document.success !== true || typeof id !== "string" || id.toLowerCase() !== credential.habiticaUserId) throw new VaultError("invalid_credentials", 401);
  const name = document.data?.profile?.name;
  return typeof name === "string" ? name.slice(0, 256) : null;
}
async function enroll(request: Request, env: AuthEnv): Promise<Response> {
  if (!Boolean(env.ENROLLMENT_ENABLED)) throw new VaultError("unauthorized", 401);
  if (!request.headers.get("content-type")?.startsWith("application/json")) throw new VaultError("invalid_request", 400);
  const parsed = await boundedJson(request, 16384);
  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) throw new VaultError("invalid_request", 400);
  const body = parsed as Partial<HabiticaCredential> & { consentVersion?: string };
  if (body.consentVersion !== consentVersion || typeof body.habiticaUserId !== "string" || !uuid.test(body.habiticaUserId) || typeof body.apiToken !== "string" || !body.apiToken.trim() || body.apiToken.length > 1024) throw new VaultError("invalid_request", 400);
  const credential = { habiticaUserId: body.habiticaUserId.toLowerCase(), apiToken: body.apiToken };
  validateKeySeparation(env.PROFILE_ID_KEY, credentialKeys(env));
  const name = await verifyHabitica(credential, env);
  const profileId = await deriveProfileId(credential.habiticaUserId, env.PROFILE_ID_KEY);
  await charge(env,"enrollment:profile:"+profileId,5);
  const envelope = await encryptCredential(profileId, credential, env.CREDENTIAL_CURRENT_KEY_VERSION, credentialKeys(env));
  const previous = await loadProfile(env, profileId);
  // A blocked profile keeps the management credential until cross-service cleanup completes.
  if(previous?.revoked_at_utc&&previous.management_token_hash)throw new VaultError('cleanup_pending',409);
  let connectionId = crypto.randomUUID();
  if (previous && !previous.revoked_at_utc && previous.credential_ciphertext && previous.credential_iv && previous.credential_key_version && previous.credential_schema_version === 1) {
    const stored = await decryptCredential(profileId, { ciphertext: previous.credential_ciphertext, iv: previous.credential_iv, keyVersion: previous.credential_key_version, schemaVersion: 1 }, credentialKeys(env));
    if (stored.habiticaUserId === credential.habiticaUserId && await tokenMatches(credential.apiToken, await tokenHash(stored.apiToken))) connectionId = previous.connection_id;
  }
  const managementToken = newManagementToken();
  const hash = await tokenHash(managementToken);
  const now = new Date().toISOString();
  const upsert = env.HABITICA_INTEGRATION_DB.prepare(`INSERT INTO integration_profiles
    (profile_id, credential_ciphertext, credential_iv, credential_key_version, credential_schema_version,
     display_name_cache, created_at_utc, updated_at_utc, last_verified_at_utc, management_token_hash,
     management_token_rotated_at_utc, consent_version, consented_at_utc, connection_id)
    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    ON CONFLICT(profile_id) DO UPDATE SET credential_ciphertext=excluded.credential_ciphertext,
    credential_iv=excluded.credential_iv, credential_key_version=excluded.credential_key_version,
    credential_schema_version=excluded.credential_schema_version, display_name_cache=excluded.display_name_cache,
    updated_at_utc=excluded.updated_at_utc, last_verified_at_utc=excluded.last_verified_at_utc,
    management_token_hash=excluded.management_token_hash, management_token_rotated_at_utc=excluded.management_token_rotated_at_utc,
    consent_version=excluded.consent_version, consented_at_utc=excluded.consented_at_utc,
    revoked_at_utc=NULL, generation=integration_profiles.generation+1, connection_id=excluded.connection_id
    WHERE integration_profiles.generation=?`)
    .bind(profileId, envelope.ciphertext, envelope.iv, envelope.keyVersion, envelope.schemaVersion, name, now, now, now, hash, now, consentVersion, now, connectionId, previous?.generation ?? 0);
  const writes = [upsert];
  if (Boolean(env.OAUTH_ENABLED) || Boolean(env.WORKSPACE_ENABLED)) writes.push(env.HABITICA_INTEGRATION_DB.prepare(`UPDATE integration_read_state SET credential_repair_required=0
    WHERE profile_id=? AND connection_id=? AND EXISTS(SELECT 1 FROM integration_profiles WHERE profile_id=? AND management_token_hash=?)`)
    .bind(profileId, connectionId, profileId, hash));
  const results = await env.HABITICA_INTEGRATION_DB.batch(writes);
  if (!results[0].meta.changes) throw new VaultError("enrollment_changed", 409);
  if(previous&&previous.connection_id!==connectionId)await clearPushContext(env,profileId,previous.connection_id,false);
  return Response.json({ profileId, managementToken }, { status: 201, headers });
}
export async function managedProfile(request: Request, env: AuthEnv, profileId: string, applyBudget = true): Promise<IntegrationProfile> {
  const token = request.headers.get("authorization")?.match(/^Bearer (\S+)$/)?.[1];
  const profile = await loadProfile(env, profileId);
  if (!token || !profile || !await tokenMatches(token, profile.management_token_hash)) throw new VaultError("unauthorized", 401);
  if(applyBudget)await charge(env,"management:profile:"+profileId,30);
  return activeProfile(env, profileId);
}
async function manage(request: Request, env: AuthEnv, profileId: string, cleanup?: (id: string, connectionId: string, deleteProfile:boolean) => Promise<void>): Promise<Response> {
  const token = request.headers.get("authorization")?.match(/^Bearer (\S+)$/)?.[1];
  const profile = await loadProfile(env, profileId);
  if (!token || !profile || !await tokenMatches(token, profile.management_token_hash)) throw new VaultError("unauthorized", 401);
  await charge(env,"management:recovery:"+profileId,20);
  const now = new Date().toISOString();
  const blocked = await env.HABITICA_INTEGRATION_DB.prepare(`UPDATE integration_profiles SET credential_ciphertext=NULL,
    credential_iv=NULL, credential_key_version=NULL, credential_schema_version=NULL,
    revoked_at_utc=COALESCE(revoked_at_utc,?), updated_at_utc=?,generation=generation+1 WHERE profile_id=? AND management_token_hash=?`)
    .bind(now, now, profileId, profile.management_token_hash).run();
  if (!blocked.meta.changes) throw new VaultError("unauthorized", 401);
  try { if (cleanup) await cleanup(profileId, profile.connection_id,request.method==='DELETE'); }
  catch { return Response.json({ accessBlocked: true, pendingCleanup: true }, { status: 202, headers }); }
  if (request.method === "DELETE") {
    const result = await env.HABITICA_INTEGRATION_DB.prepare("DELETE FROM integration_profiles WHERE profile_id = ? AND management_token_hash = ?").bind(profileId, profile.management_token_hash).run();
    if (!result.meta.changes) throw new VaultError("unauthorized", 401);
    return Response.json({ deleted: true }, { headers });
  }
  const result = await env.HABITICA_INTEGRATION_DB.prepare(`UPDATE integration_profiles SET credential_ciphertext=NULL,
    credential_iv=NULL, credential_key_version=NULL, credential_schema_version=NULL, management_token_hash=NULL,
    revoked_at_utc=?, updated_at_utc=?, generation=generation+1 WHERE profile_id=? AND management_token_hash=?`)
    .bind(now, now, profileId, profile.management_token_hash).run();
  if (!result.meta.changes) throw new VaultError("unauthorized", 401);
  return Response.json({ disconnected: true }, { headers });
}
export async function vaultRoute(request: Request, env: AuthEnv, cleanup?: (id: string, connectionId: string, deleteProfile:boolean) => Promise<void>): Promise<Response | null> {
  const url = new URL(request.url);
  const management = url.pathname.match(/^\/profiles\/(prf_[A-Za-z0-9_-]+)(\/disconnect)?$/);
  if (!(url.pathname === "/enroll" && request.method === "POST") && !(management && ((!management[2] && request.method === "DELETE") || (management[2] && request.method === "POST")))) return null;
  try {
    if (url.protocol !== "https:" && !(env.ENVIRONMENT === "local" && ["localhost", "127.0.0.1", "[::1]"].includes(url.hostname))) throw new VaultError("invalid_transport", 403);
    const origin = request.headers.get("origin");
    const config = resourceConfiguration(env.ENVIRONMENT, env);
    if (origin && origin !== config.issuer && origin !== config.pwaOrigin) throw new VaultError("invalid_origin", 403);
    return management ? await manage(request, env, management[1], cleanup) : await enroll(request, env);
  } catch (error) {
    const quota=trafficFailure(error);if(quota)return quota;
    const safe = error instanceof VaultError ? error : new VaultError("integration_unavailable", 503);
    return Response.json({ error: safe.code }, { status: safe.status, headers });
  }
}
