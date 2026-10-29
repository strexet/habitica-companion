import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {fixture,enrolled,authorization,exchange,resource,accounts} from '../../chatgpt-auth/test/fixtures/oauth.mjs';
import {Client} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js';
import {StreamableHTTPClientTransport} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/streamableHttp.js';
import {ordinaryTaskIntentOperations,operationForTool,taskToolInput} from '../../shared/task-tools.ts';
const root=new URL('../../../',import.meta.url);
async function connected(t,f,p,scope='profile:read tasks:read habitica.tasks.write',lose=()=>false){
 const response=await exchange(f,await authorization(f,p,scope));assert.equal(response.status,200);const token=(await response.json()).access_token;
 const transport=new StreamableHTTPClientTransport(new URL(resource),{requestInit:{headers:{authorization:`Bearer ${token}`}},fetch:async(url,init)=>{const response=await f.mcp.fetch(String(url),init);if(lose(init))throw new TypeError('Synthetic acknowledgement loss');return response;}});
 const client=new Client({name:'explicit-intent-fixture',version:'1'});t.after(()=>client.close());await client.connect(transport);return{client,token};
}
async function setup(t){const f=await fixture(t,{workspace:true}),p=await enrolled(f);assert.equal((await f.workspace.fetch('http://localhost:8789/connect',{method:'POST',headers:{authorization:`Bearer ${p.managementToken}`,'content-type':'application/json'},body:JSON.stringify({consentVersion:'workspace-v1'})})).status,201);return{f,p};}
test('ordinary guidance covers exactly four operations while schemas and honest write annotations retain existing safeguards',async t=>{
 const {f,p}=await setup(t),{client}=await connected(t,f,p),tools=new Map((await client.listTools()).tools.map(tool=>[tool.name,tool]));
 assert.deepEqual(ordinaryTaskIntentOperations,['createTodo','createDaily','createHabit','updateTask']);
 for(const [name,operation] of Object.entries(operationForTool)){
  const tool=tools.get(name);assert.equal(tool.annotations.readOnlyHint,false);assert.deepEqual(tool.inputSchema.required,['commandId']);assert.equal(tool._meta.securitySchemes[0].scopes.includes('habitica.tasks.write'),true);
  if(ordinaryTaskIntentOperations.includes(operation)){assert.match(tool.description,/without an extra agent approval question/);assert.match(tool.description,/unexpected effects/);assert.match(tool.description,/host permission checks/);}
  else assert.doesNotMatch(tool.description,/without an extra agent approval question/);
 }
 assert.equal(tools.get('update_task').annotations.destructiveHint,true);assert.equal(tools.get('delete_task').annotations.destructiveHint,true);assert.match(tools.get('prepare_task_command').description,/completion, scoring, spells, Cron, bulk/);
 assert.throws(()=>taskToolInput('update_task',{commandId:crypto.randomUUID(),skipConfirmation:true}));
 const corpus=JSON.parse(await readFile(new URL('tests/fixtures/plugin/ordinary-task-intent-v1.json',root),'utf8'));assert.equal(corpus.cases.length,15);for(const name of ['ambiguous-name','unexpected-effect','read-only','missing-consent','uncertain-write','stale-revision','delete','complete','score','cron','bulk'])assert.ok(corpus.cases.some(row=>row.id===name));
 const skill=await readFile(new URL('plugin/skills/habitica-task-management/SKILL.md',root),'utf8'),dates=await readFile(new URL('plugin/skills/habitica-task-management/references/deadline-dates.md',root),'utf8');assert.match(skill,/without an extra agent approval question/);assert.match(skill,/Host permission UI remains authoritative/);assert.match(dates,/without another agent approval question/);assert.doesNotMatch(dates,/obtain explicit approval before update_task/);
});
test('explicit ordinary create and exact partial edit flow through internal preparation, immutable acceptance and separate projection receipts',async t=>{
 const {f,p}=await setup(t),{client}=await connected(t,f,p);const input={requestId:crypto.randomUUID(),operation:'createTodo',patch:{text:'Water the garden',notes:'Synthetic retained notes'},projectToHabitica:true};
 const prepared=await client.callTool({name:'prepare_task_command',arguments:input});assert.notEqual(prepared.isError,true,JSON.stringify(prepared));const preview=prepared.structuredContent;
 assert.equal((await f.workspaceDb.prepare('SELECT COUNT(*) AS n FROM workspace_commands').first()).n,0);
 const saved=(await client.callTool({name:'create_todo',arguments:{commandId:preview.preparedCommandId}})).structuredContent;assert.equal(saved.savedInCompanion,true);assert.equal(saved.habiticaProjectionStatus,'pending');assert.equal(saved.confirmedProjectionCount,0);
 assert.deepEqual((await client.callTool({name:'create_todo',arguments:{commandId:preview.preparedCommandId}})).structuredContent,saved);
 const current=(await client.callTool({name:'get_companion_task',arguments:{taskId:saved.taskId}})).structuredContent;
 const edit={requestId:crypto.randomUUID(),operation:'updateTask',taskId:saved.taskId,expectedRevision:current.revision,patch:{deadline:'2099-12-01'}};
 const editPreview=(await client.callTool({name:'prepare_task_command',arguments:edit})).structuredContent;assert.equal(editPreview.command.taskId,saved.taskId);assert.equal(editPreview.command.patch.text,null);assert.equal(editPreview.command.patch.notes,null);
 const edited=await client.callTool({name:'update_task',arguments:{commandId:editPreview.preparedCommandId}});assert.notEqual(edited.isError,true,JSON.stringify(edited));assert.equal(edited.structuredContent.savedInCompanion,true);assert.equal(edited.structuredContent.habiticaProjectionStatus,'pending');
 const after=(await client.callTool({name:'get_companion_task',arguments:{taskId:saved.taskId}})).structuredContent;assert.equal(after.task.text,'Water the garden');assert.equal(after.task.notes,'Synthetic retained notes');assert.equal(after.task.deadline,'2099-12-01');
 assert.deepEqual((await client.callTool({name:'get_task_command',arguments:{commandId:editPreview.preparedCommandId}})).structuredContent,edited.structuredContent);assert.equal((await f.workspaceDb.prepare('SELECT COUNT(*) AS n FROM workspace_commands').first()).n,2);
 assert.equal((await client.callTool({name:'prepare_task_command',arguments:{...edit,requestId:crypto.randomUUID(),expectedRevision:1}})).isError,true);
 assert.equal((await client.callTool({name:'prepare_task_command',arguments:{requestId:crypto.randomUUID(),operation:'updateTask',query:'garden',patch:{text:'Unselected'}}})).isError,true);
});
test('lost acknowledgement recovers original create rather than replacing intent; cross-profile and scope denials remain enforced',async t=>{
 const {f,p}=await setup(t);let lose=false;const {client}=await connected(t,f,p,undefined,init=>{if(!init?.body)return false;const body=JSON.parse(init.body);if(lose&&body.method==='tools/call'&&body.params.name==='create_todo'){lose=false;return true;}return false;});
 const args={requestId:crypto.randomUUID(),operation:'createTodo',patch:{text:'Original uncertain task'}},preview=(await client.callTool({name:'prepare_task_command',arguments:args})).structuredContent;lose=true;
 await assert.rejects(client.callTool({name:'create_todo',arguments:{commandId:preview.preparedCommandId}}));
 const original=(await client.callTool({name:'get_task_command',arguments:{commandId:preview.preparedCommandId}})).structuredContent;assert.equal(original.savedInCompanion,true);assert.equal(original.commandId,preview.preparedCommandId);assert.equal((await f.workspaceDb.prepare('SELECT COUNT(*) AS n FROM workspace_commands').first()).n,1);
 const read=await connected(t,f,p,'profile:read tasks:read');await assert.rejects(read.client.callTool({name:'prepare_task_command',arguments:{requestId:crypto.randomUUID(),operation:'createTodo',patch:{text:'Denied write'}}}));
 const other=await enrolled(f,accounts[1]);assert.equal((await f.workspace.fetch('http://localhost:8789/connect',{method:'POST',headers:{authorization:`Bearer ${other.managementToken}`,'content-type':'application/json'},body:JSON.stringify({consentVersion:'workspace-v1'})})).status,201);
 const second=await connected(t,f,other);assert.equal((await second.client.callTool({name:'create_todo',arguments:{commandId:preview.preparedCommandId}})).isError,true);
});
