import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { Miniflare, convertV4MiniflareOptions, Log, LogLevel } from "miniflare";
import { encode, decryptCredential } from "../src/cryptography.ts";

const root = new URL("../", import.meta.url);
const config = JSON.parse(await readFile(new URL("wrangler.jsonc", root), "utf8"));
const key = byte => encode(new Uint8Array(32).fill(byte));
const accountA = { habiticaUserId: "00000000-0000-4000-8000-000000000011", apiToken: "synthetic-a-secret" };
const accountB = { habiticaUserId: "00000000-0000-4000-8000-000000000012", apiToken: "synthetic-b-secret" };
async function fixture(t, overrides = {}) {
  const messages = [];
  class CaptureLog extends Log { log(message) { messages.push(message); } }
  const mf = new Miniflare(convertV4MiniflareOptions({
    log: new CaptureLog(LogLevel.VERBOSE), name: "auth", modules: true, script: await readFile(new URL("dist/index.js", root), "utf8"),
    compatibilityDate: config.compatibility_date, compatibilityFlags: ["nodejs_compat"],
    bindings: { ...config.vars, ENROLLMENT_ENABLED: true, PROFILE_ID_KEY: key(3), CREDENTIAL_KEY_V1: key(1), CREDENTIAL_KEY_V2: key(2), ...overrides },
    d1Databases: ["HABITICA_INTEGRATION_DB"],
    outboundService: async request => {
      assert.equal(request.url, "https://habitica.com/api/v3/user?userFields=_id,profile");
      assert.equal(request.headers.get("x-client"), config.vars.HABITICA_X_CLIENT_HEADER);
      const user = request.headers.get("x-api-user");
      const token = request.headers.get("x-api-key");
      if (token === "invalid") return new Response(JSON.stringify({ error: "invalid", secret: token }), { status: 401 });
      if (![accountA.habiticaUserId, accountB.habiticaUserId].includes(user)) return new Response("invalid", { status: 401 });
      return Response.json({ success: true, data: { _id: user, profile: { name: user === accountA.habiticaUserId ? "Fixture A" : "Fixture B" } } });
    },
  }));
  t.after(() => mf.dispose());
  const db = await mf.getD1Database("HABITICA_INTEGRATION_DB", "auth");
  await db.prepare(await readFile(new URL("migrations/0001-integration-profiles.sql", root), "utf8")).run();
  const worker = await mf.getWorker("auth");
  return { worker, db, messages };
}
async function enroll(worker, account, extra = {}) {
  return worker.fetch("http://localhost/enroll", { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ ...account, consentVersion: "integration-v1", ...extra }) });
}
function envelope(row) {
  return { ciphertext: row.credential_ciphertext, iv: row.credential_iv, keyVersion: row.credential_key_version, schemaVersion: row.credential_schema_version };
}

test("two validated accounts persist isolated ciphertext and stable reenrollment identity", async t => {
  const { worker, db, messages } = await fixture(t);
  const responseA = await enroll(worker, accountA);
  assert.equal(responseA.status, 201);
  const a = await responseA.json();
  const b = await (await enroll(worker, accountB)).json();
  assert.notEqual(a.profileId, b.profileId);
  const rows = (await db.prepare("SELECT * FROM integration_profiles").all()).results;
  assert.equal(rows.length, 2);
  const serialized = JSON.stringify(rows);
  for (const account of [accountA, accountB]) {
    assert.ok(!serialized.includes(account.habiticaUserId));
    assert.ok(!serialized.includes(account.apiToken));
  }
  assert.ok(!serialized.includes(a.managementToken));
  const rowA = rows.find(row => row.profile_id === a.profileId);
  const rowB = rows.find(row => row.profile_id === b.profileId);
  assert.deepEqual(await decryptCredential(a.profileId, envelope(rowA), { "1": key(1) }), accountA);
  await assert.rejects(decryptCredential(b.profileId, envelope(rowA), { "1": key(1) }));
  assert.deepEqual(await decryptCredential(b.profileId, envelope(rowB), { "1": key(1) }), accountB);
  assert.ok(!messages.join("\n").includes(accountA.apiToken));
  assert.ok(!messages.join("\n").includes(accountB.apiToken));
  const rotated = await (await enroll(worker, { ...accountA, apiToken: "synthetic-a-rotated" })).json();
  assert.equal(rotated.profileId, a.profileId);
  assert.notEqual(rotated.managementToken, a.managementToken);
  const oldManagement = await worker.fetch(`http://localhost/profiles/${a.profileId}/disconnect`, { method: "POST", headers: { authorization: `Bearer ${a.managementToken}` } });
  assert.equal(oldManagement.status, 401);
  const current = await db.prepare("SELECT generation, connection_id FROM integration_profiles WHERE profile_id=?").bind(a.profileId).first();
  assert.equal(current.generation, 2);
  assert.notEqual(current.connection_id, rowA.connection_id);
});

