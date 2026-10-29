import {test} from 'node:test';
import assert from 'node:assert/strict';
import {fixture,enrolled,authorization,exchange,resource,accounts} from '../../chatgpt-auth/test/fixtures/oauth.mjs';
import {Client} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js';
import {StreamableHTTPClientTransport} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/streamableHttp.js';
const origin='http://localhost:8789';
async function connect(f,p){const response=await f.workspace.fetch(origin+'/connect',{method:'POST',headers:{authorization:`Bearer ${p.managementToken}`,'content-type':'application/json'},body:JSON.stringify({consentVersion:'workspace-v1'})});assert.equal(response.status,201);return response.json();}
async function call(f,w,tool,args){return f.workspace.fetch(origin+'/task-api',{method:'POST',headers:{authorization:`Bearer ${w.sessionToken}`,'content-type':'application/json'},body:JSON.stringify({tool,arguments:args})});}
async function client(t,f,p,scope='profile:read tasks:read habitica.tasks.write'){
 const response=await exchange(f,await authorization(f,p,scope));assert.equal(response.status,200);const token=(await response.json()).access_token;
 const transport=new StreamableHTTPClientTransport(new URL(resource),{requestInit:{headers:{authorization:`Bearer ${token}`}},fetch:(url,init)=>f.mcp.fetch(String(url),init)});const client=new Client({name:'task-fixture',version:'1.0.0'});t.after(()=>client.close());await client.connect(transport);return {client,token};
}
test('PWA previews and exact execution share one durable receipt and return separate projection state',async t=>{
 const f=await fixture(t,{workspace:true});const w=await connect(f,await enrolled(f));const input={requestId:crypto.randomUUID(),operation:'createTodo',patch:{text:'Reviewed task',priority:'urgent',habiticaDetails:{difficulty:0.1,attribute:'str',checklist:[]}},projectToHabitica:true};
 const prepare=await call(f,w,'prepare_task_command',input);assert.equal(prepare.status,200,await prepare.clone().text());const preview=await prepare.json();assert.equal(preview.savedInCompanion,false);assert.equal(preview.mayAffectHabitica,true);assert.ok(preview.command.id);assert.notEqual(preview.command.id,input.requestId);
 const listBefore=await(await call(f,w,'list_companion_tasks',{})).json();assert.equal(listBefore.tasks.length,0);
 const replay=await(await call(f,w,'prepare_task_command',input)).json();assert.equal(replay.command.id,preview.command.id);assert.deepEqual(replay.command,preview.command);
 assert.equal((await call(f,w,'prepare_task_command',{...input,patch:{text:'Reused'}})).status,409);
 assert.equal((await call(f,w,'complete_task',{commandId:preview.command.id})).status,409);
 const accepted=await call(f,w,'create_todo',{commandId:preview.command.id});assert.equal(accepted.status,200,await accepted.clone().text());const result=await accepted.json();assert.equal(result.savedInCompanion,true);assert.equal(result.habiticaProjectionStatus,'pending');assert.equal(result.taskText,'Reviewed task');assert.equal(result.refreshSucceeded,true);assert.equal(result.confirmedProjectionCount,0);assert.equal(result.outcomeUnknown,false);
 const duplicate=await(await call(f,w,'create_todo',{commandId:preview.command.id})).json();assert.equal(duplicate.commandId,result.commandId);assert.equal(duplicate.savedInCompanion,true);
 const list=await(await call(f,w,'list_companion_tasks',{query:'reviewed'})).json();assert.equal(list.tasks.length,1);assert.equal(list.tasks[0].id,result.taskId);
 const missing=await(await call(f,w,'get_companion_task',{taskId:crypto.randomUUID()})).json();assert.equal(missing.task,null);
 const preparedAfter=await(await call(f,w,'prepare_task_command',input)).json();assert.equal(preparedAfter.savedInCompanion,true);assert.equal(preparedAfter.receipt.commandId,result.commandId);
 const receipt=await(await call(f,w,'get_task_command',{commandId:result.commandId})).json();assert.equal(receipt.savedInCompanion,true);assert.equal(receipt.totalProjectionCount,1);
 assert.equal((await f.workspaceDb.prepare('SELECT COUNT(*) AS n FROM workspace_commands').first()).n,1);
});
test('native identity, whitelist, revision, expiry and tenant checks prevent unintended acceptance',async t=>{
 const f=await fixture(t,{workspace:true});const a=await connect(f,await enrolled(f));const b=await connect(f,await enrolled(f,accounts[1]));
 const input={requestId:crypto.randomUUID(),operation:'createTodo',patch:{text:'Scoped task'}};const prepared=await(await call(f,a,'prepare_task_command',input)).json();
 assert.equal((await call(f,b,'create_todo',{commandId:prepared.command.id})).status,409);
 assert.equal((await call(f,a,'prepare_task_command',{...input,requestId:crypto.randomUUID(),profileId:'other'})).status,400);
 assert.equal((await call(f,a,'prepare_task_command',{...input,requestId:crypto.randomUUID(),taskId:crypto.randomUUID()})).status,422);
 for(const bad of [{patch:{text:'x',priority:2}},{patch:{text:'x',habiticaDetails:{difficulty:4}}},{patch:{text:'x',habiticaDetails:{checklist:[{id:crypto.randomUUID(),text:'Invalid',completed:'false'}]}}}])assert.ok([400,422].includes((await call(f,a,'prepare_task_command',{...input,...bad,requestId:crypto.randomUUID()})).status));
 await f.workspaceDb.prepare("UPDATE workspace_preparations SET expires_at_utc='2000-01-01T00:00:00Z'").run();assert.equal((await call(f,a,'create_todo',{commandId:prepared.command.id})).status,409);
 const expired=await(await call(f,a,'prepare_task_command',input)).json();assert.equal(expired.expired,true);assert.equal(expired.command.id,prepared.command.id);
 assert.equal((await f.workspaceDb.prepare('SELECT COUNT(*) AS n FROM workspace_commands').first()).n,0);
});
test('MCP routes canonical preparation and acceptance through Workspace with step-up and independent grant enforcement',async t=>{
 const f=await fixture(t,{workspace:true});const p=await enrolled(f);const w=await connect(f,p);const {client:c,token}=await client(t,f,p);
 const tools=(await c.listTools()).tools;const complete=tools.find(t=>t.name==='complete_task');assert.equal(complete.annotations.destructiveHint,true);assert.deepEqual(complete._meta.securitySchemes,[{type:'oauth2',scopes:['profile:read','habitica.tasks.write']}]);
 const preview=await c.callTool({name:'prepare_task_command',arguments:{requestId:crypto.randomUUID(),operation:'createTodo',patch:{text:'MCP canonical task'},projectToHabitica:true}});assert.notEqual(preview.isError,true,JSON.stringify(preview));const prepared=preview.structuredContent;
 const result=await c.callTool({name:'create_todo',arguments:{commandId:prepared.preparedCommandId}});assert.notEqual(result.isError,true,JSON.stringify(result));assert.equal(result.structuredContent.savedInCompanion,true);assert.equal(result.structuredContent.habiticaProjectionStatus,'pending');
 const tasks=await c.callTool({name:'list_companion_tasks',arguments:{query:'MCP canonical'}});assert.equal(tasks.structuredContent.tasks[0].id,result.structuredContent.taskId);
 const pwaReceipt=await(await call(f,w,'get_task_command',{commandId:result.structuredContent.commandId})).json();assert.equal(pwaReceipt.savedInCompanion,true);assert.equal(pwaReceipt.commandId,result.structuredContent.commandId);
 const readonly=await client(t,f,p,'profile:read tasks:read');
 const denied=await f.mcp.fetch(resource,{method:'POST',headers:{authorization:`Bearer ${readonly.token}`,'content-type':'application/json',accept:'application/json, text/event-stream'},body:JSON.stringify({jsonrpc:'2.0',id:999,method:'tools/call',params:{name:'complete_task',arguments:{commandId:prepared.preparedCommandId}}})});assert.equal(denied.status,403);assert.match(denied.headers.get('www-authenticate'),/habitica.tasks.write/);
 const binding=(await f.mf.getBindings('mcp')).WORKSPACE_SERVER;
 const bypass=await binding.fetch('https://workspace.internal/tool',{method:'POST',headers:{authorization:`Bearer ${readonly.token}`,'content-type':'application/json'},body:JSON.stringify({tool:'complete_task',arguments:{commandId:prepared.preparedCommandId}})});assert.equal(bypass.status,403);
 assert.equal((await f.workspace.fetch(origin+'/tool',{method:'POST',headers:{authorization:`Bearer ${token}`}})).status,401);
 assert.equal(JSON.stringify(result).includes(accounts[0].apiToken),false);
});

