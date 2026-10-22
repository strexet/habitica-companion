import test from 'node:test';import assert from 'node:assert/strict';
import {setupGroupTags} from './fixtures/group-tags.mjs';
import {journal} from '../src/journal.ts';
test('fresh exact group review queues existing membership without fake task or organization edits and preserves original acceptance',async t=>{
 const x=await setupGroupTags(t);await x.consent();const requestId=crypto.randomUUID(),a=await x.prepare('reconcile',{requestId});assert.equal(a.canAccept,true);assert.equal(a.details.taskChanges.length,1);assert.equal(a.details.taskChanges[0].habiticaTaskId,x.task.id);assert.equal((await x.prepare('reconcile',{requestId})).commandId,a.commandId);
 const accepted=await x.accept(a);assert.equal(accepted.gameEffects,0);assert.equal(accepted.savedInCompanion,true);assert.equal(accepted.outcomeUnknown,false);assert.deepEqual(await x.accept(a),accepted);let exported=await x.exported();assert.equal(exported.document.revision,2);assert.equal(exported.commands.length,0);assert.equal(exported.document.organizationHistory?.length??0,0);await journal(exported);
 await x.enable(true);assert.equal(await x.wake(),'tag_applied');assert.equal(await x.wake(),'tag_applied');assert.equal(await x.wake(),'idle');const status=await x.api('get_group_tag_status');assert.equal(status.integrationComplete,true);assert.equal(status.groupCoverage.confirmed,1);assert.deepEqual(x.task.tags,[x.external,x.tags[0].id]);assert.equal(x.writes,2);
 exported=await x.exported();assert.equal(exported.document.revision,2);await journal(exported);
});
test('duplicate native names require exact adoption and never grant unreviewed task membership or native mutation',async t=>{
 const x=await setupGroupTags(t);await x.consent();const a={id:crypto.randomUUID(),name:'Shared name'},b={id:crypto.randomUUID(),name:'Shared name'};x.tags.push(a,b);
 const candidates=await x.api('inspect_group_tag_candidates',{kind:'project',entityId:x.projectId,limit:1});assert.equal(candidates.matchedCount,2);assert.equal(candidates.candidates[0].id,a.id);assert.equal(candidates.nextOffset,1);
 const next=await x.api('inspect_group_tag_candidates',{kind:'project',entityId:x.projectId,offset:1,expectedFingerprint:candidates.fingerprint});assert.equal(next.candidates[0].id,b.id);
 const preview=await x.prepare('adopt',{habiticaTagId:b.id});assert.equal(preview.details.observedTag.id,b.id);const accepted=await x.accept(preview);assert.equal(accepted.outcomeUnknown,false);assert.equal(accepted.original.state,'confirmed');assert.equal(x.writes,0);assert.deepEqual(x.task.tags,[x.external]);
 const original=await x.api('get_group_tag_command',{commandId:preview.commandId});assert.equal(original.original.state,'confirmed');assert.equal((await x.accept(preview)).outcomeUnknown,false);assert.equal(x.writes,0);const mapping=(await x.exported()).document.tagLedger.mappings[0];assert.equal(mapping.habiticaTagId,b.id);
 const repair=await x.prepare();await x.accept(repair);await x.enable(true);assert.equal(await x.wake(),'tag_applied');assert.deepEqual(x.task.tags,[x.external,b.id]);assert.equal(x.writes,1);
});
test('paused original receipt lookup and approved observed recovery never resend an unknown native create',async t=>{
 const x=await setupGroupTags(t,{unknownCreate:true});await x.consent();await x.accept(await x.prepare());await x.enable(true);assert.equal(await x.wake(),'tag_unknown');await x.enable(false);
 const work=await x.f.workspaceDb.prepare("SELECT work_id FROM workspace_tag_work WHERE state='unknownOutcome'").first();assert.ok(work);let original=await x.api('recover_group_tag_receipt',{workId:work.work_id});assert.equal(original.outcomeUnknown,true);assert.equal(x.writes,1);
 const review=await x.prepare('recover',{workId:work.work_id});assert.equal(review.details.observedTag.id,x.tags[0].id);assert.match(review.details.disclosure,/original send remains uncertain/i);const accepted=await x.accept(review);assert.equal(accepted.outcomeUnknown,false);assert.equal(accepted.original.state,'confirmed');assert.equal(x.writes,1);assert.equal((await x.accept(review)).outcomeUnknown,false);assert.equal(x.writes,1);
 const receipt=await x.f.db.prepare("SELECT outcome_json FROM integration_projection_attempts WHERE operation='tags.createTag'").first();assert.equal(JSON.parse(receipt.outcome_json).code,'reviewed_observation');original=await x.api('recover_group_tag_receipt',{workId:work.work_id});assert.equal(original.outcomeUnknown,false);assert.equal((await x.exported()).document.revision,2);
});
test('old MCP permissions retain readable incomplete status while both HTTP and private boundaries reject ungranted tag writes',async t=>{
 const x=await setupGroupTags(t);await x.consent();const binding=(await x.f.mf.getBindings('mcp')).WORKSPACE_SERVER,old=await x.token('profile:read tasks:read workspace.organization.write'),invoke=(token,tool,args)=>binding.fetch('https://workspace.internal/tool',{method:'POST',headers:{authorization:'Bearer '+token,'content-type':'application/json'},body:JSON.stringify({tool,arguments:args})});
 const status=await invoke(old,'get_group_tag_status',{});assert.equal(status.status,200);assert.equal((await status.json()).tagScopeAllowed,false);
 const args={requestId:crypto.randomUUID(),operation:'reconcile',kind:'project',entityId:x.projectId};assert.equal((await invoke(old,'prepare_group_tag_command',args)).status,403);
 const fresh=await x.token('profile:read tasks:read habitica.tags.write'),response=await invoke(fresh,'prepare_group_tag_command',args);assert.equal(response.status,200,await response.clone().text());const preview=await response.json();assert.equal(preview.canAccept,true);assert.ok([401,403].includes((await invoke(old,'accept_group_tag_command',{commandId:preview.commandId})).status));assert.equal(x.writes,0);
});
test('stale, expired, consent-changed and role-confused reviews never partially accept',async t=>{
 const x=await setupGroupTags(t);await x.consent();const a=await x.prepare();await x.f.workspaceDb.prepare('UPDATE workspace_tag_reviews SET expires_at_utc=? WHERE command_id=?').bind('2000-01-01T00:00:00Z',a.commandId).run();assert.equal((await x.response('accept_group_tag_command',{commandId:a.commandId})).status,409);
 const b=await x.prepare();await x.consent();assert.equal((await x.response('accept_group_tag_command',{commandId:b.commandId})).status,409);const c=await x.prepare();await x.accept(await x.prepare());assert.equal((await x.response('accept_group_tag_command',{commandId:c.commandId})).status,409);assert.equal(x.writes,0);
 assert.equal((await x.response('accept_group_tag_command',{commandId:crypto.randomUUID()})).status,409);assert.equal((await x.response('prepare_group_tag_command',{requestId:crypto.randomUUID(),operation:'adopt',kind:'party',entityId:x.projectId,habiticaTagId:crypto.randomUUID()})).status,422);
});

