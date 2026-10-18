import {workspaceDocument,type WorkspaceDocument} from './workspace-domain.ts';
import {queryTasks,taskFilter,taskFilterKeys} from './task-query.ts';
import {workspaceObject} from './workspace-http.ts';
export const unblockedTodoKeys=taskFilterKeys.filter(key=>key!=='kind'&&key!=='state');
export function unblockedTodoFilter(input:unknown){return taskFilter(workspaceObject(input,unblockedTodoKeys));}
export function queryUnblockedTodos(document:WorkspaceDocument,input:unknown){
 const filter=unblockedTodoFilter(input),source=workspaceDocument(document);
 const completed=new Set(source.tasks.filter(task=>task.state==='completed').map(task=>task.id));
 return queryTasks(source,{...filter,kind:'todo',state:'active'},task=>task.dependsOn.every(id=>completed.has(id)));
}
