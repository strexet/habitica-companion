import {conversionCommand} from './checklist-conversion.ts';
import {workspaceObject as object,uuid,WorkspaceError} from './workspace-http.ts';
export const conversionToolScopes={get_checklist_conversion:['profile:read','tasks:read'],prepare_checklist_conversion:['profile:read','habitica.tasks.write','workspace.organization.write'],accept_checklist_conversion:['profile:read','habitica.tasks.write','workspace.organization.write'],get_checklist_conversion_command:['profile:read','tasks:read']} as const;
export type ConversionTool=keyof typeof conversionToolScopes;
export function conversionInput(name:string,value:unknown):Record<string,unknown>{
 if(!Object.hasOwn(conversionToolScopes,name))throw new WorkspaceError('invalid_tool',422);
 if(name==='get_checklist_conversion'){const input=object(value,['sourceTaskId']);return {sourceTaskId:uuid(input.sourceTaskId)};}
 if(name!=='prepare_checklist_conversion'){const input=object(value,['commandId']);return {commandId:uuid(input.commandId)};}
 const input=object(value,['requestId','sourceTaskId','sourceRevision','expectedRevision','projectId','projectName','items']);
 if(input.projectId!==undefined)uuid(input.projectId);if(input.projectName!==undefined&&(typeof input.projectName!=='string'||!input.projectName.trim()||input.projectName.length>256))throw new WorkspaceError('invalid_project_name',422);
 if(!input.projectId&&!input.projectName)throw new WorkspaceError('project_required',422);
 if(!Array.isArray(input.items)||input.items.length>100)throw new WorkspaceError('complete_checklist_mapping_required',422);
 const dummy='11111111-1111-4111-8111-111111111111',sourceTaskId=uuid(input.sourceTaskId),items=input.items.map(raw=>{const item=object(raw,['sourceItemId','mode','targetTaskId','desiredState','notes','gameDetails']);if(item.mode==='create'&&item.targetTaskId!==undefined)throw new WorkspaceError('server_target_identity_required',422);return {...item,mode:item.mode,targetTaskId:item.mode==='create'?crypto.randomUUID():item.targetTaskId};});
 // Each cloud step admits at most ten new captures; all remaining items stay explicit and resumable.
 if(items.filter(item=>item.mode==='create').length>10)throw new WorkspaceError('conversion_capture_step_limit',422);
 conversionCommand({id:dummy,workspaceId:dummy,expectedRevision:input.expectedRevision,conversionId:dummy,sourceTaskId,sourceRevision:input.sourceRevision,projectId:input.projectId??dummy,projectName:input.projectName??'Existing project',items});
 return {...input,requestId:uuid(input.requestId),sourceTaskId};
}
