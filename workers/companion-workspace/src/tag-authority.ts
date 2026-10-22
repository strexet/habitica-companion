import {uuid,WorkspaceError,workspaceObject} from '../../shared/workspace-http.ts';
import type {WorkspaceRow} from './executor.ts';
export interface TagOrigin {source:'browser'|'mcp';tagWriteAllowed:boolean;grantEpoch?:string}
export interface TagAuthority {accountId:string;connectionId:string;scopeAllowed:boolean;consentEnabled:boolean;consentEpoch:string|null;source:'browser'|'mcp';grantEpoch:string|null}
// Origin is constructed only by authenticated server routes, never from request JSON or portable history.
export async function tagAuthority(env:Pick<WorkspaceEnv,'AUTH_IDENTITY'>,row:WorkspaceRow,origin:TagOrigin):Promise<TagAuthority>{
 const response=await env.AUTH_IDENTITY.fetch('https://identity.internal/tag-authority',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({profileId:row.profile_id,connectionId:row.connection_id}),signal:AbortSignal.timeout(5000)});
 if(!response.ok)throw new WorkspaceError('tag_authority_unavailable',503);
 const value=workspaceObject(await response.json());
 if(value.profileId!==row.profile_id||value.connectionId!==row.connection_id||typeof value.tagConsentEnabled!=='boolean'||value.tagConsentEpoch!==null&&typeof value.tagConsentEpoch!=='string')throw new WorkspaceError('tag_authority_unavailable',503);
 return {accountId:uuid(value.accountId),connectionId:row.connection_id,scopeAllowed:origin.source==='browser'||origin.tagWriteAllowed&&value.grantEpoch===origin.grantEpoch,consentEnabled:value.tagConsentEnabled,consentEpoch:value.tagConsentEpoch as string|null,source:origin.source,grantEpoch:origin.grantEpoch??null};
}
export function tagAuthorityGap(authority:TagAuthority):string|null{return !authority.scopeAllowed?'tag_scope_required':!authority.consentEnabled?'tag_consent_required':null;}
