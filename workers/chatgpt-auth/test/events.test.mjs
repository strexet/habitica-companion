import test from 'node:test';
import assert from 'node:assert/strict';
import {setup,eventBindings,secret,key} from './fixtures/events.mjs';
import {enrolled,authorization,exchange,issuer,accounts,authPage,form,ticket,handle} from './fixtures/oauth.mjs';
test('separate reviewed task consent, encrypted Auth vault and original deterministic subscription receipt',async t=>{
 const x=await setup(t);assert.equal((await x.call('events/subscribe',x.params)).status,403);assert.equal(x.packets.length,0);
 const c=await x.consent(),r=await x.call('events/subscribe',x.params);assert.equal(r.status,200,JSON.stringify({body:await r.clone().text(),receipts:(await x.f.db.prepare('SELECT receipt_json FROM integration_event_callbacks').all()).results}));const result=await r.json();assert.match(result.id,/^[a-f0-9]{64}$/);assert.equal(result.cursor,null);assert.equal(x.packets.length,1);
 assert.deepEqual(await (await x.call('events/subscribe',x.params)).json(),result);assert.equal(x.packets.length,1);
 const status=await (await x.call('get_event_subscription',{subscriptionId:result.id})).json();assert.equal(status.subscription.state,'active');assert.equal(status.receipt.challengeVerified,true);assert.equal(status.chatResponse,null);
 const authRows=(await x.f.db.prepare('SELECT * FROM integration_event_callbacks').all()).results,workspaceRows=(await x.f.workspaceDb.prepare('SELECT * FROM workspace_event_subscriptions').all()).results;
 for(const value of [authRows,workspaceRows,status,result]){const raw=JSON.stringify(value);assert.ok(!raw.includes(x.params.delivery.url));assert.ok(!raw.includes(secret));assert.ok(!raw.includes('Private fixture text'));}
 assert.equal(authRows[0].ciphertext.length>50,true);assert.equal(workspaceRows[0].callback_id,status.subscription.callbackId);
 const recovery=await x.call('get_event_consent',{taskId:x.taskId,commandId:c.preview.commandId});assert.deepEqual((await recovery.json()).receipt,c.result.receipt);
 const unsub={name:x.params.name,arguments:x.params.arguments,delivery:{mode:'webhook',url:x.params.delivery.url}};assert.deepEqual(await (await x.call('events/unsubscribe',unsub)).json(),{});assert.deepEqual(await (await x.call('events/unsubscribe',unsub)).json(),{});assert.equal((await x.f.db.prepare('SELECT count(*) n FROM integration_event_callbacks').first()).n,0);
});
test('wrong echo and uncertain challenge remain inactive and retain one original identity',async t=>{
 for(const behavior of ['wrong','throw','refuse']){const x=await setup(t,{behavior});await x.consent();assert.equal((await x.call('events/subscribe',x.params)).status,409);assert.equal((await x.call('events/subscribe',x.params)).status,409);assert.equal(x.packets.length,1);const row=await x.f.workspaceDb.prepare('SELECT * FROM workspace_event_subscriptions').first();assert.equal(row.state,'unknown');assert.equal((await x.f.db.prepare('SELECT count(*) n FROM integration_event_callbacks').first()).n,1);}
});
test('schema, current resource access, independent profiles and changed authority refuse before dispatch',async t=>{
 const x=await setup(t);await x.consent();
 for(const params of [{...x.params,arguments:{taskId:crypto.randomUUID()}},{...x.params,arguments:{taskId:x.taskId,profileId:x.p.profileId}},{...x.params,cursor:'replay'},{...x.params,delivery:{...x.params.delivery,url:'http://127.0.0.1/callback'}},{...x.params,delivery:{...x.params.delivery,secret:'whsec_bad'}}])assert.notEqual((await x.call('events/subscribe',params)).status,200);
 assert.equal(x.packets.length,0);
 const other=await enrolled(x.f,accounts[1]),r=await exchange(x.f,await authorization(x.f,other,'profile:read tasks:read events.subscriptions.write')),token=(await r.json()).access_token;
 assert.equal((await x.f.internal.fetch('https://auth.internal/events',{method:'POST',headers:{authorization:'Bearer '+token},body:JSON.stringify({method:'events/subscribe',params:x.params})})).status,403);
 await x.f.workspaceDb.prepare("UPDATE workspaces SET document_json=json_set(document_json,'$.tasks[0].isArchived',json('true')) WHERE profile_id=?").bind(x.p.profileId).run();assert.equal((await x.call('events/subscribe',x.params)).status,403);assert.equal(x.packets.length,0);
});
test('concurrent duplicates share one challenge; rotation verifies new key and retains a bounded second signature',async t=>{
 const x=await setup(t);await x.consent();const replies=await Promise.all([x.call('events/subscribe',x.params),x.call('events/subscribe',x.params)]);assert.deepEqual(replies.map(r=>r.status).sort(),[200,409]);assert.equal(x.packets.length,1);
 const next='whsec_'+Buffer.alloc(32,11).toString('base64');x.setSigningSecret(next);const response=await x.call('events/subscribe',{...x.params,delivery:{...x.params.delivery,secret:next}});assert.equal(response.status,200,await response.clone().text());assert.equal(x.packets.length,2);assert.equal(x.packets[1].payload.headers['webhook-signature'].split(' ').length,2);assert.equal((await x.f.workspaceDb.prepare('SELECT revision FROM workspace_event_subscriptions').first()).revision,2);
 const oldId=(await response.json()).id;await x.consent(false);assert.equal((await x.f.db.prepare('SELECT count(*) n FROM integration_event_callbacks').first()).n,0);assert.equal((await x.call('events/subscribe',x.params)).status,403);
 await x.consent(true);x.setSigningSecret(secret);const renewed=await x.call('events/subscribe',x.params);assert.equal(renewed.status,200,await renewed.clone().text());assert.equal((await renewed.json()).id,oldId);assert.equal(x.packets[2].payload.headers['webhook-signature'].split(' ').length,1);
});
test('expiry, grant revocation and vault tampering never mint replacement challenge authority',async t=>{
 const x=await setup(t);await x.consent();let r=await x.call('events/subscribe',x.params);assert.equal(r.status,200);const id=(await r.json()).id;
 await x.f.workspaceDb.prepare('UPDATE workspace_event_subscriptions SET expires_at=?').bind(Date.now()-1).run();assert.equal((await x.call('events/subscribe',x.params)).status,409);assert.equal(x.packets.length,1);
 await x.consent(true);await x.call('events/subscribe',x.params);assert.equal(x.packets.length,2);
 await x.f.db.prepare("UPDATE integration_event_callbacks SET ciphertext='tampered' WHERE revision=(SELECT MAX(revision) FROM integration_event_callbacks)").run();assert.equal((await x.call('events/subscribe',x.params)).status,503);assert.equal(x.packets.length,2);
 const revoked=await x.f.auth.fetch(`${issuer}/profiles/${x.p.profileId}/revoke-grants`,{method:'POST',headers:{authorization:'Bearer '+x.p.managementToken},body:JSON.stringify({confirm:'revoke-chatgpt-grants'})});assert.equal(revoked.status,200);assert.equal((await x.call('get_event_subscription',{subscriptionId:id})).status,401);assert.equal((await x.f.db.prepare('SELECT count(*) n FROM integration_event_callbacks').first()).n,0);
});
import {PROTOCOL_VERSION_META_KEY,CLIENT_CAPABILITIES_META_KEY} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/client/dist/index.mjs';
import {resource} from './fixtures/oauth.mjs';
test('authenticated modern MCP discovery and extension methods preserve exact schemas, consent tools and callback error code',async t=>{
 const x=await setup(t);let next=0;
 const rpc=async(method,params={})=>{
  const body={jsonrpc:'2.0',id:++next,method,params:{...params,_meta:{[PROTOCOL_VERSION_META_KEY]:'2026-07-28',[CLIENT_CAPABILITIES_META_KEY]:{}}}};
  return x.f.mcp.fetch(resource,{method:'POST',headers:{authorization:'Bearer '+x.token,'content-type':'application/json',accept:'application/json, text/event-stream','mcp-protocol-version':'2026-07-28','mcp-method':method,...(method==='tools/call'?{'mcp-name':params.name}:{})},body:JSON.stringify(body)});
 };
 const discovery=await (await rpc('server/discover')).json();assert.deepEqual(discovery.result.capabilities.events,{});
 const tools=await (await rpc('tools/list')).json();assert.ok(tools.result.tools.some(t=>t.name==='prepare_event_consent'));const events=await (await rpc('events/list')).json();assert.equal(events.result.events[0].name,'task.reminder.due');
 await x.consent();x.setBehavior('wrong');const refused=await (await rpc('events/subscribe',x.params)).json();assert.equal(refused.error.code,-32015,JSON.stringify(refused));assert.equal(refused.error.data.reason,'challenge_failed');assert.equal(x.packets.length,1);
});
test('unsubscribe racing verification and failed secret cleanup retain a closed resource fence',async t=>{
 let x;x=await setup(t,{onPacket:async()=>{
  const r=await x.call('events/unsubscribe',{name:x.params.name,arguments:x.params.arguments,delivery:{mode:'webhook',url:x.params.delivery.url}});assert.equal(r.status,200);
 }});await x.consent();assert.equal((await x.call('events/subscribe',x.params)).status,409);assert.equal((await x.f.workspaceDb.prepare('SELECT state FROM workspace_event_subscriptions').first()).state,'unsubscribed');assert.equal(x.packets.length,1);
 const y=await setup(t);await y.consent();assert.equal((await y.call('events/subscribe',y.params)).status,200);
 await y.f.db.prepare("CREATE TRIGGER synthetic_cleanup_failure BEFORE DELETE ON integration_event_callbacks BEGIN SELECT RAISE(ABORT,'synthetic cleanup outage'); END").run();
 const unsub={name:y.params.name,arguments:y.params.arguments,delivery:{mode:'webhook',url:y.params.delivery.url}};assert.equal((await y.call('events/unsubscribe',unsub)).status,503);assert.equal((await y.f.workspaceDb.prepare('SELECT state FROM workspace_event_subscriptions').first()).state,'unsubscribed');assert.equal((await y.call('events/subscribe',y.params)).status,409);assert.equal(y.packets.length,1);
 await y.f.db.prepare('DROP TRIGGER synthetic_cleanup_failure').run();assert.equal((await y.call('events/unsubscribe',unsub)).status,200);assert.equal((await y.f.db.prepare('SELECT count(*) n FROM integration_event_callbacks').first()).n,0);
});
test('unsupported configuration, connection change and ciphertext key version fail closed without exposing callback data',async t=>{
 const disabled=await setup(t,{fixture:{eventBindings:{...eventBindings,CALLBACK_EGRESS_ENABLED:false}}});assert.equal((await disabled.call('events/list',{})).status,503);assert.equal(disabled.packets.length,0);
 const x=await setup(t,{fixture:{eventBindings:{...eventBindings,CALLBACK_VAULT_CURRENT_KEY_VERSION:'2',CALLBACK_VAULT_KEY_V2:key(12)}}});await x.consent();assert.equal((await x.call('events/subscribe',x.params)).status,200);assert.equal((await x.f.db.prepare('SELECT key_version FROM integration_event_callbacks').first()).key_version,'2');
 await x.f.db.prepare('UPDATE integration_profiles SET connection_id=? WHERE profile_id=?').bind(crypto.randomUUID(),x.p.profileId).run();assert.equal((await x.call('events/subscribe',x.params)).status,401);assert.equal(x.packets.length,1);
});
test('OAuth Events permission has an independent readable opt-in and preserves selected consent',async t=>{
 const x=await setup(t),code=await ticket(x.f,x.p),page=await authPage(x.f,'profile:read tasks:read'),first=await page.response.text();
 const verified=await form(x.f,page,{handle:handle(first),decision:'identify',code:code.code,scope:'profile:read tasks:read'});assert.equal(verified.status,200);const html=await verified.text();
 assert.match(html,/Reminder events/);assert.match(html,/Can create and stop signed callbacks only for separately reviewed exact-task Events consent/);const control=html.match(/<input[^>]+name="scope"[^>]+value="events.subscriptions.write"[^>]*>/)[0];assert.ok(!control.includes('checked'));assert.ok(!control.includes('disabled'));
 if(process.env.EVENTS_UI_CAPTURE_DIR){const {writeFile,mkdir}=await import('node:fs/promises');await mkdir(process.env.EVENTS_UI_CAPTURE_DIR,{recursive:true});await writeFile(process.env.EVENTS_UI_CAPTURE_DIR+'/consent.html',html,{mode:0o600});await writeFile(process.env.EVENTS_UI_CAPTURE_DIR+'/preview.html',first,{mode:0o600});}
});
test('a shorter refresh grants no more than the requested finite lifetime',async t=>{
 const x=await setup(t);await x.consent();const first=await x.call('events/subscribe',x.params);assert.equal(first.status,200);const original=await first.json(),before=Date.now();
 const response=await x.call('events/subscribe',{...x.params,ttlMs:60000});assert.equal(response.status,200,await response.clone().text());const refreshed=await response.json();assert.equal(refreshed.id,original.id);assert.ok(Date.parse(refreshed.refreshBefore)<=before+65000,'refresh retained authority beyond the requested one-minute lifetime');
});
