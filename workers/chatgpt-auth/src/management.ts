import {managedProfile,boundedJson,VaultError} from './vault';
import {authorizationServer,scopes} from './oauth';
import {newManagementToken,tokenHash} from './cryptography';
import {workspaceObject,WorkspaceError} from '../../shared/workspace-http';
const headers={'cache-control':'no-store','x-content-type-options':'nosniff'};
export async function managementRoute(request:Request,env:AuthEnv):Promise<Response|null>{
 const route=new URL(request.url).pathname.match(/^\/profiles\/(prf_[A-Za-z0-9_-]{43})\/(status|revoke-grants|rotate-management)$/);
 if(!route)return null;
 try{
  const profile=await managedProfile(request,env,route[1]),operation=route[2];
  if(operation==='status'){
   if(request.method!=='GET')throw new VaultError('invalid_request',405);
   const permissions=new Set<string>();let cursor:string|undefined;let truncated=false;let grantCount=0;
   if(env.OAUTH_ENABLED){
    const oauth=authorizationServer(env).getOAuthApi(env);
    for(let i=0;i<5;i++){
     const page=await oauth.listUserGrants(profile.profile_id,{limit:20,cursor});
     for(const g of page.items)if(g.metadata?.connectionId===profile.connection_id&&g.metadata?.grantEpoch===profile.grant_epoch&&(!g.expiresAt||g.expiresAt>Date.now()/1000)){
      grantCount++;for(const scope of g.scope)if(scopes.includes(scope))permissions.add(scope);
     }
     cursor=page.cursor;if(!cursor)break;truncated=true;
    }
    truncated=Boolean(cursor);
   }
   const state=await env.HABITICA_INTEGRATION_DB.prepare('SELECT credential_repair_required FROM integration_read_state WHERE profile_id=? AND connection_id=?').bind(profile.profile_id,profile.connection_id).first<{credential_repair_required:number}>();
   const final=await managedProfile(request,env,profile.profile_id);if(final.grant_epoch!==profile.grant_epoch||final.connection_id!==profile.connection_id)throw new VaultError('management_changed',409);
   return Response.json({displayName:profile.display_name_cache??'Habitica account',permissions:[...permissions].sort(),grantCount,permissionsTruncated:truncated,credentialStatus:state?.credential_repair_required?'repair_needed':'last_verified',lastVerifiedAtUtc:profile.last_verified_at_utc,lastUsedAtUtc:profile.last_used_at_utc,usageMeaning:'Last verified ChatGPT tool authorization; background Workspace execution is separate.'},{headers});
  }
  if(request.method!=='POST')throw new VaultError('invalid_request',405);
  const input=workspaceObject(await boundedJson(request,1024),['confirm']);
  if(input.confirm!==(operation==='revoke-grants'?'revoke-chatgpt-grants':'rotate-management-token'))throw new VaultError('confirmation_required',409);
  if(operation==='rotate-management'){
   const token=newManagementToken();
   const changed=await env.HABITICA_INTEGRATION_DB.prepare('UPDATE integration_profiles SET management_token_hash=?,management_token_rotated_at_utc=?,generation=generation+1 WHERE profile_id=? AND management_token_hash=? AND generation=? AND revoked_at_utc IS NULL').bind(await tokenHash(token),new Date().toISOString(),profile.profile_id,profile.management_token_hash,profile.generation).run();
   if(!changed.meta.changes)throw new VaultError('management_changed',409);
   return Response.json({rotated:true,managementToken:token},{headers});
  }
  const epoch=String(profile.generation+1);
  const result=await env.HABITICA_INTEGRATION_DB.batch([
   env.HABITICA_INTEGRATION_DB.prepare('UPDATE integration_profiles SET grant_epoch=?,generation=generation+1 WHERE profile_id=? AND management_token_hash=? AND generation=? AND revoked_at_utc IS NULL').bind(epoch,profile.profile_id,profile.management_token_hash,profile.generation),
   env.HABITICA_INTEGRATION_DB.prepare('DELETE FROM linking_tickets WHERE profile_id=? AND EXISTS(SELECT 1 FROM integration_profiles WHERE profile_id=? AND grant_epoch=?)').bind(profile.profile_id,profile.profile_id,epoch),
   env.HABITICA_INTEGRATION_DB.prepare('DELETE FROM consent_links WHERE profile_id=? AND EXISTS(SELECT 1 FROM integration_profiles WHERE profile_id=? AND grant_epoch=?)').bind(profile.profile_id,profile.profile_id,epoch),
  ]);
  if(!result[0].meta.changes)throw new VaultError('management_changed',409);
  // The D1 epoch is the immediate barrier even if bounded provider cleanup is unavailable.
  let pendingCleanup=false;
  if(env.OAUTH_ENABLED)try{
   const oauth=authorizationServer(env).getOAuthApi(env);let cursor:string|undefined;
   for(let page=0;page<5;page++){
    const grants=await oauth.listUserGrants(profile.profile_id,{limit:20,cursor});
    for(const grant of grants.items)if(grant.metadata?.grantEpoch!==epoch)await oauth.revokeGrant(grant.id,profile.profile_id);
    cursor=grants.cursor;if(!cursor)break;
   }
   pendingCleanup=Boolean(cursor);
  }catch{pendingCleanup=true;}
  return Response.json({revoked:true,pendingCleanup,credentialsRetained:true,workspaceAccessRetained:true},{headers});
 }catch(error){const safe=error instanceof VaultError||error instanceof WorkspaceError?error:new VaultError('management_unavailable',503);return Response.json({error:safe.code},{status:safe.status,headers});}
}
