import {requireTagConsent} from './tag-consent';
import {decryptCredential} from './cryptography';
import {credentialKeys} from './vault';
import {tagWriteScope} from '../../shared/tag-gateway';
import {profileBudget,trafficFailure} from './traffic';
import {trafficOperation} from './traffic-route';
import { activeProfile } from "./vault";
import { tokenHash, tokenMatches } from "./cryptography";
import { validatedToken } from "./oauth";
import { resourceConfiguration } from "../../shared/resources";
import { bearer, workspaceJson, workspaceObject, WorkspaceError } from "../../shared/workspace-http";

export async function workspaceIdentity(request: Request, env: AuthEnv): Promise<Response> {
  if(env.WORKSPACE_ENABLED&&new URL(request.url).pathname==="/traffic")return trafficOperation(request,env);
  try {
    if (!Boolean(env.WORKSPACE_ENABLED) || request.method !== "POST") throw new WorkspaceError("unauthorized", 401);
    const path = new URL(request.url).pathname;
    if (path === "/management") {
      const token = bearer(request);
      const row = await env.HABITICA_INTEGRATION_DB.prepare("SELECT profile_id,management_token_hash FROM integration_profiles WHERE management_token_hash=?").bind(await tokenHash(token)).first<{ profile_id: string; management_token_hash: string }>();
      if (!row || !await tokenMatches(token, row.management_token_hash)) throw new WorkspaceError("unauthorized", 401);
      const profile = await activeProfile(env, row.profile_id);
      await profileBudget(env,profile.profile_id,"workspace");
      return Response.json({ profileId: profile.profile_id, connectionId: profile.connection_id }, { headers: { "cache-control": "no-store" } });
    }
    if(path==="/diagnostic-authority"){
      const body=workspaceObject(await workspaceJson(request),["profileId","connectionId"]);
      if(typeof body.profileId!=="string"||!/^prf_[A-Za-z0-9_-]{43}$/.test(body.profileId)||typeof body.connectionId!=="string")throw new WorkspaceError("unauthorized",401);
      const profile=await activeProfile(env,body.profileId,body.connectionId);
      return Response.json({profileId:profile.profile_id,connectionId:profile.connection_id,grantEpoch:profile.grant_epoch},{headers:{"cache-control":"no-store"}});
    }
    if(path==="/tag-authority"||path==="/task-recovery-authority") {
      const body=workspaceObject(await workspaceJson(request),["profileId","connectionId"]);
      if(typeof body.profileId!=="string"||!/^prf_[A-Za-z0-9_-]{43}$/.test(body.profileId)||typeof body.connectionId!=="string")throw new WorkspaceError("unauthorized",401);
      const profile=await activeProfile(env,body.profileId,body.connectionId);
      if(profile.credential_schema_version!==1)throw new WorkspaceError("credential_repair_required",401);
      const credential=await decryptCredential(profile.profile_id,{ciphertext:profile.credential_ciphertext!,iv:profile.credential_iv!,keyVersion:profile.credential_key_version!,schemaVersion:1},credentialKeys(env));
      if(path==="/task-recovery-authority")return Response.json({profileId:profile.profile_id,connectionId:profile.connection_id,accountId:credential.habiticaUserId,grantEpoch:profile.grant_epoch},{headers:{"cache-control":"no-store"}});
      let consentEpoch:string|null=null;try{consentEpoch=(await requireTagConsent(env,profile.profile_id,profile.connection_id)).consent_epoch;}catch(error){if(!(error instanceof WorkspaceError)||error.code!=="tag_consent_required")throw error;}
      return Response.json({profileId:profile.profile_id,connectionId:profile.connection_id,accountId:credential.habiticaUserId,tagConsentEnabled:consentEpoch!==null,tagConsentEpoch:consentEpoch,grantEpoch:profile.grant_epoch},{headers:{"cache-control":"no-store"}});
    }
    if (path === "/epoch") {
      const body = workspaceObject(await workspaceJson(request), ["profileId", "connectionId"]);
      if (typeof body.profileId !== "string" || !/^prf_[A-Za-z0-9_-]{43}$/.test(body.profileId) || typeof body.connectionId !== "string") throw new WorkspaceError("unauthorized", 401);
      const profile = await activeProfile(env, body.profileId, body.connectionId);
      return Response.json({ profileId: profile.profile_id, connectionId: profile.connection_id }, { headers: { "cache-control": "no-store" } });
    }
    if (path === "/mcp") {
      const token = request.headers.get("authorization")?.match(/^Bearer (\S+)$/)?.[1];
      const context = token && token.length <= 4096 ? await validatedToken(env, resourceConfiguration(env.ENVIRONMENT, env).resource, token) : null;
      if (!context) throw new WorkspaceError("unauthorized", 401);
      return Response.json({ profileId: context.props.profileId, connectionId: context.props.connectionId, scopes: context.scope, tagWriteAllowed:context.scope.includes(tagWriteScope), grantEpoch:context.props.grantEpoch }, { headers: { "cache-control": "no-store" } });
    }
    throw new WorkspaceError("unauthorized", 401);
  } catch(error) { return trafficFailure(error)??Response.json({ error: "unauthorized" }, { status: 401, headers: { "cache-control": "no-store" } }); }
}
