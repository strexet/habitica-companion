import {test} from 'node:test';import assert from 'node:assert/strict';import {readFile} from 'node:fs/promises';
import {workspaceCommand,commandHash} from '../../shared/workspace-commands.ts';import {emptyWorkspace} from '../../shared/workspace-domain.ts';import {accept} from '../src/commands.ts';import {journal} from '../src/journal.ts';import {projectionStep,adoptProjection} from '../src/projection-adapter.ts';
const now='2026-10-21T12:00:00Z';
const fixture=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/projection-command-v1.json',import.meta.url),'utf8'));
test('projection-only identity is portable without rewriting older command encodings',async()=>assert.equal(await commandHash(workspaceCommand(fixture.command)),fixture.payloadHash));
test('projection-only acceptance queues one original create without content edit score or duplicate native task',async()=>{
 let d=emptyWorkspace(fixture.command.workspaceId);const create=workspaceCommand({...fixture.command,id:crypto.randomUUID(),operation:'createTodo',expectedRevision:1,projectToHabitica:false,patch:{text:'Unchanged',notes:'Keep notes',priority:'urgent'}});
 const first=await accept(d,create,now);d=first.document;const original=structuredClone(d.tasks[0]);const repair=await accept(d,workspaceCommand(fixture.command),now);
 assert.equal(repair.document.tasks.length,1);assert.deepEqual(repair.document.tasks[0],{...original,revision:3});assert.deepEqual(repair.entry.effects,['create']);assert.equal(repair.document.completions.length,0);assert.equal(repair.document.habitScores.length,0);
 await journal({document:repair.document,storeVersion:3,commands:[first.entry,repair.entry],events:[first.event,repair.event]});
 assert.equal(projectionStep(repair.document,repair.entry,{task:null,checkToken:'fixture'}).operation,'create');
 for(const change of [entry=>entry.effects.push('scoreUp'),entry=>entry.command.projectToHabitica=false]){const entry=structuredClone(repair.entry);change(entry);await assert.rejects(journal({document:repair.document,storeVersion:3,commands:[first.entry,entry],events:[first.event,repair.event]}));}
 for(const [state,kind,archived,code] of [['completed','todo',false,'historical_projection_requires_review'],['active','daily',false,'adoption_required'],['active','todo',true,'task_unavailable']]){
  const current={...d,tasks:[{...original,state,kind,isArchived:archived}]};
  if(state==='completed')current.completions=[{id:crypto.randomUUID(),revision:2,taskId:original.id,commandId:crypto.randomUUID(),completedAtUtc:now,occurrenceId:null,isArchived:false}];
  await assert.rejects(accept(current,workspaceCommand(fixture.command),now),error=>error.code===code);
 }
});
test('adoption requires matching kind completion and exact fingerprint without changing retained content',()=>{
 const task={id:crypto.randomUUID(),revision:1,text:'Historical',kind:'todo',state:'completed',priority:'normal',createdAtUtc:now,updatedAtUtc:now,notes:null,categoryId:null,projectId:null,sectionId:null,parentTaskId:null,labelIds:[],dependsOn:[],plannedDate:null,deadline:null,reviewAfterUtc:null,staleSinceUtc:null,recurrence:null,habiticaDetails:null,isArchived:true};
 const d={...emptyWorkspace(),tasks:[task],completions:[{id:crypto.randomUUID(),revision:1,taskId:task.id,commandId:crypto.randomUUID(),completedAtUtc:now,occurrenceId:null,isArchived:false}]},remote={id:crypto.randomUUID(),kind:'todo',completed:true,fingerprint:'a'.repeat(64)};
 const linked=adoptProjection(d,task.id,remote,remote.fingerprint);assert.deepEqual(linked.tasks,d.tasks);assert.deepEqual(linked.completions,d.completions);
 for(const changed of [{...remote,completed:false},{...remote,kind:'daily'},{...remote,fingerprint:'b'.repeat(64)}])assert.throws(()=>adoptProjection(d,task.id,changed,remote.fingerprint));
 const collision={...d,projections:[{id:crypto.randomUUID(),revision:1,taskId:crypto.randomUUID(),habiticaTaskId:remote.id,state:'linked',isArchived:false}]};assert.throws(()=>adoptProjection(collision,task.id,remote,remote.fingerprint));
});
