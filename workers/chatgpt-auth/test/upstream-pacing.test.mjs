import test from 'node:test';
import assert from 'node:assert/strict';
import {fixture,enrolled,accounts,authorization,exchange} from './fixtures/oauth.mjs';
import {admitUpstream,observePacing,pacingSummary,purgePacing} from '../src/upstream-pacing.ts';
import {TrafficLimit} from '../src/traffic.ts';
const start=Date.parse('2026-10-17T00:00:00Z');
const environment=f=>({HABITICA_INTEGRATION_DB:f.db,ADAPTIVE_PACING_ENABLED:true});
const bucket=(f,key)=>f.db.prepare('SELECT * FROM upstream_pacing WHERE bucket=?').bind(key).first();

test('concurrent independent admissions cannot overspend shared capacity',async t=>{
 const f=await fixture(t);const p=await enrolled(f);const db=f.db;
 // Every caller has independent JS state and the same durable database.
 const results=await Promise.allSettled(Array.from({length:30},()=>admitUpstream({HABITICA_INTEGRATION_DB:db,ADAPTIVE_PACING_ENABLED:true},p.profileId,start)));
 const accepted=results.filter(r=>r.status==='fulfilled').length;
 assert.ok(accepted>=1&&accepted<=15);
 assert.ok(results.filter(r=>r.status==='rejected').every(r=>r.reason instanceof TrafficLimit&&r.reason.retryAfterSeconds>=1));
 assert.equal((await bucket(f,'global')).tokens,300-accepted);
 assert.equal((await bucket(f,'profile:'+p.profileId)).tokens,30-accepted);
});

test('early slowdown preserves reserve and denied profile leaves another profile capacity',async t=>{
 const f=await fixture(t);const a=await enrolled(f),b=await enrolled(f,accounts[1]);const env=environment(f);
 for(let index=0;index<16;index++)await admitUpstream(env,a.profileId,start);
 const before=await bucket(f,'global');
 for(let index=0;index<20;index++)await assert.rejects(admitUpstream(env,a.profileId,start),TrafficLimit);
 assert.equal((await bucket(f,'global')).tokens,before.tokens);
 assert.equal((await bucket(f,'profile:'+a.profileId)).tokens,14);
 await admitUpstream(env,b.profileId,start);
 let now=start;let slowed=false;
 for(let index=0;index<100;index++){
  for(let attempt=0;;attempt++){
   assert.ok(attempt<20,'admission must not spin without a future retry');
   try{await admitUpstream(env,a.profileId,now);break;}
   catch(error){assert.ok(error instanceof TrafficLimit);if((await bucket(f,'profile:'+a.profileId)).tokens>7)slowed=true;now+=error.retryAfterSeconds*1000;}
  }
  assert.ok((await bucket(f,'profile:'+a.profileId)).tokens>=2-1e-6);
 }
 assert.equal(slowed,true);assert.ok(now-start>120000);
});

test('shared remaining headers, 429 and success retain hard cooldown and gradually recover',async t=>{
 const f=await fixture(t);const a=await enrolled(f),b=await enrolled(f,accounts[1]);const env=environment(f);
 await admitUpstream(env,a.profileId,start);
 await observePacing(env,new Response(null,{status:429,headers:{'retry-after':'90','x-ratelimit-limit':'30','x-ratelimit-remaining':'0','x-ratelimit-reset':String((start+120000)/1000)}}),a.profileId,start,0);
 await assert.rejects(admitUpstream(env,b.profileId,start+100000),error=>error instanceof TrafficLimit&&error.retryAfterSeconds>=20);
 const row=await bucket(f,'global');assert.equal(row.capacity,30);assert.equal(row.tokens,0);assert.equal(row.recovery,2);assert.equal(row.cooldown_ms,start+120000);
 await observePacing(env,new Response(null),a.profileId,start+1000,0);
 assert.equal((await bucket(f,'global')).cooldown_ms,start+120000);
 assert.equal((await bucket(f,'global')).recovery,1.875);
 assert.deepEqual(await pacingSummary(env,start+1000),{recoveringBuckets:2,coolingBuckets:2});
 for(let index=0;index<8;index++)await observePacing(env,new Response(null),a.profileId,start+2000,0);
 assert.equal((await bucket(f,'global')).recovery,1);
 await admitUpstream(env,b.profileId,start+120000);
});

