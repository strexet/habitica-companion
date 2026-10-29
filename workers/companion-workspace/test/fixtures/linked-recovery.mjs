import assert from 'node:assert/strict';
import {fixture,enrolled,accounts} from '../../../chatgpt-auth/test/fixtures/oauth.mjs';
import {habiticaDay} from '../../../shared/cron.ts';
import {remoteTask} from '../../../shared/projection.ts';
import {executeProfile} from '../../src/executor.ts';
import {seedLegacyCommand} from './legacy-todo.mjs';
export const origin='http://localhost:8789';
export const call=(f,w,path,body)=>f.workspace.fetch(origin+path,{method:body===undefined?'GET':'POST',headers:{authorization:'Bearer '+w.sessionToken,'content-type':'application/json'},body:body===undefined?undefined:JSON.stringify(body)});
export async function linkedFixture(t,kind='todo',options={}){
 const dayStart=(new Date().getUTCHours()+12)%24,day=habiticaDay(Date.now(),dayStart,0),nativeId=crypto.randomUUID();
 const state={up:0,down:0,reads:0,mode:'success',hook:null,lose:false,user:{_id:accounts[0].habiticaUserId,needsCron:false,lastCron:new Date(Date.parse(day.dayStartUtc)+1000).toISOString(),preferences:{dayStart,timezoneOffset:0,timezoneOffsetAtLastCron:0}},task:{id:nativeId,userId:accounts[0].habiticaUserId,type:kind,text:'Synthetic linked recovery',notes:'',priority:1,attribute:'str',completed:false,value:1,streak:3,checklist:[],group:{},challenge:{},...(kind==='daily'?{frequency:'daily',everyX:1,startDate:day.day+'T00:00:00.000Z',repeat:{su:true,m:true,t:true,w:true,th:true,f:true,s:true},daysOfMonth:[],weeksOfMonth:[],isDue:true}:{})}};
 let f;f=await fixture(t,{workspace:true,projection:true,executor:true,...options,projectionService:async request=>{
  const body=await request.clone().json(),response=await f.projection.fetch(request);
  if(body.operation==='recoverTask'&&state.lose){state.lose=false;throw Error('Synthetic lost Auth acknowledgement');}return response;
 },habitica:async request=>{
  const url=new URL(request.url);
  if(url.pathname==='/api/v3/user'&&url.searchParams.has('userFields'))return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:request.headers.get('x-api-user')===accounts[0].habiticaUserId?'Fixture A':'Fixture B'}}});
  if(request.method==='GET'){state.reads++;if(state.hook)await state.hook(request);return Response.json({success:true,data:url.pathname==='/api/v3/user'?structuredClone(state.user):url.pathname==='/api/v3/tasks/user'?[structuredClone(state.task)]:structuredClone(state.task)});}
  assert.equal(request.method,'POST');
  if(url.pathname.endsWith('/score/up')){state.up++;state.task.completed=true;state.task.dateCompleted=new Date().toISOString();return Response.json({success:true,data:{delta:1}});}
  assert.equal(url.pathname,'/api/v3/tasks/'+nativeId+'/score/down');state.down++;
  if(state.mode==='failure')return new Response('',{status:500});
  if(state.mode==='lost')throw Error('Synthetic unknown upstream');
  state.task.completed=false;return Response.json({success:true,data:{delta:-1}});
 }});
 const p=await enrolled(f),connected=await f.workspace.fetch(origin+'/connect',{method:'POST',headers:{authorization:'Bearer '+p.managementToken,'content-type':'application/json'},body:JSON.stringify({consentVersion:'workspace-v1'})});assert.equal(connected.status,201);const w=await connected.json();
 const taskId=crypto.randomUUID();await seedLegacyCommand(f,{id:crypto.randomUUID(),workspaceId:w.workspaceId,expectedRevision:1,operation:kind==='todo'?'createTodo':'createDaily',taskId,patch:{text:state.task.text,...(kind==='daily'?{recurrence:{frequency:'daily',interval:1,startDate:day.day,timeZoneId:'UTC',weekdays:[],daysOfMonth:[]}}:{})}});
 let document=JSON.parse((await f.workspaceDb.prepare('SELECT document_json FROM workspaces').first()).document_json);
 const native=await remoteTask(state.task,state.task.userId,kind==='daily');document.projections=[{id:crypto.randomUUID(),revision:document.revision,taskId,state:'linked',habiticaTaskId:nativeId,fingerprint:native.fingerprint,isArchived:false}];
 await f.workspaceDb.prepare('UPDATE workspaces SET document_json=?').bind(JSON.stringify(document)).run();
 const completionCommand={id:crypto.randomUUID(),workspaceId:w.workspaceId,expectedRevision:document.revision,operation:'completeTask',taskId,...(kind==='daily'?{occurrenceDate:day.day}:{}),projectToHabitica:true};
 const submitted=await call(f,w,'/commands',completionCommand);assert.equal(submitted.status,201,await submitted.clone().text());
 assert.equal((await call(f,w,'/executor',{enabled:true,consentVersion:'executor-v1'})).status,200);
 const env=await f.mf.getBindings('workspace'),wait=async()=>{await f.workspaceDb.prepare("UPDATE workspace_runtime SET next_network_at_utc='2000-01-01T00:00:00Z'").run();await f.db.prepare("UPDATE integration_projection_state SET next_automated_at_utc='2000-01-01T00:00:00Z'").run();};
 assert.equal(await executeProfile(env,p.profileId,{wait}),'applied');assert.equal(state.up,1);
 const exported=async()=>{const r=await call(f,w,'/export');assert.equal(r.status,200);return r.json();};
 const before=await exported(),completion=before.document.completions[0];
 const api=async(name,args)=>{const r=await call(f,w,'/linked-recovery-api',{tool:name,arguments:args});return {status:r.status,body:await r.json()};};
 const request={requestId:crypto.randomUUID(),taskId,completionId:completion.id,expectedRevision:before.document.revision};
 const prepare=async()=>{const r=await api('prepare_linked_task_recovery',request);assert.equal(r.status,200,JSON.stringify(r));return r.body;};
 const accept=review=>api('accept_linked_task_recovery',{commandId:review.commandId,contextHash:review.contextHash,reviewHash:review.review.reviewHash});
 const recover=commandId=>api('get_linked_task_recovery_command',{commandId});
 return {f,p,w,state,day,nativeId,taskId,before,request,completion,completionCommand,env,wait,api,exported,prepare,accept,recover};
}
