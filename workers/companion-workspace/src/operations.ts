import {WorkspaceError} from '../../shared/workspace-http';
import type {WorkspaceRow} from './executor';
export async function operationalStatus(env:WorkspaceEnv,row:WorkspaceRow){
 const now=new Date().toISOString(),stalledBefore=new Date(Date.now()-15*60000).toISOString();
 const results=await env.WORKSPACE_DB.batch([
  env.WORKSPACE_DB.prepare(`SELECT COUNT(CASE WHEN json_extract(entry_json,'$.projectionState') IN ('pending','leased','executing') THEN 1 END) AS pending,
   COUNT(CASE WHEN json_extract(entry_json,'$.projectionState') IN ('pending','leased','executing') AND julianday(json_extract(entry_json,'$.acceptedAtUtc'))<julianday(?) THEN 1 END) AS stalled,
   COUNT(CASE WHEN json_extract(entry_json,'$.projectionState')='unknownOutcome' THEN 1 END) AS uncertain,
   COUNT(CASE WHEN json_extract(entry_json,'$.projectionState')='blocked' THEN 1 END) AS blocked,
   COUNT(CASE WHEN julianday(json_extract(entry_json,'$.lease.expiresAtUtc'))<=julianday(?) THEN 1 END) AS expiredLeases,
   MIN(CASE WHEN json_extract(entry_json,'$.projectionState') IN ('pending','leased','executing') THEN json_extract(entry_json,'$.acceptedAtUtc') END) AS oldestPendingAtUtc
   FROM workspace_commands WHERE profile_id=?`).bind(stalledBefore,now,row.profile_id),
  ...['prepared_spell_actions','prepared_cron_actions'].map(table=>env.WORKSPACE_DB.prepare(`SELECT COUNT(CASE WHEN status IN ('accepted','dispatching') THEN 1 END) AS pending,COUNT(CASE WHEN status IN ('accepted','dispatching') AND created_at_utc<? THEN 1 END) AS stalled,COUNT(CASE WHEN status='unknownOutcome' THEN 1 END) AS uncertain FROM ${table} WHERE profile_id=?`).bind(stalledBefore,row.profile_id)),
  env.WORKSPACE_DB.prepare('SELECT active FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1').bind(row.profile_id,row.connection_id,row.access_epoch)
 ]);
 if(!results[3].results.length)throw new WorkspaceError('unauthorized',401);
 const counts=results.slice(0,3).map(r=>r.results[0] as Record<string,unknown>);const sum=(key:string)=>counts.reduce((n,r)=>n+Number(r[key]??0),0);
 return {checkedAtUtc:now,pending:sum('pending'),stalled:sum('stalled'),uncertain:sum('uncertain'),blocked:Number(counts[0].blocked??0),expiredLeases:Number(counts[0].expiredLeases??0),oldestPendingAtUtc:counts[0].oldestPendingAtUtc??null};
}
