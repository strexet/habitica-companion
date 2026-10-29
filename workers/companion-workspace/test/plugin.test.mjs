import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFile,readdir} from 'node:fs/promises';
import Ajv2020 from '../../chatgpt-mcp/node_modules/ajv/dist/2020.js';
import {fixture,enrolled,authorization,exchange,resource,accounts} from '../../chatgpt-auth/test/fixtures/oauth.mjs';
import {Client} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js';
import {StreamableHTTPClientTransport} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/streamableHttp.js';
const root=new URL('../../../',import.meta.url);
const json=async path=>JSON.parse(await readFile(new URL(path,root),'utf8'));

test('portable plugin and MCP configuration validate against pinned upstream schemas',async()=>{
 const ajv=new Ajv2020();
 for(const name of ['plugin','mcp']){
  const schema=await json(`tests/fixtures/plugin/${name}.schema.json`);const doc=await json(`plugin/${name}.json`);
  const validate=ajv.compile(schema);assert.equal(validate(doc),true,JSON.stringify(validate.errors));
  assert.equal(validate({...doc,unknownConfiguration:true}),false);
 }
 const mcp=await json('plugin/mcp.json');const server=mcp.mcpServers['habitica-companion'];
 assert.equal(server.type,'streamable-http');assert.equal(new URL(server.url).pathname,'/mcp');assert.ok(new URL(server.url).hostname.endsWith('.invalid'));assert.equal(server.headers,undefined);
 assert.deepEqual((await readdir(new URL('plugin/skills',root))).sort(),['habitica-account-review','habitica-activity-review','habitica-daily-review','habitica-diagnostics','habitica-notes','habitica-task-management']);
});
async function client(t,f,p,scope){
 const response=await exchange(f,await authorization(f,p,scope));assert.equal(response.status,200);const token=(await response.json()).access_token;
 const config=await json('plugin/mcp.json');const endpoint=new URL(config.mcpServers['habitica-companion'].url);endpoint.protocol='http:';endpoint.host='localhost:8788';
 assert.equal(endpoint.href,resource);
 const transport=new StreamableHTTPClientTransport(endpoint,{requestInit:{headers:{authorization:`Bearer ${token}`}},fetch:(url,init)=>{assert.equal(String(url),resource);return f.mcp.fetch(String(url),init);}});
 const c=new Client({name:'packaged-workflow-fixture',version:'0.1.0'});t.after(()=>c.close());await c.connect(transport);return {c,token};
}

test('discovery expectations match advertised schemas and preserve permission boundaries without executing prompts',async t=>{
 const corpus=await json('tests/fixtures/plugin/discovery-prompts-v1.json');
 assert.deepEqual(corpus.chatVariants,['new','existing']);
 assert.equal(new Set(corpus.cases.map(row=>row.id)).size,corpus.cases.length);
 for(const category of ['direct','indirect','negative','followup'])assert.ok(corpus.cases.some(row=>row.category===category));
 let upstream=0;
 const f=await fixture(t,{workspace:true,habitica:async request=>{
  upstream++;assert.equal(request.method,'GET');
  return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:'Fixture A'}}});
 }});
 const p=await enrolled(f);const before=upstream;
 const {c}=await client(t,f,p,'profile:read account:read tasks:read habitica.tasks.write');
 const advertised=(await c.listTools()).tools;const tools=new Map(advertised.map(tool=>[tool.name,tool]));
 const ajv=new Ajv2020({strict:false,validateFormats:false});
 for(const row of corpus.cases){
  assert.ok(row.prompt&&row.context&&row.confirmation);
  if(row.tool===null){assert.equal(row.expectedAction,'no-companion-call');assert.equal(row.arguments,null);assert.equal(row.scope,null);continue;}
  const tool=tools.get(row.tool);assert.ok(tool,row.id);
  assert.equal(ajv.compile(tool.inputSchema)(row.arguments),true,row.id);
  assert.ok(tool._meta.securitySchemes.some(s=>s.scopes.includes(row.scope)),row.id);
  assert.equal(tool.annotations.readOnlyHint,row.expectedAction==='read',row.id);
  assert.equal(tool.annotations.openWorldHint,false,row.id);
 }
 const list=tools.get('list_companion_tasks');assert.match(list.description,/^Use this when/);assert.match(list.description,/read-only/i);assert.match(list.description,/get_completed_history/);
 for(const name of ['create_todo','update_task','complete_task','score_habit','delete_task'])assert.equal(tools.get(name).annotations.readOnlyHint,false);
 assert.equal(upstream,before);assert.equal((await f.workspaceDb.prepare('SELECT COUNT(*) AS n FROM workspace_commands').first()).n,0);
});

