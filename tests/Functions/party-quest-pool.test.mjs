import assert from "node:assert/strict";
import test from "node:test";
import { readFile, readdir } from "node:fs/promises";
import { createRequire } from "node:module";
import { onRequestPost } from "../../functions/api/party-sync/[partyId].js";

// Use the repository's pinned local D1 runtime; no remote bindings or Habitica calls.
const require = createRequire(new URL("../../workers/chatgpt-auth/package.json", import.meta.url));
const { Miniflare, convertV4MiniflareOptions } = require("miniflare");
const partyId = "fixture-party-123";
const userId = "fixture-user-123";
const makeEntries = count => Array.from({ length: count }, (_, i) => ({
  questKey: `quest-${i}`, questName: `Quest ${i}`, availableCount: i + 1,
  questType: "Boss", rewardSummary: ["Synthetic reward"],
}));

async function fixture(t) {
  const mf = new Miniflare(convertV4MiniflareOptions({
    name: "party-pool-fixture", modules: true, compatibilityDate: "2026-10-12",
    script: 'export default { fetch() { return new Response("fixture"); } }',
    d1Databases: ["DB"],
    outboundService: () => { throw new Error("External requests are prohibited"); },
  }));
  t.after(() => mf.dispose());
  const db = await mf.getD1Database("DB", "party-pool-fixture");
  const migrations = new URL("../../migrations/", import.meta.url);
  for (const name of (await readdir(migrations)).filter(name => name.endsWith(".sql")).sort()) {
    for (const sql of (await readFile(new URL(name, migrations), "utf8")).replace(/^\s*--.*$/gm, "").split(";")) {
      if (sql.trim()) await db.prepare(sql).run();
    }
  }
  let queries = 0;
  const countQueries = count => {
    queries += count;
    if (queries > 50) throw new Error("Fixture Free D1 invocation query limit exceeded");
  };
  const statement = raw => ({
    raw,
    bind: (...args) => statement(raw.bind(...args)),
    first: async (...args) => { countQueries(1); return raw.first(...args); },
    all: async () => { countQueries(1); return raw.all(); },
    run: async () => { countQueries(1); return raw.run(); },
  });
  const guardedDb = {
    prepare: sql => statement(db.prepare(sql)),
    batch: async statements => {
      countQueries(statements.length);
      return db.batch(statements.map(statement => statement.raw));
    },
  };
  async function publish(entries) {
    queries = 0;
    const request = new Request(`https://fixture.test/api/party-sync/${partyId}`, {
      method: "POST", headers: {
        "content-type": "application/json", "x-party-sync-proof-version": "local-claim-v1",
        "x-party-sync-party-id": partyId, "x-party-sync-user-id": userId,
        "x-party-sync-display-name": "Fixture owner", "x-party-sync-leader-id": userId,
      }, body: JSON.stringify({ action: "publishQuestPool", entries }),
    });
    return onRequestPost({ request, params: { partyId }, env: { HABITICA_PARTY_DB: guardedDb } });
  }
  const seed = (party, owner, key) => db.prepare(`INSERT INTO party_quest_pool_entries
    (party_id, quest_key, quest_name, owner_user_id, owner_display_name, quest_type, reward_summary_json, available_count, last_seen_at_utc)
    VALUES (?, ?, ?, ?, 'Fixture', 'Boss', '[]', 1, '2026-10-20T00:00:00Z')`).bind(party, key, key, owner).run();
  return { db, publish, seed, queryCount: () => queries };
}

test("52 and 300 owned quest types publish below the 50-query guard with a full response", { timeout: 60000 }, async t => {
  const f = await fixture(t);
  await f.seed(partyId, "other-owner", "other-quest");
  await f.seed("other-party", userId, "other-party-quest");
  let firstCount;
  for (const size of [52, 300]) {
    const response = await f.publish(makeEntries(size));
    assert.equal(response.status, 200);
    const body = await response.json();
    assert.equal(body.ok, true);
    assert.equal(body.questPool.length, size + 1);
    assert.ok(Array.isArray(body.questQueue));
    assert.ok(Array.isArray(body.recentlyCompleted));
    assert.equal(body.management.currentUserIsOwner, true);
    assert.ok(f.queryCount() < 50);
    t.diagnostic(`${size} quest types: ${f.queryCount()} database statements for the complete handler`);
    firstCount ??= f.queryCount();
    assert.equal(f.queryCount(), firstCount);
    const last = body.questPool.find(entry => entry.questKey === `quest-${size - 1}`);
    assert.equal(last.availableCount, size);
    assert.deepEqual(last.rewardSummary, ["Synthetic reward"]);
  }
  assert.equal((await f.db.prepare("SELECT COUNT(*) AS count FROM party_quest_pool_entries WHERE party_id = 'other-party'").first()).count, 1);
});

test("empty replacement and duplicate keys keep owner boundaries and last entry semantics", { timeout: 60000 }, async t => {
  const f = await fixture(t);
  await f.seed(partyId, "other-owner", "other-quest");
  const entries = makeEntries(1);
  assert.equal((await f.publish([...entries, { ...entries[0], availableCount: 9 }])).status, 200);
  assert.equal((await f.db.prepare("SELECT available_count FROM party_quest_pool_entries WHERE owner_user_id = ?").bind(userId).first()).available_count, 9);
  assert.equal((await f.publish([])).status, 200);
  assert.equal((await f.db.prepare("SELECT COUNT(*) AS count FROM party_quest_pool_entries WHERE owner_user_id = ?").bind(userId).first()).count, 0);
  assert.equal((await f.db.prepare("SELECT COUNT(*) AS count FROM party_quest_pool_entries WHERE owner_user_id = 'other-owner'").first()).count, 1);
});

test("failed replacement rolls back the deletion and partial inserts in real local D1", { timeout: 60000 }, async t => {
  const f = await fixture(t);
  await f.seed(partyId, userId, "previous-quest");
  await f.seed(partyId, "other-owner", "other-quest");
  await f.db.prepare(`CREATE TRIGGER fixture_reject BEFORE INSERT ON party_quest_pool_entries
    WHEN NEW.quest_key = 'quest-1' BEGIN SELECT RAISE(ABORT, 'Synthetic insertion failure'); END`).run();
  await assert.rejects(f.publish(makeEntries(3)), /Synthetic insertion failure/);
  const rows = (await f.db.prepare("SELECT quest_key FROM party_quest_pool_entries ORDER BY quest_key").all()).results;
  assert.deepEqual(rows.map(row => row.quest_key), ["other-quest", "previous-quest"]);
});

test("oversized serialized pool is rejected before deletion", { timeout: 60000 }, async t => {
  const f = await fixture(t);
  await f.seed(partyId, userId, "previous-quest");
  const response = await f.publish([{ ...makeEntries(1)[0], questName: "x".repeat(1_900_000) }]);
  assert.equal(response.status, 413);
  assert.equal((await f.db.prepare("SELECT quest_key FROM party_quest_pool_entries").first()).quest_key, "previous-quest");
});
