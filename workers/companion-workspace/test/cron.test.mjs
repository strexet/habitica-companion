import {test} from 'node:test';
import assert from 'node:assert/strict';
import {executeProfile} from '../src/executor.ts';
import {setup,scope,gate,party} from './fixtures/cron.mjs';

test('reviewed Cron preview rotates token, dispatches once and exports an explicit secret-free receipt',async t=>{
 const s=await setup(t);const status=(await s.tool(0,'get_cron_status')).structuredContent;assert.equal(status.canPrepare,true);assert.equal(status.unfinishedDueDailyCount,1);assert.equal(status.risks.personalDamage,'unknown');assert.equal(status.preconditionHash,undefined);
 const requestId=crypto.randomUUID(),p=await s.prepare(requestId),latest=await s.prepare(requestId);assert.equal(p.actionId,latest.actionId);assert.notEqual(p.executeToken,latest.executeToken);assert.ok(Date.parse(p.expiresAtUtc)<=Date.now()+120000);assert.ok(p.preview.warnings.some(w=>w.includes('buff')));
 assert.equal((await s.execute(p)).isError,true);assert.equal(s.state.posts,0);
 const r=(await s.execute(latest)).structuredContent;assert.equal(r.status,'confirmed');assert.equal(r.confirmed,true);assert.equal(r.accepted,true);assert.equal(r.refreshSucceeded,true);assert.equal(r.outcomeUnknown,false);
 assert.deepEqual((await s.execute(latest)).structuredContent,r);assert.deepEqual((await s.tool(0,'get_cron_command',{actionId:p.actionId})).structuredContent,r);assert.equal(s.state.posts,1);
 assert.equal((await s.tool(1,'get_cron_command',{actionId:p.actionId})).structuredContent.accepted,null);assert.equal((await s.execute(latest,1)).isError,true);
 const response=await s.http(0,'/cron/history',undefined,'GET',true);assert.equal(response.status,200);const text=await response.text();assert.equal(JSON.parse(text).cron.length,1);
 for(const secret of [p.executeToken,latest.executeToken,'token_hash','payload_json','attempt_json','never-export'])assert.ok(!text.includes(secret),secret);
 assert.equal((await s.f.workspace.fetch('http://localhost:8789/cron/history')).status,401);
});

test('fresh Daily/checklist/quest/account changes refuse stale previews without rebuilding or replay',async t=>{
 const s=await setup(t);
 for(const change of [()=>s.state.task.completed=true,()=>s.state.task.checklist[0].completed=true,()=>s.state.questHp--,()=>s.state.stats.hp--]){
  const old=structuredClone(s.state),p=await s.prepare();change();const r=(await s.execute(p)).structuredContent;assert.equal(r.status,'stopped');assert.equal(r.failure,'cron_precondition_changed');assert.equal(r.confirmed,false);assert.equal(r.outcomeUnknown,false);assert.deepEqual((await s.execute(p)).structuredContent,r);Object.assign(s.state,old);
 }
 const p=await s.prepare();await s.f.workspaceDb.prepare('UPDATE prepared_cron_actions SET expires_at_utc=? WHERE action_id=?').bind('2000-01-01T00:00:00Z',p.actionId).run();assert.equal((await s.execute(p)).isError,true);assert.equal(s.state.posts,0);
 s.state.needsCron=false;assert.equal((await s.tool(0,'prepare_start_new_day',{requestId:crypto.randomUUID()})).isError,true);
});

test('success followed by refresh failure remains confirmed and is never resent',async t=>{
 const s=await setup(t);const p=await s.prepare();s.state.refreshFail=true;const r=(await s.execute(p)).structuredContent;assert.equal(r.status,'confirmed');assert.equal(r.refreshSucceeded,false);assert.equal(r.outcomeUnknown,false);assert.deepEqual((await s.execute(p)).structuredContent,r);assert.equal(s.state.posts,1);
});

test('lost acknowledgement recovers only the original receipt and reports unperformed refresh',async t=>{
 const s=await setup(t);const p=await s.prepare();s.state.lost=true;assert.equal((await s.execute(p)).structuredContent.status,'unknownOutcome');
 const r=(await s.tool(0,'get_cron_command',{actionId:p.actionId})).structuredContent;assert.equal(r.status,'confirmed');assert.equal(r.refreshSucceeded,false);assert.equal(s.state.posts,1);
 assert.deepEqual((await s.execute(p)).structuredContent,r);assert.equal(s.state.posts,1);
});

