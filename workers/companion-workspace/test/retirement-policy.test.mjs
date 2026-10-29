import test from 'node:test';import assert from 'node:assert/strict';import {readFile} from 'node:fs/promises';
import {inspectRetirement} from '../src/retirement-policy.ts';
const fixture=async()=>JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/conversion-retirement-prerequisites.json',import.meta.url),'utf8'));
test('stored retirement prerequisites match C# context identity without granting deletion or changing history',async()=>{
 const x=await fixture(),before=JSON.stringify(x.state),result=await inspectRetirement(x.state,x.conversionId,x.accountId);
 assert.equal(result.canPrepareNativeReview,true,JSON.stringify(result.blockers));assert.equal(result.requiresFreshNativeVerification,true);assert.equal(result.canDelete,false);assert.equal(result.plan.contextHash,x.contextHash);assert.equal(result.plan.targets.length,10);assert.equal(result.plan.targets.filter(row=>row.desiredState==='completedArchived').length,2);assert.equal(JSON.stringify(x.state),before);
});
test('missing mappings, changed content or placement and incomplete tag membership refuse native review',async()=>{
 const x=await fixture(),target=x.state.document.checklistConversions[0].items[0].targetTaskId;
 for(const state of ['pending','unknownOutcome','externalConflict']){const changed=structuredClone(x.state);changed.document.projections.find(row=>row.taskId===target).state=state;const result=await inspectRetirement(changed,x.conversionId,x.accountId);assert.equal(result.canPrepareNativeReview,false);assert.ok(result.blockers.includes('target_mapping_required'));}
 let changed=structuredClone(x.state);changed.document.tasks.find(row=>row.id===changed.document.checklistConversions[0].sourceTaskId).notes='Changed content without a revision bump';assert.ok((await inspectRetirement(changed,x.conversionId,x.accountId)).blockers.includes('conversion_source_changed'));
 changed=structuredClone(x.state);changed.document.tasks.find(row=>row.id===target).projectId=null;assert.ok((await inspectRetirement(changed,x.conversionId,x.accountId)).blockers.includes('conversion_target_placement_changed'));
 changed=structuredClone(x.state);changed.document.tagLedger.intents[0].members.shift();assert.ok((await inspectRetirement(changed,x.conversionId,x.accountId)).blockers.includes('project_tag_membership_requires_review'));
 assert.ok((await inspectRetirement(x.state,x.conversionId,crypto.randomUUID())).blockers.includes('retirement_tag_account_unverified'));
 changed=structuredClone(x.state);changed.document.tasks.find(row=>row.id===target).dependsOn=[changed.document.checklistConversions[0].sourceTaskId];assert.ok((await inspectRetirement(changed,x.conversionId,x.accountId)).blockers.includes('source_relationships_require_review'));
 assert.ok((await inspectRetirement(x.state,x.conversionId,null)).blockers.includes('retirement_account_unverified'));
});
test('scoped receipt evidence survives unrelated revisions and stays conservative for every member',async()=>{
 const {groupTagStatus}=await import('../../shared/group-tag-status.ts');const x=await fixture(),base=x.state.document,target=base.checklistConversions[0].items[0].targetTaskId;
 const changed=structuredClone(x.state);changed.document.revision++;changed.storeVersion++;const task=changed.document.tasks.find(row=>row.id===target);task.text='Independent metadata';task.priority='urgent';task.deadline='2030-12-31';task.revision=changed.document.revision;
 changed.document.projections.find(row=>row.taskId===target).revision=changed.document.revision;
 let result=await inspectRetirement(changed,x.conversionId,x.accountId);assert.equal(result.canPrepareNativeReview,true,JSON.stringify(result.blockers));assert.equal(result.requiresFreshNativeVerification,true);assert.equal(result.canDelete,false);
 assert.equal(groupTagStatus(changed.document,{accountId:x.accountId,consentEnabled:true,scopeAllowed:true}).groups[0].membershipState,'confirmed');
 const completed=structuredClone(changed);completed.document.tasks.find(row=>row.id===target).state='completed';completed.document.completions.push({id:crypto.randomUUID(),revision:completed.document.revision,taskId:target,commandId:crypto.randomUUID(),completedAtUtc:new Date().toISOString(),occurrenceId:null,isArchived:false});
 assert.equal(groupTagStatus(completed.document,{accountId:x.accountId,consentEnabled:true,scopeAllowed:true}).groups[0].membershipState,'confirmed');
 assert.equal((await inspectRetirement(completed,x.conversionId,x.accountId)).canPrepareNativeReview,false); // Replacement lifecycle still blocks deletion.
 const before=JSON.stringify(changed);
 for(const mutate of [d=>d.projects[0].name+=' changed',d=>{d.projects[0].isArchived=true;d.projects[0].revision=d.revision;},d=>{d.projects[0].isRemoved=true;d.projects[0].revision=d.revision;},d=>d.tasks.find(t=>t.id===target).projectId=null,d=>d.projections.find(p=>p.taskId===target).habiticaTaskId=crypto.randomUUID(),d=>d.tagLedger.intents[0].members.shift(),d=>{const intent=structuredClone(d.tagLedger.intents[0]);intent.id=crypto.randomUUID();intent.commandId=crypto.randomUUID();d.tagLedger.intents.push(intent);},d=>{const i=d.tagLedger.intents[0];i.state='unknownOutcome';i.members[0].state='unknownOutcome';},d=>d.tagLedger.accountId=crypto.randomUUID()]){
  const bad=structuredClone(changed);mutate(bad.document);assert.equal((await inspectRetirement(bad,x.conversionId,x.accountId)).canPrepareNativeReview,false);try{const status=groupTagStatus(bad.document,{accountId:x.accountId,consentEnabled:true,scopeAllowed:true});assert.ok(status.groups.every(g=>g.membershipState!=='confirmed'&&g.membershipState!=='retained'));}catch(error){assert.ok(['invalid_workspace','invalid_tag_ledger'].includes(error.code));}
 }
 assert.equal(JSON.stringify(changed),before);
});
