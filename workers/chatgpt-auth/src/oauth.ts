import { OAuthAuthorizationServer, OAuthError, AuthorizationError, CimdFetchError, type OAuthHelpers } from "@cloudflare/workers-oauth-provider";
import { resourceConfiguration } from "../../shared/resources";
import { authProps, type AuthProps } from "../../shared/contracts";
import { activeProfile, boundedText, VaultError } from "./vault";
import { attempts, consumeTicket, networkAttempts } from "./linking";
import { tokenHash } from "./cryptography";

export const scopes = ["profile:read", "account:read", "tasks:read", "habitica.tasks.write", "workspace.organization.write", "party:read", "party.planning.write", "habitica.spells.execute", "habitica.cron.execute"];
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
  return `<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>${escape(title)}</title><style>body{font:18px system-ui;max-width:38rem;margin:2rem auto;padding:1rem;line-height:1.5}input,button{font:inherit;padding:.6rem;margin:.3rem}button{cursor:pointer}label{display:block}code{overflow-wrap:anywhere}</style><h1>${escape(title)}</h1>${content}</html>`;
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
async function display(oauth: OAuthHelpers, request: Parameters<OAuthHelpers["beginConsent"]>[0], profileLabel?: string) {
  const details = await oauth.describeConsent(request);
  const consent = await oauth.beginConsent(request);
  const labels: Record<string,string>={"workspace.organization.write":"Create, edit and archive native categories, projects, sections, labels and goals","habitica.cron.execute":"Execute an explicitly reviewed Start New Day action with irreversible game effects","habitica.spells.execute":"Execute explicitly reviewed allowlisted spell plans with game effects","party:read":"Read verified party, owned quest scrolls and consented planning state","party.planning.write":"Queue owned quests and set personal votes in shared Companion planning","profile:read":"Identify the linked account","account:read":"Read Habitica stats and day status","tasks:read":"Read Companion tasks and legacy Habitica tasks","habitica.tasks.write":"Save Companion tasks, completions and Habit scores"};
  const writeNotice=details.scope.includes("habitica.tasks.write")?"<p>Task access can save changes and completion or scoring commands. The separately enabled Companion executor can apply those commands to Habitica game state, even while the app is closed. Existing read access does not grant task writes.</p>":"";
  const organizationNotice=details.scope.includes('workspace.organization.write')?'<p>Organization access can create, edit and archive Companion categories, projects, sections, labels and goals after explicit preview approval. Archiving retains related tasks and history. This independent permission does not complete tasks, spend resources or change Habitica game state; existing task-write access does not grant organization writes.</p>':'';
  const cronNotice=details.scope.includes('habitica.cron.execute')?'<p>Start New Day can apply missed-Daily damage, change buffs, reset tasks and process party quest progress. Each action requires an explicit yesterday review and preview approval. Unknown outcomes pause gameplay; no automatic retry or scheduled Cron is permitted.</p>':'';
  const spellNotice=details.scope.includes('habitica.spells.execute')?'<p>Spell access can spend mana and affect the verified current party after explicit preview approval. Habitica may post an automatic spell notice in party chat. Unknown outcomes pause new gameplay effects; no automatic replay or scheduled spell casting is permitted.</p>':'';
  const partyNotice=details.scope.some(scope=>scope.startsWith('party'))?'<p>Authenticated Companion planning is separate from legacy party sync. Shared quest selections and contributor labels are readable by opted-in current party members. Enable separate planning consent in Companion Settings before using shared records. Planning access does not start quests or cast spells.</p>':'';
  const scopeInputs = details.scope.map(scope => `<label><input ${profileLabel ? 'type="checkbox" checked' : 'type="hidden"'} name="scope" value="${escape(scope)}">${profileLabel ? escape(labels[scope]??scope) : ''}</label>`).join("");
  const origin = details.clientDomain ? `Published by ${escape(details.clientDomain)}.` : "Client name is self-declared.";
  const identity = profileLabel ? `<p>Habitica account: <strong>${escape(profileLabel)}</strong></p>` : '<label>One-time Companion code <input name="code" maxlength="16" autocomplete="off" required></label><p>Generate the code from Companion Settings after explicit integration enrollment.</p>';
  const warning = details.redirectIsLoopback ? "<p>This grants access to a local app. Continue only after starting that app's connection flow.</p>" : "";
  const content = `<p>Client: <strong>${escape(details.clientName)}</strong>. ${origin}</p><p>Redirect destination: ${escape(details.redirectHost)}.</p>${warning}${writeNotice}${organizationNotice}${partyNotice}${spellNotice}${cronNotice}<form method="post"><input type="hidden" name="handle" value="${escape(consent.handle)}">${identity}${scopeInputs}<button name="decision" value="${profileLabel ? 'approve' : 'identify'}">${profileLabel ? 'Allow selected access' : 'Verify code'}</button><button name="decision" value="deny" formnovalidate>Deny</button></form><p>Habitica API tokens are never sent to ChatGPT. Credential storage belongs to the opted-in Companion integration.</p>`;
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
      return (await display(oauth, parsed)).response;
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
    const approved = await oauth.approveConsent(request, handle, { scope: form.getAll("scope") });
    if (form.get("decision") === "identify") {
      const profile = await consumeTicket(env, form.get("code") ?? "");
      const next = await display(oauth, approved.request, profile.display_name_cache || "Habitica account");
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
    return Response.json({ error: status === 429 ? "try_later" : "authorization_failed", reason: error instanceof AuthorizationError ? (error.description.startsWith("This authorization was not started") ? "browser_binding_missing" : error.code) : error instanceof VaultError ? error.code : "service_unavailable" }, { status, headers: { "cache-control": "no-store" } });
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
