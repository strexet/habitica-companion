import { test } from "node:test";
import assert from "node:assert/strict";
import { Client } from "../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js";
import { StreamableHTTPClientTransport } from "../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/streamableHttp.js";
import { fixture, enrolled, authorization, exchange, issuer, resource, accounts } from "./fixtures/oauth.mjs";

const ids = Array.from({ length: 5 }, (_, index) => `00000000-0000-4000-8000-00000000010${index}`);
const activeTasks = [
  { id: ids[0], type: "daily", text: "Due Daily", completed: false, isDue: true },
  { id: ids[1], type: "daily", text: "Not due", completed: false, isDue: false },
  { id: ids[2], type: "daily", text: "Unknown schedule", completed: false },
  { id: ids[3], type: "todo", text: "Unfinished To Do", completed: false, secret: "omit" },
];
function habitica(request, status = 200, responseHeaders = {}) {
  assert.equal(request.method, 'GET');
  assert.equal(request.headers.get('x-client'), 'ff304279-d0c6-43e5-bd2d-caa41c7e017a-habitica-tool');
  const url = new URL(request.url);
  const id = request.headers.get('x-api-user');
  const name = id === accounts[0].habiticaUserId ? 'Fixture A' : 'Fixture B';
  if (url.searchParams.has('userFields')) return Response.json({ success: true, data: { _id: id, profile: { name } } });
  if (status !== 200) return new Response('Never expose upstream secret ' + accounts[0].apiToken, { status, headers: responseHeaders });
  if (url.pathname.endsWith('/user') && !url.pathname.includes('/tasks/')) return Response.json({ success: true, data: { profile: { name }, stats: { class: 'wizard', lvl: 12, hp: 45, maxHealth: 50, mp: 'malformed', maxMP: 80, exp: 15, toNextLevel: 60, gp: 9 }, flags: { needsCron: true }, party: { _id: 'party-fixture' }, auth: { secret: accounts[0].apiToken } } }, { headers: responseHeaders });
  assert.ok(url.searchParams.get('dueDate'));
  assert.ok(Number.isFinite(Date.parse(url.searchParams.get('dueDate'))));
  return Response.json({ success: true, data: url.searchParams.get('type') === 'completedTodos' ? [{ id: ids[4], type: 'todo', text: 'Completed To Do', completed: true }] : activeTasks }, { headers: responseHeaders });
}
async function connected(t, options = {}, account = accounts[0], scope = 'profile:read account:read tasks:read') {
  const f = await fixture(t, options); const profile = await enrolled(f, account);
  const tokens = await exchange(f, await authorization(f, profile, scope));
  assert.equal(tokens.status, 200);
  const token = (await tokens.json()).access_token;
  const transport = new StreamableHTTPClientTransport(new URL(resource), { requestInit: { headers: { authorization: `Bearer ${token}` } }, fetch: (url, init) => f.mcp.fetch(String(url), init) });
  const client = new Client({ name: 'read-fixture', version: '1.0.0' });
  t.after(() => client.close()); await client.connect(transport);
  return { ...f, profile, token, client };
}
async function rpc(f, token, name, args = {}, moreHeaders = {}) {
  return f.mcp.fetch(resource, { method: 'POST', headers: { authorization: `Bearer ${token}`, 'content-type': 'application/json', accept: 'application/json, text/event-stream', ...moreHeaders }, body: JSON.stringify({ jsonrpc: '2.0', id: 1, method: 'tools/call', params: { name, arguments: args } }) });
}

