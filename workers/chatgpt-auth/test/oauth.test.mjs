import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { Miniflare, convertV4MiniflareOptions } from "miniflare";
import { encode } from "../src/cryptography.ts";

const authRoot = new URL("../", import.meta.url);
const mcpRoot = new URL("../../chatgpt-mcp/", import.meta.url);
const authConfig = JSON.parse(await readFile(new URL("wrangler.jsonc", authRoot), "utf8"));
const issuer = "http://localhost:8787";
const resource = "http://localhost:8788/mcp";
const redirectUri = "http://localhost:9999/callback";
const key = byte => encode(new Uint8Array(32).fill(byte));
const accounts = [
  { habiticaUserId: "00000000-0000-4000-8000-000000000011", apiToken: "fixture-a-token" },
  { habiticaUserId: "00000000-0000-4000-8000-000000000012", apiToken: "fixture-b-token" },
];
async function fixture(t) {
  const mf = new Miniflare(convertV4MiniflareOptions({ workers: [
    { name: "auth", modules: true, script: await readFile(new URL("dist/index.js", authRoot), "utf8"),
      compatibilityDate: authConfig.compatibility_date, compatibilityFlags: ["nodejs_compat", "global_fetch_strictly_public"],
      bindings: { ...authConfig.vars, OAUTH_ENABLED: true, ENROLLMENT_ENABLED: true, PROFILE_ID_KEY: key(3), CREDENTIAL_KEY_V1: key(1), CREDENTIAL_KEY_V2: key(2) },
      kvNamespaces: ["OAUTH_KV"], d1Databases: ["HABITICA_INTEGRATION_DB"],
      outboundService: async request => {
        if (request.url.startsWith("https://habitica.com/api/v3/user")) {
          const user = request.headers.get("x-api-user");
          return Response.json({ success: true, data: { _id: user, profile: { name: user === accounts[0].habiticaUserId ? "Fixture A" : "Fixture B" } } });
        }
        if (request.url === "https://client.example.com/oauth-client.json") return Response.json({ client_id: request.url, client_name: "CIMD Fixture", redirect_uris: [redirectUri], token_endpoint_auth_method: "none" });
        return new Response("Blocked fixture destination", { status: 404 });
      },
    },
    { name: "mcp", modules: true, script: await readFile(new URL("dist/index.js", mcpRoot), "utf8"),
      compatibilityDate: authConfig.compatibility_date, compatibilityFlags: ["nodejs_compat"],
      bindings: { ENVIRONMENT: "local", OAUTH_ENABLED: true }, serviceBindings: { AUTH_SERVER: { name: "auth", entrypoint: "AuthService" } } },
  ] }));
  t.after(() => mf.dispose());
  const db = await mf.getD1Database("HABITICA_INTEGRATION_DB", "auth");
  for (const migration of ["0001-integration-profiles.sql", "0002-linking.sql"]) {
    for (const sql of (await readFile(new URL("migrations/" + migration, authRoot), "utf8")).split(';')) if (sql.trim()) await db.prepare(sql).run();
  }
  const auth = await mf.getWorker("auth");
  const mcp = await mf.getWorker("mcp");
  const internal = (await mf.getBindings("mcp")).AUTH_SERVER;
  const client = await auth.fetch(issuer + "/oauth/register", { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ client_name: "Fixture <script>untrusted</script>", redirect_uris: [redirectUri], token_endpoint_auth_method: "none", grant_types: ["authorization_code", "refresh_token"], response_types: ["code"] }) });
  assert.equal(client.status, 201);
  const registration = await client.json();
  return { mf, db, auth, mcp, internal, clientId: registration.client_id };
}
async function enrolled(f, account = accounts[0]) {
  const response = await f.auth.fetch(issuer + "/enroll", { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ ...account, consentVersion: "integration-v1" }) });
  assert.equal(response.status, 201);
  return response.json();
}
async function ticket(f, profile) {
  const response = await f.auth.fetch(`${issuer}/profiles/${profile.profileId}/linking-ticket`, { method: "POST", headers: { authorization: `Bearer ${profile.managementToken}` } });
  assert.equal(response.status, 200);
  return response.json();
}
function remember(jar, response) {
  for (const cookie of response.headers.getSetCookie()) {
    const pair = cookie.split(';', 1)[0];
    const position = pair.indexOf('=');
    const name = pair.slice(0, position); const value = pair.slice(position + 1);
    if (value) jar.set(name, value); else jar.delete(name);
  }
}
const cookies = jar => Array.from(jar, ([name, value]) => `${name}=${value}`).join('; ');
function handle(html) { return html.match(/name="handle" value="([^"]+)"/)[1]; }
async function authPage(f, scope = "profile:read", overrides = {}) {
  const verifier = "fixture-verifier-" + "v".repeat(48);
  const challenge = encode(new Uint8Array(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(verifier))));
  const params = new URLSearchParams({ client_id: f.clientId, redirect_uri: redirectUri, response_type: "code", scope, state: "fixture-state", resource, code_challenge: challenge, code_challenge_method: "S256", ...overrides });
  if (overrides.resource === null) params.delete("resource");
  const response = await f.auth.fetch(issuer + "/authorize?" + params, { redirect: "manual" });
  const jar = new Map(); remember(jar, response);
  return { response, jar, verifier };
}
async function form(f, page, values) {
  const body = new URLSearchParams(values);
  if (values.scope) { body.delete("scope"); for (const scope of values.scope.split(" ")) body.append("scope", scope); }
  const response = await f.auth.fetch(issuer + "/authorize", { method: "POST", redirect: "manual", headers: { "content-type": "application/x-www-form-urlencoded", cookie: cookies(page.jar), origin: issuer }, body });
  remember(page.jar, response); return response;
}
async function authorization(f, profile, scope = "profile:read") {
  const code = await ticket(f, profile);
  const page = await authPage(f, scope);
  assert.equal(page.response.status, 200);
  const firstHtml = await page.response.text();
  assert.ok(firstHtml.includes('&#60;script&#62;untrusted&#60;/script&#62;'));
  assert.ok(!firstHtml.includes('<script>untrusted</script>'));
  const verify = await form(f, page, { handle: handle(firstHtml), decision: "identify", code: code.code, scope });
  assert.equal(verify.status, 200);
  const html = await verify.text();
  assert.ok(html.includes("Fixture A") || html.includes("Fixture B"));
  const allowed = await form(f, page, { handle: handle(html), decision: "approve", scope });
  assert.equal(allowed.status, 302);
  const location = new URL(allowed.headers.get('location'));
  assert.equal(location.searchParams.get('state'), 'fixture-state');
  assert.equal(location.searchParams.get('iss'), issuer);
  return { code: location.searchParams.get('code'), verifier: page.verifier };
}
async function exchange(f, auth, overrides = {}) {
  return f.auth.fetch(issuer + "/oauth/token", { method: "POST", headers: { "content-type": "application/x-www-form-urlencoded" }, body: new URLSearchParams({ grant_type: "authorization_code", code: auth.code, code_verifier: auth.verifier, client_id: f.clientId, redirect_uri: redirectUri, resource, ...overrides }) });
}

