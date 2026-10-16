import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {fixture,enrolled,authorization,exchange,accounts,issuer} from './fixtures/oauth.mjs';
import {habiticaDay} from '../../shared/cron.ts';
const partyId='90000000-0000-4000-8000-000000000081',taskId='90000000-0000-4000-8000-000000000082';
const scopes='profile:read account:read tasks:read party:read habitica.cron.execute habitica.spells.execute';
const gate=()=>{let resolve;return {promise:new Promise(r=>resolve=r),release:()=>resolve()};};
async function setup(t,enabled=true){
 const dayStart=(new Date().getUTCHours()+12)%24;const day=habiticaDay(Date.now(),dayStart,0);
 const state={needsCron:true,lastCron:new Date(Date.parse(day.dayStartUtc)-86400000).toISOString(),posts:0,postStatus:200,malformed:false,advanceOnFailure:false,partyId,questHp:100,groupHeaders:{},groupHook:null,userId:null,
  preferences:{dayStart,timezoneOffset:0,timezoneOffsetAtLastCron:0,sleep:false,disableClasses:false},
  task:{id:taskId,userId:accounts[0].habiticaUserId,type:'daily',text:'Synthetic yesterday Daily',completed:false,isDue:true,checklist:[{id:'90000000-0000-4000-8000-000000000083',text:'Reviewed item',completed:false}],priority:1,value:0,frequency:'daily',everyX:1},
  stats:{class:'healer',lvl:20,hp:40,mp:100,buffs:{str:5},str:10,int:10,con:10,per:10}};
 const calls=[];
 const f=await fixture(t,{workspace:true,party:true,spells:true,cron:enabled,projection:true,habitica:async request=>{
  const url=new URL(request.url),actor=request.headers.get('x-api-user');assert.ok(request.headers.get('x-client'));calls.push({method:request.method,path:url.pathname,filtered:url.searchParams.has('userFields')});
  if(request.method==='GET'&&url.pathname==='/api/v3/user'){
   const data={_id:state.userId??actor,profile:{name:actor===accounts[0].habiticaUserId?'Fixture A':'Fixture B'},stats:structuredClone(state.stats),preferences:{...state.preferences},lastCron:state.lastCron,needsCron:state.needsCron,party:{_id:state.partyId,quest:{key:'whale',progress:{up:2,down:0}}},items:{gear:{equipped:{weapon:'fixture-healer'}}},flags:{cronCount:1},auth:{private:'never-return'}};
   if(url.searchParams.has('userFields'))delete data.needsCron;return Response.json({success:true,data});
  }
  if(request.method==='GET'&&url.pathname==='/api/v3/tasks/user'){
   assert.equal(url.searchParams.get('history'),'false');assert.ok(Number.isFinite(Date.parse(url.searchParams.get('dueDate'))));
   return Response.json({success:true,data:[structuredClone(state.task)]});
  }
  if(request.method==='GET'&&url.pathname==='/api/v3/groups/party'){
   if(state.groupHook)await state.groupHook();return Response.json({success:true,data:{_id:state.partyId,type:'party',leader:accounts[0].habiticaUserId,name:'Synthetic party',memberCount:2,quest:{key:'whale',active:true,progress:{hp:state.questHp},private:'never-return'}}},{headers:state.groupHeaders});
  }
  assert.equal(url.pathname,'/api/v3/cron');assert.equal(request.method,'POST');assert.deepEqual(await request.json(),{});state.posts++;
  if(state.postStatus===200||state.advanceOnFailure){state.lastCron=new Date().toISOString();state.needsCron=false;}
  if(state.postStatus!==200)return new Response('private fixture-a-token',{status:state.postStatus,headers:{'retry-after':'60'}});
  return state.malformed?Response.json({success:true,data:{unexpected:'not-complete-proof'}}):Response.json({success:true,data:{},private:'never-return'});
 }});
 const profile=await enrolled(f);const row=await f.db.prepare('SELECT connection_id FROM integration_profiles WHERE profile_id=?').bind(profile.profileId).first();
 const token=await(await exchange(f,await authorization(f,profile,scopes))).json();const bindings=await f.mf.getBindings('mcp');
 const who={profileId:profile.profileId,connectionId:row.connection_id};
 const call=(operation,args={},extra={},bearer=token.access_token)=>bindings.AUTH_CRON.fetch('https://cron.internal/operation',{method:'POST',headers:{authorization:'Bearer '+bearer,'content-type':'application/json'},body:JSON.stringify({...who,operation,arguments:args,...extra})});
 const status=async()=>{const response=await call('status');assert.equal(response.status,200,await response.clone().text());return response.json();};
 const intent=async()=>{const observed=await status();return {args:{expectedPreconditionHash:observed.preconditionHash,expectedDay:observed.context.day,expectedLastCron:observed.context.lastCron},attempt:{attemptId:crypto.randomUUID(),attemptAtUtc:new Date().toISOString()}};};
 return {...f,state,calls,profile,who,bindings,token,call,status,intent};
}

