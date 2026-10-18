import {workspaceObject,uuid} from './workspace-http.ts';
export interface TodoRestore {completionId:string}
export function todoRestore(input:unknown):TodoRestore|null {
 if(input===undefined||input===null)return null;
 const row=workspaceObject(input,['completionId']);return {completionId:uuid(row.completionId)};
}
