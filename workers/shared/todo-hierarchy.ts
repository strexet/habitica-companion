import type {CompanionTask,WorkspaceDocument} from './workspace-domain.ts';
import {workspaceDocument} from './workspace-domain.ts';
import {workspaceObject,WorkspaceError,uuid} from './workspace-http.ts';
import {taskFilter} from './task-query.ts';
export interface HierarchyFilter {taskId?:string;mode?:'subtree'|'children'|'ancestors';limit?:number;offset?:number;expectedRevision?:number}
export function hierarchyFilter(input:unknown):HierarchyFilter {
 const raw=workspaceObject(input,['taskId','mode','limit','offset','expectedRevision']);
 const page=taskFilter(Object.fromEntries(Object.entries(raw).filter(([key])=>key!=='taskId'&&key!=='mode')));
 if(raw.mode!==undefined&&!['subtree','children','ancestors'].includes(raw.mode as string)||raw.mode==='ancestors'&&raw.taskId===undefined)throw new WorkspaceError('invalid_filter',422);
 return {...page,...(raw.mode===undefined?{}:{mode:raw.mode as HierarchyFilter['mode']}),...(raw.taskId===undefined?{}:{taskId:uuid(raw.taskId)})};
}
export function queryTodoHierarchy(input:WorkspaceDocument,args:unknown){
 const filter=hierarchyFilter(args),mode=filter.mode??'subtree',offset=filter.offset??0,limit=filter.limit??50;
 if(filter.expectedRevision!==undefined&&filter.expectedRevision!==input.revision)throw new WorkspaceError('stale_revision',409);
 const document=workspaceDocument(input),tasks=new Map(document.tasks.filter(task=>task.kind==='todo').map(task=>[task.id,task]));
 const context=filter.taskId?tasks.get(filter.taskId):null;if(filter.taskId&&!context)throw new WorkspaceError('task_unavailable',404);
 const children=new Map(Array.from(tasks.keys(),id=>[id,[] as CompanionTask[]])),roots:CompanionTask[]=[];
 for(const task of tasks.values())if(task.parentTaskId)children.get(task.parentTaskId)!.push(task);else roots.push(task);
 const ordered:CompanionTask[]=[],depth=new Map<string,number>(),stack=roots.map(task=>({task,depth:0})).reverse();
 while(stack.length){const row=stack.pop()!;ordered.push(row.task);depth.set(row.task.id,row.depth);const descendants=children.get(row.task.id)!;for(let index=descendants.length-1;index>=0;index--)stack.push({task:descendants[index],depth:row.depth+1});}
 let selected:CompanionTask[];
 if(mode==='children')selected=context?children.get(context.id)!:roots;
 else if(mode==='ancestors'){selected=[];let parent=context!.parentTaskId;while(parent){const ancestor=tasks.get(parent)!;selected.push(ancestor);parent=ancestor.parentTaskId;}selected.reverse();}
 else if(!context)selected=ordered;
 else {const first=ordered.findIndex(task=>task.id===context.id);let end=first+1;while(end<ordered.length&&depth.get(ordered[end].id)!>depth.get(context.id)!)end++;selected=ordered.slice(first,end);}
 const positions=new Map(selected.map((task,index)=>[task.id,index])),page=selected.slice(offset,offset+limit),next=offset+page.length;
 const items=page.map(task=>{const parent=task.parentTaskId?tasks.get(task.parentTaskId):null,parentOffset=parent?(positions.get(parent.id)??null):null;
  return {task,depth:depth.get(task.id)!,childCount:children.get(task.id)!.length,parentStatus:!parent?'none':parent.isArchived?'archived':'available',parentCoverage:!parent?'none':parentOffset===null?'outsideSelection':parentOffset<offset?'earlierPage':'page',parentOffset};});
 return {provenance:'companion-canonical',workspaceId:document.id,revision:document.revision,taskId:filter.taskId??null,mode,contextTask:context??null,items,totalTodoCount:tasks.size,matchedCount:selected.length,offset,nextOffset:next<selected.length?next:null,truncated:next<selected.length};
}
