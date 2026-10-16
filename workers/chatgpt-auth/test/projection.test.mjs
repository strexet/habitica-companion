import { test } from 'node:test';
import assert from 'node:assert/strict';
import { fixture, enrolled, accounts, issuer } from './fixtures/oauth.mjs';
const taskId = '90000000-0000-4000-8000-000000000001';
const alias = 'companion_' + '1'.repeat(32) + '_' + '2'.repeat(32);
function task(overrides = {}) { return { id: taskId, userId: accounts[0].habiticaUserId, type: 'todo', text: 'Synthetic task', notes: '', priority: 1, attribute: 'str', completed: false, up: true, down: true, checklist: [], group: {}, challenge: {}, ...overrides }; }
async function setup(t, handler) {
 const requests = [];
 const f = await fixture(t, { workspace: true, projection: true, habitica: async request => {
  if (request.url.includes('user?userFields=_id,profile')) return Response.json({ success: true, data: { _id: request.headers.get('x-api-user'), profile: { name: 'Synthetic' } } });
  assert.ok(request.url.startsWith('https://habitica.com/api/v3/')); assert.ok(request.headers.get('x-client'));
  requests.push({ method: request.method, url: request.url }); return handler(request);
 } });
 const profile = await enrolled(f); const row = await f.db.prepare('SELECT connection_id FROM integration_profiles WHERE profile_id=?').bind(profile.profileId).first();
 const context = { profileId: profile.profileId, connectionId: row.connection_id };
 const call = (operation, args = {}, extra = {}) => f.projection.fetch('https://projection.internal/operation', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ ...context, operation, arguments: args, automated: false, ...extra }) });
 const attempt = () => ({ attemptId: crypto.randomUUID(), attemptAtUtc: new Date().toISOString() });
 return { ...f, profile, context, call, attempt, requests };
}

test('private gateway requires verified absence and deduplicates a durable create attempt', async t => {
 let created;
 const f = await setup(t, async request => {
  if (request.method === 'GET') return created ? Response.json({ success: true, data: created }) : new Response('', { status: 404 });
  assert.equal(request.method, 'POST'); const body = await request.json(); assert.equal(body.alias, alias); assert.equal(body.type, 'todo'); created = task(body); return Response.json({ success: true, data: created }, { status: 201 });
 });
 const absent = await (await f.call('inspect', { selector: alias })).json(); assert.equal(absent.task, null);
 const args = { selector: alias, checkToken: absent.checkToken, patch: { text: 'Synthetic task' } }; const a = f.attempt();
 const first = await (await f.call('create', args, a)).json(); assert.equal(first.kind, 'applied'); assert.equal(first.task.id, taskId);
 const duplicate = await (await f.call('create', args, a)).json(); assert.deepEqual(duplicate, first); assert.equal(f.requests.filter(r => r.method === 'POST').length, 1);
 assert.equal((await f.call('create', { ...args, patch: { text: 'Changed' } }, a)).status, 409);
 const stored = await f.db.prepare('SELECT * FROM integration_projection_attempts').first(); assert.equal(stored.state, 'applied'); assert.ok(!JSON.stringify(stored).includes('Synthetic task')); assert.ok(!JSON.stringify(stored).includes(accounts[0].apiToken));
 assert.notEqual((await f.auth.fetch(issuer + '/projection', { method: 'POST' })).status, 200);
});

test('foreign, group and challenge tasks cannot obtain a write precondition', async t => {
 let current = task({ userId: accounts[1].habiticaUserId }); const f = await setup(t, () => Response.json({ success: true, data: current }));
 for (const fields of [{ userId: accounts[1].habiticaUserId }, { group: { id: taskId } }, { challenge: { id: taskId } }]) {
  current = task(fields); assert.equal((await f.call('inspect', { selector: taskId })).status, 409);
 }
 assert.equal((await f.db.prepare('SELECT COUNT(*) AS count FROM integration_projection_checks').first()).count, 0);
 assert.equal(f.requests.filter(r => r.method !== 'GET').length, 0);
});

