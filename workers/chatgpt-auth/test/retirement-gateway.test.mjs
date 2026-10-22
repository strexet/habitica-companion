import test from 'node:test';import assert from 'node:assert/strict';
import {fixture,enrolled,accounts,issuer} from './fixtures/oauth.mjs';
const nativeId='90000000-0000-4000-8000-000000000001';
const nativeTask=(fields={})=>({id:nativeId,userId:accounts[0].habiticaUserId,type:'todo',text:'Synthetic converted checklist',notes:'Retained notes',priority:1,attribute:'int',completed:false,up:true,down:true,checklist:[],group:{},challenge:{},...fields});
async function setup(t,handler){
 const requests=[];const f=await fixture(t,{workspace:true,projection:true,habitica:async request=>{
  if(request.url.includes('user?userFields=_id,profile'))return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:'Synthetic'}}});
  requests.push({method:request.method,url:request.url});assert.equal(new URL(request.url).pathname,'/api/v3/tasks/'+nativeId);return handler(request);
 }});const profile=await enrolled(f),row=await f.db.prepare('SELECT connection_id FROM integration_profiles WHERE profile_id=?').bind(profile.profileId).first();
 await f.auth.fetch(`${issuer}/profiles/${profile.profileId}/tag-consent`,{method:'POST',headers:{authorization:'Bearer '+profile.managementToken,'content-type':'application/json'},body:JSON.stringify({consentVersion:'habitica-tags-v1',enabled:true})});
 const consent=await f.db.prepare('SELECT consent_epoch FROM integration_tag_consents').first();
 const context={profileId:profile.profileId,connectionId:row.connection_id};
 const call=(operation,args={},extra={})=>f.projection.fetch('https://projection.internal/operation',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({...context,operation,arguments:args,automated:false,...(operation==='retireSource'?{expectedConsentEpoch:consent.consent_epoch}:{}),...extra})});
 const approval=observation=>({selector:nativeId,checkToken:observation.checkToken,expectedFingerprint:observation.task.fingerprint,expectedCompleted:observation.task.completed,workspaceId:crypto.randomUUID(),conversionId:crypto.randomUUID(),sourceTaskId:crypto.randomUUID(),contextHash:'a'.repeat(64),irreversibleApproved:true});
 const attempt=()=>({attemptId:crypto.randomUUID(),attemptAtUtc:new Date().toISOString()});return {...f,call,approval,attempt,requests};
}
test('private exact own-Todo retirement confirms one empty-object DELETE receipt and deduplicates original attempts',async t=>{
 const f=await setup(t,request=>request.method==='GET'?Response.json({success:true,data:nativeTask()}):Response.json({success:true,data:{}}));
 const observed=await(await f.call('inspect',{selector:nativeId})).json(),args=f.approval(observed),attempt=f.attempt();
 const first=await(await f.call('retireSource',args,attempt)).json();assert.equal(first.kind,'applied');assert.equal(first.code,'source_retired');assert.equal(first.task.id,nativeId);
 assert.deepEqual(await(await f.call('retireSource',args,attempt)).json(),first);assert.equal(f.requests.filter(row=>row.method==='DELETE').length,1);assert.equal(f.requests.filter(row=>row.method==='GET').length,2);
 assert.equal((await f.call('retireSource',args,f.attempt())).status,409);assert.equal((await f.call('retireSource',{...args,sourceTaskId:crypto.randomUUID()},attempt)).status,409);
 assert.notEqual((await f.auth.fetch(issuer+'/projection',{method:'POST'})).status,200);
 const stored=await f.db.prepare('SELECT * FROM integration_projection_attempts').first();assert.equal(stored.operation,'retireSource');assert.ok(!JSON.stringify(stored).includes('Retained notes'));assert.ok(!JSON.stringify(stored).includes(accounts[0].apiToken));
});
test('retirement requires explicit irreversible approval, manual dispatch and a fresh matching owned Todo',async t=>{
 let current=nativeTask();const f=await setup(t,()=>Response.json({success:true,data:current}));const observed=await(await f.call('inspect',{selector:nativeId})).json(),args=f.approval(observed);
 for(const changed of [{irreversibleApproved:false},{irreversibleApproved:undefined},{contextHash:'bad'},{workspaceId:undefined},{expectedCompleted:undefined}])assert.ok((await f.call('retireSource',{...args,...changed},f.attempt())).status>=400);
 assert.equal((await f.call('retireSource',args,{...f.attempt(),automated:true})).status,409);
 for(const fields of [{notes:'Changed after approval'},{completed:true},{type:'habit'},{userId:accounts[1].habiticaUserId},{group:{id:nativeId}},{challenge:{id:nativeId}}]){
  current=nativeTask(fields);assert.ok((await f.call('retireSource',args,f.attempt())).status>=400);
 }
 assert.equal(f.requests.filter(row=>row.method==='DELETE').length,0);assert.equal((await f.db.prepare('SELECT COUNT(*) AS count FROM integration_projection_attempts').first()).count,0);
});
test('an already-absent source before dispatch is a separate refusal and never generates a delete attempt',async t=>{
 let missing=false;const f=await setup(t,()=>missing?new Response('',{status:404}):Response.json({success:true,data:nativeTask()}));const observed=await(await f.call('inspect',{selector:nativeId})).json();missing=true;
 const response=await f.call('retireSource',f.approval(observed),f.attempt());assert.equal(response.status,409);assert.equal((await response.json()).code,'source_already_absent');assert.equal(f.requests.filter(row=>row.method==='DELETE').length,0);assert.equal((await f.db.prepare('SELECT COUNT(*) AS count FROM integration_projection_attempts').first()).count,0);
});
test('unknown delete never resends and explicit absence reconciliation differs from original success',async t=>{
 let missing=false;const f=await setup(t,request=>request.method==='GET'?(missing?new Response('',{status:404}):Response.json({success:true,data:nativeTask()})):new Response('Private upstream details',{status:500}));
 const observation=await(await f.call('inspect',{selector:nativeId})).json(),args=f.approval(observation),attempt=f.attempt();assert.equal((await(await f.call('retireSource',args,attempt)).json()).kind,'unknown');
 assert.equal((await(await f.call('retireSource',args,attempt)).json()).kind,'unknown');assert.equal((await f.call('retireSource',args,f.attempt())).status,409);
 const original={operation:'retireSource',arguments:args};assert.equal((await(await f.call('lookup',original,attempt)).json()).kind,'unknown');assert.equal((await(await f.call('reconcile',original,attempt)).json()).kind,'unknown');
 missing=true;const observed=await(await f.call('reconcile',original,attempt)).json();assert.equal(observed.kind,'applied');assert.equal(observed.code,'retirement_absence_observed');assert.notEqual(observed.code,'source_retired');assert.deepEqual(await(await f.call('lookup',original,attempt)).json(),observed);assert.equal(f.requests.filter(row=>row.method==='DELETE').length,1);
});
test('definitive native refusal records a refusal without claiming source retirement',async t=>{
 const f=await setup(t,request=>request.method==='GET'?Response.json({success:true,data:nativeTask()}):new Response('Private refusal',{status:400}));const observation=await(await f.call('inspect',{selector:nativeId})).json(),args=f.approval(observation),attempt=f.attempt();
 const refused=await(await f.call('retireSource',args,attempt)).json();assert.equal(refused.kind,'refused');assert.equal(refused.code,'habitica_refused');assert.deepEqual(await(await f.call('retireSource',args,attempt)).json(),refused);assert.equal(f.requests.filter(row=>row.method==='DELETE').length,1);
});
test('failed durable reservation sends no DELETE and leaves the source untouched',async t=>{
 const f=await setup(t,()=>Response.json({success:true,data:nativeTask()}));const observation=await(await f.call('inspect',{selector:nativeId})).json();
 await f.db.prepare("CREATE TRIGGER retirement_dispatch_failure BEFORE INSERT ON integration_projection_attempts BEGIN SELECT RAISE(ABORT,'fixture reservation failure'); END").run();
 const response=await f.call('retireSource',f.approval(observation),f.attempt());assert.equal(response.status,503);assert.equal(f.requests.filter(row=>row.method==='DELETE').length,0);assert.equal((await f.db.prepare('SELECT COUNT(*) AS count FROM integration_projection_attempts').first()).count,0);
});
test('lost storage acknowledgement after DELETE keeps the original unknown fence without resend',async t=>{
 const f=await setup(t,request=>Response.json({success:true,data:request.method==='GET'?nativeTask():{}}));const observation=await(await f.call('inspect',{selector:nativeId})).json(),args=f.approval(observation),attempt=f.attempt();
 await f.db.prepare("CREATE TRIGGER retirement_ack_failure BEFORE UPDATE ON integration_projection_attempts WHEN NEW.state='applied' BEGIN SELECT RAISE(ABORT,'fixture receipt failure'); END").run();
 assert.equal((await(await f.call('retireSource',args,attempt)).json()).kind,'unknown');assert.equal((await(await f.call('retireSource',args,attempt)).json()).kind,'unknown');assert.equal(f.requests.filter(row=>row.method==='DELETE').length,1);assert.equal((await f.db.prepare('SELECT state FROM integration_projection_attempts').first()).state,'unknown');
});
test('a quota exhausted by final verification prevents reservation and deletion',async t=>{
 let reads=0;const f=await setup(t,()=>Response.json({success:true,data:nativeTask()},{headers:++reads>1?{'x-ratelimit-remaining':'0','retry-after':'60'}:{}}));const observation=await(await f.call('inspect',{selector:nativeId})).json();
 const response=await f.call('retireSource',f.approval(observation),f.attempt());assert.equal(response.status,429);assert.equal((await response.json()).kind,'refused');assert.equal(f.requests.filter(row=>row.method==='DELETE').length,0);assert.equal((await f.db.prepare('SELECT COUNT(*) AS count FROM integration_projection_attempts').first()).count,0);assert.equal((await f.db.prepare('SELECT COUNT(*) AS count FROM integration_projection_checks').first()).count,1);
});

test('revoked consent during final native observation prevents reservation and DELETE',async t=>{
 let f,reads=0;f=await setup(t,async()=>{if(++reads>1)await f.db.prepare("UPDATE integration_tag_consents SET revoked_at_utc=?").bind(new Date().toISOString()).run();return Response.json({success:true,data:nativeTask()});});const observed=await(await f.call('inspect',{selector:nativeId})).json();
 const result=await f.call('retireSource',f.approval(observed),f.attempt());assert.equal(result.status,403);assert.equal(f.requests.filter(row=>row.method==='DELETE').length,0);assert.equal((await f.db.prepare('SELECT COUNT(*) AS count FROM integration_projection_attempts').first()).count,0);
});
