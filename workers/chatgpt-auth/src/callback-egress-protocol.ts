// Service authentication only. Webhook signing keys and callback vaults are separate.
import {createHash, createHmac, timingSafeEqual} from 'node:crypto';

export const egressVersion = 'callback-egress-v1';
export interface EgressGrant {
  version: typeof egressVersion;
  requestId: string;
  issuer: string;
  audience: string;
  operation: 'challenge' | 'event';
  payloadHash: string;
  issuedAt: number;
  expiresAt: number;
}
export function exactObject(value: unknown, fields: string[]): Record<string, unknown> {
  if (!value || typeof value !== 'object' || Array.isArray(value) ||
      Object.keys(value).length !== fields.length || Object.keys(value).some(key => !fields.includes(key))) throw Error('invalid_envelope');
  return value as Record<string, unknown>;
}
export function egressId(value: unknown): string {
  if (typeof value !== 'string' || !/^[a-f0-9]{8}-[a-f0-9]{4}-4[a-f0-9]{3}-[89ab][a-f0-9]{3}-[a-f0-9]{12}$/.test(value)) throw Error('invalid_identity');
  return value;
}
export function serviceUrl(value: unknown, originOnly = false): string {
  if (typeof value !== 'string' || value.length > 2048) throw Error('invalid_service');
  const url = new URL(value);
  if (url.protocol !== 'https:' || url.username || url.password || url.hash || url.search ||
      url.port || !/^[a-z0-9](?:[a-z0-9.-]*[a-z0-9])?$/.test(url.hostname) ||
      !url.hostname.includes('.') || (originOnly && url.origin !== value) || (!originOnly && url.href !== value)) throw Error('invalid_service');
  return value;
}
export function egressGrant(value: unknown, issuer: string, audience: string, now = Date.now()): EgressGrant {
  const v = exactObject(value, ['version','requestId','issuer','audience','operation','payloadHash','issuedAt','expiresAt']);
  if (v.version !== egressVersion || v.issuer !== serviceUrl(issuer, true) || v.audience !== serviceUrl(audience) ||
      !['challenge','event'].includes(String(v.operation)) || typeof v.payloadHash !== 'string' || !/^[a-f0-9]{64}$/.test(v.payloadHash) ||
      !Number.isSafeInteger(v.issuedAt) || !Number.isSafeInteger(v.expiresAt) ||
      Number(v.issuedAt) * 1000 > now || Number(v.expiresAt) * 1000 <= now ||
      Number(v.expiresAt) <= Number(v.issuedAt) || Number(v.expiresAt) - Number(v.issuedAt) > 30) throw Error('invalid_grant');
  return {version: egressVersion, requestId: egressId(v.requestId), issuer, audience,
    operation: v.operation as EgressGrant['operation'], payloadHash: v.payloadHash,
    issuedAt: Number(v.issuedAt), expiresAt: Number(v.expiresAt)};
}
export function egressHash(value: string | Uint8Array): string { return createHash('sha256').update(value).digest('hex'); }
function key(value: string): Buffer {
  if (typeof value !== 'string' || !/^[A-Za-z0-9_-]{43}$/.test(value)) throw Error('configuration_unavailable');
  const bytes = Buffer.from(value, 'base64url');
  if (bytes.length !== 32 || bytes.toString('base64url') !== value) throw Error('configuration_unavailable');
  return bytes;
}
export function egressKeys(grantKey: string, claimKey: string): void {
  if (timingSafeEqual(key(grantKey), key(claimKey))) throw Error('configuration_unavailable');
}
export function egressMac(secret: string, purpose: 'grant' | 'claim' | 'claimed', grant: EgressGrant, nonce = ''): string {
  return createHmac('sha256', key(secret)).update(`${egressVersion}\n${purpose}\n${JSON.stringify(grant)}\n${nonce}`).digest('hex');
}
export function egressMatches(expected: string, actual: unknown): boolean {
  return typeof actual === 'string' && /^[a-f0-9]{64}$/.test(actual) && timingSafeEqual(Buffer.from(expected, 'hex'), Buffer.from(actual, 'hex'));
}
