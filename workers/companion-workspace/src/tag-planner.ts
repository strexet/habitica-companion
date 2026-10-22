import {workspaceDocument,type WorkspaceDocument} from '../../shared/workspace-domain.ts';
import {desiredTagMembership,type DesiredTagGroup} from '../../shared/tag-membership.ts';
import {tagAlias,type TagMapping,type TagIntent,type TagMember} from '../../shared/tag-projection.ts';
import {workspaceTokenHash} from '../../shared/workspace-http.ts';
import {tagAuthorityGap,type TagAuthority} from './tag-authority.ts';
export interface TagWorkPlan {manualReview?:boolean;id:string;intentId:string;commandId:string;sourceRevision:number;desiredHash:string;context:{workspaceId:string;entityId:string;kind:DesiredTagGroup['kind']};accountId:string;consentEpoch:string;origin:TagAuthority;operation:'ensureTag'|'setMembership';mappingId:string;plannedTagId:string;name:string;members:TagMember[];state:'pending'|'needsReview';error:string|null}
export interface PlannedTags {document:WorkspaceDocument;work:TagWorkPlan[];gap:string|null}
const bytes=(value:unknown)=>new TextEncoder().encode(JSON.stringify(value)).length;
export async function planTags(source:WorkspaceDocument,authority:TagAuthority,commandId:string,beforeSource?:WorkspaceDocument,entityId?:string):Promise<PlannedTags>{
 const document=workspaceDocument(source),desired=desiredTagMembership(document),prior=document.tagLedger;
 if(prior&&prior.accountId!==authority.accountId)return {document,work:[],gap:'tag_account_mismatch'};
 const ledger=structuredClone(prior??{accountId:authority.accountId,mappings:[],intents:[]}),work:TagWorkPlan[]=[],gap=tagAuthorityGap(authority);
 const taskMappings=new Map(document.projections.map(row=>[row.taskId,row]));
 const previousGroups=beforeSource?new Map(desiredTagMembership(beforeSource).groups.map(group=>[group.entityId,group])):null;
 const signature=(group:DesiredTagGroup)=>JSON.stringify({kind:group.kind,name:group.name,archived:group.archived,removed:group.removed,taskIds:group.taskIds,retainMembership:group.retainMembership});
 for(const group of desired.groups){
  if(entityId!==undefined&&group.entityId!==entityId)continue;
  const previous=previousGroups?.get(group.entityId);if(previousGroups&&previous&&signature(previous)===signature(group))continue;
  let mapping=ledger.mappings.find(row=>row.entityId===group.entityId&&row.kind===group.kind);
  const uncertain=mapping?.state==='unknownOutcome'||ledger.intents.some(row=>row.mappingId===mapping?.id&&row.state==='unknownOutcome');
  if(uncertain)continue; // Original uncertain intent survives; it is recovered before any replacement work.
  const history=ledger.intents.filter(row=>row.mappingId===mapping?.id&&row.operation==='setMembership').sort((a,b)=>b.desiredRevision-a.desiredRevision||b.revision-a.revision);
  const before=new Map<string,TagMember>();for(const intent of history)for(const member of intent.members??[])if(member.state==='confirmed'&&!before.has(member.taskId))before.set(member.taskId,member);
  if(!mapping){mapping={id:crypto.randomUUID(),revision:document.revision,isArchived:group.archived,entityId:group.entityId,kind:group.kind,state:'needsReview',desiredRevision:document.revision,correlationId:commandId,correlationAlias:tagAlias(document.id,group.kind,group.entityId),desiredName:group.name,habiticaTagId:null,evidence:null};ledger.mappings.push(mapping);}
  const identityCurrent=mapping.state==='confirmed'&&mapping.desiredName===group.name&&mapping.habiticaTagId!==null;
  Object.assign(mapping,{revision:document.revision,isArchived:group.archived,desiredRevision:document.revision,desiredName:group.name,correlationId:commandId});
  if(group.retainMembership){if(!identityCurrent)mapping.state='needsReview';continue;}
  const desiredIds=new Set(group.taskIds),ids=[...group.taskIds,...[...before.keys()].filter(id=>!desiredIds.has(id))];
  const members:TagMember[]=ids.map(taskId=>{
   const remote=taskMappings.get(taskId),present=desiredIds.has(taskId),old=before.get(taskId),remoteId=remote?.habiticaTaskId??old?.habiticaTaskId??null;
   // A changed group requires fresh observations for its complete selection.
   // Retained receipts stay in older intents; they are never presented as this reconciliation.
   if(old&&old.present===present&&old.habiticaTaskId===remoteId)return {...old,state:gap?'needsReview':'pending',evidence:null};
   return {taskId,habiticaTaskId:remoteId,present,state:gap?'needsReview':'pending',evidence:null};
  });
  const scopeGap=gap??(group.removed&&!mapping.habiticaTagId?'retained_tag_mapping_requires_review':null);
  const plannedTagId=mapping.habiticaTagId??crypto.randomUUID(),context={workspaceId:document.id,entityId:group.entityId,kind:group.kind};
  async function add(operation:'ensureTag'|'setMembership'){
   const intent:TagIntent={id:crypto.randomUUID(),revision:document.revision,isArchived:group.archived,mappingId:mapping!.id,commandId,correlationId:crypto.randomUUID(),desiredRevision:document.revision,operation,state:scopeGap?'needsReview':'pending',habiticaTagId:operation==='ensureTag'?mapping!.habiticaTagId:plannedTagId,desiredName:operation==='ensureTag'?group.name:null,members:operation==='setMembership'?members:null,evidence:null};
   ledger.intents.push(intent);
   if(intent.state!=='confirmed')work.push({id:crypto.randomUUID(),intentId:intent.id,commandId,sourceRevision:document.revision,desiredHash:await workspaceTokenHash(JSON.stringify({context,name:group.name,members:group.taskIds,removed:group.removed})),context,accountId:authority.accountId,consentEpoch:authority.consentEpoch??'',origin:authority,operation,mappingId:mapping!.id,plannedTagId,name:group.name,members:operation==='setMembership'?members:[],state:scopeGap?'needsReview':'pending',error:scopeGap});
  }
  if(!identityCurrent){mapping.state=scopeGap?'needsReview':'pending';await add('ensureTag');}
  await add('setMembership');
 }
 const next={...document,tagLedger:ledger};
 // Preserve all prior receipts and the canonical save when expanded membership cannot fit.
 if(bytes(ledger)>262144||bytes(next)>1024*1024)return {document,work:[],gap:'tag_ledger_capacity'};
 return {document:workspaceDocument(next),work,gap};
}
