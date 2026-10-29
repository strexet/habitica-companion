import {test} from 'node:test';import assert from 'node:assert/strict';
import {fixture,enrolled,accounts} from '../../chatgpt-auth/test/fixtures/oauth.mjs';import {executeProfile} from '../src/executor.ts';
const origin='http://localhost:8789';
async function connect(f,p){const r=await f.workspace.fetch(origin+'/connect',{method:'POST',headers:{authorization:`Bearer ${p.managementToken}`,'content-type':'application/json'},body:JSON.stringify({consentVersion:'workspace-v1'})});assert.equal(r.status,201);return r.json();}
async function call(f,w,tool,args){return f.workspace.fetch(origin+'/task-api',{method:'POST',headers:{authorization:`Bearer ${w.sessionToken}`,'content-type':'application/json'},body:JSON.stringify({tool,arguments:args})});}
for(const [up,down,unknown] of [[true,true,false],[true,false,false],[false,true,false],[false,false,false],[true,false,true]])test(`Habit gateway ${up}/${down} unknown=${unknown} creates the right kind and preserves original scoring receipts`,async t=>{
 let remote=null,creates=0,scores=0;
 const f=await fixture(t,{workspace:true,projection:true,executor:true,habitica:async request=>{
  const user=request.headers.get('x-api-user');if(request.url.includes('user?userFields=_id,profile'))return Response.json({success:true,data:{_id:user,profile:{name:'Fixture'}}});
  if(request.method==='GET')return remote?Response.json({success:true,data:remote}):new Response('',{status:404});
  if(request.url.endsWith('/tasks/user')){
   creates++;const patch=await request.json();assert.equal(patch.type,'habit');assert.equal(patch.up,up);assert.equal(patch.down,down);assert.equal(patch.date,undefined);assert.equal(patch.checklist,undefined);
   remote={...patch,id:crypto.randomUUID(),userId:user,priority:patch.priority??1,attribute:patch.attribute??'str',notes:patch.notes??'',group:{},challenge:{}};
   return unknown?new Response('',{status:502}):Response.json({success:true,data:remote},{status:201});
  }
  assert.match(request.url,/\/score\/(up|down)$/);assert.equal(request.url.endsWith('/up')?up:down,true);scores++;
  return Response.json({success:true,data:{delta:request.url.endsWith('/up')?1:-1}});
 }}),p=await enrolled(f),w=await connect(f,p);
 const preview=await(await call(f,w,'prepare_task_command',{requestId:crypto.randomUUID(),operation:'createHabit',patch:{text:'Typed Habit',notes:'Synthetic notes',habiticaDetails:{up,down,difficulty:1.5,attribute:'int'}}})).json();assert.equal(preview.command.projectToHabitica,true);
 const accepted=await(await call(f,w,'create_habit',{commandId:preview.preparedCommandId})).json();assert.equal(accepted.totalProjectionCount,1);assert.equal(accepted.acceptedEventCount,0);
 await f.workspace.fetch(origin+'/executor',{method:'POST',headers:{authorization:`Bearer ${w.sessionToken}`,'content-type':'application/json'},body:JSON.stringify({enabled:true,consentVersion:'executor-v1'})});
 const reset=async()=>{await f.workspaceDb.prepare("UPDATE workspace_runtime SET next_network_at_utc='2000-01-01T00:00:00Z'").run();await f.db.prepare("UPDATE integration_projection_state SET next_automated_at_utc='2000-01-01T00:00:00Z'").run();};
 const env=await f.mf.getBindings('workspace');await executeProfile(env,p.profileId,{wait:reset});
 if(unknown){
  const uncertain=await(await call(f,w,'get_task_command',{commandId:preview.preparedCommandId})).json();assert.equal(uncertain.outcomeUnknown,true);
  const duplicate=await(await call(f,w,'create_habit',{commandId:preview.preparedCommandId})).json();assert.deepEqual(duplicate,uncertain);assert.equal(creates,1);
  await reset();assert.equal(await executeProfile(env,p.profileId,{wait:reset}),'unknown');assert.equal(creates,1);
  // Synthetic explicit review reuses the saved private attempt. The executor itself only looks up original receipts.
  const runtime=await f.workspaceDb.prepare('SELECT prepared_request_json FROM workspace_runtime WHERE profile_id=?').bind(p.profileId).first(),original=JSON.parse(runtime.prepared_request_json);
  const reconciled=await f.projection.fetch('https://projection.internal/operation',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({...original,operation:'reconcile',arguments:{operation:original.operation,arguments:original.arguments},automated:false})});assert.equal(reconciled.status,200);assert.equal((await reconciled.json()).kind,'applied');
  await reset();assert.equal(await executeProfile(env,p.profileId,{wait:reset}),'recovered');assert.equal(creates,1);
 }
 const receipt=await(await call(f,w,'get_task_command',{commandId:preview.preparedCommandId})).json();assert.equal(receipt.habiticaProjectionStatus,'confirmed');assert.equal(receipt.confirmedProjectionCount,1);assert.equal(creates,1);assert.equal(scores,0);
 for(const direction of ['up','down']){
  const r=await call(f,w,'prepare_task_command',{requestId:crypto.randomUUID(),operation:'scoreHabit',taskId:accepted.taskId,direction,count:1});
  if(direction==='up'?!up:!down){assert.equal(r.status,422);continue;}
  assert.equal(r.status,200,await r.clone().text());const score=await r.json();assert.equal((await call(f,w,'score_habit',{commandId:score.preparedCommandId})).status,200);await reset();await executeProfile(env,p.profileId,{wait:reset});
  const done=await(await call(f,w,'get_task_command',{commandId:score.preparedCommandId})).json();assert.equal(done.confirmedProjectionCount,1);assert.equal(done.totalProjectionCount,1);
 }
 assert.equal(creates,1);assert.equal(scores,Number(up)+Number(down));
 const other=await connect(f,await enrolled(f,accounts[1]));assert.equal((await call(f,other,'create_habit',{commandId:preview.preparedCommandId})).status,409);
});

