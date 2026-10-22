import {workspaceDocument,type WorkspaceDocument} from './workspace-domain.ts';
import {desiredTagMembership} from './tag-membership.ts';
import {WorkspaceError} from './workspace-http.ts';
export interface TagStatusAuthority {accountId:string;scopeAllowed:boolean;consentEnabled:boolean}
export function groupTagStatus(source:WorkspaceDocument,authority:TagStatusAuthority|null,input:Record<string,unknown>={}){
 const document=workspaceDocument(source);if(input.expectedRevision!==undefined&&input.expectedRevision!==document.revision)throw new WorkspaceError('stale_revision',409);
 const accountVerified=Boolean(authority&&(!document.tagLedger||document.tagLedger.accountId===authority.accountId));
 const ledger=accountVerified?document.tagLedger:null,projections=new Map(document.projections.map(row=>[row.taskId,row]));
 const tasks=document.tasks.map(task=>{const mapping=projections.get(task.id),state=mapping?.state==='linked'&&mapping.habiticaTaskId?task.isArchived||task.state==='completed'?'retained':'linked':mapping?.state==='pending'?'pending':!mapping||mapping.state==='unlinked'?'missing':'needsReview';return {taskId:task.id,kind:task.kind,archived:task.isArchived,state};});
 const groups=desiredTagMembership(document).groups.map(group=>{
  const mapping=ledger?.mappings.find(row=>row.kind===group.kind&&row.entityId===group.entityId),intents=ledger?.intents.filter(row=>row.mappingId===mapping?.id&&row.operation==='setMembership').sort((a,b)=>b.desiredRevision-a.desiredRevision||b.revision-a.revision)??[],latest=intents[0];
  const identity=accountVerified?mapping?.state==='confirmed'&&mapping.desiredName===group.name?group.archived?'retained':'linked':mapping?.state==='pending'?'pending':mapping?'needsReview':'missing':'needsReview';
  let membership='missing',reason:string|null=null;
  if(!accountVerified){membership='needsReview';reason='tag_account_unverified';}
  else if(latest){
   const present=latest.members?.filter(member=>member.present)??[],desired=new Set(group.taskIds);
   const exact=present.length===desired.size&&present.every(member=>desired.has(member.taskId)&&member.habiticaTaskId&&projections.get(member.taskId)?.habiticaTaskId===member.habiticaTaskId);
   if(latest.desiredRevision!==document.revision||intents.filter(row=>row.desiredRevision===latest.desiredRevision&&row.revision===latest.revision).length!==1||latest.habiticaTagId!==mapping?.habiticaTagId||!exact){membership='needsReview';reason='tag_membership_requires_review';}
   else membership=latest.state==='confirmed'&&['linked','retained'].includes(identity)?group.archived?'retained':'confirmed':latest.state==='pending'?'pending':'needsReview';
  }
  return {entityId:group.entityId,kind:group.kind,name:group.name,archived:group.archived,removed:group.removed,identityState:identity,membershipState:membership,habiticaTagId:accountVerified?mapping?.habiticaTagId??null:null,desiredTaskCount:group.taskIds.length,confirmedMemberCount:latest?.members?.filter(member=>member.state==='confirmed').length??0,reason};
 });
 const matching=groups.filter(group=>(input.kind===undefined||group.kind===input.kind)&&(input.entityId===undefined||group.entityId===input.entityId)),offset=Number(input.offset??0),limit=Number(input.limit??20),next=offset+limit<matching.length?offset+limit:null;
 return {provenance:'stored-canonical-evidence',workspaceId:document.id,revision:document.revision,accountVerified,tagConsentEnabled:authority?.consentEnabled===true,tagScopeAllowed:authority?.scopeAllowed===true,integrationComplete:accountVerified&&authority?.consentEnabled===true&&authority.scopeAllowed&&groups.every(group=>['confirmed','retained'].includes(group.membershipState))&&tasks.every(task=>['linked','retained'].includes(task.state)),
  taskCoverage:{total:tasks.length,linked:tasks.filter(task=>['linked','retained'].includes(task.state)).length,pending:tasks.filter(task=>task.state==='pending').length,missing:tasks.filter(task=>task.state==='missing').length,needsReview:tasks.filter(task=>task.state==='needsReview').length,archived:tasks.filter(task=>task.archived).length},
  groupCoverage:{total:groups.length,confirmed:groups.filter(group=>['confirmed','retained'].includes(group.membershipState)).length,pending:groups.filter(group=>group.membershipState==='pending').length,needsReview:groups.filter(group=>group.membershipState==='needsReview').length,missing:groups.filter(group=>group.membershipState==='missing').length},
  groups:matching.slice(offset,offset+limit),matchedCount:matching.length,nextOffset:next};
}
