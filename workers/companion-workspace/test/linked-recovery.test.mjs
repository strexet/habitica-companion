import {test} from 'node:test';import assert from 'node:assert/strict';import {readFile} from 'node:fs/promises';
import {journal} from '../src/journal.ts';import {linkedRecoveryInput} from '../../shared/linked-recovery-tools.ts';
for(const kind of ['todo','daily'])test('neutral '+kind+' linked history requires original score source and paired causal recovery',async()=>{
 const state=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/linked-recovery-'+kind+'-journal-v1.json',import.meta.url),'utf8'));await journal(state);
 for(const change of [s=>delete s.document.linkedTaskRecoveries,s=>s.document.linkedTaskRecoveries[0].sourceAttemptId=crypto.randomUUID(),s=>s.document.linkedTaskRecoveries[0].accountId=crypto.randomUUID(),s=>s.document.linkedTaskRecoveries[0].sourceContext.commandPayloadHash='b'.repeat(64),s=>s.document.linkedTaskRecoveries[0].contextHash='b'.repeat(64),s=>{s.document.restorations=[];s.document.occurrenceCorrections=[];},s=>s.events=s.events.filter(row=>row.attemptToken!==s.document.linkedTaskRecoveries[0].sourceAttemptId)]){const bad=structuredClone(state);change(bad);await assert.rejects(journal(bad));}
});
test('linked recovery public intent refuses account selectors, incomplete reviews and unbounded revision',()=>{
 const id=crypto.randomUUID(),valid={taskId:id,completionId:crypto.randomUUID(),requestId:crypto.randomUUID(),expectedRevision:1};
 assert.deepEqual(linkedRecoveryInput('prepare_linked_task_recovery',valid),valid);
 for(const patch of [{accountId:id},{expectedRevision:0},{expectedRevision:1.5},{taskId:'alias'},{contextHash:'a'.repeat(64)}])assert.throws(()=>linkedRecoveryInput('prepare_linked_task_recovery',{...valid,...patch}));
 for(const args of [{commandId:id},{commandId:id,contextHash:'a'.repeat(64)},{commandId:id,contextHash:'a'.repeat(64),reviewHash:'b'.repeat(64),approved:true}])assert.throws(()=>linkedRecoveryInput('accept_linked_task_recovery',args));
});
