import {reconcileTags,tagStatements,tagWorkBytes,previewTagAuthority,tagPreview,browserTagOrigin} from './tag-reconciliation';
import type {TagOrigin} from './tag-authority';
import {organizationToolInput,type OrganizationWriteTool} from '../../shared/organization-tools';
import {organizationCommand,type OrganizationEntry} from '../../shared/workspace-organization';
import {workspaceTokenHash,WorkspaceError,uuid} from '../../shared/workspace-http';
import {journal,type Journal} from './journal';
import {acceptCloudOrganization} from './organization-commands';
import {cloudAuthority,type WorkspaceRow} from './executor';
interface Preparation {grant_epoch:string;access_epoch:string;request_id:string;command_id:string;input_hash:string;command_json:string;correlation_id:string;expires_at_utc:string;tag_authority_json:string|null}
const bytes=(value:string)=>new TextEncoder().encode(value).length;
function stable(value:unknown):string {if(value===null||typeof value!=='object')return JSON.stringify(value);if(Array.isArray(value))return '['+value.map(stable).join(',')+']';return '{'+Object.keys(value).sort().map(key=>JSON.stringify(key)+':'+stable((value as Record<string,unknown>)[key])).join(',')+'}';}
async function load(env:WorkspaceEnv,row:WorkspaceRow):Promise<{state:Journal;row:WorkspaceRow}>{
 const data=await env.WORKSPACE_DB.batch([
  env.WORKSPACE_DB.prepare('SELECT * FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1').bind(row.profile_id,row.connection_id,row.access_epoch),
  env.WORKSPACE_DB.prepare('SELECT entry_json FROM workspace_commands WHERE profile_id=? ORDER BY sequence').bind(row.profile_id),
  env.WORKSPACE_DB.prepare('SELECT event_json FROM workspace_events WHERE profile_id=? ORDER BY sequence').bind(row.profile_id)]);
 const current=data[0].results[0] as WorkspaceRow|undefined;if(!current)throw new WorkspaceError('unauthorized',401);
 if((current.mcp_grant_epoch??'legacy')!==(row.mcp_grant_epoch??'legacy'))throw new WorkspaceError('preview_connection_changed',409);
 const state=await journal({document:JSON.parse(current.document_json),storeVersion:current.store_version,authority:current.authority_json?JSON.parse(current.authority_json):cloudAuthority(current.workspace_id),commands:data[1].results.map(value=>JSON.parse(String((value as {entry_json:string}).entry_json))),events:data[2].results.map(value=>JSON.parse(String((value as {event_json:string}).event_json)))});if(state.document.id!==current.workspace_id)throw new WorkspaceError('workspace_mismatch',409);return {row:current,state};
}
function receipt(entry:OrganizationEntry,correlationId:string,workspaceId:string,refreshSucceeded=true,tagEffects=false){return {commandId:entry.command.id,correlationId,workspaceId,kind:entry.command.kind,operation:entry.command.operation,entityId:entry.command.entityId,entry,savedInCompanion:true,mayAffectHabitica:tagEffects,acceptedEventCount:0,confirmedEffectCount:0,refreshSucceeded,outcomeUnknown:false};}
export async function organizationApi(env:WorkspaceEnv,initialRow:WorkspaceRow,name:OrganizationWriteTool,args:unknown,tagOrigin:TagOrigin=browserTagOrigin):Promise<Record<string,unknown>>{
 const input=organizationToolInput(name,args);let {state,row}=await load(env,initialRow);
 async function preparation(column:'request_id'|'command_id',id:string){return env.WORKSPACE_DB.prepare('SELECT * FROM workspace_preparations WHERE profile_id=? AND '+column+'=?').bind(row.profile_id,id).first<Preparation>();}
 async function original(id:string){const entry=state.document.organizationHistory?.find(entry=>entry.command.id===id);if(!entry)return null;const p=await preparation('command_id',id);const count=await env.WORKSPACE_DB.prepare("SELECT COUNT(*) AS n FROM workspace_tag_work WHERE profile_id=? AND command_id=? AND state<>'needsReview'").bind(row.profile_id,id).first<{n:number}>();return receipt(entry,p?.correlation_id??id,row.workspace_id,true,Number(count?.n??0)>0);}
 if(name==='get_organization_command')return await original(uuid(input.commandId))??{savedInCompanion:null,commandId:input.commandId,outcomeUnknown:true};
 if(name==='prepare_organization_command'){
  const requestId=uuid(input.requestId),inputHash=await workspaceTokenHash('organization-v1:'+stable(input));let p=await preparation('request_id',requestId);
  if(p&&p.input_hash!==inputHash)throw new WorkspaceError('request_id_reused',409);
  if(!p){
   const command=organizationCommand({id:crypto.randomUUID(),workspaceId:row.workspace_id,expectedRevision:input.expectedRevision??state.document.revision,kind:input.kind,operation:input.operation,entityId:input.operation==='create'?crypto.randomUUID():input.entityId,patch:input.patch});
   const preview=await acceptCloudOrganization(state,command,new Date().toISOString());if(preview.disposition!=='accepted')throw new WorkspaceError(preview.errorCode??'invalid_command',409);
   const serialized=JSON.stringify(command),size=row.archive_bytes+bytes(serialized)+512;if(size>8*1024*1024-32768||row.store_version>=Number.MAX_SAFE_INTEGER)throw new WorkspaceError('workspace_capacity',413);
   const tagAuthorityJson=await previewTagAuthority(env,row,tagOrigin);
   const tag=crypto.randomUUID(),now=new Date().toISOString(),expires=new Date(Date.now()+600000).toISOString();
   const changed=await env.WORKSPACE_DB.batch([
    env.WORKSPACE_DB.prepare('UPDATE workspaces SET archive_bytes=?,commit_tag=?,store_version=store_version+1 WHERE profile_id=? AND connection_id=? AND access_epoch=? AND store_version=? AND commit_tag=? AND active=1').bind(size,tag,row.profile_id,row.connection_id,row.access_epoch,row.store_version,row.commit_tag),
    env.WORKSPACE_DB.prepare('INSERT INTO workspace_preparations(profile_id,access_epoch,request_id,command_id,input_hash,command_json,correlation_id,created_at_utc,expires_at_utc,grant_epoch,tag_authority_json) SELECT profile_id,access_epoch,?,?,?,?,?,?,?,?,? FROM workspaces WHERE profile_id=? AND commit_tag=?').bind(requestId,command.id,inputHash,serialized,crypto.randomUUID(),now,expires,row.mcp_grant_epoch??'legacy',tagAuthorityJson,row.profile_id,tag)]);
   p=await preparation('request_id',requestId);if(!p||p.input_hash!==inputHash)throw new WorkspaceError(changed[0].meta.changes?'request_id_reused':'workspace_changed',409);
  }
  const command=organizationCommand(JSON.parse(p.command_json)),found=await original(p.command_id);
  if(!found&&(p.access_epoch!==row.access_epoch||p.grant_epoch!==(row.mcp_grant_epoch??'legacy')))throw new WorkspaceError('preview_connection_changed',409);
  const previewed=found?null:await acceptCloudOrganization(state,command,new Date().toISOString());
  const proposed=found?.entry??previewed?.entry;if(!proposed)throw new WorkspaceError('stale_revision',409);
  // An unchanged original expired preview remains recoverable; expiry never authorizes acceptance.
  return {preparedCommandId:p.command_id,correlationId:p.correlation_id,expiresAtUtc:p.expires_at_utc,expired:p.expires_at_utc<=new Date().toISOString()&&!found,command,snapshot:proposed?.snapshot??null,tagProjection:tagPreview(state.document,previewed?.snapshot?.document??state.document,p.tag_authority_json),mayAffectHabitica:found?.mayAffectHabitica??Boolean(p.tag_authority_json&&JSON.parse(p.tag_authority_json).scopeAllowed&&JSON.parse(p.tag_authority_json).consentEnabled),savedInCompanion:Boolean(found),...(found?{receipt:found}:{})};
 }
 const commandId=uuid(input.commandId),found=await original(commandId);if(found)return found;
 const p=await preparation('command_id',commandId);if(!p||p.access_epoch!==row.access_epoch||p.grant_epoch!==(row.mcp_grant_epoch??'legacy')||p.expires_at_utc<=new Date().toISOString())throw new WorkspaceError('prepare_required_or_expired',409);
 const command=organizationCommand(JSON.parse(p.command_json)),accepted=await acceptCloudOrganization(state,command,new Date().toISOString());if(accepted.disposition!=='accepted')throw new WorkspaceError(accepted.errorCode??'command_unavailable',409);
 const tags=await reconcileTags(env,row,state.document,accepted.snapshot!.document,commandId,tagOrigin,p.tag_authority_json?JSON.parse(p.tag_authority_json):null);
 const serialized=JSON.stringify(tags.document),size=row.archive_bytes-bytes(row.document_json)+bytes(serialized)+tagWorkBytes(tags);if(bytes(serialized)>1024*1024||size>8*1024*1024)throw new WorkspaceError('workspace_capacity',413);
 const commitTag=crypto.randomUUID();
 const changed=await env.WORKSPACE_DB.batch([env.WORKSPACE_DB.prepare(`UPDATE workspaces SET document_json=?,store_version=store_version+1,archive_bytes=?,commit_tag=?,updated_at_utc=? WHERE profile_id=? AND connection_id=? AND access_epoch=? AND store_version=? AND active=1
 AND EXISTS(SELECT 1 FROM workspace_preparations p WHERE p.profile_id=workspaces.profile_id AND p.command_id=? AND p.access_epoch=workspaces.access_epoch AND p.grant_epoch=? AND p.expires_at_utc>strftime('%Y-%m-%dT%H:%M:%fZ','now'))`).bind(serialized,size,commitTag,accepted.entry!.acceptedAtUtc,row.profile_id,row.connection_id,row.access_epoch,row.store_version,commandId,row.mcp_grant_epoch??'legacy'),...tagStatements(env,row,tags,commitTag)]);
 if(!changed[0].meta.changes){({state,row}=await load(env,row));const retry=await original(commandId);if(retry)return retry;throw new WorkspaceError('stale_revision',409);}
 return receipt(accepted.entry!,p.correlation_id,row.workspace_id,true,tags.work.some(work=>work.state==='pending'));
}