test('SDK initialization, strict tool schemas and useful read-only outputs', async t => {
  const f = await connected(t, { habitica });
  const tools = (await f.client.listTools()).tools;
  assert.deepEqual(tools.map(tool => tool.name), ['get_profile', 'get_account_status', 'list_tasks', 'get_task']);
  for (const tool of tools) {
    assert.equal(tool.inputSchema.additionalProperties, false); assert.equal(tool.outputSchema.additionalProperties, false);
    assert.equal(tool.annotations.readOnlyHint, true); assert.equal(tool.annotations.destructiveHint, false); assert.equal(tool.annotations.openWorldHint, false);
     assert.ok(tool._meta.securitySchemes.length);
  }
  assert.equal(tools[0]._meta['openai/profile'], true);
  const wireList = await f.mcp.fetch(resource, { method: 'POST', headers: { authorization: `Bearer ${f.token}`, 'content-type': 'application/json', accept: 'application/json, text/event-stream' }, body: JSON.stringify({ jsonrpc: '2.0', id: 2, method: 'tools/list', params: {} }) });
  for (const tool of (await wireList.json()).result.tools) assert.ok(tool.securitySchemes.length);

  const profile = await f.client.callTool({ name: 'get_profile', arguments: {} });
  assert.deepEqual(profile.structuredContent, { id: f.profile.profileId, name: 'Fixture A', nickname: 'Habitica — Fixture A' });
  const stats = await f.client.callTool({ name: 'get_account_status', arguments: {} });
  assert.equal(stats.structuredContent.class, 'wizard'); assert.equal(stats.structuredContent.maxHp, 50); assert.equal(stats.structuredContent.needsCron, true);
  assert.equal(stats.structuredContent.mp, undefined); assert.equal(stats.structuredContent.auth, undefined);
  const due = await f.client.callTool({ name: 'list_tasks', arguments: { type: 'daily', dueOnly: true, completed: false } });
  assert.deepEqual(due.structuredContent.tasks.map(task => task.id), [ids[0]]); assert.equal(due.structuredContent.unknownDueCount, 1);
  const todos = await f.client.callTool({ name: 'list_tasks', arguments: { type: 'todo', completed: false } });
  assert.deepEqual(todos.structuredContent.tasks.map(task => task.id), [ids[3]]);
  const completed = await f.client.callTool({ name: 'list_tasks', arguments: { type: 'todo', completed: true } });
  assert.deepEqual(completed.structuredContent.tasks.map(task => task.id), [ids[4]]); assert.equal(completed.structuredContent.completedTodoCoverage, '30-most-recent-todos');
  const single = await f.client.callTool({ name: 'get_task', arguments: { taskId: ids[4] } });
  assert.equal(single.structuredContent.task.id, ids[4]);
  const unknown = await f.client.callTool({ name: 'get_task', arguments: { taskId: '00000000-0000-4000-8000-000000000999' } });
  assert.equal(unknown.structuredContent.task, null);
  const limited = await f.client.callTool({ name: 'list_tasks', arguments: { query: 'daily', limit: 1 } });
  assert.equal(limited.structuredContent.tasks.length, 1);
  assert.ok(!f.messages.join(" ").includes(accounts[0].apiToken));
  assert.ok(!f.messages.join(" ").includes(accounts[0].habiticaUserId));
  for (const value of [profile, stats, due, todos, completed, single, unknown]) {
    assert.ok(!JSON.stringify(value).includes(accounts[0].apiToken)); assert.ok(!JSON.stringify(value).includes(accounts[0].habiticaUserId));
  }
});

test('scopes, account selectors, origins and public Auth proxy are denied', async t => {
  const f = await connected(t, { habitica }, accounts[0], 'profile:read');
  assert.equal((await rpc(f, f.token, 'list_tasks')).status, 403);
  const challenge = await rpc(f, f.token, 'get_account_status');
  assert.ok(challenge.headers.get('www-authenticate').includes('account:read'));
  const selector = await rpc(f, f.token, 'get_profile', { profileId: 'other' });
  assert.equal((await selector.json()).result.isError, true);
  const wrong = await rpc(f, f.token, 'get_profile', {}, { origin: 'https://hostile.invalid' }); assert.equal(wrong.status, 403);
  const missing = await rpc(f, 'invalid', 'get_profile'); assert.equal(missing.status, 401);
  const publicRead = await f.auth.fetch(issuer + '/read', { method: 'POST', headers: { authorization: `Bearer ${f.token}` }, body: '{}' }); assert.notEqual(publicRead.status, 200);
  for (const name of ['create_task', 'cron', 'cast_spell']) assert.equal((await (await rpc(f, f.token, name)).json()).result.isError, true);
});