test("two profiles complete PKCE and retain isolated token contexts", async t => {
  const f = await fixture(t);
  const a = await enrolled(f); const b = await enrolled(f, accounts[1]);
  const tokenA = await exchange(f, await authorization(f, a));
  assert.equal(tokenA.status, 200);
  const tokensA = await tokenA.json();
  const tokenB = await exchange(f, await authorization(f, b));
  assert.equal(tokenB.status, 200);
  const tokensB = await tokenB.json();
  for (const [token, profile] of [[tokensA.access_token, a], [tokensB.access_token, b]]) {
    const context = await f.internal.fetch("https://auth.internal/validate", { method: "POST", body: JSON.stringify({ resource, token }) });
    const data = await context.json();
    assert.equal(data.props.profileId, profile.profileId);
    assert.equal(data.userId, profile.profileId);
    assert.ok(!JSON.stringify(data).includes(accounts[0].apiToken));
    assert.ok(!JSON.stringify(data).includes(accounts[1].apiToken));
    const access = await f.mcp.fetch(resource, { headers: { authorization: `Bearer ${token}` } });
    assert.equal(access.status, 200);
  }
});

test("metadata, wrong resource, redirect and PKCE fail safely", async t => {
  const f = await fixture(t);
  const metadata = await (await f.auth.fetch(issuer + "/.well-known/oauth-authorization-server")).json();
  assert.ok(metadata.code_challenge_methods_supported.includes("S256"));
  assert.equal(metadata.client_id_metadata_document_supported, true);
  const challenge = await f.mcp.fetch(resource);
  assert.equal(challenge.status, 401);
  assert.ok(challenge.headers.get('www-authenticate').includes('resource_metadata'));
  assert.equal((await authPage(f, "profile:read", { resource: null })).response.status, 400);
  assert.equal((await authPage(f, "profile:read", { resource: "https://other.invalid/mcp" })).response.status, 400);
  assert.equal((await authPage(f, "profile:read", { redirect_uri: "https://other.invalid/callback" })).response.status, 400);
  const profile = await enrolled(f);
  const grant = await authorization(f, profile);
  assert.equal((await exchange(f, grant, { code_verifier: "w".repeat(64) })).status, 400);
  const wrongResource = await f.internal.fetch("https://auth.internal/validate", { method: "POST", body: JSON.stringify({ resource: "https://other.invalid/mcp", token: (await (await exchange(f, await authorization(f, profile))).json()).access_token }) });
  assert.equal(await wrongResource.json(), null);
});