test('fixed-offset day boundaries match the neutral C# fixture without a timezone-transition parity claim',async()=>{
 const data=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/cron-day-v1.json',import.meta.url),'utf8'));
 for(const row of data.cases){const value=habiticaDay(Date.parse(row.nowUtc),row.dayStart,row.timezoneOffset);assert.equal(value.day,row.day,row.name);assert.equal(value.dayStartUtc,row.dayStartUtc,row.name);assert.equal(Date.parse(value.reviewDateUtc),Date.parse(row.dayStartUtc)-1);}
 for(const [start,offset]of [[24,0],[0,900],[0,0.5]])assert.throws(()=>habiticaDay(Date.now(),start,offset));
});

test('verified Cron status is bounded and one successful receipt never repeats the accepted request',async t=>{
 const f=await setup(t);const observed=await f.status();assert.equal(observed.context.canPrepare,true);assert.equal(observed.context.unfinishedDueDailyCount,1);assert.equal(observed.context.risks.personalDamage,'unknown');assert.ok(observed.context.warnings.some(w=>w.includes('buff')));assert.ok(!JSON.stringify(observed).includes('never-return'));assert.ok(!JSON.stringify(observed).includes('fixture-healer'));
 const intent=await f.intent();const first=await(await f.call('execute',intent.args,intent.attempt)).json();assert.deepEqual(first,{kind:'applied',code:'applied'});
 assert.deepEqual(await(await f.call('execute',intent.args,intent.attempt)).json(),first);assert.deepEqual(await(await f.call('lookup',intent.args,intent.attempt)).json(),first);assert.equal(f.state.posts,1);
 assert.equal((await f.call('execute',{...intent.args,expectedDay:'2000-01-01'},intent.attempt)).status,409);
 const text=JSON.stringify(await f.db.prepare('SELECT * FROM integration_projection_attempts').all());for(const value of ['fixture-a-token','never-return','Synthetic yesterday Daily'])assert.ok(!text.includes(value));
 assert.notEqual((await f.auth.fetch(issuer+'/cron',{method:'POST'})).status,200);
});

test('fresh day/account/Daily/checklist/quest changes refuse execution before any Cron effect',async t=>{
 const f=await setup(t);const intent=await f.intent();
 const changes=[()=>{f.state.stats.hp--;},()=>{f.state.task.completed=true;},()=>{f.state.task.checklist[0].completed=true;},()=>{f.state.questHp--;},()=>{f.state.preferences.dayStart=(f.state.preferences.dayStart+1)%24;}];
 for(const change of changes){const old=structuredClone(f.state);change();const response=await f.call('execute',intent.args,{...intent.attempt,attemptId:crypto.randomUUID()});assert.equal((await response.json()).kind,'refused');Object.assign(f.state,old);}
 assert.equal((await f.call('execute',{...intent.args,expectedDay:'2000-01-01'},intent.attempt)).status,409);
 assert.equal((await f.call('execute',intent.args,{...intent.attempt,attemptAtUtc:'2000-01-01T00:00:00Z'})).status,409);assert.equal(f.state.posts,0);
});

