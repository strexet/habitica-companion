import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {fixture,enrolled,authorization,exchange,resource,accounts} from '../../chatgpt-auth/test/fixtures/oauth.mjs';
import {Client} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js';
import {StreamableHTTPClientTransport} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/streamableHttp.js';
import {workspaceCommand,commandHash} from '../../shared/workspace-commands.ts';
import {accept} from '../src/commands.ts';
import {journal} from '../src/journal.ts';
import {emptyWorkspace} from '../../shared/workspace-domain.ts';
const schedule={frequency:'weekly',interval:1,startDate:'2026-10-17',timeZoneId:'UTC',weekdays:['monday'],daysOfMonth:[]};
const now='2026-10-17T00:00:00Z';const origin='http://localhost:8789';
const command=(document,taskId,operation,patch,extra={})=>workspaceCommand({id:crypto.randomUUID(),workspaceId:document.id,expectedRevision:document.revision,taskId,operation,...(patch?{patch}:{}),...extra});
async function connect(f,p){const response=await f.workspace.fetch(origin+'/connect',{method:'POST',headers:{authorization:'Bearer '+p.managementToken,'content-type':'application/json'},body:JSON.stringify({consentVersion:'workspace-v1'})});assert.equal(response.status,201);return response.json();}
async function call(f,w,tool,args){return f.workspace.fetch(origin+'/task-api',{method:'POST',headers:{authorization:'Bearer '+w.sessionToken,'content-type':'application/json'},body:JSON.stringify({tool,arguments:args})});}
for(const name of ['command-v1','command-v2','command-v3','command-v3-clear','command-v3-details'])test('portable recurrence command hash: '+name,async()=>{
 const value=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/'+name+'.json',import.meta.url),'utf8'));
 assert.equal(await commandHash(workspaceCommand(value.command)),value.payloadHash);
 if(['command-v1','command-v2'].includes(name)){const patch=workspaceCommand({...value.command,patch:{...value.command.patch,recurrence:null,clearRecurrence:false}}).patch;assert.equal(Object.hasOwn(patch,'recurrence'),false);assert.equal(Object.hasOwn(patch,'clearRecurrence'),false);}
});
test('calendar authoring, scheduled completion and clear retain history and immutable intent',async()=>{
 let document=emptyWorkspace(crypto.randomUUID());const id=crypto.randomUUID();const created=await accept(document,command(document,id,'createDaily',{text:'Recurring reading',recurrence:schedule}),now);document=created.document;
 assert.deepEqual(document.tasks[0].recurrence,schedule);assert.equal(created.entry.projectionState,'notRequested');assert.deepEqual(created.entry.effects,[]);
 const partial=await accept(document,command(document,id,'updateTask',{notes:'Keep schedule'}),now);document=partial.document;assert.deepEqual(document.tasks[0].recurrence,schedule);
 await assert.rejects(accept(document,command(document,id,'completeTask',null,{occurrenceDate:'2026-10-18'}),now),error=>error.code==='occurrence_not_scheduled');
 const complete=await accept(document,command(document,id,'completeTask',null,{occurrenceDate:'2026-10-19'}),now);document=complete.document;
 const cleared=await accept(document,command(document,id,'updateTask',{clearRecurrence:true}),now);assert.equal(cleared.document.tasks[0].recurrence,null);assert.equal(cleared.document.completions.length,1);assert.equal(cleared.document.occurrences.length,1);assert.deepEqual(created.entry.taskSnapshot.recurrence,schedule);
 const exported={document:cleared.document,storeVersion:cleared.document.revision,commands:[created,partial,complete,cleared].map(v=>v.entry),events:[created,partial,complete,cleared].map(v=>v.event)};
 await journal(exported);
 const falseEffect=structuredClone(exported);falseEffect.commands[0].command.projectToHabitica=true;falseEffect.commands[0].payloadHash=await commandHash(falseEffect.commands[0].command);falseEffect.commands[0].projectionState='pending';falseEffect.commands[0].effects=['create'];await assert.rejects(journal(falseEffect));
 const changedSnapshot=structuredClone(exported);changedSnapshot.commands[0].taskSnapshot.recurrence.interval=2;await assert.rejects(journal(changedSnapshot));
 const missingSnapshot=structuredClone(exported);delete missingSnapshot.commands[0].taskSnapshot;await assert.rejects(journal(missingSnapshot));
});
test('invalid recurrence edits, wrong task kinds and linked projections cannot accept',async()=>{
 let document=emptyWorkspace(crypto.randomUUID());const id=crypto.randomUUID();
 for(const operation of ['createTodo','createHabit'])await assert.rejects(accept(document,command(document,id,operation,{text:'Reject',recurrence:schedule}),now),error=>error.code==='recurrence_requires_daily');
 for(const recurrence of [{...schedule,interval:0},{...schedule,weekdays:[]},{...schedule,daysOfMonth:[1]},{...schedule,endpoint:'/cron'}])assert.throws(()=>command(document,id,'createDaily',{text:'Reject',recurrence}));
 await assert.rejects(accept(document,command(document,id,'createDaily',{text:'Reject',recurrence:{...schedule,timeZoneId:'Missing/Zone'}}),now),error=>error.code==='invalid_patch');
 assert.throws(()=>command(document,id,'createDaily',{text:'Reject',recurrence:schedule,clearRecurrence:true}));
 await assert.rejects(accept(document,command(document,id,'createDaily',{text:'Reject',recurrence:schedule},{projectToHabitica:true}),now),error=>error.code==='unsupported_projection');
 document=(await accept(document,command(document,id,'createDaily',{text:'Unlinked'}),now)).document;
 for(const state of ['linked','pending','unknownOutcome','externalConflict']){
  const mapped={...document,projections:[{id:crypto.randomUUID(),taskId:id,revision:document.revision,isArchived:false,state,habiticaTaskId:state==='linked'?crypto.randomUUID():null}]};
  for(const patch of [{recurrence:schedule},{clearRecurrence:true}])await assert.rejects(accept(mapped,command(mapped,id,'updateTask',patch),now),error=>error.code==='unsupported_projection');
 }
});
test('actual MCP reviews one scheduled Daily and retains original command receipts',async t=>{
 let upstreamWrites=0;const f=await fixture(t,{workspace:true,habitica:request=>{if(request.method!=='GET')upstreamWrites++;return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:'Fixture A'}}});}});
 const p=await enrolled(f);const w=await connect(f,p);const oauth=await exchange(f,await authorization(f,p,'profile:read tasks:read habitica.tasks.write'));assert.equal(oauth.status,200);const token=(await oauth.json()).access_token;
 const c=new Client({name:'recurrence-fixture',version:'1.0.0'});t.after(()=>c.close());await c.connect(new StreamableHTTPClientTransport(new URL(resource),{requestInit:{headers:{authorization:'Bearer '+token}},fetch:(url,init)=>f.mcp.fetch(String(url),init)}));
 const invoke=async(name,args)=>{const result=await c.callTool({name,arguments:args});assert.equal(result.isError,undefined,JSON.stringify(result));return result.structuredContent;};
 const tools=await c.listTools();assert.ok(tools.tools.find(t=>t.name==='prepare_task_command').inputSchema.properties.patch.properties.recurrence);
 const input={requestId:crypto.randomUUID(),operation:'createDaily',patch:{text:'MCP recurring Daily',recurrence:schedule}};
 const preview=await invoke('prepare_task_command',input);assert.deepEqual(preview.command.patch.recurrence,schedule);assert.equal(preview.mayAffectHabitica,false);assert.equal(preview.savedInCompanion,false);
 assert.equal((await invoke('prepare_task_command',input)).preparedCommandId,preview.preparedCommandId);
 const accepted=await invoke('create_daily',{commandId:preview.preparedCommandId});assert.equal(accepted.savedInCompanion,true);assert.equal(accepted.totalProjectionCount,0);
 assert.equal((await invoke('create_daily',{commandId:preview.preparedCommandId})).commandId,accepted.commandId);
 const daily=await invoke('get_companion_task',{taskId:accepted.taskId});assert.deepEqual(daily.task.recurrence,schedule);
 const notDue=await call(f,w,'prepare_task_command',{requestId:crypto.randomUUID(),operation:'completeTask',taskId:accepted.taskId,occurrenceDate:'2026-10-18'});assert.equal(notDue.status,422);
 const done=await invoke('prepare_task_command',{requestId:crypto.randomUUID(),operation:'completeTask',taskId:accepted.taskId,occurrenceDate:'2026-10-19'});const receipt=await invoke('complete_task',{commandId:done.preparedCommandId});assert.equal(receipt.acceptedEventCount,1);assert.equal(receipt.confirmedProjectionCount,0);
 await invoke('complete_task',{commandId:done.preparedCommandId});assert.equal((await invoke('get_task_command',{commandId:done.preparedCommandId})).commandId,done.preparedCommandId);
 const clear=await invoke('prepare_task_command',{requestId:crypto.randomUUID(),operation:'updateTask',taskId:accepted.taskId,patch:{clearRecurrence:true}});assert.equal(clear.mayAffectHabitica,false);await invoke('update_task',{commandId:clear.preparedCommandId});assert.equal((await invoke('get_companion_task',{taskId:accepted.taskId})).task.recurrence,null);
 const exported=await(await f.workspace.fetch(origin+'/export',{headers:{authorization:'Bearer '+w.sessionToken}})).json();assert.equal(exported.document.completions.length,1);assert.equal(exported.document.occurrences.length,1);assert.equal(exported.commands.length,3);await journal(exported);assert.equal(upstreamWrites,0);
 const other=await connect(f,await enrolled(f,accounts[1]));assert.equal((await call(f,other,'get_task_command',{commandId:done.preparedCommandId})).status,200);assert.equal((await(await call(f,other,'get_task_command',{commandId:done.preparedCommandId})).json()).savedInCompanion,null);
});
