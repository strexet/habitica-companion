import type {WorkspaceDocument} from './workspace-domain.ts';
import type {WorkspaceCommand} from './workspace-commands.ts';
import {WorkspaceError} from './workspace-http.ts';

// Applied only to new intent. Existing accepted commands and command hashes are immutable.
export const todoProjectionPolicyVersion='todo-required-v1';
export const habitProjectionPolicyVersion='habit-required-v1';
export function requiresTodoProjection(document:WorkspaceDocument,command:WorkspaceCommand):boolean{
 return command.operation==='createTodo'||['updateTask','completeTask'].includes(command.operation)&&
  document.tasks.some(task=>task.id===command.taskId&&task.kind==='todo');
}
export function prepareTodoProjection(document:WorkspaceDocument,command:WorkspaceCommand):WorkspaceCommand{
 if(command.operation==='createHabit'&&command.patch){const details=command.patch.habiticaDetails;return {...command,projectToHabitica:true,patch:{...command.patch,habiticaDetails:{difficulty:details?.difficulty??null,attribute:details?.attribute??null,checklist:details?.checklist??null,up:details?.up??true,down:details?.down??true}}};}
 return requiresTodoProjection(document,command)?{...command,projectToHabitica:true}:command;
}
export function requireTodoProjection(document:WorkspaceDocument,command:WorkspaceCommand):void{
 if(command.operation==='createHabit'&&!command.projectToHabitica)throw new WorkspaceError('habit_projection_required',422);
 if(!requiresTodoProjection(document,command))return;
 if(!command.projectToHabitica)throw new WorkspaceError('todo_projection_required',422);
 const task=document.tasks.find(task=>task.id===command.taskId);
 if(task?.state==='completed'&&!document.projections.some(mapping=>mapping.taskId===task.id&&
  (mapping.habiticaTaskId!==null||mapping.state!=='unlinked')))
  throw new WorkspaceError('historical_projection_requires_review',409);
}