test('unknown effects stay fenced after day progression and Auth receipt retention',async t=>{
 const s=await setup(t);const p=await s.prepare();s.state.unknown=true;assert.equal((await s.execute(p)).structuredContent.status,'unknownOutcome');
 let r=(await s.tool(0,'get_cron_command',{actionId:p.actionId})).structuredContent;assert.equal(r.outcomeUnknown,true);assert.equal(r.observedProgress.dayProgressObserved,true);assert.equal(r.observedProgress.confirmsRequest,false);
 await s.f.db.prepare('DELETE FROM integration_projection_attempts').run();r=(await s.tool(0,'get_cron_command',{actionId:p.actionId})).structuredContent;assert.equal(r.outcomeUnknown,true);assert.equal(r.failure,'attempt_not_found');
 await s.f.workspaceDb.prepare('UPDATE workspaces SET executor_enabled=1 WHERE profile_id=?').bind(s.profiles[0].profileId).run();assert.equal(await executeProfile(await s.f.mf.getBindings('workspace'),s.profiles[0].profileId),'cron_fenced');
 // A separately prepared intent cannot bypass the retained Workspace fence.
 s.state.needsCron=true;s.state.lastCron=p.preview.lastCron;const next=await s.prepare();assert.equal((await s.execute(next)).isError,true);
 assert.equal((await s.http(0,'/party/consent',{consentVersion:'party-planning-v1',expectedPartyId:party})).status,200);
 const spell=await s.tool(0,'prepare_spell_cast',{requestId:crypto.randomUUID(),spellId:'healAll',count:1});assert.notEqual(spell.isError,true,JSON.stringify(spell));assert.equal((await s.tool(0,'execute_spell_cast',{actionId:spell.structuredContent.actionId,executeToken:spell.structuredContent.executeToken})).isError,true);assert.equal(s.state.posts,1);
});

test('shared task lease and uncertainty prevent Cron acceptance while preserving saved task attempts',async t=>{
 const s=await setup(t),p=await s.prepare(),profile=s.profiles[0].profileId;
 await s.f.workspaceDb.prepare("INSERT INTO workspace_runtime(profile_id,access_epoch,lease_token,lease_until_utc,prepared_request_json) SELECT profile_id,access_epoch,'task-owner',?,'original-task-attempt' FROM workspaces WHERE profile_id=?").bind(new Date(Date.now()+60000).toISOString(),profile).run();assert.equal((await s.execute(p)).isError,true);
 assert.equal((await s.f.workspaceDb.prepare('SELECT prepared_request_json FROM workspace_runtime WHERE profile_id=?').bind(profile).first()).prepared_request_json,'original-task-attempt');
 await s.f.workspaceDb.prepare('UPDATE workspace_runtime SET lease_until_utc=NULL WHERE profile_id=?').bind(profile).run();const w=await(await s.http(0,'/workspace',undefined,'GET')).json();
 assert.equal((await s.http(0,'/commands',{id:crypto.randomUUID(),workspaceId:w.document.id,expectedRevision:1,operation:'createTodo',taskId:crypto.randomUUID(),patch:{text:'Synthetic pending'},projectToHabitica:true})).status,201);
 await s.f.workspaceDb.prepare("UPDATE workspace_commands SET entry_json=json_set(entry_json,'$.projectionState','unknownOutcome') WHERE profile_id=?").bind(profile).run();assert.equal((await s.execute(p)).isError,true);assert.equal(s.state.posts,0);
});

test('concurrent identical and different Cron actions share one owner and one dispatch',async t=>{
 const s=await setup(t),p=await s.prepare(),other=await s.prepare(),entered=gate(),resume=gate();s.state.groupHook=async()=>{entered.release();await resume.promise;};
 const first=s.execute(p);await entered.promise;
 try{assert.equal((await s.execute(p)).structuredContent.status,'dispatching');assert.equal((await s.execute(other)).isError,true);assert.equal((await s.tool(0,'get_cron_command',{actionId:p.actionId})).structuredContent.outcomeUnknown,true);}finally{resume.release();}
 assert.equal((await first).structuredContent.status,'confirmed');assert.equal(s.state.posts,1);
});

