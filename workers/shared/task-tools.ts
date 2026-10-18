import {unblockedTodoFilter} from './unblocked-todos.ts';
import { workspaceObject, WorkspaceError, uuid } from './workspace-http.ts';
import { taskFilter, taskFilterKeys } from './task-query.ts';
import { agendaFilter, agendaKeys } from './task-agenda.ts';
import {habitHistoryFilter,habitHistoryKeys} from './habit-history.ts';
import {historyFilter,historyKeys} from './completion-history.ts';
import { organizationNames, organizationInput, type OrganizationTool } from './organization-query.ts';
export const taskToolScopes={list_companion_tasks:'tasks:read',list_companion_agenda:'tasks:read',list_categories:'tasks:read',list_projects:'tasks:read',list_project_sections:'tasks:read',list_labels:'tasks:read',list_goals:'tasks:read',get_companion_task:'tasks:read',get_task_command:'tasks:read',get_task_sync_status:'tasks:read',get_completed_history:'tasks:read',get_habit_history:'tasks:read',get_unblocked_todos:'tasks:read',prepare_task_command:'habitica.tasks.write',create_todo:'habitica.tasks.write',create_daily:'habitica.tasks.write',create_habit:'habitica.tasks.write',delete_task:'habitica.tasks.write',update_task:'habitica.tasks.write',complete_task:'habitica.tasks.write',score_habit:'habitica.tasks.write',skip_occurrence:'habitica.tasks.write',pause_task:'habitica.tasks.write',resume_task:'habitica.tasks.write',plan_task:'habitica.tasks.write',restore_todo:'habitica.tasks.write',relate_todo:'habitica.tasks.write',set_todo_state:'habitica.tasks.write',set_todo_stage:'habitica.tasks.write'} as const;
export type TaskTool=keyof typeof taskToolScopes;
export const operationForTool={create_todo:'createTodo',create_daily:'createDaily',create_habit:'createHabit',delete_task:'deleteTask',update_task:'updateTask',complete_task:'completeTask',score_habit:'scoreHabit',skip_occurrence:'skipOccurrence',pause_task:'pauseTask',resume_task:'resumeTask',plan_task:'planTask',restore_todo:'restoreTodo',relate_todo:'relateTodo',set_todo_state:'setTodoState',set_todo_stage:'setTodoStage'} as const;
export function taskToolInput(name:string,input:unknown):Record<string,unknown>{
 if(!Object.hasOwn(taskToolScopes,name))throw new WorkspaceError('invalid_tool',422);
 if(name==='get_unblocked_todos'){unblockedTodoFilter(input);return input as Record<string,unknown>;}
 if(name==='get_habit_history'){workspaceObject(input,habitHistoryKeys);habitHistoryFilter(input);return input as Record<string,unknown>;}
 if(name==='get_completed_history'){workspaceObject(input,historyKeys);historyFilter(input);return input as Record<string,unknown>;}
 if(name==='get_task_sync_status')return workspaceObject(input,[]);
 if(organizationNames.includes(name as OrganizationTool))return {...organizationInput(name as OrganizationTool,input)};
 const keys=name==='prepare_task_command'?['requestId','operation','taskId','patch','direction','count','occurrenceDate','projectToHabitica','expectedRevision']:name==='list_companion_tasks'?taskFilterKeys:name==='list_companion_agenda'?agendaKeys:name==='get_companion_task'?['taskId']:['commandId'];
 const value=workspaceObject(input,keys);
 if(name==='prepare_task_command'){uuid(value.requestId);if(!Object.values(operationForTool).includes(value.operation as never))throw new WorkspaceError('invalid_operation',422);}
 else if(name==='list_companion_tasks')return {...taskFilter(value)};
 else if(name==='list_companion_agenda')return {...agendaFilter(value)};
 else if(name==='get_companion_task')return {...value,taskId:uuid(value.taskId)};else uuid(value.commandId);
 return value;
}
