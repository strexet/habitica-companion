import {WorkspaceError} from '../../shared/workspace-http.ts';
import type {WorkspaceRow} from './executor';
export async function mcpEpoch(env:WorkspaceEnv,row:WorkspaceRow,epoch:unknown):Promise<WorkspaceRow>{
 if(typeof epoch!=='string'||!epoch||epoch.length>64)throw new WorkspaceError('unauthorized',401);
 const source=await env.WORKSPACE_DB.prepare('SELECT * FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1').bind(row.profile_id,row.connection_id,row.access_epoch).first<WorkspaceRow&{mcp_grant_epoch:string|null}>();
 if(!source)throw new WorkspaceError('unauthorized',401);
 if(source.mcp_grant_epoch===epoch)return source;
 const number=(value:string|null)=>value===null||value==='legacy'?0:/^[1-9][0-9]*$/.test(value)&&Number.isSafeInteger(Number(value))?Number(value):-1;
 if(number(epoch)<0||number(epoch)<number(source.mcp_grant_epoch))throw new WorkspaceError('unauthorized',401);
 const tag=crypto.randomUUID();
 const result=await env.WORKSPACE_DB.batch([
  env.WORKSPACE_DB.prepare(`UPDATE workspaces SET mcp_grant_epoch=?,commit_tag=?,store_version=store_version+1,archive_bytes=archive_bytes-COALESCE((SELECT SUM(length(CAST(p.command_json AS BLOB))+512) FROM workspace_preparations p WHERE p.profile_id=workspaces.profile_id AND NOT EXISTS(SELECT 1 FROM workspace_commands c WHERE c.profile_id=p.profile_id AND c.command_id=p.command_id) AND NOT EXISTS(SELECT 1 FROM workspaces retained_w, json_each(retained_w.document_json,'$.organizationHistory') h WHERE retained_w.profile_id=p.profile_id AND json_extract(h.value,'$.command.id')=p.command_id)),0) WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1 AND mcp_grant_epoch IS ? AND store_version<9007199254740991`).bind(epoch,tag,row.profile_id,row.connection_id,row.access_epoch,source.mcp_grant_epoch),
  env.WORKSPACE_DB.prepare(`DELETE FROM workspace_preparations WHERE profile_id=? AND NOT EXISTS(SELECT 1 FROM workspace_commands c WHERE c.profile_id=workspace_preparations.profile_id AND c.command_id=workspace_preparations.command_id) AND NOT EXISTS(SELECT 1 FROM workspaces retained_w, json_each(retained_w.document_json,'$.organizationHistory') h WHERE retained_w.profile_id=workspace_preparations.profile_id AND json_extract(h.value,'$.command.id')=workspace_preparations.command_id) AND EXISTS(SELECT 1 FROM workspaces WHERE profile_id=? AND commit_tag=?)`).bind(row.profile_id,row.profile_id,tag),
  env.WORKSPACE_DB.prepare("DELETE FROM prepared_spell_actions WHERE profile_id=? AND status='prepared' AND EXISTS(SELECT 1 FROM workspaces WHERE profile_id=? AND commit_tag=?)").bind(row.profile_id,row.profile_id,tag),
  env.WORKSPACE_DB.prepare("DELETE FROM prepared_cron_actions WHERE profile_id=? AND status='prepared' AND EXISTS(SELECT 1 FROM workspaces WHERE profile_id=? AND commit_tag=?)").bind(row.profile_id,row.profile_id,tag),
 ]);
 if(!result[0].meta.changes)throw new WorkspaceError('workspace_changed',409);
 const fresh=await env.WORKSPACE_DB.prepare('SELECT * FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1 AND mcp_grant_epoch=?').bind(row.profile_id,row.connection_id,row.access_epoch,epoch).first<WorkspaceRow>();
 if(!fresh)throw new WorkspaceError('unauthorized',401);return fresh;
}
