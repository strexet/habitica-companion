import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile, writeFile } from 'node:fs/promises';
import { fixture, enrolled, accounts, issuer } from '../../chatgpt-auth/test/fixtures/oauth.mjs';
const origin = 'http://localhost:8789';
async function call(f, path, token, body, method = body === undefined ? 'GET' : 'POST') {
 return f.workspace.fetch(origin + path, { method, headers: { ...(token ? { authorization: `Bearer ${token}` } : {}), 'content-type': 'application/json' }, body: body === undefined ? undefined : JSON.stringify(body) });
}
async function connect(f, profile, workspaceId) {
 const r = await call(f, '/connect', profile.managementToken, { consentVersion: 'workspace-v1', ...(workspaceId ? { workspaceId } : {}) }); assert.equal(r.status, 201, await r.clone().text()); return r.json();
}
function command(workspaceId, revision = 1, text = 'A synthetic task') { return { id: crypto.randomUUID(), workspaceId, expectedRevision: revision, operation: 'createTodo', taskId: crypto.randomUUID(), patch: { text }, projectToHabitica: true }; }

test('separate consent, authenticated tenancy and private identity broker gate workspace access', async t => {
 const f = await fixture(t, { workspace: true }); const a = await enrolled(f); const b = await enrolled(f, accounts[1]);
 assert.equal((await call(f, '/connect', a.managementToken, {})).status, 403);
 assert.equal((await call(f, '/workspace', a.managementToken)).status, 401);
 assert.equal((await f.auth.fetch(issuer + '/management', { method: 'POST', headers: { authorization: `Bearer ${a.managementToken}` } })).status, 404);
 const wa = await connect(f, a); const wb = await connect(f, b); assert.notEqual(wa.workspaceId, wb.workspaceId);
 assert.equal((await call(f, '/commands', wa.sessionToken, command(wb.workspaceId))).status, 409);
 const response = await call(f, '/commands', wa.sessionToken, command(wa.workspaceId)); assert.equal(response.status, 201);
 const dataA = await (await call(f, '/export', wa.sessionToken)).json(); const dataB = await (await call(f, '/export', wb.sessionToken)).json();
 assert.equal(dataA.document.tasks.length, 1); assert.equal(dataB.document.tasks.length, 0);
 await writeFile("/tmp/habitica-cloud-journal-v1.json", JSON.stringify(dataA, null, 2) + "\n");
 assert.equal(dataA.commands.length, 1); assert.equal(dataA.events.length, 1); assert.equal(dataA.storeVersion, 2);
 assert.equal(JSON.stringify(dataA).includes(accounts[0].apiToken), false);
 assert.equal((await f.workspace.fetch(origin + '/workspace', { headers: { origin: 'https://untrusted.example', authorization: `Bearer ${wa.sessionToken}` } })).status, 403);
});

test('command receipts deduplicate before revision checks and compare-and-swap admits one concurrent writer', async t => {
 const f = await fixture(t, { workspace: true }); const profile = await enrolled(f); const w = await connect(f, profile); const c = command(w.workspaceId);
 assert.equal((await call(f, '/commands', w.sessionToken, c)).status, 201);
 const replay = await call(f, '/commands', w.sessionToken, c); assert.equal(replay.status, 200); assert.equal((await replay.json()).disposition, 'duplicate');
 assert.equal((await call(f, '/commands', w.sessionToken, { ...c, patch: { text: 'reused' } })).status, 409);
 const replies = await Promise.all([command(w.workspaceId, 2), command(w.workspaceId, 2)].map(c => call(f, '/commands', w.sessionToken, c)));
 assert.deepEqual(replies.map(r => r.status).sort(), [201, 409]);
 const snapshot = await (await call(f, '/export', w.sessionToken)).json();
 assert.equal(snapshot.document.revision, 3); assert.equal(snapshot.document.tasks.length, 2); assert.equal(snapshot.commands.length, 2); assert.equal(snapshot.events.length, 2);
 assert.equal(snapshot.commands[0].projectionState, 'pending'); assert.deepEqual(snapshot.commands[0].effects, ['create']);
});

test('D1 journal append failure rolls canonical update and command receipt back', async t => {
 const f = await fixture(t, { workspace: true }); const w = await connect(f, await enrolled(f));
 await f.workspaceDb.prepare("CREATE TRIGGER fixture_event_failure BEFORE INSERT ON workspace_events BEGIN SELECT RAISE(ABORT, 'fixture quota failure'); END").run();
 assert.equal((await call(f, '/commands', w.sessionToken, command(w.workspaceId))).status, 503);
 const snapshot = await (await call(f, '/export', w.sessionToken)).json(); assert.equal(snapshot.document.revision, 1); assert.equal(snapshot.document.tasks.length, 0); assert.equal(snapshot.commands.length, 0); assert.equal(snapshot.events.length, 0);
});

