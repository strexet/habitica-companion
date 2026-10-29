import {test} from 'node:test';
import assert from 'node:assert/strict';
import {fixture,enrolled,issuer,accounts} from './fixtures/oauth.mjs';
const encode=value=>Buffer.from(value).toString('base64url');
const key=byte=>encode(new Uint8Array(32).fill(byte));
const signing=await crypto.subtle.generateKey({name:'ECDSA',namedCurve:'P-256'},true,['sign','verify']);
const vapid=encode(await crypto.subtle.exportKey('raw',signing.publicKey));
const privateJwk=JSON.stringify(await crypto.subtle.exportKey('jwk',signing.privateKey));
const receiver=await crypto.subtle.generateKey({name:'ECDH',namedCurve:'P-256'},true,['deriveBits']);
const material={endpoint:'https://web.push.apple.com/fixture-private-endpoint',keys:{p256dh:encode(await crypto.subtle.exportKey('raw',receiver.publicKey)),auth:encode(new Uint8Array(16).fill(9))},expirationTime:null};
const bindings={PUSH_ENABLED:true,PUSH_VAULT_KEY_V1:key(4),PUSH_VAULT_CURRENT_KEY_VERSION:'1',PUSH_VAPID_PUBLIC_KEY:vapid,PUSH_VAPID_PRIVATE_JWK:privateJwk};
async function setup(t,overrides={},workspace=false){const f=await fixture(t,{workspace,pushBindings:{...bindings,...overrides}}),p=await enrolled(f);const row=await f.db.prepare('SELECT connection_id FROM integration_profiles WHERE profile_id=?').bind(p.profileId).first();return {f,p,connectionId:row.connection_id};}
async function call(x,path='',body,token=x.p.managementToken){return x.f.auth.fetch(`${issuer}/profiles/${x.p.profileId}/push${path}`,{method:body===undefined?'GET':'POST',headers:{authorization:`Bearer ${token}`,'content-type':'application/json'},...(body===undefined?{}:{body:JSON.stringify(body)})});}
async function grant(x,enabled=true,expectedConsentEpoch=null){const body={requestId:crypto.randomUUID(),connectionId:x.connectionId,expectedConsentEpoch,consentVersion:'web-push-v1',enabled};const response=await call(x,'/consent',body);assert.equal(response.status,200,await response.clone().text());return response.json();}
function intent(x,consentEpoch,extra={}){return {requestId:crypto.randomUUID(),connectionId:x.connectionId,deviceId:crypto.randomUUID(),expectedRevision:0,consentEpoch,subscription:material,userVisibleOnly:true,...extra};}
async function subscribed(x,extra={}){const c=await grant(x),body=intent(x,c.consentEpoch,extra),response=await call(x,'/subscribe',body);assert.equal(response.status,200,await response.clone().text());return {body,result:await response.json(),c};}
async function row(x){return x.f.db.prepare('SELECT * FROM integration_push_devices WHERE profile_id=?').bind(x.p.profileId).first();}
async function clearValue(x,r,profile=x.p.profileId,device=r.device_id,revision=r.revision){const k=await crypto.subtle.importKey('raw',Buffer.from(key(4),'base64url'),'AES-GCM',false,['decrypt']);return JSON.parse(new TextDecoder().decode(await crypto.subtle.decrypt({name:'AES-GCM',iv:Buffer.from(r.iv,'base64url'),additionalData:new TextEncoder().encode(`companion-push-subscription:1:${profile}:${device}:${revision}:${r.key_version}`)},k,Buffer.from(r.ciphertext,'base64url'))));}

test('subscription requires separate consent; encrypted material is context-bound and absent from status, receipt and logs',async t=>{
 const x=await setup(t),initial=await (await call(x)).json();assert.equal(initial.enabled,false);assert.equal(initial.available,true);assert.deepEqual(initial.devices,[]);
 assert.equal((await call(x,'/subscribe',intent(x,crypto.randomUUID()))).status,403);
 const s=await subscribed(x),r=await row(x);assert.deepEqual(await clearValue(x,r),material);
 await assert.rejects(clearValue(x,r,'prf_'+'x'.repeat(43)));await assert.rejects(clearValue(x,r,x.p.profileId,crypto.randomUUID()));await assert.rejects(clearValue(x,r,x.p.profileId,r.device_id,r.revision+1));
 const status=await (await call(x)).json(),recovered=await (await call(x,'/commands/'+s.body.requestId)).json();
 assert.equal(status.devices[0].subscriptionId,s.result.subscriptionId);assert.deepEqual(recovered.receipt,s.result);
 for(const sensitive of [material.endpoint,material.keys.auth,material.keys.p256dh,privateJwk,key(4)]){assert.ok(!JSON.stringify([r,status,recovered,x.f.messages]).includes(sensitive));}
 assert.equal(status.applicationServerKey,vapid);assert.equal(status.devices[0].state,'active');
});

