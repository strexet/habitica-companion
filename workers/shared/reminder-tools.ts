import {reminderIntent} from './reminder-calendar.ts';
import {workspaceObject,uuid,WorkspaceError} from './workspace-http.ts';
export const reminderToolScopes={get_task_reminders:['profile:read','tasks:read'],prepare_reminder_command:['profile:read','tasks:read','habitica.tasks.write'],accept_reminder_command:['profile:read','tasks:read','habitica.tasks.write'],get_reminder_command:['profile:read','tasks:read']} as const;
export type ReminderTool=keyof typeof reminderToolScopes;
export function reminderInput(tool:ReminderTool,value:unknown):Record<string,unknown>{
 if(tool==='get_task_reminders'){
  const v=workspaceObject(value,['taskId','offset']);if(v.offset!==undefined&&(!Number.isSafeInteger(v.offset)||Number(v.offset)<0||Number(v.offset)>8192))throw new WorkspaceError('invalid_offset',422);
  return {taskId:uuid(v.taskId),offset:v.offset??0};
 }
 if(tool==='prepare_reminder_command'){
  const v=workspaceObject(value,['requestId','expectedWorkspaceRevision','intent']);if(!Number.isSafeInteger(v.expectedWorkspaceRevision)||Number(v.expectedWorkspaceRevision)<1||Number(v.expectedWorkspaceRevision)>Number.MAX_SAFE_INTEGER)throw new WorkspaceError('invalid_revision',422);
  return {requestId:uuid(v.requestId),expectedWorkspaceRevision:v.expectedWorkspaceRevision,intent:reminderIntent(v.intent)};
 }
 const v=workspaceObject(value,tool==='accept_reminder_command'?['commandId','acceptToken','expectedHash']:['commandId']);
 if(tool==='accept_reminder_command'&&(typeof v.acceptToken!=='string'||!/^[A-Za-z0-9_-]{43}$/.test(v.acceptToken)||typeof v.expectedHash!=='string'||!/^[a-f0-9]{64}$/.test(v.expectedHash)))throw new WorkspaceError('invalid_acceptance',422);
 return {commandId:uuid(v.commandId),...(tool==='accept_reminder_command'?{acceptToken:v.acceptToken,expectedHash:v.expectedHash}:{})};
}
