import {test} from 'node:test';import assert from 'node:assert/strict';
import {linkedFixture,call} from './fixtures/linked-recovery.mjs';
import {executeProfile} from '../src/executor.ts';
import {accounts} from '../../chatgpt-auth/test/fixtures/oauth.mjs';
for(const control of ['canonical','expiry','disabled','grant','unknown-source','foreign','shared','challenge','needs-cron','prior-day'])test('linked pre-dispatch control '+control+' refuses recovery',async t=>{
 const x=await linkedFixture(t,control==='prior-day'?'daily':'todo'),review=await x.prepare();
 if(control==='canonical'){
  const document=structuredClone(x.before.document);document.revision++;document.tasks[0].revision=document.revision;document.tasks[0].notes='Changed locally';
  await x.f.workspaceDb.prepare('UPDATE workspaces SET document_json=?,store_version=store_version+1').bind(JSON.stringify(document)).run();
 }else if(control==='expiry')await x.f.workspaceDb.prepare("UPDATE workspace_linked_recoveries SET expires_at_utc='2000-01-01T00:00:00Z'").run();
 else if(control==='disabled')await call(x.f,x.w,'/executor',{enabled:false});
 else if(control==='grant')await x.f.db.prepare("UPDATE integration_profiles SET grant_epoch=CAST(grant_epoch AS INTEGER)+1").run();
 else if(control==='unknown-source')await x.f.db.prepare("UPDATE integration_projection_attempts SET state='unknown'").run();
 else if(control==='foreign')x.state.task.userId=accounts[1].habiticaUserId;
 else if(control==='shared')x.state.task.group={id:crypto.randomUUID()};
 else if(control==='challenge')x.state.task.challenge={taskId:crypto.randomUUID()};
 else if(control==='needs-cron')x.state.user.needsCron=true;
 else x.state.user.preferences.timezoneOffset=720;
 const result=await x.accept(review);assert.ok(result.status!==200||result.body.state==='refused',JSON.stringify(result));assert.equal(x.state.down,0);
 if(control!=='canonical')assert.deepEqual((await x.exported()).document,x.before.document);
});
test('two concurrent accept requests and a competing executor cannot duplicate down or native recovery',async t=>{
 const x=await linkedFixture(t),review=await x.prepare();let enter,release;const entered=new Promise(resolve=>enter=resolve),gate=new Promise(resolve=>release=resolve);let once=true;
 x.state.hook=async()=>{if(once){once=false;enter();await gate;}};
 const first=x.accept(review);await entered;
 const duplicate=await x.accept(review);assert.ok(duplicate.status===409||duplicate.body.outcomeUnknown===true,JSON.stringify(duplicate));assert.equal(await executeProfile(x.env,x.p.profileId,{wait:x.wait}),'linked_recovery_pending');
 release();assert.equal((await first).body.state,'confirmed');assert.equal(x.state.down,1);assert.equal((await x.exported()).document.restorations.length,1);
});
