import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {workspaceDocument} from '../../shared/workspace-domain.ts';
import {organizationCommand,organizationCommandHash,projectRemovalFrom} from '../../shared/workspace-organization.ts';
import {queryOrganization} from '../../shared/organization-query.ts';
import {queryTasks} from '../../shared/task-query.ts';
import {acceptOrganization} from '../src/organization-commands.ts';
import {journal} from '../src/journal.ts';
const neutral=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/project-removal-v3.json',import.meta.url),'utf8'));
const now='2026-10-20T12:00:00Z';
const initial=()=>({document:workspaceDocument(structuredClone(neutral.document)),storeVersion:10,commands:[],events:[]});

test('portable v3 approvals bind exact task and section references while legacy defaults remain absent',async()=>{
 for(const row of neutral.hashes){const command=organizationCommand(row.command);assert.equal(await organizationCommandHash(command),row.payloadHash);}
 const state=initial();assert.equal(Object.hasOwn(state.document.projects[0],'isRemoved'),false);
 assert.equal(Object.hasOwn(organizationCommand({...neutral.hashes[0].command,operation:'archive',patch:null}).patch??{},'removal'),false);
});
test('logical removal retains tasks hierarchy sections activity and uncertain mappings with an original idempotent receipt',async()=>{
 for(const row of neutral.hashes){
  const before=initial(),command=organizationCommand(row.command),impact=queryOrganization(before.document,'get_project_removal_impact',{projectId:command.entityId,expectedRevision:10});
  assert.equal(impact.canRemove,true);assert.deepEqual(impact.removal,command.patch.removal);
  const result=await acceptOrganization(before,command,now);assert.equal(result.disposition,'accepted',result.errorCode);const state=result.snapshot;
  for(const key of ['tasks','sections','completions','occurrences','projections','habitScores','restorations'])assert.deepEqual(state.document[key],before.document[key],key);
  assert.deepEqual(state.commands,before.commands);assert.deepEqual(state.events,before.events);
  const removed=state.document.projects.find(project=>project.id===command.entityId);assert.equal(removed.isRemoved,true);assert.equal(removed.isArchived,true);
  assert.equal(queryOrganization(state.document,'get_project_removal_impact',{projectId:command.entityId}).canRemove,false);
  assert.equal(queryOrganization(state.document,'list_projects',{}).items.some(project=>project.id===command.entityId),false);
  assert.deepEqual(queryOrganization(state.document,'list_project_sections',{projectId:command.entityId}).items,[]);
  await journal(JSON.parse(JSON.stringify(state)));
  const duplicate=await acceptOrganization(state,command,'2026-10-21T12:00:00Z');assert.equal(duplicate.disposition,'duplicate');assert.deepEqual(duplicate.entry,result.entry);
  assert.equal((await acceptOrganization(state,{...command,id:crypto.randomUUID(),expectedRevision:11},now)).errorCode,'organization_unavailable');
 }
});
test('removed project references remain readable while inherited category membership no longer implies active placement',async()=>{
 const before=initial(),command=neutral.hashes[0].command,task=before.document.tasks[0],project=before.document.projects[0];
 assert.equal(queryTasks(before.document,{categoryId:project.categoryId}).tasks.some(row=>row.id===task.id),true);
 const state=(await acceptOrganization(before,command,now)).snapshot;
 assert.equal(queryTasks(state.document,{categoryId:project.categoryId}).tasks.some(row=>row.id===task.id),false);
 assert.equal(queryTasks(state.document,{projectId:project.id}).tasks.some(row=>row.id===task.id),true);
 assert.equal(state.document.tasks[0].projectId,project.id);
});
test('missing changed mixed reordered or stale removal approvals and tampered tombstones are refused',async()=>{
 const state=initial(),command=neutral.hashes[0].command,removal=command.patch.removal;
 const invalid=[{...command,patch:null},{...command,kind:'category'},{...command,patch:{name:'Mixed',removal}},
  {...command,patch:{removal:{...removal,taskIds:[]}}},{...command,patch:{removal:{...removal,taskIds:[...removal.taskIds].reverse()}}},
  {...command,patch:{removal:{...removal,sectionIds:[]}}},{...command,patch:{removal:{...removal,taskIds:[crypto.randomUUID()]}}}];
 for(const value of invalid)assert.equal((await acceptOrganization(state,value,now)).disposition,'invalid');
 assert.equal((await acceptOrganization(state,{...command,expectedRevision:9},now)).disposition,'revisionConflict');
 const accepted=(await acceptOrganization(state,command,now)).snapshot;
 for(const mutate of [copy=>{delete copy.document.organizationHistory;},copy=>{copy.document.projects[0].isRemoved=false;},copy=>{copy.document.organizationHistory[0].command.patch.removal.taskIds=[];}]){
  const copy=structuredClone(accepted);mutate(copy);await assert.rejects(()=>journal(copy));
 }
 assert.throws(()=>queryOrganization(state.document,'get_project_removal_impact',{projectId:command.entityId,expectedRevision:9}),/stale_revision/);
 assert.throws(()=>queryOrganization(state.document,'get_project_removal_impact',{projectId:crypto.randomUUID()}),/project_unavailable/);
 assert.deepEqual(projectRemovalFrom(state.document,command.entityId),removal);
});
