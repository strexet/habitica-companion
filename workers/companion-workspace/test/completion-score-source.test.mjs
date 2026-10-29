import {test} from 'node:test';import assert from 'node:assert/strict';
import {completionScoreBinding,completionScoreSource,sameCompletionScoreSource} from '../../shared/completion-score-source.ts';
import {workspaceCommand} from '../../shared/workspace-commands.ts';import {emptyWorkspace} from '../../shared/workspace-domain.ts';import {accept} from '../src/commands.ts';
for(const [kind,index] of [['todo',0],['todo',1],['daily',0]])test('completion binding preserves '+kind+' source and effect position '+index,async()=>{
 let document=emptyWorkspace();const taskId=crypto.randomUUID(),now='2026-10-27T12:00:00Z',date='2026-10-27';
 const create=workspaceCommand({id:crypto.randomUUID(),workspaceId:document.id,expectedRevision:1,taskId,operation:kind==='daily'?'createDaily':'createTodo',patch:{text:'Synthetic source',...(kind==='daily'?{recurrence:{frequency:'daily',interval:1,startDate:date,timeZoneId:'UTC',weekdays:[],daysOfMonth:[]}}:{})}});document=(await accept(document,create,now)).document;
 const command=workspaceCommand({id:crypto.randomUUID(),workspaceId:document.id,expectedRevision:document.revision,taskId,operation:'completeTask',...(kind==='daily'?{occurrenceDate:date}:{})}),result=await accept(document,command,now);
 const entry={...result.entry,effects:index?['create','scoreUp']:['scoreUp'],confirmedEffects:index},source=completionScoreBinding(result.document,entry);
 assert.equal(source.workspaceId,document.id);assert.equal(source.commandId,command.id);assert.equal(source.commandPayloadHash,entry.payloadHash);assert.equal(source.taskId,taskId);assert.equal(source.completionId,result.document.completions[0].id);assert.equal(source.effectIndex,index);assert.equal(source.occurrenceDate,kind==='daily'?date:null);
 assert.deepEqual(completionScoreSource(JSON.parse(JSON.stringify(source))),source);assert.equal(sameCompletionScoreSource(source,{...source,completionId:crypto.randomUUID()}),false);
 assert.equal(completionScoreBinding({...result.document,completions:[]},entry),null);assert.equal(completionScoreBinding(result.document,{...entry,command:{...command,operation:'scoreHabit'}}),null);
});
test('malformed source proof never validates through coercion or ignored fields',()=>{
 const source={contract:'companion-completion-score-v1',workspaceId:crypto.randomUUID(),taskId:crypto.randomUUID(),commandId:crypto.randomUUID(),commandPayloadHash:'a'.repeat(64),completionId:crypto.randomUUID(),effectIndex:0,taskKind:'todo',occurrenceDate:null};
 for(const patch of [{effectIndex:'0'},{effectIndex:-1},{effectIndex:11},{taskKind:'habit'},{occurrenceDate:'2026-10-27'},{commandPayloadHash:'bad'},{ignored:true}])assert.throws(()=>completionScoreSource({...source,...patch}));assert.equal(completionScoreSource(null),null);
});
