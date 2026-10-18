import {test} from 'node:test';import assert from 'node:assert/strict';import {readFile} from 'node:fs/promises';
import {workspaceCommand,commandHash} from '../../shared/workspace-commands.ts';import {workspaceDocument} from '../../shared/workspace-domain.ts';import {accept} from '../src/commands.ts';import {journal} from '../src/journal.ts';
import {fixture,enrolled,authorization,exchange,resource,accounts} from '../../chatgpt-auth/test/fixtures/oauth.mjs';
import {Client} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js';import {StreamableHTTPClientTransport} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/streamableHttp.js';
const base=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/organization-v1.json',import.meta.url),'utf8')).document;
const hashes=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/planning-command-v4.json',import.meta.url),'utf8'));
const now='2026-10-18T00:00:00Z',origin='http://localhost:8789';
const intent=(d,planning,extra={})=>workspaceCommand({id:crypto.randomUUID(),workspaceId:d.id,expectedRevision:d.revision,operation:'planTask',taskId:d.tasks[0].id,patch:{planning},...extra});
for(const value of hashes.cases)test('portable planning v4 identity: '+value.name,async()=>assert.equal(await commandHash(workspaceCommand(value.command)),value.payloadHash));
test('native placement preserves order, partial fields and retained history through explicit clearing',async()=>{
 let d=workspaceDocument(base);const original=structuredClone(d.tasks[0]),order=d.tasks.map(task=>task.id),entries=[],events=[];
 const apply=async planning=>{const result=await accept(d,intent(d,planning),now);d=result.document;entries.push(result.entry);events.push(result.event);return result;};
 const planning={plannedDate:'2026-10-20',categoryId:d.categories[0].id,projectId:d.projects[1].id,sectionId:d.sections[3].id,labelIds:[d.labels[0].id]};
 await apply(planning);assert.deepEqual(d.tasks.map(task=>task.id),order);assert.equal(d.tasks[0].projectId,planning.projectId);assert.equal(d.tasks[0].sectionId,planning.sectionId);for(const key of ['text','notes','deadline','habiticaDetails','state','kind'])assert.deepEqual(d.tasks[0][key],original[key]);
 await apply({plannedDate:'2026-10-22'});assert.equal(d.tasks[0].projectId,planning.projectId);await assert.rejects(accept(d,intent(d,{clearProject:true}),now),error=>error.code==='invalid_planning_reference');
 await apply({clearPlannedDate:true,clearCategory:true,clearProject:true,clearSection:true,labelIds:[]});for(const key of ['plannedDate','categoryId','projectId','sectionId'])assert.equal(d.tasks[0][key],null);assert.deepEqual(d.tasks[0].labelIds,[]);assert.equal(d.completions.length,1);assert.equal(d.habitScores.length,0);
 await journal({document:d,storeVersion:d.revision,commands:entries,events});
 const broken=structuredClone(entries);broken[0].taskSnapshot.plannedDate='2026-10-21';await assert.rejects(journal({document:d,storeVersion:d.revision,commands:broken,events}));
 const effects=structuredClone(entries);effects[0].effects=['update'];effects[0].projectionState='pending';await assert.rejects(journal({document:d,storeVersion:d.revision,commands:effects,events}));
});
test('linked and uncertain mappings remain fenced while native edits enqueue no game effects',async()=>{
 for(const state of ['unlinked','linked','pending','unknownOutcome','externalConflict']){
  const d=workspaceDocument({...base,projections:base.projections.map((mapping,i)=>i===0?{...mapping,state,habiticaTaskId:state==='unlinked'?null:mapping.habiticaTaskId}:mapping)});
  const result=await accept(d,intent(d,{plannedDate:'2026-10-20'}),now);assert.deepEqual(result.document.projections,d.projections);assert.equal(result.entry.projectionState,'notRequested');assert.deepEqual(result.entry.effects,[]);
  await assert.rejects(accept(d,intent(d,{plannedDate:'2026-10-20'},{projectToHabitica:true}),now),error=>error.code==='unsupported_projection');
 }
 for(const index of [2,3,4]){const d=workspaceDocument(base),task=d.tasks[index],result=await accept(d,intent(d,{plannedDate:'2026-10-20'},{taskId:task.id}),now);assert.equal(result.document.tasks[index].state,task.state);assert.equal(result.document.tasks[index].kind,task.kind);assert.deepEqual(result.entry.effects,[]);}
});
test('empty, contradictory, mixed, archived and incompatible planning never accepts',async()=>{
 const d=workspaceDocument(base);
 for(const planning of [{},{plannedDate:'0001-01-01'},{plannedDate:'2026-10-20',clearPlannedDate:true},{labelIds:[d.labels[0].id,d.labels[0].id]},{projectId:'00000000-0000-0000-0000-000000000000'},{plannedDate:'2026-02-30'},{labelIds:Array(101).fill(d.labels[0].id)}])assert.throws(()=>intent(d,planning));
 for(const planning of [{categoryId:d.categories[2].id},{projectId:d.projects[2].id},{sectionId:d.sections[2].id},{labelIds:[d.labels[1].id]},{projectId:d.projects[1].id,sectionId:d.sections[0].id},{sectionId:d.sections[4].id},{categoryId:crypto.randomUUID()}])await assert.rejects(accept(d,intent(d,planning),now),error=>error.code==='invalid_planning_reference');
 assert.throws(()=>intent(d,{plannedDate:'2026-10-20'},{patch:{planning:{plannedDate:'2026-10-20'},text:'Unexpected'}}));assert.throws(()=>intent(d,{plannedDate:'2026-10-20'},{operation:'updateTask'}));
});
async function connect(f,p){const r=await f.workspace.fetch(origin+'/connect',{method:'POST',headers:{authorization:'Bearer '+p.managementToken,'content-type':'application/json'},body:JSON.stringify({consentVersion:'workspace-v1'})});assert.equal(r.status,201);return r.json();}
async function native(t,f,p,scope='profile:read tasks:read habitica.tasks.write'){const r=await exchange(f,await authorization(f,p,scope));assert.equal(r.status,200);const token=(await r.json()).access_token;const c=new Client({name:'planning-fixture',version:'1.0.0'});t.after(()=>c.close());await c.connect(new StreamableHTTPClientTransport(new URL(resource),{requestInit:{headers:{authorization:'Bearer '+token}},fetch:(url,init)=>f.mcp.fetch(String(url),init)}));return {c,token};}
const call=(f,w,name,args)=>f.workspace.fetch(origin+'/task-api',{method:'POST',headers:{authorization:'Bearer '+w.sessionToken,'content-type':'application/json'},body:JSON.stringify({tool:name,arguments:args})});
test('synthetic MCP reviews exact native placement and recovers one original no-effect receipt',async t=>{
 let writes=0;const f=await fixture(t,{workspace:true,habitica:request=>{if(request.method!=='GET')writes++;return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:'Fixture A'}}});}});const p=await enrolled(f),w=await connect(f,p),row=await f.workspaceDb.prepare('SELECT * FROM workspaces WHERE profile_id=?').bind(p.profileId).first();
 const d=workspaceDocument({...base,id:row.workspace_id}),serialized=JSON.stringify(d),size=row.archive_bytes-new TextEncoder().encode(row.document_json).length+new TextEncoder().encode(serialized).length;
 await f.workspaceDb.prepare('UPDATE workspaces SET document_json=?,store_version=?,archive_bytes=? WHERE profile_id=?').bind(serialized,d.revision,size,p.profileId).run();
 const {c}=await native(t,f,p),tools=(await c.listTools()).tools,tool=tools.find(tool=>tool.name==='plan_task');assert.equal(tool.annotations.destructiveHint,true);assert.equal(tool.annotations.readOnlyHint,false);assert.deepEqual(tool._meta.securitySchemes,[{type:'oauth2',scopes:['profile:read','habitica.tasks.write']}]);assert.ok(tools.find(tool=>tool.name==='prepare_task_command').inputSchema.properties.patch.properties.planning);
 const invoke=async(name,args)=>{const result=await c.callTool({name,arguments:args});assert.notEqual(result.isError,true,JSON.stringify(result));return result.structuredContent;};
 const args={requestId:crypto.randomUUID(),operation:'planTask',taskId:d.tasks[0].id,patch:{planning:{plannedDate:'2026-10-20',projectId:d.projects[1].id,sectionId:d.sections[3].id,labelIds:[d.labels[0].id]}}};
 const preview=await invoke('prepare_task_command',args);assert.equal(preview.mayAffectHabitica,false);assert.equal(preview.command.taskId,args.taskId);assert.equal(preview.command.patch.planning.projectId,d.projects[1].id);assert.equal((await invoke('prepare_task_command',args)).preparedCommandId,preview.preparedCommandId);
 const accepted=await invoke('plan_task',{commandId:preview.preparedCommandId});assert.equal(accepted.habiticaProjectionStatus,'notRequested');assert.equal(accepted.acceptedEventCount,0);assert.equal(accepted.totalProjectionCount,0);
 assert.equal((await invoke('plan_task',{commandId:preview.preparedCommandId})).commandId,accepted.commandId);assert.equal((await invoke('get_task_command',{commandId:accepted.commandId})).confirmedProjectionCount,0);
 const task=(await invoke('get_companion_task',{taskId:args.taskId})).task;assert.equal(task.plannedDate,'2026-10-20');assert.equal(task.deadline,d.tasks[0].deadline);assert.equal(task.text,d.tasks[0].text);
 const exported=await(await f.workspace.fetch(origin+'/export',{headers:{authorization:'Bearer '+w.sessionToken}})).json();await journal(exported);assert.equal(exported.commands.length,1);assert.deepEqual(exported.document.tasks.map(task=>task.id),d.tasks.map(task=>task.id));assert.deepEqual(exported.document.projections,d.projections);assert.equal(exported.document.completions.length,d.completions.length);assert.equal(writes,0);
 const other=await connect(f,await enrolled(f,accounts[1]));assert.equal((await(await call(f,other,'get_task_command',{commandId:accepted.commandId})).json()).savedInCompanion,null);assert.equal((await call(f,other,'plan_task',{commandId:accepted.commandId})).status,409);
 const readonly=await native(t,f,p,'profile:read tasks:read'),binding=(await f.mf.getBindings('mcp')).WORKSPACE_SERVER;const request={method:'POST',headers:{authorization:'Bearer '+readonly.token,'content-type':'application/json',accept:'application/json, text/event-stream'},body:JSON.stringify({jsonrpc:'2.0',id:999,method:'tools/call',params:{name:'plan_task',arguments:{commandId:accepted.commandId}}})};assert.equal((await f.mcp.fetch(resource,request)).status,403);
 assert.equal((await binding.fetch('https://workspace.internal/tool',{method:'POST',headers:{authorization:'Bearer '+readonly.token,'content-type':'application/json'},body:JSON.stringify({tool:'plan_task',arguments:{commandId:accepted.commandId}})})).status,403);
});
