import test from 'node:test';import assert from 'node:assert/strict';import {readFile} from 'node:fs/promises';
import {fixture,enrolled,authorization,exchange,resource,accounts} from '../../chatgpt-auth/test/fixtures/oauth.mjs';
import {Client} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js';
import {StreamableHTTPClientTransport} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/streamableHttp.js';
import {workspaceCommand,commandHash} from '../../shared/workspace-commands.ts';import {emptyWorkspace} from '../../shared/workspace-domain.ts';
import {accept} from '../src/commands.ts';import {journal} from '../src/journal.ts';
const now='2026-10-17T00:00:00Z';const rule={frequency:'daily',interval:1,startDate:'2026-10-17',timeZoneId:'UTC',weekdays:[],daysOfMonth:[]};const origin='http://localhost:8789';
const intent=(d,id,operation,patch=null,extra={})=>workspaceCommand({id:crypto.randomUUID(),workspaceId:d.id,expectedRevision:d.revision,operation,taskId:id,patch,...extra});
async function seed(kind='daily'){const d=emptyWorkspace(crypto.randomUUID());const id=crypto.randomUUID();const created=await accept(d,intent(d,id,kind==='daily'?'createDaily':kind==='habit'?'createHabit':'createTodo',{text:'Synthetic recurring work',...(kind==='daily'?{recurrence:rule}:{})}),now);return {d:created.document,id,entries:[created.entry],events:[created.event]};}
async function step(s,operation,date=null,patch=null){const value=await accept(s.d,intent(s.d,s.id,operation,patch,date?{occurrenceDate:date}:{}),now);s.d=value.document;s.entries.push(value.entry);s.events.push(value.event);return value;}
async function exportable(s){return journal({document:s.d,storeVersion:s.d.revision,commands:s.entries,events:s.events});}
const hashes=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/lifecycle-command-v1.json',import.meta.url),'utf8'));
for(const value of hashes.cases)test('portable appended lifecycle identity: '+value.name,async()=>assert.equal(await commandHash(workspaceCommand(value.command)),value.payloadHash));

