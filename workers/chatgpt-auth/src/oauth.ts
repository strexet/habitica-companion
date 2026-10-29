import {diagnosticReadScope} from '../../shared/diagnostics';
import {tagWriteScope} from '../../shared/tag-gateway';
import {notesReadScope,notesWriteScope} from '../../shared/notes';
import { OAuthAuthorizationServer, OAuthError, AuthorizationError, CimdFetchError, type OAuthHelpers } from "@cloudflare/workers-oauth-provider";
import { resourceConfiguration } from "../../shared/resources";
import { authProps, type AuthProps } from "../../shared/contracts";
import { activeProfile, boundedText, VaultError } from "./vault";
import { attempts, consumeTicket, networkAttempts } from "./linking";
import { tokenHash } from "./cryptography";

export const scopes = ["profile:read", "account:read", "tasks:read", "habitica.tasks.write", "workspace.organization.write", tagWriteScope, "party:read", "party.planning.write", "habitica.spells.execute", "habitica.cron.execute",notesReadScope,notesWriteScope,diagnosticReadScope];
export function authorizationServer(env: AuthEnv) {
  const config = resourceConfiguration(env.ENVIRONMENT, env);
  return new OAuthAuthorizationServer<AuthEnv>({
    issuer: config.issuer, resources: [config.resource],
    clientRegistrationEndpoint: "/oauth/register",
    clientIdMetadataDocumentEnabled: true,
    scopesSupported: scopes, accessTokenTTL: 600, refreshTokenTTL: 2592000,
    onError: () => {},
    tokenExchangeCallback: async options => {
      const props = authProps(options.props);
      if (!props) throw new OAuthError("invalid_grant", { description: "Connection no longer active." });
      try { const profile=await activeProfile(env, props.profileId, props.connectionId); if(props.grantEpoch!==profile.grant_epoch)throw new VaultError("unauthorized",401); }
      catch (error) {
        if (error instanceof VaultError && error.status === 401) throw new OAuthError("invalid_grant", { description: "Connection no longer active." });
        // invalid_grant revokes the provider grant; storage outages must remain recoverable.
        throw new OAuthError("temporarily_unavailable", { description: "Connection verification temporarily unavailable.", statusCode: 503 });
      }
      if (options.userId !== props.profileId) throw new OAuthError("invalid_grant", { description: "Connection no longer active." });
    },
  });
}
export async function validatedToken(env: AuthEnv, resource: string, token: string) {
  if (!Boolean(env.OAUTH_ENABLED) || resource !== resourceConfiguration(env.ENVIRONMENT, env).resource) return null;
  const validated = await authorizationServer(env).validateToken<AuthProps>(resource, token, env);
  if (!validated) return null;
  const props = authProps(validated.props);
  if (!props || props.profileId !== validated.userId) return null;
  try { const profile=await activeProfile(env, props.profileId, props.connectionId); if(props.grantEpoch!==profile.grant_epoch)throw new VaultError("unauthorized",401); }
  catch (error) { if (error instanceof VaultError) return null; throw error; }
  const used=await env.HABITICA_INTEGRATION_DB.prepare("UPDATE integration_profiles SET last_used_at_utc=? WHERE profile_id=? AND connection_id=? AND grant_epoch=? AND revoked_at_utc IS NULL").bind(new Date().toISOString(),props.profileId,props.connectionId,props.grantEpoch??'').run();
  if(!used.meta.changes)return null;
  return { props, audience: validated.audience, expiresAt: validated.expiresAt, scope: validated.scope, userId: validated.userId, clientId: validated.clientId };
}
const escape = (value: string) => value.replace(/[&<>"']/g, character => `&#${character.charCodeAt(0)};`);
function page(title: string, content: string) {
  return `<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>${escape(title)} · Habitica Companion</title><style>
:root{color-scheme:light dark;font:100%/1.5 system-ui,-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif;--page:#f6f3fa;--card:#fff;--ink:#30243e;--muted:#60576c;--line:#ded4eb;--primary:#6940ad;--primary-ink:#fff;--soft:#f0eaf9;--focus:#8759d8;--warning:#755025}
@media(prefers-color-scheme:dark){:root{--page:#080a12;--card:#111522;--ink:#e7ecf6;--muted:#bac4d7;--line:#39435a;--primary:#a7c1ff;--primary-ink:#090d18;--soft:#1d263d;--focus:#b78cff;--warning:#f4ce91}}
*{box-sizing:border-box}body{margin:0;min-height:100vh;background:radial-gradient(circle at 5% 0%,var(--soft),transparent 38%),var(--page);color:var(--ink);overflow-wrap:anywhere}main{width:min(100% - 2rem,52rem);margin:clamp(1.25rem,5vw,4rem) auto 3rem}.brand{display:flex;align-items:center;gap:.75rem;font-weight:750;color:var(--ink);text-decoration:none}.brand-mark{display:inline-grid;place-items:center;width:2.5rem;height:2.5rem;border-radius:.8rem;background:var(--primary);color:var(--primary-ink);font-size:1.35rem}.panel{margin-top:1.25rem;padding:clamp(1.15rem,3vw,2rem);border:1px solid var(--line);border-radius:1.3rem;background:var(--card);box-shadow:0 18px 48px rgba(25,14,50,.1)}h1{font-size:clamp(1.6rem,4vw,2.1rem);line-height:1.2;margin:.35rem 0 1rem}h2{font-size:1.1rem;margin:1.6rem 0 .5rem}p{margin:.65rem 0;color:var(--muted)}strong,.client-name{color:var(--ink)}.eyebrow{color:var(--primary);font-size:.76rem;font-weight:800;letter-spacing:.12em;text-transform:uppercase}.identity{padding:.85rem 1rem;border:1px solid var(--line);border-radius:.85rem;background:var(--soft)}.identity p{margin:.15rem 0}.note{border-left:3px solid var(--focus);padding:.5rem .8rem;background:var(--soft);border-radius:.35rem}.warning{color:var(--warning)}.code-field{display:grid;gap:.4rem;margin:1.25rem 0}.code-field input{width:100%;min-height:3rem;padding:.7rem .85rem;font:inherit;font-size:1.1rem;letter-spacing:.09em;border:1px solid var(--line);border-radius:.7rem;background:var(--card);color:var(--ink)}.scope-group{margin:1.2rem 0 0;padding:0;border:0}.scope-group legend{padding:0;font-weight:750}.scope-list{display:grid;gap:.55rem;margin-top:.65rem}.scope-row{display:flex;gap:.7rem;align-items:flex-start;padding:.85rem;border:1px solid var(--line);border-radius:.8rem;background:var(--card)}.scope-row:has(input:checked){background:var(--soft)}.scope-row input{width:1.25rem;height:1.25rem;flex:none;margin:.13rem 0;accent-color:var(--primary)}.scope-row input:disabled{opacity:1}.scope-text{flex:1;min-width:0}.scope-title{display:block;font-weight:700;line-height:1.3}.scope-description{display:block;margin-top:.2rem;color:var(--muted);font-size:.91rem}.badge{display:inline-block;align-self:flex-start;white-space:nowrap;padding:.16rem .45rem;border:1px solid var(--line);border-radius:999px;color:var(--muted);font-size:.72rem;font-weight:700}.badge.requested{color:var(--primary)}.actions{display:flex;flex-wrap:wrap;gap:.65rem;margin-top:1.5rem}.actions button,.actions a{display:inline-flex;align-items:center;justify-content:center;min-height:2.75rem;padding:.65rem 1rem;border:1px solid var(--line);border-radius:.7rem;background:var(--card);color:var(--ink);font:inherit;font-weight:700;text-decoration:none;cursor:pointer}.actions .primary{background:var(--primary);border-color:var(--primary);color:var(--primary-ink)}button:hover,a:hover{filter:brightness(1.08)}:focus-visible{outline:3px solid var(--focus);outline-offset:3px}.fineprint{font-size:.87rem}.scope-row.locked{opacity:.9}.permission-preview{margin-top:2rem;padding-top:1rem;border-top:1px solid var(--line)}@media(max-width:38rem){main{width:min(100% - 1rem,52rem);margin:1rem auto}.actions>*{flex:1 1 100%}.scope-row{flex-wrap:wrap}.badge{margin-left:1.9rem}}
</style><main><div class="brand"><span class="brand-mark" aria-hidden="true">✓</span>Habitica Companion</div><section class="panel"><span class="eyebrow">Secure connection</span><h1>${escape(title)}</h1>${content}</section></main></html>`;
}
function consentHeaders(headers: Headers, redirectUri: string) {
  const callback = new URL(redirectUri);
  if (!["https:", "http:"].includes(callback.protocol) || /[;*'"\s]/.test(callback.origin)) throw new VaultError("invalid_redirect", 400);
  headers.set("content-type", "text/html; charset=utf-8");
  // Chromium also checks form-action when a successful form POST redirects to the client.
  headers.set("content-security-policy", `default-src 'none'; style-src 'unsafe-inline'; form-action 'self' ${callback.origin}; frame-ancestors 'none'; base-uri 'none'`);
  headers.set("referrer-policy", "same-origin");
  headers.set("cache-control", "no-store");
  return headers;
}
async function display(oauth: OAuthHelpers, request: Parameters<OAuthHelpers["beginConsent"]>[0], pwaOrigin: string, profileLabel?: string) {
  const details = await oauth.describeConsent(request);
  const consent = await oauth.beginConsent(request);
  const labels: Record<string,string>={
    "profile:read":"Identify the linked account",
    "account:read":"Read Habitica account",
    "tasks:read":"Read tasks",
    "habitica.tasks.write":"Change and complete tasks",
    "workspace.organization.write":"Organize Companion workspace",
    [tagWriteScope]:"Manage Habitica tags",
    "party:read":"Read party and quests",
    "party.planning.write":"Plan party quests",
    "habitica.spells.execute":"Cast spells",
    "habitica.cron.execute":"Start New Day",
    [diagnosticReadScope]:"Read explicitly shared diagnostics",
    [notesReadScope]:"Read personal Notes",
    [notesWriteScope]:"Change personal Notes",
  };
  const writeNotice=details.scope.includes("habitica.tasks.write")?"<p>Task access can save changes and completion or scoring commands. The separately enabled Companion executor can apply those commands to Habitica game state, even while the app is closed. Existing read access does not grant task writes.</p>":"";
  const organizationNotice=details.scope.includes('workspace.organization.write')?'<p>Organization access can create, edit and archive Companion categories, projects, sections, labels, goals and saved task views after explicit preview approval. Archiving retains related tasks and history. Logical project removal requires a separate exact reference preview, retains tasks and history, and presents retained project assignments as Unassigned. This independent permission does not complete tasks, spend resources or change Habitica game state; existing task-write access does not grant organization writes.</p>':'';
  const tagNotice=details.scope.includes(tagWriteScope)?'<p>Tag access is separate from Companion organization and task writes. With additional tag consent in Companion, reviewed metadata changes can create or rename owned tags and change individual task membership. Unrelated tags are preserved. Tags are never automatically deleted; this permission grants no scoring, spells, party changes or Start New Day.</p>':'';
  const cronNotice=details.scope.includes('habitica.cron.execute')?'<p>Start New Day can apply missed-Daily damage, change buffs, reset tasks and process party quest progress. Each action requires an explicit yesterday review and preview approval. Unknown outcomes pause gameplay; no automatic retry or scheduled Cron is permitted.</p>':'';
  const spellNotice=details.scope.includes('habitica.spells.execute')?'<p>Spell access can spend mana and affect the verified current party after explicit preview approval. Habitica may post an automatic spell notice in party chat. Unknown outcomes pause new gameplay effects; no automatic replay or scheduled spell casting is permitted.</p>':'';
  const partyNotice=details.scope.some(scope=>scope.startsWith('party'))?'<p>Authenticated Companion planning is separate from legacy party sync. Shared quest selections and contributor labels are readable by opted-in current party members. Enable separate planning consent in Companion Settings before using shared records. Planning access does not start quests or cast spells.</p>':'';
  const descriptions: Record<string,string>={
    "profile:read":"Required for every MCP connection; identifies the linked Habitica account.",
    "account:read":"Reads stats and day status without changing gameplay.",
    "tasks:read":"Reads task titles, details and completion state.",
    "habitica.tasks.write":"Can change tasks and trigger reviewed scoring with Habitica rewards or penalties.",
    "workspace.organization.write":"Can organize projects, categories and views after review; does not score tasks.",
    [tagWriteScope]:"Can change owned Habitica tags and reviewed task membership after separate Companion consent.",
    "party:read":"Reads current party and consented shared planning.",
    "party.planning.write":"Can queue owned quests and cast a personal vote; cannot leave the party.",
    "habitica.spells.execute":"Can spend mana and affect the party after a separate spell preview.",
    "habitica.cron.execute":"Can start a new day with irreversible task, buff and quest effects after a separate preview.",
    [diagnosticReadScope]:"Can read explicitly shared sanitized browser diagnostics and reduced own server receipts after separate diagnostic consent. Shared content expires within seven days; no hidden browser access or game effects.",
    [notesReadScope]:"Can search and read sensitive Markdown, revisions and trash after separate Notes cloud consent.",
    [notesWriteScope]:"Can save, edit, trash or permanently purge Notes after explicit requests and separate Notes cloud consent.",
  };
  const notesNotice=details.scope.some(scope=>[notesReadScope,notesWriteScope].includes(scope))?'<p>Personal Notes can contain sensitive personal facts and documents. Notes access requires separate Notes cloud consent in Companion. Existing task access does not grant Notes access. The server can read stored titles, Markdown and retained revisions until permanent deletion. Notes do not create Habitica tasks or game effects.</p>':'';
  const groups = [
    { title: "Account and tasks", items: ["profile:read","account:read","tasks:read","habitica.tasks.write","workspace.organization.write",tagWriteScope] },
    { title: "Party and gameplay", items: ["party:read","party.planning.write","habitica.spells.execute","habitica.cron.execute"] },
    { title: "Personal notes", items: [notesReadScope,notesWriteScope] },
    { title: "Shared diagnostics", items: [diagnosticReadScope] },
  ];
  const scopeInputs = groups.map(group => `<fieldset class="scope-group"><legend>${escape(group.title)}</legend><div class="scope-list">${group.items.map(scope => {
    const requested=details.scope.includes(scope), required=scope==="profile:read";
    const input=profileLabel ? `<input type="checkbox" name="scope" value="${escape(scope)}" ${requested||required?"checked":""} ${required?"disabled":""} aria-label="${escape(labels[scope]??scope)}">${required?'<input type="hidden" name="scope" value="profile:read">':''}` : `<input type="checkbox" ${requested||required?"checked":""} disabled aria-label="${escape(labels[scope]??scope)} (preview)">`;
    return `<label class="scope-row ${required?"locked":""}">${input}<span class="scope-text"><span class="scope-title">${escape(labels[scope]??scope)}</span><span class="scope-description">${escape(descriptions[scope]??"")}</span></span><span class="badge ${requested?"requested":""}">${required?"Required":requested?"App requested":"Available"}</span></label>`;
  }).join("")}</div></fieldset>`).join("");
  const origin = details.clientDomain ? `Published by ${escape(details.clientDomain)}.` : "Client name is self-declared.";
  const identity = profileLabel ? `<div class="identity"><p>Habitica account: <strong>${escape(profileLabel)}</strong></p></div>` : `<p>Start <strong>Authenticate</strong> in Codex or <strong>Connect</strong> in ChatGPT. Generate a code in <a href="${escape(pwaOrigin)}/settings">Companion Settings</a>, then paste it here. Never put the code in MCP settings or chat.</p><label class="code-field"><strong>One-time Companion code</strong><input name="code" maxlength="16" autocomplete="one-time-code" autocapitalize="characters" spellcheck="false" required></label><p class="fineprint">Codes last ten minutes and work once. If verification fails, generate a fresh code in Companion Settings.</p>`;
  const warning = details.redirectIsLoopback ? "<p class=\"note\">The return address belongs to a local app on this computer. Continue only if you just started that connection.</p>" : "";
  const notices = [writeNotice,organizationNotice,tagNotice,partyNotice,spellNotice,cronNotice,notesNotice].filter(Boolean).join("");
  const permissionReview = profileLabel
    ? `<h2>Choose permissions</h2><p>App-requested permissions start selected. Other permissions start off. Only checked permissions will be granted.</p>${scopeInputs}${notices?`<div class="note">${notices}</div>`:""}`
    : `<section class="permission-preview" aria-labelledby="permission-preview-title"><h2 id="permission-preview-title">Permissions available after verification</h2><p>This preview shows requested access. Verify your code to choose permissions on the next screen.</p>${scopeInputs}</section>`;
  const actions = `<div class="actions"><button class="primary" name="decision" value="${profileLabel ? 'approve' : 'identify'}">${profileLabel ? 'Allow selected access' : 'Verify code'}</button><button name="decision" value="deny" formnovalidate>Cancel connection</button></div>`;
  const content = `<div class="identity"><p>App requesting access: <strong class="client-name">${escape(details.clientName)}</strong></p><p>${origin} Return address: <strong>${escape(details.redirectHost)}</strong></p></div>${warning}<form method="post"><input type="hidden" name="handle" value="${escape(consent.handle)}">${identity}${profileLabel?permissionReview:""}${actions}</form>${profileLabel?"":permissionReview}<p class="fineprint">Habitica API tokens are never sent to the connected client. Companion stores credentials only after separate enrollment consent.</p>`;
  return { consent, response: new Response(page(profileLabel ? "Approve Companion access" : "Link Companion account", content), { headers: consentHeaders(consent.headers, details.redirectUri) }) };
}
export async function authorize(request: Request, env: AuthEnv): Promise<Response> {
  const server = authorizationServer(env);
  const oauth = server.getOAuthApi(env);
  try {
    const config = resourceConfiguration(env.ENVIRONMENT, env);
    if (new URL(request.url).origin !== config.issuer) throw new VaultError("invalid_origin", 400);
    if (request.method === "GET") {
      if (new URL(request.url).searchParams.get("resource") !== config.resource) throw new VaultError("invalid_resource", 400);
      const parsed = await oauth.parseAuthRequest(request);
      if (!parsed.codeChallenge || parsed.codeChallengeMethod !== "S256") throw new VaultError("invalid_pkce", 400);
      return (await display(oauth, parsed,config.pwaOrigin)).response;
    }
    if (request.method !== "POST") throw new VaultError("invalid_request", 405);
    if (request.headers.get("origin") && request.headers.get("origin") !== config.issuer) throw new VaultError("invalid_origin", 403);
    await networkAttempts(request, env);
    const form = new URLSearchParams(await boundedText(request, 16384));
    const handle = form.get("handle") ?? "";
    await attempts(env, "session:" + await tokenHash(handle), 5, 600);
    if (form.get("decision") === "deny") {
      const denied = await oauth.denyConsent(request, handle);
      await env.HABITICA_INTEGRATION_DB.prepare("DELETE FROM consent_links WHERE handle_hash=?").bind(await tokenHash(handle)).run();
      return new Response(null, { status: 302, headers: denied.headers });
    }
    if (!["identify", "approve"].includes(form.get("decision") ?? "")) throw new VaultError("invalid_request", 400);
    const selectedScopes = form.getAll("scope");
    if (form.get("decision")==="approve" && (!selectedScopes.includes("profile:read") || selectedScopes.some(scope=>!scopes.includes(scope)) || new Set(selectedScopes).size!==selectedScopes.length)) throw new VaultError("invalid_scope",400);
    const approved = await oauth.approveConsent(request, handle, { scope: selectedScopes });
    if (form.get("decision") === "identify") {
      const profile = await consumeTicket(env, form.get("code") ?? "");
      const next = await display(oauth, approved.request, config.pwaOrigin, profile.display_name_cache || "Habitica account");
      // Preserve both the old-cookie deletion and new browser-binding cookie.
      for (const cookie of approved.headers.getSetCookie()) next.response.headers.append("set-cookie", cookie);
      await env.HABITICA_INTEGRATION_DB.prepare("INSERT INTO consent_links(handle_hash,profile_id,connection_id,expires_at_utc,grant_epoch) VALUES(?,?,?,?,?)")
        .bind(await tokenHash(next.consent.handle), profile.profile_id, profile.connection_id, new Date(Date.now() + 600000).toISOString(),profile.grant_epoch).run();
      return next.response;
    }
    const now = new Date().toISOString();
    const link = await env.HABITICA_INTEGRATION_DB.prepare("UPDATE consent_links SET consumed_at_utc=? WHERE handle_hash=? AND consumed_at_utc IS NULL AND expires_at_utc>? RETURNING profile_id,connection_id,grant_epoch")
      .bind(now, await tokenHash(handle), now).first<{ profile_id: string; connection_id: string; grant_epoch:string }>();
    if (!link) throw new VaultError("invalid_consent", 400);
    const profile=await activeProfile(env, link.profile_id, link.connection_id);if(profile.grant_epoch!==link.grant_epoch)throw new VaultError("invalid_consent",400);
    const { redirectTo } = await oauth.completeAuthorization({ request: approved.request, userId: link.profile_id, scope: approved.request.scope, metadata: { connectionId: link.connection_id, grantEpoch:link.grant_epoch }, props: { profileId: link.profile_id, connectionId: link.connection_id, grantEpoch:link.grant_epoch } });
    approved.headers.set("location", redirectTo);
    return new Response(null, { status: 302, headers: approved.headers });
  } catch (error) {
    if (error instanceof AuthorizationError && request.method === "GET" && error.redirectTo) return Response.redirect(error.redirectTo, 302);
    const status = error instanceof VaultError ? error.status : error instanceof AuthorizationError || error instanceof CimdFetchError ? 400 : 503;
    const reason=error instanceof AuthorizationError ? (error.description.startsWith("This authorization was not started") ? "browser_binding_missing" : error.code) : error instanceof VaultError ? error.code : "service_unavailable";
    const detail=reason==="browser_binding_missing"?"This authorization page lost the browser session that started the connection. Return to Codex or ChatGPT and select Authenticate or Connect again in the same browser.":reason==="invalid_linking_code"?"The one-time code is invalid, expired or already used. Generate a fresh code in Companion Settings, then restart the connection.":status===429?"Too many attempts. Wait before starting a new connection.":status===503?"The connection service is temporarily unavailable. Try again later.":"This connection step could not be completed. Restart the connection from Codex or ChatGPT and use a fresh code if needed.";
    const headers=new Headers({"content-type":"text/html; charset=utf-8","cache-control":"no-store","referrer-policy":"no-referrer","content-security-policy":"default-src 'none'; style-src 'unsafe-inline'; frame-ancestors 'none'; base-uri 'none'"});
    return new Response(page("Connection needs a restart",`<p role="alert">${escape(detail)}</p><p class="fineprint">Error reference: ${escape(reason)}</p><div class="actions"><a class="primary" href="${escape(resourceConfiguration(env.ENVIRONMENT,env).pwaOrigin)}/settings">Open Companion Settings</a></div>`),{status,headers});
  }
}

export async function cleanupProfile(env: AuthEnv, profileId: string, connectionId: string): Promise<void> {
  await env.HABITICA_INTEGRATION_DB.batch([
    env.HABITICA_INTEGRATION_DB.prepare("DELETE FROM integration_read_state WHERE profile_id=? AND connection_id=?").bind(profileId, connectionId),
    env.HABITICA_INTEGRATION_DB.prepare("DELETE FROM linking_tickets WHERE profile_id=? AND connection_id=?").bind(profileId, connectionId),
    env.HABITICA_INTEGRATION_DB.prepare("DELETE FROM consent_links WHERE profile_id=? AND connection_id=?").bind(profileId, connectionId),
  ]);
  const oauth = authorizationServer(env).getOAuthApi(env);
  let cursor: string | undefined;
  for (let page = 0; page < 5; page++) {
    const grants = await oauth.listUserGrants(profileId, { limit: 20, cursor });
    for (const grant of grants.items) {
      if (grant.metadata?.connectionId === connectionId) await oauth.revokeGrant(grant.id, profileId);
    }
    cursor = grants.cursor;
    if (!cursor) return;
  }
  throw new Error("Grant cleanup requires another bounded pass.");
}