test('known scoring receipts never replay and consumed preconditions cannot authorize another score', async t => {
 const f = await setup(t, request => Response.json({ success: true, data: request.method === 'GET' ? task({ type: 'habit' }) : { delta: 1 } }));
 const inspected = await (await f.call('inspect', { selector: taskId })).json(); const args = { selector: taskId, checkToken: inspected.checkToken, expectedFingerprint: inspected.task.fingerprint, direction: 'up' }; const a = f.attempt();
 assert.equal((await (await f.call('score', args, a)).json()).kind, 'applied');
 assert.equal((await (await f.call('score', args, a)).json()).kind, 'applied');
 assert.equal((await f.call('score', args, f.attempt())).status, 409);
 assert.equal(f.requests.filter(r => r.method === 'POST').length, 1);
});

test('ambiguous scoring pauses later writes and never repeats the uncertain upstream call', async t => {
 const f = await setup(t, request => request.method === 'GET' ? Response.json({ success: true, data: task({ type: 'habit' }) }) : new Response('Synthetic upstream error with private details', { status: 500 }));
 const inspected = await (await f.call('inspect', { selector: taskId })).json(); const args = { selector: taskId, checkToken: inspected.checkToken, expectedFingerprint: inspected.task.fingerprint, direction: 'up' }; const a = f.attempt();
 assert.deepEqual(await (await f.call('score', args, a)).json(), { kind: 'unknown', code: 'outcome_unknown' });
 assert.deepEqual(await (await f.call('score', args, a)).json(), { kind: 'unknown', code: 'outcome_unknown' });
 const retry = await f.call('score', args, f.attempt()); assert.equal(retry.status, 409); assert.equal((await retry.json()).code, 'unknown_outcome_pending');
 assert.equal(f.requests.filter(r => r.method === 'POST').length, 1);
 assert.equal(f.messages.join('\n').includes(accounts[0].apiToken), false);
});

test('upstream quota and credential failures persist profile-wide refusal without exposing raw bodies', async t => {
 let status = 429;
 const f = await setup(t, () => new Response('synthetic-private-token', { status, headers: { 'retry-after': '90' } }));
 const limited = await f.call('inspect', { selector: taskId }); assert.equal(limited.status, 429); const rate = await limited.json(); assert.ok(Date.parse(rate.retryAtUtc) >= Date.now() + 88000);
 assert.equal((await f.call('inspect', { selector: taskId })).status, 429); assert.equal(f.requests.length, 1);
 await f.db.prepare('UPDATE integration_read_state SET blocked_until_utc=NULL').run(); status = 401;
 const denied = await f.call('inspect', { selector: taskId }); assert.equal(denied.status, 401); assert.ok(!(await denied.text()).includes('synthetic-private-token'));
 assert.equal((await f.call('inspect', { selector: taskId })).status, 401); assert.equal(f.requests.length, 2);
});

test('automated calls share a persisted thirty-second gate', async t => {
 const f = await setup(t, () => Response.json({ success: true, data: task() }));
 assert.equal((await f.call('inspect', { selector: taskId }, { automated: true })).status, 200);
 const paused = await f.call('inspect', { selector: taskId }, { automated: true }); assert.equal(paused.status, 429); assert.equal(f.requests.length, 1);
 const state = await f.db.prepare('SELECT next_automated_at_utc FROM integration_projection_state').first(); assert.ok(Date.parse(state.next_automated_at_utc) > Date.now() + 28000);
 await f.db.prepare('UPDATE integration_projection_state SET next_automated_at_utc=?').bind(new Date(Date.now()-1).toISOString()).run();
 assert.equal((await f.call('inspect', { selector: taskId }, { automated: true })).status, 200); assert.equal(f.requests.length, 2);
});

