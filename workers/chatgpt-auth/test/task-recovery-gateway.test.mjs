import {test} from 'node:test';import assert from 'node:assert/strict';
import {fixture,enrolled,accounts,issuer} from './fixtures/oauth.mjs';
import {habiticaDay} from '../../shared/cron.ts';
const taskId='90000000-0000-4000-8000-000000000201',contextHash='a'.repeat(64),itemId='90000000-0000-4000-8000-000000000202';
async function setup(t,kind='todo'){
 const dayStart=(new Date().getUTCHours()+12)%24,day=habiticaDay(Date.now(),dayStart,0);
 const state={posts:0,mode:'success',hook:null,user:{_id:accounts[0].habiticaUserId,needsCron:false,lastCron:new Date(Date.parse(day.dayStartUtc)+1000).toISOString(),preferences:{dayStart,timezoneOffset:0,timezoneOffsetAtLastCron:0}},task:{id:taskId,userId:accounts[0].habiticaUserId,type:kind,text:'Synthetic recovery only',notes:'never-store-private-task-note',priority:1,attribute:'str',completed:true,value:1,streak:3,dateCompleted:new Date().toISOString(),checklist:[{id:itemId,text:'Synthetic item',completed:true}],group:{},challenge:{},...(kind==='daily'?{frequency:'daily',everyX:1,startDate:day.day+'T00:00:00.000Z',repeat:{su:true,m:true,t:true,w:true,th:true,f:true,s:true},daysOfMonth:[],weeksOfMonth:[],isDue:true}:{})}};
 const f=await fixture(t,{workspace:true,projection:true,habitica:async request=>{
  const url=new URL(request.url);
  if(url.pathname==='/api/v3/user'&&url.searchParams.has('userFields'))return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:'Synthetic'}}});
  assert.ok(request.headers.get('x-client'));assert.equal(request.headers.get('x-api-user'),accounts[0].habiticaUserId);
  if(request.method==='GET'){assert.ok(['/api/v3/user','/api/v3/tasks/user','/api/v3/tasks/'+taskId].includes(url.pathname));if(state.hook)await state.hook(request);return Response.json({success:true,data:url.pathname==='/api/v3/user'?structuredClone(state.user):url.pathname==='/api/v3/tasks/user'?[structuredClone(state.task)]:structuredClone(state.task)});}
  assert.equal(request.method,'POST');assert.equal(url.pathname,'/api/v3/tasks/'+taskId+'/score/down');assert.equal(await request.text(),'');state.posts++;
  if(state.mode==='lost')throw Error('Synthetic lost acknowledgement');
  if(state.mode==='failure')return new Response('',{status:500});
  if(state.mode==='refused-response')return new Response('',{status:409});
  if(state.mode==='malformed')return Response.json({success:true,data:{delta:'unknown'}});
  if(state.mode==='positive')return Response.json({success:true,data:{delta:1}});
  state.task.completed=false;return Response.json({success:true,data:{delta:-1,hp:50,mp:5,gp:2,exp:20}});
 }});
 const profile=await enrolled(f),row=await f.db.prepare('SELECT connection_id,grant_epoch FROM integration_profiles WHERE profile_id=?').bind(profile.profileId).first();
 const who={profileId:profile.profileId,connectionId:row.connection_id};
 const intent={selector:taskId,taskKind:kind,...(kind==='daily'?{occurrenceDate:day.day}:{}),contextHash};
 const call=(operation,args={},extra={})=>f.projection.fetch('https://projection.internal/operation',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({...who,operation,arguments:args,automated:false,...extra})});
 const inspect=async(extra={})=>{const response=await call('inspectRecovery',{...intent,...extra});assert.equal(response.status,200,await response.clone().text());return response.json();};
 const execute=(review,extra={})=>({args:{...intent,checkToken:review.checkToken,expectedFingerprint:review.task.fingerprint},attempt:{attemptId:crypto.randomUUID(),attemptAtUtc:new Date().toISOString(),...extra}});
 return {...f,profile,who,state,day,intent,call,inspect,execute,grantEpoch:row.grant_epoch};
}
for(const kind of ['todo','daily'])test('private '+kind+' recovery binds exact source context and only the original successful receipt',async t=>{
 const f=await setup(t,kind),review=await f.inspect(),original=f.execute(review);assert.equal(review.review.contextHash,contextHash);assert.ok(Object.values(review.review.resourceChanges).every(value=>value==='unknown'));
 const outcome=await(await f.call('recoverTask',original.args,original.attempt)).json();assert.equal(outcome.kind,'applied');assert.equal(outcome.code,'task_recovered');assert.deepEqual(outcome.recovery,review.review);assert.equal(outcome.delta,undefined);assert.equal(f.state.posts,1);
 assert.equal((await f.call('recoverTask',original.args,f.execute(review).attempt)).status,409);assert.equal(f.state.posts,1);
 await f.db.prepare("DELETE FROM integration_projection_checks").run();
 assert.deepEqual(await(await f.call('recoverTask',original.args,original.attempt)).json(),outcome);
 assert.deepEqual(await(await f.call('lookup',{operation:'recoverTask',arguments:original.args},original.attempt)).json(),outcome);assert.equal(f.state.posts,1);
 assert.equal((await f.call('recoverTask',{...original.args,contextHash:'b'.repeat(64)},original.attempt)).status,409);
 const stored=JSON.stringify(await f.db.prepare('SELECT * FROM integration_projection_attempts').all());assert.ok(!stored.includes('never-store-private-task-note'));assert.ok(!stored.includes(accounts[0].apiToken));
 assert.notEqual((await f.auth.fetch(issuer+'/projection',{method:'POST'})).status,200);
});
for(const mode of ['lost','failure','refused-response','malformed','positive'])test('recovery '+mode+' remains unknown without replay or current-state reconciliation',async t=>{
 const f=await setup(t),review=await f.inspect(),original=f.execute(review);f.state.mode=mode;
 assert.equal((await(await f.call('recoverTask',original.args,original.attempt)).json()).kind,'unknown');assert.equal(f.state.posts,1);f.state.task.completed=false;
 assert.equal((await(await f.call('recoverTask',original.args,original.attempt)).json()).kind,'unknown');
 assert.equal((await(await f.call('reconcile',{operation:'recoverTask',arguments:original.args},original.attempt)).json()).kind,'unknown');
 assert.equal((await(await f.call('recoverTask',original.args,f.execute(review).attempt)).json()).code,'unknown_outcome_pending');assert.equal(f.state.posts,1);
});
test('changed context, task contents/checklist and stale single-use review refuse before down dispatch',async t=>{
 const f=await setup(t),review=await f.inspect();const original=f.execute(review);
 assert.equal((await(await f.call('recoverTask',{...original.args,contextHash:'b'.repeat(64)},original.attempt)).json()).code,'recovery_precondition_mismatch');
 f.state.task.checklist[0].completed=false;assert.equal((await(await f.call('recoverTask',original.args,original.attempt)).json()).code,'recovery_state_changed');assert.equal(f.state.posts,0);
 f.state.task.checklist[0].completed=true;await f.db.prepare("UPDATE integration_projection_checks SET expires_at_utc='2000-01-01T00:00:00Z'").run();assert.equal((await f.call('recoverTask',original.args,original.attempt)).status,409);assert.equal(f.state.posts,0);
});
for(const field of ['foreign','group','group-copy','challenge','challenge-copy','incomplete','habit'])test('recovery observation rejects '+field+' targets',async t=>{
 const f=await setup(t);if(field==='foreign')f.state.task.userId=accounts[1].habiticaUserId;
 else if(field==='incomplete')f.state.task.completed=false;else if(field==='habit')f.state.task.type='habit';
 else f.state.task[field.startsWith('group')?'group':'challenge']={[field.endsWith('copy')?'taskId':'id']:taskId};
 assert.notEqual((await f.call('inspectRecovery',f.intent)).status,200);assert.equal(f.state.posts,0);assert.equal((await f.db.prepare('SELECT COUNT(*) AS n FROM integration_projection_checks').first()).n,0);
});
test('Daily recovery rejects prior day, needsCron, unknown readiness and changed account-day context',async t=>{
 const f=await setup(t,'daily');assert.equal((await f.call('inspectRecovery',{...f.intent,occurrenceDate:'2000-01-01'})).status,409);
 for(const readiness of [true,undefined]){f.state.user.needsCron=readiness;assert.equal((await f.call('inspectRecovery',f.intent)).status,409);}
 f.state.user.needsCron=false;const review=await f.inspect(),original=f.execute(review);f.state.user.preferences.dayStart=(f.state.user.preferences.dayStart+1)%24;
 assert.equal((await f.call('recoverTask',original.args,original.attempt)).status,409);assert.equal(f.state.posts,0);
});
test('recovery token cannot widen ordinary scoring or unrelated updates and normal non-Habit down remains refused',async t=>{
 const f=await setup(t),review=await f.inspect();
 for(const operation of ['score','update']){const args={selector:taskId,checkToken:review.checkToken,expectedFingerprint:review.task.fingerprint,...(operation==='score'?{direction:'down'}:{patch:{text:'Changed'}})};assert.equal((await f.call(operation,args,f.execute(review).attempt)).status,409);}
 const normal=await(await f.call('inspect',{selector:taskId})).json();assert.equal((await f.call('score',{selector:taskId,checkToken:normal.checkToken,expectedFingerprint:normal.task.fingerprint,direction:'down'},f.execute(review).attempt)).status,409);assert.equal(f.state.posts,0);
});
test('grant or account revocation and account lease fencing refuse before dispatch',async t=>{
 const f=await setup(t),review=await f.inspect(),original=f.execute(review,{requiredGrantEpoch:f.grantEpoch});
 await f.db.prepare('UPDATE integration_profiles SET grant_epoch=? WHERE profile_id=?').bind(crypto.randomUUID(),f.profile.profileId).run();assert.equal((await f.call('recoverTask',original.args,original.attempt)).status,409);
 await f.db.prepare('UPDATE integration_profiles SET revoked_at_utc=? WHERE profile_id=?').bind(new Date().toISOString(),f.profile.profileId).run();assert.equal((await f.call('recoverTask',original.args,original.attempt)).status,401);assert.equal(f.state.posts,0);
});
test('reservation or receipt persistence failure cannot create another send',async t=>{
 const f=await setup(t),review=await f.inspect(),original=f.execute(review);
 await f.db.prepare("CREATE TRIGGER recovery_reservation_failure BEFORE INSERT ON integration_projection_attempts BEGIN SELECT RAISE(ABORT,'synthetic reservation failure'); END").run();assert.notEqual((await f.call('recoverTask',original.args,original.attempt)).status,200);assert.equal(f.state.posts,0);await f.db.prepare('DROP TRIGGER recovery_reservation_failure').run();
 await f.db.prepare("CREATE TRIGGER recovery_receipt_failure BEFORE UPDATE ON integration_projection_attempts WHEN NEW.state='applied' BEGIN SELECT RAISE(ABORT,'synthetic receipt failure'); END").run();assert.equal((await(await f.call('recoverTask',original.args,original.attempt)).json()).kind,'unknown');assert.equal(f.state.posts,1);assert.equal((await(await f.call('recoverTask',original.args,original.attempt)).json()).kind,'unknown');assert.equal(f.state.posts,1);
});
for(const blockingState of ['dispatched','unknown'])test('unresolved cross-tool '+blockingState+' work blocks recovery review and dispatch',async t=>{
 const f=await setup(t),review=await f.inspect(),original=f.execute(review);
 await f.db.prepare("INSERT INTO integration_projection_attempts(profile_id,connection_id,attempt_id,payload_hash,operation,state,created_at_utc) VALUES(?,?,?,?,?,?,?)").bind(f.who.profileId,f.who.connectionId,crypto.randomUUID(),'c'.repeat(64),'spell',blockingState,new Date().toISOString()).run();
 assert.equal((await(await f.call('inspectRecovery',f.intent)).json()).code,'unknown_outcome_pending');assert.equal((await(await f.call('recoverTask',original.args,original.attempt)).json()).code,'unknown_outcome_pending');assert.equal(f.state.posts,0);
});
test('active account lease and changed lease during final observation prevent reservation',async t=>{
 const f=await setup(t),review=await f.inspect(),original=f.execute(review);const until=new Date(Date.now()+60000).toISOString();
 await f.db.prepare('UPDATE integration_projection_state SET lease_token=?,lease_until_utc=?').bind(crypto.randomUUID(),until).run();assert.equal((await f.call('recoverTask',original.args,original.attempt)).status,429);
 await f.db.prepare('UPDATE integration_projection_state SET lease_token=NULL,lease_until_utc=NULL').run();
 f.state.hook=async request=>{if(new URL(request.url).pathname==='/api/v3/tasks/'+taskId)await f.db.prepare('UPDATE integration_projection_state SET lease_token=?,lease_until_utc=?').bind(crypto.randomUUID(),until).run();};
 assert.equal((await f.call('recoverTask',original.args,original.attempt)).status,409);assert.equal(f.state.posts,0);
});
test('grant revocation during the final read refuses without a dispatched attempt',async t=>{
 const f=await setup(t),review=await f.inspect(),original=f.execute(review,{requiredGrantEpoch:f.grantEpoch});assert.equal(typeof f.grantEpoch,'string');
 f.state.hook=async request=>{if(new URL(request.url).pathname==='/api/v3/tasks/'+taskId)await f.db.prepare('UPDATE integration_profiles SET grant_epoch=? WHERE profile_id=?').bind(crypto.randomUUID(),f.who.profileId).run();};
 assert.equal((await(await f.call('recoverTask',original.args,original.attempt)).json()).code,'recovery_authority_changed');assert.equal(f.state.posts,0);assert.equal((await f.db.prepare('SELECT COUNT(*) AS n FROM integration_projection_attempts').first()).n,0);
});
test('quota exhausted by final observation refuses before consuming the score token',async t=>{
 const f=await setup(t),review=await f.inspect(),original=f.execute(review);
 f.state.hook=async request=>{if(new URL(request.url).pathname==='/api/v3/tasks/'+taskId){await f.db.prepare('INSERT INTO integration_read_state(profile_id,connection_id,blocked_until_utc,credential_repair_required) VALUES(?,?,?,0) ON CONFLICT(profile_id,connection_id) DO UPDATE SET blocked_until_utc=excluded.blocked_until_utc').bind(f.who.profileId,f.who.connectionId,new Date(Date.now()+60000).toISOString()).run();}};
 assert.equal((await f.call('recoverTask',original.args,original.attempt)).status,429);assert.equal(f.state.posts,0);assert.equal((await f.db.prepare('SELECT COUNT(*) AS n FROM integration_projection_attempts').first()).n,0);
});
