import {savedTaskReadInput,readSavedTaskViews,type SavedTaskReadTool} from '../../shared/saved-task-read.ts';
import {WorkspaceError,uuid} from '../../shared/workspace-http.ts';import type {WorkspaceRow} from './executor.ts';
export async function savedTaskReads(env:WorkspaceEnv,row:WorkspaceRow,name:SavedTaskReadTool,input:unknown){
 savedTaskReadInput(name,input);let current:{document_json:string;workspace_id:string}|null;
 try{current=await env.WORKSPACE_DB.prepare('SELECT document_json,workspace_id FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1').bind(row.profile_id,row.connection_id,row.access_epoch).first<{document_json:string;workspace_id:string}>();}
 catch{throw new WorkspaceError('saved_views_unavailable',503);}
 if(!current)throw new WorkspaceError('unauthorized',401);
 try{const source=JSON.parse(current.document_json);if(uuid(source.id)!==current.workspace_id)throw new Error('workspace_mismatch');return readSavedTaskViews(source,name,input);}
 catch(error){if(error instanceof WorkspaceError&&['stale_revision','saved_view_not_found'].includes(error.code))throw error;throw new WorkspaceError('saved_views_unavailable',503);}
}
