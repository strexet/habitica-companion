import { WorkspaceError } from '../../shared/workspace-http.ts';
import type { WorkspaceRow } from './executor';

const states=['notRequested','pending','leased','executing','confirmed','blocked','unknownOutcome','cancelled'] as const;
export async function taskSyncStatus(env:WorkspaceEnv,row:WorkspaceRow){
 let result:Record<string,unknown>|null;
 try{
  result=await env.WORKSPACE_DB.prepare(`SELECT w.workspace_id,w.store_version,w.executor_enabled,
   json_extract(w.document_json,'$.revision') AS revision,COUNT(c.command_id) AS acceptedCommandCount,
   ${states.map(state=>`COUNT(CASE WHEN json_extract(c.entry_json,'$.projectionState')='${state}' THEN 1 END) AS ${state}`).join(',')},
   COALESCE(SUM(json_array_length(c.entry_json,'$.effects')),0) AS requestedEffectCount,
   COALESCE(SUM(json_extract(c.entry_json,'$.confirmedEffects')),0) AS confirmedEffectCount,
   MIN(CASE WHEN json_extract(c.entry_json,'$.projectionState') IN ('pending','leased','executing') THEN json_extract(c.entry_json,'$.acceptedAtUtc') END) AS oldestPendingAtUtc,
   COUNT(CASE WHEN c.command_id IS NOT NULL AND (json_type(c.entry_json,'$.effects') IS NOT 'array'
    OR json_type(c.entry_json,'$.confirmedEffects') IS NOT 'integer' OR json_extract(c.entry_json,'$.confirmedEffects')<0
    OR json_extract(c.entry_json,'$.confirmedEffects')>json_array_length(c.entry_json,'$.effects')
    OR json_array_length(c.entry_json,'$.effects')>11
    OR EXISTS (SELECT 1 FROM json_each(c.entry_json,'$.effects') WHERE type<>'text' OR value NOT IN ('create','update','scoreUp','scoreDown'))
    OR (json_extract(c.entry_json,'$.projectionState')='notRequested' AND json_array_length(c.entry_json,'$.effects')<>0)
    OR (json_extract(c.entry_json,'$.projectionState')='confirmed' AND json_extract(c.entry_json,'$.confirmedEffects')<>json_array_length(c.entry_json,'$.effects'))
    OR julianday(json_extract(c.entry_json,'$.acceptedAtUtc')) IS NULL) THEN 1 END) AS invalidCount
   FROM workspaces w LEFT JOIN workspace_commands c ON c.profile_id=w.profile_id
   WHERE w.profile_id=? AND w.connection_id=? AND w.access_epoch=? AND w.active=1 GROUP BY w.profile_id`)
   .bind(row.profile_id,row.connection_id,row.access_epoch).first<Record<string,unknown>>();
 }catch{throw new WorkspaceError('task_status_unavailable',503);}
 if(!result)throw new WorkspaceError('unauthorized',401);
 const counts=Object.fromEntries(states.map(state=>[state,Number(result![state])]));
 const total=Number(result.acceptedCommandCount),requested=Number(result.requestedEffectCount),confirmed=Number(result.confirmedEffectCount);
 if(Number(result.invalidCount)!==0||Object.values(counts).some(value=>!Number.isSafeInteger(value)||value<0)
  ||Object.values(counts).reduce((sum,value)=>sum+value,0)!==total||!Number.isSafeInteger(requested)||requested<0||!Number.isSafeInteger(confirmed)||confirmed<0||confirmed>requested
  ||!Number.isSafeInteger(result.revision)||Number(result.revision)<1||!Number.isSafeInteger(result.store_version)||Number(result.store_version)<1)
  throw new WorkspaceError('task_status_unavailable',503);
 return {provenance:'companion-canonical',workspaceId:result.workspace_id,revision:result.revision,storeVersion:result.store_version,
  checkedAtUtc:new Date().toISOString(),executorEnabled:Boolean(result.executor_enabled),executorAvailable:Boolean(env.EXECUTOR_ENABLED),
  acceptedCommandCount:total,counts,requestedEffectCount:requested,confirmedEffectCount:confirmed,oldestPendingAtUtc:result.oldestPendingAtUtc??null};
}