test('packaged workflow tool names route through actual SDK with two isolated OAuth profiles',async t=>{
 let writes=0;
 const f=await fixture(t,{workspace:true,habitica:async request=>{
  if(request.method!=='GET'){writes++;return new Response(null,{status:500});}
  const user=request.headers.get('x-api-user');
  const data=request.url.includes('tasks/user')?[{id:'90000000-0000-4000-8000-000000000011',type:'daily',text:'Fixture Daily',isDue:true,completed:false}]:{_id:user,profile:{name:user===accounts[0].habiticaUserId?'Fixture A':'Fixture B'},stats:{hp:37,mp:12,lvl:5},needsCron:false};
  return Response.json({success:true,data});
 }});
 const profiles=await Promise.all(accounts.map(account=>enrolled(f,account)));
 for(const p of profiles){assert.equal((await f.workspace.fetch('http://localhost:8789/connect',{method:'POST',headers:{authorization:`Bearer ${p.managementToken}`,'content-type':'application/json'},body:JSON.stringify({consentVersion:'workspace-v1'})})).status,201);}
 const {c:a}=await client(t,f,profiles[0],'profile:read account:read tasks:read habitica.tasks.write');const {c:b}=await client(t,f,profiles[1],'profile:read account:read tasks:read habitica.tasks.write');
 const advertised=new Set((await a.listTools()).tools.map(tool=>tool.name));
 for(const folder of await readdir(new URL('plugin/skills',root))){
  const skill=await readFile(new URL(`plugin/skills/${folder}/SKILL.md`,root),'utf8');
  for(const [_,symbol] of skill.matchAll(/`([a-z]+(?:_[a-z]+)+)`/g))assert.ok(advertised.has(symbol),`${folder}: missing tool ${symbol}`);
 }
 for(const c of [a,b]){
  const account=await c.callTool({name:'get_account_status',arguments:{}});assert.equal(account.structuredContent.hp,37);assert.equal(account.structuredContent.needsCron,false);
  const daily=await c.callTool({name:'list_tasks',arguments:{type:'daily',limit:100}});assert.equal(daily.structuredContent.tasks[0].isDue,true);
 }
 const preview=await a.callTool({name:'prepare_task_command',arguments:{requestId:crypto.randomUUID(),operation:'createTodo',patch:{text:'Private canonical intent'},projectToHabitica:false}});assert.notEqual(preview.isError,true);const commandId=preview.structuredContent.preparedCommandId;
 assert.equal((await b.callTool({name:'create_todo',arguments:{commandId}})).isError,true);
 const own=await a.callTool({name:'create_todo',arguments:{commandId}});assert.equal(own.structuredContent.savedInCompanion,true);assert.equal(own.structuredContent.habiticaProjectionStatus,'pending');
 assert.equal((await b.callTool({name:'list_companion_tasks',arguments:{}})).structuredContent.matchedCount,0);
 const otherReceipt=await b.callTool({name:'get_task_command',arguments:{commandId}});assert.equal(otherReceipt.structuredContent.savedInCompanion,null);assert.equal(otherReceipt.structuredContent.taskText,undefined);
 const {token}=await client(t,f,profiles[1],'profile:read tasks:read');
 const denied=await f.mcp.fetch(resource,{method:'POST',headers:{authorization:`Bearer ${token}`,'content-type':'application/json',accept:'application/json, text/event-stream'},body:JSON.stringify({jsonrpc:'2.0',id:8,method:'tools/call',params:{name:'prepare_task_command',arguments:{requestId:crypto.randomUUID(),operation:'createTodo',patch:{text:'Denied'}}}})});assert.equal(denied.status,403);
 assert.equal((await a.callTool({name:'prepare_task_command',arguments:{requestId:crypto.randomUUID(),operation:'completeTask'}})).isError,true);
 assert.equal(writes,0);assert.equal((await f.workspaceDb.prepare('SELECT COUNT(*) AS n FROM workspace_commands').first()).n,1);
 for(const account of accounts)assert.equal(JSON.stringify(f.messages).includes(account.apiToken),false);
});

test('packaged task references share the all-kind required-default contract',async()=>{
 for(const path of ['SKILL.md','references/habitica-correspondence.md','references/todo-hierarchy.md','references/project-planning.md']){
  const source=await readFile(new URL('plugin/skills/habitica-task-management/'+path,root),'utf8');assert.match(source,/task-required-v1/);assert.match(source,/original.*receipt|receipts/s);assert.match(source,/consent/);
 }
});

test('staged release validates portable schemas and reports descriptor evidence separately from installation',async t=>{
 const {mkdtemp,rm}=await import('node:fs/promises');const {tmpdir}=await import('node:os');const {join}=await import('node:path');const {execFileSync}=await import('node:child_process');const {fileURLToPath}=await import('node:url');
 const dir=await mkdtemp(join(tmpdir(),'companion-package-schema-'));t.after(()=>rm(dir,{recursive:true,force:true}));const staged=join(dir,'plugin');
 execFileSync('python3',['deploy/package-plugin.py','build','--source','plugin','--target',staged,'--source-revision','a'.repeat(40),'--requirements','deploy/plugin-requirements.json'],{cwd:fileURLToPath(root),timeout:15000});
 const ajv=new Ajv2020();for(const name of ['plugin','mcp']){const validate=ajv.compile(await json(`tests/fixtures/plugin/${name}.schema.json`));assert.equal(validate(JSON.parse(await readFile(join(staged,name+'.json'),'utf8'))),true,JSON.stringify(validate.errors));}
 const identity=JSON.parse(await readFile(join(staged,'package-identity.json'),'utf8'));assert.equal(identity.packageVersion,(await json('plugin/plugin.json')).version);assert.match(identity.contentHash,/^[a-f0-9]{64}$/);assert.deepEqual(Object.keys(identity.skills).sort(),(await readdir(new URL('plugin/skills',root))).sort());
 for(const folder of Object.keys(identity.skills)){const claim=JSON.parse(await readFile(join(staged,'skills',folder,'references','package-identity.json'),'utf8'));assert.deepEqual(claim,identity);const guidance=await readFile(join(staged,'skills',folder,'references','package-version.md'),'utf8');assert.match(guidance,/host installation unverified/);}
 execFileSync('python3',['deploy/package-plugin.py','verify-archive',staged+'.zip'],{cwd:fileURLToPath(root),timeout:15000});
});
