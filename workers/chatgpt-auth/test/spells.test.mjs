import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {fixture,enrolled,authorization,exchange,accounts,issuer} from './fixtures/oauth.mjs';
import {spellEligibility,supportedSpell} from '../../shared/spells.ts';
const partyId='90000000-0000-4000-8000-000000000001';
const scopes='profile:read account:read party:read habitica.spells.execute';
const gate=()=>{let release;return {promise:new Promise(r=>release=r),release:()=>release()};};
async function setup(t){
 const state={stats:{class:'healer',lvl:20,hp:35,mp:100,str:1,int:20,con:30,per:1,buffs:{}},partyId,postStatus:200,posts:0,groupHook:null,groupHeaders:{},malformed:false};
 const user=actor=>({_id:actor,profile:{name:actor===accounts[0].habiticaUserId?'Fixture A':'Fixture B'},stats:structuredClone(state.stats),party:{_id:state.partyId},flags:{needsCron:false},preferences:{disableClasses:false,sleep:false},items:{gear:{equipped:{weapon:'healer-fixture'}}},lastCron:'2026-10-15T00:00:00Z'});
 const f=await fixture(t,{workspace:true,party:true,spells:true,projection:true,habitica:async request=>{
  assert.ok(request.headers.get('x-client')); const path=new URL(request.url).pathname;const actor=request.headers.get('x-api-user');
  if(request.method==='GET'&&path==='/api/v3/user')return Response.json({success:true,data:user(actor)});
  if(request.method==='GET'&&path==='/api/v3/groups/party'){
   if(state.groupHook)await state.groupHook();
   return Response.json({success:true,data:{_id:state.partyId,type:'party',name:'Synthetic party',leader:accounts[0].habiticaUserId,memberCount:2,quest:{key:'whale',active:false}}},{headers:state.groupHeaders});
  }
  if(request.method==='POST'){
   assert.equal(path,'/api/v3/user/class/cast/healAll');assert.deepEqual(await request.json(),{quantity:1});state.posts++;
   if(state.postStatus!==200)return new Response('private upstream message fixture-a-token',{status:state.postStatus,headers:{'retry-after':'60'}});
   state.stats.mp-=25;state.stats.hp=Math.min(50,state.stats.hp+3);
   return state.malformed?Response.json({success:true,data:{user:{_id:actor}}}):Response.json({success:true,data:{user:user(actor),partyMembers:[{secret:'must-not-escape'}]}});
  }
  return new Response('',{status:404});
 }});
 const profile=await enrolled(f);const row=await f.db.prepare('SELECT connection_id FROM integration_profiles WHERE profile_id=?').bind(profile.profileId).first();
 const token=await(await exchange(f,await authorization(f,profile,scopes))).json();
 const service=(await f.mf.getBindings('mcp')).AUTH_SPELL;
 const who={profileId:profile.profileId,connectionId:row.connection_id};
 const call=(operation,args={},extra={},bearer=token.access_token)=>service.fetch('https://spell.internal/operation',{method:'POST',headers:{authorization:'Bearer '+bearer,'content-type':'application/json'},body:JSON.stringify({...who,operation,arguments:args,...extra})});
 const context=async()=>{const response=await call('context');assert.equal(response.status,200,await response.clone().text());return response.json();};
 const intent=async(count=1)=>({args:{spellId:'healAll',partyId,expectedPreconditionHash:(await context()).preconditionHash,remainingCount:count},attempt:{attemptId:crypto.randomUUID(),attemptAtUtc:new Date().toISOString()}});
 return {...f,profile,who,call,context,intent,state,token};
}

test('neutral spell fixture agrees with C# metadata while marking stricter gateway limits',async()=>{
 const data=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/spell-safety-v1.json',import.meta.url),'utf8'));
 assert.equal(supportedSpell.id,data.spell.id);assert.equal(supportedSpell.manaCost,data.spell.manaCost);assert.equal(supportedSpell.unlockLevel,data.spell.unlockLevel);assert.equal(supportedSpell.target,data.spell.target.toLowerCase());
 for(const sample of data.cases)assert.equal(spellEligibility(sample,sample.count)===null,sample.workerEligible,sample.name);
});