test('stale successful header observation cannot refill capacity reserved by concurrent callers',async t=>{
 const f=await fixture(t);const a=await enrolled(f);const env=environment(f);
 for(let index=0;index<10;index++)await admitUpstream(env,a.profileId,start);
 await observePacing(env,new Response(null,{headers:{'x-ratelimit-remaining':'30'}}),a.profileId,start,0);
 assert.equal((await bucket(f,'profile:'+a.profileId)).tokens,20);
 await observePacing(env,null,a.profileId,start,1);
 assert.equal((await bucket(f,'profile:'+a.profileId)).recovery,2);
 await assert.rejects(admitUpstream(env,a.profileId,start),TrafficLimit);
});

test('successful exhaustion is a hard pause without a failure recovery penalty',async t=>{
 const f=await fixture(t);const a=await enrolled(f);const env=environment(f);
 await admitUpstream(env,a.profileId,start);
 await observePacing(env,new Response(null,{headers:{'x-ratelimit-remaining':'0','x-ratelimit-reset':String((start+60000)/1000)}}),a.profileId,start,0);
 assert.deepEqual(await pacingSummary(env,start),{recoveringBuckets:0,coolingBuckets:2});
 await assert.rejects(admitUpstream(env,a.profileId,start),error=>error instanceof TrafficLimit&&error.retryAfterSeconds===60);
});

test('CAS loss and a failed second update do not consume one side of the reservation',async t=>{
 const f=await fixture(t);const a=await enrolled(f);const env=environment(f);
 await admitUpstream(env,a.profileId,start);const g=await bucket(f,'global'),p=await bucket(f,'profile:'+a.profileId);
 // Inject a storage failure in the second statement, after the global update.
 await f.db.exec("CREATE TRIGGER fail_profile_reservation BEFORE UPDATE ON upstream_pacing WHEN NEW.bucket LIKE 'profile:%' BEGIN SELECT RAISE(ABORT,'synthetic reservation failure'); END");
 await assert.rejects(admitUpstream(env,a.profileId,start),/synthetic reservation failure/);
 assert.equal((await bucket(f,'global')).tokens,g.tokens);assert.equal((await bucket(f,'global')).version,g.version);
 assert.equal((await bucket(f,'profile:'+a.profileId)).tokens,p.tokens);
 await f.db.exec('DROP TRIGGER fail_profile_reservation');
});

test('bounded cleanup retains cooldown and profile deletion cascades private pacing state',async t=>{
 const f=await fixture(t);const a=await enrolled(f);const env=environment(f);
 await admitUpstream(env,a.profileId,start);
 await f.db.prepare('UPDATE upstream_pacing SET expires_ms=?,cooldown_ms=? WHERE bucket=?').bind(start-1,start+60000,'profile:'+a.profileId).run();
 await purgePacing(env,start);assert.ok(await bucket(f,'profile:'+a.profileId));
 await f.db.prepare('DELETE FROM integration_profiles WHERE profile_id=?').bind(a.profileId).run();
 assert.equal(await bucket(f,'profile:'+a.profileId),null);
 await f.db.prepare('UPDATE upstream_pacing SET expires_ms=?,cooldown_ms=0').bind(start-1).run();
 await purgePacing(env,start);assert.equal(await bucket(f,'global'),null);
});

test('enabled runtime gate stops upstream reads before dispatch and emits only anonymous metrics',async t=>{
 let reads=0;const f=await fixture(t,{adaptivePacing:true,habitica:request=>{
  if(request.url.includes('userFields='))return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:'Fixture A'}}});
  reads++;return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:'Fixture A'},stats:{}}});
 }});const p=await enrolled(f);const token=await(await exchange(f,await authorization(f,p,'profile:read account:read'))).json();
 // A real private read request must consult durable state before sending HTTP.
 await f.db.prepare('INSERT INTO upstream_pacing(bucket,profile_id,capacity,tokens,updated_ms,next_ms,expires_ms) VALUES(?,?,30,15,?,?,?)').bind('profile:'+p.profileId,p.profileId,Date.now(),Date.now()+60000,Date.now()+86400000).run();
 const response=await f.internal.fetch('https://auth.internal/read',{method:'POST',headers:{authorization:'Bearer '+token.access_token},body:JSON.stringify({tool:'get_account_status',arguments:{}})});
 assert.equal(response.status,429);assert.equal(reads,0);
 await f.db.prepare('UPDATE integration_profiles SET revoked_at_utc=? WHERE profile_id=?').bind(new Date().toISOString(),p.profileId).run();
 const revoked=await f.internal.fetch('https://auth.internal/read',{method:'POST',headers:{authorization:'Bearer '+token.access_token},body:JSON.stringify({tool:'get_account_status',arguments:{}})});
 assert.equal(revoked.status,401);assert.equal(reads,0);
 assert.ok(!f.messages.join('\n').includes(p.profileId));assert.ok(!f.messages.join('\n').includes(accounts[0].apiToken));
});
