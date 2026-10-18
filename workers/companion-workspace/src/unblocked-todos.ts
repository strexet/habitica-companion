import {unblockedTodoFilter,queryUnblockedTodos} from '../../shared/unblocked-todos.ts';
import {WorkspaceError} from '../../shared/workspace-http.ts';
import type {WorkspaceRow} from './executor.ts';

export async function unblockedTodos(env:WorkspaceEnv,row:WorkspaceRow,input:unknown){
 unblockedTodoFilter(input);
 let source:{document_json:string}|null;
 try{source=await env.WORKSPACE_DB.prepare('SELECT document_json FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1').bind(row.profile_id,row.connection_id,row.access_epoch).first<{document_json:string}>();}
 catch{throw new WorkspaceError('candidates_unavailable',503);}
 if(!source)throw new WorkspaceError('unauthorized',401);
 try{return queryUnblockedTodos(JSON.parse(source.document_json),input);}
 catch(error){if(error instanceof WorkspaceError&&error.code==='stale_revision')throw error;throw new WorkspaceError('candidates_unavailable',503);}
}
