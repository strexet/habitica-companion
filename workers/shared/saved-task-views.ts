import {hasRelativeDates,isRelativeTimeZone,relativeDatePresets,type RelativeDatePreset} from './relative-saved-dates.ts';
import {taskFilter,taskFilterKeys,type TaskFilter} from './task-query.ts';
import {workspaceObject,uuid,WorkspaceError} from './workspace-http.ts';
import type {Entity} from './workspace-domain.ts';
export type SavedTaskPredicates=Omit<TaskFilter,'limit'|'offset'|'expectedRevision'>&{plannedRelative?:RelativeDatePreset;deadlineRelative?:RelativeDatePreset};
export interface SavedTaskQuery {source:'tasks'|'unblockedTodos';filter:SavedTaskPredicates;timeZoneId?:string}
export interface SavedTaskView extends Entity {name:string;query:SavedTaskQuery}
const legacyPredicateKeys=taskFilterKeys.filter(key=>!['limit','offset','expectedRevision'].includes(key));
export const savedTaskPredicateKeys=[...legacyPredicateKeys,'plannedRelative','deadlineRelative'];
const invalid=():never=>{throw new WorkspaceError('invalid_saved_task_view',422);};
export function savedTaskQuery(input:unknown):SavedTaskQuery {
 const value=workspaceObject(input,['source','filter','timeZoneId']);if(value.source!=='tasks'&&value.source!=='unblockedTodos')return invalid();
 const raw=workspaceObject(value.filter,savedTaskPredicateKeys),{plannedRelative,deadlineRelative,...legacy}=raw,filter:SavedTaskPredicates=taskFilter(Object.fromEntries(Object.entries(legacy).filter(([,value])=>value!==null)));
 for(const [key,choice,from,to] of [['plannedRelative',plannedRelative,filter.plannedFrom,filter.plannedTo],['deadlineRelative',deadlineRelative,filter.deadlineFrom,filter.deadlineTo]] as const){if(choice==null)continue;if(typeof choice!=='string'||!relativeDatePresets.includes(choice as RelativeDatePreset)||from!==undefined||to!==undefined)return invalid();filter[key]=choice as RelativeDatePreset;}
 const relative=filter.plannedRelative!==undefined||filter.deadlineRelative!==undefined;if(relative?!isRelativeTimeZone(value.timeZoneId):value.timeZoneId!=null)return invalid();
 if(value.source==='unblockedTodos'&&(filter.kind!==undefined||filter.state!==undefined))return invalid();
 return {source:value.source,filter,...(relative?{timeZoneId:value.timeZoneId as string}:{})};
}
export function savedTaskViews(input:unknown,revision:number):SavedTaskView[]{
 if(!Array.isArray(input)||input.length>100000)return invalid();
 return input.map(item=>{const row=workspaceObject(item,['id','revision','isArchived','name','query']);if(!Number.isSafeInteger(row.revision)||Number(row.revision)<1||Number(row.revision)>revision||row.isArchived!==undefined&&typeof row.isArchived!=='boolean'||typeof row.name!=='string'||row.name.length>256||/^\p{White_Space}*$/u.test(row.name))return invalid();return {id:uuid(row.id),revision:Number(row.revision),isArchived:row.isArchived===true,name:row.name,query:savedTaskQuery(row.query)};});
}
export function savedTaskQueryFields(query:SavedTaskQuery):(string|null)[]{return [query.source,...legacyPredicateKeys.map(key=>query.filter[key as keyof SavedTaskPredicates]??null),...(hasRelativeDates(query)?[query.timeZoneId??null,query.filter.plannedRelative??null,query.filter.deadlineRelative??null]:[])];}
export function savedTaskQueriesEqual(left:SavedTaskQuery|undefined,right:SavedTaskQuery|undefined):boolean{if(left===undefined)return right===undefined;if(right===undefined)return false;const a=savedTaskQueryFields(left),b=savedTaskQueryFields(right);return a.length===b.length&&a.every((value,index)=>value===b[index]);}