test('Daily scoring requires fresh server due-state, confirmed Cron state and matching Habitica day', async t => {
 const f = await setup(t, request => {
  const data = new URL(request.url).pathname === '/api/v3/user' ? { preferences: { dayStart: 0, timezoneOffset: 0 }, needsCron: false }
   : request.url.includes('tasks/user?') ? [task({ type: 'daily', isDue: true })] : { delta: 1 };
  return Response.json({ success: true, data });
 });
 const account = await (await f.call('inspectAccount')).json(); const inspected = await (await f.call('inspectDaily', { selector: taskId })).json();
 const args = { selector: taskId, checkToken: inspected.checkToken, expectedFingerprint: inspected.task.fingerprint, direction: 'up', accountCheckToken: account.accountCheckToken, occurrenceDate: account.day };
 assert.equal((await f.call('score', { ...args, occurrenceDate: '2000-01-01' }, f.attempt())).status, 409);
 assert.equal((await (await f.call('score', args, f.attempt())).json()).kind, 'applied');
 assert.equal(f.requests.filter(r => r.method === 'POST').length, 1);
});

test('Daily readiness comes from full UserGet and unavailable server readiness cannot authorize scoring',async t=>{
 let readiness=false;
 const f=await setup(t,request=>{
  if(new URL(request.url).pathname==='/api/v3/user'){
   assert.equal(new URL(request.url).searchParams.has('userFields'),false);
   return Response.json({success:true,data:{preferences:{dayStart:0,timezoneOffset:0},needsCron:readiness,flags:{needsCron:false}}});
  }
  return Response.json({success:true,data:[task({type:'daily',isDue:true})]});
 });
 assert.equal((await(await f.call('inspectAccount')).json()).needsCron,false);
 for(const value of [undefined,null,'false',0,{}]){readiness=value;assert.equal((await f.call('inspectAccount')).status,409);}
 readiness=true;const account=await(await f.call('inspectAccount')).json();const inspected=await(await f.call('inspectDaily',{selector:taskId})).json();
 const denied=await f.call('score',{selector:taskId,checkToken:inspected.checkToken,expectedFingerprint:inspected.task.fingerprint,direction:'up',accountCheckToken:account.accountCheckToken,occurrenceDate:account.day},f.attempt());assert.equal(denied.status,409);assert.equal(f.requests.filter(r=>r.method==='POST').length,0);
});

test('credential disconnect deletes private projection state and denies subsequent operations', async t => {
 const f = await setup(t, () => Response.json({ success: true, data: task() }));
 assert.equal((await f.call('inspect', { selector: taskId })).status, 200);
 const disconnected = await f.auth.fetch(`${issuer}/profiles/${f.profile.profileId}/disconnect`, { method: 'POST', headers: { authorization: `Bearer ${f.profile.managementToken}` } }); assert.equal(disconnected.status, 200);
 assert.equal((await f.call('inspect', { selector: taskId })).status, 401); assert.equal(f.requests.length, 1);
 assert.equal((await f.db.prepare('SELECT COUNT(*) AS count FROM integration_projection_checks').first()).count, 0);
});

test('ambiguous create reconciles positive alias evidence without repeating creation; absence never proves failure', async t => {
 let created = null; let hide = false;
 const f = await setup(t, async request => {
  if (request.method === 'GET') return created && !hide ? Response.json({ success: true, data: created }) : new Response('', { status: 404 });
  created = task(await request.json()); return new Response('', { status: 500 });
 });
 const absent = await (await f.call('inspect', { selector: alias })).json(); const args = { selector: alias, checkToken: absent.checkToken, patch: { text: 'Synthetic task' } }; const a = f.attempt();
 assert.equal((await (await f.call('create', args, a)).json()).kind, 'unknown');
 hide = true; assert.equal((await (await f.call('reconcile', { operation: 'create', arguments: args }, a)).json()).kind, 'unknown');
 hide = false; const resolved = await (await f.call('reconcile', { operation: 'create', arguments: args }, a)).json(); assert.equal(resolved.code, 'reconciled_applied'); assert.equal(resolved.task.id, taskId);
 assert.equal((await (await f.call('create', args, a)).json()).kind, 'applied'); assert.equal(f.requests.filter(r => r.method === 'POST').length, 1);
});

