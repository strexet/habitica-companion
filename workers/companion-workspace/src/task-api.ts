import { taskToolInput, operationForTool, type TaskTool } from '../../shared/task-tools.ts';
import { workspaceCommand, isTaskCreation } from '../../shared/workspace-commands.ts';
import { WorkspaceError, workspaceTokenHash, uuid } from '../../shared/workspace-http.ts';
import type { WorkspaceRow } from './executor.ts';
import type { CommandEntry } from './commands.ts';
import { accept } from './commands.ts';
import { queryTasks } from '../../shared/task-query.ts';
import { taskSyncStatus } from './task-status.ts';
import { queryAgenda } from '../../shared/task-agenda.ts';
import { organizationNames, queryOrganization, type OrganizationTool } from '../../shared/organization-query.ts';
interface Preparation {grant_epoch:string;access_epoch:string;request_id:string;command_id:string;input_hash:string;command_json:string;correlation_id:string;expires_at_utc:string}
const bytes=(value:string)=>new TextEncoder().encode(value).length;
function stable(value:unknown):string {if(value===null||typeof value!=='object')return JSON.stringify(value);if(Array.isArray(value))return '['+value.map(stable).join(',')+']';return '{'+Object.keys(value).sort().map(k=>JSON.stringify(k)+':'+stable((value as Record<string,unknown>)[k])).join(',')+'}';}
export function mutationResult(entry:CommandEntry,document:{tasks:{id:string;text:string}[]},correlationId:string,refreshSucceeded=true){
 const state=entry.projectionState;const unknown=state==='unknownOutcome';
 return {operation:entry.command.operation,taskId:entry.command.taskId,commandId:entry.command.id,correlationId,taskText:entry.taskSnapshot?.text??document.tasks.find(t=>t.id===entry.command.taskId)?.text??'',savedInCompanion:true,habiticaProjectionStatus:unknown?'UnknownOutcome':['notRequested','confirmed','blocked','cancelled'].includes(state)?state:'pending',acceptedEventCount:entry.command.operation==='scoreHabit'?entry.command.count:['completeTask','skipOccurrence'].includes(entry.command.operation)?1:0,confirmedProjectionCount:entry.confirmedEffects,totalProjectionCount:entry.effects.length,failurePosition:entry.failure==='none'?null:entry.confirmedEffects+1,failure:entry.failure,retryAtUtc:entry.retryAtUtc,refreshSucceeded,outcomeUnknown:unknown};
}
export async function taskApi(env:WorkspaceEnv,row:WorkspaceRow,name:TaskTool,args:unknown,submit:(command:unknown)=>Promise<Response>):Promise<Record<string,unknown>>{
 const input=taskToolInput(name,args);
 if(name==='get_task_sync_status')return taskSyncStatus(env,row);
 const document=JSON.parse(row.document_json);
 if(name==='list_companion_tasks')return queryTasks(document,input);
 if(name==='list_companion_agenda')return queryAgenda(document,input);
 if(organizationNames.includes(name as OrganizationTool))return queryOrganization(document,name as OrganizationTool,input);
 if(name==='get_companion_task'){const task=document.tasks.find((t:{id:string;isArchived:boolean})=>t.id===input.taskId&&!t.isArchived);return {provenance:'companion-canonical',workspaceId:row.workspace_id,revision:document.revision,task:task??null};}
 async function receipt(commandId:string){
  const found=await env.WORKSPACE_DB.prepare('SELECT c.entry_json,p.correlation_id FROM workspace_commands c JOIN workspaces w ON w.profile_id=c.profile_id LEFT JOIN workspace_preparations p ON p.profile_id=c.profile_id AND p.command_id=c.command_id WHERE c.profile_id=? AND c.command_id=? AND w.access_epoch=? AND w.active=1').bind(row.profile_id,commandId,row.access_epoch).first<{entry_json:string;correlation_id:string|null}>();
  return found?{entry:JSON.parse(found.entry_json) as CommandEntry,correlationId:found.correlation_id??commandId}:null;
 }
 if(name==='get_task_command'){const found=await receipt(uuid(input.commandId));return found?mutationResult(found.entry,document,found.correlationId):{savedInCompanion:null,commandId:input.commandId,habiticaProjectionStatus:'unavailable',outcomeUnknown:true};}
 if(name==='prepare_task_command'){
  const requestId=uuid(input.requestId);const hash=await workspaceTokenHash(stable(input));
  let prepared=await env.WORKSPACE_DB.prepare('SELECT * FROM workspace_preparations WHERE profile_id=? AND request_id=?').bind(row.profile_id,requestId).first<Preparation>();
  if(prepared&&prepared.input_hash!==hash)throw new WorkspaceError('request_id_reused',409);
  if(!prepared){
   // IDs come from the server. Task names and transport request IDs never select mutation targets.
   let command;try{command=workspaceCommand({id:crypto.randomUUID(),workspaceId:row.workspace_id,expectedRevision:input.expectedRevision??document.revision,operation:input.operation,taskId:isTaskCreation(input.operation)?crypto.randomUUID():uuid(input.taskId),patch:input.patch,direction:input.direction,count:input.count,occurrenceDate:input.occurrenceDate,projectToHabitica:input.projectToHabitica});}catch(error){if(error instanceof WorkspaceError)throw error;throw new WorkspaceError('invalid_command',422);}
   if(isTaskCreation(input.operation)&&input.taskId!==undefined)throw new WorkspaceError('server_task_identity_required',422);
   await accept(document,command,new Date().toISOString()); // Validate exact canonical state without accepting a command.
   const now=new Date().toISOString();const serialized=JSON.stringify(command);const size=row.archive_bytes+bytes(serialized)+512;
   if(size>8*1024*1024-32768||row.store_version>=Number.MAX_SAFE_INTEGER)throw new WorkspaceError('workspace_capacity',413);
   const tag=crypto.randomUUID();const expires=new Date(Date.now()+10*60000).toISOString();const correlationId=crypto.randomUUID();
   const changed=await env.WORKSPACE_DB.batch([
    env.WORKSPACE_DB.prepare('UPDATE workspaces SET archive_bytes=?,commit_tag=?,store_version=store_version+1 WHERE profile_id=? AND access_epoch=? AND store_version=? AND commit_tag=? AND active=1').bind(size,tag,row.profile_id,row.access_epoch,row.store_version,row.commit_tag),
    env.WORKSPACE_DB.prepare('INSERT INTO workspace_preparations(profile_id,access_epoch,request_id,command_id,input_hash,command_json,correlation_id,created_at_utc,expires_at_utc,grant_epoch) SELECT profile_id,access_epoch,?,?,?,?,?,?,?,? FROM workspaces WHERE profile_id=? AND commit_tag=?').bind(requestId,command.id,hash,serialized,correlationId,now,expires,row.mcp_grant_epoch??'legacy',row.profile_id,tag),
   ]);
   prepared=await env.WORKSPACE_DB.prepare('SELECT * FROM workspace_preparations WHERE profile_id=? AND request_id=?').bind(row.profile_id,requestId).first<Preparation>();
   if(!prepared||prepared.input_hash!==hash)throw new WorkspaceError(changed[0].meta.changes?'request_id_reused':'workspace_changed',409);
  }
  const found=await receipt(prepared.command_id);if(!found&&(prepared.access_epoch!==row.access_epoch||prepared.grant_epoch!==(row.mcp_grant_epoch??'legacy')))throw new WorkspaceError('preview_connection_changed',409);const command=JSON.parse(prepared.command_json);
  return {preparedCommandId:prepared.command_id,correlationId:prepared.correlation_id,expiresAtUtc:prepared.expires_at_utc,expired:prepared.expires_at_utc<=new Date().toISOString()&&!found,command,mayAffectHabitica:command.projectToHabitica||document.projections.some((p:{taskId:string;state:string})=>p.taskId===command.taskId&&p.state!=='unlinked'),savedInCompanion:Boolean(found),...(found?{receipt:mutationResult(found.entry,document,found.correlationId)}:{})};
 }
 const commandId=uuid(input.commandId);const found=await receipt(commandId);const operation=operationForTool[name as keyof typeof operationForTool];
 if(found){if(found.entry.command.operation!==operation)throw new WorkspaceError('operation_mismatch',409);return mutationResult(found.entry,document,found.correlationId);}
 const prepared=await env.WORKSPACE_DB.prepare('SELECT * FROM workspace_preparations WHERE profile_id=? AND command_id=?').bind(row.profile_id,commandId).first<Preparation>();
 if(!prepared||prepared.grant_epoch!==(row.mcp_grant_epoch??'legacy')||prepared.access_epoch!==row.access_epoch||prepared.expires_at_utc<=new Date().toISOString())throw new WorkspaceError('prepare_required_or_expired',409);
 const command=JSON.parse(prepared.command_json);if(command.operation!==operation)throw new WorkspaceError('operation_mismatch',409);
 const response=await submit(command);if(!response.ok){let code='command_unavailable';try{code=(await response.json<{error:string}>()).error??code;}catch{}throw new WorkspaceError(code,response.status);}
 const result=await response.json<{entry:CommandEntry}>();
 try{const current=await env.WORKSPACE_DB.prepare('SELECT document_json FROM workspaces WHERE profile_id=? AND access_epoch=? AND active=1').bind(row.profile_id,row.access_epoch).first<{document_json:string}>();const latest=await receipt(commandId);if(current&&latest)return mutationResult(latest.entry,JSON.parse(current.document_json),prepared.correlation_id,true);}catch{}
 return mutationResult(result.entry,document,prepared.correlation_id,false);
}
