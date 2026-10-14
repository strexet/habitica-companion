import { OAuthResourceServer, insufficientScope, type OAuthResourceTokenValidation } from "@cloudflare/workers-oauth-provider";
import { authProps, type AuthProps } from "../../shared/contracts";
import { resourceConfiguration } from "../../shared/resources";

const headers = { "cache-control": "no-store", "x-content-type-options": "nosniff" };

export default {
  async fetch(request: Request, env: McpEnv, ctx: ExecutionContext): Promise<Response> {
    resourceConfiguration(env.ENVIRONMENT);
    const path = new URL(request.url).pathname;
    if (path === "/health" && request.method === "GET") {
      try {
        const upstream = await env.AUTH_SERVER.fetch("https://auth.internal/health");
        if (!upstream.ok) throw new Error("Auth health unavailable.");
        // No upstream body, credentials or headers are exposed by the health check.
        return Response.json({ service: "mcp", authService: "reachable", authorization: "unavailable" }, { headers });
      } catch {
        return Response.json({ error: "auth_service_unavailable" }, { status: 503, headers });
      }
    }

    if (Boolean(env.OAUTH_ENABLED)) {
      const config = resourceConfiguration(env.ENVIRONMENT);
      const server = new OAuthResourceServer<McpEnv, AuthProps>({
        resourceMetadata: { resource: config.resource, authorization_servers: [config.issuer] },
        requiredScopes: ["profile:read"],
        validateToken: binding => async (resource, token) => {
          const response = await binding.AUTH_SERVER.fetch("https://auth.internal/validate", { method: "POST", body: JSON.stringify({ resource, token }) });
          if (!response.ok) throw new Error("Authorization unavailable.");
          const result = await response.json() as OAuthResourceTokenValidation<AuthProps> | null;
          if (result && !authProps(result.props)) return null;
          return result;
        },
        handler: { async fetch(_request, _env, context) {
          if (!context.auth.scope?.includes("profile:read")) return insufficientScope(context.auth, ["profile:read"]);
          return Response.json({ authorized: true, toolsAvailable: false }, { headers });
        } },
      });
      return server.fetch(request, env, ctx);
    }
    return Response.json({ error: "unauthorized" }, { status: 401, headers });
  },
} satisfies ExportedHandler<McpEnv>;