test('scheduled retention removes expired operational metadata without touching current credentials', async t => {
 const f=await setup(t,()=>Response.json({success:true,data:task()}));
 await f.call('inspect',{selector:taskId});
 await f.db.prepare("INSERT INTO integration_projection_attempts(profile_id,connection_id,attempt_id,payload_hash,operation,state,created_at_utc) VALUES(?,?,?,?,?,'unknown',?)").bind(f.context.profileId,f.context.connectionId,crypto.randomUUID(),'a'.repeat(64),'score',new Date(Date.now()-31*86400000).toISOString()).run();
 const scheduled=await f.auth.scheduled({cron:'17 * * * *'}); assert.equal(scheduled.outcome,'ok');
 assert.equal((await f.db.prepare('SELECT COUNT(*) AS count FROM integration_projection_attempts').first()).count,0);
 assert.equal((await f.db.prepare('SELECT COUNT(*) AS count FROM integration_profiles WHERE revoked_at_utc IS NULL').first()).count,1);
});

test('known write refusals remain durable receipts and preserve shared quota deadlines', async t => {
 let status=429;
 const f=await setup(t,request=>request.method==='GET'?Response.json({success:true,data:task({type:'habit'})}):new Response('private upstream details',{status,headers:{'retry-after':'60'}}));
 const inspected=await(await f.call('inspect',{selector:taskId})).json(); const args={selector:taskId,checkToken:inspected.checkToken,expectedFingerprint:inspected.task.fingerprint,direction:'up'}; const a=f.attempt();
 const first=await(await f.call('score',args,a)).json(); assert.equal(first.kind,'refused'); assert.equal(first.code,'rate_limited'); assert.ok(first.retryAtUtc);
 assert.deepEqual(await(await f.call('score',args,a)).json(),first); assert.equal(f.requests.filter(r=>r.method==='POST').length,1);
 assert.equal((await f.call('inspect',{selector:taskId})).status,429);
 await f.db.prepare('UPDATE integration_read_state SET blocked_until_utc=NULL').run();status=401;
 const refreshed=await(await f.call('inspect',{selector:taskId})).json(); const denied=await(await f.call('score',{...args,checkToken:refreshed.checkToken},f.attempt())).json(); assert.equal(denied.code,'credential_repair_required');
 assert.equal((await f.call('inspect',{selector:taskId})).status,401);
});

test('two concurrent attempts cannot consume one precondition twice', async t => {
 const f=await setup(t,request=>Response.json({success:true,data:request.method==='GET'?task({type:'habit'}):{delta:1}}));
 const inspected=await(await f.call('inspect',{selector:taskId})).json();const args={selector:taskId,checkToken:inspected.checkToken,expectedFingerprint:inspected.task.fingerprint,direction:'up'};
 const responses=await Promise.all([f.call('score',args,f.attempt()),f.call('score',args,f.attempt())]);assert.ok(responses.every(r=>[200,409,429].includes(r.status)));assert.equal(f.requests.filter(r=>r.method==='POST').length,1);
});

test('failed durable dispatch reservation prevents an upstream mutation', async t => {
 const f=await setup(t,()=>Response.json({success:true,data:task({type:'habit'})}));const inspected=await(await f.call('inspect',{selector:taskId})).json();
 await f.db.prepare("CREATE TRIGGER fixture_dispatch_failure BEFORE INSERT ON integration_projection_attempts BEGIN SELECT RAISE(ABORT,'fixture storage failure'); END").run();
 const result=await f.call('score',{selector:taskId,checkToken:inspected.checkToken,expectedFingerprint:inspected.task.fingerprint,direction:'up'},f.attempt());assert.equal(result.status,503);assert.equal(f.requests.filter(r=>r.method==='POST').length,0);
});

test('expired preconditions cannot dispatch a write', async t => {
 const f=await setup(t,()=>Response.json({success:true,data:task({type:'habit'})}));const inspected=await(await f.call('inspect',{selector:taskId})).json();
 await f.db.prepare('UPDATE integration_projection_checks SET expires_at_utc=?').bind(new Date(Date.now()-1).toISOString()).run();
 const result=await f.call('score',{selector:taskId,checkToken:inspected.checkToken,expectedFingerprint:inspected.task.fingerprint,direction:'up'},f.attempt());assert.equal(result.status,409);assert.equal(f.requests.filter(r=>r.method==='POST').length,0);
});
