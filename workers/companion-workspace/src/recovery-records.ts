import {recoveryRecordsInput,queryRecoveryRecords} from '../../shared/recovery-records.ts';
import {journal} from './journal.ts';
import {cloudAuthority,type WorkspaceRow} from './executor.ts';
import {WorkspaceError} from '../../shared/workspace-http.ts';
export async function recoveryRecords(env:WorkspaceEnv,row:WorkspaceRow,input:unknown){
 recoveryRecordsInput(input);
 try{
  const results=await env.WORKSPACE_DB.batch<Record<string,unknown>>([
   env.WORKSPACE_DB.prepare('SELECT document_json,store_version,authority_json,linked_recovery_fence,retirement_fence FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1').bind(row.profile_id,row.connection_id,row.access_epoch),
   env.WORKSPACE_DB.prepare('SELECT entry_json FROM workspace_commands WHERE profile_id=? ORDER BY sequence').bind(row.profile_id),
   env.WORKSPACE_DB.prepare('SELECT event_json FROM workspace_events WHERE profile_id=? ORDER BY sequence').bind(row.profile_id)]);
  const current=results[0].results[0];if(!current)throw new WorkspaceError('recovery_history_unavailable',503);
  const state=await journal({document:JSON.parse(String(current.document_json)),storeVersion:Number(current.store_version),authority:current.authority_json?JSON.parse(String(current.authority_json)):cloudAuthority(row.workspace_id),commands:results[1].results.map(value=>JSON.parse(String(value.entry_json))),events:results[2].results.map(value=>JSON.parse(String(value.event_json)))});
  return queryRecoveryRecords(state,input,Boolean(current.linked_recovery_fence||current.retirement_fence));
 }catch(error){if(error instanceof WorkspaceError&&error.code==='stale_revision')throw error;throw new WorkspaceError('recovery_history_unavailable',503);}
}
