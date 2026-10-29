import {uuid,workspaceObject,WorkspaceError} from './workspace-http.ts';
const read=['profile:read','tasks:read'] as const,write=['profile:read','tasks:read','habitica.tasks.write'] as const;
export const adoptionScopes={prepare_task_adoption:write,accept_task_adoption:write,get_task_adoption_command:read} as const;
export type AdoptionTool=keyof typeof adoptionScopes;
export function adoptionInput(name:string,value:unknown):Record<string,unknown>{
 if(!Object.hasOwn(adoptionScopes,name))throw new WorkspaceError('invalid_tool',422);
 if(name==='prepare_task_adoption'){
  const input=workspaceObject(value,['requestId','taskId','habiticaTaskId','expectedRevision']);
  if(input.expectedRevision!==undefined&&(!Number.isSafeInteger(input.expectedRevision)||Number(input.expectedRevision)<1))throw new WorkspaceError('invalid_revision',422);
  return {requestId:uuid(input.requestId),taskId:uuid(input.taskId),habiticaTaskId:uuid(input.habiticaTaskId),...(input.expectedRevision===undefined?{}:{expectedRevision:input.expectedRevision})};
 }
 const input=workspaceObject(value,name==='accept_task_adoption'?['commandId','reviewHash']:['commandId']);
 if(name==='accept_task_adoption'&&(typeof input.reviewHash!=='string'||!/^[a-f0-9]{64}$/.test(input.reviewHash)))throw new WorkspaceError('exact_adoption_review_required',409);
 return {...input,commandId:uuid(input.commandId)};
}
