import { activeProfile, managedProfile, VaultError } from "./vault";
import { deriveProfileId, tokenHash } from "./cryptography";

const alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
export async function attempts(env: AuthEnv, bucket: string, limit: number, windowSeconds: number): Promise<void> {
  const now = new Date();
  const window = Math.floor(now.getTime() / (windowSeconds * 1000));
  const key = `${bucket}:${window}`;
  const expires = new Date((window + 1) * windowSeconds * 1000).toISOString();
  const row = await env.HABITICA_INTEGRATION_DB.prepare(`INSERT INTO authorization_attempts(bucket,attempts,expires_at_utc) VALUES(?,1,?)
    ON CONFLICT(bucket) DO UPDATE SET attempts=attempts+1 WHERE attempts < ? RETURNING attempts`)
    .bind(key, expires, limit).first();
  if (!row) throw new VaultError("try_later", 429);
  await env.HABITICA_INTEGRATION_DB.prepare("DELETE FROM authorization_attempts WHERE expires_at_utc < ?").bind(now.toISOString()).run();
}
export async function networkAttempts(request: Request, env: AuthEnv): Promise<void> {
  const ip = request.headers.get("cf-connecting-ip") ?? (env.ENVIRONMENT === "local" ? "local" : null);
  if (!ip) throw new VaultError("try_later", 429);
  const bucket = await deriveProfileId(`authorization-network:${ip}`, env.PROFILE_ID_KEY);
  await attempts(env, bucket, 15, 60);
}
export async function ticketRoute(request: Request, env: AuthEnv): Promise<Response | null> {
  const path = new URL(request.url).pathname.match(/^\/profiles\/(prf_[A-Za-z0-9_-]+)\/linking-ticket$/);
  if (!path || request.method !== "POST") return null;
  const profile = await managedProfile(request, env, path[1]);
  await networkAttempts(request, env);
  await attempts(env, "ticket:" + profile.profile_id, 5, 600);
  const bytes = crypto.getRandomValues(new Uint8Array(12));
  const code = Array.from(bytes, byte => alphabet[byte & 31]).join("");
  const id = crypto.randomUUID();
  const now = new Date().toISOString();
  const expires = new Date(Date.now() + 600000).toISOString();
  await env.HABITICA_INTEGRATION_DB.prepare("INSERT INTO linking_tickets(ticket_id,profile_id,connection_id,code_hash,created_at_utc,expires_at_utc) VALUES(?,?,?,?,?,?)")
    .bind(id, profile.profile_id, profile.connection_id, await tokenHash(code), now, expires).run();
  await env.HABITICA_INTEGRATION_DB.prepare("DELETE FROM linking_tickets WHERE expires_at_utc < ?").bind(now).run();
  return Response.json({ ticketId: id, code, expiresAtUtc: expires }, { headers: { "cache-control": "no-store" } });
}
export async function consumeTicket(env: AuthEnv, code: string) {
  const normalized = code.trim().replace(/-/g, "").toUpperCase();
  if (normalized.length !== 12 || Array.from(normalized).some(character => !alphabet.includes(character))) throw new VaultError("invalid_linking_code", 400);
  const hash = await tokenHash(normalized);
  const now = new Date().toISOString();
  const row = await env.HABITICA_INTEGRATION_DB.prepare(`UPDATE linking_tickets SET consumed_at_utc=?
    WHERE code_hash=? AND consumed_at_utc IS NULL AND expires_at_utc > ? AND failed_attempts < 5
    RETURNING profile_id,connection_id`).bind(now, hash, now).first<{ profile_id: string; connection_id: string }>();
  if (!row) {
    await env.HABITICA_INTEGRATION_DB.prepare("UPDATE linking_tickets SET failed_attempts=failed_attempts+1 WHERE code_hash=?").bind(hash).run();
    throw new VaultError("invalid_linking_code", 400);
  }
  return activeProfile(env, row.profile_id, row.connection_id);
}