test('original command recovery, concurrent duplicate and stale revision do not replace current device',async t=>{
 const x=await setup(t),s=await subscribed(x);const replies=await Promise.all([call(x,'/subscribe',s.body),call(x,'/subscribe',s.body)]);for(const response of replies)assert.deepEqual(await response.json(),s.result);
 assert.equal((await row(x)).revision,1);
 assert.equal((await call(x,'/subscribe',{...s.body,subscription:{...material,endpoint:'https://fcm.googleapis.com/other'}})).status,409);
 const replace={...s.body,requestId:crypto.randomUUID(),expectedRevision:1,subscription:{...material,endpoint:'https://fcm.googleapis.com/replacement'}};
 const results=await Promise.all([call(x,'/subscribe',replace),call(x,'/subscribe',{...replace,requestId:crypto.randomUUID()})]);assert.deepEqual(results.map(r=>r.status).sort(),[200,409]);
 assert.equal((await row(x)).revision,2);assert.deepEqual(await (await call(x,'/subscribe',s.body)).json(),s.result);assert.equal((await row(x)).revision,2);
});

test('endpoint identity cannot cross accounts or devices; tokens and connection ids cannot cross profiles',async t=>{
 const x=await setup(t),s=await subscribed(x),b=await enrolled(x.f,accounts[1]);const br=await x.f.db.prepare('SELECT connection_id FROM integration_profiles WHERE profile_id=?').bind(b.profileId).first();const y={f:x.f,p:b,connectionId:br.connection_id},bc=await grant(y);
 assert.equal((await call(y,'',undefined,x.p.managementToken)).status,401);
 assert.equal((await call(y,'/subscribe',intent(y,bc.consentEpoch))).status,409);
 assert.equal((await call(x,'/subscribe',intent(x,s.c.consentEpoch))).status,409);
 assert.equal((await call(x,'/subscribe',intent(x,s.c.consentEpoch,{connectionId:y.connectionId}))).status,409);
 const foreign=await (await call(y,'/commands/'+s.body.requestId)).json();assert.equal(foreign.receipt,null);
});

test('unsubscribe is exact-device CAS, removes secrets, and preserves tasks and original receipts',async t=>{
 const x=await setup(t),s=await subscribed(x);const before=await x.f.db.prepare('SELECT credential_ciphertext FROM integration_profiles').first();
 const body={requestId:crypto.randomUUID(),connectionId:x.connectionId,deviceId:s.body.deviceId,expectedRevision:1,consentEpoch:s.c.consentEpoch,subscriptionId:s.result.subscriptionId};
 assert.equal((await call(x,'/unsubscribe',{...body,subscriptionId:crypto.randomUUID()})).status,409);
 const response=await call(x,'/unsubscribe',body);assert.equal(response.status,200);assert.deepEqual(await (await call(x,'/unsubscribe',body)).json(),await response.json());
 const r=await row(x);for(const field of ['ciphertext','iv','key_version','endpoint_hash','vapid_key_hash'])assert.equal(r[field],null);assert.equal(r.state,'unsubscribed');assert.equal(r.revision,2);
 assert.deepEqual(await x.f.db.prepare('SELECT credential_ciphertext FROM integration_profiles').first(),before);
 assert.equal((await call(x,'/subscribe',{...s.body,requestId:crypto.randomUUID()})).status,409);
 assert.equal((await (await call(x,'/commands/'+s.body.requestId)).json()).receipt.saved,true);
});

test('consent revocation atomically clears all devices and invalidates old epochs, including after regrant',async t=>{
 const x=await setup(t),s=await subscribed(x),revoked=await grant(x,false,s.c.consentEpoch);assert.notEqual(revoked.consentEpoch,s.c.consentEpoch);assert.equal((await row(x)).ciphertext,null);
 assert.equal((await call(x,'/subscribe',{...s.body,requestId:crypto.randomUUID(),expectedRevision:2})).status,403);
 const c=await grant(x,true,revoked.consentEpoch);assert.notEqual(c.consentEpoch,revoked.consentEpoch);
 assert.equal((await call(x,'/subscribe',{...s.body,requestId:crypto.randomUUID(),expectedRevision:2})).status,403);
 const response=await call(x,'/subscribe',{...s.body,requestId:crypto.randomUUID(),expectedRevision:2,consentEpoch:c.consentEpoch});assert.equal(response.status,200);assert.equal((await row(x)).revision,3);
});

