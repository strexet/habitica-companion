import {test} from 'node:test';import assert from 'node:assert/strict';import {readFile} from 'node:fs/promises';
import {queryTodoHierarchy,hierarchyFilter} from '../../shared/todo-hierarchy.ts';import {workspaceDocument} from '../../shared/workspace-domain.ts';import {workspaceCommand} from '../../shared/workspace-commands.ts';import {accept} from '../src/commands.ts';
const fixture=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/todo-hierarchy-v1.json',import.meta.url),'utf8')),now='2026-10-20T12:00:00Z';
test('portable retained hierarchy pages preserve depth order parent coverage and all source records',()=>{
 const document=workspaceDocument(fixture.document),before=JSON.stringify(document);
 for(const row of fixture.cases){const page=queryTodoHierarchy(document,row.arguments);assert.deepEqual(page.items.map(node=>[node.task.id,node.depth,node.childCount,node.parentStatus,node.parentCoverage,node.parentOffset]),row.rows,row.name);for(const key of ['matchedCount','offset','nextOffset','truncated'])assert.equal(page[key],row[key],row.name);assert.equal(page.contextTask?.id??null,row.arguments.taskId??null);assert.equal(page.totalTodoCount,6);}
 assert.equal(JSON.stringify(document),before);
});
test('2000 parent edges use iterative traversal bounded pages and independent exact reparent or detach',async()=>{
 const {count,idPrefix,sameName}=fixture.deepChain,id=index=>idPrefix+String(index+1).padStart(12,'0'),source=workspaceDocument(fixture.document),template=source.tasks[0];
 const tasks=Array.from({length:count},(_,index)=>({...template,id:id(index),text:sameName,parentTaskId:index===0?null:id(index-1),dependsOn:[],labelIds:[],projectId:source.projects[index%2].id,notes:null})).reverse();
 const document=workspaceDocument({...source,tasks,completions:[],projections:[],goals:[]}),before=JSON.stringify(document),rows=[];let offset=0;
 do{const page=queryTodoHierarchy(document,{limit:100,offset,expectedRevision:10});rows.push(...page.items);assert.ok(page.items.length<=100);assert.equal(page.matchedCount,count);if(page.nextOffset===null)break;offset=page.nextOffset;}while(true);
 assert.equal(rows.length,count);assert.deepEqual(rows.map(row=>row.depth),Array.from({length:count},(_,index)=>index));assert.equal(rows.at(-1).task.id,id(count-1));
 const ancestors=queryTodoHierarchy(document,{taskId:id(count-1),mode:'ancestors',offset:1900,expectedRevision:10});assert.equal(ancestors.matchedCount,2000);assert.equal(ancestors.items[0].parentOffset,1899);assert.equal(ancestors.items[0].parentCoverage,'earlierPage');
 const command=relationships=>workspaceCommand({id:crypto.randomUUID(),workspaceId:document.id,expectedRevision:10,operation:'relateTodo',taskId:id(count-1),patch:{relationships}});
 const moved=await accept(document,command({parentTaskId:id(0)}),now);assert.equal(moved.document.tasks[0].parentTaskId,id(0));assert.equal(moved.document.tasks[0].projectId,tasks[0].projectId);assert.deepEqual(moved.entry.effects,[]);assert.deepEqual(moved.document.completions,document.completions);
 const detached=await accept(document,command({clearParentTask:true}),now);assert.equal(detached.document.tasks[0].parentTaskId,null);assert.deepEqual(detached.entry.effects,[]);
 await assert.rejects(accept(document,{...command({parentTaskId:id(count-1)}),taskId:id(0)},now));assert.equal(JSON.stringify(document),before);
});
test('invalid queries missing and nonTodo targets and broken graphs never return partial hierarchies',()=>{
 for(const args of fixture.invalid)assert.throws(()=>hierarchyFilter(args));
 assert.throws(()=>queryTodoHierarchy(fixture.document,{expectedRevision:9}),error=>error.code==='stale_revision');
 for(const taskId of [crypto.randomUUID(),fixture.document.tasks[2].id,fixture.document.tasks[3].id])assert.throws(()=>queryTodoHierarchy(fixture.document,{taskId}),error=>error.code==='task_unavailable');
 for(const parent of [crypto.randomUUID(),fixture.document.tasks[0].id,fixture.document.tasks[4].id]){const d=structuredClone(fixture.document);d.tasks[0].parentTaskId=parent;assert.throws(()=>queryTodoHierarchy(d,{}));}
});