test('multiple sessions survive unchanged credential validation; revocation and workspace lifecycle fail closed', async t => {
 const f = await fixture(t, { workspace: true }); const p = await enrolled(f); const w = await connect(f, p); const second = await connect(f, p);
 assert.equal((await call(f, '/workspace', w.sessionToken)).status, 200);
 const revalidated = await enrolled(f); const third = await connect(f, revalidated);
 assert.equal((await call(f, '/workspace', w.sessionToken)).status, 200);
 assert.equal((await call(f, '/disconnect', second.sessionToken, {})).status, 200);
 assert.equal((await call(f, '/workspace', third.sessionToken)).status, 401);
 const resumed = await connect(f, revalidated); assert.equal(resumed.workspaceId, w.workspaceId);
 assert.equal((await call(f, '/workspace', resumed.sessionToken, { confirmWorkspaceId: crypto.randomUUID() }, 'DELETE')).status, 409);
 assert.equal((await call(f, '/workspace', resumed.sessionToken, { confirmWorkspaceId: resumed.workspaceId }, 'DELETE')).status, 200);
 assert.equal((await call(f, '/workspace', resumed.sessionToken)).status, 401);
 assert.equal((await f.workspaceDb.prepare('SELECT COUNT(*) AS count FROM workspace_sessions').first()).count, 0);
 const recreated = await connect(f, revalidated); assert.notEqual(recreated.workspaceId, w.workspaceId);
 await f.auth.fetch(`${issuer}/profiles/${p.profileId}/disconnect`, { method: 'POST', headers: { authorization: `Bearer ${revalidated.managementToken}` } });
 assert.equal((await call(f, '/workspace', recreated.sessionToken)).status, 401);
});

test('neutral canonical fixture supports typed update, Daily completion and bounded Habit scoring without upstream writes', async t => {
 const f = await fixture(t, { workspace: true }); const document = JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/workspace-v1.json', import.meta.url), 'utf8'));
 document.tasks.push({ ...document.tasks[0], id: crypto.randomUUID(), kind: "daily", recurrence: { frequency: "daily", interval: 1, startDate: "2026-10-15", timeZoneId: "UTC", weekdays: [], daysOfMonth: [] } }, { ...document.tasks[0], id: crypto.randomUUID(), kind: "habit" });
 const w = await connect(f, await enrolled(f), document.id);
 await f.workspaceDb.prepare('UPDATE workspaces SET document_json=?').bind(JSON.stringify(document)).run();
 const daily = document.tasks.find(task => task.kind === 'daily'); const habit = document.tasks.find(task => task.kind === 'habit');
 assert.ok(daily); assert.ok(habit);
 let rev = document.revision;
 const complete = { id: crypto.randomUUID(), workspaceId: document.id, expectedRevision: rev++, operation: 'completeTask', taskId: daily.id, occurrenceDate: '2026-10-15' };
 const completed = await call(f, '/commands', w.sessionToken, complete); assert.equal(completed.status, 201, await completed.clone().text());
 const score = { id: crypto.randomUUID(), workspaceId: document.id, expectedRevision: rev++, operation: 'scoreHabit', taskId: habit.id, direction: 'up', count: 3 };
 assert.equal((await call(f, '/commands', w.sessionToken, score)).status, 201);
 const result = await (await call(f, '/export', w.sessionToken)).json(); assert.equal(result.document.completions.length, (document.completions ?? []).length + 1); assert.equal(result.document.habitScores.length, (document.habitScores ?? []).length + 3);
 assert.equal(result.commands[1].projectionState, 'notRequested');
 const invalid = { ...score, id: crypto.randomUUID(), expectedRevision: rev, count: 11 };
 assert.equal((await call(f, '/commands', w.sessionToken, invalid)).status, 422);
});