test('private casts bind fresh context, return minimal receipts and never repeat an attempt',async t=>{
 const f=await setup(t);const intent=await f.intent(2);
 const first=await(await f.call('cast',intent.args,intent.attempt)).json();assert.equal(first.kind,'applied');assert.equal(first.mana,75);assert.equal(first.health,38);assert.match(first.nextPreconditionHash,/^[a-f0-9]{64}$/);
 assert.deepEqual(await(await f.call('cast',intent.args,intent.attempt)).json(),first);
 assert.deepEqual(await(await f.call('lookup',intent.args,intent.attempt)).json(),first);assert.equal(f.state.posts,1);
 assert.equal((await f.call('cast',{...intent.args,remainingCount:1},intent.attempt)).status,409);
 assert.equal((await f.context()).preconditionHash,first.nextPreconditionHash);
 const second={...intent.args,expectedPreconditionHash:first.nextPreconditionHash,remainingCount:1};
 assert.equal((await(await f.call('cast',second,{...intent.attempt,attemptId:crypto.randomUUID()})).json()).kind,'applied');assert.equal(f.state.posts,2);
 const stored=JSON.stringify(await f.db.prepare('SELECT * FROM integration_projection_attempts').all());
 for(const secret of [accounts[0].apiToken,'must-not-escape','healer-fixture'])assert.ok(!stored.includes(secret));
 assert.notEqual((await f.auth.fetch(issuer+'/spells',{method:'POST'})).status,200);
});

test('changed preconditions, invalid allowlist, level, class and total mana prevent effects',async t=>{
 const f=await setup(t);const intent=await f.intent(3);
 for(const [key,value,code] of [['class','wizard','wrong_class'],['lvl',13,'spell_locked'],['mp',74,'insufficient_mana'],['hp',0,'not_alive'],['int',21,'spell_precondition_changed']]){
  const old=f.state.stats[key];f.state.stats[key]=value;
  const result=await f.call('cast',intent.args,{...intent.attempt,attemptId:crypto.randomUUID()});assert.equal((await result.json()).code,code);f.state.stats[key]=old;
 }
 assert.equal((await f.call('cast',{...intent.args,spellId:'heal'},intent.attempt)).status,422);
 assert.equal((await f.call('cast',{...intent.args,targetId:accounts[1].habiticaUserId},intent.attempt)).status,400);
 assert.equal((await f.call('cast',intent.args,{...intent.attempt,attemptAtUtc:'2000-01-01T00:00:00Z'})).status,409);
 f.state.partyId='90000000-0000-4000-8000-000000000002';assert.equal((await(await f.call('cast',intent.args,{...intent.attempt,attemptId:crypto.randomUUID()})).json()).code,'spell_precondition_changed');
 assert.equal(f.state.posts,0);
});

test('scope and profile identity are independently enforced without a write',async t=>{
 const f=await setup(t);const intent=await f.intent();
 const other=await enrolled(f,accounts[1]);const otherToken=await(await exchange(f,await authorization(f,other,scopes))).json();
 assert.equal((await f.call('cast',intent.args,intent.attempt,otherToken.access_token)).status,401);
 const readonly=await(await exchange(f,await authorization(f,f.profile,'profile:read account:read party:read'))).json();
 assert.equal((await f.call('cast',intent.args,intent.attempt,readonly.access_token)).status,403);
 assert.equal(f.state.posts,0);
});

test('same in-flight attempt cannot be refused over a later applied receipt or release its lease',async t=>{
 const f=await setup(t);const intent=await f.intent();const entered=gate(),resume=gate();
 f.state.groupHook=async()=>{entered.release();await resume.promise;};
 const pending=f.call('cast',intent.args,intent.attempt);await Promise.race([entered.promise,pending.then(()=>assert.fail('preflight gate not reached'))]);
 const duplicate=await f.call('cast',intent.args,intent.attempt);assert.equal((await duplicate.json()).kind,'unknown');
 const competitor=await f.call('cast',intent.args,{...intent.attempt,attemptId:crypto.randomUUID()});assert.equal((await competitor.json()).code,'gameplay_busy');
 assert.ok((await f.db.prepare('SELECT lease_token FROM integration_projection_state').first()).lease_token);
 resume.release();const applied=await(await pending).json();assert.equal(applied.kind,'applied');
 assert.deepEqual(await(await f.call('cast',intent.args,intent.attempt)).json(),applied);assert.equal(f.state.posts,1);
 assert.equal((await f.db.prepare('SELECT COUNT(*) AS count FROM integration_projection_attempts').first()).count,1);
});

test('uncertain upstream effects never replay and fence new task and spell writes',async t=>{
 const f=await setup(t);const intent=await f.intent();f.state.postStatus=500;
 const unknown=await(await f.call('cast',intent.args,intent.attempt)).json();assert.equal(unknown.kind,'unknown');assert.ok(!JSON.stringify(unknown).includes('fixture-a-token'));
 assert.deepEqual(await(await f.call('cast',intent.args,intent.attempt)).json(),unknown);
 assert.equal((await(await f.call('cast',intent.args,{...intent.attempt,attemptId:crypto.randomUUID()})).json()).code,'unknown_outcome_pending');
 const projection=await f.projection.fetch('https://projection.internal/operation',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({...f.who,operation:'score',arguments:{selector:partyId,checkToken:'a'.repeat(43),expectedFingerprint:'b'.repeat(64),direction:'up'},automated:false,attemptId:crypto.randomUUID(),attemptAtUtc:new Date().toISOString()})});
 assert.equal((await projection.json()).code,'unknown_outcome_pending');assert.equal(f.state.posts,1);
});