test('prepared partial updates, Todo and Daily completions, exact-name selection and bounded Habit events remain canonical',async t=>{
 const f=await fixture(t,{workspace:true});const w=await connect(f,await enrolled(f));
 const execute=async(operation,args={})=>{const prep=await call(f,w,'prepare_task_command',{requestId:crypto.randomUUID(),operation,...args});assert.equal(prep.status,200,await prep.clone().text());const preview=await prep.json();const tool={createTodo:'create_todo',createDaily:'create_daily',createHabit:'create_habit',deleteTask:'delete_task',updateTask:'update_task',completeTask:'complete_task',scoreHabit:'score_habit'}[operation];const result=await call(f,w,tool,{commandId:preview.preparedCommandId});assert.equal(result.status,200,await result.clone().text());return result.json();};
 const a=await execute('createTodo',{patch:{text:'Same label'}});const b=await execute('createTodo',{patch:{text:'Same label'}});
 const matches=await(await call(f,w,'list_companion_tasks',{query:'Same label'})).json();assert.equal(matches.matchedCount,2);assert.notEqual(matches.tasks[0].id,matches.tasks[1].id);
 assert.equal((await call(f,w,'prepare_task_command',{requestId:crypto.randomUUID(),operation:'completeTask',query:'Same label'})).status,400);
 const completed=await execute('completeTask',{taskId:a.taskId});assert.equal(completed.acceptedEventCount,1);assert.equal(completed.totalProjectionCount,2);
 const updated=await execute('updateTask',{taskId:b.taskId,patch:{notes:'A partial note'}});assert.equal(updated.taskText,'Same label');
 const current=await(await call(f,w,'get_companion_task',{taskId:b.taskId.toUpperCase()})).json();assert.equal(current.task.notes,'A partial note');assert.equal(current.task.text,'Same label');
 const dailyId=(await execute('createDaily',{patch:{text:'Scheduled Daily',recurrence:{frequency:'daily',interval:1,startDate:'2026-10-15',timeZoneId:'UTC',weekdays:[],daysOfMonth:[]}}})).taskId;const habitId=(await execute('createHabit',{patch:{text:'Native Habit'}})).taskId;
 assert.equal((await call(f,w,'prepare_task_command',{requestId:crypto.randomUUID(),operation:'completeTask',taskId:habitId})).status,422);
 assert.equal((await call(f,w,'prepare_task_command',{requestId:crypto.randomUUID(),operation:'scoreHabit',taskId:habitId,direction:'up',count:11})).status,422);
 const daily=await execute('completeTask',{taskId:dailyId,occurrenceDate:'2026-10-15'});assert.equal(daily.acceptedEventCount,1);
 const up=await execute('scoreHabit',{taskId:habitId,direction:'up',count:10});assert.equal(up.acceptedEventCount,10);assert.equal(up.confirmedProjectionCount,0);
 const down=await execute('scoreHabit',{taskId:habitId,direction:'down',count:2});assert.equal(down.acceptedEventCount,2);
 const exported=await(await f.workspace.fetch(origin+'/export',{headers:{authorization:`Bearer ${w.sessionToken}`}})).json();assert.equal(exported.document.habitScores.length,12);assert.equal(exported.document.completions.length,2);assert.equal(exported.document.occurrences.length,1);
});

