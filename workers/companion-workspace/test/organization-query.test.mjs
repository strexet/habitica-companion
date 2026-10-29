import {test} from 'node:test';import assert from 'node:assert/strict';import {readFile} from 'node:fs/promises';
import {queryOrganization,organizationInput,organizationNames} from '../../shared/organization-query.ts';import {workspaceDocument} from '../../shared/workspace-domain.ts';
import {fixture,enrolled,authorization,exchange,resource,accounts} from '../../chatgpt-auth/test/fixtures/oauth.mjs';
import {Client} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js';import {StreamableHTTPClientTransport} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/streamableHttp.js';
const neutral=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/organization-v1.json',import.meta.url),'utf8'));
test('portable organization lists preserve canonical ordering and Todo-only goal progress',()=>{
 const document=workspaceDocument(neutral.document),before=JSON.stringify(document);
 for(const value of neutral.cases){const page=queryOrganization(document,value.tool,value.arguments);assert.deepEqual(page.items,value.items,value.name);for(const key of ['matchedCount','offset','nextOffset','truncated'])assert.equal(page[key],value[key],value.name);}
 assert.equal(JSON.stringify(document),before);
});
test('organization inputs reject unsupported fields, identities, bounds and stale pages',()=>{
 for(const value of neutral.invalid)assert.throws(()=>organizationInput(value.tool,value.arguments),undefined,JSON.stringify(value));
 for(const name of organizationNames)assert.throws(()=>queryOrganization(neutral.document,name,{...(name==='resolve_project'?{reference:'missing'}:['list_project_sections','get_project_removal_impact'].includes(name)?{projectId:neutral.document.projects[0].id}:{}),expectedRevision:9}),error=>error.code==='stale_revision');
});
const origin='http://localhost:8789';
async function connect(f,p){const r=await f.workspace.fetch(origin+'/connect',{method:'POST',headers:{authorization:'Bearer '+p.managementToken,'content-type':'application/json'},body:JSON.stringify({consentVersion:'workspace-v1'})});assert.equal(r.status,201);return r.json();}
async function native(t,f,p,scope='profile:read tasks:read'){const r=await exchange(f,await authorization(f,p,scope));assert.equal(r.status,200);const token=(await r.json()).access_token;const c=new Client({name:'organization-fixture',version:'1.0.0'});t.after(()=>c.close());await c.connect(new StreamableHTTPClientTransport(new URL(resource),{requestInit:{headers:{authorization:'Bearer '+token}},fetch:(url,init)=>f.mcp.fetch(String(url),init)}));return {c,token};}
const call=(f,w,name,args)=>f.workspace.fetch(origin+'/task-api',{method:'POST',headers:{authorization:'Bearer '+w.sessionToken,'content-type':'application/json'},body:JSON.stringify({tool:name,arguments:args})});
test('actual organization MCP reads enforce read scope, own-profile state and zero writes',async t=>{
 let writes=0;const f=await fixture(t,{workspace:true,habitica:request=>{if(request.method!=='GET')writes++;return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:'Fixture A'}}});}});const p=await enrolled(f),w=await connect(f,p);
 const row=await f.workspaceDb.prepare('SELECT * FROM workspaces WHERE profile_id=?').bind(p.profileId).first();const document=workspaceDocument({...neutral.document,id:row.workspace_id});await f.workspaceDb.prepare('UPDATE workspaces SET document_json=? WHERE profile_id=?').bind(JSON.stringify(document),p.profileId).run();
 const {c}=await native(t,f,p),tools=(await c.listTools()).tools;
 for(const name of organizationNames){const tool=tools.find(tool=>tool.name===name);assert.equal(tool.annotations.readOnlyHint,true);assert.equal(tool.annotations.destructiveHint,false);assert.deepEqual(tool._meta.securitySchemes,[{type:'oauth2',scopes:['profile:read','tasks:read']}]);}
 for(const value of neutral.cases){const response=await call(f,w,value.tool,value.arguments);assert.equal(response.status,200,await response.clone().text());const page=await response.json();assert.deepEqual(page.items,value.items,value.name);assert.equal(page.matchedCount,value.matchedCount);const mcp=await c.callTool({name:value.tool,arguments:value.arguments});assert.notEqual(mcp.isError,true,JSON.stringify(mcp));assert.deepEqual(mcp.structuredContent.items,value.items,value.name);}
 for(const value of neutral.invalid)assert.ok([400,422].includes((await call(f,w,value.tool,value.arguments)).status),JSON.stringify(value));
 assert.equal((await call(f,w,'list_projects',{expectedRevision:9})).status,409);
 const other=await connect(f,await enrolled(f,accounts[1]));for(const name of organizationNames){const response=await call(f,other,name,name==='resolve_project'?{reference:'missing'}:['list_project_sections','get_project_removal_impact'].includes(name)?{projectId:document.projects[0].id}:{});if(name==='resolve_project'||name==='get_project_removal_impact')assert.equal(response.status,404);else assert.equal((await response.json()).matchedCount,0);}
 const denied=await native(t,f,p,'profile:read'),binding=(await f.mf.getBindings('mcp')).WORKSPACE_SERVER;
 for(const name of organizationNames){const args=name==='resolve_project'?{reference:'missing'}:['list_project_sections','get_project_removal_impact'].includes(name)?{projectId:document.projects[0].id}:{};assert.equal((await f.mcp.fetch(resource,{method:'POST',headers:{authorization:'Bearer '+denied.token,'content-type':'application/json',accept:'application/json, text/event-stream'},body:JSON.stringify({jsonrpc:'2.0',id:999,method:'tools/call',params:{name,arguments:args}})})).status,403);assert.equal((await binding.fetch('https://workspace.internal/tool',{method:'POST',headers:{authorization:'Bearer '+denied.token,'content-type':'application/json'},body:JSON.stringify({tool:name,arguments:args})})).status,403);}
 assert.equal((await f.workspaceDb.prepare('SELECT COUNT(*) AS n FROM workspace_commands').first()).n,0);assert.equal((await f.workspaceDb.prepare('SELECT COUNT(*) AS n FROM workspace_preparations').first()).n,0);assert.equal(writes,0);
});
