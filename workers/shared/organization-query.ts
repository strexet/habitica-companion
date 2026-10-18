import type { WorkspaceDocument } from './workspace-domain.ts';
import { taskFilter } from './task-query.ts';
import { WorkspaceError, workspaceObject, uuid } from './workspace-http.ts';
export const organizationNames=['list_categories','list_projects','list_project_sections','list_labels','list_goals'] as const;
export type OrganizationTool=typeof organizationNames[number];
export function organizationInput(name:OrganizationTool,input:unknown){
 const value=workspaceObject(input,['limit','offset','expectedRevision',...(name==='list_projects'?['categoryId']:name==='list_project_sections'?['projectId']:[])]);
 const filter=taskFilter(value);
 if(name==='list_project_sections')filter.projectId=uuid(value.projectId);
 return filter;
}
export function queryOrganization(document:WorkspaceDocument,name:OrganizationTool,input:unknown){
 const filter=organizationInput(name,input);
 if(filter.expectedRevision!==undefined&&filter.expectedRevision!==document.revision)throw new WorkspaceError('stale_revision',409);
 const source=name==='list_categories'?document.categories:name==='list_projects'?document.projects:name==='list_project_sections'?document.sections:name==='list_labels'?document.labels:document.goals;
 let items=source.filter(value=>!value.isArchived);
 if(name==='list_projects')items=items.filter(value=>filter.categoryId===undefined||('categoryId' in value&&value.categoryId===filter.categoryId));
 if(name==='list_project_sections'){
  const active=document.projects.some(project=>project.id===filter.projectId&&!project.isArchived);
  items=items.filter(value=>active&&'projectId' in value&&value.projectId===filter.projectId);
  items.sort((a,b)=>('position' in a?Number(a.position):0)-('position' in b?Number(b.position):0));
 }
 const offset=filter.offset??0,limit=filter.limit??50,selected=items.slice(offset,offset+limit),next=offset+selected.length;
 const tasks=name==='list_goals'?new Map(document.tasks.map(task=>[task.id,task])):null;
 const result=name==='list_goals'?selected.map(value=>{
  const goal=value as WorkspaceDocument['goals'][number];
  const eligible=goal.taskIds.map(id=>tasks!.get(id)!).filter(task=>task.kind==='todo'&&!task.isArchived&&task.state!=='cancelled');
  return {goal,completedTodos:eligible.filter(task=>task.state==='completed').length,eligibleTodos:eligible.length};
 }):selected;
 return {provenance:'companion-canonical',workspaceId:document.id,revision:document.revision,items:result,matchedCount:items.length,truncated:next<items.length,offset,nextOffset:next<items.length?next:null};
}
