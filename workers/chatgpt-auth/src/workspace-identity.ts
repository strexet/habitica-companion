import { activeProfile } from "./vault";
import { tokenHash, tokenMatches } from "./cryptography";
import { validatedToken } from "./oauth";
import { resourceConfiguration } from "../../shared/resources";
import { bearer, workspaceJson, workspaceObject, WorkspaceError } from "../../shared/workspace-http";

export async function workspaceIdentity(request: Request, env: AuthEnv): Promise<Response> {
  try {
    if (!Boolean(env.WORKSPACE_ENABLED) || request.method !== "POST") throw new WorkspaceError("unauthorized", 401);
    const path = new URL(request.url).pathname;
    if (path === "/management") {
      const token = bearer(request);
      const row = await env.HABITICA_INTEGRATION_DB.prepare("SELECT profile_id,management_token_hash FROM integration_profiles WHERE management_token_hash=?").bind(await tokenHash(token)).first<{ profile_id: string; management_token_hash: string }>();
      if (!row || !await tokenMatches(token, row.management_token_hash)) throw new WorkspaceError("unauthorized", 401);
      const profile = await activeProfile(env, row.profile_id);
      return Response.json({ profileId: profile.profile_id, connectionId: profile.connection_id }, { headers: { "cache-control": "no-store" } });
    }
    if (path === "/epoch") {
      const body = workspaceObject(await workspaceJson(request), ["profileId", "connectionId"]);
      if (typeof body.profileId !== "string" || !/^prf_[A-Za-z0-9_-]{43}$/.test(body.profileId) || typeof body.connectionId !== "string") throw new WorkspaceError("unauthorized", 401);
      const profile = await activeProfile(env, body.profileId, body.connectionId);
      return Response.json({ profileId: profile.profile_id, connectionId: profile.connection_id }, { headers: { "cache-control": "no-store" } });
    }
    if (path === "/mcp") {
      const token = request.headers.get("authorization")?.match(/^Bearer (\S+)$/)?.[1];
      const context = token && token.length <= 4096 ? await validatedToken(env, resourceConfiguration(env.ENVIRONMENT).resource, token) : null;
      if (!context) throw new WorkspaceError("unauthorized", 401);
      return Response.json({ profileId: context.props.profileId, connectionId: context.props.connectionId, scopes: context.scope }, { headers: { "cache-control": "no-store" } });
    }
    throw new WorkspaceError("unauthorized", 401);
  } catch { return Response.json({ error: "unauthorized" }, { status: 401, headers: { "cache-control": "no-store" } }); }
}
