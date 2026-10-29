import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {emptyWorkspace,workspaceDocument} from '../../shared/workspace-domain.ts';
import {workspaceCommand} from '../../shared/workspace-commands.ts';
import {accept} from '../src/commands.ts';
import {journal} from '../src/journal.ts';
import {readActivityCoverage} from '../src/activity-coverage-query.ts';
const time=h=>`2026-10-27T${String(h).padStart(2,'0')}:00:00Z`;
const rule={frequency:'daily',interval:1,startDate:'2026-10-27',timeZoneId:'UTC',weekdays:[],daysOfMonth:[]};
async function fixture(kind='daily'){
 const state={document:emptyWorkspace(),storeVersion:1,commands:[],events:[]},taskId=crypto.randomUUID();
 async function apply(operation,h,extra={}){const command=workspaceCommand({id:crypto.randomUUID(),workspaceId:state.document.id,expectedRevision:state.document.revision,taskId,operation,...extra});const r=await accept(state.document,command,time(h),state.commands);state.document=r.document;state.storeVersion=r.document.revision;state.commands.push(r.entry);state.events.push(r.event);await journal(state);return r;}
 await apply(kind==='daily'?'createDaily':kind==='habit'?'createHabit':'createTodo',0,{patch:{text:'Synthetic coverage',...(kind==='daily'?{recurrence:rule}:{})}});
 return {state,taskId,apply,read:(from=0,through=3)=>readActivityCoverage(state,taskId,time(from),time(through))};
}
test('complete task intervals keep effective schedule, timezone, pause and resume',async()=>{
 const f=await fixture();await f.apply('updateTask',1,{patch:{recurrence:{...rule,timeZoneId:'Europe/Prague'}}});await f.apply('pauseTask',2);await f.apply('resumeTask',3);await f.apply('updateTask',4,{patch:{text:'observed'}});
 const intervals=await f.read(0,4);assert.ok(intervals.every(i=>i.isKnown));assert.deepEqual(intervals.map(i=>[i.schedule.state,i.schedule.recurrence.timeZoneId]),[['active','UTC'],['active','Europe/Prague'],['paused','Europe/Prague'],['active','Europe/Prague']]);
 const original=JSON.stringify(f.state);const later=await f.read(0,5);assert.equal(later.at(-1).isKnown,false);assert.equal(JSON.stringify(f.state),original);assert.equal((await f.read(-0,4)).length,4);
 assert.deepEqual((await journal(JSON.parse(original))).document,f.state.document);assert.deepEqual(workspaceDocument(JSON.parse(JSON.stringify(f.state.document))),f.state.document);
});
for(const removeCommand of [false,true])test('missing witness/revision stays unknown '+removeCommand,async()=>{
 const f=await fixture();await f.apply('updateTask',1,{patch:{text:'middle'}});await f.apply('updateTask',2,{patch:{text:'last'}});await f.apply('updateTask',3,{patch:{text:'tail'}});
 const removed=f.state.document.activityCoverage.splice(1,1)[0];if(removeCommand){f.state.commands=f.state.commands.filter(e=>e.command.id!==removed.commandId);f.state.events=f.state.events.filter(e=>e.commandId!==removed.commandId);}
 await journal(f.state);const intervals=await f.read();assert.equal(intervals[0].isKnown,false);assert.equal(intervals[0].throughUtc,time(2));assert.equal(intervals[1].isKnown,true);
});
test('legacy/full-looking journal never gains an anchor from creation time, current recurrence or count',async()=>{
 const f=await fixture();await f.apply('updateTask',1,{patch:{text:'legacy'}});delete f.state.document.activityCoverage;
 assert.ok((await f.read()).every(i=>!i.isKnown));await f.apply('updateTask',2,{patch:{text:'first witnessed'}});await f.apply('updateTask',3,{patch:{text:'second witnessed'}});
 const intervals=await f.read();assert.equal(intervals[0].isKnown,false);assert.equal(intervals[0].throughUtc,time(2));assert.equal(intervals[1].isKnown,true);assert.ok(f.state.document.activityCoverage[0].previousTaskRevision>0);
});
for(const kind of ['todo','daily','habit'])test('archive and native recovery retain identity and intervals '+kind,async()=>{
 const f=await fixture(kind);if(kind==='todo')await f.apply('completeTask',1);await f.apply('deleteTask',2);await f.apply('unarchiveTask',3,{patch:{unarchive:{archivedRevision:f.state.document.tasks[0].revision}}});
 if(kind==='todo')await f.apply('restoreTodo',4,{patch:{restoreTodo:{completionId:f.state.document.completions[0].id}}});else await f.apply('updateTask',4,{patch:{text:'tail'}});
 const intervals=await f.read(0,4);assert.ok(intervals.every(i=>i.isKnown));assert.equal(intervals.find(i=>i.fromUtc===time(2)).schedule.isArchived,true);assert.equal(f.state.document.tasks[0].id,f.taskId);assert.ok(f.state.commands.every(e=>e.effects.length===0));
});
test('correction retains original terminal source, creates no score and round-trips',async()=>{
 const f=await fixture();await f.apply('completeTask',1,{occurrenceDate:'2026-10-27'});const o=f.state.document.occurrences[0],source=f.state.commands.at(-1).command.id;await f.apply('correctOccurrence',2,{patch:{correctOccurrence:{occurrenceId:o.id,occurrenceRevision:o.revision,targetCommandId:source}}});await f.apply('updateTask',3,{patch:{text:'tail'}});
 assert.equal(f.state.document.completions.length,1);assert.equal(f.state.document.habitScores.length,0);assert.ok((await f.read()).every(i=>i.isKnown));await journal(JSON.parse(JSON.stringify(f.state)));
});
test('coverage rejects tampered source identity, recurrence, accepted event, state, revision and score retention',async()=>{
 const f=await fixture('habit');await f.apply('scoreHabit',1,{direction:'up',count:2});await f.apply('scoreHabit',2,{direction:'down'});await f.apply('updateTask',3,{patch:{text:'tail'}});
 for(const change of [s=>s.document.activityCoverage[1].payloadHash='0'.repeat(64),s=>s.document.activityCoverage[1].state='paused',s=>s.document.activityCoverage[1].recordedAtUtc=time(2),s=>s.document.activityCoverage[1].previousTaskRevision=0,s=>s.document.activityCoverage[2].previousTaskRevision=2,s=>s.document.activityCoverage[1].recurrence=rule,s=>s.document.activityCoverage.push(s.document.activityCoverage[0]),s=>s.document.habitScores.pop(),s=>s.events=s.events.filter(e=>e.commandId!==s.commands[1].command.id)]){const bad=structuredClone(f.state);change(bad);await assert.rejects(journal(bad));await assert.rejects(readActivityCoverage(bad,f.taskId,time(0),time(3)));}
 assert.equal(f.state.document.habitScores.length,3);assert.ok((await f.read()).every(i=>i.isKnown));
});
test('schedule assertions cannot be imported without the original accepted recurrence',async()=>{
 const f=await fixture();await f.apply('updateTask',1,{patch:{text:'tail'}});const bad=structuredClone(f.state);bad.document.activityCoverage[0].recurrence.timeZoneId='Europe/Prague';await assert.rejects(journal(bad));
});
test('backwards accepted clock refuses chronological completeness',async()=>{
 const f=await fixture();await f.apply('updateTask',2,{patch:{text:'later'}});await f.apply('updateTask',1,{patch:{text:'clock rollback'}});assert.equal((await f.read())[0].unknownReason,'clock_order_unknown');
});
test('neutral C#/Worker fixture preserves causal coverage and schedule epochs',async()=>{
 const neutral=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/activity-coverage-v1.json',import.meta.url),'utf8'));await journal(neutral);const result=await readActivityCoverage(neutral,neutral.document.tasks[0].id,time(0),time(8));assert.ok(result.every(i=>i.isKnown));assert.equal(result.length,8);assert.equal(result[2].schedule.state,'paused');assert.equal(result[6].schedule.isArchived,true);
});
test('document metadata revisions preserve task chain; unobserved task transition splits it',async()=>{
 const f=await fixture();f.state.document.revision++;f.state.storeVersion++;await f.apply('updateTask',1,{patch:{text:'after metadata only'}});
 assert.ok((await f.read(0,1)).every(i=>i.isKnown));
 f.state.document.revision++;f.state.storeVersion++;f.state.document.tasks[0].revision=f.state.document.revision;f.state.document.tasks[0].state='paused';f.state.document.tasks[0].updatedAtUtc=time(2);
 await f.apply('resumeTask',3);await f.apply('updateTask',4,{patch:{text:'observed suffix'}});
 const intervals=await f.read(0,4);assert.equal(intervals[0].isKnown,true);assert.equal(intervals[1].isKnown,false);assert.equal(intervals[1].fromUtc,time(1));assert.equal(intervals[1].throughUtc,time(3));assert.equal(intervals[2].isKnown,true);
});
test('lost terminal occurrence evidence cannot masquerade as covered inactivity',async()=>{
 const f=await fixture();await f.apply('skipOccurrence',1,{occurrenceDate:'2026-10-27'});await f.apply('updateTask',2,{patch:{text:'tail'}});const bad=structuredClone(f.state);bad.document.occurrences=[];await assert.rejects(journal(bad));
});
test('current task cannot contradict a witness at the same revision',async()=>{
 const f=await fixture();await f.apply('updateTask',1,{patch:{text:'tail'}});for(const change of [s=>s.document.tasks[0].state='paused',s=>s.document.tasks[0].recurrence.timeZoneId='Europe/Prague',s=>s.document.tasks[0].isArchived=true]){const bad=structuredClone(f.state);change(bad);await assert.rejects(journal(bad));}
});
test('text-only command cannot invent a historical recurrence change',async()=>{
 const f=await fixture();await f.apply('updateTask',1,{patch:{text:'tail'}});const bad=structuredClone(f.state);bad.document.tasks[0].recurrence.timeZoneId='Europe/Prague';bad.document.activityCoverage[1].recurrence.timeZoneId='Europe/Prague';bad.commands[1].taskSnapshot.recurrence.timeZoneId='Europe/Prague';await assert.rejects(journal(bad));
});
test('an unrelated score at a covered revision cannot invent activity',async()=>{
 const f=await fixture('habit');await f.apply('updateTask',1,{patch:{text:'tail'}});const bad=structuredClone(f.state);bad.document.habitScores.push({id:crypto.randomUUID(),revision:bad.commands[0].taskSnapshot.revision,taskId:f.taskId,commandId:crypto.randomUUID(),recordedAtUtc:time(0),direction:'up',isArchived:false});await assert.rejects(journal(bad));assert.equal(f.state.document.habitScores.length,0);
});