test("single-use linking rejects expired codes and concurrent consumption", async t => {
  const f = await fixture(t); const profile = await enrolled(f);
  const expired = await ticket(f, profile);
  await f.db.prepare("UPDATE linking_tickets SET expires_at_utc='2000-01-01T00:00:00.000Z' WHERE ticket_id=?").bind(expired.ticketId).run();
  const oldPage = await authPage(f); const oldHtml = await oldPage.response.text();
  assert.equal((await form(f, oldPage, { handle: handle(oldHtml), decision: "identify", code: expired.code, scope: "profile:read" })).status, 400);
  const current = await ticket(f, profile);
  const pages = await Promise.all([authPage(f), authPage(f)]);
  const handles = await Promise.all(pages.map(async page => handle(await page.response.text())));
  const replies = await Promise.all(pages.map((page, index) => form(f, page, { handle: handles[index], decision: "identify", code: current.code, scope: "profile:read" })));
  assert.deepEqual(replies.map(response => response.status).sort(), [200, 400]);
  assert.ok((await f.db.prepare("SELECT consumed_at_utc FROM linking_tickets WHERE ticket_id=?").bind(current.ticketId).first()).consumed_at_utc);
});

test("denial and invalid browser binding issue no grant; session attempts are bounded", async t => {
  const f = await fixture(t);
  const page = await authPage(f); const html = await page.response.text();
  const denied = await form(f, page, { handle: handle(html), decision: "deny" });
  assert.equal(denied.status, 302);
  assert.equal(new URL(denied.headers.get('location')).searchParams.get('error'), 'access_denied');
  const bad = await authPage(f); const badHtml = await bad.response.text();
  bad.jar.clear();
  for (let attempt = 0; attempt < 5; attempt++) assert.equal((await form(f, bad, { handle: handle(badHtml), decision: "identify", code: "000000000000", scope: "profile:read" })).status, 400);
  assert.equal((await form(f, bad, { handle: handle(badHtml), decision: "identify", code: "000000000000", scope: "profile:read" })).status, 429);
  const kv = await f.mf.getKVNamespace("OAUTH_KV", "auth");
  assert.equal((await kv.list({ prefix: "grant:" })).keys.length, 0);
});

