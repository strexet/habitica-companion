import {cronScopes} from './cron.ts';
import {workspaceObject,uuid,WorkspaceError} from './workspace-http.ts';
export const cronToolScopes={get_cron_status:cronScopes.status,prepare_start_new_day:cronScopes.execute,execute_start_new_day:cronScopes.execute,get_cron_command:cronScopes.lookup} as const;
export type CronTool=keyof typeof cronToolScopes;
export function cronInput(tool:string,value:unknown){
 if(!Object.hasOwn(cronToolScopes,tool))throw new WorkspaceError('invalid_tool',422);
 const fields=tool==='get_cron_status'?[]:tool==='prepare_start_new_day'?['requestId']:tool==='execute_start_new_day'?['actionId','executeToken']:['actionId'];
 const input=workspaceObject(value,fields);
 if(tool==='prepare_start_new_day')input.requestId=uuid(input.requestId);else if(tool!=='get_cron_status')input.actionId=uuid(input.actionId);
 if(tool==='execute_start_new_day'&&(typeof input.executeToken!=='string'||!/^[A-Za-z0-9_-]{43}$/.test(input.executeToken)))throw new WorkspaceError('invalid_execute_token',422);
 return input;
}
