import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFile,writeFile} from 'node:fs/promises';
import {workspaceDocument} from '../../shared/workspace-domain.ts';
import {workspaceCommand,commandHash} from '../../shared/workspace-commands.ts';
import {queryTaskReviews} from '../../shared/task-review.ts';
import {accept} from '../src/commands.ts';
import {journal} from '../src/journal.ts';
const neutral=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/organization-history-v1.json',import.meta.url),'utf8'));
const initial=()=>workspaceDocument(neutral.cases.find(row=>row.name==='all entity kinds preserve create and partial update snapshots').document);
const now='2026-10-26T08:00:00Z',future='2026-10-27T09:00:00.1234567Z',later='2026-10-28T09:00:00Z',past='2026-10-23T09:00:00Z';
const command=(d,review,extra={})=>workspaceCommand({id:crypto.randomUUID(),workspaceId:d.id,expectedRevision:d.revision,operation:'reviewTask',taskId:d.tasks[0].id,patch:{review},...extra});
const patch=(action,extra={})=>({action,timeZoneId:'Asia/Novosibirsk',...extra});
test('review actions retain causal history without native/task/game changes',async()=>{
 for(const mapping of ['unlinked','linked','pending','externalConflict','unknownOutcome']){
  let d=initial();d.projections=[{id:crypto.randomUUID(),revision:d.revision,isArchived:false,taskId:d.tasks[0].id,state:mapping,habiticaTaskId:mapping==='unlinked'?null:crypto.randomUUID(),fingerprint:null,correlationAlias:null}];const before=structuredClone(d),entries=[],events=[];
  for(const review of [patch('set',{reviewAfterUtc:future}),patch('snooze',{reviewAfterUtc:later}),patch('setStale',{staleSinceUtc:past}),patch('markReviewed'),patch('set',{reviewAfterUtc:future}),patch('clear'),patch('clearStale')]){
   const saved=await accept(d,command(d,review),now);d=saved.document;entries.push(saved.entry);events.push(saved.event);assert.deepEqual(saved.entry.effects,[]);assert.equal(saved.entry.projectionState,'notRequested');await journal({document:d,storeVersion:d.revision,commands:entries,events});
  }
  assert.equal(d.reviewEvents.length,7);assert.equal(d.reviewEvents.filter(row=>row.action==='markReviewed').length,1);assert.deepEqual(d.tasks[0],{...before.tasks[0],revision:d.revision,updatedAtUtc:now});
  for(const key of ['completions','habitScores','restorations','projections','organizationHistory','occurrences'])assert.deepEqual(d[key],before[key]);
  const page=queryTaskReviews(d,{taskId:d.tasks[0].id,limit:2});assert.equal(page.matchedCount,7);assert.equal(page.items[0].action,'clearStale');assert.equal(page.nextOffset,2);assert.throws(()=>queryTaskReviews(d,{taskId:d.tasks[0].id,limit:2,offset:2}));assert.throws(()=>queryTaskReviews(d,{taskId:d.tasks[0].id,expectedRevision:1}));assert.equal(queryTaskReviews(d,{taskId:d.tasks[0].id,limit:2,offset:2,expectedRevision:d.revision}).items[0].action,'set');
 }
});
test('legacy fields stay optional; invalid/mixed/projected/stale review intents and archive tampering fail',async()=>{
 let d=initial();assert.equal(d.reviewEvents,undefined);d.tasks[0].reviewAfterUtc=past;d.tasks[0].staleSinceUtc=past;assert.equal(workspaceDocument(d).reviewEvents,undefined);
 for(const review of [{},patch('unknown'),patch('set'),patch('clear',{reviewAfterUtc:future}),patch('set',{reviewAfterUtc:'2026-10-27'}),patch('set',{reviewAfterUtc:'2026-10-27T09:00:00+07:00'}),{...patch('set',{reviewAfterUtc:future}),timeZoneId:'Unavailable/Zone'}])assert.throws(()=>command(d,review));
 assert.throws(()=>command(d,patch('clear'),{patch:{text:'Mixed',review:patch('clear')}}));
 await assert.rejects(accept(d,command(d,patch('set',{reviewAfterUtc:future}),{projectToHabitica:true}),now));await assert.rejects(accept(d,command(d,patch('snooze',{reviewAfterUtc:past})),now));await assert.rejects(accept(d,command(d,patch('setStale',{staleSinceUtc:future})),now));
 const saved=await accept(d,command(d,patch('snooze',{reviewAfterUtc:future})),now);d=saved.document;const archive={document:d,storeVersion:d.revision,commands:[saved.entry],events:[saved.event]};
 await assert.rejects(journal({...archive,document:{...d,reviewEvents:[]}}));assert.throws(()=>workspaceDocument({...d,reviewEvents:d.reviewEvents.map(row=>({...row,taskId:crypto.randomUUID()}))}));assert.throws(()=>workspaceDocument({...d,tasks:d.tasks.map((row,i)=>i===0?{...row,reviewAfterUtc:null}:row)}));
 await assert.rejects(journal({...archive,commands:[{...saved.entry,taskSnapshot:{...saved.entry.taskSnapshot,reviewAfterUtc:null}}]}));await assert.rejects(accept(d,command(d,patch('clear'),{expectedRevision:1}),now));
});
test('v10 identity canonicalizes UTC and preserves unused legacy encodings',async()=>{
 const d=initial(),c=command(d,patch('set',{reviewAfterUtc:future})),equivalent=workspaceCommand({...c,patch:{review:patch('set',{reviewAfterUtc:future.replace('Z','+00:00')})}});assert.equal(await commandHash(c),await commandHash(equivalent));
 for(const changed of [patch('snooze',{reviewAfterUtc:future}),{...patch('set',{reviewAfterUtc:future}),timeZoneId:'UTC'},patch('set',{reviewAfterUtc:later})])assert.notEqual(await commandHash(c),await commandHash(workspaceCommand({...c,patch:{review:changed}})));
 if(process.env.HABITICA_REVIEW_FIXTURE_EXPORT==='1'){
  const cases=[];for(const review of [patch('set',{reviewAfterUtc:future}),patch('snooze',{reviewAfterUtc:later}),patch('clear'),patch('markReviewed'),patch('setStale',{staleSinceUtc:past}),patch('clearStale')]){const c=command(d,review);cases.push({command:c,payloadHash:await commandHash(c)});}
  await writeFile(new URL('../../../tests/fixtures/workspace/task-review-command-v10.json',import.meta.url),JSON.stringify({cases},null,2)+'\n');
 }
 for(const file of ['task-review-command-v10.json','todo-stage-command-v8.json']){const golden=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/'+file,import.meta.url),'utf8'));for(const row of golden.cases)assert.equal(await commandHash(workspaceCommand(row.command)),row.payloadHash);}
});

import {fixture,enrolled,authorization,exchange,resource,accounts} from '../../chatgpt-auth/test/fixtures/oauth.mjs';
import {Client} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js';
import {StreamableHTTPClientTransport} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/streamableHttp.js';
const origin='http://localhost:8789';
async function connect(f,p){const response=await f.workspace.fetch(origin+'/connect',{method:'POST',headers:{authorization:'Bearer '+p.managementToken,'content-type':'application/json'},body:JSON.stringify({consentVersion:'workspace-v1'})});assert.equal(response.status,201);return response.json();}
async function sdk(t,f,p,scope='profile:read tasks:read habitica.tasks.write'){
 const r=await exchange(f,await authorization(f,p,scope));assert.equal(r.status,200);const token=(await r.json()).access_token,c=new Client({name:'voluntary-review-fixture',version:'1'});t.after(()=>c.close());await c.connect(new StreamableHTTPClientTransport(new URL(resource),{requestInit:{headers:{authorization:'Bearer '+token}},fetch:(url,init)=>f.mcp.fetch(String(url),init)}));return {c,token};
}
test('authenticated SDK review preview/accept/original receipt enforce account, revision and scope without game dispatch',async t=>{
 let writes=0;const f=await fixture(t,{workspace:true,habitica:request=>{if(request.method!=='GET')writes++;return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:'Fixture A'}}});}}),p=await enrolled(f),w=await connect(f,p),{c}=await sdk(t,f,p);
 const invoke=async(name,args)=>{const result=await c.callTool({name,arguments:args});assert.notEqual(result.isError,true,JSON.stringify(result));return result.structuredContent;};
 const create=await invoke('prepare_task_command',{requestId:crypto.randomUUID(),operation:'createTodo',patch:{text:'Optional review'},projectToHabitica:false}),task=await invoke('create_todo',{commandId:create.preparedCommandId});
 const tools=(await c.listTools()).tools;assert.equal(tools.find(row=>row.name==='get_task_reviews').annotations.readOnlyHint,true);assert.equal(tools.find(row=>row.name==='review_task').annotations.destructiveHint,true);assert.ok(tools.find(row=>row.name==='prepare_task_command').inputSchema.properties.patch.properties.review);
 const args={requestId:crypto.randomUUID(),operation:'reviewTask',taskId:task.taskId,patch:{review:patch('set',{reviewAfterUtc:'2099-11-01T09:00:00Z'})}},preview=await invoke('prepare_task_command',args);assert.equal(preview.mayAffectHabitica,false);assert.equal((await invoke('prepare_task_command',args)).preparedCommandId,preview.preparedCommandId);
 const receipt=await invoke('review_task',{commandId:preview.preparedCommandId});assert.equal(receipt.totalProjectionCount,0);assert.equal(receipt.acceptedEventCount,1);assert.equal((await invoke('review_task',{commandId:receipt.commandId})).commandId,receipt.commandId);
 const marked=await invoke('prepare_task_command',{...args,requestId:crypto.randomUUID(),patch:{review:patch('markReviewed')}});await invoke('review_task',{commandId:marked.preparedCommandId});
 const current=(await invoke('get_companion_task',{taskId:task.taskId})).task;assert.equal(current.reviewAfterUtc,null);assert.equal(current.state,'active');const history=await invoke('get_task_reviews',{taskId:task.taskId,limit:1});assert.equal(history.matchedCount,2);assert.equal(history.items[0].action,'markReviewed');assert.equal((await invoke('get_task_command',{commandId:receipt.commandId})).savedInCompanion,true);assert.equal(writes,0);
 const exported=await(await f.workspace.fetch(origin+'/export',{headers:{authorization:'Bearer '+w.sessionToken}})).json();await journal(exported);assert.equal(exported.document.completions.length,0);
 const other=await connect(f,await enrolled(f,accounts[1]));assert.equal((await f.workspace.fetch(origin+'/task-api',{method:'POST',headers:{authorization:'Bearer '+other.sessionToken,'content-type':'application/json'},body:JSON.stringify({tool:'review_task',arguments:{commandId:receipt.commandId}})})).status,409);
 const readOnly=await sdk(t,f,p,'profile:read tasks:read'),binding=(await f.mf.getBindings('mcp')).WORKSPACE_SERVER;assert.equal((await binding.fetch('https://workspace.internal/tool',{method:'POST',headers:{authorization:'Bearer '+readOnly.token,'content-type':'application/json'},body:JSON.stringify({tool:'review_task',arguments:{commandId:receipt.commandId}})})).status,403);
});
