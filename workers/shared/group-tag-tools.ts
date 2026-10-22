import {workspaceObject as object,uuid,WorkspaceError} from './workspace-http.ts';
export const groupTagScopes={get_group_tag_status:'tasks:read',inspect_group_tag_candidates:'habitica.tags.write',prepare_group_tag_command:'habitica.tags.write',accept_group_tag_command:'habitica.tags.write',get_group_tag_command:'tasks:read',recover_group_tag_receipt:'tasks:read'} as const;
export type GroupTagTool=keyof typeof groupTagScopes;
const kinds=['category','project','section','label','goal','savedTaskView'];
export function groupTagInput(name:string,value:unknown):Record<string,unknown>{
 if(!Object.hasOwn(groupTagScopes,name))throw new WorkspaceError('invalid_tool',422);
 const input=object(value,name==='get_group_tag_status'?['kind','entityId','limit','offset','expectedRevision']:name==='inspect_group_tag_candidates'?['kind','entityId','limit','offset','expectedFingerprint']:name==='prepare_group_tag_command'?['requestId','operation','kind','entityId','habiticaTagId','workId']:name==='recover_group_tag_receipt'?['workId']:['commandId']);
 for(const field of ['entityId','habiticaTagId','requestId','commandId','workId'])if(input[field]!==undefined)uuid(input[field]);
 if(input.kind!==undefined&&!kinds.includes(String(input.kind)))throw new WorkspaceError('invalid_group_kind',422);
 for(const [field,min,max] of [['limit',1,100],['offset',0,100000],['expectedRevision',1,Number.MAX_SAFE_INTEGER]] as const)if(input[field]!==undefined&&(!Number.isSafeInteger(input[field])||Number(input[field])<min||Number(input[field])>max))throw new WorkspaceError('invalid_pagination',422);
 if(input.expectedFingerprint!==undefined&&(typeof input.expectedFingerprint!=='string'||!/^[a-f0-9]{64}$/.test(input.expectedFingerprint)))throw new WorkspaceError('invalid_precondition',422);
 if(name==='prepare_group_tag_command'){
  uuid(input.requestId);if(!['reconcile','adopt','recover'].includes(String(input.operation)))throw new WorkspaceError('invalid_operation',422);
  if(input.operation==='recover'){uuid(input.workId);if(input.kind!==undefined||input.entityId!==undefined||input.habiticaTagId!==undefined)throw new WorkspaceError('invalid_request',422);}
  else {uuid(input.entityId);if(!kinds.includes(String(input.kind))||input.workId!==undefined||input.operation==='reconcile'&&input.habiticaTagId!==undefined)throw new WorkspaceError('invalid_request',422);if(input.operation==='adopt')uuid(input.habiticaTagId);}
 }
 if(name==='inspect_group_tag_candidates'&&(!kinds.includes(String(input.kind))||input.entityId===undefined))throw new WorkspaceError('invalid_group',422);
 if(name==='recover_group_tag_receipt')uuid(input.workId);
 if(name==='accept_group_tag_command'||name==='get_group_tag_command')uuid(input.commandId);
 return input;
}
