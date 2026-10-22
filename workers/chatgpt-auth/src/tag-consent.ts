import {managedProfile,activeProfile,boundedJson,VaultError} from './vault';
import {trafficFailure} from './traffic';
import {workspaceObject,WorkspaceError} from '../../shared/workspace-http';
import {tagConsentVersion} from '../../shared/tag-gateway';
const headers={'cache-control':'no-store','x-content-type-options':'nosniff'};
export interface TagConsent {consent_epoch:string;consent_version:string}
export async function requireTagConsent(env:AuthEnv,profileId:string,connectionId:string):Promise<TagConsent>{
 const row=await env.HABITICA_INTEGRATION_DB.prepare('SELECT consent_epoch,consent_version FROM integration_tag_consents WHERE profile_id=? AND connection_id=? AND revoked_at_utc IS NULL').bind(profileId,connectionId).first<TagConsent>();
 if(!row||row.consent_version!==tagConsentVersion)throw new WorkspaceError('tag_consent_required',403);return row;
}
export async function tagConsentRoute(request:Request,env:AuthEnv):Promise<Response|null>{
 const match=new URL(request.url).pathname.match(/^\/profiles\/(prf_[A-Za-z0-9_-]{43})\/tag-consent$/);if(!match)return null;
 try{
  if(!env.WORKSPACE_ENABLED||!env.PROJECTION_ENABLED)throw new WorkspaceError('service_unavailable',503);
  const profile=await managedProfile(request,env,match[1]);await activeProfile(env,profile.profile_id,profile.connection_id);
  if(request.method==='GET'){
   const row=await env.HABITICA_INTEGRATION_DB.prepare('SELECT consent_version,granted_at_utc,revoked_at_utc FROM integration_tag_consents WHERE profile_id=? AND connection_id=?').bind(profile.profile_id,profile.connection_id).first<{consent_version:string;granted_at_utc:string;revoked_at_utc:string|null}>();
   return Response.json({enabled:row?.consent_version===tagConsentVersion&&!row.revoked_at_utc,consentVersion:tagConsentVersion,grantedAtUtc:row?.granted_at_utc??null},{headers});
  }
  if(request.method!=='POST')throw new WorkspaceError('invalid_request',405);
  const body=workspaceObject(await boundedJson(request,1024),['consentVersion','enabled']);if(body.consentVersion!==tagConsentVersion||typeof body.enabled!=='boolean')throw new WorkspaceError('tag_consent_required',403);
  const now=new Date().toISOString(),epoch=crypto.randomUUID();
  const result=await env.HABITICA_INTEGRATION_DB.batch([
   env.HABITICA_INTEGRATION_DB.prepare(`INSERT INTO integration_tag_consents(profile_id,connection_id,consent_version,consent_epoch,granted_at_utc,revoked_at_utc)
    SELECT profile_id,connection_id,?,?,?,? FROM integration_profiles WHERE profile_id=? AND connection_id=? AND management_token_hash=? AND revoked_at_utc IS NULL
    ON CONFLICT(profile_id,connection_id) DO UPDATE SET consent_version=excluded.consent_version,consent_epoch=excluded.consent_epoch,granted_at_utc=excluded.granted_at_utc,revoked_at_utc=excluded.revoked_at_utc`).bind(tagConsentVersion,epoch,now,body.enabled?null:now,profile.profile_id,profile.connection_id,profile.management_token_hash),
   env.HABITICA_INTEGRATION_DB.prepare('DELETE FROM integration_tag_checks WHERE profile_id=? AND connection_id=?').bind(profile.profile_id,profile.connection_id),
  ]);
  if(!result[0].meta.changes)throw new WorkspaceError('connection_changed',409);
  return Response.json({enabled:body.enabled,consentVersion:tagConsentVersion,remoteTagsRetained:true,unknownAttemptsRetained:true},{headers});
 }catch(error){return trafficFailure(error)??Response.json({error:error instanceof WorkspaceError||error instanceof VaultError?error.code:'tag_consent_unavailable'},{status:error instanceof WorkspaceError||error instanceof VaultError?error.status:503,headers});}
}
export async function clearTagContext(env:AuthEnv,profileId:string,_connectionId:string):Promise<void>{
 await env.HABITICA_INTEGRATION_DB.batch(['integration_tag_consents','integration_tag_checks','integration_owned_tags','integration_tag_attempt_details'].map(table=>env.HABITICA_INTEGRATION_DB.prepare(`DELETE FROM ${table} WHERE profile_id=?`).bind(profileId)));
}