test('invalid canonical graphs and malformed commands cannot partially save', async t => {
 const f = await fixture(t, { workspace: true }); const source = JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/workspace-v1.json', import.meta.url), 'utf8'));
 const w = await connect(f, await enrolled(f), source.id);
 const corruptions = [
  doc => { doc.tasks.push(structuredClone(doc.tasks[0])); },
  doc => { doc.tasks[0].projectId = crypto.randomUUID(); },
  doc => { doc.tasks[0].dependsOn = [doc.tasks[0].id]; },
  doc => { doc.tasks[0].createdAtUtc = '2026-10-15T00:00:00.0000002Z'; doc.tasks[0].updatedAtUtc = '2026-10-15T00:00:00.0000001Z'; },
  doc => { doc.tasks[0].kind = 0; },
  doc => { doc.unknown = true; },
 ];
 for (const corrupt of corruptions) {
  const doc = structuredClone(source); corrupt(doc);
  await f.workspaceDb.prepare('UPDATE workspaces SET document_json=?').bind(JSON.stringify(doc)).run();
  const response = await call(f, '/commands', w.sessionToken, command(source.id)); assert.ok([400, 422].includes(response.status), await response.clone().text());
 }
 await f.workspaceDb.prepare('UPDATE workspaces SET document_json=?').bind(JSON.stringify(source)).run();
 const c = command(source.id);
 const duplicateKeys = JSON.stringify(c).replace('"expectedRevision":1', '"expectedRevision":999,"expectedRevision":1');
 const malformed = await f.workspace.fetch(origin + '/commands', { method: 'POST', headers: { authorization: `Bearer ${w.sessionToken}` }, body: duplicateKeys }); assert.equal(malformed.status, 400);
 assert.equal((await call(f, '/commands', w.sessionToken, { ...c, patch: { text: 'x'.repeat(70000) } })).status, 413);
 const data = await (await call(f, '/export', w.sessionToken)).json(); assert.equal(data.commands.length, 0); assert.equal(data.storeVersion, 1);
});

test('delayed authorized requests cannot reach a deleted and recreated workspace with the same canonical ID', async t => {
 let f, gate;
 f = await fixture(t, { workspace: true, identity: async request => {
  const response = await f.workspaceIdentity.fetch(request);
  if (gate && new URL(request.url).pathname === '/epoch') { const held = gate; gate = null; held.enter(); await held.wait; }
  return response;
 } });
 const profile = await enrolled(f);
 for (const action of ['command', 'export', 'disconnect', 'delete', 'disconnect-reconnect']) {
  const w = await connect(f, profile); let enter, release;
  const entered = new Promise(resolve => { enter = resolve; }); const wait = new Promise(resolve => { release = resolve; }); gate = { enter, wait };
  const pending = action === 'command' ? call(f, '/commands', w.sessionToken, command(w.workspaceId))
   : action === 'export' ? call(f, '/export', w.sessionToken)
   : ['disconnect', 'disconnect-reconnect'].includes(action) ? call(f, '/disconnect', w.sessionToken, {})
   : call(f, '/workspace', w.sessionToken, { confirmWorkspaceId: w.workspaceId }, 'DELETE');
  await entered;
  try {
   if (action === 'disconnect-reconnect') assert.equal((await call(f, '/disconnect', w.sessionToken, {})).status, 200);
   else assert.equal((await call(f, '/workspace', w.sessionToken, { confirmWorkspaceId: w.workspaceId }, 'DELETE')).status, 200);
   const replacement = await connect(f, profile, w.workspaceId);
   release(); const stale = await pending;
   assert.ok([401,409].includes(stale.status), `${action}: a stale request reached replacement data (status ${stale.status})`);
   const current = await call(f, '/export', replacement.sessionToken); assert.equal(current.status, 200, `${action}: ${await current.clone().text()}`); const data = await current.json(); assert.equal(data.document.tasks.length, 0); assert.equal(data.commands.length, 0);
  } finally { release(); }
 }
});

test('archive capacity stops new acceptance before retained history becomes unexportable', async t => {
 const f = await fixture(t, { workspace: true }); const w = await connect(f, await enrolled(f));
 await f.workspaceDb.prepare('UPDATE workspaces SET archive_bytes=?').bind(8 * 1024 * 1024).run();
 const response = await call(f, '/commands', w.sessionToken, command(w.workspaceId)); assert.equal(response.status, 413);
 const data = await (await call(f, '/export', w.sessionToken)).json(); assert.equal(data.document.revision, 1); assert.equal(data.commands.length, 0); assert.equal(data.events.length, 0);
});

