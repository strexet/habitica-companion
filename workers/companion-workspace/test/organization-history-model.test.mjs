import {test} from 'node:test';import assert from 'node:assert/strict';import {readFile} from 'node:fs/promises';
import {organizationCommand,organizationCommandHash} from '../../shared/workspace-organization.ts';import {workspaceDocument,emptyWorkspace} from '../../shared/workspace-domain.ts';import {queryOrganization} from '../../shared/organization-query.ts';
import {workspaceCommand,commandHash} from '../../shared/workspace-commands.ts';import {accept} from '../src/commands.ts';import {journal} from '../src/journal.ts';
import {fixture,enrolled} from '../../chatgpt-auth/test/fixtures/oauth.mjs';
const neutral=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/organization-history-v1.json',import.meta.url),'utf8'));
const archive=document=>({document,storeVersion:document.revision,commands:[],events:[]});
test('independent native organization hashes bind every kind/action and exact unicode patch',async()=>{
 for(const value of neutral.hashes)assert.equal(await organizationCommandHash(organizationCommand(value.command)),value.payloadHash,value.name);
});
test('neutral native organization metadata and full journal checks distinguish shape from checksum integrity',async()=>{
 for(const value of neutral.cases){
  if(!value.documentValid){assert.throws(()=>workspaceDocument(value.document),undefined,value.name);continue;}
  const document=workspaceDocument(value.document),present=value.document.organizationHistory!=null;assert.equal(Object.hasOwn(document,'organizationHistory'),present,value.name);assert.equal(document.organizationHistory?.length??0,value.document.organizationHistory?.length??0);
  if(!value.valid){await assert.rejects(journal(archive(document)),undefined,value.name);continue;}
  const encoded=JSON.stringify(await journal(archive(document)));assert.equal(JSON.stringify(await journal(JSON.parse(encoded))),encoded,value.name);
 }
 assert.equal(Object.hasOwn(emptyWorkspace(),'organizationHistory'),false);
});
test('native task changes preserve organization history and current Todo goal progress',async()=>{
 let d=workspaceDocument(neutral.cases.find(value=>value.name==='all entity kinds preserve create and partial update snapshots').document);const before=JSON.stringify(d.organizationHistory),goal=d.goals.at(-1);
 assert.equal(queryOrganization(d,'list_goals',{}).items.find(row=>row.goal.id===goal.id).completedTodos,1);
 const corrupted=structuredClone(d);corrupted.organizationHistory[0].payloadHash='0'.repeat(64);await assert.rejects(accept(corrupted,workspaceCommand({id:crypto.randomUUID(),workspaceId:d.id,expectedRevision:d.revision,operation:'planTask',taskId:d.tasks[0].id,patch:{planning:{plannedDate:'2026-10-25'}}}),'2026-10-18T13:00:00Z'));
 const c=workspaceCommand({id:crypto.randomUUID(),workspaceId:d.id,expectedRevision:d.revision,operation:'planTask',taskId:d.tasks[0].id,patch:{planning:{plannedDate:'2026-10-25'}}}),planned=await accept(d,c,'2026-10-18T13:00:00Z');d=planned.document;assert.equal(JSON.stringify(d.organizationHistory),before);assert.deepEqual(planned.entry.effects,[]);await journal({document:d,storeVersion:d.revision,commands:[planned.entry],events:[planned.event]});
 await assert.rejects(accept(d,{...c,id:d.organizationHistory[0].command.id,expectedRevision:d.revision},'2026-10-18T13:00:00Z'),e=>e.code==='command_id_reused');
 const completion=d.completions[0],restored=await accept(d,workspaceCommand({id:crypto.randomUUID(),workspaceId:d.id,expectedRevision:d.revision,operation:'restoreTodo',taskId:completion.taskId,patch:{restoreTodo:{completionId:completion.id}}}),'2026-10-18T13:00:00Z');d=restored.document;
 assert.equal(JSON.stringify(d.organizationHistory),before);assert.equal(queryOrganization(d,'list_goals',{}).items.find(row=>row.goal.id===goal.id).completedTodos,0);
 await journal({document:d,storeVersion:d.revision,commands:[planned.entry,restored.entry],events:[planned.event,restored.event]});
 const conflicting=structuredClone(planned.entry);conflicting.command.id=d.organizationHistory[0].command.id;conflicting.payloadHash=await commandHash(conflicting.command);await assert.rejects(journal({document:d,storeVersion:d.revision,commands:[conflicting],events:[]}));
 await assert.rejects(journal({...archive(d),events:[{...planned.event,commandId:d.organizationHistory[0].command.id}]}));
});
test('name-only goal history retains legacy sets beyond the new replacement bound',async()=>{
 const d=structuredClone(neutral.cases.find(value=>value.name==='name-only goal update retains legacy non-Todo references').document),goal=d.goals.at(-1);
 const tasks=Array.from({length:120},()=>({...structuredClone(d.tasks[0]),id:crypto.randomUUID()})),ids=tasks.map(task=>task.id);d.tasks.push(...tasks);goal.taskIds=ids;
 const entry=d.organizationHistory.find(value=>value.snapshot.id===goal.id);entry.snapshot.taskIds=ids;
 assert.equal(entry.command.patch.taskIds??null,null);await journal(archive(workspaceDocument(d)));
 assert.throws(()=>organizationCommand({...entry.command,patch:{...entry.command.patch,taskIds:ids}}));
});
test('neutral organization model also matches the pinned Workers runtime without outbound requests',async t=>{
 const {createRequire}=await import('node:module'),{fileURLToPath}=await import('node:url'),require=createRequire(new URL('../../chatgpt-auth/package.json',import.meta.url));const {build}=require('esbuild'),{Miniflare,convertV4MiniflareOptions}=require('miniflare'),config=JSON.parse(await readFile(new URL('../wrangler.jsonc',import.meta.url),'utf8'));
 const bundle=await build({stdin:{contents:"import {workspaceDocument} from '../../shared/workspace-domain.ts';import {journal} from '../src/journal.ts';export default {async fetch(request){let document;try{document=workspaceDocument(await request.json());}catch{return Response.json({documentValid:false,valid:false});}let valid=true;try{await journal({document,storeVersion:document.revision,commands:[],events:[]});}catch{valid=false;}return Response.json({documentValid:true,valid,present:Object.hasOwn(document,'organizationHistory'),count:document.organizationHistory?.length??0});}}",resolveDir:fileURLToPath(new URL('.',import.meta.url))},bundle:true,write:false,format:'esm',platform:'browser',target:'es2022'});
 let calls=0;const mf=new Miniflare(convertV4MiniflareOptions({workers:[{name:'organization-model',modules:true,script:bundle.outputFiles[0].text,compatibilityDate:config.compatibility_date,outboundService:()=>{calls++;throw new Error('unexpected_outbound');}}]}));t.after(()=>mf.dispose());const worker=await mf.getWorker('organization-model');
 for(const value of neutral.cases)await t.test(value.name,async()=>{const response=await worker.fetch('https://organization.test/',{method:'POST',body:JSON.stringify(value.document)}),body=await response.json();assert.equal(body.documentValid,value.documentValid,value.name);assert.equal(body.valid,value.valid,value.name);if(value.documentValid){assert.equal(body.present,value.document.organizationHistory!=null);assert.equal(body.count,value.document.organizationHistory?.length??0);}});assert.equal(calls,0);
});
test('synthetic frozen handoff/export and task acceptance retain native organization history atomically',async t=>{
 let writes=0;const f=await fixture(t,{workspace:true,habitica:request=>{if(request.method!=='GET')writes++;return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:'Fixture A'}}});}}),p=await enrolled(f),origin='http://localhost:8789';
 const connected=await f.workspace.fetch(origin+'/connect',{method:'POST',headers:{authorization:'Bearer '+p.managementToken,'content-type':'application/json'},body:JSON.stringify({consentVersion:'workspace-v1'})});assert.equal(connected.status,201);const w=await connected.json(),headers={authorization:'Bearer '+w.sessionToken,'content-type':'application/json'};
 const document=workspaceDocument(neutral.cases.find(value=>value.name==='all entity kinds preserve create and partial update snapshots').document),transferId=crypto.randomUUID(),incoming={...archive(document),authority:{mode:'frozen',transferId,cloudWorkspaceId:null}},body=JSON.stringify({consentVersion:'workspace-handoff-v1',transferId,archive:JSON.stringify(incoming)});
 const handoff=()=>f.workspace.fetch(origin+'/handoff',{method:'POST',headers,body});assert.equal((await handoff()).status,201);assert.equal((await handoff()).status,200);
 let exported=await(await f.workspace.fetch(origin+'/export',{headers})).json();await journal(exported);assert.deepEqual(exported.document.organizationHistory,document.organizationHistory);assert.equal(exported.authority.mode,'cloud');assert.equal(exported.commands.length,0);
 const command=workspaceCommand({id:crypto.randomUUID(),workspaceId:document.id,expectedRevision:document.revision,operation:'planTask',taskId:document.tasks[0].id,patch:{planning:{plannedDate:'2026-10-25'}}}),accepted=await f.workspace.fetch(origin+'/commands',{method:'POST',headers,body:JSON.stringify(command)});assert.equal(accepted.status,201,await accepted.clone().text());assert.deepEqual((await accepted.json()).entry.effects,[]);
 exported=await(await f.workspace.fetch(origin+'/export',{headers})).json();await journal(exported);assert.deepEqual(exported.document.organizationHistory,document.organizationHistory);assert.equal(exported.commands.length,1);assert.equal(writes,0);
 const collision=await f.workspace.fetch(origin+'/commands',{method:'POST',headers,body:JSON.stringify({...command,id:document.organizationHistory[0].command.id,expectedRevision:exported.document.revision})});assert.equal(collision.status,409);
 const damaged=structuredClone(incoming),nextTransfer=crypto.randomUUID();damaged.authority.transferId=nextTransfer;damaged.document.organizationHistory[0].payloadHash='0'.repeat(64);const rejected=await f.workspace.fetch(origin+'/handoff',{method:'POST',headers,body:JSON.stringify({consentVersion:'workspace-handoff-v1',transferId:nextTransfer,archive:JSON.stringify(damaged)})});assert.equal(rejected.status,422);assert.equal((await f.workspaceDb.prepare('SELECT COUNT(*) AS n FROM workspace_commands').first()).n,1);
});
