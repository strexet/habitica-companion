import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { Miniflare, convertV4MiniflareOptions } from "miniflare";

const authRoot = new URL("../", import.meta.url);
const mcpRoot = new URL("../../chatgpt-mcp/", import.meta.url);
const authConfig = JSON.parse(await readFile(new URL("wrangler.jsonc", authRoot), "utf8"));
const mcpConfig = JSON.parse(await readFile(new URL("wrangler.jsonc", mcpRoot), "utf8"));

async function fixture(t, bindAuth = true) {
  const mf = new Miniflare(convertV4MiniflareOptions({ workers: [
    { name: "auth", modules: true, script: await readFile(new URL("dist/index.js", authRoot), "utf8"),
      compatibilityDate: authConfig.compatibility_date, compatibilityFlags: ["nodejs_compat"], bindings: { ENVIRONMENT: "local" } },
    { name: "mcp", modules: true, script: await readFile(new URL("dist/index.js", mcpRoot), "utf8"),
      compatibilityDate: mcpConfig.compatibility_date, compatibilityFlags: ["nodejs_compat"], bindings: { ENVIRONMENT: "local" },
      serviceBindings: bindAuth ? { AUTH_SERVER: { name: "auth", entrypoint: "AuthService" } } : {} },
  ] }));
  t.after(() => mf.dispose());
  return mf;
}

test("real local service binding reaches Auth named entrypoint", async t => {
  const mf = await fixture(t);
  const mcp = await mf.getWorker("mcp");
  const response = await mcp.fetch("http://localhost/health");
  assert.equal(response.status, 200);
  assert.deepEqual(await response.json(), { service: "mcp", authService: "reachable", authorization: "unavailable" });
  assert.equal(response.headers.get("cache-control"), "no-store");
});

test("both public workers deny protected requests even with arbitrary bearer values", async t => {
  const mf = await fixture(t);
  for (const name of ["auth", "mcp"]) {
    const worker = await mf.getWorker(name);
    for (const path of ["/mcp", "/enroll", "/internal/credentials", "/oauth/token"]) {
      const response = await worker.fetch(`http://localhost${path}`, { method: "POST", headers: { authorization: "Bearer fixture-invalid" } });
      assert.equal(response.status, 401);
      assert.deepEqual(await response.json(), { error: "unauthorized" });
    }
  }
});

test("environment configs isolate integration storage and service bindings", () => {
  const namespaces = new Set();
  const databases = new Set();
  for (const environment of ["local", "preview", "production"]) {
    const auth = environment === "local" ? authConfig : authConfig.env[environment];
    const mcp = environment === "local" ? mcpConfig : mcpConfig.env[environment];
    assert.equal(auth.vars.ENVIRONMENT, environment);
    assert.equal(mcp.vars.ENVIRONMENT, environment);
    assert.equal(auth.kv_namespaces[0].binding, "OAUTH_KV");
    assert.equal(auth.d1_databases[0].binding, "HABITICA_INTEGRATION_DB");
    namespaces.add(auth.kv_namespaces[0].id);
    databases.add(auth.d1_databases[0].database_id);
    assert.equal(mcp.services[0].service, `habitica-chatgpt-auth-${environment}`);
    assert.equal(mcp.services[0].entrypoint, "AuthService");
    assert.equal(mcp.kv_namespaces, undefined);
    assert.equal(mcp.d1_databases, undefined);
    const serialized = JSON.stringify([auth, mcp]);
    assert.ok(!serialized.includes('HABITICA_SYNC_KV'));
    assert.ok(!serialized.includes('HABITICA_PARTY_DB'));
  }
  assert.equal(namespaces.size, 3);
  assert.equal(databases.size, 3);
});

test("missing Auth binding yields bounded unavailable response", async t => {
  const mf = await fixture(t, false);
  const worker = await mf.getWorker("mcp");
  const response = await worker.fetch("http://localhost/health");
  assert.equal(response.status, 503);
  assert.deepEqual(await response.json(), { error: "auth_service_unavailable" });
});
