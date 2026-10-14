import { OAuthResourceServer, insufficientScope, type OAuthResourceTokenValidation } from "@cloudflare/workers-oauth-provider";
import { handleMcp, requestBody } from "./tools";
import { readScopes } from "../../shared/read-tools";
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
          const response = await binding.AUTH_SERVER.fetch("https://auth.internal/validate", { method: "POST", body: JSON.stringify({ resource, token }), signal: AbortSignal.timeout(5000) });
          if (!response.ok) throw new Error("Authorization unavailable.");
          const result = await response.json() as OAuthResourceTokenValidation<AuthProps> | null;
          if (result && !authProps(result.props)) return null;
          return result;
        },
        handler: { async fetch(mcpRequest, mcpEnv, context) {
          if (!context.auth.scope?.includes("profile:read")) return insufficientScope(context.auth, ["profile:read"]);
          if (new URL(mcpRequest.url).pathname !== "/mcp") return new Response(null, { status: 404 });
          const origin = mcpRequest.headers.get("origin");
          if (origin && ![new URL(config.resource).origin, config.pwaOrigin, "https://chatgpt.com"].includes(origin)) return new Response(null, { status: 403 });
          if (mcpRequest.method !== "POST") return new Response(null, { status: 405, headers: { allow: "POST" } });
          let body: unknown;
          try { body = await requestBody(mcpRequest.clone()); }
          catch { return Response.json({ error: "invalid_request" }, { status: 400, headers }); }
          const call = body as { method?: string; params?: { name?: string } } | null;
          const name = call?.method === "tools/call" ? call.params?.name : undefined;
          if (name && Object.hasOwn(readScopes, name)) {
            const scope = readScopes[name as keyof typeof readScopes];
            if (!context.auth.scope?.includes(scope)) return insufficientScope(context.auth, [scope]);
          }
          const response = await handleMcp(mcpRequest, mcpEnv, body);
          response.headers.set("cache-control", "no-store");
          return response;
        } },
      });
      return server.fetch(request, env, ctx);
    }
    return Response.json({ error: "unauthorized" }, { status: 401, headers });
  },
} satisfies ExportedHandler<McpEnv>;
