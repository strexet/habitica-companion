import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFile,writeFile} from 'node:fs/promises';
import {fixture,enrolled} from '../../chatgpt-auth/test/fixtures/oauth.mjs';
import {projectionStep} from '../src/projection-adapter.ts';
import {workspaceDocument} from '../../shared/workspace-domain.ts';
import {remoteTask} from '../../shared/projection.ts';
import {journal} from '../src/journal.ts';
const seed=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/command-v2.json',import.meta.url),'utf8'));
const origin='http://localhost:8789';
async function call(f,path,token,body){return f.workspace.fetch(origin+path,{method:body===undefined?'GET':'POST',headers:{authorization:`Bearer ${token}`,'content-type':'application/json'},body:body===undefined?undefined:JSON.stringify(body)});}
test('cloud commands preserve typed details, project the whitelist and keep native urgency independent',async t=>{
 const f=await fixture(t,{workspace:true});const p=await enrolled(f);const connected=await call(f,'/connect',p.managementToken,{consentVersion:'workspace-v1',workspaceId:seed.command.workspaceId});assert.equal(connected.status,201);const w=await connected.json();
 const accepted=await call(f,'/commands',w.sessionToken,seed.command);assert.equal(accepted.status,201,await accepted.clone().text());const receipt=await accepted.json();assert.equal(receipt.entry.payloadHash,seed.payloadHash);
 let exported=await(await call(f,'/export',w.sessionToken)).json();await journal(exported);const before=exported.document.tasks[0];assert.equal(before.priority,'high');assert.deepEqual(before.habiticaDetails,seed.command.patch.habiticaDetails);
 const need=projectionStep(exported.document,exported.commands[0]);assert.equal(need.kind,'inspect');const create=projectionStep(exported.document,exported.commands[0],{task:null,checkToken:'synthetic'});assert.equal(create.kind,'write');assert.equal(create.operation,'create');assert.equal(create.arguments.patch.priority,1.5);assert.equal(create.arguments.patch.attribute,'per');assert.deepEqual(create.arguments.patch.checklist,before.habiticaDetails.checklist);
 const update={id:crypto.randomUUID(),workspaceId:w.workspaceId,expectedRevision:exported.document.revision,operation:'updateTask',taskId:before.id,projectToHabitica:true,patch:{habiticaDetails:{attribute:'int'}}};assert.equal((await call(f,'/commands',w.sessionToken,update)).status,201);
 exported=await(await call(f,'/export',w.sessionToken)).json();const current=exported.document.tasks[0];assert.equal(current.habiticaDetails.attribute,'int');assert.equal(current.habiticaDetails.difficulty,1.5);assert.deepEqual(current.habiticaDetails.checklist,before.habiticaDetails.checklist);assert.equal(exported.commands[0].taskSnapshot.habiticaDetails.attribute,'per');
 const remote=await remoteTask({id:'90000000-0000-4000-8000-000000000024',userId:'00000000-0000-4000-8000-000000000011',type:'todo',text:before.text,notes:before.notes,priority:1.5,attribute:'per',completed:false,checklist:before.habiticaDetails.checklist},'00000000-0000-4000-8000-000000000011');
 const mapped=workspaceDocument({...exported.document,projections:[{...exported.document.projections[0],state:'linked',habiticaTaskId:remote.id,fingerprint:remote.fingerprint}]});const partial=projectionStep(mapped,{...exported.commands[1],effects:['update']},{task:remote,checkToken:'synthetic'});assert.equal(partial.kind,'write');assert.deepEqual(partial.arguments.patch,{attribute:'int'});
 const clear={...update,id:crypto.randomUUID(),expectedRevision:exported.document.revision,patch:{habiticaDetails:{checklist:[]}}};assert.equal((await call(f,'/commands',w.sessionToken,clear)).status,201);exported=await(await call(f,'/export',w.sessionToken)).json();assert.deepEqual(exported.document.tasks[0].habiticaDetails.checklist,[]);assert.equal(exported.document.tasks[0].habiticaDetails.difficulty,1.5);await journal(exported);
 await writeFile('/tmp/habitica-cloud-journal-v2.json',JSON.stringify(exported,null,2)+'\n');
});
test('invalid details and unknown nested fields leave the canonical document and journal unchanged',async t=>{
 const f=await fixture(t,{workspace:true});const p=await enrolled(f);const w=await(await call(f,'/connect',p.managementToken,{consentVersion:'workspace-v1',workspaceId:seed.command.workspaceId})).json();const before=await(await call(f,'/export',w.sessionToken)).json();
 for(const details of [{difficulty:10},{attribute:'str;POST'},{checklist:[{id:crypto.randomUUID(),text:'Invalid',completed:'true'}]},{checklist:seed.command.patch.habiticaDetails.checklist.map(i=>({...i,id:seed.command.patch.habiticaDetails.checklist[0].id}))},{endpoint:'tasks/user'},{}]){
  assert.ok([400,422].includes((await call(f,'/commands',w.sessionToken,{...seed.command,patch:{text:'Reject',habiticaDetails:details}})).status));assert.deepEqual(await(await call(f,'/export',w.sessionToken)).json(),before);
 }
});
