import {callbackEgressClaim} from './callback-egress';
import {pushDispatchRoute} from './push-dispatch';
import {pushDispatchAvailable} from './push-message';
import {identityEnvelope} from '../../shared/build-identity';
import {pushRoute,pushStatus,clearPushContext,expirePush} from './push-subscriptions';
import {activeProfile} from './vault';
import {workspaceJson,workspaceObject,uuid} from '../../shared/workspace-http';
import {tagOperation} from './tag-gateway';
import {tagConsentRoute,clearTagContext} from './tag-consent';
import {purgePacing} from './upstream-pacing';
import {networkBudget,trafficFailure} from './traffic';
import {trafficOperation} from './traffic-route';
import {keyOperation,purgeKeyJobs} from './key-operations';
import {managementRoute} from './management';
import {cronOperation} from './cron';
import {spellOperation} from './spells';
import {partyProofOperation} from './party';
import { projectionOperation, clearProjectionContext, purgeProjectionMetadata } from "./projection";
import { workspaceIdentity } from "./workspace-identity";
import { readOperation } from "./reads";
import { WorkerEntrypoint } from "cloudflare:workers";
import { vaultRoute, boundedJson, boundedText, VaultError } from "./vault";
import { ticketRoute, networkAttempts } from "./linking";
import { authorizationServer, authorize, validatedToken, cleanupProfile } from "./oauth";
import { resourceConfiguration } from "../../shared/resources";

const headers = { "cache-control": "no-store", "x-content-type-options": "nosniff" };

export class AuthService extends WorkerEntrypoint<AuthEnv> {
  async fetch(request: Request): Promise<Response> {
    const path = new URL(request.url).pathname;
    if(path === "/build-identity" && request.method === "GET") return Response.json(identityEnvelope("auth"), {headers});
    if(path === "/traffic") return trafficOperation(request,this.env);
    if (path === "/health" && request.method === "GET") {
      return Response.json({ service: "auth", authorization: "unavailable" }, { headers });
    }
    if (path === "/validate" && request.method === "POST") {
      try {
        const input = await boundedJson(request, 8192) as { resource?: string; token?: string };
        if (typeof input.resource !== "string" || typeof input.token !== "string") throw new VaultError("invalid_request", 400);
        return Response.json(await validatedToken(this.env, input.resource, input.token), { headers });
      } catch { return Response.json({ error: "authorization_unavailable" }, { status: 503, headers }); }
    }
    if (path === "/read" && request.method === "POST") return readOperation(request, this.env);
    // Credential-bearing operations are never exposed by the public handler.
    return Response.json({ error: "unauthorized" }, { status: 401, headers });
  }
}

// This binding belongs only to the Workspace Worker; public routes never dispatch named entrypoints.
export class WorkspaceIdentityService extends WorkerEntrypoint<AuthEnv> {
  async fetch(request: Request): Promise<Response> { return workspaceIdentity(request, this.env); }
}

export class PushService extends WorkerEntrypoint<AuthEnv> {
  async fetch(request:Request):Promise<Response>{
    if(['/dispatch','/receipt'].includes(new URL(request.url).pathname))return pushDispatchRoute(request,this.env);
    try{
      if(request.method!=='POST'||new URL(request.url).pathname!=='/status')throw new Error();
      const body=workspaceObject(await workspaceJson(request,1024),['profileId','connectionId']);
      if(typeof body.profileId!=='string'||!/^prf_[A-Za-z0-9_-]{43}$/.test(body.profileId))throw new Error();
      return Response.json({...await pushStatus(this.env,await activeProfile(this.env,body.profileId,uuid(body.connectionId))),dispatchAvailable:await pushDispatchAvailable(this.env)},{headers});
    }catch{return Response.json({error:'push_unavailable'},{status:503,headers});}
  }
}

export class CredentialKeyService extends WorkerEntrypoint<AuthEnv> {
  async fetch(request:Request):Promise<Response>{return keyOperation(request,this.env);}
}

export class CronService extends WorkerEntrypoint<AuthEnv> {
  async fetch(request:Request):Promise<Response>{return cronOperation(request,this.env);}
}

export class SpellService extends WorkerEntrypoint<AuthEnv> {
  async fetch(request:Request):Promise<Response>{return spellOperation(request,this.env);}
}

export class PartyService extends WorkerEntrypoint<AuthEnv> {
  async fetch(request:Request):Promise<Response>{return partyProofOperation(request,this.env);}
}

export class ProjectionService extends WorkerEntrypoint<AuthEnv> {
  async fetch(request: Request): Promise<Response> { return new URL(request.url).pathname==="/tags"?tagOperation(request,this.env):projectionOperation(request, this.env); }
}

