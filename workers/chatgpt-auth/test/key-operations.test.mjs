import {test} from 'node:test';
import assert from 'node:assert/strict';
import {fixture,enrolled,accounts,issuer} from './fixtures/oauth.mjs';
import {encryptCredential,decryptCredential} from '../src/cryptography.ts';
import {stepKeyJob,keyInventory,assertKeyRemovable,purgeKeyJobs} from '../src/key-operations.ts';
async function setup(t,enabled=true){
 const f=await fixture(t,{workspace:true,keyOperations:enabled,currentKeyVersion:'2'}),profiles=[await enrolled(f),await enrolled(f,accounts[1])],env=await f.mf.getBindings('auth');const keys={'1':env.CREDENTIAL_KEY_V1,'2':env.CREDENTIAL_KEY_V2};
 for(let i=0;i<profiles.length;i++){
  const e=await encryptCredential(profiles[i].profileId,accounts[i],'1',keys);
  await f.db.prepare('UPDATE integration_profiles SET credential_ciphertext=?,credential_iv=?,credential_key_version=? WHERE profile_id=?').bind(e.ciphertext,e.iv,e.keyVersion,profiles[i].profileId).run();
 }
 const service=(await f.mf.getBindings('mcp')).AUTH_KEYS;
 const call=body=>service.fetch('https://keys.internal/operation',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify(body)});
 const start=async(jobId=crypto.randomUUID())=>{const response=await call({operation:'start',jobId,fromVersion:'1'});assert.equal(response.status,200,await response.clone().text());return response.json();};
 const row=id=>f.db.prepare('SELECT * FROM integration_profiles WHERE profile_id=?').bind(id).first();
 const credential=async row=>decryptCredential(row.profile_id,{ciphertext:row.credential_ciphertext,iv:row.credential_iv,keyVersion:row.credential_key_version,schemaVersion:row.credential_schema_version},keys);
 return {f,profiles,env,keys,call,start,row,credential};
}

test('bounded key jobs resume by identity, preserve credentials/profile identity and return only safe counts',async t=>{
 const s=await setup(t);assert.equal((await(await s.call({operation:'inventory'})).json()).references['1'],2);const job=await s.start();assert.equal(job.status,'running');assert.deepEqual(await s.start(job.jobId),job);
 let result=await(await s.call({operation:'step',jobId:job.jobId,limit:1})).json();assert.equal(result.migrated,1);assert.equal(result.scanned,1);
 for(let i=0;i<3&&result.status!=='complete';i++)result=await(await s.call({operation:'step',jobId:job.jobId,limit:1})).json();
 assert.equal(result.status,'complete');assert.equal(result.migrated,2);assert.equal(result.skipped,0);assert.equal(result.scanned,2);assert.deepEqual(await(await s.call({operation:'step',jobId:job.jobId,limit:1})).json(),result);
 for(let i=0;i<2;i++){const row=await s.row(s.profiles[i].profileId);assert.equal(row.credential_key_version,'2');assert.deepEqual(await s.credential(row),accounts[i]);assert.equal(row.grant_epoch,'legacy');}
 const text=JSON.stringify(result);for(const secret of ['fixture-a-token','ciphertext','last_profile_id',s.profiles[0].profileId])assert.ok(!text.includes(secret));
 assert.equal((await s.f.db.prepare('SELECT last_profile_id FROM credential_key_jobs WHERE job_id=?').bind(job.jobId).first()).last_profile_id,null);
});

test('current, referenced and unfinished-job keys cannot be declared removable',async t=>{
 const s=await setup(t);assert.equal((await s.call({operation:'assert-removable',keyVersion:'1'})).status,409);assert.equal((await s.call({operation:'assert-removable',keyVersion:'2'})).status,409);
 const j=await s.start();assert.equal((await s.call({operation:'start',jobId:crypto.randomUUID(),fromVersion:'1'})).status,409);
 const r=await(await s.call({operation:'step',jobId:j.jobId,limit:25})).json();assert.equal(r.status,'complete');assert.equal((await s.call({operation:'assert-removable',keyVersion:'1'})).status,200);
 const pending=await s.start(crypto.randomUUID());assert.equal((await s.call({operation:'assert-removable',keyVersion:'1'})).status,409);await s.call({operation:'step',jobId:pending.jobId});assert.equal((await s.call({operation:'assert-removable',keyVersion:'1'})).status,200);
});

test('durable batch progress and envelope write roll back together after storage failure',async t=>{
 const s=await setup(t),job=await s.start(),before=await s.row(s.profiles[0].profileId);
 await s.f.db.prepare("CREATE TRIGGER fail_key_progress BEFORE UPDATE ON credential_key_jobs WHEN NEW.scanned>OLD.scanned BEGIN SELECT RAISE(ABORT,'synthetic progress storage failure'); END").run();
 assert.equal((await s.call({operation:'step',jobId:job.jobId,limit:1})).status,503);assert.equal((await s.row(s.profiles[0].profileId)).credential_ciphertext,before.credential_ciphertext);assert.equal((await(await s.call({operation:'inventory'})).json()).references['1'],2);
 const stored=await s.f.db.prepare('SELECT scanned,last_profile_id FROM credential_key_jobs WHERE job_id=?').bind(job.jobId).first();assert.equal(stored.scanned,0);assert.equal(stored.last_profile_id,null);
 await s.f.db.prepare('DROP TRIGGER fail_key_progress').run();assert.equal((await(await s.call({operation:'step',jobId:job.jobId,limit:25})).json()).migrated,2);
});

