import {uuid,workspaceObject,WorkspaceError} from './workspace-http.ts';
const read=['profile:read','tasks:read'] as const,write=['profile:read','tasks:read','habitica.tasks.write'] as const;
export const linkedRecoveryScopes={get_linked_task_recovery:read,prepare_linked_task_recovery:write,accept_linked_task_recovery:write,get_linked_task_recovery_command:read} as const;
export type LinkedRecoveryTool=keyof typeof linkedRecoveryScopes;
export function linkedRecoveryInput(name:string,input:unknown):Record<string,unknown>{
 if(!Object.hasOwn(linkedRecoveryScopes,name))throw new WorkspaceError('invalid_tool',422);
 if(name==='get_linked_task_recovery'||name==='prepare_linked_task_recovery'){
  const prepare=name==='prepare_linked_task_recovery',row=workspaceObject(input,prepare?['taskId','completionId','requestId','expectedRevision']:['taskId','completionId']);
  if(prepare&&(!Number.isSafeInteger(row.expectedRevision)||Number(row.expectedRevision)<1))throw new WorkspaceError('invalid_revision',422);
  return {taskId:uuid(row.taskId),completionId:uuid(row.completionId),...(prepare?{requestId:uuid(row.requestId),expectedRevision:row.expectedRevision}:{})};
 }
 const accept=name==='accept_linked_task_recovery',row=workspaceObject(input,accept?['commandId','contextHash','reviewHash']:['commandId']);
 if(accept&&[row.contextHash,row.reviewHash].some(value=>typeof value!=='string'||!/^[a-f0-9]{64}$/.test(value)))throw new WorkspaceError('exact_recovery_review_required',409);
 return {...row,commandId:uuid(row.commandId)};
}
