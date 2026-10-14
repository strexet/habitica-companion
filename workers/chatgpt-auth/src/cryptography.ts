import { timingSafeEqual } from "node:crypto";

export interface HabiticaCredential {
  habiticaUserId: string;
  apiToken: string;
}
export interface CredentialEnvelope {
  ciphertext: string;
  iv: string;
  keyVersion: string;
  schemaVersion: 1;
}
export type CredentialKeys = Readonly<Record<string, string | undefined>>;
const text = new TextEncoder();

export function encode(bytes: Uint8Array): string {
  return btoa(String.fromCharCode(...bytes)).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}
export function decode(value: string): Uint8Array<ArrayBuffer> {
  const decoded = atob(value.replace(/-/g, "+").replace(/_/g, "/"));
  return Uint8Array.from(decoded, character => character.charCodeAt(0));
}
function masterKey(value: string | undefined): Uint8Array<ArrayBuffer> {
  if (!value) throw new Error("Credential key unavailable.");
  const bytes = decode(value);
  if (bytes.length !== 32) throw new Error("Credential key unavailable.");
  return bytes;
}
function aad(profileId: string, version: string, schema: number) {
  return text.encode(`habitica-integration-credential:${profileId}:${schema}:${version}`);
}
export function validateKeySeparation(profileSecret: string, keys: CredentialKeys): void {
  const profileKey = masterKey(profileSecret);
  for (const secret of Object.values(keys)) {
    if (secret && timingSafeEqual(profileKey, masterKey(secret))) throw new Error("Credential keys must be distinct.");
  }
}
export async function deriveProfileId(userId: string, secret: string): Promise<string> {
  const key = await crypto.subtle.importKey("raw", masterKey(secret), { name: "HMAC", hash: "SHA-256" }, false, ["sign"]);
  return "prf_" + encode(new Uint8Array(await crypto.subtle.sign("HMAC", key, text.encode(userId.toLowerCase()))));
}
export async function encryptCredential(profileId: string, credential: HabiticaCredential, version: string, keys: CredentialKeys): Promise<CredentialEnvelope> {
  const key = await crypto.subtle.importKey("raw", masterKey(keys[version]), "AES-GCM", false, ["encrypt"]);
  const iv = crypto.getRandomValues(new Uint8Array(12));
  const ciphertext = await crypto.subtle.encrypt({ name: "AES-GCM", iv, additionalData: aad(profileId, version, 1) }, key, text.encode(JSON.stringify(credential)));
  return { ciphertext: encode(new Uint8Array(ciphertext)), iv: encode(iv), keyVersion: version, schemaVersion: 1 };
}
export async function decryptCredential(profileId: string, envelope: CredentialEnvelope, keys: CredentialKeys): Promise<HabiticaCredential> {
  if (envelope.schemaVersion !== 1) throw new Error("Credential schema unavailable.");
  const key = await crypto.subtle.importKey("raw", masterKey(keys[envelope.keyVersion]), "AES-GCM", false, ["decrypt"]);
  const plaintext = await crypto.subtle.decrypt({ name: "AES-GCM", iv: decode(envelope.iv), additionalData: aad(profileId, envelope.keyVersion, envelope.schemaVersion) }, key, decode(envelope.ciphertext));
  const parsed = JSON.parse(new TextDecoder().decode(plaintext));
  if (typeof parsed.habiticaUserId !== "string" || typeof parsed.apiToken !== "string") throw new Error("Credential schema unavailable.");
  return parsed;
}
export function newManagementToken(): string {
  return encode(crypto.getRandomValues(new Uint8Array(32)));
}
export async function tokenHash(token: string): Promise<string> {
  return encode(new Uint8Array(await crypto.subtle.digest("SHA-256", text.encode(token))));
}
export async function tokenMatches(token: string, expectedHash: string | null): Promise<boolean> {
  if (!expectedHash || token.length > 1024) return false;
  const expected = decode(expectedHash);
  const actual = decode(await tokenHash(token));
  return expected.length === actual.length && timingSafeEqual(expected, actual);
}