test('unreadable envelopes block safely and resume after repair without plaintext diagnostics',async t=>{
 const s=await setup(t),first=await s.f.db.prepare('SELECT * FROM integration_profiles ORDER BY profile_id LIMIT 1').first();await s.f.db.prepare("UPDATE integration_profiles SET credential_ciphertext='invalid-envelope' WHERE profile_id=?").bind(first.profile_id).run();const job=await s.start();
 const response=await s.call({operation:'step',jobId:job.jobId,limit:25});assert.equal(response.status,200);const result=await response.json();assert.equal(result.status,'blocked');assert.equal(result.failureCode,'credential_unreadable');assert.equal(result.migrated,0);assert.ok(!JSON.stringify(result).includes('invalid-envelope'));
 await s.f.db.prepare('UPDATE integration_profiles SET credential_ciphertext=? WHERE profile_id=?').bind(first.credential_ciphertext,first.profile_id).run();assert.equal((await(await s.call({operation:'step',jobId:job.jobId,limit:25})).json()).status,'complete');
});

test('generation checks preserve a concurrent credential replacement instead of overwriting the fresh envelope',async t=>{
 const s=await setup(t),job=await s.start(),first=await s.f.db.prepare('SELECT * FROM integration_profiles ORDER BY profile_id LIMIT 1').first();const replacement={habiticaUserId:(await s.credential(first)).habiticaUserId,apiToken:'fixture-revalidated-token'},envelope=await encryptCredential(first.profile_id,replacement,'2',s.keys);let intercepted=false;
 const env={...s.env,HABITICA_INTEGRATION_DB:{prepare:(...a)=>s.f.db.prepare(...a),batch:async statements=>{
  if(!intercepted){intercepted=true;await s.f.db.prepare('UPDATE integration_profiles SET credential_ciphertext=?,credential_iv=?,credential_key_version=?,generation=generation+1 WHERE profile_id=?').bind(envelope.ciphertext,envelope.iv,envelope.keyVersion,first.profile_id).run();}
  return s.f.db.batch(statements);
 }}};
 const result=await stepKeyJob(env,job.jobId,25);assert.equal(result.status,'complete');assert.equal(result.skipped,1);assert.equal(result.migrated,1);assert.deepEqual(await s.credential(await s.row(first.profile_id)),replacement);
});

test('an active owner blocks overlap; an expired owner resumes with the original job',async t=>{
 const s=await setup(t),job=await s.start();await s.f.db.prepare('UPDATE credential_key_jobs SET lease_token=?,lease_until_utc=? WHERE job_id=?').bind('synthetic-owner',new Date(Date.now()+60000).toISOString(),job.jobId).run();assert.equal((await s.call({operation:'step',jobId:job.jobId})).status,409);
 await s.f.db.prepare("UPDATE credential_key_jobs SET lease_until_utc='2000-01-01T00:00:00Z' WHERE job_id=?").bind(job.jobId).run();assert.equal((await(await s.call({operation:'step',jobId:job.jobId})).json()).status,'complete');
});

test('missing required keys and changed target configuration block rotation without changing credentials',async t=>{
 const s=await setup(t),job=await s.start();const missing={...s.env,CREDENTIAL_KEY_V1:undefined};assert.deepEqual((await keyInventory(missing)).missingReferencedKeys,['1']);await assert.rejects(()=>stepKeyJob(missing,job.jobId),/required_key_unavailable/);await assert.rejects(()=>assertKeyRemovable(missing,'1'),/key_still_required/);await assert.rejects(()=>stepKeyJob({...s.env,CREDENTIAL_CURRENT_KEY_VERSION:'1'},job.jobId),/current_key_changed/);await assert.rejects(()=>stepKeyJob({...s.env,CREDENTIAL_KEY_V1:s.env.CREDENTIAL_KEY_V2},job.jobId),/invalid_key_configuration/);assert.equal((await keyInventory(s.env)).references['1'],2);await s.f.db.prepare("UPDATE integration_profiles SET credential_key_version='3'").run();await assert.rejects(()=>assertKeyRemovable(s.env,'1'),/key_inventory_incomplete/);
});

test('operator boundary rejects disabled flags, public routing, invalid limits and supplied secret fields',async t=>{
 const s=await setup(t,false);assert.equal((await s.call({operation:'inventory'})).status,503);assert.notEqual((await s.f.auth.fetch(issuer+'/key-operations',{method:'POST'})).status,200);
});

test('operator inputs accept only bounded numeric batches and configured version identifiers',async t=>{
 const s=await setup(t),job=await s.start();for(const limit of [0,26,'1',null])assert.equal((await s.call({operation:'step',jobId:job.jobId,limit})).status,422);assert.equal((await s.call({operation:'start',jobId:crypto.randomUUID(),fromVersion:'1',key:'never-return-key'})).status,400);assert.equal((await s.call({operation:'assert-removable',keyVersion:'3'})).status,422);
});


test('inactive operator progress expires after thirty days without removing any credential envelope',async t=>{
 const s=await setup(t),job=await s.start();await s.f.db.prepare("UPDATE credential_key_jobs SET updated_at_utc='2000-01-01T00:00:00Z' WHERE job_id=?").bind(job.jobId).run();await purgeKeyJobs(s.env);assert.equal((await s.f.db.prepare('SELECT COUNT(*) AS count FROM credential_key_jobs').first()).count,0);assert.equal((await keyInventory(s.env)).references['1'],2);assert.equal((await s.call({operation:'step',jobId:job.jobId})).status,404);
});
