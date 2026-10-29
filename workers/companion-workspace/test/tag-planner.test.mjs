import test from 'node:test';
import assert from 'node:assert/strict';
import {workspaceDocument,emptyWorkspace} from '../../shared/workspace-domain.ts';
import {planTags} from '../src/tag-planner.ts';
const group=()=>({id:crypto.randomUUID(),revision:1,isArchived:false,name:'Synthetic project',categoryId:null});
const authority=()=>({accountId:crypto.randomUUID(),connectionId:crypto.randomUUID(),scopeAllowed:true,consentEnabled:true,consentEpoch:crypto.randomUUID(),source:'mcp',grantEpoch:'synthetic-epoch'});
const seed=()=>workspaceDocument({...emptyWorkspace(),projects:[group()]});
test('fresh scoped intent queues native metadata without tasks or scoring while missing scope/consent remain explicit review work',async()=>{
 const document=seed(),owner=authority(),before=JSON.stringify(document);
 for(const denied of [{}, {scopeAllowed:false},{consentEnabled:false,consentEpoch:null}]){
  const result=await planTags(document,{...owner,...denied},crypto.randomUUID());assert.equal(result.work.length,2);assert.equal(result.document.tasks.length,0);assert.equal(result.document.completions.length,0);assert.equal(result.document.habitScores.length,0);
  assert.ok(result.work.every(row=>row.state===(Object.keys(denied).length?'needsReview':'pending')));assert.equal(result.document.tagLedger.accountId,owner.accountId);
 }
 assert.equal(JSON.stringify(document),before);
});
test('ordinary changes never reinterpret old zero-effect organization history or unrelated groups as tag authorization',async()=>{
 const document=seed(),result=await planTags({...document,revision:2},authority(),crypto.randomUUID(),document);assert.deepEqual(result.work,[]);assert.deepEqual(result.document.tagLedger.intents,[]);
 const newGroup=group(),added=workspaceDocument({...document,revision:2,projects:[...document.projects,newGroup]});const change=await planTags(added,authority(),crypto.randomUUID(),document);assert.equal(change.work.length,2);assert.ok(change.work.every(row=>row.context.entityId===newGroup.id));
});
test('foreign portable tag identity cannot grant current-account work',async()=>{
 const document=seed(),old=await planTags(document,authority(),crypto.randomUUID()),result=await planTags(old.document,authority(),crypto.randomUUID());assert.equal(result.gap,'tag_account_mismatch');assert.deepEqual(result.work,[]);assert.deepEqual(result.document,old.document);
});