test('already-started days, unknown readiness, timezone transitions and incomplete safety facts cannot prepare',async t=>{
 const f=await setup(t);f.state.needsCron=false;assert.equal((await f.status()).context.unavailableReason,'cron_not_needed');
 f.state.needsCron=undefined;assert.equal((await f.status()).context.unavailableReason,'cron_precondition_unknown');
 f.state.needsCron=true;f.state.preferences.timezoneOffsetAtLastCron=60;assert.equal((await f.status()).context.unavailableReason,'timezone_transition_review_required');
 f.state.preferences.timezoneOffsetAtLastCron=0;f.state.stats.mp='missing';assert.equal((await f.call('status')).status,409);f.state.stats.mp=100;f.state.task.isDue=undefined;assert.equal((await f.call('status')).status,502);assert.equal(f.state.posts,0);
});

test('independent grants and profile identity prevent a private Cron effect',async t=>{
 const f=await setup(t);const intent=await f.intent();const other=await enrolled(f,accounts[1]);const otherToken=await(await exchange(f,await authorization(f,other,scopes))).json();
 assert.equal((await f.call('execute',intent.args,intent.attempt,otherToken.access_token)).status,401);
 const readToken=await(await exchange(f,await authorization(f,f.profile,'profile:read account:read tasks:read party:read'))).json();assert.equal((await f.call('execute',intent.args,intent.attempt,readToken.access_token)).status,403);assert.equal(f.state.posts,0);
});

test('overlapping copies cannot release the original lease or dispatch another Cron request',async t=>{
 const f=await setup(t);const intent=await f.intent();const entered=gate(),resume=gate();f.state.groupHook=async()=>{entered.release();await resume.promise;};
 const pending=f.call('execute',intent.args,intent.attempt);await Promise.race([entered.promise,pending.then(()=>assert.fail('preflight gate not reached'))]);
 assert.equal((await(await f.call('execute',intent.args,intent.attempt)).json()).kind,'unknown');assert.equal((await(await f.call('execute',intent.args,{...intent.attempt,attemptId:crypto.randomUUID()})).json()).code,'gameplay_busy');
 assert.ok((await f.db.prepare('SELECT lease_token FROM integration_projection_state').first()).lease_token);resume.release();assert.equal((await(await pending).json()).kind,'applied');assert.equal(f.state.posts,1);
});

test('day progress after a failed response remains unknown and fences task and spell effects',async t=>{
 const f=await setup(t);const intent=await f.intent();f.state.postStatus=500;f.state.advanceOnFailure=true;
 const outcome=await(await f.call('execute',intent.args,intent.attempt)).json();assert.equal(outcome.kind,'unknown');
 const progress=await(await f.call('progress',{previousLastCron:intent.args.expectedLastCron})).json();assert.equal(progress.needsCron,false);assert.equal(progress.dayProgressObserved,true);assert.equal(progress.confirmsRequest,false);
 assert.deepEqual(await(await f.call('lookup',intent.args,intent.attempt)).json(),outcome);assert.equal((await(await f.call('execute',intent.args,{...intent.attempt,attemptId:crypto.randomUUID()})).json()).code,'unknown_outcome_pending');
 const projection=await f.projection.fetch('https://projection.internal/operation',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({...f.who,operation:'score',arguments:{selector:taskId,checkToken:'a'.repeat(43),expectedFingerprint:'b'.repeat(64),direction:'up'},automated:false,attemptId:crypto.randomUUID(),attemptAtUtc:new Date().toISOString()})});assert.equal((await projection.json()).code,'unknown_outcome_pending');
 const spell=await f.bindings.AUTH_SPELL.fetch('https://spell.internal/operation',{method:'POST',headers:{authorization:'Bearer '+f.token.access_token,'content-type':'application/json'},body:JSON.stringify({...f.who,operation:'cast',arguments:{spellId:'healAll',partyId,expectedPreconditionHash:'b'.repeat(64),remainingCount:1},attemptId:crypto.randomUUID(),attemptAtUtc:new Date().toISOString()})});assert.equal((await spell.json()).code,'unknown_outcome_pending');assert.equal(f.state.posts,1);
});

test('any dispatched non-success or malformed success remains unknown without replay',async t=>{
 const f=await setup(t);const intent=await f.intent();f.state.postStatus=403;
 assert.equal((await(await f.call('execute',intent.args,intent.attempt)).json()).kind,'unknown');assert.equal((await(await f.call('execute',intent.args,intent.attempt)).json()).kind,'unknown');assert.equal(f.state.posts,1);
 assert.equal((await f.db.prepare('SELECT credential_repair_required FROM integration_read_state').first()).credential_repair_required,0);
});

