import {test} from 'node:test';
import assert from 'node:assert/strict';
import {executeProfile} from '../src/executor.ts';
import {setup} from './fixtures/spells.mjs';
const origin='http://localhost:8789',party='90000000-0000-4000-8000-000000000001';

test('reviewed previews rotate only tokens, execute sequentially and export no secrets',async t=>{
 const s=await setup(t);const requestId=crypto.randomUUID();const p=await s.prepare(3,requestId);const duplicate=await s.prepare(3,requestId);
 assert.equal(p.actionId,duplicate.actionId);assert.notEqual(p.executeToken,duplicate.executeToken);assert.equal(p.preview.totalMana,75);assert.equal(p.preview.repetitions,3);assert.ok(p.preview.warnings.some(w=>w.includes('chat')));assert.ok(Date.parse(p.expiresAtUtc)<=Date.now()+120000);
 assert.equal((await s.execute(p)).isError,true);assert.equal(s.state.posts,0);
 const result=await s.execute(duplicate);assert.notEqual(result.isError,true);const receipt=result.structuredContent;
 assert.equal(receipt.status,'confirmed');assert.equal(receipt.requestedCount,3);assert.equal(receipt.attemptedCount,3);assert.equal(receipt.confirmedCount,3);assert.equal(receipt.mana,25);assert.equal(receipt.refreshSucceeded,true);
 assert.deepEqual((await s.execute(duplicate)).structuredContent,receipt);assert.deepEqual((await s.tool(0,'get_spell_command',{actionId:p.actionId})).structuredContent,receipt);assert.equal(s.state.posts,3);
 assert.equal((await s.tool(1,'get_spell_command',{actionId:p.actionId})).structuredContent.accepted,null);
 const exported=await s.http(0,'/spells/history',undefined,'GET',true);assert.equal(exported.status,200);const text=await exported.text();
 for(const secret of [p.executeToken,duplicate.executeToken,'token_hash','payload_json','attempt_json','fixture-a-token','never-export','fixture-healer'])assert.ok(!text.includes(secret),secret);
 assert.equal(JSON.parse(text).spells.length,1);
 const publicRoute=await s.f.workspace.fetch(origin+'/spell-api',{method:'POST'});assert.notEqual(publicRoute.status,200);
});

test('fresh changes and short expiry reject effects without silently rebuilding a plan',async t=>{
 const s=await setup(t);const p=await s.prepare();s.stats[0].mp--;
 const result=(await s.execute(p)).structuredContent;assert.equal(result.status,'stopped');assert.equal(result.failure,'spell_precondition_changed');assert.equal(result.confirmedCount,0);assert.equal(s.state.posts,0);
 assert.equal((await s.execute(p)).structuredContent.status,'stopped');
 const next=await s.prepare();await s.f.workspaceDb.prepare('UPDATE prepared_spell_actions SET expires_at_utc=? WHERE action_id=?').bind('2000-01-01T00:00:00Z',next.actionId).run();
 assert.equal((await s.execute(next)).isError,true);assert.equal(s.state.posts,0);
 assert.equal((await s.tool(0,'prepare_spell_cast',{requestId:p.requestId,spellId:'healAll',count:2})).isError,true);
});

test('first refusal stops repetitions with explicit partial counts and no repeated effects',async t=>{
 const s=await setup(t);const p=await s.prepare(3);s.state.failAt=2;
 const receipt=(await s.execute(p)).structuredContent;assert.equal(receipt.status,'stopped');assert.equal(receipt.confirmedCount,1);assert.equal(receipt.attemptedCount,2);assert.equal(receipt.failurePosition,2);assert.equal(receipt.outcomeUnknown,false);assert.equal(receipt.refreshSucceeded,true);
 assert.deepEqual((await s.execute(p)).structuredContent,receipt);assert.equal(s.state.posts,2);
});

test('fresh preconditions are checked between every single cast',async t=>{
 const s=await setup(t);const p=await s.prepare(3);s.state.changeAfterFirst=true;
 const result=(await s.execute(p)).structuredContent;assert.equal(result.status,'stopped');assert.equal(result.failure,'spell_precondition_changed');assert.equal(result.confirmedCount,1);assert.equal(result.failurePosition,2);assert.equal(s.state.posts,1);
});