test("disconnect denies access and refresh even when provider records remain", async t => {
  const f = await fixture(t); const profile = await enrolled(f);
  const response = await exchange(f, await authorization(f, profile));
  assert.equal(response.status, 200); const tokens = await response.json();
  assert.equal((await f.auth.fetch(`${issuer}/profiles/${profile.profileId}/disconnect`, { method: "POST", headers: { authorization: `Bearer ${profile.managementToken}` } })).status, 200);
  assert.equal((await f.mcp.fetch(resource, { headers: { authorization: `Bearer ${tokens.access_token}` } })).status, 401);
  const refresh = await f.auth.fetch(issuer + "/oauth/token", { method: "POST", headers: { "content-type": "application/x-www-form-urlencoded" }, body: new URLSearchParams({ grant_type: "refresh_token", refresh_token: tokens.refresh_token, client_id: f.clientId, resource }) });
  assert.equal(refresh.status, 400);
  assert.equal((await refresh.json()).error, 'invalid_grant');
  assert.equal((await f.db.prepare("SELECT COUNT(*) AS count FROM linking_tickets WHERE profile_id=?").bind(profile.profileId).first()).count, 0);
});

test("CIMD discovery and explicit scope upgrade retain stable profile identity", async t => {
  const f = await fixture(t);
  const cimd = await authPage(f, "profile:read", { client_id: "https://client.example.com/oauth-client.json" });
  assert.equal(cimd.response.status, 200);
  const html = await cimd.response.text();
  assert.ok(html.includes("CIMD Fixture"));
  assert.ok(html.includes("client.example.com"));
  const profile = await enrolled(f);
  const initial = await exchange(f, await authorization(f, profile));
  assert.equal(initial.status, 200);
  const initialTokens = await initial.json();
  const upgraded = await exchange(f, await authorization(f, profile, "profile:read account:read"));
  assert.equal(upgraded.status, 200);
  const tokens = await upgraded.json();
  const validation = await f.internal.fetch("https://auth.internal/validate", { method: "POST", body: JSON.stringify({ resource, token: tokens.access_token }) });
  const context = await validation.json();
  assert.equal(context.props.profileId, profile.profileId);
  assert.ok(context.scope.includes("account:read"));
  assert.ok(context.scope.includes("profile:read"));
  assert.equal((await f.mcp.fetch(resource, { headers: { authorization: `Bearer ${initialTokens.access_token}` } })).status, 401);
});

test("token exchange requires exact resource and scope challenges request minimum access", async t => {
  const f = await fixture(t);
  const missing = await f.auth.fetch(issuer + "/oauth/token", { method: "POST", body: new URLSearchParams({ grant_type: "authorization_code", code: "unused", client_id: f.clientId }) });
  assert.equal(missing.status, 400);
  assert.equal((await missing.json()).error, "invalid_target");
  const profile = await enrolled(f);
  const noProfile = await exchange(f, await authorization(f, profile, "account:read"));
  assert.equal(noProfile.status, 200);
  const token = (await noProfile.json()).access_token;
  const response = await f.mcp.fetch(resource, { headers: { authorization: `Bearer ${token}` } });
  assert.equal(response.status, 403);
  assert.ok(response.headers.get("www-authenticate").includes("profile:read"));
});


test("browser consent preserves same-origin POST and rejects hostile or opaque origins", async t => {
  const f = await fixture(t); const profile = await enrolled(f); const code = await ticket(f, profile);
  const page = await authPage(f);
  assert.equal(page.response.headers.get("referrer-policy"), "same-origin");
  for (const cookie of page.response.headers.getSetCookie()) {
    assert.ok(cookie.includes("Secure")); assert.ok(cookie.includes("HttpOnly"));
  }
  const html = await page.response.text();
  for (const origin of ["https://hostile.invalid", "null"]) {
    const denied = await f.auth.fetch(issuer + "/authorize", { method: "POST", redirect: "manual", headers: { origin, cookie: cookies(page.jar), "content-type": "application/x-www-form-urlencoded" }, body: new URLSearchParams({ handle: handle(html), decision: "identify", code: code.code, scope: "profile:read" }) });
    assert.equal(denied.status, 403);
    const failure = await denied.text();
    assert.ok(failure.includes("Origin") || failure.includes("invalid_origin"));
    assert.ok(!failure.includes(code.code));
  }
  const verified = await form(f, page, { handle: handle(html), decision: "identify", code: code.code, scope: "profile:read" });
  assert.equal(verified.status, 200);
  assert.ok((await verified.text()).includes("Approve Companion access"));
});