test('final-read quota and failed durable dispatch reservation prevent an upstream Cron call',async t=>{
 const f=await setup(t);const intent=await f.intent();f.state.groupHeaders={'x-ratelimit-remaining':'0','retry-after':'60'};
 const limited=await(await f.call('execute',intent.args,intent.attempt)).json();assert.equal(limited.code,'rate_limited');assert.ok(limited.retryAtUtc);assert.equal(f.state.posts,0);
 await f.db.prepare('UPDATE integration_read_state SET blocked_until_utc=NULL').run();f.state.groupHeaders={};
 await f.db.prepare("CREATE TRIGGER fixture_cron_dispatch_failure BEFORE INSERT ON integration_projection_attempts BEGIN SELECT RAISE(ABORT,'fixture failure'); END").run();
 assert.equal((await f.call('execute',intent.args,{...intent.attempt,attemptId:crypto.randomUUID()})).status,503);assert.equal(f.state.posts,0);
});

test('revoking the credential while preflight is held prevents dispatch',async t=>{
 const f=await setup(t);const intent=await f.intent();const entered=gate(),resume=gate();f.state.groupHook=async()=>{entered.release();await resume.promise;};
 const pending=f.call('execute',intent.args,intent.attempt);await Promise.race([entered.promise,pending.then(()=>assert.fail('preflight gate not reached'))]);
 assert.equal((await f.auth.fetch(`${issuer}/profiles/${f.profile.profileId}/disconnect`,{method:'POST',headers:{authorization:'Bearer '+f.profile.managementToken}})).status,200);resume.release();assert.equal((await pending).status,401);assert.equal(f.state.posts,0);
});

test('replacing the OAuth grant during preflight denies the old token before dispatch',async t=>{
 const f=await setup(t);const intent=await f.intent();const entered=gate(),resume=gate();f.state.groupHook=async()=>{entered.release();await resume.promise;};
 const pending=f.call('execute',intent.args,intent.attempt);await Promise.race([entered.promise,pending.then(()=>assert.fail('preflight gate not reached'))]);
 await exchange(f,await authorization(f,f.profile,'profile:read account:read tasks:read party:read'));
 resume.release();assert.equal((await pending).status,401);assert.equal(f.state.posts,0);
});

test('no-party accounts can prepare; receipt lookup never sends another upstream request',async t=>{
 const f=await setup(t);f.state.partyId='';const observed=await f.status();assert.equal(observed.context.party,null);assert.equal(observed.context.canPrepare,true);
 const intent=await f.intent();const before=f.calls.length;
 assert.deepEqual(await(await f.call('lookup',intent.args,intent.attempt)).json(),{kind:'unknown',code:'attempt_not_found'});assert.equal(f.calls.length,before);assert.equal(f.state.posts,0);
 f.state.userId=accounts[1].habiticaUserId;assert.equal((await f.call('status')).status,502);assert.equal(f.state.posts,0);
});

test('unknown task receipts fence Cron and disabled configuration exposes no new private action',async t=>{
 const f=await setup(t);const intent=await f.intent();await f.db.prepare("INSERT INTO integration_projection_attempts(profile_id,connection_id,attempt_id,payload_hash,operation,state,created_at_utc) VALUES(?,?,?,?,'score','unknown',?)").bind(f.who.profileId,f.who.connectionId,crypto.randomUUID(),'a'.repeat(64),new Date().toISOString()).run();assert.equal((await(await f.call('execute',intent.args,intent.attempt)).json()).code,'unknown_outcome_pending');assert.equal(f.state.posts,0);
});

test('disabled Cron status is unavailable and malformed success cannot confirm completion',async t=>{
 const disabled=await setup(t,false);assert.equal((await disabled.call('status')).status,503);assert.equal(disabled.state.posts,0);
 const f=await setup(t);const intent=await f.intent();f.state.malformed=true;assert.equal((await(await f.call('execute',intent.args,intent.attempt)).json()).kind,'unknown');assert.equal(f.state.posts,1);
});
