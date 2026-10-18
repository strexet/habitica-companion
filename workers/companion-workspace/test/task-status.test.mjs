import {test} from 'node:test';import assert from 'node:assert/strict';
import {fixture,enrolled,authorization,exchange,resource,accounts} from '../../chatgpt-auth/test/fixtures/oauth.mjs';
import {Client} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js';import {StreamableHTTPClientTransport} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/streamableHttp.js';
import {taskSyncStatus} from '../src/task-status.ts';
const origin='http://localhost:8789',acceptedAt='2026-10-17T00:00:00Z';
async function connect(f,p){const r=await f.workspace.fetch(origin+'/connect',{method:'POST',headers:{authorization:'Bearer '+p.managementToken,'content-type':'application/json'},body:JSON.stringify({consentVersion:'workspace-v1'})});assert.equal(r.status,201);return r.json();}
async function native(t,f,p,scope='profile:read tasks:read'){const r=await exchange(f,await authorization(f,p,scope));assert.equal(r.status,200);const token=(await r.json()).access_token;const c=new Client({name:'status-fixture',version:'1.0.0'});t.after(()=>c.close());await c.connect(new StreamableHTTPClientTransport(new URL(resource),{requestInit:{headers:{authorization:'Bearer '+token}},fetch:(url,init)=>f.mcp.fetch(String(url),init)}));return {c,token};}
const call=(f,w,args={})=>f.workspace.fetch(origin+'/task-api',{method:'POST',headers:{authorization:'Bearer '+w.sessionToken,'content-type':'application/json'},body:JSON.stringify({tool:'get_task_sync_status',arguments:args})});
async function seed(f,p,states){
 const effects=[[],['create','update'],['create','scoreUp'],['scoreUp'],['create','update'],['update'],['scoreUp','scoreUp'],['update']];
 for(let i=0;i<states.length;i++){const id=crypto.randomUUID(),entry={projectionState:states[i],effects:effects[i]??[],confirmedEffects:i===2||i===6?1:i===4?2:0,acceptedAtUtc:i===1?acceptedAt:'2026-10-17T01:00:00Z'};await f.workspaceDb.prepare('INSERT INTO workspace_commands(profile_id,command_id,sequence,payload_hash,entry_json) VALUES(?,?,?,?,?)').bind(p.profileId,id,i+1,'0'.repeat(64),JSON.stringify(entry)).run();}
}
test('task-only MCP status separates durable states/effects and performs no task or executor writes',async t=>{
 let writes=0;const f=await fixture(t,{workspace:true,habitica:request=>{if(request.method!=='GET')writes++;return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:'Fixture A'}}});}});const p=await enrolled(f),w=await connect(f,p),{c}=await native(t,f,p);
 const tool=(await c.listTools()).tools.find(tool=>tool.name==='get_task_sync_status');assert.equal(tool.annotations.readOnlyHint,true);assert.equal(tool.annotations.destructiveHint,false);assert.deepEqual(tool.inputSchema.properties,{});assert.deepEqual(tool._meta.securitySchemes,[{type:'oauth2',scopes:['profile:read','tasks:read']}]);
 // Warm the existing authorization epoch before checking that status reads preserve stores.
 const empty=await c.callTool({name:'get_task_sync_status',arguments:{}});assert.notEqual(empty.isError,true,JSON.stringify(empty));assert.equal(empty.structuredContent.acceptedCommandCount,0);
 const states=['notRequested','pending','leased','executing','confirmed','blocked','unknownOutcome','cancelled'];await seed(f,p,states);
 const row=await f.workspaceDb.prepare('SELECT * FROM workspaces WHERE profile_id=?').bind(p.profileId).first();
 for(const table of ['prepared_spell_actions','prepared_cron_actions']){const spell=table==='prepared_spell_actions';await f.workspaceDb.prepare(`INSERT INTO ${table}(profile_id,action_id,request_id,input_hash,connection_id,access_epoch,${spell?'namespace_id,':''}status,payload_json,entry_json,created_at_utc,expires_at_utc) VALUES(?,?,?,?,?,?,${spell?'?,':''}?,?,?,?,?)`).bind(p.profileId,crypto.randomUUID(),crypto.randomUUID(),'0'.repeat(64),row.connection_id,row.access_epoch,...(spell?[crypto.randomUUID()]:[]),'unknownOutcome','{}','{}',acceptedAt,'2026-10-18T00:00:00Z').run();}
 await f.workspaceDb.prepare('UPDATE workspaces SET executor_enabled=1 WHERE profile_id=?').bind(p.profileId).run();
 const before=await f.workspaceDb.prepare('SELECT * FROM workspaces WHERE profile_id=?').bind(p.profileId).first();
 const response=await c.callTool({name:'get_task_sync_status',arguments:{}});assert.notEqual(response.isError,true,JSON.stringify(response));const status=response.structuredContent;
 assert.deepEqual(status.counts,Object.fromEntries(states.map(state=>[state,1])));assert.equal(status.acceptedCommandCount,8);assert.equal(status.requestedEffectCount,11);assert.equal(status.confirmedEffectCount,4);assert.equal(status.oldestPendingAtUtc,acceptedAt);assert.equal(status.executorEnabled,true);assert.equal(status.executorAvailable,false);assert.equal(status.storeVersion,before.store_version);assert.equal(status.revision,JSON.parse(before.document_json).revision);assert.ok(Number.isFinite(Date.parse(status.checkedAtUtc)));
 const browser=await(await call(f,w)).json();for(const key of ['counts','acceptedCommandCount','requestedEffectCount','confirmedEffectCount','executorEnabled','executorAvailable'])assert.deepEqual(browser[key],status[key]);
 assert.deepEqual(await f.workspaceDb.prepare('SELECT * FROM workspaces WHERE profile_id=?').bind(p.profileId).first(),before);
 assert.equal((await f.workspaceDb.prepare('SELECT COUNT(*) AS n FROM workspace_commands').first()).n,8);assert.equal((await f.workspaceDb.prepare('SELECT COUNT(*) AS n FROM workspace_preparations').first()).n,0);
 for(const table of ['prepared_spell_actions','prepared_cron_actions'])assert.equal((await f.workspaceDb.prepare(`SELECT status FROM ${table} WHERE profile_id=?`).bind(p.profileId).first()).status,'unknownOutcome');
 assert.equal(writes,0);
 for(const args of [{profileId:p.profileId},{taskId:crypto.randomUUID()},{enabled:true},{limit:1}])assert.equal((await call(f,w,args)).status,400);
 const other=await connect(f,await enrolled(f,accounts[1]));assert.equal((await(await call(f,other)).json()).acceptedCommandCount,0);
 const denied=await native(t,f,p,'profile:read'),binding=(await f.mf.getBindings('mcp')).WORKSPACE_SERVER;
 assert.equal((await f.mcp.fetch(resource,{method:'POST',headers:{authorization:'Bearer '+denied.token,'content-type':'application/json',accept:'application/json, text/event-stream'},body:JSON.stringify({jsonrpc:'2.0',id:999,method:'tools/call',params:{name:'get_task_sync_status',arguments:{}}})})).status,403);
 assert.equal((await binding.fetch('https://workspace.internal/tool',{method:'POST',headers:{authorization:'Bearer '+denied.token,'content-type':'application/json'},body:JSON.stringify({tool:'get_task_sync_status',arguments:{}})})).status,403);
});
test('status uses the current active epoch snapshot and distinguishes enabled deployment from opt-in',async t=>{
 const f=await fixture(t,{workspace:true,executor:true}),p=await enrolled(f);await connect(f,p);const env=await f.mf.getBindings('workspace');const row=await f.workspaceDb.prepare('SELECT * FROM workspaces WHERE profile_id=?').bind(p.profileId).first();
 const doc=JSON.parse(row.document_json);doc.revision=12;await f.workspaceDb.prepare('UPDATE workspaces SET document_json=?,store_version=7 WHERE profile_id=?').bind(JSON.stringify(doc),p.profileId).run();
 const status=await taskSyncStatus(env,row);assert.equal(status.revision,12);assert.equal(status.storeVersion,7);assert.equal(status.executorEnabled,false);assert.equal(status.executorAvailable,true);assert.equal(status.acceptedCommandCount,0);
 await f.workspaceDb.prepare('UPDATE workspaces SET access_epoch=? WHERE profile_id=?').bind(crypto.randomUUID(),p.profileId).run();await assert.rejects(taskSyncStatus(env,row),error=>error.code==='unauthorized');
 await f.workspaceDb.prepare('DELETE FROM workspaces WHERE profile_id=?').bind(p.profileId).run();await assert.rejects(taskSyncStatus(env,row),error=>error.code==='unauthorized');
});
test('unreadable and unsupported task rows return unavailable instead of zero or partial counts',async t=>{
 const f=await fixture(t,{workspace:true}),p=await enrolled(f),w=await connect(f,p);await seed(f,p,['notRequested']);
 for(const entry of ['{',JSON.stringify({projectionState:'futureState',effects:[],confirmedEffects:0,acceptedAtUtc:acceptedAt}),JSON.stringify({projectionState:'confirmed',effects:['create'],confirmedEffects:0,acceptedAtUtc:acceptedAt}),JSON.stringify({projectionState:'pending',effects:['futureEffect'],confirmedEffects:0,acceptedAtUtc:acceptedAt}),JSON.stringify({projectionState:'pending',effects:['create'],confirmedEffects:-1,acceptedAtUtc:acceptedAt})]){
  await f.workspaceDb.prepare('UPDATE workspace_commands SET entry_json=? WHERE profile_id=?').bind(entry,p.profileId).run();const response=await call(f,w);assert.equal(response.status,503);assert.deepEqual(await response.json(),{error:'task_status_unavailable'});
 }
});
