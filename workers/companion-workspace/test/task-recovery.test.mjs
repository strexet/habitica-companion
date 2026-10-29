import {taskRecoveryQuery} from '../../shared/task-recovery-query.ts';
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {workspaceCommand,commandHash} from '../../shared/workspace-commands.ts';
import {emptyWorkspace,workspaceDocument} from '../../shared/workspace-domain.ts';
import {accept} from '../src/commands.ts';
import {journal} from '../src/journal.ts';
const now='2026-10-27T12:00:00Z',day='2026-10-27';
const hashes=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/task-recovery-command-v11.json',import.meta.url),'utf8'));
for(const row of hashes.cases)test('recovery identity '+row.name,async()=>assert.equal(await commandHash(workspaceCommand(row.command)),row.payloadHash));
async function setup(kind='daily'){
 let document=emptyWorkspace();const entries=[],events=[];const taskId=crypto.randomUUID();
 const command=(operation,extra={})=>workspaceCommand({id:crypto.randomUUID(),workspaceId:document.id,expectedRevision:document.revision,operation,taskId,...extra});
 const apply=async c=>{const result=await accept(document,c,now,entries);document=result.document;entries.push(result.entry);events.push(result.event);assert.deepEqual(result.entry.effects,[]);assert.equal(result.entry.projectionState,'notRequested');await journal({document,storeVersion:document.revision,commands:entries,events});return result;};
 await apply(command(kind==='todo'?'createTodo':kind==='habit'?'createHabit':'createDaily',{patch:{text:'Synthetic recovery',...(kind==='daily'?{recurrence:{frequency:'daily',interval:1,startDate:day,timeZoneId:'UTC',weekdays:[],daysOfMonth:[]}}:{})}}));
 const correction=()=>{const o=document.occurrences[0];return command('correctOccurrence',{patch:{correctOccurrence:{occurrenceId:o.id,occurrenceRevision:o.revision,targetCommandId:entries.findLast(row=>['completeTask','skipOccurrence'].includes(row.command.operation)).command.id}}});};
 const unarchive=(planning)=>command('unarchiveTask',{patch:{unarchive:{archivedRevision:document.tasks[0].revision,...(planning?{planning}:{})}}});
 return {get d(){return document;},entries,events,command,apply,correction,unarchive,set(d){document=d;}};
}
for(const skip of [false,true])test('native correction retains original '+(skip?'skip':'completion')+' through repeated cycles',async()=>{
 const f=await setup();await f.apply(f.command(skip?'skipOccurrence':'completeTask',{occurrenceDate:day}));const previous=structuredClone(f.d.occurrences[0]),originals=structuredClone(f.d.completions),command=f.correction();
 await f.apply(command);assert.deepEqual(f.d.occurrenceCorrections[0].previousOccurrence,previous);assert.deepEqual(f.d.completions,originals);assert.equal(f.d.occurrences[0].state,'pending');
 await assert.rejects(accept(f.d,f.command('correctOccurrence',{patch:command.patch}),now,f.entries),e=>e.code==='occurrence_source_unavailable');
 await f.apply(f.command('completeTask',{occurrenceDate:day}));await f.apply(f.correction());assert.equal(f.d.occurrenceCorrections.length,2);assert.equal(f.d.completions.length,skip?1:2);assert.equal(f.d.occurrences.length,1);assert.equal(f.d.occurrences[0].id,previous.id);
 assert.deepEqual(workspaceDocument(JSON.parse(JSON.stringify(f.d))),f.d);
 for(const change of [row=>row.targetCommandId=crypto.randomUUID(),row=>row.previousOccurrence.revision=row.revision,row=>row.previousOccurrence.state='pending',row=>row.previousOccurrence.date='2026-10-28']){const bad=structuredClone(f.d);change(bad.occurrenceCorrections[0]);assert.throws(()=>workspaceDocument(bad));}
 const bad=structuredClone(f.d);bad.occurrenceCorrections.push({...bad.occurrenceCorrections[0],id:crypto.randomUUID()});assert.throws(()=>workspaceDocument(bad));
 const archive={document:f.d,storeVersion:f.d.revision,commands:structuredClone(f.entries),events:f.events};archive.commands.at(-1).effects=['scoreDown'];archive.commands.at(-1).projectionState='pending';await assert.rejects(journal(archive));
});
for(const kind of ['todo','daily','habit'])test('unarchive preserves '+kind+' state separately from completion undo',async()=>{
 const f=await setup(kind);if(kind==='todo')await f.apply(f.command('completeTask'));await f.apply(f.command('deleteTask'));const before=structuredClone(f.d.tasks[0]);await f.apply(f.unarchive());assert.deepEqual(f.d.tasks[0],{...before,revision:f.d.revision,isArchived:false});assert.equal(f.d.unarchives.length,1);await assert.rejects(accept(f.d,f.unarchive(),now,f.entries),e=>e.code==='task_unavailable');
});
test('unarchive requires explicit relationship repair and correction requires original source',async()=>{
 const f=await setup('todo');await f.apply(f.command('deleteTask'));const d=structuredClone(f.d),projectId=crypto.randomUUID();d.projects.push({id:projectId,revision:1,name:'Archived project',categoryId:null,isArchived:true});d.tasks[0].projectId=projectId;f.set(d);const before=JSON.stringify(f.d);
 await assert.rejects(accept(f.d,f.unarchive(),now,f.entries),e=>e.code==='unarchive_repair_required');assert.equal(JSON.stringify(f.d),before);await f.apply(f.unarchive({clearProject:true}));assert.equal(f.d.tasks[0].projectId,null);
 const daily=await setup();await daily.apply(daily.command('skipOccurrence',{occurrenceDate:day}));await assert.rejects(accept(daily.d,daily.correction(),now,[]),e=>e.code==='occurrence_source_unavailable');
});
test('stale, foreign and linked/unknown corrections cannot dispatch or silently mutate',async()=>{
 const f=await setup();await f.apply(f.command('completeTask',{occurrenceDate:day}));const command=f.correction(),before=JSON.stringify(f.d);
 for(const change of [c=>c.projectToHabitica=true,c=>c.patch.correctOccurrence.occurrenceId=crypto.randomUUID(),c=>c.patch.correctOccurrence.targetCommandId=crypto.randomUUID()]){const bad=structuredClone(command);change(bad);await assert.rejects(accept(f.d,bad,now,f.entries));}
 for(const state of ['linked','pending','unknownOutcome','externalConflict']){const d=structuredClone(f.d);d.projections=[{id:crypto.randomUUID(),revision:1,taskId:command.taskId,state,habiticaTaskId:crypto.randomUUID(),isArchived:true}];await assert.rejects(accept(d,command,now,f.entries),e=>e.code==='unsupported_projection');}
 for(const change of [c=>c.patch.text='Unexpected',c=>c.count=2,c=>c.occurrenceDate=day,c=>c.operation='updateTask']){const bad=structuredClone(command);change(bad);assert.throws(()=>workspaceCommand(bad));}
 assert.equal(JSON.stringify(f.d),before);await f.apply(command);await assert.rejects(accept(f.d,{...command,id:crypto.randomUUID()},now,f.entries),e=>e.code==='stale_revision');
});