test('prepared receipts distinguish upstream uncertainty and quota from durable canonical acceptance without replay',async t=>{
 const {executeProfile}=await import('../src/executor.ts');
 for(const failure of ['confirmed','unknown','quota']){
  let writes=0,reads=0;const f=await fixture(t,{workspace:true,projection:true,executor:true,habitica:async request=>{
   if(request.url.includes('user?userFields=_id,profile'))return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:'Synthetic'}}});
   if(request.method==='GET'){reads++;return new Response('',{status:failure==='quota'?429:404,headers:failure==='quota'?{'retry-after':'120'}:{}});}writes++;if(failure==='confirmed'){const patch=await request.json();return Response.json({success:true,data:{id:'90000000-0000-4000-8000-000000000031',userId:request.headers.get('x-api-user'),text:patch.text,notes:patch.notes??'',type:'todo',alias:patch.alias,priority:1,attribute:'str',completed:false,checklist:[],group:{},challenge:{}}},{status:201});}return new Response('',{status:502});
  }});const p=await enrolled(f);const w=await connect(f,p);const preview=await(await call(f,w,'prepare_task_command',{requestId:crypto.randomUUID(),operation:'createTodo',patch:{text:'Durable despite projection failure'},projectToHabitica:true})).json();
  const saved=await(await call(f,w,'create_todo',{commandId:preview.preparedCommandId})).json();assert.equal(saved.savedInCompanion,true);assert.equal(saved.habiticaProjectionStatus,'pending');
  assert.equal((await f.workspace.fetch(origin+'/executor',{method:'POST',headers:{authorization:`Bearer ${w.sessionToken}`,'content-type':'application/json'},body:JSON.stringify({enabled:true,consentVersion:'executor-v1'})})).status,200);
  const wait=async()=>{await f.workspaceDb.prepare("UPDATE workspace_runtime SET next_network_at_utc='2000-01-01T00:00:00Z'").run();await f.db.prepare("UPDATE integration_projection_state SET next_automated_at_utc='2000-01-01T00:00:00Z'").run();};
  assert.equal(await executeProfile(await f.mf.getBindings('workspace'),p.profileId,{wait}),failure==='confirmed'?'applied':failure==='unknown'?'unknown':'refused');
  const receipt=await(await call(f,w,'get_task_command',{commandId:preview.preparedCommandId})).json();assert.equal(receipt.savedInCompanion,true);assert.equal(receipt.habiticaProjectionStatus,failure==='confirmed'?'confirmed':failure==='unknown'?'UnknownOutcome':'pending');assert.equal(receipt.outcomeUnknown,failure==='unknown');assert.equal(receipt.failurePosition,failure==='confirmed'?null:1);assert.equal(receipt.confirmedProjectionCount,failure==='confirmed'?1:0);
  const again=await(await call(f,w,'create_todo',{commandId:preview.preparedCommandId})).json();assert.deepEqual(again,receipt);assert.equal(reads,1);assert.equal(writes,failure==='quota'?0:1);assert.equal((await f.workspaceDb.prepare('SELECT COUNT(*) AS n FROM workspace_commands').first()).n,1);
 }
});

