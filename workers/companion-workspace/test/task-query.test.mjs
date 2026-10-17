import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {queryTasks,taskFilter} from '../../shared/task-query.ts';
import {workspaceDocument} from '../../shared/workspace-domain.ts';
import {fixture as integrationFixture,enrolled,authorization,exchange,resource,accounts} from '../../chatgpt-auth/test/fixtures/oauth.mjs';
import {Client} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js';
import {StreamableHTTPClientTransport} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/streamableHttp.js';
const fixture=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/task-query-v1.json',import.meta.url),'utf8'));
test('canonical query matches portable filters, Unicode and guarded page fixtures without mutation',()=>{
 const document=workspaceDocument(fixture.document),before=JSON.stringify(document);
 for(const value of fixture.cases){const page=queryTasks(document,value.filter);assert.deepEqual(page.tasks.map(task=>task.id),value.taskIds,value.name);for(const key of ['matchedCount','offset','nextOffset','truncated'])assert.equal(page[key],value[key],`${value.name}: ${key}`);assert.equal(page.workspaceId,document.id);assert.equal(page.revision,document.revision);}
 assert.equal(JSON.stringify(document),before);
});
test('invalid query arguments and stale revisions fail explicitly',()=>{
 for(const value of fixture.invalid)assert.throws(()=>taskFilter(value),undefined,JSON.stringify(value));
 for(const filter of [{expectedRevision:9},{expectedRevision:9,offset:1}])assert.throws(()=>queryTasks(fixture.document,filter),error=>error.code==='stale_revision'&&error.status===409);
});
const origin='http://localhost:8789';
async function connect(f,p){const response=await f.workspace.fetch(origin+'/connect',{method:'POST',headers:{authorization:'Bearer '+p.managementToken,'content-type':'application/json'},body:JSON.stringify({consentVersion:'workspace-v1'})});assert.equal(response.status,201);return response.json();}
async function native(t,f,p,scope='profile:read tasks:read'){const response=await exchange(f,await authorization(f,p,scope));assert.equal(response.status,200);const token=(await response.json()).access_token;const c=new Client({name:'query-fixture',version:'1.0.0'});t.after(()=>c.close());await c.connect(new StreamableHTTPClientTransport(new URL(resource),{requestInit:{headers:{authorization:'Bearer '+token}},fetch:(url,init)=>f.mcp.fetch(String(url),init)}));return {c,token};}
async function list(f,w,args){return f.workspace.fetch(origin+'/task-api',{method:'POST',headers:{authorization:'Bearer '+w.sessionToken,'content-type':'application/json'},body:JSON.stringify({tool:'list_companion_tasks',arguments:args})});}
test('actual Worker and MCP apply filters, guard continuations and retain own-workspace read authorization',async t=>{
 let writes=0;const f=await integrationFixture(t,{workspace:true,habitica:request=>{if(request.method!=='GET')writes++;return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:'Fixture A'}}});}});
 const p=await enrolled(f),w=await connect(f,p);
 const row=await f.workspaceDb.prepare('SELECT * FROM workspaces WHERE profile_id=?').bind(p.profileId).first();
 const document=workspaceDocument({...fixture.document,id:row.workspace_id});
 // Synthetic fixture storage only: no external service or canonical write command runs.
 await f.workspaceDb.prepare('UPDATE workspaces SET document_json=? WHERE profile_id=?').bind(JSON.stringify(document),p.profileId).run();
 for(const value of fixture.cases){const response=await list(f,w,value.filter);assert.equal(response.status,200,await response.clone().text());const page=await response.json();assert.deepEqual(page.tasks.map(task=>task.id),value.taskIds,value.name);assert.equal(page.matchedCount,value.matchedCount);assert.equal(page.nextOffset,value.nextOffset);}
 for(const args of fixture.invalid)assert.ok([400,422].includes((await list(f,w,args)).status),JSON.stringify(args));
 const {c}=await native(t,f,p),tool=(await c.listTools()).tools.find(tool=>tool.name==='list_companion_tasks');
 assert.equal(tool.annotations.readOnlyHint,true);for(const key of ['query','state','priority','categoryId','projectId','labelId','plannedFrom','plannedTo','deadlineFrom','deadlineTo','projectionState','offset','expectedRevision'])assert.ok(tool.inputSchema.properties[key],key);
 assert.deepEqual(tool._meta.securitySchemes,[{type:'oauth2',scopes:['profile:read','tasks:read']}]);
 const result=await c.callTool({name:'list_companion_tasks',arguments:{...fixture.cases.find(value=>value.name==='AND combination').filter}});assert.notEqual(result.isError,true,JSON.stringify(result));assert.deepEqual(result.structuredContent.tasks.map(task=>task.id),[fixture.document.tasks[0].id]);assert.equal(result.structuredContent.revision,10);
 const first=await(await list(f,w,{limit:2})).json();assert.equal(first.nextOffset,2);
 await f.workspaceDb.prepare('UPDATE workspaces SET document_json=? WHERE profile_id=?').bind(JSON.stringify({...document,revision:11}),p.profileId).run();
 const stale=await list(f,w,{offset:first.nextOffset,expectedRevision:first.revision});assert.equal(stale.status,409);assert.equal((await stale.json()).error,'stale_revision');
 const other=await connect(f,await enrolled(f,accounts[1]));assert.equal((await(await list(f,other,{query:'HiddenNeedle'})).json()).matchedCount,0);
 const denied=await native(t,f,p,'profile:read');
 const response=await f.mcp.fetch(resource,{method:'POST',headers:{authorization:'Bearer '+denied.token,'content-type':'application/json',accept:'application/json, text/event-stream'},body:JSON.stringify({jsonrpc:'2.0',id:999,method:'tools/call',params:{name:'list_companion_tasks',arguments:{}}})});assert.equal(response.status,403);
 const binding=(await f.mf.getBindings('mcp')).WORKSPACE_SERVER;assert.equal((await binding.fetch('https://workspace.internal/tool',{method:'POST',headers:{authorization:'Bearer '+denied.token,'content-type':'application/json'},body:JSON.stringify({tool:'list_companion_tasks',arguments:{}})})).status,403);
 assert.equal((await f.workspaceDb.prepare('SELECT COUNT(*) AS n FROM workspace_commands').first()).n,0);assert.equal((await f.workspaceDb.prepare('SELECT COUNT(*) AS n FROM workspace_preparations').first()).n,0);assert.equal(writes,0);
});
