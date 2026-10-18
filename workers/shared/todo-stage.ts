import {workspaceObject,WorkspaceError} from './workspace-http.ts';
import type {CompanionTask} from './workspace-domain.ts';
export type TodoExecutionStage='inbox'|'next'|'inProgress';
export interface TodoStage {stage:TodoExecutionStage|null;clearStage:boolean}
export function executionStage(input:unknown):TodoExecutionStage|null {
 if(input===undefined||input===null)return null;
 if(typeof input!=='string'||!['inbox','next','inProgress'].includes(input))throw new WorkspaceError('invalid_execution_stage',422);return input as TodoExecutionStage;
}
export function todoStage(input:unknown):TodoStage|null {
 if(input===undefined||input===null)return null;const value=workspaceObject(input,['stage','clearStage']);
 if(value.clearStage!==undefined&&typeof value.clearStage!=='boolean')throw new WorkspaceError('invalid_todo_stage',422);
 const stage=executionStage(value.stage),clearStage=value.clearStage===true;if(clearStage?stage!==null:stage===null)throw new WorkspaceError('invalid_todo_stage',422);return {stage,clearStage};
}
export function todoStageMatches(patch:TodoStage,task:CompanionTask):boolean{return task.kind==='todo'&&task.state==='active'&&!task.isArchived&&(task.executionStage??null)===(patch.clearStage?null:patch.stage);}