test('expiry cleanup removes only unaccepted previews and preserves accepted retry identities',async t=>{
 const f=await fixture(t,{workspace:true});const w=await connect(f,await enrolled(f));
 const one={requestId:crypto.randomUUID(),operation:'createTodo',patch:{text:'Retained accepted intent'}};
 const two={requestId:crypto.randomUUID(),operation:'createTodo',patch:{text:'Expired unaccepted notes',notes:'Remove this unaccepted preview'}};
 const a=await(await call(f,w,'prepare_task_command',one)).json();const b=await(await call(f,w,'prepare_task_command',two)).json();
 assert.equal((await call(f,w,'create_todo',{commandId:a.preparedCommandId})).status,200);
 const before=await f.workspaceDb.prepare('SELECT archive_bytes,store_version FROM workspaces').first();
 await f.workspaceDb.prepare("UPDATE workspace_preparations SET expires_at_utc='2000-01-01T00:00:00Z'").run();
 assert.equal((await f.workspace.scheduled({cron:'* * * * *'})).outcome,'ok');
 const kept=await f.workspaceDb.prepare('SELECT command_id FROM workspace_preparations').all();assert.deepEqual(kept.results.map(r=>r.command_id),[a.preparedCommandId]);
 const after=await f.workspaceDb.prepare('SELECT archive_bytes,store_version FROM workspaces').first();assert.ok(after.archive_bytes<before.archive_bytes);assert.equal(after.store_version,before.store_version+1);
 const replay=await(await call(f,w,'prepare_task_command',one)).json();assert.equal(replay.preparedCommandId,a.preparedCommandId);assert.equal(replay.savedInCompanion,true);
 assert.equal((await call(f,w,'create_todo',{commandId:b.preparedCommandId})).status,409);
});

