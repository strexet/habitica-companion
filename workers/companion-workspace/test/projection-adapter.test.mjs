import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { projectionStep, adoptProjection, confirmMapping } from '../src/projection-adapter.ts';
import { workspaceDocument } from '../../shared/workspace-domain.ts';
import { remoteTask, projectionAlias } from '../../shared/projection.ts';
const userId='00000000-0000-4000-8000-000000000011';
const externalId='90000000-0000-4000-8000-000000000001';
async function setup() {
 const doc=workspaceDocument(JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/workspace-v1.json',import.meta.url),'utf8'))); const task=doc.tasks[0];
 const remote=await remoteTask({id:externalId,userId,type:'todo',text:task.text,notes:'',priority:1,attribute:'str',completed:false,checklist:[],group:{},challenge:{}},userId);
 const accepted={...task,revision:2,updatedAtUtc:'2026-10-15T01:00:00Z'};
 const entry={command:{id:crypto.randomUUID(),workspaceId:doc.id,expectedRevision:1,operation:'completeTask',taskId:task.id,patch:null,direction:null,count:1,occurrenceDate:null,projectToHabitica:true},acceptedAtUtc:accepted.updatedAtUtc,taskSnapshot:accepted,projectionState:'pending',effects:['create','scoreUp'],confirmedEffects:0};
 return {doc,task,remote,entry};
}

test('explicit adoption retains independent Companion identity and verified remote fingerprint',async()=>{
 const {doc,task,remote}=await setup(); const adopted=adoptProjection(doc,task.id,remote,remote.fingerprint);
 assert.equal(adopted.tasks[0].id,task.id); assert.notEqual(adopted.tasks[0].id,externalId);
 assert.equal(adopted.projections[0].habiticaTaskId,externalId); assert.equal(adopted.projections[0].fingerprint,remote.fingerprint);
 assert.equal(adopted.tasks[0].text,doc.tasks[0].text); assert.equal(adopted.revision,2);
 assert.throws(()=>adoptProjection(doc,task.id,remote,'a'.repeat(64)));
});

test('create then complete preserves one canonical ID and uses separate ordered effects',async()=>{
 const {doc,task,remote,entry}=await setup(); doc.projections=[{id:crypto.randomUUID(),revision:1,taskId:task.id,state:'pending',habiticaTaskId:null,isArchived:false}];
 assert.equal(projectionStep(doc,entry).selector,projectionAlias(doc.id,task.id));
 const create=projectionStep(doc,entry,{task:null,checkToken:'fixture'}); assert.equal(create.kind,'write'); assert.equal(create.operation,'create');
 const created={...remote,alias:projectionAlias(doc.id,task.id)}; const linked=confirmMapping(doc,task.id,created);
 assert.equal(linked.projections[0].id,doc.projections[0].id); assert.equal(linked.tasks[0].id,task.id);
 const score=projectionStep(linked,{...entry,confirmedEffects:1},{task:created,checkToken:'fresh'}); assert.equal(score.kind,'write'); assert.equal(score.operation,'score'); assert.equal(score.arguments.selector,externalId);
});

test('verified external deletion and edits require review and never recreate a mapped task',async()=>{
 const {doc,task,remote,entry}=await setup(); const linked=adoptProjection(doc,task.id,remote,remote.fingerprint);
 assert.deepEqual(projectionStep(linked,entry,{task:null,checkToken:'fresh'}),{kind:'blocked',code:'external_deleted'});
 const changed={...remote,fingerprint:'f'.repeat(64)};
 assert.equal(projectionStep(linked,{...entry,confirmedEffects:1},{task:changed,checkToken:'fresh'}).code,'external_edit_review_required');
 assert.equal(projectionStep(linked,{...entry,confirmedEffects:1},{task:{...remote,completed:true},checkToken:'fresh'}).code,'external_completion_or_direction_review');
});

test('unknown creates need positive correlation evidence; uncertain scores remain blocked',async()=>{
 const {doc,task,remote,entry}=await setup(); doc.projections=[{id:crypto.randomUUID(),revision:1,taskId:task.id,state:'unknownOutcome',habiticaTaskId:null,isArchived:false}];
 const unknown={...entry,projectionState:'unknownOutcome'};
 assert.equal(projectionStep(doc,unknown,{task:null,checkToken:'fresh'}).kind,'blocked');
 assert.equal(projectionStep(doc,unknown,{task:{...remote,alias:projectionAlias(doc.id,task.id)},checkToken:'fresh'}).kind,'observedApplied');
 const linked=confirmMapping(doc,task.id,remote);
 assert.equal(projectionStep(linked,{...unknown,confirmedEffects:1},{task:{...remote,completed:true},checkToken:'fresh'}).kind,'blocked');
});

test('projection uses the accepted task snapshot and requires historical intent when missing',async()=>{
 const {doc,task,entry}=await setup(); doc.projections=[{id:crypto.randomUUID(),revision:1,taskId:task.id,state:'pending',habiticaTaskId:null,isArchived:false}];
 doc.tasks[0]={...task,text:'A later edit'};
 assert.equal(projectionStep(doc,entry,{task:null,checkToken:'fresh'}).arguments.patch.text,task.text);
 assert.equal(projectionStep(doc,{...entry,taskSnapshot:null}).code,'accepted_task_snapshot_required');
});
