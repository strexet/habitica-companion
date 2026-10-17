import { workspaceObject, WorkspaceError, uuid } from './workspace-http.ts';
export const taskToolScopes={list_companion_tasks:'tasks:read',get_companion_task:'tasks:read',get_task_command:'tasks:read',prepare_task_command:'habitica.tasks.write',create_todo:'habitica.tasks.write',create_daily:'habitica.tasks.write',create_habit:'habitica.tasks.write',delete_task:'habitica.tasks.write',update_task:'habitica.tasks.write',complete_task:'habitica.tasks.write',score_habit:'habitica.tasks.write'} as const;
export type TaskTool=keyof typeof taskToolScopes;
export const operationForTool={create_todo:'createTodo',create_daily:'createDaily',create_habit:'createHabit',delete_task:'deleteTask',update_task:'updateTask',complete_task:'completeTask',score_habit:'scoreHabit'} as const;
export function taskToolInput(name:string,input:unknown):Record<string,unknown>{
 if(!Object.hasOwn(taskToolScopes,name))throw new WorkspaceError('invalid_tool',422);
 const keys=name==='prepare_task_command'?['requestId','operation','taskId','patch','direction','count','occurrenceDate','projectToHabitica','expectedRevision']:name==='list_companion_tasks'?['query','kind','limit']:name==='get_companion_task'?['taskId']:['commandId'];
 const value=workspaceObject(input,keys);
 if(name==='prepare_task_command'){uuid(value.requestId);if(!Object.values(operationForTool).includes(value.operation as never))throw new WorkspaceError('invalid_operation',422);}
 else if(name==='list_companion_tasks'){
  if(value.query!==undefined&&(typeof value.query!=='string'||value.query.length>256)||value.kind!==undefined&&!['todo','daily','habit'].includes(value.kind as string)||value.limit!==undefined&&(!Number.isInteger(value.limit)||Number(value.limit)<1||Number(value.limit)>100))throw new WorkspaceError('invalid_filter',422);
 }else if(name==='get_companion_task')return {...value,taskId:uuid(value.taskId)};else uuid(value.commandId);
 return value;
}
