import {test} from 'node:test';
import assert from 'node:assert/strict';
import {fixture,enrolled,ticket,authorization,exchange,issuer,resource,accounts,authPage,form,handle} from './fixtures/oauth.mjs';
const manage=(f,p,op,body,token=p.managementToken)=>f.auth.fetch(`${issuer}/profiles/${p.profileId}/${op}`,{method:body===undefined?'GET':'POST',headers:{authorization:'Bearer '+token,'content-type':'application/json'},...(body===undefined?{}:{body:JSON.stringify(body)})});
const validate=async(f,token)=>(await f.internal.fetch('https://auth.internal/validate',{method:'POST',body:JSON.stringify({resource,token})})).json();

test('management status projects current permissions, verified use and credential repair without vault details',async t=>{
 const f=await fixture(t),p=await enrolled(f),other=await enrolled(f,accounts[1]);
 const tokens=await(await exchange(f,await authorization(f,p,'profile:read account:read'))).json();assert.ok(await validate(f,tokens.access_token));
 const response=await manage(f,p,'status');assert.equal(response.status,200);const status=await response.json();assert.equal(status.displayName,'Fixture A');assert.deepEqual(status.permissions,['account:read','profile:read']);assert.equal(status.grantCount,1);assert.ok(Number.isFinite(Date.parse(status.lastUsedAtUtc)));assert.equal(status.credentialStatus,'last_verified');
 const row=await f.db.prepare('SELECT connection_id FROM integration_profiles WHERE profile_id=?').bind(p.profileId).first();await f.db.prepare('INSERT INTO integration_read_state(profile_id,connection_id,credential_repair_required) VALUES(?,?,1)').bind(p.profileId,row.connection_id).run();assert.equal((await(await manage(f,p,'status')).json()).credentialStatus,'repair_needed');
 assert.equal((await manage(f,p,'status',undefined,other.managementToken)).status,401);
 const text=JSON.stringify(status);for(const secret of ['fixture-a-token','credential_ciphertext','managementToken','grant_epoch',tokens.access_token])assert.ok(!text.includes(secret));
 const cors=await f.auth.fetch(`${issuer}/profiles/${p.profileId}/status`,{headers:{authorization:'Bearer '+p.managementToken,origin:'http://localhost:5081'}});assert.equal(cors.headers.get('access-control-allow-origin'),'http://localhost:5081');
});

test('grant revocation rejects access and refresh immediately while preserving vault identity and management access',async t=>{
 const f=await fixture(t),p=await enrolled(f),q=await enrolled(f,accounts[1]);
 const tokens=await(await exchange(f,await authorization(f,p,'profile:read account:read'))).json(),other=await(await exchange(f,await authorization(f,q))).json();await ticket(f,p);
 const before=await f.db.prepare('SELECT * FROM integration_profiles WHERE profile_id=?').bind(p.profileId).first();const r=await manage(f,p,'revoke-grants',{confirm:'revoke-chatgpt-grants'});assert.equal(r.status,200);assert.equal((await r.json()).credentialsRetained,true);
 assert.equal(await validate(f,tokens.access_token),null);assert.ok(await validate(f,other.access_token));
 const refresh=await f.auth.fetch(issuer+'/oauth/token',{method:'POST',headers:{'content-type':'application/x-www-form-urlencoded'},body:new URLSearchParams({grant_type:'refresh_token',refresh_token:tokens.refresh_token,client_id:f.clientId,resource})});assert.notEqual(refresh.status,200);
 const after=await f.db.prepare('SELECT * FROM integration_profiles WHERE profile_id=?').bind(p.profileId).first();assert.equal(after.connection_id,before.connection_id);assert.equal(after.credential_ciphertext,before.credential_ciphertext);assert.equal(after.management_token_hash,before.management_token_hash);assert.notEqual(after.grant_epoch,before.grant_epoch);
 assert.equal((await f.db.prepare('SELECT COUNT(*) AS n FROM linking_tickets WHERE profile_id=?').bind(p.profileId).first()).n,0);
 assert.deepEqual((await(await manage(f,p,'status')).json()).permissions,[]);
 const fresh=await(await exchange(f,await authorization(f,p))).json();assert.ok(await validate(f,fresh.access_token));assert.equal((await manage(f,p,'revoke-grants',{})).status,409);
});