test('projection adoption requires reviewed fresh metadata and never writes upstream or overwrites canonical fields', async t => {
 const externalId='90000000-0000-4000-8000-000000000001'; let taskWrites=0; let remoteText='External task';
 const f=await fixture(t,{workspace:true,projection:true,habitica:async request=>{
  const user=request.headers.get('x-api-user');
  if (request.url.includes('user?userFields=_id,profile')) return Response.json({success:true,data:{_id:user,profile:{name:'Synthetic'}}});
  if (request.method!=='GET')taskWrites++;
  return Response.json({success:true,data:{id:externalId,userId:user,type:'todo',text:remoteText,notes:'External notes',priority:1,attribute:'str',completed:false,checklist:[],group:{},challenge:{}}});
 }});
 const w=await connect(f,await enrolled(f)); const c={...command(w.workspaceId),projectToHabitica:false}; assert.equal((await call(f,'/commands',w.sessionToken,c)).status,201);
 const preview=await(await call(f,'/projection/preview',w.sessionToken,{taskId:c.taskId,habiticaTaskId:externalId})).json();
 assert.equal(preview.task.text,c.patch.text); assert.equal(preview.external.text,remoteText); assert.equal(preview.pendingCommandCount,0);
 remoteText='Changed after preview';
 const approval={taskId:c.taskId,habiticaTaskId:externalId,expectedRevision:preview.expectedRevision,expectedFingerprint:preview.external.fingerprint};
 assert.equal((await call(f,'/projection/adopt',w.sessionToken,approval)).status,409);
 const fresh=await(await call(f,'/projection/preview',w.sessionToken,{taskId:c.taskId,habiticaTaskId:externalId})).json();
 assert.equal((await call(f,'/projection/adopt',w.sessionToken,{...approval,expectedFingerprint:fresh.external.fingerprint})).status,200);
 const exported=await(await call(f,'/export',w.sessionToken)).json(); assert.equal(exported.document.tasks[0].id,c.taskId); assert.equal(exported.document.tasks[0].text,c.patch.text); assert.equal(exported.document.projections[0].habiticaTaskId,externalId); assert.equal(taskWrites,0);
 const update={id:crypto.randomUUID(),workspaceId:w.workspaceId,expectedRevision:exported.document.revision,operation:'updateTask',taskId:c.taskId,patch:{notes:'An explicit later edit'}};
 const saved=await(await call(f,'/commands',w.sessionToken,update)).json(); assert.equal(saved.entry.projectionState,'pending'); assert.deepEqual(saved.entry.effects,['update']);
 assert.equal((await call(f,'/projection/adopt',w.sessionToken,{...approval,expectedRevision:exported.document.revision+1,expectedFingerprint:fresh.external.fingerprint})).status,409);
});

test('accepted completion drives ordered create then score through the private gateway without a second canonical ID', async t => {
 const {projectionStep,confirmMapping}=await import('../src/projection-adapter.ts');
 let remote=null;const writes=[];
 const f=await fixture(t,{workspace:true,projection:true,habitica:async request=>{
  const user=request.headers.get('x-api-user');
  if(request.url.includes('user?userFields=_id,profile'))return Response.json({success:true,data:{_id:user,profile:{name:'Synthetic'}}});
  if(request.method==='GET')return remote?Response.json({success:true,data:remote}):new Response('',{status:404});
  writes.push(new URL(request.url).pathname);
  if(request.url.endsWith('/score/up')){remote.completed=true;return Response.json({success:true,data:{delta:1}});}
  remote={id:'90000000-0000-4000-8000-000000000002',userId:user,notes:'',priority:1,attribute:'str',completed:false,checklist:[],group:{},challenge:{},...await request.json()};
  return Response.json({success:true,data:remote},{status:201});
 }});
 const profile=await enrolled(f);const w=await connect(f,profile);const c={...command(w.workspaceId),projectToHabitica:false};assert.equal((await call(f,'/commands',w.sessionToken,c)).status,201);
 const complete={id:crypto.randomUUID(),workspaceId:w.workspaceId,expectedRevision:2,operation:'completeTask',taskId:c.taskId,projectToHabitica:true};assert.equal((await call(f,'/commands',w.sessionToken,complete)).status,201);
 const exported=await(await call(f,'/export',w.sessionToken)).json();let doc=exported.document;let entry=exported.commands[1];
 const context=await(await f.workspaceIdentity.fetch('https://identity.internal/management',{method:'POST',headers:{authorization:`Bearer ${profile.managementToken}`}})).json();
 const gateway=async(operation,args,writing=false)=>{
  const r=await f.projection.fetch('https://projection.internal/operation',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({...context,operation,arguments:args,automated:false,...(writing?{attemptId:crypto.randomUUID(),attemptAtUtc:new Date().toISOString()}:{})})});assert.equal(r.status,200,await r.clone().text());return r.json();
 };
 for(let i=0;i<2;i++){
  const need=projectionStep(doc,entry);assert.equal(need.kind,'inspect');const observed=await gateway(need.operation,{selector:need.selector});
  const step=projectionStep(doc,entry,observed);assert.equal(step.kind,'write');const result=await gateway(step.operation,step.arguments,true);assert.equal(result.kind,'applied');
  doc=confirmMapping(doc,c.taskId,result.task);entry={...entry,confirmedEffects:entry.confirmedEffects+1};
 }
 assert.deepEqual(writes,['/api/v3/tasks/user','/api/v3/tasks/90000000-0000-4000-8000-000000000002/score/up']);assert.equal(doc.tasks[0].id,c.taskId);assert.equal(doc.tasks[0].state,'completed');assert.equal(doc.completions.length,1);assert.equal(entry.confirmedEffects,2);
});
