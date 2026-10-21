import {dailyRuleFailure} from './daily-projection.ts';
import type {WorkspaceDocument} from './workspace-domain.ts';
import type {WorkspaceCommand} from './workspace-commands.ts';
import {WorkspaceError} from './workspace-http.ts';
// Applied only to new intent. Existing accepted commands and command hashes are immutable.
export const todoProjectionPolicyVersion='todo-required-v1';
export const dailyProjectionPolicyVersion='daily-required-v1';
export const habitProjectionPolicyVersion='habit-required-v1';
export const taskProjectionPolicyVersion='task-required-v1';
export function isNativeScheduleEdit(command:WorkspaceCommand):boolean{
 const patch=command.patch;return command.operation==='updateTask'&&!!patch&&(!!patch.recurrence||!!patch.clearRecurrence)
  &&Object.entries(patch).every(([key,value])=>['recurrence','clearRecurrence'].includes(key)||value===null||value===false);
}
export function requiresTodoProjection(document:WorkspaceDocument,command:WorkspaceCommand):boolean{
 return ['createTodo','createDaily','createHabit'].includes(command.operation)||!isNativeScheduleEdit(command)
  &&['updateTask','completeTask','scoreHabit'].includes(command.operation)&&(document.tasks??[]).some(task=>task.id===command.taskId);
}
export function prepareTodoProjection(document:WorkspaceDocument,command:WorkspaceCommand):WorkspaceCommand{
 if(command.operation==='createHabit'&&command.patch){const details=command.patch.habiticaDetails;return {...command,projectToHabitica:true,patch:{...command.patch,habiticaDetails:{difficulty:details?.difficulty??null,attribute:details?.attribute??null,checklist:details?.checklist??null,up:details?.up??true,down:details?.down??true}}};}
 return requiresTodoProjection(document,command)?{...command,projectToHabitica:true}:command;
}
export function requireTodoProjection(document:WorkspaceDocument,command:WorkspaceCommand):void{
 const task=(document.tasks??[]).find(task=>task.id===command.taskId),mappings=(document.projections??[]).filter(mapping=>mapping.taskId===command.taskId);
 if(isNativeScheduleEdit(command)){if(mappings.some(mapping=>mapping.habiticaTaskId!=null||mapping.state!=='unlinked'))throw new WorkspaceError('unsupported_projection',422);return;}
 if(!requiresTodoProjection(document,command))return;
 const kind=command.operation==='createDaily'?'daily':command.operation==='createHabit'?'habit':task?.kind??'todo';
 if(!command.projectToHabitica)throw new WorkspaceError(kind+'_projection_required',422);
 if(mappings.some(mapping=>mapping.isArchived&&(mapping.habiticaTaskId!=null||mapping.state!=='unlinked')||mapping.state==='unlinked'&&mapping.habiticaTaskId!=null))throw new WorkspaceError('projection_review_required',409);
 const linked=mappings.some(mapping=>!mapping.isArchived&&mapping.habiticaTaskId!=null);
 if(task?.kind==='todo'&&task.state==='completed'&&!mappings.some(mapping=>!mapping.isArchived&&(mapping.habiticaTaskId!=null||mapping.state!=='unlinked')))throw new WorkspaceError('historical_projection_requires_review',409);
 if(!linked&&task?.kind==='habit'&&(task.deadline!=null||task.habiticaDetails?.checklist!=null))throw new WorkspaceError('unsupported_habit_fields',422);
 if(!linked&&task?.kind==='daily'&&task.deadline!=null)throw new WorkspaceError('unsupported_daily_fields',422);
 if(kind==='daily'&&(command.operation==='createDaily'||!linked)){const failure=dailyRuleFailure(command.patch?.recurrence??task?.recurrence);if(failure)throw new WorkspaceError(failure,422);}
}
