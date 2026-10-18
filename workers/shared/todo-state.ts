import {workspaceObject,WorkspaceError} from './workspace-http.ts';
import type {CompanionTask} from './workspace-domain.ts';
export interface TodoState {state:'active'|'waiting'|'someday'|'cancelled'}
export function todoState(input:unknown):TodoState|null {
 if(input===undefined||input===null)return null;
 const value=workspaceObject(input,['state']);if(typeof value.state!=='string'||!['active','waiting','someday','cancelled'].includes(value.state))throw new WorkspaceError('invalid_todo_state',422);
 return {state:value.state as TodoState['state']};
}
export function todoStateMatches(patch:TodoState,task:CompanionTask):boolean{return task.kind==='todo'&&!task.isArchived&&task.state===patch.state;}
