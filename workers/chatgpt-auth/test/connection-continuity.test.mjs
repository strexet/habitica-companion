import { test } from 'node:test';
import assert from 'node:assert/strict';
import { fixture, enrolled, authorization, exchange, resource, issuer, accounts } from './fixtures/oauth.mjs';

test('same verified credentials preserve connected sessions while management credentials rotate', async t => {
  const f = await fixture(t); const profile = await enrolled(f);
  const tokens = await (await exchange(f, await authorization(f, profile))).json();
  const validate = async () => (await f.internal.fetch('https://auth.internal/validate', { method: 'POST', body: JSON.stringify({ resource, token: tokens.access_token }) })).json();
  const first = await validate(); assert.equal(first.props.profileId, profile.profileId);
  const blockedUntil = new Date(Date.now() + 120000).toISOString();
  await f.db.prepare('INSERT INTO integration_read_state(profile_id,connection_id,blocked_until_utc,credential_repair_required) VALUES(?,?,?,1)').bind(profile.profileId, first.props.connectionId, blockedUntil).run();
  const revalidated = await enrolled(f);
  const repaired = await f.db.prepare('SELECT blocked_until_utc,credential_repair_required FROM integration_read_state WHERE profile_id=? AND connection_id=?').bind(profile.profileId, first.props.connectionId).first();
  assert.equal(repaired.credential_repair_required, 0); assert.equal(repaired.blocked_until_utc, blockedUntil);
  assert.equal(revalidated.profileId, profile.profileId); assert.notEqual(revalidated.managementToken, profile.managementToken);
  const after = await validate(); assert.ok(after, 'Signing in on another client must preserve existing grants for unchanged verified credentials.');
  assert.equal(after.props.connectionId, first.props.connectionId);
  assert.equal((await f.auth.fetch(`${issuer}/profiles/${profile.profileId}/disconnect`, { method: 'POST', headers: { authorization: `Bearer ${profile.managementToken}` } })).status, 401);
  await enrolled(f, { ...accounts[0], apiToken: 'fixture-rotated-token' });
  assert.equal(await validate(), null, 'Changed credentials must invalidate the previous connection epoch.');
});


test('concurrent unchanged enrollments preserve the active epoch without stale overwrites', async t => {
  const f = await fixture(t); const profile = await enrolled(f);
  const tokens = await (await exchange(f, await authorization(f, profile))).json();
  const replies = await Promise.all([0, 1].map(() => f.auth.fetch(issuer + '/enroll', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ ...accounts[0], consentVersion: 'integration-v1' }) })));
  assert.ok(replies.some(reply => reply.status === 201)); assert.ok(replies.every(reply => [201, 409].includes(reply.status)));
  const context = await (await f.internal.fetch('https://auth.internal/validate', { method: 'POST', body: JSON.stringify({ resource, token: tokens.access_token }) })).json();
  assert.equal(context.props.profileId, profile.profileId);
});
