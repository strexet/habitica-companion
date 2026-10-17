import { test } from 'node:test';
import assert from 'node:assert/strict';
import { fixture, enrolled, authorization, exchange, issuer, resource } from './fixtures/oauth.mjs';

test('MCP and public/private Auth surfaces cannot dispatch party departure or arbitrary member mutations', async t => {
  let upstreamCalls = 0;
  const f = await fixture(t, { habitica: request => {
    upstreamCalls++;
    assert.equal(request.method, 'GET');
    return Response.json({ success: true, data: { _id: request.headers.get('x-api-user'), profile: { name: 'Fixture A' } } });
  } });
  const profile = await enrolled(f);
  const tokens = await (await exchange(f, await authorization(f, profile))).json();
  const baseline = upstreamCalls;
  for (const name of ['leave_party', 'remove_party_member', 'join_party']) {
    const response = await f.mcp.fetch(resource, { method: 'POST', headers: { authorization: `Bearer ${tokens.access_token}`, 'content-type': 'application/json', accept: 'application/json, text/event-stream' }, body: JSON.stringify({ jsonrpc: '2.0', id: 1, method: 'tools/call', params: { name, arguments: {} } }) });
    const result = await response.json();
    assert.ok(result.error || result.result?.isError, name);
  }
  assert.ok((await f.auth.fetch(issuer + '/groups/party/leave', { method: 'POST', headers: { authorization: `Bearer ${tokens.access_token}` } })).status >= 400);
  assert.equal((await f.internal.fetch('https://auth.internal/groups/party/leave', { method: 'POST' })).status, 401);
  assert.equal(upstreamCalls, baseline, 'No credential-bearing Habitica request is sent for forbidden operations.');
});
