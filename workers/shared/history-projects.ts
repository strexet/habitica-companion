import type {WorkspaceDocument,CompanionTask} from './workspace-domain.ts';
import {WorkspaceError} from './workspace-http.ts';
export function historyProjects(document:WorkspaceDocument,projectId:string|undefined,unassigned:boolean){
 const projects=new Map(document.projects.map(project=>[project.id,project]));
 if(projectId!==undefined&&!projects.has(projectId))throw new WorkspaceError('history_project_not_found',404);
 const active=projectId!==undefined||unassigned;
 return {
  matches:(task:CompanionTask)=>projectId!==undefined?task.projectId===projectId:!unassigned||task.projectId===null||projects.get(task.projectId)!.isRemoved===true,
  placement:(task:CompanionTask)=>{if(!active)return null;const project=task.projectId?projects.get(task.projectId):null;return {projectId:task.projectId,projectName:project?.name??null,projectArchived:project?.isArchived===true,projectRemoved:project?.isRemoved===true,unassigned:!project||project.isRemoved===true,basis:'currentRetainedTaskAssignment'};}
 };
}
