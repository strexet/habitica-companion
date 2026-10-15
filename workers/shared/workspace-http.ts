export class WorkspaceError extends Error {
  readonly code: string;
  readonly status: number;
  constructor(code: string, status = 400) { super(code); this.code = code; this.status = status; }
}
export async function workspaceJson(request: Request, limit = 16384): Promise<unknown> {
  const reader = request.body?.getReader(); if (!reader) throw new WorkspaceError("invalid_request");
  const chunks: Uint8Array[] = []; let length = 0;
  try {
    while (true) { const { done, value } = await reader.read(); if (done) break; length += value.length; if (length > limit) { await reader.cancel(); throw new WorkspaceError("payload_too_large", 413); } chunks.push(value); }
  } finally { reader.releaseLock(); }
  const bytes = new Uint8Array(length); let offset = 0; for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.length; }
  try {
    const text = new TextDecoder("utf-8", { fatal: true, ignoreBOM: true }).decode(bytes); const parsed = JSON.parse(text);
    const stack: (Set<string> | null)[] = [];
    for (const match of text.matchAll(/"(?:\\.|[^"\\])*"|[{}\[\]]/g)) {
      const token = match[0];
      if (token === "{" || token === "[") { stack.push(token === "{" ? new Set() : null); if (stack.length > 32) throw new Error(); }
      else if (token === "}" || token === "]") stack.pop();
      else if (/^\s*:/.test(text.slice(match.index! + token.length))) { const keys = stack[stack.length - 1]; const key = JSON.parse(token); if (!keys || keys.has(key)) throw new Error(); keys.add(key); }
    }
    return parsed;
  } catch { throw new WorkspaceError("invalid_json"); }
}
export function workspaceObject(value: unknown, allowed?: string[]): Record<string, unknown> {
  if (!value || typeof value !== "object" || Array.isArray(value)) throw new WorkspaceError("invalid_request");
  if (allowed && Object.keys(value).some(key => !allowed.includes(key))) throw new WorkspaceError("unknown_field");
  return value as Record<string, unknown>;
}
export async function workspaceTokenHash(token: string): Promise<string> {
  return Array.from(new Uint8Array(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(token))), value => value.toString(16).padStart(2, "0")).join("");
}
export function workspaceSessionToken(): string { return btoa(String.fromCharCode(...crypto.getRandomValues(new Uint8Array(32)))).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, ""); }
export function bearer(request: Request): string {
  const token = request.headers.get("authorization")?.match(/^Bearer ([A-Za-z0-9_-]{43})$/)?.[1];
  if (!token) throw new WorkspaceError("unauthorized", 401); return token;
}
export function uuid(value: unknown): string {
  if (typeof value !== "string" || !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value) || value === "00000000-0000-0000-0000-000000000000") throw new WorkspaceError("invalid_id");
  return value.toLowerCase();
}