test('expiry erases material and retains revision tombstone; old receipt never resurrects a subscription',async t=>{
 const x=await setup(t),s=await subscribed(x,{subscription:{...material,expirationTime:Date.now()+60000}});await x.f.db.prepare('UPDATE integration_push_devices SET expires_at_ms=?').bind(Date.now()-1).run();
 const status=await (await call(x)).json();assert.equal(status.devices[0].state,'expired');assert.equal((await row(x)).ciphertext,null);assert.equal((await row(x)).revision,2);
 assert.deepEqual(await (await call(x,'/subscribe',s.body)).json(),s.result);assert.equal((await row(x)).state,'expired');
 await x.f.db.prepare('UPDATE integration_push_commands SET expires_at_utc=?').bind('2000-01-01T00:00:00.000Z').run();
 assert.equal((await (await call(x,'/commands/'+s.body.requestId)).json()).receipt,null);
 assert.equal((await call(x,'/subscribe',s.body)).status,409);
});

test('failed receipt storage rolls back subscription and consent mutations',async t=>{
 const x=await setup(t),c=await grant(x);await x.f.db.exec("CREATE TRIGGER fixture_push_failure BEFORE INSERT ON integration_push_commands BEGIN SELECT RAISE(ABORT,'synthetic receipt failure'); END");
 assert.equal((await call(x,'/subscribe',intent(x,c.consentEpoch))).status,503);assert.equal(await row(x),null);
 assert.equal((await call(x,'/consent',{requestId:crypto.randomUUID(),connectionId:x.connectionId,expectedConsentEpoch:c.consentEpoch,consentVersion:'web-push-v1',enabled:false})).status,503);
 assert.equal((await (await call(x)).json()).enabled,true);
});

test('malformed, private, redirected-looking and unknown push endpoints are rejected without outbound fetch',async t=>{
 const x=await setup(t),c=await grant(x);
 for(const endpoint of ['http://web.push.apple.com/path','https://localhost/path','https://127.0.0.1/path','https://[::1]/path','https://169.254.169.254/path','https://web.push.apple.com.evil.invalid/path','https://web.push.apple.com@evil.invalid/path','https://user:pass@web.push.apple.com/path','https://web.push.apple.com:444/path','https://web.push.apple.com/path?next=https://localhost','https://web.push.apple.com/path#x','https://example.com/path','https://web.push.apple.com/']){
  const response=await call(x,'/subscribe',intent(x,c.consentEpoch,{subscription:{...material,endpoint}}));assert.equal(response.status,422,endpoint);
 }
 for(const subscription of [{...material,keys:{...material.keys,auth:'bad'}},{...material,keys:{...material.keys,p256dh:encode(new Uint8Array(65))}},{...material,expirationTime:Date.now()-1},{...material,expirationTime:1.5}])assert.equal((await call(x,'/subscribe',intent(x,c.consentEpoch,{subscription}))).status,422);
 assert.equal(await row(x),null);
});

test('push off or invalid separate key configuration refuses grant yet permits status and revocation',async t=>{
 for(const overrides of [{PUSH_ENABLED:false},{PUSH_VAULT_KEY_V1:key(1)},{PUSH_VAULT_KEY_V2:key(4)},{PUSH_VAPID_PRIVATE_JWK:'invalid'},{PUSH_VAPID_PUBLIC_KEY:material.keys.p256dh}]){
  const x=await setup(t,overrides);assert.equal((await (await call(x)).json()).available,false);
  const response=await call(x,'/consent',{requestId:crypto.randomUUID(),connectionId:x.connectionId,expectedConsentEpoch:null,consentVersion:'web-push-v1',enabled:true});assert.equal(response.status,503);
  assert.equal((await grant(x,false)).enabled,false);
 }
});

test('disconnect, deletion and credential replacement clear only the selected profile secret material',async t=>{
 for(const operation of ['disconnect','delete','replace']){
  const x=await setup(t),s=await subscribed(x);const other=await enrolled(x.f,accounts[1]);
  if(operation==='replace')await enrolled(x.f,{...accounts[0],apiToken:'fixture-new-token'});
  else{const response=await x.f.auth.fetch(`${issuer}/profiles/${x.p.profileId}${operation==='disconnect'?'/disconnect':''}`,{method:operation==='delete'?'DELETE':'POST',headers:{authorization:`Bearer ${x.p.managementToken}`}});assert.equal(response.status,200,await response.clone().text());}
  const r=await row(x);if(operation==='delete'){assert.equal(r,null);assert.equal(await x.f.db.prepare('SELECT * FROM integration_push_commands WHERE profile_id=?').bind(x.p.profileId).first(),null);}else{assert.equal(r.ciphertext,null);assert.equal(r.state,'revoked');}
  assert.ok(await x.f.db.prepare('SELECT * FROM integration_profiles WHERE profile_id=? AND revoked_at_utc IS NULL').bind(other.profileId).first());
  assert.equal((await call(x,'/subscribe',s.body)).status,401);
 }
});

