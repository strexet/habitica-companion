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