test('Workspace epoch invalidates previews and independent grants and field whitelists protect execution',async t=>{
 const s=await setup(t),p=await s.prepare();
 for(const args of [{requestId:crypto.randomUUID(),profileId:s.profiles[1].profileId},{requestId:crypto.randomUUID(),day:'2000-01-01'}])assert.equal((await s.tool(0,'prepare_start_new_day',args)).isError,true);
 await s.client(0,'profile:read account:read tasks:read party:read');await assert.rejects(()=>s.execute(p),/403|scope/i);await s.client(0,scope);
 assert.equal((await s.http(0,'/disconnect',{})).status,200);const connected=await s.http(0,'/connect',{consentVersion:'workspace-v1'},'POST',true);s.sessions[0]=(await connected.json()).sessionToken;assert.equal((await s.execute(p)).isError,true);assert.equal(s.state.posts,0);
 const w=await(await s.http(0,'/workspace',undefined,'GET')).json();assert.equal((await s.http(0,'/workspace',{confirmWorkspaceId:w.document.id},'DELETE')).status,200);assert.equal((await s.f.workspaceDb.prepare('SELECT COUNT(*) AS count FROM prepared_cron_actions').first()).count,0);
});

test('own-account Cron works without shared planning consent or a party',async t=>{
 const s=await setup(t,{party:false});const p=await s.prepare();assert.equal(p.preview.party,null);assert.equal((await s.execute(p)).structuredContent.confirmed,true);assert.equal(s.state.posts,1);
});

test('disabled Cron and fabricated action IDs cannot cause effects',async t=>{
 const s=await setup(t,{enabled:false});assert.equal((await s.tool(0,'get_cron_status')).isError,true);assert.equal((await s.tool(0,'prepare_start_new_day',{requestId:crypto.randomUUID()})).isError,true);assert.equal((await s.tool(0,'get_cron_command',{actionId:crypto.randomUUID()})).structuredContent.outcomeUnknown,true);assert.equal(s.state.posts,0);
});

test('failed durable dispatch reservation stops before Cron; failed acknowledgement persistence recovers by lookup only',async t=>{
 const s=await setup(t);const p=await s.prepare();
 await s.f.workspaceDb.prepare("CREATE TRIGGER fail_cron_dispatch BEFORE UPDATE ON prepared_cron_actions WHEN NEW.status='dispatching' BEGIN SELECT RAISE(ABORT,'synthetic dispatch storage failure'); END").run();
 assert.equal((await s.execute(p)).structuredContent.status,'stopped');assert.equal(s.state.posts,0);await s.f.workspaceDb.prepare('DROP TRIGGER fail_cron_dispatch').run();
 const next=await s.prepare();await s.f.workspaceDb.prepare("CREATE TRIGGER fail_cron_receipt BEFORE UPDATE ON prepared_cron_actions WHEN NEW.status='confirmed' BEGIN SELECT RAISE(ABORT,'synthetic receipt storage failure'); END").run();
 const uncertain=(await s.execute(next)).structuredContent;assert.equal(uncertain.status,'dispatching');assert.equal(uncertain.outcomeUnknown,true);assert.equal(s.state.posts,1);
 await s.f.workspaceDb.prepare('DROP TRIGGER fail_cron_receipt').run();assert.equal((await s.tool(0,'get_cron_command',{actionId:next.actionId})).structuredContent.status,'confirmed');assert.equal(s.state.posts,1);
});

test('accepted actions without a saved dispatch are stopped on recovery rather than sent later',async t=>{
 const s=await setup(t),p=await s.prepare();await s.f.workspaceDb.prepare("UPDATE prepared_cron_actions SET status='accepted',consumed_at_utc=?,token_hash=NULL WHERE action_id=?").bind(new Date().toISOString(),p.actionId).run();
 const r=(await s.tool(0,'get_cron_command',{actionId:p.actionId})).structuredContent;assert.equal(r.status,'stopped');assert.equal(r.failure,'execution_stopped');assert.equal(r.outcomeUnknown,false);assert.equal(s.state.posts,0);
});