test('lost cast acknowledgement recovers only its receipt and stops the unexecuted remainder',async t=>{
 const s=await setup(t);const p=await s.prepare(3);s.state.lost=true;
 const uncertain=(await s.execute(p)).structuredContent;assert.equal(uncertain.status,'unknownOutcome');assert.equal(s.state.posts,1);
 const recovered=(await s.tool(0,'get_spell_command',{actionId:p.actionId})).structuredContent;assert.equal(recovered.status,'stopped');assert.equal(recovered.confirmedCount,1);assert.equal(recovered.failure,'interrupted_remainder');assert.equal(recovered.refreshSucceeded,false);assert.equal(s.state.posts,1);
 assert.deepEqual((await s.execute(p)).structuredContent,recovered);assert.equal(s.state.posts,1);
});

test('retained unknown spells fence new spells and the task executor after Auth retention',async t=>{
 const s=await setup(t);const p=await s.prepare();s.state.failAt=1;s.state.unknown=true;
 assert.equal((await s.execute(p)).structuredContent.status,'unknownOutcome');
 await s.f.db.prepare('DELETE FROM integration_projection_attempts').run();
 const next=await s.prepare();assert.equal((await s.execute(next)).isError,true);assert.equal(s.state.posts,1);
 await s.f.workspaceDb.prepare('UPDATE workspaces SET executor_enabled=1 WHERE profile_id=?').bind(s.profiles[0].profileId).run();
 assert.equal(await executeProfile(await s.f.mf.getBindings('workspace'),s.profiles[0].profileId),'spell_fenced');
 const stillUnknown=(await s.tool(0,'get_spell_command',{actionId:p.actionId})).structuredContent;assert.equal(stillUnknown.outcomeUnknown,true);assert.equal(stillUnknown.confirmedCount,0);assert.equal(s.state.posts,1);
});

test('task uncertainty and the shared runtime lease prevent spell acceptance',async t=>{
 const s=await setup(t);const p=await s.prepare();const profile=s.profiles[0].profileId;
 await s.f.workspaceDb.prepare("INSERT INTO workspace_runtime(profile_id,access_epoch,lease_token,lease_until_utc,prepared_request_json) SELECT profile_id,access_epoch,'fixture-task-lease',?,'task-attempt-preserved' FROM workspaces WHERE profile_id=?").bind(new Date(Date.now()+60000).toISOString(),profile).run();
 assert.equal((await s.execute(p)).isError,true);assert.equal((await s.f.workspaceDb.prepare('SELECT prepared_request_json FROM workspace_runtime WHERE profile_id=?').bind(profile).first()).prepared_request_json,'task-attempt-preserved');
 await s.f.workspaceDb.prepare('UPDATE workspace_runtime SET lease_until_utc=NULL WHERE profile_id=?').bind(profile).run();
 const w=await s.f.workspaceDb.prepare('SELECT workspace_id FROM workspaces WHERE profile_id=?').bind(profile).first();
 await s.http(0,'/commands',{id:crypto.randomUUID(),workspaceId:w.workspace_id,expectedRevision:1,operation:'createTodo',taskId:crypto.randomUUID(),patch:{text:'Synthetic pending'},projectToHabitica:true});
 await s.f.workspaceDb.prepare("UPDATE workspace_commands SET entry_json=json_set(entry_json,'$.projectionState','unknownOutcome') WHERE profile_id=?").bind(profile).run();
 assert.equal((await s.execute(p)).isError,true);assert.equal(s.state.posts,0);
});

test('planning opt-out and namespace deletion revoke unconsumed previews',async t=>{
 const s=await setup(t);const p=await s.prepare();assert.equal((await s.http(0,'/party/contributions',{confirm:'remove-my-party-contributions'},'POST',true)).status,200);
 await s.consent(0);assert.equal((await s.execute(p)).isError,true);
 const next=await s.prepare();const response=await s.http(0,'/party/state',undefined,'GET');const state=await response.json();
 assert.equal((await s.http(0,'/party/planning',{expectedNamespaceId:state.namespaceId,expectedRevision:state.revision,confirmNamespaceId:state.namespaceId},'DELETE')).status,200);
 await s.consent(0);assert.equal((await s.execute(next)).isError,true);assert.equal(s.state.posts,0);
});

