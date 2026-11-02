import test from 'node:test';
import assert from 'node:assert/strict';
import {setup} from '../../chatgpt-auth/test/fixtures/events.mjs';
import {issuer} from '../../chatgpt-auth/test/fixtures/oauth.mjs';
import {scheduledEvents} from '../src/event-scheduler.ts';
import {reminderCivilInstant} from '../../shared/reminder-calendar.ts';
import {Client} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js';
import {StreamableHTTPClientTransport} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/streamableHttp.js';
import {resource} from '../../chatgpt-auth/test/fixtures/oauth.mjs';
async function ready(t,options={}){
 const x=await setup(t,{at:Date.now()-60000,...options});await x.consent();const r=await x.call('events/subscribe',x.params);assert.equal(r.status,200,await r.clone().text());x.subscription=await r.json();x.env=await x.f.mf.getBindings('workspace');return x;
}
const applications=x=>x.packets.filter(p=>p.grant.operation==='event');
const outbox=x=>x.f.workspaceDb.prepare('SELECT * FROM workspace_event_outbox ORDER BY event_id').all().then(r=>r.results);
test('due exact-task event has one durable identity, generic signed bytes and original public receipt across repeated wakes',async t=>{
 const x=await ready(t);await Promise.all([scheduledEvents(x.env),scheduledEvents(x.env)]);assert.equal(applications(x).length,1);
 const packet=applications(x)[0],body=JSON.parse(Buffer.from(packet.payload.body,'base64'));assert.deepEqual(body.data,{taskId:x.taskId});assert.equal(body.name,'task.reminder.due');assert.equal(body.cursor,null);assert.equal(packet.payload.headers['webhook-id'],body.eventId);assert.equal(packet.payload.headers['x-mcp-subscription-id'],x.subscription.id);assert.ok(!JSON.stringify(packet).includes('Private fixture text'));
 const row=(await outbox(x))[0];assert.equal(row.event_id,body.eventId);assert.equal(row.state,'accepted');assert.equal(row.attempt,1);await scheduledEvents({...x.env});assert.equal(applications(x).length,1);
 const status=await x.call('get_event_delivery',{eventId:row.event_id});assert.equal(status.status,200);const value=await status.json();assert.equal(value.acceptedByHost,true);assert.equal(value.chatResponse,null);assert.equal(value.receipt.requestId,packet.grant.requestId);
 const client=new Client({name:'original-event-receipt-fixture',version:'1'});t.after(()=>client.close());await client.connect(new StreamableHTTPClientTransport(new URL(resource),{requestInit:{headers:{authorization:'Bearer '+x.token}},fetch:(url,init)=>x.f.mcp.fetch(String(url),init)}));const recovered=await client.callTool({name:'get_event_delivery',arguments:{eventId:row.event_id}});assert.notEqual(recovered.isError,true,JSON.stringify(recovered));assert.deepEqual(recovered.structuredContent,value);
 assert.deepEqual(JSON.parse((await x.f.workspaceDb.prepare('SELECT document_json FROM workspaces WHERE profile_id=?').bind(x.p.profileId).first()).document_json),x.document);
});
test('future, changed cue, archived task, revoked consent and changed grants cannot dispatch an application event',async t=>{
 for(const change of ['future','cue','archive','consent','unsubscribe','grant']){
  const x=await ready(t,change==='future'?{at:Date.now()+60000}:{});
  if(change==='cue')await x.f.workspaceDb.prepare("UPDATE workspaces SET document_json=json_set(document_json,'$.tasks[0].reviewAfterUtc',?)").bind(new Date(Date.now()+120000).toISOString()).run();
  if(change==='archive')await x.f.workspaceDb.prepare("UPDATE workspaces SET document_json=json_set(document_json,'$.tasks[0].isArchived',json('true'))").run();
  if(change==='consent')await x.consent(false);
  if(change==='unsubscribe')assert.equal((await x.call('events/unsubscribe',{name:x.params.name,arguments:x.params.arguments,delivery:{mode:'webhook',url:x.params.delivery.url}})).status,200);
  if(change==='grant'){const r=await x.f.auth.fetch(`${issuer}/profiles/${x.p.profileId}/revoke-grants`,{method:'POST',headers:{authorization:'Bearer '+x.p.managementToken},body:JSON.stringify({confirm:'revoke-chatgpt-grants'})});assert.equal(r.status,200);}
  await scheduledEvents(x.env);assert.equal(applications(x).length,0,change);
 }
});
test('lost Workspace acknowledgment recovers the original Auth receipt and never repeats a send',async t=>{
 const x=await ready(t),actual=x.env.AUTH_EVENTS;
 x.env.AUTH_EVENTS={fetch:async(url,init)=>{const response=await actual.fetch(url,init);if(new URL(url).pathname==='/dispatch'){await response.text();throw Error('synthetic lost acknowledgment');}return response;}};
 await scheduledEvents(x.env);assert.equal(applications(x).length,1);assert.equal((await outbox(x))[0].state,'accepted');await scheduledEvents(x.env);assert.equal(applications(x).length,1);
 const y=await ready(t);y.setBehavior('throw');await scheduledEvents(y.env);assert.equal(applications(y).length,1);assert.equal((await outbox(y))[0].state,'unknown');await scheduledEvents(y.env);assert.equal(applications(y).length,1);
 const z=await ready(t),original=z.env.AUTH_EVENTS;z.env.AUTH_EVENTS={fetch:(url,init)=>new URL(url).pathname==='/dispatch'?Promise.reject(Error('synthetic transport failed before acceptance')):original.fetch(url,init)};await scheduledEvents(z.env);assert.equal(applications(z).length,0);assert.equal((await outbox(z))[0].state,'unknown');await scheduledEvents(z.env);assert.equal(applications(z).length,0);
});
test('a restart after Workspace receipt storage failure observes the same original Auth attempt',async t=>{
 const x=await ready(t);await x.f.workspaceDb.prepare("CREATE TRIGGER synthetic_receipt_outage BEFORE UPDATE ON workspace_event_outbox WHEN NEW.receipt_json IS NOT NULL BEGIN SELECT RAISE(ABORT,'synthetic receipt outage'); END").run();
 await assert.rejects(scheduledEvents(x.env));assert.equal(applications(x).length,1);const original=(await outbox(x))[0];assert.equal(original.state,'dispatching');
 await x.f.workspaceDb.prepare('DROP TRIGGER synthetic_receipt_outage').run();await x.f.workspaceDb.prepare('UPDATE workspace_event_outbox SET lease_until=0').run();await scheduledEvents({...x.env});const recovered=(await outbox(x))[0];assert.equal(recovered.state,'accepted');assert.equal(recovered.event_id,original.event_id);assert.equal(recovered.attempt,1);assert.equal(applications(x).length,1);
});
test('known transient responses use bounded exponential retries with the same event body and fresh service request identities',async t=>{
 const x=await ready(t);x.setEventStatus(503);let clock=Date.now();await scheduledEvents(x.env,{now:()=>clock});assert.equal((await outbox(x))[0].state,'retryable');await scheduledEvents(x.env,{now:()=>clock});assert.equal(applications(x).length,1);
 clock+=1001;await scheduledEvents(x.env,{now:()=>clock});assert.equal(applications(x).length,2);clock+=2001;await scheduledEvents(x.env,{now:()=>clock});assert.equal(applications(x).length,3);assert.equal((await outbox(x))[0].state,'refused');clock+=60000;await scheduledEvents(x.env,{now:()=>clock});assert.equal(applications(x).length,3);
 const packets=applications(x);assert.equal(new Set(packets.map(p=>p.grant.requestId)).size,3);assert.equal(new Set(packets.map(p=>p.payload.body)).size,1);assert.equal(new Set(packets.map(p=>p.payload.headers['webhook-id'])).size,1);for(const p of packets)assert.ok(Math.abs(Number(p.payload.headers['webhook-timestamp'])*1000-Date.now())<10000);
});
test('410 and 413 stop the callback; expiry and independent opt-outs produce no further application events',async t=>{
 for(const status of [410,413]){const x=await ready(t);x.setEventStatus(status);await scheduledEvents(x.env);const row=(await outbox(x))[0];assert.equal(row.state,'refused');assert.equal(JSON.parse(row.receipt_json).httpStatus,status);assert.equal((await x.f.workspaceDb.prepare('SELECT state FROM workspace_event_subscriptions').first()).state,'ended');await scheduledEvents(x.env);assert.equal(applications(x).length,1);}
 const expired=await ready(t,{at:Date.now()-3600001});await scheduledEvents(expired.env);assert.equal(applications(expired).length,0);assert.equal((await outbox(expired))[0].state,'expired');
 const off=await ready(t);await scheduledEvents({...off.env,EVENTS_ENABLED:false});assert.equal(applications(off).length,0);assert.equal((await outbox(off)).length,0);
});
test('the bounded queue advances beyond earlier accepted subscriptions without starving a burst',async t=>{
 const x=await ready(t);for(let i=1;i<21;i++){const r=await x.call('events/subscribe',{...x.params,delivery:{...x.params.delivery,url:x.params.delivery.url+'&fixture='+i}});assert.equal(r.status,200,await r.clone().text());}
 await scheduledEvents(x.env);assert.equal(applications(x).length,20);await scheduledEvents(x.env);assert.equal(applications(x).length,21);await scheduledEvents(x.env);assert.equal(applications(x).length,21);assert.equal(new Set(applications(x).map(p=>p.payload.headers['webhook-id'])).size,21);
});
test('civil DST overlap choices remain distinct absolute cues and expire without late delivery after an interruption',async t=>{
 const early={atUtc:reminderCivilInstant('2026-11-01','01:30','America/New_York','earlier')},late={atUtc:reminderCivilInstant('2026-11-01','01:30','America/New_York','later')};assert.equal(Date.parse(late.atUtc)-Date.parse(early.atUtc),3600000);
 for(const occurrence of [early,late]){const x=await ready(t,{at:Date.parse(occurrence.atUtc)});await scheduledEvents(x.env,{now:()=>Date.parse(occurrence.atUtc)-1});assert.equal(applications(x).length,0);await scheduledEvents(x.env,{now:()=>Date.parse(occurrence.atUtc)+3600001});assert.equal(applications(x).length,0);assert.equal((await outbox(x))[0].state,'expired');}
});
