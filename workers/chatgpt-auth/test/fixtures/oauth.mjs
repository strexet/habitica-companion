import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { Miniflare, convertV4MiniflareOptions, Log, LogLevel } from "miniflare";
import { encode } from "../../src/cryptography.ts";
const authRoot = new URL("../../", import.meta.url);
const mcpRoot = new URL("../../../chatgpt-mcp/", import.meta.url);
const authConfig = JSON.parse(await readFile(new URL("wrangler.jsonc", authRoot), "utf8"));
const issuer = "http://localhost:8787";
const resource = "http://localhost:8788/mcp";
const redirectUri = "http://localhost:9999/callback";
const key = byte => encode(new Uint8Array(32).fill(byte));
const accounts = [
  { habiticaUserId: "00000000-0000-4000-8000-000000000011", apiToken: "fixture-a-token" },
  { habiticaUserId: "00000000-0000-4000-8000-000000000012", apiToken: "fixture-b-token" },
];
async function fixture(t, options = {}) {
  const messages = [];
  class CaptureLog extends Log { log(message) { messages.push(message); } }
  const mf = new Miniflare(convertV4MiniflareOptions({ log: new CaptureLog(LogLevel.VERBOSE), workers: [
    { name: "auth", modules: true, script: await readFile(new URL("dist/index.js", authRoot), "utf8"),
      compatibilityDate: authConfig.compatibility_date, compatibilityFlags: ["nodejs_compat", "global_fetch_strictly_public"],
      bindings: { ...authConfig.vars, OAUTH_ENABLED: true, ENROLLMENT_ENABLED: true, WORKSPACE_ENABLED: Boolean(options.workspace), PROJECTION_ENABLED: Boolean(options.projection), PARTY_ENABLED:Boolean(options.party), SPELLS_ENABLED:Boolean(options.spells), PROFILE_ID_KEY: key(3), CREDENTIAL_KEY_V1: key(1), CREDENTIAL_KEY_V2: key(2) },
      kvNamespaces: ["OAUTH_KV"], d1Databases: ["HABITICA_INTEGRATION_DB"],
      outboundService: async request => {
        if (request.url.startsWith("https://habitica.com/api/v3/")) {
          if (options.habitica) return options.habitica(request);
          const user = request.headers.get("x-api-user");
          return Response.json({ success: true, data: { _id: user, profile: { name: user === accounts[0].habiticaUserId ? "Fixture A" : "Fixture B" } } });
        }
        if (request.url === "https://client.example.com/oauth-client.json") return Response.json({ client_id: request.url, client_name: "CIMD Fixture", redirect_uris: [redirectUri], token_endpoint_auth_method: "none" });
        return new Response("Blocked fixture destination", { status: 404 });
      },
    },
    { name: "mcp", modules: true, script: await readFile(new URL("dist/index.js", mcpRoot), "utf8"),
      compatibilityDate: authConfig.compatibility_date, compatibilityFlags: ["nodejs_compat"],
      bindings: { ENVIRONMENT: "local", OAUTH_ENABLED: true }, serviceBindings: { ...(options.workspace ? { WORKSPACE_SERVER: { name: "workspace", entrypoint: "WorkspaceCommandService" }, AUTH_PROJECTION: { name: "auth", entrypoint: "ProjectionService" }, WORKSPACE_IDENTITY: { name: "auth", entrypoint: "WorkspaceIdentityService" }, AUTH_SPELL:{name:"auth",entrypoint:"SpellService"} } : {}), AUTH_SERVER: { name: "auth", entrypoint: "AuthService" } } },
    ...(options.workspace ? [{ name: "workspace", modules: true, script: await readFile(new URL("../companion-workspace/dist/index.js", authRoot), "utf8"), compatibilityDate: authConfig.compatibility_date, compatibilityFlags: ["nodejs_compat"], bindings: { ENVIRONMENT: "local", WORKSPACE_ENABLED: true, EXECUTOR_ENABLED: Boolean(options.executor), PARTY_ENABLED:Boolean(options.party) }, d1Databases: ["WORKSPACE_DB"], serviceBindings: { AUTH_PROJECTION: options.projectionService ?? { name: "auth", entrypoint: "ProjectionService" }, AUTH_IDENTITY: options.identity ?? { name: "auth", entrypoint: "WorkspaceIdentityService" }, AUTH_PARTY:{name:"auth",entrypoint:"PartyService"} } }] : []),
  ] }));
  t.after(() => mf.dispose());
  const db = await mf.getD1Database("HABITICA_INTEGRATION_DB", "auth");
  for (const migration of ["0001-integration-profiles.sql", "0002-linking.sql", "0003-read-state.sql", "0004-management-identity.sql", "0005-projection-gateway.sql"]) {
    for (const sql of (await readFile(new URL("migrations/" + migration, authRoot), "utf8")).split(';')) if (sql.trim()) await db.prepare(sql).run();
  }
  let workspace, workspaceDb, workspaceIdentity, projection;
  if (options.workspace) {
    workspace = await mf.getWorker("workspace"); workspaceDb = await mf.getD1Database("WORKSPACE_DB", "workspace");
    for (const migration of ["0001-workspace.sql","0002-execution-ownership.sql","0003-prepared-commands.sql","0004-authenticated-party-planning.sql"]) {
      const sqlText = await readFile(new URL("../companion-workspace/migrations/"+migration, authRoot), "utf8");
      for (const sql of sqlText.split(";")) if (sql.trim()) await workspaceDb.prepare(sql).run();
    }
    workspaceIdentity = (await mf.getBindings("mcp")).WORKSPACE_IDENTITY; projection = (await mf.getBindings("mcp")).AUTH_PROJECTION;
  }
  const auth = await mf.getWorker("auth");
  const mcp = await mf.getWorker("mcp");
  const internal = (await mf.getBindings("mcp")).AUTH_SERVER;
  const client = await auth.fetch(issuer + "/oauth/register", { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ client_name: "Fixture <script>untrusted</script>", redirect_uris: [redirectUri], token_endpoint_auth_method: "none", grant_types: ["authorization_code", "refresh_token"], response_types: ["code"] }) });
  assert.equal(client.status, 201);
  const registration = await client.json();
  return { mf, db, auth, mcp, internal, workspace, workspaceDb, workspaceIdentity, projection, messages, clientId: registration.client_id };
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


export { fixture, enrolled, ticket, authPage, form, authorization, exchange, handle, cookies, issuer, resource, accounts };