test('unaccepted previews cannot survive a disconnected workspace access epoch',async t=>{
 const f=await fixture(t,{workspace:true});const p=await enrolled(f);const w=await connect(f,p);const input={requestId:crypto.randomUUID(),operation:'createTodo',patch:{text:'Old connection preview'}};
 const old=await(await call(f,w,'prepare_task_command',input)).json();
 await f.workspace.fetch(origin+'/disconnect',{method:'POST',headers:{authorization:`Bearer ${w.sessionToken}`},body:'{}'});
 const next=await connect(f,p);
 assert.equal((await call(f,next,'create_todo',{commandId:old.preparedCommandId})).status,409);
 assert.equal((await call(f,next,'prepare_task_command',input)).status,409);
 assert.equal((await f.workspaceDb.prepare('SELECT COUNT(*) AS n FROM workspace_commands').first()).n,0);
});


test('native Daily and retained legacy Habit archival preserve receipts and history without upstream effects',async t=>{
 let writes=0;const f=await fixture(t,{workspace:true,habitica:async request=>{
  if(request.method!=='GET')writes++;
  return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:'Fixture A'}}});
 }});const p=await enrolled(f);const w=await connect(f,p);const other=await connect(f,await enrolled(f,accounts[1]));const {client:c}=await client(t,f,p);
 const tools=(await c.listTools()).tools;
 for(const name of ['create_daily','create_habit','delete_task'])assert.deepEqual(tools.find(t=>t.name===name)._meta.securitySchemes,[{type:'oauth2',scopes:['profile:read','habitica.tasks.write']}]);
 assert.equal(tools.find(t=>t.name==='delete_task').annotations.destructiveHint,true);
 const execute=async(operation,tool,args={})=>{
  const input={requestId:crypto.randomUUID(),operation,...args};const preview=await c.callTool({name:'prepare_task_command',arguments:input});assert.notEqual(preview.isError,true,JSON.stringify(preview));const prepared=preview.structuredContent;assert.equal(prepared.mayAffectHabitica,false);
  const again=await c.callTool({name:'prepare_task_command',arguments:input});assert.equal(again.structuredContent.preparedCommandId,prepared.preparedCommandId);
  const result=await c.callTool({name:tool,arguments:{commandId:prepared.preparedCommandId}});assert.notEqual(result.isError,true,JSON.stringify(result));assert.equal(result.structuredContent.habiticaProjectionStatus,'notRequested');assert.equal(result.structuredContent.totalProjectionCount,0);
  const duplicate=await c.callTool({name:tool,arguments:{commandId:prepared.preparedCommandId}});assert.deepEqual(duplicate.structuredContent,result.structuredContent);
  const receipt=await c.callTool({name:'get_task_command',arguments:{commandId:prepared.preparedCommandId}});assert.deepEqual(receipt.structuredContent,result.structuredContent);
  assert.equal((await call(f,other,tool,{commandId:prepared.preparedCommandId})).status,409);
  return result.structuredContent;
 };
 const {seedLegacyCommand}=await import('./fixtures/legacy-todo.mjs');
 const daily={taskId:crypto.randomUUID()};await seedLegacyCommand(f,{id:crypto.randomUUID(),workspaceId:w.workspaceId,expectedRevision:1,operation:'createDaily',taskId:daily.taskId,patch:{text:'Retained native Daily',deadline:'2026-10-18'}});
 const habit={taskId:crypto.randomUUID()};await seedLegacyCommand(f,{id:crypto.randomUUID(),workspaceId:w.workspaceId,expectedRevision:2,operation:'createHabit',taskId:habit.taskId,patch:{text:'Retained native Habit'}});
 const read=await(await call(f,w,'get_companion_task',{taskId:daily.taskId})).json();assert.equal(read.task.kind,'daily');assert.equal(read.task.recurrence,null);assert.equal(read.task.deadline,'2026-10-18');
 await seedLegacyCommand(f,{id:crypto.randomUUID(),workspaceId:w.workspaceId,expectedRevision:3,operation:'completeTask',taskId:daily.taskId,occurrenceDate:'2026-10-17'});
 const legacyScoreId=crypto.randomUUID();await seedLegacyCommand(f,{id:legacyScoreId,workspaceId:w.workspaceId,expectedRevision:4,operation:'scoreHabit',taskId:habit.taskId,direction:'down',count:2});
 const recoveredLegacy=await(await call(f,w,'score_habit',{commandId:legacyScoreId})).json();assert.equal(recoveredLegacy.habiticaProjectionStatus,'notRequested');assert.equal(recoveredLegacy.totalProjectionCount,0);
 for(const operation of ['createDaily','createHabit']){
  if(operation==='createDaily')assert.equal((await call(f,w,'prepare_task_command',{requestId:crypto.randomUUID(),operation,patch:{text:'Unsupported projection'},projectToHabitica:true})).status,422);
  assert.equal((await call(f,w,'prepare_task_command',{requestId:crypto.randomUUID(),operation,taskId:crypto.randomUUID(),patch:{text:'Caller identity'}})).status,422);
 }
 assert.equal((await call(f,w,'prepare_task_command',{requestId:crypto.randomUUID(),operation:'deleteTask',taskId:daily.taskId,expectedRevision:1})).status,409);
 assert.equal((await call(f,w,'prepare_task_command',{requestId:crypto.randomUUID(),operation:'deleteTask',taskId:daily.taskId,projectToHabitica:true})).status,422);
 await execute('deleteTask','delete_task',{taskId:daily.taskId});await execute('deleteTask','delete_task',{taskId:habit.taskId});
 assert.equal((await(await call(f,w,'get_companion_task',{taskId:daily.taskId})).json()).task,null);
 assert.equal((await(await call(f,w,'list_companion_tasks',{})).json()).tasks.length,0);
 const exported=await(await f.workspace.fetch(origin+'/export',{headers:{authorization:`Bearer ${w.sessionToken}`}})).json();assert.equal(exported.document.tasks.filter(t=>t.isArchived).length,2);assert.equal(exported.document.completions.length,1);assert.equal(exported.document.habitScores.length,2);assert.equal(exported.commands.length,6);assert.equal(writes,0);
 assert.equal((await call(f,w,'prepare_task_command',{requestId:crypto.randomUUID(),operation:'updateTask',taskId:daily.taskId,patch:{text:'Resurrect'}})).status,422);
 const readonly=await client(t,f,p,'profile:read tasks:read');
 for(const name of ['create_daily','create_habit','delete_task']){
  const denied=await f.mcp.fetch(resource,{method:'POST',headers:{authorization:`Bearer ${readonly.token}`,'content-type':'application/json',accept:'application/json, text/event-stream'},body:JSON.stringify({jsonrpc:'2.0',id:999,method:'tools/call',params:{name,arguments:{commandId:crypto.randomUUID()}}})});assert.equal(denied.status,403);
 }
});

