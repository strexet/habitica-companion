import {test} from 'node:test';import assert from 'node:assert/strict';import {readFile} from 'node:fs/promises';
import {queryCompletionHistory,historyFilter} from '../../shared/completion-history.ts';import {workspaceDocument} from '../../shared/workspace-domain.ts';import {completionHistory} from '../src/completion-history.ts';
import {workspaceCommand,commandHash} from '../../shared/workspace-commands.ts';
import {fixture,enrolled,authorization,exchange,resource,accounts} from '../../chatgpt-auth/test/fixtures/oauth.mjs';
import {Client} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js';import {StreamableHTTPClientTransport} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/streamableHttp.js';
const neutral=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/completion-history-v1.json',import.meta.url),'utf8'));
const labels=entries=>entries.map(entry=>({commandId:entry.command.id,taskId:entry.command.taskId,operation:entry.command.operation,snapshot:entry.taskSnapshot}));
test('portable completion history retains archive flags, original detail sources and UTC fractional ordering',()=>{
 const document=workspaceDocument(neutral.document),commands=labels(neutral.commands),before=JSON.stringify({document,commands});
 for(const value of neutral.cases){const page=queryCompletionHistory(document,commands,value.filter);assert.deepEqual(page.items,value.items,value.name);for(const key of ['matchedCount','offset','nextOffset','truncated'])assert.equal(page[key],value[key],value.name);}
 assert.equal(JSON.stringify({document,commands}),before);
});
test('invalid history filters and inconsistent matching snapshots fail instead of inventing history',()=>{
 for(const input of neutral.invalid)assert.throws(()=>historyFilter(input),undefined,JSON.stringify(input));
 const document=workspaceDocument(neutral.document),commands=labels(neutral.commands);
 assert.throws(()=>queryCompletionHistory(document,commands,{expectedRevision:19}),error=>error.code==='stale_revision');
 for(const altered of [{...commands[0],operation:'updateTask'},{...commands[0],snapshot:{...commands[0].snapshot,id:crypto.randomUUID()}}])assert.throws(()=>queryCompletionHistory(document,[altered],{}),error=>error.code==='history_unavailable');
 const legacy=queryCompletionHistory(document,[],{taskId:commands[0].taskId});assert.equal(legacy.items[0].detailSource,'currentTask');assert.equal(legacy.items[0].taskText,'Renamed current task');
});
test('canonical history retains more than the legacy recent-completed bound',()=>{
 const d=structuredClone(neutral.document),task=d.tasks.find(value=>value.kind==='daily');
 for(let index=0;index<35;index++){const at=new Date(Date.UTC(2026,7,1+index)).toISOString(),id=crypto.randomUUID();d.occurrences.push({id,revision:20,isArchived:false,taskId:task.id,date:at.slice(0,10),state:'completed'});d.completions.push({id:crypto.randomUUID(),revision:20,isArchived:false,taskId:task.id,commandId:crypto.randomUUID(),completedAtUtc:at,occurrenceId:id});}
 const document=workspaceDocument(d),all=queryCompletionHistory(document,labels(neutral.commands),{limit:100});assert.equal(all.matchedCount,41);assert.equal(all.items.length,41);
 const page=queryCompletionHistory(document,labels(neutral.commands),{limit:10});assert.equal(page.matchedCount,41);assert.equal(page.nextOffset,10);
});
const origin='http://localhost:8789';
async function connect(f,p){const r=await f.workspace.fetch(origin+'/connect',{method:'POST',headers:{authorization:'Bearer '+p.managementToken,'content-type':'application/json'},body:JSON.stringify({consentVersion:'workspace-v1'})});assert.equal(r.status,201);return r.json();}
async function native(t,f,p,scope='profile:read tasks:read'){const r=await exchange(f,await authorization(f,p,scope));assert.equal(r.status,200);const token=(await r.json()).access_token;const c=new Client({name:'history-fixture',version:'1.0.0'});t.after(()=>c.close());await c.connect(new StreamableHTTPClientTransport(new URL(resource),{requestInit:{headers:{authorization:'Bearer '+token}},fetch:(url,init)=>f.mcp.fetch(String(url),init)}));return {c,token};}
async function seed(f,p){
 const old=await f.workspaceDb.prepare('SELECT * FROM workspaces WHERE profile_id=?').bind(p.profileId).first();const document=workspaceDocument({...neutral.document,id:old.workspace_id}),serialized=JSON.stringify(document);let bytes=old.archive_bytes-new TextEncoder().encode(old.document_json).length+new TextEncoder().encode(serialized).length;
 for(let i=0;i<neutral.commands.length;i++){const entry=structuredClone(neutral.commands[i]);entry.command.workspaceId=document.id;entry.payloadHash=await commandHash(workspaceCommand(entry.command));const text=JSON.stringify(entry);bytes+=new TextEncoder().encode(text).length;await f.workspaceDb.prepare('INSERT INTO workspace_commands(profile_id,command_id,sequence,payload_hash,entry_json) VALUES(?,?,?,?,?)').bind(p.profileId,entry.command.id,i+1,entry.payloadHash,text).run();}
 await f.workspaceDb.prepare('UPDATE workspaces SET document_json=?,store_version=?,archive_bytes=? WHERE profile_id=?').bind(serialized,document.revision,bytes,p.profileId).run();return {old,document};
}
const call=(f,w,args)=>f.workspace.fetch(origin+'/task-api',{method:'POST',headers:{authorization:'Bearer '+w.sessionToken,'content-type':'application/json'},body:JSON.stringify({tool:'get_completed_history',arguments:args})});
test('actual Worker/MCP history preserves retained rows and read permissions without dispatch or mutation',async t=>{
 let writes=0;const f=await fixture(t,{workspace:true,habitica:request=>{if(request.method!=='GET')writes++;return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:'Fixture A'}}});}});const p=await enrolled(f),w=await connect(f,p);await seed(f,p);const {c}=await native(t,f,p);
 const tool=(await c.listTools()).tools.find(tool=>tool.name==='get_completed_history');assert.equal(tool.annotations.readOnlyHint,true);assert.equal(tool.annotations.destructiveHint,false);assert.deepEqual(tool._meta.securitySchemes,[{type:'oauth2',scopes:['profile:read','tasks:read']}]);assert.ok(tool.inputSchema.properties.completedFromUtc);
 await c.callTool({name:'get_completed_history',arguments:{}});const before=await f.workspaceDb.prepare('SELECT * FROM workspaces WHERE profile_id=?').bind(p.profileId).first();const entries=await f.workspaceDb.prepare('SELECT entry_json FROM workspace_commands WHERE profile_id=? ORDER BY sequence').bind(p.profileId).all();
 for(const value of neutral.cases){const response=await call(f,w,value.filter);assert.equal(response.status,200,await response.clone().text());const page=await response.json();assert.deepEqual(page.items,value.items,value.name);assert.equal(page.nextOffset,value.nextOffset);const mcp=await c.callTool({name:'get_completed_history',arguments:value.filter});assert.notEqual(mcp.isError,true,JSON.stringify(mcp));assert.deepEqual(mcp.structuredContent.items,value.items,value.name);}
 for(const input of neutral.invalid)assert.ok([400,422].includes((await call(f,w,input)).status),JSON.stringify(input));
 assert.deepEqual(await f.workspaceDb.prepare('SELECT * FROM workspaces WHERE profile_id=?').bind(p.profileId).first(),before);assert.deepEqual((await f.workspaceDb.prepare('SELECT entry_json FROM workspace_commands WHERE profile_id=? ORDER BY sequence').bind(p.profileId).all()).results,entries.results);assert.equal((await f.workspaceDb.prepare('SELECT COUNT(*) AS n FROM workspace_preparations').first()).n,0);assert.equal(writes,0);
 const other=await connect(f,await enrolled(f,accounts[1]));assert.equal((await(await call(f,other,{})).json()).matchedCount,0);
 const denied=await native(t,f,p,'profile:read'),binding=(await f.mf.getBindings('mcp')).WORKSPACE_SERVER;
 assert.equal((await f.mcp.fetch(resource,{method:'POST',headers:{authorization:'Bearer '+denied.token,'content-type':'application/json',accept:'application/json, text/event-stream'},body:JSON.stringify({jsonrpc:'2.0',id:999,method:'tools/call',params:{name:'get_completed_history',arguments:{}}})})).status,403);
 assert.equal((await binding.fetch('https://workspace.internal/tool',{method:'POST',headers:{authorization:'Bearer '+denied.token,'content-type':'application/json'},body:JSON.stringify({tool:'get_completed_history',arguments:{}})})).status,403);
});
test('coherent history reads use current revisions and reject malformed sources or lost epochs',async t=>{
 const f=await fixture(t,{workspace:true}),p=await enrolled(f),w=await connect(f,p),{old,document}=await seed(f,p),env=await f.mf.getBindings('workspace');
 assert.equal((await completionHistory(env,old,{})).revision,20);
 await f.workspaceDb.prepare('UPDATE workspaces SET document_json=? WHERE profile_id=?').bind(JSON.stringify({...document,revision:21}),p.profileId).run();await assert.rejects(completionHistory(env,old,{expectedRevision:20}),error=>error.code==='stale_revision');assert.equal((await completionHistory(env,old,{})).revision,21);
 const first=neutral.commands[0];const legacy={...first,taskSnapshot:null};await f.workspaceDb.prepare('UPDATE workspace_commands SET entry_json=? WHERE profile_id=? AND command_id=?').bind(JSON.stringify(legacy),p.profileId,first.command.id).run();const fallback=await completionHistory(env,old,{taskId:first.command.taskId});assert.equal(fallback.items[0].detailSource,'currentTask');
 for(const malformed of ['{',JSON.stringify({...first,taskSnapshot:false}),JSON.stringify({...first,command:{...first.command,operation:'updateTask'}})]){await f.workspaceDb.prepare('UPDATE workspace_commands SET entry_json=? WHERE profile_id=? AND command_id=?').bind(malformed,p.profileId,first.command.id).run();const response=await call(f,w,{});assert.equal(response.status,503);assert.deepEqual(await response.json(),{error:'history_unavailable'});}
 const {c}=await native(t,f,p);const unavailable=await c.callTool({name:'get_completed_history',arguments:{}});assert.equal(unavailable.isError,true);assert.equal(unavailable.structuredContent.unavailable,true);assert.equal(Object.hasOwn(unavailable.structuredContent,'items'),false);
 await f.workspaceDb.prepare('UPDATE workspace_commands SET entry_json=? WHERE profile_id=? AND command_id=?').bind(JSON.stringify(first),p.profileId,first.command.id).run();await f.workspaceDb.prepare('UPDATE workspaces SET access_epoch=? WHERE profile_id=?').bind(crypto.randomUUID(),p.profileId).run();await assert.rejects(completionHistory(env,old,{}),error=>error.code==='unauthorized');
 await f.workspaceDb.prepare('DELETE FROM workspaces WHERE profile_id=?').bind(p.profileId).run();await assert.rejects(completionHistory(env,old,{}),error=>error.code==='unauthorized');
});
