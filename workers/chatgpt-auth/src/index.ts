import { WorkerEntrypoint } from "cloudflare:workers";
import { vaultRoute } from "./vault";
import { resourceConfiguration } from "../../shared/resources";

const headers = { "cache-control": "no-store", "x-content-type-options": "nosniff" };

export class AuthService extends WorkerEntrypoint<AuthEnv> {
  async fetch(request: Request): Promise<Response> {
    const path = new URL(request.url).pathname;
    if (path === "/health" && request.method === "GET") {
      return Response.json({ service: "auth", authorization: "unavailable" }, { headers });
    }
    // Authorization and credential-bearing operations remain denied during scaffolding.
    return Response.json({ error: "unauthorized" }, { status: 401, headers });
  }
}

export default {
  async fetch(request: Request, env: AuthEnv): Promise<Response> {
    resourceConfiguration(env.ENVIRONMENT);
    const vault = await vaultRoute(request, env);
    if (vault) return vault;
    if (new URL(request.url).pathname === "/health" && request.method === "GET") {
      return Response.json({ service: "auth", authorization: "unavailable" }, { headers });
    }
    return Response.json({ error: "unauthorized" }, { status: 401, headers });
  },
} satisfies ExportedHandler<AuthEnv>;