test('archival refuses linked or pending projections and acceptance rechecks fresh revisions',async t=>{
 const f=await fixture(t,{workspace:true});const w=await connect(f,await enrolled(f));
 const prepare=async args=>call(f,w,'prepare_task_command',{requestId:crypto.randomUUID(),...args});
 const created=await(await prepare({operation:'createTodo',patch:{text:'Pending projected task'},projectToHabitica:true})).json();
 await call(f,w,'create_todo',{commandId:created.preparedCommandId});
 assert.equal((await prepare({operation:'deleteTask',taskId:created.command.taskId})).status,422);
 const {seedLegacyCommand}=await import('./fixtures/legacy-todo.mjs');const taskId=crypto.randomUUID();await seedLegacyCommand(f,{id:crypto.randomUUID(),workspaceId:w.workspaceId,expectedRevision:2,operation:'createDaily',taskId,patch:{text:'Retained native Daily'}});const daily={command:{taskId}};
 const deletion=await(await prepare({operation:'deleteTask',taskId:daily.command.taskId})).json();
 const edit=await(await prepare({operation:'planTask',taskId:daily.command.taskId,patch:{planning:{plannedDate:'2026-10-21'}}})).json();await call(f,w,'plan_task',{commandId:edit.preparedCommandId});
 assert.equal((await call(f,w,'delete_task',{commandId:deletion.preparedCommandId})).status,409);
 assert.equal((await(await call(f,w,'get_companion_task',{taskId:daily.command.taskId})).json()).task.isArchived,false);
});