test('management rotation invalidates the old bearer without revoking separate ChatGPT grants',async t=>{
 const f=await fixture(t),p=await enrolled(f);const tokens=await(await exchange(f,await authorization(f,p))).json();
 const rotated=await manage(f,p,'rotate-management',{confirm:'rotate-management-token'});assert.equal(rotated.status,200);const value=await rotated.json();assert.match(value.managementToken,/^[A-Za-z0-9_-]{43}$/);assert.notEqual(value.managementToken,p.managementToken);
 assert.equal((await manage(f,p,'status')).status,401);assert.equal((await manage(f,p,'status',undefined,value.managementToken)).status,200);assert.ok(await validate(f,tokens.access_token));assert.equal((await manage(f,p,'rotate-management',{confirm:'rotate-management-token'})).status,401);
});

test('the durable grant epoch alone rejects a provider token even before provider cleanup',async t=>{
 const f=await fixture(t),p=await enrolled(f),tokens=await(await exchange(f,await authorization(f,p))).json();assert.ok(await validate(f,tokens.access_token));
 const kv=await f.mf.getKVNamespace('OAUTH_KV','auth'),before=await kv.list();
 await f.db.prepare("UPDATE integration_profiles SET grant_epoch='2',generation=generation+1 WHERE profile_id=?").bind(p.profileId).run();
 assert.deepEqual(await kv.list(),before);assert.equal(await validate(f,tokens.access_token),null);
 const response=await f.auth.fetch(issuer+'/oauth/token',{method:'POST',headers:{'content-type':'application/x-www-form-urlencoded'},body:new URLSearchParams({grant_type:'refresh_token',refresh_token:tokens.refresh_token,client_id:f.clientId,resource})});assert.notEqual(response.status,200);
});


test('revocation invalidates pending consent and a late old linking ticket cannot acquire a new epoch',async t=>{
 const f=await fixture(t),p=await enrolled(f),code=await ticket(f,p),saved=await f.db.prepare('SELECT * FROM linking_tickets WHERE profile_id=?').bind(p.profileId).first(),page=await authPage(f);
 const identified=await form(f,page,{handle:handle(await page.response.text()),decision:'identify',code:code.code,scope:'profile:read'});assert.equal(identified.status,200);const pending=handle(await identified.text());
 assert.equal((await manage(f,p,'revoke-grants',{confirm:'revoke-chatgpt-grants'})).status,200);
 assert.equal((await form(f,page,{handle:pending,decision:'approve',scope:'profile:read'})).status,400);
 const keys=Object.keys(saved);await f.db.prepare(`INSERT INTO linking_tickets(${keys.join(',')}) VALUES(${keys.map(()=>'?').join(',')})`).bind(...keys.map(k=>saved[k])).run();
 const again=await authPage(f);const refused=await form(f,again,{handle:handle(await again.response.text()),decision:'identify',code:code.code,scope:'profile:read'});assert.equal(refused.status,400);
});

test('concurrent management rotations leave exactly one current management bearer',async t=>{
 const f=await fixture(t),p=await enrolled(f);const responses=await Promise.all([manage(f,p,'rotate-management',{confirm:'rotate-management-token'}),manage(f,p,'rotate-management',{confirm:'rotate-management-token'})]);
 assert.equal(responses.filter(r=>r.status===200).length,1);assert.ok(responses.some(r=>[401,409].includes(r.status)));
 const token=(await responses.find(r=>r.status===200).json()).managementToken;assert.equal((await manage(f,p,'status',undefined,token)).status,200);assert.equal((await manage(f,p,'status')).status,401);
});
