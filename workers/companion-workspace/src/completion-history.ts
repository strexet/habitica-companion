import {historyFilter,queryCompletionHistory,type HistoryCommand} from '../../shared/completion-history.ts';
import {workspaceDocument,type CompanionTask} from '../../shared/workspace-domain.ts';
import {WorkspaceError} from '../../shared/workspace-http.ts';
import type {WorkspaceRow} from './executor.ts';

export async function completionHistory(env:WorkspaceEnv,row:WorkspaceRow,input:unknown){
 historyFilter(input);
 let results;
 try{
  results=await env.WORKSPACE_DB.batch<Record<string,unknown>>([
   env.WORKSPACE_DB.prepare('SELECT document_json FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1').bind(row.profile_id,row.connection_id,row.access_epoch),
   env.WORKSPACE_DB.prepare(`SELECT c.command_id,json_extract(c.entry_json,'$.command.taskId') AS task_id,
    json_extract(c.entry_json,'$.command.operation') AS operation,json_extract(c.entry_json,'$.taskSnapshot') AS snapshot_json
    FROM workspace_commands c JOIN workspaces w ON w.profile_id=c.profile_id
    WHERE w.profile_id=? AND w.connection_id=? AND w.access_epoch=? AND w.active=1
    AND c.command_id IN (SELECT json_extract(value,'$.commandId') FROM json_each(w.document_json,'$.completions'))`).bind(row.profile_id,row.connection_id,row.access_epoch)
  ]);
 }catch{throw new WorkspaceError('history_unavailable',503);}
 const source=results[0].results[0] as {document_json:string}|undefined;
 if(!source)throw new WorkspaceError('unauthorized',401);
 try{
  const document=workspaceDocument(JSON.parse(source.document_json));
  const commands:HistoryCommand[]=results[1].results.map(value=>{
   const snapshot=value.snapshot_json===null?null:JSON.parse(String(value.snapshot_json));
   if(snapshot!==null&&(typeof snapshot!=='object'||Array.isArray(snapshot)))throw new Error('invalid_snapshot');
   return {commandId:String(value.command_id),taskId:String(value.task_id),operation:String(value.operation),snapshot:snapshot as CompanionTask|null};
  });
  return queryCompletionHistory(document,commands,input);
 }catch(error){if(error instanceof WorkspaceError&&['stale_revision','history_unavailable'].includes(error.code))throw error;throw new WorkspaceError('history_unavailable',503);}
}