async function handlePublicRequest(request: Request, env: AuthEnv, ctx: ExecutionContext): Promise<Response> {
    resourceConfiguration(env.ENVIRONMENT, env);
    const callbackClaim = await callbackEgressClaim(request,env); if(callbackClaim)return callbackClaim;
    const vault = await vaultRoute(request, env, async (id, connectionId,deleteProfile) => {
      if (Boolean(env.OAUTH_ENABLED)) await cleanupProfile(env, id, connectionId);
      await clearProjectionContext(env,id,connectionId); await clearTagContext(env,id,connectionId);
      await clearPushContext(env,id,connectionId,deleteProfile);
      if(env.NOTES_LIFECYCLE){
        const cleanup=await env.NOTES_LIFECYCLE.fetch('https://notes.internal/cleanup',{method:'POST',body:JSON.stringify({profileId:id,connectionId,deleteProfile}),signal:AbortSignal.timeout(10000)});
        if(!cleanup.ok||(await cleanup.json<{cleaned?:boolean}>()).cleaned!==true)throw new Error('Notes cleanup pending.');
      }else if(env.WORKSPACE_ENABLED)throw new Error('Notes cleanup binding unavailable.');
    });
    if (vault) return vault;
    const push=await pushRoute(request,env);if(push)return push;
    const tagConsent=await tagConsentRoute(request,env);if(tagConsent)return tagConsent;
    const management=await managementRoute(request,env);if(management)return management;
    if (new URL(request.url).pathname === "/health" && request.method === "GET") return Response.json({ service: "auth", authorization: Boolean(env.OAUTH_ENABLED) ? "enabled" : "unavailable" }, { headers });
    if (Boolean(env.OAUTH_ENABLED)) {
      try {
        const ticket = await ticketRoute(request, env);
        if (ticket) return ticket;
        const path = new URL(request.url).pathname;
        if (path === "/authorize") return authorize(request, env);
        if (path === "/oauth/register") await networkAttempts(request, env);
        if (path === "/oauth/token" && request.method === "POST") {
          const input = new URLSearchParams(await boundedText(request.clone(), 16384));
          // Refresh may inherit the resource already bound to the validated grant.
          const inheritedRefresh = input.get("grant_type") === "refresh_token" && !input.has("resource");
          if (!inheritedRefresh && input.get("resource") !== resourceConfiguration(env.ENVIRONMENT, env).resource) return Response.json({ error: "invalid_target" }, { status: 400, headers });
        }
        return authorizationServer(env).fetch(request, env, ctx);
      } catch (error) {
        const quota=trafficFailure(error);if(quota)return quota;
        const status = error instanceof VaultError ? error.status : 503;
        return Response.json({ error: status === 429 ? "try_later" : "integration_unavailable" }, { status, headers });
      }
    }
    if (new URL(request.url).pathname === "/health" && request.method === "GET") {
      return Response.json({ service: "auth", authorization: "unavailable" }, { headers });
    }
    return Response.json({ error: "unauthorized" }, { status: 401, headers });
}
export default {
  async scheduled(_event, env): Promise<void> { await expirePush(env);await purgeProjectionMetadata(env); await purgeKeyJobs(env); await purgePacing(env); await env.HABITICA_INTEGRATION_DB.batch([env.HABITICA_INTEGRATION_DB.prepare("DELETE FROM integration_tag_checks WHERE expires_at_utc<=?").bind(new Date().toISOString()),env.HABITICA_INTEGRATION_DB.prepare("DELETE FROM integration_tag_attempt_details WHERE created_at_utc<?").bind(new Date(Date.now()-30*86400000).toISOString())]); },
  async fetch(request: Request, env: AuthEnv, ctx: ExecutionContext): Promise<Response> {
    const url = new URL(request.url);
    const config = resourceConfiguration(env.ENVIRONMENT, env);
    const isManagement = url.pathname === "/enroll" || url.pathname.startsWith("/profiles/");
    const origin = request.headers.get("origin");
    if (isManagement && url.protocol !== "https:" && !(env.ENVIRONMENT === "local" && ["localhost", "127.0.0.1", "[::1]"].includes(url.hostname))) return Response.json({ error: "invalid_transport" }, { status: 403, headers });
    if (isManagement && origin && origin !== config.pwaOrigin && origin !== config.issuer) return Response.json({ error: "invalid_origin" }, { status: 403, headers });
    let response:Response;
    try {
      if(request.method!=="OPTIONS"&&url.pathname!=="/health"&&(env.OAUTH_ENABLED||env.ENROLLMENT_ENABLED||env.WORKSPACE_ENABLED))await networkBudget(env,request,url.pathname==="/enroll"?"enrollment":"public");
      response=isManagement&&request.method==="OPTIONS"?new Response(null,{status:204,headers}):await handlePublicRequest(request,env,ctx);
    }catch(error){response=trafficFailure(error)??Response.json({error:"integration_unavailable"},{status:503,headers});}
    if (isManagement && origin === config.pwaOrigin) {
      response.headers.set("access-control-allow-origin", origin);
      response.headers.set("access-control-allow-methods", "GET, POST, DELETE, OPTIONS");
      response.headers.set("access-control-allow-headers", "content-type, authorization");
      response.headers.set("vary", "Origin");
    }
    return response;
  },
} satisfies ExportedHandler<AuthEnv>;