test('exact recovery read includes archived state, bounded source ownership and stale refusal',async()=>{
 const f=await setup();await f.apply(f.command('skipOccurrence',{occurrenceDate:day}));let read=taskRecoveryQuery(f.d,f.entries,{taskId:f.d.tasks[0].id});assert.equal(read.items[0].canCorrect,true);assert.equal(read.items[0].targetCommandId,f.entries.at(-1).command.id);
 assert.equal(taskRecoveryQuery(f.d,[],{taskId:f.d.tasks[0].id}).items[0].reason,'occurrence_source_unavailable');
 await f.apply(f.command('deleteTask'));read=taskRecoveryQuery(f.d,f.entries,{taskId:f.d.tasks[0].id});assert.equal(read.task.isArchived,true);assert.equal(read.canUnarchive,true);assert.equal(read.items[0].canCorrect,false);
 assert.equal(taskRecoveryQuery(f.d,[],{taskId:crypto.randomUUID()}).task,null);
 for(const input of [{taskId:f.d.tasks[0].id,limit:101},{taskId:f.d.tasks[0].id,offset:1},{taskId:f.d.tasks[0].id,expectedRevision:1}])assert.throws(()=>taskRecoveryQuery(f.d,f.entries,input));
});

test('neutral retained journal validates exact command hashes and causal source receipts',async()=>{
 const snapshot=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/task-recovery-journal-v1.json',import.meta.url),'utf8'));await journal(snapshot);assert.equal(snapshot.document.occurrenceCorrections.length,2);assert.equal(snapshot.document.unarchives.length,1);
 const bad=structuredClone(snapshot);bad.commands.at(-1).taskSnapshot.state='paused';await assert.rejects(journal(bad));
});
