import {test} from 'node:test';import assert from 'node:assert/strict';import {readFile} from 'node:fs/promises';
import {workspaceCommand} from '../../shared/workspace-commands.ts';import {workspaceDocument} from '../../shared/workspace-domain.ts';import {prepareTodoProjection,requireTodoProjection} from '../../shared/todo-projection-policy.ts';
const fixture=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/task-projection-defaults-v1.json',import.meta.url),'utf8'));
for(const row of fixture.cases)test(`required task defaults: ${row.name}`,()=>{
 workspaceDocument(row.document);const document=row.rawDocument?row.document:workspaceDocument(row.document),command=workspaceCommand(row.command),prepared=prepareTodoProjection(document,command);assert.equal(prepared.projectToHabitica,row.projected);assert.equal(command.projectToHabitica,false);
 const check=(input,failure)=>{if(failure)assert.throws(()=>requireTodoProjection(document,input),error=>error.code===failure);else assert.doesNotThrow(()=>requireTodoProjection(document,input));};check(prepared,row.preparedFailure);check(command,row.rawFailure);
 if(command.operation==='createHabit'){assert.equal(prepared.patch.habiticaDetails.up,true);assert.equal(prepared.patch.habiticaDetails.down,true);}
});
