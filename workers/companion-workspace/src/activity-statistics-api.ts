import {cloudAuthority,type WorkspaceRow} from './executor.ts';
import {readActivityStatistics} from './activity-streaks.ts';
import {WorkspaceError} from '../../shared/workspace-http.ts';
export async function activityStatistics(env:WorkspaceEnv,row:WorkspaceRow,input:unknown){
 const result=await env.WORKSPACE_DB.batch([
  env.WORKSPACE_DB.prepare('SELECT document_json,store_version,authority_json FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1').bind(row.profile_id,row.connection_id,row.access_epoch),
  env.WORKSPACE_DB.prepare('SELECT c.entry_json FROM workspace_commands c JOIN workspaces w ON w.profile_id=c.profile_id WHERE w.profile_id=? AND w.connection_id=? AND w.access_epoch=? AND w.active=1 ORDER BY c.sequence').bind(row.profile_id,row.connection_id,row.access_epoch),
  env.WORKSPACE_DB.prepare('SELECT e.event_json FROM workspace_events e JOIN workspaces w ON w.profile_id=e.profile_id WHERE w.profile_id=? AND w.connection_id=? AND w.access_epoch=? AND w.active=1 ORDER BY e.sequence').bind(row.profile_id,row.connection_id,row.access_epoch)
 ]);
 const current=result[0].results[0] as {document_json:string;store_version:number;authority_json:string|null}|undefined;if(!current)throw new WorkspaceError('unauthorized',401);
 const snapshot={document:JSON.parse(current.document_json),storeVersion:current.store_version,authority:current.authority_json?JSON.parse(current.authority_json):cloudAuthority(row.workspace_id),commands:result[1].results.map(v=>JSON.parse((v as {entry_json:string}).entry_json)),events:result[2].results.map(v=>JSON.parse((v as {event_json:string}).event_json))};
 if(new TextEncoder().encode(JSON.stringify(snapshot)).length>16*1024*1024)throw new WorkspaceError('activity_capacity',413);
 return readActivityStatistics(snapshot,input);
}