test("invalid credentials, missing consent, disabled enrollment and encryption failure persist nothing", async t => {
  const { worker, db } = await fixture(t);
  assert.equal((await enroll(worker, { ...accountA, apiToken: "invalid" })).status, 401);
  assert.equal((await enroll(worker, accountA, { consentVersion: "other" })).status, 400);
  assert.equal((await db.prepare("SELECT COUNT(*) AS count FROM integration_profiles").first()).count, 0);
  const disabled = await fixture(t, { ENROLLMENT_ENABLED: false });
  assert.equal((await enroll(disabled.worker, accountA)).status, 401);
  const broken = await fixture(t, { CREDENTIAL_KEY_V1: "invalid-key" });
  const response = await enroll(broken.worker, accountA);
  assert.equal(response.status, 503);
  assert.ok(!(await response.text()).includes(accountA.apiToken));
  assert.equal((await broken.db.prepare("SELECT COUNT(*) AS count FROM integration_profiles").first()).count, 0);
});

test("cross-profile management is denied; disconnect deletes envelope; revalidation reconnects", async t => {
  const { worker, db } = await fixture(t);
  const a = await (await enroll(worker, accountA)).json();
  const b = await (await enroll(worker, accountB)).json();
  const rejected = await worker.fetch(`http://localhost/profiles/${b.profileId}/disconnect`, { method: "POST", headers: { authorization: `Bearer ${a.managementToken}` } });
  assert.equal(rejected.status, 401);
  const wrongMethod = await worker.fetch(`http://localhost/profiles/${a.profileId}/disconnect`, { method: "DELETE", headers: { authorization: `Bearer ${a.managementToken}` } });
  assert.equal(wrongMethod.status, 401);
  const disconnected = await worker.fetch(`http://localhost/profiles/${a.profileId}/disconnect`, { method: "POST", headers: { authorization: `Bearer ${a.managementToken}` } });
  assert.equal(disconnected.status, 200);
  const row = await db.prepare("SELECT * FROM integration_profiles WHERE profile_id=?").bind(a.profileId).first();
  assert.equal(row.credential_ciphertext, null);
  assert.equal(row.credential_iv, null);
  assert.equal(row.management_token_hash, null);
  assert.ok(row.revoked_at_utc);
  const reconnected = await (await enroll(worker, accountA)).json();
  assert.equal(reconnected.profileId, a.profileId);
  const oldB = await db.prepare("SELECT connection_id FROM integration_profiles WHERE profile_id=?").bind(b.profileId).first();
  const deleted = await worker.fetch(`http://localhost/profiles/${b.profileId}`, { method: "DELETE", headers: { authorization: `Bearer ${b.managementToken}` } });
  assert.equal(deleted.status, 200);
  assert.equal(await db.prepare("SELECT * FROM integration_profiles WHERE profile_id=?").bind(b.profileId).first(), null);
  const newB = await (await enroll(worker, accountB)).json();
  assert.equal(newB.profileId, b.profileId);
  const restored = await db.prepare("SELECT connection_id FROM integration_profiles WHERE profile_id=?").bind(b.profileId).first();
  assert.notEqual(restored.connection_id, oldB.connection_id);
});

test("oversized input, hostile origin and database failure are bounded and secret-free", async t => {
  const { worker, db } = await fixture(t);
  const oversized = await enroll(worker, accountA, { padding: "x".repeat(20000) });
  assert.equal(oversized.status, 413);
  const origin = await worker.fetch("http://localhost/enroll", { method: "POST", headers: { origin: "https://hostile.invalid", "content-type": "application/json" }, body: JSON.stringify({ ...accountA, consentVersion: "integration-v1" }) });
  assert.equal(origin.status, 403);
  await db.exec("DROP TABLE integration_profiles");
  const failure = await enroll(worker, accountA);
  assert.equal(failure.status, 503);
  assert.deepEqual(await failure.json(), { error: "integration_unavailable" });
});
