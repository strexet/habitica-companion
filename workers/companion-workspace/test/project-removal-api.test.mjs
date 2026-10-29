import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {fixture,enrolled,authorization,exchange,resource,accounts} from '../../chatgpt-auth/test/fixtures/oauth.mjs';
import {Client} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js';
import {StreamableHTTPClientTransport} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/streamableHttp.js';
import {workspaceDocument} from '../../shared/workspace-domain.ts';
import {journal} from '../src/journal.ts';
const origin='http://localhost:8789',neutral=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/project-removal-v3.json',import.meta.url),'utf8'));
async function connect(f,p){const r=await f.workspace.fetch(origin+'/connect',{method:'POST',headers:{authorization:'Bearer '+p.managementToken,'content-type':'application/json'},body:JSON.stringify({consentVersion:'workspace-v1'})});assert.equal(r.status,201);return r.json();}
async function client(t,f,p,scope='profile:read tasks:read workspace.organization.write'){
 const response=await exchange(f,await authorization(f,p,scope));assert.equal(response.status,200);const token=(await response.json()).access_token,c=new Client({name:'project-lifecycle-fixture',version:'1.0.0'});t.after(()=>c.close());
 await c.connect(new StreamableHTTPClientTransport(new URL(resource),{requestInit:{headers:{authorization:'Bearer '+token}},fetch:(url,init)=>f.mcp.fetch(String(url),init)}));return {c,token};
}
const http=(f,w,tool,args)=>f.workspace.fetch(origin+'/organization-api',{method:'POST',headers:{authorization:'Bearer '+w.sessionToken,'content-type':'application/json'},body:JSON.stringify({tool,arguments:args})});
async function invoke(c,name,args){const result=await c.callTool({name,arguments:args});assert.notEqual(result.isError,true,JSON.stringify(result));return result.structuredContent;}
async function seed(f,p,w,source=neutral.document){
 const row=await f.workspaceDb.prepare('SELECT * FROM workspaces WHERE profile_id=?').bind(p.profileId).first(),document=workspaceDocument({...structuredClone(source),id:w.workspaceId});const serialized=JSON.stringify(document);
 assert.ok(new TextEncoder().encode(serialized).length<=1024*1024);
 await f.workspaceDb.prepare('UPDATE workspaces SET document_json=?,store_version=?,archive_bytes=? WHERE profile_id=?').bind(serialized,document.revision,row.archive_bytes-new TextEncoder().encode(row.document_json).length+new TextEncoder().encode(serialized).length,p.profileId).run();return document;
}

