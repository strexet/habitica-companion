import {uuid,workspaceObject,WorkspaceError} from './workspace-http.ts';
export const migrationWriteScope='workspace.migrate';
const read=['profile:read','tasks:read'] as const,write=['profile:read','tasks:read',migrationWriteScope] as const;
export const workspaceTransferScopes={list_workspace_transfers:read,get_workspace_transfer_status:read,preview_workspace_transfer:write,accept_workspace_transfer:write,get_workspace_transfer_receipt:read,get_workspace_transfer_record:read} as const;
export type WorkspaceTransferTool=keyof typeof workspaceTransferScopes;
export function workspaceTransferInput(name:string,input:unknown){
 if(!Object.hasOwn(workspaceTransferScopes,name))throw new WorkspaceError('invalid_tool',422);
 const keys=name==='list_workspace_transfers'?['offset']:name==='preview_workspace_transfer'?['transferId','archiveConflicts']:name==='accept_workspace_transfer'?['transferId','reviewHash']:name==='get_workspace_transfer_record'?['transferId','kind','sourceWorkspaceId','commandId']:['transferId'];
 const value=workspaceObject(input,keys);
 if(name==='list_workspace_transfers'){if(value.offset!==undefined&&(!Number.isSafeInteger(value.offset)||Number(value.offset)<0||Number(value.offset)>32))throw new WorkspaceError('invalid_offset',422);return value;}
 uuid(value.transferId);
 if(name==='preview_workspace_transfer'&&value.archiveConflicts!==undefined&&typeof value.archiveConflicts!=='boolean')throw new WorkspaceError('invalid_conflict_policy',422);
 if(name==='accept_workspace_transfer'&&(typeof value.reviewHash!=='string'||!/^[a-f0-9]{64}$/.test(value.reviewHash)))throw new WorkspaceError('exact_transfer_review_required',409);
 if(name==='get_workspace_transfer_record'){uuid(value.commandId);if(!['taskCommand','organizationCommand','noteReceipt','folderReceipt'].includes(String(value.kind)))throw new WorkspaceError('invalid_source_record',422);if(['noteReceipt','folderReceipt'].includes(String(value.kind))){if(value.sourceWorkspaceId!==undefined)throw new WorkspaceError('invalid_source_record',422);}else uuid(value.sourceWorkspaceId);}
 return value;
}
