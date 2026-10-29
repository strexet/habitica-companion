import type {WorkspaceDocument,Entity} from './workspace-domain.ts';
import type {DesiredTagGroup} from './tag-membership.ts';
import type {TagMapping} from './tag-projection.ts';
// Stored receipt correspondence only. Current credentials, consent and fresh
// native observations remain independent requirements at the service boundary.
export function scopedTagCorrespondence(document:WorkspaceDocument,group:DesiredTagGroup,mapping:TagMapping|undefined){
 const collections={category:document.categories,project:document.projects,section:document.sections,label:document.labels,goal:document.goals,savedTaskView:document.savedTaskViews??[]};
 const entity=(collections[group.kind] as Entity[]).find(row=>row.id===group.entityId);
 const intents=document.tagLedger?.intents.filter(row=>row.mappingId===mapping?.id&&row.operation==='setMembership').sort((a,b)=>b.desiredRevision-a.desiredRevision||b.revision-a.revision)??[],intent=intents[0];
 const identityCurrent=Boolean(entity&&mapping&&mapping.desiredName===group.name&&mapping.isArchived===group.archived&&mapping.desiredRevision>=entity.revision&&mapping.habiticaTagId&&mapping.evidence);
 const desired=new Set(group.taskIds),present=intent?.members?.filter(member=>member.present)??[],projections=new Map(document.projections.map(row=>[row.taskId,row]));
 const exact=Boolean(identityCurrent&&intent&&intent.isArchived===group.archived&&intent.desiredRevision>=entity!.revision&&intent.desiredRevision===mapping!.desiredRevision&&!document.tagLedger?.intents.some(row=>row.mappingId===mapping!.id&&row.state==='unknownOutcome')&&intent.habiticaTagId===mapping!.habiticaTagId
  &&intents.filter(row=>row.desiredRevision===intent.desiredRevision&&row.revision===intent.revision).length===1&&present.length===desired.size
  &&present.every(member=>desired.has(member.taskId))&&intent.members?.every(member=>{const p=projections.get(member.taskId);return Boolean(member.habiticaTaskId&&p?.state==='linked'&&!p.isArchived&&p.habiticaTaskId===member.habiticaTaskId);}));
 const confirmed=Boolean(exact&&mapping?.state==='confirmed'&&intent?.state==='confirmed'&&intent.evidence&&intent.members?.every(member=>member.state==='confirmed'&&member.evidence));
 return {identityCurrent,exact,confirmed,intent};
}
