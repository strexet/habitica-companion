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
  const code = await ticket(f, profile); const page = await authPage(f, "account:read");
  const identified = await form(f, page, { handle: handle(await page.response.text()), decision: "identify", code: code.code, scope: "account:read" });
  assert.equal(identified.status, 200);
  const noProfile = await form(f, page, { handle: handle(await identified.text()), decision: "approve", scope: "account:read" });
  assert.equal(noProfile.status, 400);
  assert.match(await noProfile.text(), /invalid_scope/);
});

test("consent shows the whole catalogue and issues only chosen supported scopes", async t => {
  const f = await fixture(t); const profile = await enrolled(f); const code = await ticket(f, profile);
  const page = await authPage(f, "profile:read tasks:read"); const first = await page.response.text();
  for (const label of ["Identify the linked account","Read tasks","Manage Habitica tags","Cast spells","Read personal Notes","Change personal Notes","Read explicitly shared diagnostics"]) assert.ok(first.includes(label));
  assert.match(first, /One-time Companion code/);
  assert.ok(first.indexOf('<form method="post">') < first.indexOf('name="code"'));
  assert.ok(first.indexOf('name="code"') < first.indexOf('>Verify code</button>'));
  assert.ok(first.indexOf('>Verify code</button>') < first.indexOf('</form>'));
  assert.ok(first.indexOf('</form>') < first.indexOf('id="permission-preview-title"'));
  assert.equal((first.match(/type="checkbox"/g) ?? []).length, 13);
  assert.equal((first.match(/disabled aria-label=/g) ?? []).length, 13);
  assert.equal((first.match(/name="scope"/g) ?? []).length, 0);
  const identified = await form(f, page, { handle: handle(first), decision: "identify", code: code.code, scope: "profile:read tasks:read" });
  assert.equal(identified.status, 200); const html = await identified.text();
  assert.match(html, /name="scope" value="tasks:read" checked/);
  assert.match(html, /name="scope" value="habitica.tags.write"  /);
  assert.ok(html.indexOf('id="permission-preview-title"') === -1);
  assert.ok(html.indexOf('name="scope" value="habitica.tags.write"') < html.indexOf('>Allow selected access</button>'));
  const allowed = await form(f, page, { handle: handle(html), decision: "approve", scope: "profile:read habitica.tags.write" });
  assert.equal(allowed.status, 302);
  const callback = new URL(allowed.headers.get("location"));
  const tokens = await exchange(f, { code: callback.searchParams.get("code"), verifier: page.verifier });
  assert.equal(tokens.status, 200);
  const validated = await f.internal.fetch("https://auth.internal/validate", { method: "POST", body: JSON.stringify({ resource, token: (await tokens.json()).access_token }) });
  const grant = await validated.json();
  assert.deepEqual(grant.scope.sort(), ["profile:read","habitica.tags.write"].sort());
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

test("consent CSP permits Auth posts and only the validated callback origin", async t => {
  const callback = "https://client.example.com/callback?next=ignored;script-src%20*";
  const clientId = "https://client.example.com/oauth-client.json";
  const f = await fixture(t, { clientMetadata: { client_id: clientId, client_name: "Callback fixture", redirect_uris: [callback], token_endpoint_auth_method: "none" } });
  const profile = await enrolled(f);
  for (const overrides of [{}, { client_id: clientId, redirect_uri: callback }]) {
    const page = await authPage(f, "profile:read", overrides);
    const expected = "default-src 'none'; style-src 'unsafe-inline'; form-action 'self' " + new URL(overrides.redirect_uri ?? "http://localhost:9999/callback").origin + "; frame-ancestors 'none'; base-uri 'none'";
    assert.equal(page.response.status, 200);
    assert.equal(page.response.headers.get("content-security-policy"), expected);
    assert.equal(page.response.headers.get("x-frame-options"), "DENY");
    const firstHtml = await page.response.text();
    const code = await ticket(f, profile);
    const identified = await form(f, page, { handle: handle(firstHtml), decision: "identify", code: code.code, scope: "profile:read" });
    assert.equal(identified.status, 200);
    assert.equal(identified.headers.get("content-security-policy"), expected);
    const html = await identified.text();
    const denied = await form(f, page, { handle: handle(html), decision: "deny" });
    assert.equal(denied.status, 302);
    const target = new URL(denied.headers.get("location"));
    assert.equal(target.origin, new URL(overrides.redirect_uri ?? "http://localhost:9999/callback").origin);
    assert.equal(target.searchParams.get("error"), "access_denied");
    assert.equal(target.searchParams.get("state"), "fixture-state");
    assert.equal(target.searchParams.get("iss"), issuer);
  }
  const kv = await f.mf.getKVNamespace("OAUTH_KV", "auth");
  assert.equal((await kv.list({ prefix: "grant:" })).keys.length, 0);
});


test("consent rejects callback hosts that would add CSP wildcards or directives", async t => {
  for (const callback of ["https://*.example.com/callback", "https://client.example.com;sandbox/callback"]) {
    const clientId = "https://client.example.com/oauth-client.json";
    const f = await fixture(t, { clientMetadata: { client_id: clientId, client_name: "Unsafe CSP fixture", redirect_uris: [callback], token_endpoint_auth_method: "none" } });
    const page = await authPage(f, "profile:read", { client_id: clientId, redirect_uri: callback });
    assert.equal(page.response.status, 400);
    assert.match(await page.response.text(), /invalid_redirect/);
    const kv = await f.mf.getKVNamespace("OAUTH_KV", "auth");
    assert.equal((await kv.list({ prefix: "grant:" })).keys.length, 0);
  }
});
test('diagnostic permission is independently requested, disclosed and never implied by task or Notes grants',async t=>{
 const f=await fixture(t),profile=await enrolled(f),code=await ticket(f,profile),page=await authPage(f,'profile:read diagnostics.read'),first=await page.response.text();
 assert.match(first,/Read explicitly shared diagnostics/);assert.match(first,/seven days/);const identified=await form(f,page,{handle:handle(first),decision:'identify',code:code.code,scope:'profile:read diagnostics.read'});assert.equal(identified.status,200);const html=await identified.text();
 assert.match(html,/name="scope" value="diagnostics.read" checked/);assert.match(html,/name="scope" value="notes.read"  /);assert.match(html,/name="scope" value="tasks:read"  /);
 if(process.env.HABITICA_AUTH_BROWSER_EVIDENCE){const {mkdir,writeFile}=await import('node:fs/promises');await mkdir(process.env.HABITICA_AUTH_BROWSER_EVIDENCE,{recursive:true});await writeFile(process.env.HABITICA_AUTH_BROWSER_EVIDENCE+'/diagnostic-consent.html',html);await writeFile(process.env.HABITICA_AUTH_BROWSER_EVIDENCE+'/diagnostic-identify.html',first);}
 const allowed=await form(f,page,{handle:handle(html),decision:'approve',scope:'profile:read'});assert.equal(allowed.status,302);const callback=new URL(allowed.headers.get('location')),tokens=await exchange(f,{code:callback.searchParams.get('code'),verifier:page.verifier});assert.equal(tokens.status,200);assert.equal((await tokens.json()).scope,'profile:read');
});