test('new Todo preparation requires game intent for omitted false and true while execution stays disabled',async t=>{
 let writes=0;const f=await fixture(t,{workspace:true,habitica:request=>{if(request.method!=='GET')writes++;return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:'Fixture'}}});}});
 const w=await connect(f,await enrolled(f));
 for(const flag of [undefined,false,true]){
  const input={requestId:crypto.randomUUID(),operation:'createTodo',patch:{text:'Required Todo'},...(flag===undefined?{}:{projectToHabitica:flag})};
  const preview=await(await call(f,w,'prepare_task_command',input)).json();assert.equal(preview.command.projectToHabitica,true);assert.equal(preview.mayAffectHabitica,true);
  const saved=await(await call(f,w,'create_todo',{commandId:preview.preparedCommandId})).json();assert.equal(saved.habiticaProjectionStatus,'pending');assert.equal(saved.totalProjectionCount,1);assert.equal(saved.confirmedProjectionCount,0);
  const replay=await(await call(f,w,'prepare_task_command',input)).json();assert.equal(replay.preparedCommandId,preview.preparedCommandId);assert.deepEqual(replay.receipt,saved);
 }
 assert.equal(writes,0);const d=(await(await f.workspace.fetch(origin+'/export',{headers:{authorization:`Bearer ${w.sessionToken}`}})).json()).document;
 const raw={id:crypto.randomUUID(),workspaceId:w.workspaceId,expectedRevision:d.revision,operation:'createTodo',taskId:crypto.randomUUID(),patch:{text:'Cannot bypass'},projectToHabitica:false};
 const denied=await f.workspace.fetch(origin+'/commands',{method:'POST',headers:{authorization:`Bearer ${w.sessionToken}`,'content-type':'application/json'},body:JSON.stringify(raw)});assert.equal(denied.status,422);assert.match(await denied.text(),/todo_projection_required/);
});

test('accepted legacy false commands recover verbatim but old unaccepted previews cannot bypass the prospective policy',async t=>{
 const {seedLegacyCommand}=await import('./fixtures/legacy-todo.mjs');const f=await fixture(t,{workspace:true}),w=await connect(f,await enrolled(f));
 const raw={id:crypto.randomUUID(),workspaceId:w.workspaceId,expectedRevision:1,operation:'createTodo',taskId:crypto.randomUUID(),patch:{text:'Retained legacy'},projectToHabitica:false};
 const legacy=await seedLegacyCommand(f,raw),headers={authorization:`Bearer ${w.sessionToken}`,'content-type':'application/json'};
 const recovered=await f.workspace.fetch(origin+'/commands',{method:'POST',headers,body:JSON.stringify(raw)});assert.equal(recovered.status,200);assert.deepEqual((await recovered.json()).entry,legacy.entry);
 const changed=await f.workspace.fetch(origin+'/commands',{method:'POST',headers,body:JSON.stringify({...raw,projectToHabitica:true})});assert.equal(changed.status,409);
 const input={requestId:crypto.randomUUID(),operation:'createTodo',patch:{text:'Old preview'}};
 const preview=await(await call(f,w,'prepare_task_command',input)).json();const old={...preview.command,projectToHabitica:false};
 await f.workspaceDb.prepare('UPDATE workspace_preparations SET command_json=? WHERE command_id=?').bind(JSON.stringify(old),preview.preparedCommandId).run();
 const refused=await call(f,w,'prepare_task_command',input);assert.equal(refused.status,200);const expired=await refused.json();assert.equal(expired.expired,true);assert.deepEqual(expired.command,old);
 assert.equal((await call(f,w,'create_todo',{commandId:preview.preparedCommandId})).status,422);
 assert.equal((await f.workspaceDb.prepare('SELECT COUNT(*) AS n FROM workspace_commands').first()).n,1);
 const completion=await(await call(f,w,'prepare_task_command',{requestId:crypto.randomUUID(),operation:'completeTask',taskId:raw.taskId})).json();
 const saved=await(await call(f,w,'complete_task',{commandId:completion.preparedCommandId})).json();assert.equal(saved.totalProjectionCount,2);assert.equal(saved.confirmedProjectionCount,0);
 const exported=await(await f.workspace.fetch(origin+'/export',{headers})).json();assert.deepEqual(exported.commands[1].effects,['create','scoreUp']);assert.deepEqual(exported.commands[0],legacy.entry);
});


