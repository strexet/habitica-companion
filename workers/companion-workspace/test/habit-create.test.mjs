import {test} from 'node:test';import assert from 'node:assert/strict';import {readFile} from 'node:fs/promises';
import {workspaceCommand,commandHash} from '../../shared/workspace-commands.ts';import {emptyWorkspace} from '../../shared/workspace-domain.ts';import {prepareTodoProjection} from '../../shared/todo-projection-policy.ts';import {accept} from '../src/commands.ts';import {projectionStep,confirmMapping} from '../src/projection-adapter.ts';import {remoteTask,remotePatch} from '../../shared/projection.ts';
const fixture=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/habit-commands-v9.json',import.meta.url),'utf8'));
const now='2026-10-21T12:00:00Z',userId='00000000-0000-4000-8000-000000000011';
for(const row of fixture.cases)test(`Habit directions ${row.up}/${row.down} preserve portable identity and create without scoring`,async()=>{
 const command=workspaceCommand(row.command);assert.equal(await commandHash(command),row.payloadHash);
 const d=emptyWorkspace(command.workspaceId),accepted=await accept(d,command,now);assert.deepEqual(accepted.entry.effects,['create']);assert.equal(accepted.document.habitScores.length,0);
 const step=projectionStep(accepted.document,accepted.entry,{task:null,checkToken:'fixture'});assert.equal(step.kind,'write');assert.equal(step.arguments.taskKind,'habit');assert.equal(step.arguments.patch.up,row.up);assert.equal(step.arguments.patch.down,row.down);assert.equal(step.arguments.patch.checklist,undefined);assert.equal(step.arguments.patch.date,undefined);
 const remote=await remoteTask({id:crypto.randomUUID(),userId,type:'habit',...step.arguments.patch,alias:step.arguments.selector,group:{},challenge:{}},userId);assert.equal(remote.completed,false);
 const linked=confirmMapping(accepted.document,command.taskId,remote);assert.equal(linked.projections[0].habiticaTaskId,remote.id);
 const correlated=projectionStep(accepted.document,{...accepted.entry,projectionState:'unknownOutcome'},{task:remote,checkToken:'new'});assert.equal(correlated.kind,'observedApplied');
 for(const direction of ['up','down']){
  const score=workspaceCommand({id:crypto.randomUUID(),workspaceId:d.id,expectedRevision:accepted.document.revision,operation:'scoreHabit',taskId:command.taskId,direction,count:2,projectToHabitica:true});
  if(row[direction])assert.equal((await accept(accepted.document,score,now)).document.habitScores.length,2);else await assert.rejects(accept(accepted.document,score,now),error=>error.code==='habit_direction_disabled');
 }
});
test('fresh Habit defaults are explicit and unsupported kind fields fail before dispatch',async()=>{
 const d=emptyWorkspace(),raw=workspaceCommand({id:crypto.randomUUID(),workspaceId:d.id,expectedRevision:1,operation:'createHabit',taskId:crypto.randomUUID(),patch:{text:'Defaults'}}),prepared=prepareTodoProjection(d,raw);
 assert.equal(prepared.projectToHabitica,true);assert.equal(prepared.patch.habiticaDetails.up,true);assert.equal(prepared.patch.habiticaDetails.down,true);
 for(const patch of [{...prepared.patch,deadline:'2026-10-21'},{...prepared.patch,habiticaDetails:{...prepared.patch.habiticaDetails,checklist:[]}}])await assert.rejects(accept(d,{...prepared,patch},now),error=>error.code==='unsupported_habit_fields');
 for(const patch of [{text:'Habit',date:null},{text:'Habit',checklist:[]},{text:'Habit',up:'false'}])assert.throws(()=>remotePatch(patch,true,'habit'));
 assert.throws(()=>remotePatch({text:'Todo',up:true},true,'todo'));
});
test('an unchanged legacy Habit can acquire its counterpart without a fake edit or score',async()=>{
 const d=emptyWorkspace(),taskId=crypto.randomUUID();
 const legacy=await accept(d,workspaceCommand({id:crypto.randomUUID(),workspaceId:d.id,expectedRevision:1,operation:'createHabit',taskId,patch:{text:'Legacy Habit'}}),now);
 const repair=await accept(legacy.document,workspaceCommand({id:crypto.randomUUID(),workspaceId:d.id,expectedRevision:2,operation:'ensureProjection',taskId,projectToHabitica:true}),now);
 assert.deepEqual(repair.entry.effects,['create']);assert.equal(repair.document.tasks.length,1);assert.equal(repair.document.habitScores.length,0);
 const step=projectionStep(repair.document,repair.entry,{task:null,checkToken:'fixture'});assert.equal(step.arguments.taskKind,'habit');assert.equal(step.arguments.patch.text,'Legacy Habit');assert.equal(step.arguments.patch.up,true);assert.equal(step.arguments.patch.down,true);
});
