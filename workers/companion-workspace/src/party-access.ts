import {WorkspaceError} from '../../shared/workspace-http.ts';
export async function requireCurrentPartyAccess(env:Pick<WorkspaceEnv,'WORKSPACE_DB'>,who:{profileId:string;connectionId:string;accessEpoch:string}){
 const current=await env.WORKSPACE_DB.prepare('SELECT 1 FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1').bind(who.profileId,who.connectionId,who.accessEpoch).first();
 if(!current)throw new WorkspaceError('unauthorized',401);
}
