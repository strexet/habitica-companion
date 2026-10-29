import {test} from 'node:test';import assert from 'node:assert/strict';
import {fixture,enrolled} from '../../chatgpt-auth/test/fixtures/oauth.mjs';import {executeProfile} from '../src/executor.ts';
const origin='http://localhost:8789';
async function connect(f,p){const r=await f.workspace.fetch(origin+'/connect',{method:'POST',headers:{authorization:`Bearer ${p.managementToken}`,'content-type':'application/json'},body:JSON.stringify({consentVersion:'workspace-v1'})});assert.equal(r.status,201);return r.json();}
async function call(f,w,tool,args){return f.workspace.fetch(origin+'/task-api',{method:'POST',headers:{authorization:`Bearer ${w.sessionToken}`,'content-type':'application/json'},body:JSON.stringify({tool,arguments:args})});}
for(const mode of ['success','unknown','expired-account','changed-schedule'])test(`Daily creation ${mode} preserves a single typed dispatch`,async t=>{
 let remote=null,creates=0,scores=0,f;const today=new Date().toISOString().slice(0,10);
 f=await fixture(t,{workspace:true,projection:true,executor:true,habitica:async request=>{
  const user=request.headers.get('x-api-user');if(request.url.includes('user?userFields=_id,profile'))return Response.json({success:true,data:{_id:user,profile:{name:'Fixture'}}});
  if(request.url.endsWith('/user')&&request.method==='GET')return Response.json({success:true,data:{preferences:{dayStart:0,timezoneOffset:0},needsCron:false}});
  if(request.method==='GET'){
   if(mode==='expired-account')await f.db.prepare("UPDATE integration_projection_checks SET expires_at_utc='2000-01-01T00:00:00Z' WHERE selector='account'").run();
   if(request.url.includes('tasks/user?type=dailys'))return Response.json({success:true,data:remote?[{...remote,isDue:true}]:[]});
   return remote?Response.json({success:true,data:remote}):new Response('',{status:404});
  }
  if(request.url.endsWith('/tasks/user')){creates++;const patch=await request.json();assert.equal(patch.type,'daily');assert.equal(patch.frequency,'daily');assert.equal(patch.everyX,1);assert.deepEqual(patch.weeksOfMonth,[]);assert.equal(patch.date,undefined);assert.equal(patch.up,undefined);remote={...patch,id:crypto.randomUUID(),userId:user,completed:false,priority:1,attribute:'str',group:{},challenge:{},...(mode==='changed-schedule'?{everyX:2}:{})};return mode==='unknown'?new Response('',{status:502}):Response.json({success:true,data:remote},{status:201});}
  assert.match(request.url,/\/score\/up$/);scores++;return Response.json({success:true,data:{delta:1}});
 }});const p=await enrolled(f),w=await connect(f,p);
 const recurrence={frequency:'daily',interval:1,startDate:today,timeZoneId:'UTC',weekdays:[],daysOfMonth:[]};
 const previewResponse=await call(f,w,'prepare_task_command',{requestId:crypto.randomUUID(),operation:'createDaily',patch:{text:'Reviewed synthetic Daily',recurrence}});assert.equal(previewResponse.status,200,await previewResponse.clone().text());const preview=await previewResponse.json();assert.equal(preview.command.projectToHabitica,true);
 assert.equal((await call(f,w,'create_daily',{commandId:preview.preparedCommandId})).status,200);
 await f.workspace.fetch(origin+'/executor',{method:'POST',headers:{authorization:`Bearer ${w.sessionToken}`,'content-type':'application/json'},body:JSON.stringify({enabled:true,consentVersion:'executor-v1'})});
 const reset=async()=>{await f.workspaceDb.prepare("UPDATE workspace_runtime SET next_network_at_utc='2000-01-01T00:00:00Z'").run();await f.db.prepare("UPDATE integration_projection_state SET next_automated_at_utc='2000-01-01T00:00:00Z'").run();};const env=await f.mf.getBindings('workspace');await executeProfile(env,p.profileId,{wait:reset});
 const status=async()=>await(await call(f,w,'get_task_command',{commandId:preview.preparedCommandId})).json();const result=await status();
 if(mode==='expired-account'){assert.equal(creates,0);assert.equal(result.habiticaProjectionStatus,'blocked');return;}
 assert.equal(creates,1);assert.equal(scores,0);
 if(mode==='unknown'||mode==='changed-schedule'){
  assert.equal(result.outcomeUnknown,true);await reset();assert.equal(await executeProfile(env,p.profileId,{wait:reset}),'unknown');assert.equal(creates,1);
  const runtime=await f.workspaceDb.prepare('SELECT prepared_request_json FROM workspace_runtime WHERE profile_id=?').bind(p.profileId).first(),original=JSON.parse(runtime.prepared_request_json);
  const response=await f.projection.fetch('https://projection.internal/operation',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({...original,operation:'reconcile',arguments:{operation:original.operation,arguments:original.arguments},automated:false})});const recovered=await response.json();assert.equal(recovered.kind,mode==='unknown'?'applied':'unknown');
  if(mode==='changed-schedule')return;
  await reset();assert.equal(await executeProfile(env,p.profileId,{wait:reset}),'recovered');
 }
 assert.equal((await status()).habiticaProjectionStatus,'confirmed');assert.equal(creates,1);
 const accepted=await(await call(f,w,'create_daily',{commandId:preview.preparedCommandId})).json();assert.equal(accepted.commandId,preview.preparedCommandId);
 const complete=await(await call(f,w,'prepare_task_command',{requestId:crypto.randomUUID(),operation:'completeTask',taskId:accepted.taskId,occurrenceDate:today})).json();assert.equal((await call(f,w,'complete_task',{commandId:complete.preparedCommandId})).status,200);await reset();await executeProfile(env,p.profileId,{wait:reset});const scored=await(await call(f,w,'get_task_command',{commandId:complete.preparedCommandId})).json();assert.equal(scored.confirmedProjectionCount,1);assert.equal(scores,1);assert.equal(creates,1);
});