test('cleanup failure blocks profile first, remains explicit and retries secret erasure',async t=>{
 const x=await setup(t);await subscribed(x);await x.f.db.exec("CREATE TRIGGER fixture_push_cleanup BEFORE UPDATE ON integration_push_devices BEGIN SELECT RAISE(ABORT,'synthetic cleanup failure'); END");
 const cleanup=()=>x.f.auth.fetch(`${issuer}/profiles/${x.p.profileId}/disconnect`,{method:'POST',headers:{authorization:`Bearer ${x.p.managementToken}`}});
 const response=await cleanup();assert.equal(response.status,202);assert.equal((await response.json()).pendingCleanup,true);assert.equal((await call(x)).status,401);
 await x.f.db.exec('DROP TRIGGER fixture_push_cleanup');assert.equal((await cleanup()).status,200);assert.equal((await row(x)).ciphertext,null);
});

test('private binding offers current opaque status only, with exact connection fence',async t=>{
 const x=await setup(t);await subscribed(x);const service=(await x.f.mf.getBindings('mcp')).AUTH_PUSH;
 const response=await service.fetch('https://push.internal/status',{method:'POST',body:JSON.stringify({profileId:x.p.profileId,connectionId:x.connectionId})});assert.equal(response.status,200);const status=await response.json();assert.equal(status.devices.length,1);assert.ok(!JSON.stringify(status).includes(material.endpoint));
 assert.equal((await service.fetch('https://push.internal/status',{method:'POST',body:JSON.stringify({profileId:x.p.profileId,connectionId:crypto.randomUUID()})})).status,503);
 assert.equal((await service.fetch('https://push.internal/decrypt',{method:'POST',body:'{}'})).status,503);
 assert.notEqual((await x.f.auth.fetch(issuer+'/status',{method:'POST',body:'{}'})).status,200);
});

test('Workspace export/disconnect/delete exclude subscription secrets and preserve independent account consent',async t=>{
 const x=await setup(t,{},true);await subscribed(x);
 const workspace=async(path,token,body,method=body===undefined?'GET':'POST')=>x.f.workspace.fetch('http://localhost:8789'+path,{method,headers:{authorization:`Bearer ${token}`,'content-type':'application/json'},...(body===undefined?{}:{body:JSON.stringify(body)})});
 const connection=await workspace('/connect',x.p.managementToken,{consentVersion:'workspace-v1'});assert.equal(connection.status,201);const w=await connection.json();
 const exported=await workspace('/export',w.sessionToken);assert.equal(exported.status,200);const text=await exported.text();for(const secret of [material.endpoint,material.keys.auth,material.keys.p256dh,privateJwk,key(4)])assert.ok(!text.includes(secret));
 assert.equal((await workspace('/disconnect',w.sessionToken,{})).status,200);assert.equal((await (await call(x)).json()).enabled,true);assert.equal((await row(x)).state,'active');
 const resumed=await (await workspace('/connect',x.p.managementToken,{consentVersion:'workspace-v1'})).json();assert.equal((await workspace('/workspace',resumed.sessionToken,{confirmWorkspaceId:resumed.workspaceId},'DELETE')).status,200);assert.equal((await row(x)).state,'active');
});

test('bounded device and receipt capacity fail closed without partial writes',async t=>{
 const x=await setup(t),s=await subscribed(x);
 for(let i=1;i<64;i++)await x.f.db.prepare(`INSERT INTO integration_push_devices SELECT profile_id,?,connection_id,revision,subscription_id,'unsubscribed',consent_epoch,NULL,NULL,NULL,NULL,NULL,NULL,commit_tag,updated_at_utc FROM integration_push_devices WHERE profile_id=? AND device_id=?`).bind(crypto.randomUUID(),x.p.profileId,s.body.deviceId).run();
 assert.equal((await call(x,'/subscribe',intent(x,s.c.consentEpoch,{subscription:{...material,endpoint:'https://fcm.googleapis.com/new-device'}}))).status,413);
 for(let i=1;i<1000;i++)await x.f.db.prepare(`INSERT INTO integration_push_commands SELECT profile_id,?,connection_id,payload_hash,result_json,created_at_utc,expires_at_utc FROM integration_push_commands WHERE profile_id=? AND request_id=?`).bind(crypto.randomUUID(),x.p.profileId,s.body.requestId).run();
 assert.equal((await call(x,'/subscribe',{...s.body,requestId:crypto.randomUUID(),expectedRevision:1})).status,413);assert.equal((await row(x)).revision,1);
 assert.deepEqual(await (await call(x,'/subscribe',s.body)).json(),s.result);
});
