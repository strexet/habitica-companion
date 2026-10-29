import {organizationCommand} from './workspace-organization.ts';
import {workspaceObject,uuid,WorkspaceError} from './workspace-http.ts';
export const organizationToolScopes={prepare_organization_command:'workspace.organization.write',accept_organization_command:'workspace.organization.write',get_organization_command:'tasks:read'} as const;
export type OrganizationWriteTool=keyof typeof organizationToolScopes;
export function organizationToolInput(name:string,input:unknown):Record<string,unknown>{
 if(!Object.hasOwn(organizationToolScopes,name))throw new WorkspaceError('invalid_tool',422);
 if(name!=='prepare_organization_command'){const raw=workspaceObject(input,['commandId']);return {commandId:uuid(raw.commandId)};}
 const raw=workspaceObject(input,['requestId','kind','operation','entityId','patch','expectedRevision']);
 if(raw.operation==='remove'&&raw.expectedRevision===undefined)throw new WorkspaceError('reviewed_revision_required',422);
 if(raw.patch&&typeof raw.patch==='object'&&('alias' in raw.patch||'clearAlias' in raw.patch)&&raw.expectedRevision===undefined)throw new WorkspaceError('reviewed_revision_required',422);
 const create=raw.operation==='create';if(create&&raw.entityId!==undefined)throw new WorkspaceError('server_entity_identity_required',422);
 const dummy='11111111-1111-4111-8111-111111111111';
 const command=organizationCommand({id:dummy,workspaceId:dummy,expectedRevision:raw.expectedRevision??1,kind:raw.kind,operation:raw.operation,entityId:create?dummy:uuid(raw.entityId),patch:raw.patch});
 return {requestId:uuid(raw.requestId),kind:command.kind,operation:command.operation,...(create?{}:{entityId:command.entityId}),patch:command.patch,...(raw.expectedRevision===undefined?{}:{expectedRevision:command.expectedRevision})};
}