test('an unknown task receipt also prevents a new spell effect',async t=>{
 const f=await setup(t);const intent=await f.intent();
 await f.db.prepare("INSERT INTO integration_projection_attempts(profile_id,connection_id,attempt_id,payload_hash,operation,state,created_at_utc) VALUES(?,?,?,?,'score','unknown',?)").bind(f.who.profileId,f.who.connectionId,crypto.randomUUID(),'a'.repeat(64),new Date().toISOString()).run();
 assert.equal((await(await f.call('cast',intent.args,intent.attempt)).json()).code,'unknown_outcome_pending');assert.equal(f.state.posts,0);
});

test('quota from the final read stops dispatch; semantic refusal does not invalidate credentials',async t=>{
 const f=await setup(t);const intent=await f.intent();f.state.groupHeaders={'x-ratelimit-remaining':'0','retry-after':'60'};
 const blocked=await(await f.call('cast',intent.args,intent.attempt)).json();assert.equal(blocked.code,'rate_limited');assert.ok(blocked.retryAtUtc);assert.equal(f.state.posts,0);
 await f.db.prepare('UPDATE integration_read_state SET blocked_until_utc=NULL').run();f.state.groupHeaders={};f.state.postStatus=403;
 const refused=await(await f.call('cast',intent.args,{...intent.attempt,attemptId:crypto.randomUUID()})).json();assert.equal(refused.code,'spell_refused');
 assert.equal((await f.db.prepare('SELECT credential_repair_required FROM integration_read_state').first()).credential_repair_required,0);await f.context();
});

test('malformed success remains unknown instead of guessing partial party effects',async t=>{
 const f=await setup(t);const intent=await f.intent();f.state.malformed=true;
 assert.equal((await(await f.call('cast',intent.args,intent.attempt)).json()).kind,'unknown');
 assert.equal((await(await f.call('lookup',intent.args,intent.attempt)).json()).kind,'unknown');assert.equal(f.state.posts,1);
});

test('revocation during preflight prevents dispatch',async t=>{
 const f=await setup(t);const intent=await f.intent();const entered=gate(),resume=gate();f.state.groupHook=async()=>{entered.release();await resume.promise;};
 const pending=f.call('cast',intent.args,intent.attempt);await Promise.race([entered.promise,pending.then(()=>assert.fail('preflight gate not reached'))]);
 assert.equal((await f.auth.fetch(`${issuer}/profiles/${f.profile.profileId}/disconnect`,{method:'POST',headers:{authorization:'Bearer '+f.profile.managementToken}})).status,200);
 resume.release();const response=await pending;assert.equal(response.status,401);assert.equal(f.state.posts,0);
});

test('grant replacement during preflight prevents a write even with active credentials',async t=>{
 const f=await setup(t);const intent=await f.intent();const entered=gate(),resume=gate();f.state.groupHook=async()=>{entered.release();await resume.promise;};
 const pending=f.call('cast',intent.args,intent.attempt);await Promise.race([entered.promise,pending.then(()=>assert.fail('preflight gate not reached'))]);
 await exchange(f,await authorization(f,f.profile,'profile:read account:read party:read'));
 resume.release();assert.equal((await pending).status,401);assert.equal(f.state.posts,0);
});

test('failed durable dispatch reservation prevents an upstream cast',async t=>{
 const f=await setup(t);const intent=await f.intent();
 await f.db.prepare("CREATE TRIGGER fixture_spell_storage_failure BEFORE INSERT ON integration_projection_attempts BEGIN SELECT RAISE(ABORT,'fixture failure'); END").run();
 // Also refuse recording a known failure: a storage outage must still never reach Habitica.
 const response=await f.call('cast',intent.args,intent.attempt);
 assert.equal(response.status,503);assert.equal(f.state.posts,0);
});

test('local final-dispatch quota preserves an original known refusal receipt without casting',async t=>{
 const f=await setup(t);const intent=await f.intent();
 f.state.groupHook=async()=>{for(const window of [Math.floor(Date.now()/60000),Math.floor(Date.now()/60000)+1])await f.db.prepare('INSERT OR REPLACE INTO authorization_attempts(bucket,attempts,expires_at_utc) VALUES(?,?,?)').bind('traffic:upstream:profile:'+f.profile.profileId+':'+window,60,new Date((window+1)*60000).toISOString()).run();};
 const response=await f.call('cast',intent.args,intent.attempt);assert.equal(response.status,429);assert.ok(response.headers.has('retry-after'));const refused=await response.json();assert.equal(refused.kind,'refused');assert.equal(refused.code,'rate_limited');assert.equal(f.state.posts,0);
 assert.deepEqual(await(await f.call('lookup',intent.args,intent.attempt)).json(),refused);assert.equal(f.state.posts,0);
});
