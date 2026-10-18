import {taskFilter,taskFilterKeys,type TaskFilter} from './task-query.ts';
import {workspaceObject,uuid,WorkspaceError} from './workspace-http.ts';
import type {Entity} from './workspace-domain.ts';
export type SavedTaskPredicates=Omit<TaskFilter,'limit'|'offset'|'expectedRevision'>;
export interface SavedTaskQuery {source:'tasks'|'unblockedTodos';filter:SavedTaskPredicates}
export interface SavedTaskView extends Entity {name:string;query:SavedTaskQuery}
export const savedTaskPredicateKeys=taskFilterKeys.filter(key=>!['limit','offset','expectedRevision'].includes(key));
const invalid=():never=>{throw new WorkspaceError('invalid_saved_task_view',422);};
export function savedTaskQuery(input:unknown):SavedTaskQuery {
 const value=workspaceObject(input,['source','filter']);if(value.source!=='tasks'&&value.source!=='unblockedTodos')return invalid();
 const raw=workspaceObject(value.filter,savedTaskPredicateKeys),filter=taskFilter(Object.fromEntries(Object.entries(raw).filter(([,value])=>value!==null)));
 if(value.source==='unblockedTodos'&&(filter.kind!==undefined||filter.state!==undefined))return invalid();
 return {source:value.source,filter};
}
export function savedTaskViews(input:unknown,revision:number):SavedTaskView[]{
 if(!Array.isArray(input)||input.length>100000)return invalid();
 return input.map(item=>{const row=workspaceObject(item,['id','revision','isArchived','name','query']);if(!Number.isSafeInteger(row.revision)||Number(row.revision)<1||Number(row.revision)>revision||row.isArchived!==undefined&&typeof row.isArchived!=='boolean'||typeof row.name!=='string'||row.name.length>256||/^\p{White_Space}*$/u.test(row.name))return invalid();return {id:uuid(row.id),revision:Number(row.revision),isArchived:row.isArchived===true,name:row.name,query:savedTaskQuery(row.query)};});
}