test('two OAuth accounts return distinct profile identities without selectors', async t => {
  const f = await connected(t, { habitica });
  const b = await enrolled(f, accounts[1]); const tokens = await exchange(f, await authorization(f, b, 'profile:read'));
  const tokenB = (await tokens.json()).access_token;
  const a = (await (await rpc(f, f.token, 'get_profile')).json()).result.structuredContent;
  const other = (await (await rpc(f, tokenB, 'get_profile')).json()).result.structuredContent;
  assert.equal(a.id, f.profile.profileId); assert.equal(other.id, b.profileId); assert.equal(other.name, 'Fixture B'); assert.notEqual(a.id, other.id);
});

for (const status of [401, 429, 500]) test(`upstream ${status} returns safe guidance without automatic retries`, async t => {
  let calls = 0;
  const f = await connected(t, { habitica: request => { if (!new URL(request.url).searchParams.has('userFields')) calls++; return habitica(request, status, status === 429 ? { 'retry-after': '120', 'x-ratelimit-remaining': '0' } : {}); } });
  const result = await f.client.callTool({ name: 'get_account_status', arguments: {} });
  assert.equal(result.isError, true); assert.equal(calls, 1); assert.ok(!JSON.stringify(result).includes(accounts[0].apiToken));
  if (status === 429) assert.equal(result._meta.rate.retryAfterSeconds, 120);
  if (status !== 500) {
    const again = await f.client.callTool({ name: 'get_account_status', arguments: {} }); assert.equal(again.isError, true); assert.equal(calls, 1);
    const state = await f.db.prepare('SELECT * FROM integration_read_state WHERE profile_id=?').bind(f.profile.profileId).first();
    if (status === 401) assert.equal(state.credential_repair_required, 1); else assert.ok(Date.parse(state.blocked_until_utc) > Date.now());
  }
});

test('exhausted successful quota blocks later calls until upstream reset', async t => {
  let calls = 0;
  const f = await connected(t, { habitica: request => { if (!new URL(request.url).searchParams.has('userFields')) calls++; return habitica(request, 200, { 'x-ratelimit-remaining': '0', 'x-ratelimit-reset': String(Math.ceil(Date.now() / 1000) + 180) }); } });
  assert.ok((await f.client.callTool({ name: 'get_account_status', arguments: {} })).structuredContent);
  assert.equal((await f.client.callTool({ name: 'get_account_status', arguments: {} })).isError, true); assert.equal(calls, 1);
});


test('upstream redirects and malformed envelopes never expose credentials', async t => {
  let redirects = 0;
  const f = await connected(t, { habitica: request => {
    if (new URL(request.url).searchParams.has('userFields')) return habitica(request);
    redirects++;
    return new Response(null, { status: 302, headers: { location: 'https://hostile.invalid/leak' } });
  } });
  const failure = await f.client.callTool({ name: 'get_account_status', arguments: {} });
  assert.equal(failure.isError, true); assert.equal(redirects, 1);
  assert.ok(!JSON.stringify(failure).includes(accounts[0].apiToken));
  const invalid = await f.client.callTool({ name: 'list_tasks', arguments: { limit: 101 } }); assert.equal(invalid.isError, true);
  assert.equal(redirects, 1);
  const direct = await f.internal.fetch('https://auth.internal/read', { method: 'POST', headers: { authorization: `Bearer ${f.token}` }, body: JSON.stringify({ tool: 'get_profile', arguments: {}, profileId: 'other' }) });
  assert.equal(direct.status, 400);
});


test('late concurrent success cannot erase a quota cooldown', async t => {
  let calls = 0; let entered; let release;
  const started = new Promise(resolve => entered = resolve);
  const held = new Promise(resolve => release = resolve);
  t.after(() => release());
  const f = await connected(t, { habitica: async request => {
    if (new URL(request.url).searchParams.has('userFields')) return habitica(request);
    calls++;
    if (calls === 1) { entered(); await held; return habitica(request); }
    return habitica(request, 429, { 'retry-after': '120' });
  } });
  const first = f.client.callTool({ name: 'get_account_status', arguments: {} });
  await started;
  const limited = await f.client.callTool({ name: 'get_account_status', arguments: {} });
  assert.equal(limited.isError, true);
  release(); assert.ok((await first).structuredContent);
  assert.equal((await f.client.callTool({ name: 'get_account_status', arguments: {} })).isError, true);
  assert.equal(calls, 2);
});
