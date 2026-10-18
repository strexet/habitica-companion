import test from 'node:test';import assert from 'node:assert/strict';import {readFile} from 'node:fs/promises';
import {workspaceDocument,emptyWorkspace} from '../../shared/workspace-domain.ts';import {queryCompletionHistory} from '../../shared/completion-history.ts';import {queryOrganization} from '../../shared/organization-query.ts';
import {workspaceCommand} from '../../shared/workspace-commands.ts';import {accept} from '../src/commands.ts';import {journal} from '../src/journal.ts';
const fixture=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/todo-restoration-cycles-v1.json',import.meta.url),'utf8'));
test('portable restoration cycle validation preserves causal history and legacy defaults',()=>{
 for(const value of fixture.cases){
  if(!value.valid){assert.throws(()=>workspaceDocument(value.document),undefined,value.name);continue;}
  const d=workspaceDocument(value.document),encoded=JSON.stringify(d);assert.equal(JSON.stringify(workspaceDocument(JSON.parse(encoded))),encoded,value.name);
  assert.deepEqual(d.completions.map(row=>row.id),value.document.completions.map(row=>row.id));assert.deepEqual((d.restorations??[]).map(row=>row.id),(value.document.restorations??[]).map(row=>row.id));
  assert.equal(Object.hasOwn(d,'restorations'),value.document.restorations!=null,value.name);const goal=queryOrganization(d,'list_goals',{}).items[0];assert.equal(goal.completedTodos,value.completedTodos,value.name);assert.equal(goal.eligibleTodos,value.eligibleTodos,value.name);
 }
 assert.equal(Object.hasOwn(emptyWorkspace(),'restorations'),false);
});
test('completion after restoration adds an event and preserves portable history metadata',async()=>{
 const d=workspaceDocument(fixture.cases.find(value=>value.name==='all cycles restored and ready to complete again').document);
 const result=await accept(d,workspaceCommand({id:crypto.randomUUID(),workspaceId:d.id,expectedRevision:d.revision,operation:'completeTask',taskId:d.tasks[0].id}), '2026-10-18T12:00:00Z');
 assert.equal(result.document.completions.length,3);assert.deepEqual(result.document.completions.slice(0,2),d.completions);assert.deepEqual(result.document.restorations,d.restorations);assert.equal(result.document.tasks[0].state,'completed');assert.deepEqual(result.entry.effects,[]);
 await journal({document:result.document,storeVersion:result.document.revision,commands:[result.entry],events:[result.event]});
 const rows=queryCompletionHistory(d,[],{}).items;assert.equal(rows.length,2);for(const row of rows){const restoration=d.restorations.find(value=>value.completionId===row.eventId);assert.deepEqual(row.restoration,{eventId:restoration.id,commandId:restoration.commandId,restoredAtUtc:restoration.restoredAtUtc,isArchived:restoration.isArchived});}
 const legacy=workspaceDocument(fixture.cases.find(value=>value.name==='legacy completion has no extension').document);assert.equal(Object.hasOwn(queryCompletionHistory(legacy,[],{}).items[0],'restoration'),false);
});
test('restoration fixture and history metadata also match the pinned Workers runtime',async t=>{
 const {createRequire}=await import('node:module'),{fileURLToPath}=await import('node:url');const require=createRequire(new URL('../../chatgpt-auth/package.json',import.meta.url));
 const {build}=require('esbuild'),{Miniflare,convertV4MiniflareOptions}=require('miniflare');const config=JSON.parse(await readFile(new URL('../wrangler.jsonc',import.meta.url),'utf8'));
 const bundle=await build({stdin:{contents:"import {workspaceDocument} from '../../shared/workspace-domain.ts';import {queryCompletionHistory} from '../../shared/completion-history.ts';import {queryOrganization} from '../../shared/organization-query.ts';export default {async fetch(request){try{const document=workspaceDocument(await request.json());return Response.json({valid:true,document,history:queryCompletionHistory(document,[],{}),goals:queryOrganization(document,'list_goals',{}).items});}catch{return Response.json({valid:false});}}}",resolveDir:fileURLToPath(new URL('.',import.meta.url))},bundle:true,write:false,format:'esm',platform:'browser',target:'es2022'});
 let calls=0;const mf=new Miniflare(convertV4MiniflareOptions({workers:[{name:'restoration-model',modules:true,script:bundle.outputFiles[0].text,compatibilityDate:config.compatibility_date,outboundService:()=>{calls++;throw new Error('unexpected_external_call');}}]}));t.after(()=>mf.dispose());const worker=await mf.getWorker('restoration-model');
 for(const value of fixture.cases)await t.test(value.name,async()=>{const response=await worker.fetch('https://restoration.test/',{method:'POST',body:JSON.stringify(value.document)}),body=await response.json();assert.equal(body.valid,value.valid,value.name);if(value.valid){assert.deepEqual(body.document,workspaceDocument(value.document));assert.equal(body.goals[0].completedTodos,value.completedTodos);assert.equal(body.goals[0].eligibleTodos,value.eligibleTodos);assert.deepEqual(body.history.items,queryCompletionHistory(workspaceDocument(value.document),[],{}).items);}});
 assert.equal(calls,0);
});
