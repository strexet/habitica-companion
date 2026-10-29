import {seedLegacyCommand} from './fixtures/legacy-todo.mjs';
import {test} from 'node:test';import assert from 'node:assert/strict';import {readFile} from 'node:fs/promises';
import {workspaceCommand,commandHash} from '../../shared/workspace-commands.ts';import {emptyWorkspace,workspaceDocument} from '../../shared/workspace-domain.ts';import {accept} from '../src/commands.ts';import {journal} from '../src/journal.ts';
import {fixture,enrolled,authorization,exchange,resource,accounts} from '../../chatgpt-auth/test/fixtures/oauth.mjs';
import {Client} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js';import {StreamableHTTPClientTransport} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/streamableHttp.js';
const hashes=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/todo-restore-command-v5.json',import.meta.url),'utf8'));
const now='2026-10-18T12:00:00Z',origin='http://localhost:8789';
const command=(d,operation,extra={})=>workspaceCommand({id:crypto.randomUUID(),workspaceId:d.id,expectedRevision:d.revision,operation,taskId:d.tasks[0]?.id??crypto.randomUUID(),...extra});
const restore=(d,extra={})=>command(d,'restoreTodo',{patch:{restoreTodo:{completionId:d.completions.at(-1).id}},...extra});
async function seed(){let d=emptyWorkspace();d=(await accept(d,command(d,'createTodo',{patch:{text:'Retained work',notes:'Original notes',deadline:'2026-10-21'}}),now)).document;const legacy=(await accept(d,command(d,'completeTask'),now)).document;delete legacy.activityCoverage;return legacy;}
for(const value of hashes.cases)test('portable restore v5 identity: '+value.name,async()=>assert.equal(await commandHash(workspaceCommand(value.command)),value.payloadHash));
test('repeated native restore/complete cycles preserve history order and portable causal receipts',async()=>{
 let d=await seed();const original=structuredClone(d.tasks[0]),entries=[],events=[];
 const apply=async c=>{const result=await accept(d,c,now);d=result.document;entries.push(result.entry);events.push(result.event);return result;};
 const first=restore(d);await apply(first);assert.deepEqual(d.tasks[0],{...original,revision:d.revision,state:'active'});assert.equal(d.completions.length,1);assert.equal(d.restorations.length,1);
 await apply(command(d,'completeTask'));await assert.rejects(accept(d,restore(d,{patch:first.patch}),now),e=>e.code==='completion_already_restored');await apply(restore(d));
 assert.equal(d.completions.length,2);assert.equal(d.restorations.length,2);for(const entry of entries){assert.deepEqual(entry.effects,[]);assert.equal(entry.projectionState,'notRequested');}
 const archive={document:d,storeVersion:d.revision,commands:entries,events};await journal(archive);
 for(const change of [row=>row.taskSnapshot=null,row=>row.taskSnapshot.state='completed',row=>{row.effects=['scoreUp'];row.projectionState='pending';},row=>{row.acceptedAtUtc='2026-10-18T12:01:00Z';row.taskSnapshot.updatedAtUtc=row.acceptedAtUtc;}]){const broken=structuredClone(archive);change(broken.commands[0]);await assert.rejects(journal(broken));}
});
test('invalid targets patches lifecycle and retained game identity including archived mappings are refused',async()=>{
 const d=await seed();
 for(const patch of [{},{restoreTodo:{}},{restoreTodo:{completionId:'00000000-0000-0000-0000-000000000000'}},{restoreTodo:{completionId:d.completions[0].id,extra:true}},{restoreTodo:{completionId:d.completions[0].id},text:'Mixed'}])assert.throws(()=>restore(d,{patch}));
 for(const extra of [{operation:'updateTask'},{count:2},{direction:'up'},{occurrenceDate:'2026-10-18'}])assert.throws(()=>restore(d,extra));
 await assert.rejects(accept(d,restore(d,{patch:{restoreTodo:{completionId:crypto.randomUUID()}}}),now),e=>e.code==='completion_unavailable');await assert.rejects(accept(d,restore(d,{projectToHabitica:true}),now),e=>e.code==='unsupported_projection');
 const active=(await accept(d,restore(d),now)).document;await assert.rejects(accept(active,restore(active),now),e=>e.code==='todo_not_completed');
 const archived=structuredClone(d);archived.tasks[0].isArchived=true;await assert.rejects(accept(archived,restore(archived),now),e=>e.code==='task_unavailable');
 for(const state of ['unlinked','linked','pending','unknownOutcome','externalConflict'])for(const isArchived of [false,true]){const mapped=structuredClone(d);mapped.projections=[{id:crypto.randomUUID(),revision:d.revision,taskId:d.tasks[0].id,state,habiticaTaskId:crypto.randomUUID(),isArchived}];await assert.rejects(accept(workspaceDocument(mapped),restore(mapped),now),e=>e.code==='unsupported_projection');}
 for(const operation of ['createDaily','createHabit']){const extra=(await accept(d,command(d,operation,{taskId:crypto.randomUUID(),patch:{text:'Recurring'}}),now)).document;await assert.rejects(accept(extra,restore(extra,{taskId:extra.tasks[1].id}),now),e=>e.code==='todo_not_completed');}
 const harmless=structuredClone(d);harmless.projections=[{id:crypto.randomUUID(),revision:d.revision,taskId:d.tasks[0].id,state:'unlinked',habiticaTaskId:null,isArchived:true}];const kept=await accept(harmless,restore(harmless),now);assert.deepEqual(kept.document.projections,workspaceDocument(harmless).projections);
 const foreign=(await accept(d,command(d,'createTodo',{taskId:crypto.randomUUID(),patch:{text:'Other'}}),now)).document;
 const completed=(await accept(foreign,command(foreign,'completeTask',{taskId:foreign.tasks[1].id}),now)).document;await assert.rejects(accept(completed,restore(completed),now),e=>e.code==='completion_unavailable');
});
async function connect(f,p){const r=await f.workspace.fetch(origin+'/connect',{method:'POST',headers:{authorization:'Bearer '+p.managementToken,'content-type':'application/json'},body:JSON.stringify({consentVersion:'workspace-v1'})});assert.equal(r.status,201);return r.json();}
async function native(t,f,p,scope='profile:read tasks:read habitica.tasks.write'){const r=await exchange(f,await authorization(f,p,scope));assert.equal(r.status,200);const token=(await r.json()).access_token,c=new Client({name:'restore-fixture',version:'1.0.0'});t.after(()=>c.close());await c.connect(new StreamableHTTPClientTransport(new URL(resource),{requestInit:{headers:{authorization:'Bearer '+token}},fetch:(url,init)=>f.mcp.fetch(String(url),init)}));return {c,token};}
const call=(f,w,name,args)=>f.workspace.fetch(origin+'/task-api',{method:'POST',headers:{authorization:'Bearer '+w.sessionToken,'content-type':'application/json'},body:JSON.stringify({tool:name,arguments:args})});
test('synthetic MCP restores exact history once per cycle and protects private binding and readonly scope',async t=>{
 let writes=0;const f=await fixture(t,{workspace:true,habitica:request=>{if(request.method!=='GET')writes++;return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:'Fixture A'}}});}}),p=await enrolled(f),w=await connect(f,p),{c}=await native(t,f,p);
 const invoke=async(name,args)=>{const r=await c.callTool({name,arguments:args});assert.notEqual(r.isError,true,JSON.stringify(r));return r.structuredContent;};
 const tools=(await c.listTools()).tools,tool=tools.find(row=>row.name==='restore_todo');assert.equal(tool.annotations.destructiveHint,true);assert.equal(tool.annotations.readOnlyHint,false);assert.deepEqual(tool._meta.securitySchemes,[{type:'oauth2',scopes:['profile:read','habitica.tasks.write']}]);assert.deepEqual(tools.find(row=>row.name==='prepare_task_command').inputSchema.properties.patch.properties.restoreTodo.required,['completionId']);
 const prepare=async args=>invoke('prepare_task_command',{requestId:crypto.randomUUID(),...args});
 const taskId=crypto.randomUUID();
 await seedLegacyCommand(f,{id:crypto.randomUUID(),workspaceId:w.workspaceId,expectedRevision:1,operation:'createTodo',taskId,patch:{text:'Retained legacy restoration'}});
 await seedLegacyCommand(f,{id:crypto.randomUUID(),workspaceId:w.workspaceId,expectedRevision:2,operation:'completeTask',taskId});
 const finish=async()=>{const p=await prepare({operation:'completeTask',taskId});return invoke('complete_task',{commandId:p.preparedCommandId});};
 const history=await invoke('get_completed_history',{taskId}),completionId=history.items[0].eventId,args={requestId:crypto.randomUUID(),operation:'restoreTodo',taskId,patch:{restoreTodo:{completionId}}};
 const preview=await invoke('prepare_task_command',args);assert.equal(preview.mayAffectHabitica,false);assert.equal(preview.command.patch.restoreTodo.completionId,completionId);assert.equal((await invoke('prepare_task_command',args)).preparedCommandId,preview.preparedCommandId);
 const accepted=await invoke('restore_todo',{commandId:preview.preparedCommandId});assert.equal(accepted.acceptedEventCount,1);assert.equal(accepted.totalProjectionCount,0);assert.equal(accepted.habiticaProjectionStatus,'notRequested');assert.equal((await invoke('get_companion_task',{taskId})).task.state,'active');
 await finish();const next=await invoke('get_completed_history',{taskId});assert.equal(next.items.length,2);assert.ok(next.items.find(row=>row.eventId===completionId).restoration);assert.equal(next.items.filter(row=>!row.restoration).length,1);
 assert.equal((await invoke('restore_todo',{commandId:preview.preparedCommandId})).commandId,accepted.commandId);assert.equal((await invoke('get_companion_task',{taskId})).task.state,'completed');assert.equal((await invoke('get_task_command',{commandId:accepted.commandId})).acceptedEventCount,1);
 const refused=await c.callTool({name:'prepare_task_command',arguments:{requestId:crypto.randomUUID(),operation:'restoreTodo',taskId,patch:{restoreTodo:{completionId:next.items.find(row=>!row.restoration).eventId}}}});assert.equal(refused.isError,true);
 const exported=await(await f.workspace.fetch(origin+'/export',{headers:{authorization:'Bearer '+w.sessionToken}})).json();await journal(exported);assert.equal(exported.document.completions.length,2);assert.equal(exported.document.restorations.length,1);assert.equal(exported.commands.length,4);assert.equal(writes,0);
 const other=await connect(f,await enrolled(f,accounts[1]));assert.equal((await call(f,other,'restore_todo',{commandId:accepted.commandId})).status,409);assert.equal((await(await call(f,other,'get_completed_history',{taskId})).json()).items.length,0);
 const readonly=await native(t,f,p,'profile:read tasks:read'),binding=(await f.mf.getBindings('mcp')).WORKSPACE_SERVER;
 const request={method:'POST',headers:{authorization:'Bearer '+readonly.token,'content-type':'application/json',accept:'application/json, text/event-stream'},body:JSON.stringify({jsonrpc:'2.0',id:999,method:'tools/call',params:{name:'restore_todo',arguments:{commandId:accepted.commandId}}})};assert.equal((await f.mcp.fetch(resource,request)).status,403);
 assert.equal((await binding.fetch('https://workspace.internal/tool',{method:'POST',headers:{authorization:'Bearer '+readonly.token,'content-type':'application/json'},body:JSON.stringify({tool:'restore_todo',arguments:{commandId:accepted.commandId}})})).status,403);
});
