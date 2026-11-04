import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {fixture,enrolled,accounts} from './fixtures/oauth.mjs';
import {reviewLinkedDailySchedule} from '../../shared/linked-daily-schedule.ts';
const id='90000000-0000-4000-8000-000000000001';
const raw=()=>({id,userId:accounts[0].habiticaUserId,type:'daily',text:'Synthetic Daily',notes:'',priority:1,attribute:'str',completed:false,up:true,down:true,checklist:[],frequency:'daily',everyX:1,startDate:'2026-10-27T00:00:00.000Z',repeat:{su:true,m:true,t:true,w:true,th:true,f:true,s:true},daysOfMonth:[],weeksOfMonth:[],isDue:true});
const desired={frequency:'weekly',interval:1,startDate:'2026-11-02',timeZoneId:'UTC',weekdays:['monday','wednesday'],daysOfMonth:[]};
async function setup(t){
 let current=raw(),prefs={timezoneOffset:0,dayStart:0},lose=false;const requests=[];
 const f=await fixture(t,{workspace:true,projection:true,habitica:async request=>{
  if(request.url.includes('user?userFields=_id,profile'))return Response.json({success:true,data:{_id:accounts[0].habiticaUserId,profile:{name:'Synthetic'}}});
  assert.ok(request.headers.get('x-client'));const path=new URL(request.url).pathname;requests.push({method:request.method,path});
  if(request.method==='PUT'){const patch=await request.json();Object.assign(current,patch);if(current.frequency==='monthly'&&current.daysOfMonth.length)current.daysOfMonth=[new Date(Date.parse(current.startDate)-prefs.timezoneOffset*60000).getUTCDate()];if(lose)return new Response('synthetic transport uncertainty',{status:500});return Response.json({success:true,data:current});}
  assert.equal(request.method,'GET','No score or Cron endpoint is authorized');
  return Response.json({success:true,data:path==='/api/v3/user'?{preferences:prefs,needsCron:false}:path==='/api/v3/tasks/user'?[current]:current});
 }});
 const p=await enrolled(f);const row=await f.db.prepare('SELECT connection_id FROM integration_profiles WHERE profile_id=?').bind(p.profileId).first();
 const call=(operation,args={},extra={})=>f.projection.fetch('https://projection.internal/operation',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({profileId:p.profileId,connectionId:row.connection_id,operation,arguments:args,automated:false,...extra})});
 async function prepared(){const account=await(await call('inspectAccount')).json(),observed=await(await call('inspectDaily',{selector:id})).json();assert.ok(observed.task,JSON.stringify(observed));const before=observed.task.schedule;const review=reviewLinkedDailySchedule(before,desired,{timezoneOffset:0,dayStart:0});return {selector:id,checkToken:observed.checkToken,expectedFingerprint:observed.task.fingerprint,accountCheckToken:account.accountCheckToken,linkedSchedule:{habiticaTaskId:id,fingerprint:observed.task.fingerprint,observedAtUtc:new Date().toISOString(),account:{timezoneOffset:0,dayStart:0},before,expectedAfter:review.expectedAfter},recurrence:desired,patch:review.expectedAfter};}
 return {...f,call,prepared,requests,get current(){return current},get prefs(){return prefs},lose(){lose=true},attempt:()=>({attemptId:crypto.randomUUID(),attemptAtUtc:new Date().toISOString()})};
}
test('independent portable schedule examples retain defaults and refuse unsupported normalization',async()=>{
 const data=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/linked-daily-schedule-v1.json',import.meta.url),'utf8'));assert.equal(data.examples.length,20);
 for(const example of data.examples){if(example.failure)assert.throws(()=>reviewLinkedDailySchedule(example.before,example.proposed,example.account),e=>e.code===example.failure,example.name);else assert.deepEqual(reviewLinkedDailySchedule(example.before,example.proposed,example.account).expectedAfter,example.expectedAfter,example.name);}
});
test('actual gateway writes one exact schedule and recovers the original normalized receipt',async t=>{
 const f=await setup(t),args=await f.prepared(),attempt=f.attempt();const response=await f.call('update',args,attempt),outcome=await response.json();assert.equal(response.status,200,JSON.stringify(outcome));assert.equal(outcome.kind,'applied');assert.equal(outcome.scheduleResult.matchesExpected,true);assert.deepEqual(outcome.scheduleResult.actual,args.patch);assert.equal(f.requests.filter(r=>r.method==='PUT').length,1);
 f.current.text='Later external title';assert.deepEqual(await(await f.call('update',args,attempt)).json(),outcome);assert.deepEqual(await(await f.call('lookup',{operation:'update',arguments:args},attempt)).json(),outcome);assert.equal(f.requests.filter(r=>r.method==='PUT').length,1);assert.ok(!f.requests.some(r=>r.path.includes('score')||r.path.includes('cron')));
});
test('actual gateway rechecks external fields and account settings immediately before PUT',async t=>{
 const f=await setup(t),args=await f.prepared();f.current.text='External edit after preview';let response=await f.call('update',args,f.attempt());assert.equal(response.status,409);assert.equal((await response.json()).code,'external_edit_review_required');assert.equal(f.requests.filter(r=>r.method==='PUT').length,0);
 f.current.text='Synthetic Daily';f.prefs.timezoneOffset=-420;response=await f.call('update',args,f.attempt());assert.equal(response.status,409);assert.equal((await response.json()).code,'linked_schedule_precondition_changed');assert.equal(f.requests.filter(r=>r.method==='PUT').length,0);
});
test('actual gateway never proves an uncertain original write from later state equality',async t=>{
 const f=await setup(t),args=await f.prepared(),attempt=f.attempt();f.lose();assert.equal((await(await f.call('update',args,attempt)).json()).kind,'unknown');const reads=f.requests.length;
 assert.equal((await(await f.call('reconcile',{operation:'update',arguments:args},attempt)).json()).code,'schedule_outcome_requires_original_receipt');assert.equal(f.requests.length,reads);assert.equal((await(await f.call('lookup',{operation:'update',arguments:args},attempt)).json()).kind,'unknown');assert.equal((await f.call('update',args,f.attempt())).status,409);assert.equal(f.requests.filter(r=>r.method==='PUT').length,1);
});
test('ordinary Daily title PUT cannot erase retained monthly days or smuggle schedule fields',async t=>{
 const f=await setup(t);Object.assign(f.current,{frequency:'monthly',daysOfMonth:[5,27]});const observed=await(await f.call('inspectDaily',{selector:id})).json();const args={selector:id,checkToken:observed.checkToken,expectedFingerprint:observed.task.fingerprint,patch:{text:'Requested title'}};
 const response=await f.call('update',args,f.attempt());assert.equal(response.status,409);assert.equal((await response.json()).code,'daily_month_update_unrepresentable');assert.deepEqual(f.current.daysOfMonth,[5,27]);assert.equal(f.requests.filter(r=>r.method==='PUT').length,0);
 const refused=await f.call('update',{...args,patch:{frequency:'daily',everyX:1,startDate:raw().startDate,repeat:raw().repeat,daysOfMonth:[],weeksOfMonth:[]}},f.attempt());assert.equal(refused.status,422);assert.equal((await refused.json()).code,'unsupported_schedule_edit');
});
