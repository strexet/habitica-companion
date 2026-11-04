import type {CommandEntry} from './commands.ts';
import type {WorkspaceCommand} from '../../shared/workspace-commands.ts';
export async function recoveryCommands(env:WorkspaceEnv,profileId:string,command:WorkspaceCommand):Promise<CommandEntry[]> {
 if(command.operation==='editLinkedDailySchedule'){const rows=await env.WORKSPACE_DB.prepare('SELECT entry_json FROM workspace_commands WHERE profile_id=?').bind(profileId).all<{entry_json:string}>();return rows.results.map(row=>JSON.parse(row.entry_json) as CommandEntry);}
 if(!['unarchiveTask','correctOccurrence'].includes(command.operation))return [];
 return taskRecoveryCommands(env,profileId,command.taskId);
}
export async function taskRecoveryCommands(env:WorkspaceEnv,profileId:string,taskId:string):Promise<CommandEntry[]> {
 const rows=await env.WORKSPACE_DB.prepare("SELECT entry_json FROM workspace_commands WHERE profile_id=? AND json_extract(entry_json,'$.command.taskId')=?").bind(profileId,taskId).all<{entry_json:string}>();
 return rows.results.map(row=>JSON.parse(row.entry_json) as CommandEntry).filter(row=>row.command.taskId===taskId);
}
