import { resourceConfiguration } from "../../shared/resources";

const headers = { "cache-control": "no-store", "x-content-type-options": "nosniff" };

export default {
  async fetch(request: Request, env: McpEnv): Promise<Response> {
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
    return Response.json({ error: "unauthorized" }, { status: 401, headers });
  },
} satisfies ExportedHandler<McpEnv>;
