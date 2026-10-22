import {workspaceObject,uuid,WorkspaceError} from './workspace-http.ts';
const writes=['profile:read','habitica.tasks.write','workspace.organization.write','habitica.tags.write'] as const;
export const retirementToolScopes={prepare_source_retirement:writes,verify_source_retirement:writes,accept_source_retirement:writes,get_source_retirement_command:['profile:read','tasks:read'],reconcile_source_retirement:writes} as const;
export type RetirementTool=keyof typeof retirementToolScopes;
export function retirementInput(name:string,value:unknown):Record<string,unknown>{
 if(!Object.hasOwn(retirementToolScopes,name))throw new WorkspaceError('invalid_tool',422);
 if(name==='prepare_source_retirement'){const row=workspaceObject(value,['requestId','conversionId','expectedRevision']);if(!Number.isSafeInteger(row.expectedRevision)||Number(row.expectedRevision)<1)throw new WorkspaceError('invalid_revision',422);return {requestId:uuid(row.requestId),conversionId:uuid(row.conversionId),expectedRevision:row.expectedRevision};}
 const row=workspaceObject(value,name==='accept_source_retirement'?['commandId','contextHash','irreversibleApproved']:['commandId']);
 if(name==='accept_source_retirement'&&(typeof row.contextHash!=='string'||!/^[a-f0-9]{64}$/.test(row.contextHash)||row.irreversibleApproved!==true))throw new WorkspaceError('exact_retirement_approval_required',409);
 return {...row,commandId:uuid(row.commandId)};
}
