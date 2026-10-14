import { test } from "node:test";
import assert from "node:assert/strict";
import { fixture, enrolled, ticket, authPage, form, authorization, exchange, handle, cookies, issuer, resource, accounts } from "./fixtures/oauth.mjs";

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
    assert.equal(access.status, 405);
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
