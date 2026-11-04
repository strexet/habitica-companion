import {reviewLinkedDailySchedule} from '../../shared/linked-daily-schedule.ts';
import {WorkspaceError} from '../../shared/workspace-http.ts';
import type {WorkspaceCommand} from '../../shared/workspace-commands.ts';
import type {WorkspaceDocument} from '../../shared/workspace-domain.ts';
import type {RemoteTask} from '../../shared/projection.ts';
import type {WorkspaceRow} from './executor.ts';
import type {TagOrigin} from './tag-authority.ts';
export async function prepareLinkedDailySchedule(env:WorkspaceEnv,row:WorkspaceRow,document:WorkspaceDocument,command:WorkspaceCommand,origin:TagOrigin):Promise<WorkspaceCommand>{
 const task=document.tasks.find(task=>task.id===command.taskId),mapping=document.projections.find(row=>row.taskId===command.taskId&&!row.isArchived);
 if(command.patch?.linkedSchedule||!command.patch?.recurrence||task?.kind!=='daily'||task.state!=='active'||mapping?.state!=='linked'||!mapping.habiticaTaskId||!mapping.fingerprint)throw new WorkspaceError('linked_schedule_review_required',409);
 const unresolved=await env.WORKSPACE_DB.prepare("SELECT 1 FROM workspace_commands WHERE profile_id=? AND json_array_length(json_extract(entry_json,'$.effects'))>0 AND json_extract(entry_json,'$.projectionState') NOT IN ('confirmed','cancelled') LIMIT 1").bind(row.profile_id).first();
 if(unresolved||await env.WORKSPACE_DB.prepare("SELECT 1 FROM workspace_tag_work WHERE profile_id=? AND state IN ('executing','unknownOutcome') LIMIT 1").bind(row.profile_id).first())throw new WorkspaceError('unresolved_projection_effects',409);
 async function observe(operation:string,args:Record<string,unknown>){
  const response=await env.AUTH_PROJECTION.fetch('https://projection.internal/operation',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({profileId:row.profile_id,connectionId:row.connection_id,operation,arguments:args,automated:false,...(origin.source==='mcp'?{requiredGrantEpoch:origin.grantEpoch}:{})}),signal:AbortSignal.timeout(15000)});
  const value=await response.json<Record<string,unknown>>();if(!response.ok||value.kind==='refused')throw new WorkspaceError(typeof value.code==='string'?value.code:'schedule_observation_unavailable',response.status===429?429:409);return value;
 }
 const account=await observe('inspectAccount',{}),observed=await observe('inspectDaily',{selector:mapping.habiticaTaskId});const remote=observed.task as RemoteTask|null;
 if(!remote||remote.id!==mapping.habiticaTaskId||remote.kind!=='daily'||remote.fingerprint!==mapping.fingerprint||!remote.schedule)throw new WorkspaceError('external_edit_review_required',409);
 if(account.needsCron!==false||!Number.isInteger(account.timezoneOffset)||!Number.isInteger(account.dayStart))throw new WorkspaceError('daily_precondition_unknown',409);
 const settings={timezoneOffset:Number(account.timezoneOffset),dayStart:Number(account.dayStart)},review=reviewLinkedDailySchedule(remote.schedule,command.patch.recurrence,settings);
 return {...command,projectToHabitica:true,patch:{...command.patch,linkedSchedule:{habiticaTaskId:remote.id,fingerprint:remote.fingerprint,observedAtUtc:new Date().toISOString(),account:settings,before:remote.schedule,expectedAfter:review.expectedAfter}}};
}