test('accepted legacy preview uses original effects even after the task acquires a pending mapping',async t=>{
 const {seedLegacyCommand}=await import('./fixtures/legacy-todo.mjs');const f=await fixture(t,{workspace:true}),w=await connect(f,await enrolled(f));
 const input={requestId:crypto.randomUUID(),operation:'createTodo',patch:{text:'Legacy prepared Todo'},projectToHabitica:false};
 const prepared=await(await call(f,w,'prepare_task_command',input)).json(),old={...prepared.command,projectToHabitica:false};
 await f.workspaceDb.prepare('UPDATE workspace_preparations SET command_json=? WHERE command_id=?').bind(JSON.stringify(old),prepared.preparedCommandId).run();
 const legacy=await seedLegacyCommand(f,old);
 const completion=await(await call(f,w,'prepare_task_command',{requestId:crypto.randomUUID(),operation:'completeTask',taskId:old.taskId})).json();
 assert.equal((await call(f,w,'complete_task',{commandId:completion.preparedCommandId})).status,200);
 const recovered=await(await call(f,w,'prepare_task_command',input)).json();
 assert.equal(recovered.savedInCompanion,true);assert.equal(recovered.mayAffectHabitica,false);assert.equal(recovered.command.projectToHabitica,false);
 assert.equal(recovered.receipt.totalProjectionCount,0);assert.equal(recovered.receipt.habiticaProjectionStatus,'notRequested');
 assert.deepEqual((await(await call(f,w,'get_task_command',{commandId:legacy.entry.command.id})).json()),recovered.receipt);
});

test('reviewed projection-only repair creates no second native task and recovers its original receipt over PWA and MCP',async t=>{
 const {seedLegacyCommand}=await import('./fixtures/legacy-todo.mjs');const f=await fixture(t,{workspace:true}),p=await enrolled(f),w=await connect(f,p),{client:c}=await client(t,f,p);
 const taskId=crypto.randomUUID();await seedLegacyCommand(f,{id:crypto.randomUUID(),workspaceId:w.workspaceId,expectedRevision:1,operation:'createTodo',taskId,patch:{text:'Unchanged legacy task',notes:'Keep these notes',priority:'high'}});
 const args={requestId:crypto.randomUUID(),operation:'ensureProjection',taskId,projectToHabitica:true};
 const prepared=await c.callTool({name:'prepare_task_command',arguments:args});assert.notEqual(prepared.isError,true,JSON.stringify(prepared));const preview=prepared.structuredContent;assert.equal(preview.command.patch,null);assert.equal(preview.mayAffectHabitica,true);
 const result=await c.callTool({name:'ensure_task_projection',arguments:{commandId:preview.preparedCommandId}});assert.notEqual(result.isError,true,JSON.stringify(result));assert.equal(result.structuredContent.totalProjectionCount,1);assert.equal(result.structuredContent.acceptedEventCount,0);
 const recovered=await(await call(f,w,'ensure_task_projection',{commandId:preview.preparedCommandId})).json();assert.deepEqual(recovered,result.structuredContent);
 const exported=await(await f.workspace.fetch(origin+'/export',{headers:{authorization:`Bearer ${w.sessionToken}`}})).json();assert.equal(exported.document.tasks.length,1);assert.equal(exported.document.tasks[0].text,'Unchanged legacy task');assert.equal(exported.document.tasks[0].notes,'Keep these notes');assert.equal(exported.document.tasks[0].priority,'high');assert.equal(exported.document.completions.length,0);assert.deepEqual(exported.commands[1].effects,['create']);
 const again=await call(f,w,'prepare_task_command',{...args,requestId:crypto.randomUUID()});assert.equal(again.status,409);assert.match(await again.text(),/projection_already_pending/);
 assert.equal((await(await call(f,w,'prepare_task_command',args)).json()).preparedCommandId,preview.preparedCommandId);
});