test('revoked Workspace consent leaves accepted Habit creation pending without a remote write',async t=>{
 let writes=0;const f=await fixture(t,{workspace:true,projection:true,executor:true,habitica:request=>{if(request.method!=='GET')writes++;return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:'Fixture'}}});}}),p=await enrolled(f),w=await connect(f,p);
 const preview=await(await call(f,w,'prepare_task_command',{requestId:crypto.randomUUID(),operation:'createHabit',patch:{text:'Paused before dispatch'}})).json();assert.equal((await call(f,w,'create_habit',{commandId:preview.preparedCommandId})).status,200);
 await f.workspace.fetch(origin+'/disconnect',{method:'POST',headers:{authorization:`Bearer ${w.sessionToken}`},body:'{}'});assert.equal(await executeProfile(await f.mf.getBindings('workspace'),p.profileId),'disabled');assert.equal(writes,0);
});

test('MCP SDK advertises and accepts explicit Habit direction flags without an immediate game write',async t=>{
 const {authorization,exchange,resource}=await import('../../chatgpt-auth/test/fixtures/oauth.mjs');
 const {Client}=await import('../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js');const {StreamableHTTPClientTransport}=await import('../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/streamableHttp.js');
 const f=await fixture(t,{workspace:true}),p=await enrolled(f);await connect(f,p);
 const tokenResponse=await exchange(f,await authorization(f,p,'profile:read tasks:read habitica.tasks.write'));assert.equal(tokenResponse.status,200);const token=(await tokenResponse.json()).access_token;
 const client=new Client({name:'habit-directions-fixture',version:'1.0.0'});t.after(()=>client.close());await client.connect(new StreamableHTTPClientTransport(new URL(resource),{requestInit:{headers:{authorization:`Bearer ${token}`}},fetch:(url,init)=>f.mcp.fetch(String(url),init)}));
 const tools=(await client.listTools()).tools,schema=tools.find(tool=>tool.name==='prepare_task_command').inputSchema.properties.patch.properties.habiticaDetails.properties;assert.equal(schema.up.type,'boolean');assert.equal(schema.down.type,'boolean');
 const prepared=await client.callTool({name:'prepare_task_command',arguments:{requestId:crypto.randomUUID(),operation:'createHabit',patch:{text:'Negative-only Habit',habiticaDetails:{up:false,down:true}}}});assert.notEqual(prepared.isError,true,JSON.stringify(prepared));assert.equal(prepared.structuredContent.command.patch.habiticaDetails.up,false);
 const accepted=await client.callTool({name:'create_habit',arguments:{commandId:prepared.structuredContent.preparedCommandId}});assert.notEqual(accepted.isError,true);assert.equal(accepted.structuredContent.habiticaProjectionStatus,'pending');assert.equal(accepted.structuredContent.confirmedProjectionCount,0);assert.equal(accepted.structuredContent.acceptedEventCount,0);
});