test('skip, pause, resume and restart preserve civil history without automatic effects',async()=>{
 const s=await seed();const skipped=await step(s,'skipOccurrence','2026-10-17');assert.equal(s.d.occurrences[0].state,'skipped');assert.equal(s.d.completions.length,0);
 for(const operation of ['skipOccurrence','completeTask'])await assert.rejects(accept(s.d,intent(s.d,s.id,operation,null,{occurrenceDate:'2026-10-17'}),now),error=>error.code==='occurrence_unavailable');
 await step(s,'completeTask','2026-10-18');await assert.rejects(accept(s.d,intent(s.d,s.id,'skipOccurrence',null,{occurrenceDate:'2026-10-18'}),now),error=>error.code==='occurrence_unavailable');
 await step(s,'pauseTask');assert.equal(s.d.tasks[0].state,'paused');await assert.rejects(accept(s.d,intent(s.d,s.id,'completeTask',null,{occurrenceDate:'2026-10-19'}),now),error=>error.code==='task_not_completable');await assert.rejects(accept(s.d,intent(s.d,s.id,'skipOccurrence',null,{occurrenceDate:'2026-10-19'}),now),error=>error.code==='task_not_active');
 await step(s,'updateTask',null,{recurrence:{...rule,startDate:'2026-10-20'}});await step(s,'resumeTask');
 assert.equal(s.d.tasks[0].state,'active');assert.equal(s.d.tasks[0].recurrence.startDate,'2026-10-20');assert.equal(s.d.occurrences.length,2);assert.equal(s.d.completions.length,1);
 await assert.rejects(accept(s.d,intent(s.d,s.id,'completeTask',null,{occurrenceDate:'2026-10-19'}),now),error=>error.code==='occurrence_not_scheduled');
 assert.ok(s.entries.every(e=>e.projectionState==='notRequested'&&e.effects.length===0));await exportable(s);
 const bad={document:s.d,storeVersion:s.d.revision,commands:structuredClone(s.entries),events:s.events};const entry=bad.commands.find(e=>e.command.operation==='skipOccurrence');entry.command.projectToHabitica=true;entry.payloadHash=await commandHash(entry.command);entry.projectionState='pending';entry.effects=['scoreUp'];await assert.rejects(journal(bad));
 assert.equal(skipped.entry.command.occurrenceDate,'2026-10-17');
});
test('Habit pause blocks fresh score commands and resume keeps existing events',async()=>{
 const s=await seed('habit');const score=await accept(s.d,intent(s.d,s.id,'scoreHabit',null,{direction:'up'}),now);s.d=score.document;s.entries.push(score.entry);s.events.push(score.event);
 await step(s,'pauseTask');await assert.rejects(accept(s.d,intent(s.d,s.id,'scoreHabit',null,{direction:'up'}),now),error=>error.code==='habit_not_active');
 await step(s,'resumeTask');assert.equal(s.d.habitScores.length,1);await assert.rejects(accept(s.d,intent(s.d,s.id,'resumeTask'),now),error=>error.code==='task_not_paused');await exportable(s);
});
test('lifecycle refuses unsupported mappings and invalid selectors before changes',async()=>{
 const s=await seed();for(const operation of ['skipOccurrence','pauseTask','resumeTask']){
  const extra=operation==='skipOccurrence'?{occurrenceDate:'2026-10-17'}:{};
  await assert.rejects(accept(s.d,intent(s.d,s.id,operation,null,{...extra,projectToHabitica:true}),now),error=>error.code==='unsupported_projection');
  for(const state of ['linked','pending','unknownOutcome','externalConflict']){const d={...s.d,projections:[{id:crypto.randomUUID(),taskId:s.id,revision:s.d.revision,isArchived:false,state,habiticaTaskId:state==='linked'?crypto.randomUUID():null}]};await assert.rejects(accept(d,intent(d,s.id,operation,null,extra),now),error=>error.code==='unsupported_projection');}
 }
 await assert.rejects(accept(s.d,intent(s.d,s.id,'skipOccurrence'),now),error=>error.code==='occurrence_required');
 const todo=await seed('todo');await assert.rejects(accept(todo.d,intent(todo.d,todo.id,'pauseTask'),now),error=>error.code==='recurring_task_required');
 assert.throws(()=>intent(s.d,s.id,'pauseTask',null,{occurrenceDate:'2026-10-17'}));assert.throws(()=>intent(s.d,s.id,'skipOccurrence',{text:'unexpected'},{occurrenceDate:'2026-10-17'}));
});
async function connect(f,p){const response=await f.workspace.fetch(origin+'/connect',{method:'POST',headers:{authorization:'Bearer '+p.managementToken,'content-type':'application/json'},body:JSON.stringify({consentVersion:'workspace-v1'})});assert.equal(response.status,201);return response.json();}
async function native(t,f,p,scope='profile:read tasks:read habitica.tasks.write'){const response=await exchange(f,await authorization(f,p,scope));assert.equal(response.status,200);const token=(await response.json()).access_token;const c=new Client({name:'lifecycle-fixture',version:'1.0.0'});t.after(()=>c.close());await c.connect(new StreamableHTTPClientTransport(new URL(resource),{requestInit:{headers:{authorization:'Bearer '+token}},fetch:(url,init)=>f.mcp.fetch(String(url),init)}));return {c,token};}
test('synthetic MCP lifecycle uses reviewed identities, forbids cross-profile receipt access and dispatches no game writes',async t=>{
 let writes=0;const f=await fixture(t,{workspace:true,habitica:request=>{if(request.method!=='GET')writes++;return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:'Fixture A'}}});}});const p=await enrolled(f);const w=await connect(f,p);const {c}=await native(t,f,p);
 const tools=(await c.listTools()).tools;for(const name of ['skip_occurrence','pause_task','resume_task']){const tool=tools.find(t=>t.name===name);assert.equal(tool.annotations.destructiveHint,true);assert.deepEqual(tool._meta.securitySchemes,[{type:'oauth2',scopes:['profile:read','habitica.tasks.write']}]);}
 const invoke=async(name,args)=>{const result=await c.callTool({name,arguments:args});assert.notEqual(result.isError,true,JSON.stringify(result));return result.structuredContent;};
 const execute=async(operation,tool,args={})=>{const preview=await invoke('prepare_task_command',{requestId:crypto.randomUUID(),operation,...args});assert.equal(preview.mayAffectHabitica,false);const receipt=await invoke(tool,{commandId:preview.preparedCommandId});assert.equal(receipt.totalProjectionCount,0);assert.equal(receipt.confirmedProjectionCount,0);return {preview,receipt};};
 const created=await execute('createDaily','create_daily',{patch:{text:'Reviewed recurring lifecycle',recurrence:rule}});const taskId=created.receipt.taskId;
 const skipped=await execute('skipOccurrence','skip_occurrence',{taskId,occurrenceDate:'2026-10-17'});assert.equal(skipped.receipt.acceptedEventCount,1);assert.equal((await invoke('skip_occurrence',{commandId:skipped.preview.preparedCommandId})).commandId,skipped.receipt.commandId);
 await execute('completeTask','complete_task',{taskId,occurrenceDate:'2026-10-18'});
 const paused=await execute('pauseTask','pause_task',{taskId});assert.equal(paused.receipt.acceptedEventCount,0);assert.equal((await invoke('get_companion_task',{taskId})).task.state,'paused');
 await execute('updateTask','update_task',{taskId,patch:{recurrence:{...rule,startDate:'2026-10-20'}}});await execute('resumeTask','resume_task',{taskId});
 const task=(await invoke('get_companion_task',{taskId})).task;assert.equal(task.state,'active');assert.equal(task.recurrence.startDate,'2026-10-20');
 assert.equal((await invoke('get_task_command',{commandId:skipped.receipt.commandId})).acceptedEventCount,1);
 const exported=await(await f.workspace.fetch(origin+'/export',{headers:{authorization:'Bearer '+w.sessionToken}})).json();assert.equal(exported.document.occurrences.length,2);assert.equal(exported.document.completions.length,1);assert.equal(exported.commands.length,6);await journal(exported);assert.equal(writes,0);
 const readonly=await native(t,f,p,'profile:read tasks:read');const args={method:'POST',headers:{authorization:'Bearer '+readonly.token,'content-type':'application/json',accept:'application/json, text/event-stream'},body:JSON.stringify({jsonrpc:'2.0',id:999,method:'tools/call',params:{name:'pause_task',arguments:{commandId:paused.receipt.commandId}}})};const denied=await f.mcp.fetch(resource,args);assert.equal(denied.status,403);
 const binding=(await f.mf.getBindings('mcp')).WORKSPACE_SERVER;const bypass=await binding.fetch('https://workspace.internal/tool',{method:'POST',headers:{authorization:'Bearer '+readonly.token,'content-type':'application/json'},body:JSON.stringify({tool:'pause_task',arguments:{commandId:paused.receipt.commandId}})});assert.equal(bypass.status,403);
 const other=await connect(f,await enrolled(f,accounts[1]));const hidden=await f.workspace.fetch(origin+'/task-api',{method:'POST',headers:{authorization:'Bearer '+other.sessionToken,'content-type':'application/json'},body:JSON.stringify({tool:'get_task_command',arguments:{commandId:skipped.receipt.commandId}})});assert.equal((await hidden.json()).savedInCompanion,null);
});
