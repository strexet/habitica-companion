import { test } from 'node:test';
import assert from 'node:assert/strict';
import { fixture, enrolled, authorization, exchange, issuer, resource } from './fixtures/oauth.mjs';

const rpc = (f, token, method, params = {}) => f.mcp.fetch(resource, {
  method: 'POST', headers: { authorization: `Bearer ${token}`, 'content-type': 'application/json', accept: 'application/json, text/event-stream' },
  body: JSON.stringify({ jsonrpc: '2.0', id: 1, method, params }),
});
const refresh = (f, tokens, fields = {}) => f.auth.fetch(issuer + '/oauth/token', {
  method: 'POST', headers: { 'content-type': 'application/x-www-form-urlencoded' },
  body: new URLSearchParams({ grant_type: 'refresh_token', refresh_token: tokens.refresh_token, client_id: f.clientId, ...fields }),
});
const valid = async (f, token) => (await f.internal.fetch('https://auth.internal/validate', { method: 'POST', body: JSON.stringify({ resource, token }) })).json();

test('bootstrap tools work repeatedly with exactly the scopes advertised on the wire', async t => {
  for (const name of ['get_profile', 'get_account_status', 'list_tasks', 'get_task']) await t.test(name, async t => {
    const f = await fixture(t, { habitica: request => Response.json({ success: true, data: new URL(request.url).pathname.includes('/tasks/') ? [] : { _id: request.headers.get('x-api-user'), profile: { name: 'Fixture A' }, needsCron: false } }) });
    const profile = await enrolled(f);
    const discovery = await (await exchange(f, await authorization(f, profile))).json();
    const tools = (await (await rpc(f, discovery.access_token, 'tools/list')).json()).result.tools;
    const tool = tools.find(tool => tool.name === name);
    const advertised = tool.securitySchemes[0].scopes;
    assert.deepEqual(tool._meta.securitySchemes, tool.securitySchemes);
    assert.ok(advertised.includes('profile:read'), name + ' must advertise the enforced base scope');
    assert.equal(new Set(advertised).size, advertised.length);
    const tokens = await (await exchange(f, await authorization(f, profile, advertised.join(' ')))).json();
    for (let attempt = 0; attempt < 2; attempt++) {
      const response = await rpc(f, tokens.access_token, 'tools/call', { name, arguments: name === 'get_task' ? { taskId: '00000000-0000-4000-8000-000000000999' } : {} });
      assert.equal(response.status, 200, name);
      assert.equal((await response.json()).result.isError, undefined, name);
    }
  });
});

test('refresh inherits the bound resource, rotates tokens and preserves repeated access', async t => {
  const f = await fixture(t); const profile = await enrolled(f);
  let tokens = await (await exchange(f, await authorization(f, profile, 'profile:read account:read'))).json();
  assert.equal(tokens.expires_in, 600);
  for (const fields of [{}, { resource }, {}]) {
    const response = await refresh(f, tokens, fields);
    assert.equal(response.status, 200, 'An omitted resource inherits the already bound grant');
    const next = await response.json();
    assert.notEqual(next.refresh_token, tokens.refresh_token);
    assert.notEqual(next.access_token, tokens.access_token);
    const context = await valid(f, next.access_token);
    assert.equal(context.audience, resource);
    assert.deepEqual(context.scope.sort(), ['account:read', 'profile:read']);
    for (let attempt = 0; attempt < 2; attempt++) assert.equal((await rpc(f, next.access_token, 'tools/list')).status, 200);
    tokens = next;
  }
  await f.auth.fetch(`${issuer}/profiles/${profile.profileId}/disconnect`, { method: 'POST', headers: { authorization: `Bearer ${profile.managementToken}` } });
  const denied = await refresh(f, tokens);
  assert.equal(denied.status, 400); assert.equal((await denied.json()).error, 'invalid_grant');
});

test('refresh rejects explicit wrong audience and wrong client without destroying the valid grant', async t => {
  const f = await fixture(t); const profile = await enrolled(f);
  const tokens = await (await exchange(f, await authorization(f, profile))).json();
  const wrongResource = await refresh(f, tokens, { resource: 'https://other.invalid/mcp' });
  assert.equal(wrongResource.status, 400); assert.equal((await wrongResource.json()).error, 'invalid_target');
  const wrongClient = await refresh(f, tokens, { resource, client_id: 'unregistered-client' });
  assert.ok(wrongClient.status >= 400);
  assert.ok(await valid(f, tokens.access_token));
  assert.equal((await refresh(f, tokens, { resource })).status, 200);
});

test('temporary profile storage failure does not revoke a recoverable refresh grant', async t => {
  const f = await fixture(t); const profile = await enrolled(f);
  const tokens = await (await exchange(f, await authorization(f, profile))).json();
  await f.db.prepare('ALTER TABLE integration_profiles RENAME TO fixture_profiles_unavailable').run();
  let response;
  try { response = await refresh(f, tokens, { resource }); }
  finally { await f.db.prepare('ALTER TABLE fixture_profiles_unavailable RENAME TO integration_profiles').run(); }
  assert.equal(response.status, 503);
  assert.equal((await response.json()).error, 'temporarily_unavailable');
  assert.ok(await valid(f, tokens.access_token));
  assert.equal((await refresh(f, tokens, { resource })).status, 200);
});


test('authorizing a second registered client preserves the first client grant', async t => {
  const f = await fixture(t); const profile = await enrolled(f);
  const first = await (await exchange(f, await authorization(f, profile))).json();
  const registration = await (await f.auth.fetch(issuer + '/oauth/register', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ client_name: 'Fixture <script>untrusted</script>', redirect_uris: ['http://localhost:9999/callback'], token_endpoint_auth_method: 'none', grant_types: ['authorization_code', 'refresh_token'], response_types: ['code'] }) })).json();
  assert.notEqual(registration.client_id, f.clientId);
  const second = await (await exchange(f, await authorization(f, profile, 'profile:read', { client_id: registration.client_id }))).json();
  assert.ok(await valid(f, first.access_token)); assert.ok(await valid(f, second.access_token));
  assert.equal((await refresh(f, first)).status, 200);
  assert.ok(await valid(f, second.access_token));
});
