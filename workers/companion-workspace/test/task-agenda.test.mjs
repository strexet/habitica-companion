import {test} from 'node:test';import assert from 'node:assert/strict';import {readFile} from 'node:fs/promises';
import {queryAgenda,agendaFilter} from '../../shared/task-agenda.ts';import {workspaceDocument} from '../../shared/workspace-domain.ts';
import {fixture,enrolled,authorization,exchange,resource,accounts} from '../../chatgpt-auth/test/fixtures/oauth.mjs';
import {Client} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js';import {StreamableHTTPClientTransport} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/streamableHttp.js';
const neutral=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/agenda-v1.json',import.meta.url),'utf8'));
test('portable agenda coalesces dates and preserves completed/skipped history without mutation',()=>{
 const document=workspaceDocument(neutral.document),before=JSON.stringify(document);
 for(const value of neutral.cases){const page=queryAgenda(document,value.filter);assert.deepEqual(page.items,value.items,value.name);for(const key of ['matchedCount','offset','nextOffset','truncated'])assert.equal(page[key],value[key],value.name);assert.equal(new Set(page.items.map(row=>row.taskId+row.date)).size,page.items.length);}
 assert.equal(JSON.stringify(document),before);
});
test('invalid ranges, stale pages, unavailable rules and capacity reject instead of returning partial data',()=>{
 for(const input of neutral.invalid)assert.throws(()=>agendaFilter(input),undefined,JSON.stringify(input));
 const filter={from:'2026-10-17',through:'2027-10-17'};
 const task=neutral.document.tasks[2];
 assert.throws(()=>queryAgenda(neutral.document,{...neutral.cases[0].filter,expectedRevision:9}),error=>error.code==='stale_revision');
 assert.throws(()=>queryAgenda({...neutral.document,tasks:[{...task,recurrence:{...task.recurrence,timeZoneId:'Unavailable/Zone'}}]},neutral.cases[0].filter),error=>error.code==='recurrence_unavailable');
 for(const count of [1001,28])assert.throws(()=>queryAgenda({...neutral.document,tasks:Array.from({length:count},()=>({...task,id:crypto.randomUUID(),recurrence:{...task.recurrence,startDate:filter.from}}))},filter),error=>error.code==='agenda_capacity');
 assert.equal(queryAgenda({...neutral.document,tasks:[{...task,recurrence:{...task.recurrence,startDate:filter.from}}],occurrences:[]},filter).matchedCount,366);
});
const origin='http://localhost:8789';
async function connect(f,p){const r=await f.workspace.fetch(origin+'/connect',{method:'POST',headers:{authorization:'Bearer '+p.managementToken,'content-type':'application/json'},body:JSON.stringify({consentVersion:'workspace-v1'})});assert.equal(r.status,201);return r.json();}
async function native(t,f,p,scope='profile:read tasks:read'){const r=await exchange(f,await authorization(f,p,scope));assert.equal(r.status,200);const token=(await r.json()).access_token;const c=new Client({name:'agenda-fixture',version:'1.0.0'});t.after(()=>c.close());await c.connect(new StreamableHTTPClientTransport(new URL(resource),{requestInit:{headers:{authorization:'Bearer '+token}},fetch:(url,init)=>f.mcp.fetch(String(url),init)}));return {c,token};}
const call=(f,w,args)=>f.workspace.fetch(origin+'/task-api',{method:'POST',headers:{authorization:'Bearer '+w.sessionToken,'content-type':'application/json'},body:JSON.stringify({tool:'list_companion_agenda',arguments:args})});
test('actual Worker and narrow MCP agenda preserve read grants, tenant isolation and zero writes',async t=>{
 let writes=0;const f=await fixture(t,{workspace:true,habitica:request=>{if(request.method!=='GET')writes++;return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:'Fixture A'}}});}});const p=await enrolled(f),w=await connect(f,p);
 const row=await f.workspaceDb.prepare('SELECT * FROM workspaces WHERE profile_id=?').bind(p.profileId).first();const document=workspaceDocument({...neutral.document,id:row.workspace_id});await f.workspaceDb.prepare('UPDATE workspaces SET document_json=? WHERE profile_id=?').bind(JSON.stringify(document),p.profileId).run();
 for(const value of neutral.cases){const response=await call(f,w,value.filter);assert.equal(response.status,200,await response.clone().text());const page=await response.json();assert.deepEqual(page.items,value.items,value.name);assert.equal(page.nextOffset,value.nextOffset);assert.equal(page.matchedCount,value.matchedCount);}
 for(const input of neutral.invalid)assert.ok([400,422].includes((await call(f,w,input)).status),JSON.stringify(input));
 const {c}=await native(t,f,p),tool=(await c.listTools()).tools.find(tool=>tool.name==='list_companion_agenda');assert.equal(tool.annotations.readOnlyHint,true);assert.equal(tool.annotations.destructiveHint,false);assert.deepEqual(tool.inputSchema.required,['from','through']);assert.deepEqual(tool._meta.securitySchemes,[{type:'oauth2',scopes:['profile:read','tasks:read']}]);
 const response=await c.callTool({name:'list_companion_agenda',arguments:neutral.cases[0].filter});assert.notEqual(response.isError,true,JSON.stringify(response));assert.deepEqual(response.structuredContent.items,neutral.cases[0].items);
 assert.equal((await call(f,w,{...neutral.cases[0].filter,offset:2,expectedRevision:9})).status,409);
 const other=await connect(f,await enrolled(f,accounts[1]));assert.equal((await(await call(f,other,neutral.cases[0].filter)).json()).matchedCount,0);
 const denied=await native(t,f,p,'profile:read');assert.equal((await f.mcp.fetch(resource,{method:'POST',headers:{authorization:'Bearer '+denied.token,'content-type':'application/json',accept:'application/json, text/event-stream'},body:JSON.stringify({jsonrpc:'2.0',id:999,method:'tools/call',params:{name:'list_companion_agenda',arguments:neutral.cases[0].filter}})})).status,403);
 const binding=(await f.mf.getBindings('mcp')).WORKSPACE_SERVER;assert.equal((await binding.fetch('https://workspace.internal/tool',{method:'POST',headers:{authorization:'Bearer '+denied.token,'content-type':'application/json'},body:JSON.stringify({tool:'list_companion_agenda',arguments:neutral.cases[0].filter})})).status,403);
 const large=workspaceDocument({...document,tasks:[...document.tasks,...Array.from({length:28},()=>({...document.tasks[2],id:crypto.randomUUID(),recurrence:{...document.tasks[2].recurrence,startDate:'2026-10-17'}}))]});
 await f.workspaceDb.prepare('UPDATE workspaces SET document_json=? WHERE profile_id=?').bind(JSON.stringify(large),p.profileId).run();
 const capacity=await call(f,w,{from:'2026-10-17',through:'2027-10-17'});assert.equal(capacity.status,422);assert.deepEqual(await capacity.json(),{error:'agenda_capacity'});
 assert.equal((await f.workspaceDb.prepare('SELECT COUNT(*) AS n FROM workspace_commands').first()).n,0);assert.equal((await f.workspaceDb.prepare('SELECT COUNT(*) AS n FROM workspace_preparations').first()).n,0);assert.equal(writes,0);
});
