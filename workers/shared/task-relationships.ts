import {workspaceObject,uuid,WorkspaceError} from './workspace-http.ts';
import type {WorkspaceDocument,CompanionTask} from './workspace-domain.ts';
export interface TaskRelationships {parentTaskId:string|null;clearParentTask:boolean;dependsOn:string[]|null}
export function taskRelationships(input:unknown):TaskRelationships|null {
 if(input===undefined||input===null)return null;
 const value=workspaceObject(input,['parentTaskId','clearParentTask','dependsOn']);
 if(value.clearParentTask!==undefined&&typeof value.clearParentTask!=='boolean')throw new WorkspaceError('invalid_relationships',422);
 const parentTaskId=value.parentTaskId==null?null:uuid(value.parentTaskId),clearParentTask=value.clearParentTask===true;
 let dependsOn:string[]|null=null;
 if(value.dependsOn!==undefined&&value.dependsOn!==null){if(!Array.isArray(value.dependsOn)||value.dependsOn.length>100)throw new WorkspaceError('invalid_relationships',422);dependsOn=value.dependsOn.map(uuid);if(new Set(dependsOn).size!==dependsOn.length)throw new WorkspaceError('invalid_relationships',422);}
 if(clearParentTask&&parentTaskId||parentTaskId===null&&!clearParentTask&&dependsOn===null)throw new WorkspaceError('invalid_relationships',422);
 return {parentTaskId,clearParentTask,dependsOn};
}
export function applyRelationships(task:CompanionTask,patch:TaskRelationships):CompanionTask{return {...task,parentTaskId:patch.clearParentTask?null:patch.parentTaskId??task.parentTaskId,dependsOn:patch.dependsOn??task.dependsOn};}
export function relationshipReferencesAvailable(document:WorkspaceDocument,task:CompanionTask,patch:TaskRelationships):boolean {
 const available=(id:string)=>id!==task.id&&document.tasks.some(row=>row.id===id&&row.kind==='todo'&&!row.isArchived);
 return task.kind==='todo'&&(patch.parentTaskId===null||available(patch.parentTaskId))&&(patch.dependsOn===null||patch.dependsOn.every(available));
}
export function relationshipsMatch(patch:TaskRelationships,task:CompanionTask):boolean{return task.kind==='todo'&&(patch.parentTaskId===null||task.parentTaskId===patch.parentTaskId)&&(!patch.clearParentTask||task.parentTaskId===null)&&(patch.dependsOn===null||JSON.stringify(task.dependsOn)===JSON.stringify(patch.dependsOn));}