test('Workspace epoch change revokes previews; deletion clears private spell history',async t=>{
 const s=await setup(t);const p=await s.prepare();assert.equal((await s.http(0,'/disconnect',{})).status,200);
 assert.equal((await s.http(0,'/spells/history',undefined,'GET',true)).status,200);
 const connected=await s.http(0,'/connect',{consentVersion:'workspace-v1'},'POST',true);s.sessions[0]=(await connected.json()).sessionToken;await s.consent(0);
 assert.equal((await s.execute(p)).isError,true);assert.equal(s.state.posts,0);
 const w=await(await s.http(0,'/workspace',undefined,'GET')).json();assert.equal((await s.http(0,'/workspace',{confirmWorkspaceId:w.document.id},'DELETE')).status,200);
 assert.equal((await s.f.workspaceDb.prepare('SELECT COUNT(*) AS count FROM prepared_spell_actions WHERE profile_id=?').bind(s.profiles[0].profileId).first()).count,0);
});

test('scope boundaries, field whitelist, consent and disabled flags fail closed',async t=>{
 const s=await setup(t,{consent:false});assert.equal((await s.tool(0,'get_spell_context')).structuredContent.spell.id,'healAll');assert.equal((await s.tool(0,'prepare_spell_cast',{requestId:crypto.randomUUID(),spellId:'healAll',count:1})).isError,true);
 await s.consent(0);const p=await s.prepare();assert.equal((await s.execute(p,1)).isError,true);
 for(const args of [{requestId:crypto.randomUUID(),spellId:'healAll',count:4},{requestId:crypto.randomUUID(),spellId:'heal',count:1},{requestId:crypto.randomUUID(),spellId:'healAll',count:1,partyId:party}])assert.equal((await s.tool(0,'prepare_spell_cast',args)).isError,true);
 await s.client(0,'profile:read account:read party:read');await assert.rejects(()=>s.execute(p),/403|scope/i);assert.equal(s.state.posts,0);
 const privateWorkspace=(await s.f.mf.getBindings('mcp')).WORKSPACE_SERVER;
 const denied=await privateWorkspace.fetch('https://workspace.internal/tool',{method:'POST',headers:{authorization:'Bearer '+s.tokens[0],'content-type':'application/json'},body:JSON.stringify({tool:'execute_spell_cast',arguments:{actionId:p.actionId,executeToken:p.executeToken}})});
 assert.equal(denied.status,403);assert.equal(s.state.posts,0);
});

test('disabled spell configuration exposes no execution path',async t=>{
 const s=await setup(t,{enabled:false});assert.equal((await s.tool(0,'get_spell_context')).isError,true);
 assert.equal((await s.tool(0,'prepare_spell_cast',{requestId:crypto.randomUUID(),spellId:'healAll',count:1})).isError,true);assert.equal(s.state.posts,0);
});

test('concurrent execution of one accepted action dispatches only one cast',async t=>{
 const s=await setup(t);const p=await s.prepare();let enter,release;
 const entered=new Promise(r=>enter=r),resume=new Promise(r=>release=r);s.state.beforeGroup=async()=>{enter();await resume;};
 const pending=s.execute(p);await Promise.race([entered,pending.then(()=>assert.fail('preflight gate not reached'))]);
 const duplicate=await s.execute(p);assert.equal(duplicate.structuredContent.accepted,true);assert.equal(duplicate.structuredContent.status,'dispatching');
 release();assert.equal((await pending).structuredContent.confirmedCount,1);assert.equal(s.state.posts,1);
});

test('failed Workspace dispatch persistence stops before calling the private gateway',async t=>{
 const s=await setup(t);const p=await s.prepare();
 await s.f.workspaceDb.prepare("CREATE TRIGGER fixture_spell_dispatch_failure BEFORE UPDATE ON prepared_spell_actions WHEN NEW.status='dispatching' BEGIN SELECT RAISE(ABORT,'fixture failure'); END").run();
 const result=await s.execute(p);assert.equal(result.structuredContent.status,'stopped');assert.equal(result.structuredContent.attemptedCount,0);assert.equal(s.state.posts,0);
});
