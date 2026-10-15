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

export class PartyService extends WorkerEntrypoint<AuthEnv> {
  async fetch(request:Request):Promise<Response>{return partyProofOperation(request,this.env);}
}

export class ProjectionService extends WorkerEntrypoint<AuthEnv> {
  async fetch(request: Request): Promise<Response> { return projectionOperation(request, this.env); }
}

async function handlePublicRequest(request: Request, env: AuthEnv, ctx: ExecutionContext): Promise<Response> {
    resourceConfiguration(env.ENVIRONMENT);
    const vault = await vaultRoute(request, env, async (id, connectionId) => { if (Boolean(env.OAUTH_ENABLED)) await cleanupProfile(env, id, connectionId); await clearProjectionContext(env,id,connectionId); });
    if (vault) return vault;
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
          if (input.get("resource") !== resourceConfiguration(env.ENVIRONMENT).resource) return Response.json({ error: "invalid_target" }, { status: 400, headers });
        }
        return authorizationServer(env).fetch(request, env, ctx);
      } catch (error) {
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
  async scheduled(_event, env): Promise<void> { await purgeProjectionMetadata(env); },
  async fetch(request: Request, env: AuthEnv, ctx: ExecutionContext): Promise<Response> {
    const url = new URL(request.url);
    const config = resourceConfiguration(env.ENVIRONMENT);
    const isManagement = url.pathname === "/enroll" || url.pathname.startsWith("/profiles/");
    const origin = request.headers.get("origin");
    if (isManagement && url.protocol !== "https:" && !(env.ENVIRONMENT === "local" && ["localhost", "127.0.0.1", "[::1]"].includes(url.hostname))) return Response.json({ error: "invalid_transport" }, { status: 403, headers });
    if (isManagement && origin && origin !== config.pwaOrigin && origin !== config.issuer) return Response.json({ error: "invalid_origin" }, { status: 403, headers });
    const response = isManagement && request.method === "OPTIONS"
      ? new Response(null, { status: 204, headers }) : await handlePublicRequest(request, env, ctx);
    if (isManagement && origin === config.pwaOrigin) {
      response.headers.set("access-control-allow-origin", origin);
      response.headers.set("access-control-allow-methods", "POST, DELETE, OPTIONS");
      response.headers.set("access-control-allow-headers", "content-type, authorization");
      response.headers.set("vary", "Origin");
    }
    return response;
  },
} satisfies ExportedHandler<AuthEnv>;
