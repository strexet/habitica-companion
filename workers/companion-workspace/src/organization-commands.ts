import {journal,type Journal} from './journal.ts';
import {organizationCommand,organizationCommandHash,currentOrganizationSnapshot,applyOrganizationSnapshot,type OrganizationCommand,type OrganizationEntry} from '../../shared/workspace-organization.ts';
import {utcTicks} from '../../shared/workspace-domain.ts';
export type OrganizationDisposition='accepted'|'duplicate'|'revisionConflict'|'idempotencyConflict'|'invalid'|'unavailable';
export interface OrganizationSubmission {disposition:OrganizationDisposition;snapshot:Journal|null;entry:OrganizationEntry|null;errorCode:string|null;savedInCompanion:boolean|null}
const result=(disposition:OrganizationDisposition,snapshot:Journal|null,entry:OrganizationEntry|null=null,errorCode:string|null=null):OrganizationSubmission=>({disposition,snapshot,entry,errorCode,savedInCompanion:disposition==='unavailable'?null:disposition==='accepted'||disposition==='duplicate'});
export async function acceptOrganization(value:Journal,input:unknown,now:string):Promise<OrganizationSubmission>{return acceptOwnedOrganization(value,input,now,'local');}
// Only the authenticated Workspace binding selects cloud ownership; tool arguments cannot select an owner.
export async function acceptCloudOrganization(value:Journal,input:unknown,now:string):Promise<OrganizationSubmission>{return acceptOwnedOrganization(value,input,now,'cloud');}
async function acceptOwnedOrganization(value:Journal,input:unknown,now:string,owner:'local'|'cloud'):Promise<OrganizationSubmission>{
 let state:Journal,command:OrganizationCommand;
 try{state=await journal(value);}catch{return result('invalid',value,null,'invalid_workspace');}
 try{command=organizationCommand(input);if(command.workspaceId!==state.document.id||utcTicks(now)===utcTicks('0001-01-01T00:00:00Z'))throw new Error();}catch{return result('invalid',state,null,'invalid_command');}
 const document=state.document,payloadHash=await organizationCommandHash(command),existing=document.organizationHistory?.find(entry=>entry.command.id===command.id);
 if(existing)return existing.payloadHash===payloadHash?result('duplicate',state,existing):result('idempotencyConflict',state,null,'command_id_reused');
 if(state.commands.some(entry=>entry.command.id===command.id)||[...document.completions,...document.habitScores,...(document.restorations??[])].some(row=>row.commandId===command.id))return result('idempotencyConflict',state,null,'command_id_reused');
 if(owner==='cloud'?state.authority?.mode!=='cloud'||state.authority.cloudWorkspaceId!==document.id:state.authority?.mode==='frozen'||state.authority?.mode==='cloud')return result('invalid',state,null,'workspace_owned_elsewhere');
 if(command.expectedRevision!==document.revision)return result('revisionConflict',state,null,'stale_revision');
 if(document.revision>=Number.MAX_SAFE_INTEGER||state.storeVersion>=Number.MAX_SAFE_INTEGER)return result('invalid',state,null,'revision_exhausted');
 const current=currentOrganizationSnapshot(document,command.kind,command.entityId),patch=command.patch;
 if(command.operation==='create'?current!==null:current===null||current.isArchived)return result('invalid',state,null,'organization_unavailable');
 if(patch?.categoryId&&!document.categories.some(row=>row.id===patch.categoryId&&!row.isArchived)
    ||patch?.projectId&&!document.projects.some(row=>row.id===patch.projectId&&!row.isArchived)
    ||patch?.query&&(patch.query.filter.categoryId&&!document.categories.some(row=>row.id===patch.query!.filter.categoryId)||patch.query.filter.projectId&&!document.projects.some(row=>row.id===patch.query!.filter.projectId)||patch.query.filter.labelId&&!document.labels.some(row=>row.id===patch.query!.filter.labelId))
    ||patch?.taskIds?.some(id=>!document.tasks.some(task=>task.id===id&&!task.isArchived&&task.kind==='todo')))return result('invalid',state,null,'invalid_organization_reference');
 const accepted=applyOrganizationSnapshot(command,current),entry:OrganizationEntry={command,payloadHash,acceptedAtUtc:now,snapshot:accepted},next=structuredClone(state),d=next.document;
 const create=command.operation==='create';
 const replace=<T extends {id:string}>(rows:T[],row:T):T[]=>create?[...rows,row]:rows.map(value=>value.id===row.id?row:value);
 const base={id:accepted.id,revision:accepted.revision,name:accepted.name,isArchived:accepted.isArchived};
 switch(command.kind){
  case 'category':d.categories=replace(d.categories,base);break;
  case 'project':d.projects=replace(d.projects,{...base,categoryId:accepted.categoryId});break;
  case 'section':d.sections=replace(d.sections,{...base,projectId:accepted.projectId!,position:accepted.position});break;
  case 'label':d.labels=replace(d.labels,base);break;
  case 'savedTaskView':d.savedTaskViews=replace(d.savedTaskViews??[],{...base,query:accepted.query!});break;
  case 'goal':d.goals=replace(d.goals,{...base,taskIds:accepted.taskIds!});break;
 }
 d.revision=accepted.revision;d.organizationHistory=[...(d.organizationHistory??[]),entry];next.storeVersion++;
 try{return result('accepted',await journal(next),entry);}catch{return result('invalid',state,null,'invalid_result');}
}
export interface OrganizationStore {load(workspaceId:string):Promise<Journal|null>;tryCommit(expectedStoreVersion:number,snapshot:Journal):Promise<boolean>}
export async function submitOrganization(store:OrganizationStore,command:OrganizationCommand,now:string):Promise<OrganizationSubmission|null>{
 let current:Journal|null;
 try{current=await store.load(command.workspaceId);}catch{return result('unavailable',null,null,'storage_unavailable');}
 if(!current)return null;
 const accepted=await acceptOrganization(current,command,now);if(accepted.disposition!=='accepted')return accepted;
 try{
  if(await store.tryCommit(current.storeVersion,accepted.snapshot!))return accepted;
  const latest=await store.load(command.workspaceId);if(!latest)return result('revisionConflict',current,null,'workspace_unavailable');
  const recovered=await acceptOrganization(latest,command,now);
  return ['duplicate','idempotencyConflict','invalid'].includes(recovered.disposition)?recovered:result('revisionConflict',latest,null,'stale_revision');
 }catch{return result('unavailable',current,null,'acceptance_unknown');}
}