test('prepared acceptance remains uncertain, expired reviews release storage without deleting original receipts',async t=>{
 const {purgeExpiredTagReviews}=await import('../src/group-tag-retention.ts');
 const x=await setupGroupTags(t);await x.consent();const pending=await x.prepare();
 const unknown=await x.api('get_group_tag_command',{commandId:pending.commandId});assert.equal(unknown.savedInCompanion,null);assert.equal(unknown.outcomeUnknown,true);assert.equal(unknown.prepared,true);
 const saved=await x.accept(await x.prepare());const before=await x.f.workspaceDb.prepare('SELECT archive_bytes FROM workspaces').first();
 await x.f.workspaceDb.prepare('UPDATE workspace_tag_reviews SET expires_at_utc=?').bind('2000-01-01T00:00:00Z').run();await purgeExpiredTagReviews(x.env);
 const after=await x.f.workspaceDb.prepare('SELECT archive_bytes FROM workspaces').first();assert.ok(after.archive_bytes<before.archive_bytes);
 assert.equal(await x.f.workspaceDb.prepare('SELECT 1 FROM workspace_tag_reviews WHERE command_id=?').bind(pending.commandId).first(),null);
 assert.equal((await x.api('get_group_tag_command',{commandId:saved.commandId})).savedInCompanion,true);assert.equal(x.writes,0);
});
test('failed atomic acceptance cannot store partial canonical tag intent',async t=>{
 const x=await setupGroupTags(t);await x.consent();const preview=await x.prepare(),before=await x.exported();
 await x.f.workspaceDb.exec("CREATE TRIGGER fail_tag_review_accept BEFORE UPDATE OF receipt_json ON workspace_tag_reviews WHEN NEW.receipt_json IS NOT NULL BEGIN SELECT RAISE(ABORT, 'synthetic_storage_failure'); END");
 assert.equal((await x.response('accept_group_tag_command',{commandId:preview.commandId})).status,503);
 assert.deepEqual((await x.exported()).document,before.document);assert.equal((await x.f.workspaceDb.prepare('SELECT count(*) n FROM workspace_tag_work').first()).n,0);assert.equal(x.writes,0);
 await x.f.workspaceDb.exec('DROP TRIGGER fail_tag_review_accept');assert.equal((await x.accept(preview)).savedInCompanion,true);
});
