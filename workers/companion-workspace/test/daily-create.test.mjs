import {test} from 'node:test';import assert from 'node:assert/strict';import {readFile} from 'node:fs/promises';
import {readDailySchedule,normalizeDailySchedule} from '../../shared/daily-projection.ts';import {expandRecurrence} from '../../shared/recurrence-calendar.ts';import {emptyWorkspace} from '../../shared/workspace-domain.ts';import {workspaceCommand} from '../../shared/workspace-commands.ts';import {accept} from '../src/commands.ts';import {projectionStep,confirmMapping} from '../src/projection-adapter.ts';import {remoteTask,remotePatch} from '../../shared/projection.ts';import {journal} from '../src/journal.ts';
const fixture=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/daily-projection-v1.json',import.meta.url),'utf8'));
for(const row of fixture.cases)test(row.name,()=>{if(row.failure)assert.throws(()=>normalizeDailySchedule(row.rule,row.account),error=>error.code===row.failure);else{assert.deepEqual(normalizeDailySchedule(row.rule,row.account),row.schedule);assert.deepEqual(expandRecurrence(row.rule,row.from,row.through).dates,row.dates);}});
test('Daily creation binds account, complete schedule, mapping and original alias without scoring',async()=>{
 const row=fixture.cases[0],document=emptyWorkspace(),command=workspaceCommand({id:crypto.randomUUID(),workspaceId:document.id,expectedRevision:1,operation:'createDaily',taskId:crypto.randomUUID(),projectToHabitica:true,patch:{text:'Scheduled Daily',recurrence:row.rule}}),accepted=await accept(document,command,'2026-10-21T00:00:00Z');
 await journal({document:accepted.document,storeVersion:2,commands:[accepted.entry],events:[accepted.event]});assert.deepEqual(accepted.entry.effects,['create']);assert.equal(accepted.document.completions.length,0);
 assert.equal(projectionStep(accepted.document,accepted.entry).kind,'account');const account={day:'2026-10-21',needsCron:false,accountCheckToken:'account',...row.account};
 assert.equal(projectionStep(accepted.document,accepted.entry,undefined,account).operation,'inspect');
 const step=projectionStep(accepted.document,accepted.entry,{task:null,checkToken:'absent'},account);assert.equal(step.kind,'write');assert.equal(step.arguments.taskKind,'daily');assert.equal(step.arguments.accountCheckToken,'account');assert.deepEqual(step.arguments.patch,{text:'Scheduled Daily',notes:'',...row.schedule});
 const user=crypto.randomUUID(),raw={...step.arguments.patch,type:'daily',id:crypto.randomUUID(),userId:user,completed:false,priority:1,attribute:'str',alias:step.arguments.selector},remote=await remoteTask(raw,user);
 assert.equal(projectionStep(accepted.document,{...accepted.entry,projectionState:'unknownOutcome'},{task:remote,checkToken:'new'},account).kind,'observedApplied');assert.equal(confirmMapping(accepted.document,command.taskId,remote).projections[0].habiticaTaskId,remote.id);
 const changed=await remoteTask({...raw,everyX:3},user);assert.notEqual(changed.fingerprint,remote.fingerprint);assert.equal(projectionStep(accepted.document,{...accepted.entry,projectionState:'unknownOutcome'},{task:changed,checkToken:'new'},account).code,'correlation_requires_review');
 assert.equal(projectionStep(accepted.document,accepted.entry,{task:null,checkToken:'absent'},{...account,needsCron:true}).code,'daily_precondition_unknown');
 assert.equal(projectionStep(accepted.document,accepted.entry,{task:null,checkToken:'absent'},{...account,timezoneOffset:60}).code,'daily_timezone_mismatch');
 assert.throws(()=>remotePatch({...step.arguments.patch,frequency:'yearly'},true,'todo'));assert.throws(()=>remotePatch({text:'No rule'},true,'daily'));
});

test('upstream zero interval remains readable for retained adoption but cannot authorize a new schedule',()=>{
 const row=fixture.cases[0];assert.equal(readDailySchedule({...row.schedule,everyX:0}).everyX,0);assert.throws(()=>normalizeDailySchedule({...row.rule,interval:0},row.account),error=>error.code==='invalid_recurrence');
});
