import {egressGrant, egressId, egressKeys, egressMac, egressMatches, exactObject} from './callback-egress-protocol';
import type {PushEnv} from './push-cryptography';
import {resourceConfiguration} from '../../shared/resources';

export interface CallbackEgressEnv {
  CALLBACK_EGRESS_ENABLED?: boolean;
  CALLBACK_EGRESS_AUDIENCE?: string;
  CALLBACK_EGRESS_GRANT_KEY?: string;
  CALLBACK_EGRESS_CLAIM_KEY?: string;
}
const headers = {'cache-control':'no-store','x-content-type-options':'nosniff'};
async function claimBody(request: Request): Promise<unknown> {
  const reader = request.body?.getReader();
  if (!reader) throw Error();
  let expired = false, size = 0;
  const chunks: Uint8Array[] = [];
  const timer = setTimeout(() => { expired = true; void reader.cancel().catch(() => {}); }, 5000);
  try {
    while (true) {
      const {done,value} = await reader.read();
      if (expired) throw Error();
      if (done) break;
      size += value.byteLength;
      if (size > 4096) throw Error();
      chunks.push(value);
    }
    const bytes = new Uint8Array(size); let offset = 0;
    for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.length; }
    return JSON.parse(new TextDecoder().decode(bytes));
  } finally { clearTimeout(timer); await reader.cancel().catch(() => {}); reader.releaseLock(); }
}

// Narrow, service-authenticated redemption. No callback URL/body/key or user authority is accepted.
// Auth subscription orchestration must separately validate consent/resource ownership before minting.
export async function callbackEgressClaim(request: Request, env: AuthEnv & CallbackEgressEnv): Promise<Response | null> {
  if (new URL(request.url).pathname !== '/callback-egress/claim') return null;
  try {
    if ((env as CallbackEgressEnv).CALLBACK_EGRESS_ENABLED !== true || request.method !== 'POST' || request.headers.has('origin') ||
        request.headers.get('content-type') !== 'application/json' || new URL(request.url).protocol !== 'https:') throw Error();
    const grantKey = env.CALLBACK_EGRESS_GRANT_KEY!, claimKey = env.CALLBACK_EGRESS_CLAIM_KEY!;
    egressKeys(grantKey, claimKey);
    // Do not reuse existing credential or Web Push keys for this trust boundary.
    const existing = [env.PROFILE_ID_KEY, env.CREDENTIAL_KEY_V1, env.CREDENTIAL_KEY_V2,
      (env as PushEnv).PUSH_VAULT_KEY_V1, (env as PushEnv).PUSH_VAULT_KEY_V2];
    if (existing.includes(grantKey) || existing.includes(claimKey)) throw Error();
    const body = exactObject(await claimBody(request), ['grant','mac','nonce','proof']);
    const now = Date.now(), issuer = resourceConfiguration(env.ENVIRONMENT, env).issuer;
    if (request.url !== issuer + '/callback-egress/claim') throw Error();
    const grant = egressGrant(body.grant, issuer, env.CALLBACK_EGRESS_AUDIENCE!, now), nonce = egressId(body.nonce);
    if (!egressMatches(egressMac(grantKey, 'grant', grant), body.mac) ||
        !egressMatches(egressMac(claimKey, 'claim', grant, nonce), body.proof)) throw Error();
    // Retain fences beyond grant expiry. Capacity is global and atomic, not an in-memory rate gate.
    await env.HABITICA_INTEGRATION_DB.prepare('DELETE FROM integration_callback_egress_claims WHERE request_id IN (SELECT request_id FROM integration_callback_egress_claims WHERE expires_at_ms<=? LIMIT 128)').bind(now - 30 * 86400000).run();
    const result = await env.HABITICA_INTEGRATION_DB.prepare(`INSERT OR IGNORE INTO integration_callback_egress_claims(request_id,grant_hash,expires_at_ms,claimed_at_ms)
      SELECT ?,?,?,? WHERE (SELECT COUNT(*) FROM integration_callback_egress_claims)<1000`)
      .bind(grant.requestId, egressMac(grantKey, 'grant', grant), grant.expiresAt * 1000, now).run();
    if (result.meta.changes !== 1 || grant.expiresAt * 1000 <= Date.now()) return Response.json({error:'egress_claim_unavailable'}, {status:409, headers});
    return Response.json({requestId:grant.requestId, payloadHash:grant.payloadHash, nonce, claimed:true,
      proof:egressMac(claimKey, 'claimed', grant, nonce)}, {headers});
  } catch {
    return Response.json({error:'egress_claim_unavailable'}, {status:503, headers});
  }
}