test('real SDK discovers native project lifecycle and removes exact reviewed references without deleting tasks or upstream effects',async t=>{
 let outbound=0;const f=await fixture(t,{workspace:true,habitica:async request=>{outbound++;if(request.method!=='GET')return Response.json({error:'forbidden'},{status:500});return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:request.headers.get('x-api-user')===accounts[0].habiticaUserId?'Fixture A':'Fixture B'}}});}}),p=await enrolled(f),w=await connect(f,p),before=await seed(f,p,w);outbound=0;
 const {c}=await client(t,f,p),tools=(await c.listTools()).tools;outbound=0;
 assert.ok(tools.some(tool=>tool.name==='get_project_removal_impact'));const schema=tools.find(tool=>tool.name==='prepare_organization_command').inputSchema;
 assert.ok(schema.properties.operation.enum.includes('remove'));assert.ok(schema.properties.patch.anyOf[0].properties.removal);
 const impact=await invoke(c,'get_project_removal_impact',{projectId:before.projects[0].id,expectedRevision:10});assert.equal(impact.canRemove,true);
 const args={requestId:crypto.randomUUID(),kind:'project',operation:'remove',entityId:impact.project.id,expectedRevision:impact.revision,patch:{removal:impact.removal}};
 const preview=await invoke(c,'prepare_organization_command',args);assert.equal(preview.snapshot.isRemoved,true);assert.equal(preview.savedInCompanion,false);assert.equal(preview.mayAffectHabitica,false);
 assert.equal((await invoke(c,'prepare_organization_command',args)).preparedCommandId,preview.preparedCommandId);
 const receipt=await invoke(c,'accept_organization_command',{commandId:preview.preparedCommandId});assert.equal(receipt.savedInCompanion,true);assert.equal(receipt.acceptedEventCount,0);assert.equal(receipt.confirmedEffectCount,0);assert.equal(receipt.mayAffectHabitica,false);
 assert.deepEqual(await invoke(c,'get_organization_command',{commandId:preview.preparedCommandId}),receipt);assert.deepEqual(await invoke(c,'accept_organization_command',{commandId:preview.preparedCommandId}),receipt);
 const exported=await(await f.workspace.fetch(origin+'/export',{headers:{authorization:'Bearer '+w.sessionToken}})).json();await journal(exported);
 for(const key of ['tasks','sections','completions','occurrences','projections','habitScores'])assert.deepEqual(exported.document[key],before[key],key);
 assert.deepEqual(exported.commands,[]);assert.deepEqual(exported.events,[]);assert.equal(exported.document.organizationHistory.length,1);assert.equal(outbound,0);
 assert.equal((await invoke(c,'get_project_removal_impact',{projectId:impact.project.id})).canRemove,false);
 const other=await enrolled(f,accounts[1]),otherConnection=await connect(f,other),{c:otherClient}=await client(t,f,other);
 const foreignRead=await otherClient.callTool({name:'get_project_removal_impact',arguments:{projectId:impact.project.id}});assert.equal(foreignRead.isError,true);
 assert.equal((await http(f,otherConnection,'accept_organization_command',{commandId:preview.preparedCommandId})).status,409);
});
test('missing or changed exact approvals and revision cannot prepare a project removal',async t=>{
 const f=await fixture(t,{workspace:true}),p=await enrolled(f),w=await connect(f,p),document=await seed(f,p,w);
 const exact=neutral.hashes[0].command.patch.removal,args={requestId:crypto.randomUUID(),kind:'project',operation:'remove',entityId:document.projects[0].id,expectedRevision:10,patch:{removal:exact}};
 for(const changes of [{expectedRevision:undefined},{expectedRevision:9},{patch:null},{patch:{removal:{...exact,taskIds:[]}}},{patch:{name:'Mixed',removal:exact}},{entityId:crypto.randomUUID()}])assert.notEqual((await http(f,w,'prepare_organization_command',{...args,...changes})).status,200);
 assert.equal((await f.workspaceDb.prepare('SELECT COUNT(*) AS n FROM workspace_preparations').first()).n,0);
 const {token}=await client(t,f,p,'profile:read tasks:read');const denied=await f.mcp.fetch(resource,{method:'POST',headers:{authorization:'Bearer '+token,'content-type':'application/json',accept:'application/json, text/event-stream'},body:JSON.stringify({jsonrpc:'2.0',id:22,method:'tools/call',params:{name:'prepare_organization_command',arguments:args}})});assert.equal(denied.status,403);
});
test('complete reference approvals above 64 KiB remain bounded and atomic on public and private organization routes',async t=>{
 const f=await fixture(t,{workspace:true}),p=await enrolled(f),w=await connect(f,p),source=structuredClone(neutral.document),template=source.tasks[0];
 const tasks=Array.from({length:900},(_,index)=>({...template,id:crypto.randomUUID(),text:'Reference '+index,parentTaskId:null,dependsOn:[],labelIds:[]}));
 source.tasks=[...source.tasks,...tasks];source.sections.push(...Array.from({length:900},(_,index)=>({id:crypto.randomUUID(),revision:10,isArchived:false,name:'Section '+index,projectId:source.projects[0].id,position:index})));const document=await seed(f,p,w,source),{c}=await client(t,f,p),impact=await invoke(c,'get_project_removal_impact',{projectId:document.projects[0].id});
 const args={requestId:crypto.randomUUID(),kind:'project',operation:'remove',entityId:impact.project.id,expectedRevision:impact.revision,patch:{removal:impact.removal}};
 assert.ok(new TextEncoder().encode(JSON.stringify({tool:'prepare_organization_command',arguments:args})).length>65536);
 const preview=await invoke(c,'prepare_organization_command',args);assert.equal(preview.command.patch.removal.taskIds.length,907);
 const direct=await http(f,w,'prepare_organization_command',args);assert.equal(direct.status,200);assert.equal((await direct.json()).preparedCommandId,preview.preparedCommandId);
 const receipt=await invoke(c,'accept_organization_command',{commandId:preview.preparedCommandId});assert.equal(receipt.savedInCompanion,true);
 const exported=await(await f.workspace.fetch(origin+'/export',{headers:{authorization:'Bearer '+w.sessionToken}})).json();assert.deepEqual(exported.document.tasks,document.tasks);assert.equal(exported.document.organizationHistory.length,1);
 const oversized=await f.workspace.fetch(origin+'/organization-api',{method:'POST',headers:{authorization:'Bearer '+w.sessionToken,'content-type':'application/json'},body:' '.repeat(1024*1024+1)});assert.equal(oversized.status,413);
});
