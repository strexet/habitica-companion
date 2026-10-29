import {test} from 'node:test';
import assert from 'node:assert/strict';
import {fixture,enrolled,issuer} from '../../chatgpt-auth/test/fixtures/oauth.mjs';
import {executeProfile,executeScheduled} from '../src/executor.ts';
import {journal} from '../src/journal.ts';
const origin='http://localhost:8789';
async function call(f,path,token,body){return f.workspace.fetch(origin+path,{method:body===undefined?'GET':'POST',headers:{authorization:`Bearer ${token}`,'content-type':'application/json'},body:body===undefined?undefined:JSON.stringify(body)});}
async function setup(t,options={}){
 let remote=null,writes=0,reads=0;const observed=[];
 const f=await fixture(t,{workspace:true,projection:true,executor:true,...options,habitica:async request=>{
  if(request.url.includes('user?userFields=_id,profile'))return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:'Synthetic'}}});
  observed.push({method:request.method,at:Date.now()});
  if(options.habitica)return options.habitica(request);
  if(request.method==='GET'){reads++;if(options.onRead)await options.onRead();return remote?Response.json({success:true,data:remote}):new Response('',{status:404});}
  writes++;remote={id:'90000000-0000-4000-8000-000000000022',userId:request.headers.get('x-api-user'),notes:'',priority:1,attribute:'str',completed:false,checklist:[],group:{},challenge:{},...await request.json()};return Response.json({success:true,data:remote},{status:201});
 }});
 const p=await enrolled(f);const connection=await call(f,'/connect',p.managementToken,{consentVersion:'workspace-v1'});assert.equal(connection.status,201);const w=await connection.json();
 const c={id:crypto.randomUUID(),workspaceId:w.workspaceId,expectedRevision:1,operation:'createTodo',taskId:crypto.randomUUID(),patch:{text:'Owned synthetic task'},projectToHabitica:true};
 assert.equal((await call(f,'/commands',w.sessionToken,c)).status,201);
 const env=await f.mf.getBindings('workspace');const delays=[];
 const wait=async ms=>{delays.push(ms);await f.workspaceDb.prepare("UPDATE workspace_runtime SET next_network_at_utc='2000-01-01T00:00:00Z'").run();await f.db.prepare("UPDATE integration_projection_state SET next_automated_at_utc='2000-01-01T00:00:00Z'").run();if(options.onWait)await options.onWait();};
 const enable=()=>call(f,'/executor',w.sessionToken,{enabled:true,consentVersion:'executor-v1'});
 const exported=async()=>{const r=await call(f,'/export',w.sessionToken);assert.equal(r.status,200);return r.json();};
 return {f,p,w,c,env,wait,delays,enable,exported,get writes(){return writes;},get reads(){return reads;},observed};
}
test('executor opt-in is separate; one durable owner handles duplicate wakeups with persisted pacing',async t=>{
 let enter,release;const entered=new Promise(r=>enter=r);const gate=new Promise(r=>release=r);let held=true;
 const x=await setup(t,{onRead:async()=>{if(held){held=false;enter();await gate;}}});
 assert.equal(await executeProfile(x.env,x.p.profileId,{wait:x.wait}),'disabled');assert.equal(x.reads,0);
 assert.equal((await call(x.f,'/executor',x.w.sessionToken,{enabled:true})).status,403);assert.equal((await x.enable()).status,200);
 const first=executeProfile(x.env,x.p.profileId,{wait:x.wait});await entered;
 try{assert.equal(await executeProfile(await x.f.mf.getBindings('workspace'),x.p.profileId,{wait:x.wait}),'busy');}finally{release();}
 assert.equal(await first,'applied');assert.equal(x.writes,1);assert.ok(x.delays.some(ms=>ms>=29000));
 const runtime=await x.f.workspaceDb.prepare('SELECT * FROM workspace_runtime').first();assert.equal(runtime.lease_token,null);assert.ok(Date.parse(runtime.next_network_at_utc)>=x.observed.at(-1).at+29000);
 const archive=await x.exported();assert.equal(archive.commands[0].projectionState,'confirmed');assert.equal(archive.commands[0].confirmedEffects,1);assert.equal(archive.commands[0].attempts,1);assert.equal(archive.authority.mode,'cloud');assert.equal(archive.document.projections[0].state,'linked');assert.deepEqual(archive.events.map(e=>e.kind),['accepted','leaseAcquired','dispatched','resultKnown']);await journal(archive);
 assert.equal(await executeProfile(x.env,x.p.profileId,{wait:x.wait}),'idle');assert.equal(x.writes,1);
 assert.equal((await x.f.workspace.scheduled({cron:'* * * * *'})).outcome,'ok');assert.equal(x.writes,1);
});
test('restart recovers a durable Auth receipt after the browser-to-worker acknowledgement was lost',async t=>{
 let f;let lose=true;const x=await setup(t,{projectionService:async request=>{
  const body=await request.clone().json();const response=await f.projection.fetch(request);
  if(lose&&body.operation==='create'){lose=false;throw new Error('Synthetic lost acknowledgement');}return response;
 }});f=x.f;await x.enable();
 assert.equal(await executeProfile(x.env,x.p.profileId,{wait:x.wait}),'unknown');const uncertain=await x.exported();assert.equal(uncertain.commands[0].projectionState,'unknownOutcome');assert.equal(x.writes,1);
 assert.equal(await executeProfile(await f.mf.getBindings('workspace'),x.p.profileId,{wait:x.wait}),'recovered');
 const recovered=await x.exported();assert.equal(recovered.commands[0].projectionState,'confirmed');assert.equal(recovered.commands[0].attempts,1);assert.equal(recovered.events.at(-1).kind,'reconciled');assert.equal(recovered.events.at(-1).evidenceReference,'auth-attempt-receipt');assert.equal(x.writes,1);await journal(recovered);
});
test('unknown upstream outcomes fence later commands and duplicate wakes never replay them',async t=>{
 let writes=0;const x=await setup(t,{habitica:async request=>{if(request.method==='GET')return new Response('',{status:404});writes++;return new Response('',{status:502});}});await x.enable();
 assert.equal(await executeProfile(x.env,x.p.profileId,{wait:x.wait}),'unknown');const unknown=await x.exported();
 assert.equal((await call(x.f,'/commands',x.w.sessionToken,{...x.c,id:crypto.randomUUID(),taskId:crypto.randomUUID(),expectedRevision:unknown.document.revision})).status,201);
 const before=await x.exported();assert.equal(await executeProfile(x.env,x.p.profileId,{wait:x.wait}),'unknown');const after=await x.exported();assert.deepEqual(after,before);assert.equal(writes,1);
});
test('disconnect and executor disable during pacing stop the next upstream call',async t=>{
 for(const action of ['disable','disconnect']){
  let x;x=await setup(t,{onWait:async()=>{assert.equal((await call(x.f,action==='disable'?'/executor':'/disconnect',x.w.sessionToken,action==='disable'?{enabled:false}:{})).status,200);}});await x.enable();
  assert.equal(await executeProfile(x.env,x.p.profileId,{wait:x.wait}),'paused');assert.equal(x.writes,0);
  assert.equal(await executeProfile(x.env,x.p.profileId,{wait:x.wait}),'disabled');
  const entry=JSON.parse((await x.f.workspaceDb.prepare('SELECT entry_json FROM workspace_commands').first()).entry_json);assert.equal(entry.projectionState,'leased');assert.equal(entry.dispatchedAtUtc,null);
 }
});
test('known quota refusal persists a profile fence; undispatched expired leases recover safely',async t=>{
 let reads=0;const x=await setup(t,{habitica:async request=>{reads++;return new Response('',{status:429,headers:{'retry-after':'120'}});}});await x.enable();
 assert.equal(await executeProfile(x.env,x.p.profileId,{wait:x.wait}),'refused');let archive=await x.exported();assert.equal(archive.commands[0].failure,'rateLimited');assert.equal(archive.commands[0].projectionState,'pending');assert.equal(archive.commands[0].attempts,0);
 assert.equal(await executeProfile(x.env,x.p.profileId,{wait:x.wait}),'fenced');assert.equal(reads,1);
 const entry={...archive.commands[0],projectionState:'leased',lease:{owner:'old-worker',token:crypto.randomUUID(),expiresAtUtc:'2000-01-01T00:00:00Z'},retryAtUtc:null,failure:'none'};
 await x.f.workspaceDb.prepare('UPDATE workspace_commands SET entry_json=?').bind(JSON.stringify(entry)).run();
 assert.equal(await executeProfile(x.env,x.p.profileId,{wait:x.wait}),'lease_recovered');archive=await x.exported();assert.equal(archive.commands[0].projectionState,'pending');assert.equal(archive.commands[0].attempts,0);assert.equal(archive.events.at(-1).kind,'leaseExpired');assert.equal(reads,1);
});
test('missing receipt after a dispatched restart remains unknown, including after credential context changes',async t=>{
 const x=await setup(t);await x.enable();const archive=await x.exported();const lease={owner:'old-worker',token:crypto.randomUUID(),expiresAtUtc:'2000-01-01T00:00:00Z'};
 const entry={...archive.commands[0],projectionState:'executing',lease,attempts:1,dispatchedAtUtc:new Date().toISOString()};
 await x.f.workspaceDb.prepare('UPDATE workspace_commands SET entry_json=?').bind(JSON.stringify(entry)).run();
 assert.equal(await executeProfile(x.env,x.p.profileId,{wait:x.wait}),'unknown');const after=await x.exported();assert.equal(after.commands[0].projectionState,'unknownOutcome');assert.equal(x.writes,0);assert.equal(x.reads,0);
 assert.equal(await executeProfile(x.env,x.p.profileId,{wait:x.wait}),'unknown');assert.deepEqual(await x.exported(),after);
});
test('handoff is atomic, preserves IDs and history, deduplicates lost acknowledgements and never enables execution',async t=>{
 const x=await setup(t);const archive=await x.exported();const transferId=crypto.randomUUID();archive.authority={mode:'frozen',transferId,cloudWorkspaceId:null};archive.storeVersion++;
 const empty=await enrolled(x.f,{habiticaUserId:'00000000-0000-4000-8000-000000000012',apiToken:'fixture-b-token'});const connected=await(await call(x.f,'/connect',empty.managementToken,{consentVersion:'workspace-v1'})).json();
 // Separate profiles cannot import the same globally owned cloud identity.
 const collision=await call(x.f,'/handoff',connected.sessionToken,{consentVersion:'workspace-handoff-v1',transferId,archive:JSON.stringify(archive)});assert.equal(collision.status,409);
 // Use an independent local identity to qualify the real transfer path.
 const localId=crypto.randomUUID();archive.document.id=localId;for(const c of archive.commands)c.command.workspaceId=localId;
 const {commandHash}=await import('../../shared/workspace-commands.ts');for(const c of archive.commands)c.payloadHash=await commandHash(c.command);
 for(const witness of archive.document.activityCoverage??[])witness.payloadHash=archive.commands.find(c=>c.command.id===witness.commandId).payloadHash;
 const payload={consentVersion:'workspace-handoff-v1',transferId,archive:JSON.stringify(archive)};
 assert.equal((await call(x.f,'/handoff',connected.sessionToken,{...payload,consentVersion:undefined})).status,403);
 const accepted=await call(x.f,'/handoff',connected.sessionToken,payload);assert.equal(accepted.status,201,await accepted.clone().text());assert.equal((await accepted.json()).executorEnabled,false);
 assert.equal((await call(x.f,'/handoff',connected.sessionToken,payload)).status,200);
 assert.equal((await call(x.f,'/handoff',connected.sessionToken,{...payload,archive:JSON.stringify({...archive,storeVersion:archive.storeVersion+1})})).status,409);
 const restored=await(await call(x.f,'/export',connected.sessionToken)).json();assert.equal(restored.document.id,localId);assert.equal(restored.commands[0].command.id,archive.commands[0].command.id);assert.deepEqual(restored.events,archive.events);assert.equal(restored.authority.mode,'cloud');assert.equal(restored.authority.transferId,transferId);await journal(restored);
 assert.equal(await executeProfile(x.env,empty.profileId,{wait:x.wait}),'disabled');assert.equal(x.writes,0);
});
test('handoff append failure rolls all canonical state and receipt data back',async t=>{
 const x=await setup(t);const source=await x.exported();const transferId=crypto.randomUUID();source.authority={mode:'frozen',transferId,cloudWorkspaceId:null};
 // Clear the synthetic target only; transfer data remains independently held in this fixture.
 await x.f.workspaceDb.batch([x.f.workspaceDb.prepare('DELETE FROM workspace_commands'),x.f.workspaceDb.prepare('DELETE FROM workspace_events'),x.f.workspaceDb.prepare('UPDATE workspaces SET store_version=1,document_json=?').bind(JSON.stringify({...source.document,activityCoverage:undefined,tasks:[],projections:[],revision:1}))]);
 await x.f.workspaceDb.prepare("CREATE TRIGGER fixture_transfer_fail BEFORE INSERT ON workspace_events BEGIN SELECT RAISE(ABORT,'synthetic'); END").run();
 const before=await x.exported();assert.equal((await call(x.f,'/handoff',x.w.sessionToken,{consentVersion:'workspace-handoff-v1',transferId,archive:JSON.stringify(source)})).status,503);assert.deepEqual(await x.exported(),before);assert.equal((await x.f.workspaceDb.prepare('SELECT COUNT(*) AS n FROM workspace_handoffs').first()).n,0);
});

test('revoked credentials block new effects and old-context unknown receipts remain fenced after revalidation',async t=>{
 const x=await setup(t);await x.enable();
 await x.f.auth.fetch(`${issuer}/profiles/${x.p.profileId}/disconnect`,{method:'POST',headers:{authorization:`Bearer ${x.p.managementToken}`}});
 assert.equal(await executeProfile(x.env,x.p.profileId,{wait:x.wait}),'refused');assert.equal(x.writes,0);assert.equal(x.reads,0);
 let archive=JSON.parse((await x.f.workspaceDb.prepare('SELECT entry_json FROM workspace_commands').first()).entry_json);assert.equal(archive.failure,'credentialRepair');assert.equal(archive.attempts,0);
 const fresh=await enrolled(x.f);const connection=await(await call(x.f,'/connect',fresh.managementToken,{consentVersion:'workspace-v1'})).json();
 assert.equal((await call(x.f,'/executor',connection.sessionToken,{enabled:true,consentVersion:'executor-v1'})).status,200);
 const attemptId=crypto.randomUUID();const prepared={profileId:x.p.profileId,connectionId:crypto.randomUUID(),operation:'create',arguments:{selector:'companion_'+x.w.workspaceId.replaceAll('-','')+'_'+x.c.taskId.replaceAll('-',''),checkToken:'expired'},attemptId,attemptAtUtc:new Date().toISOString(),automated:true};
 archive={...archive,projectionState:'unknownOutcome',failure:'ambiguous',lease:{owner:'old-context',token:attemptId,expiresAtUtc:'2000-01-01T00:00:00Z'},dispatchedAtUtc:prepared.attemptAtUtc,attempts:1};
 await x.f.workspaceDb.prepare('UPDATE workspace_commands SET entry_json=?').bind(JSON.stringify(archive)).run();
 await x.f.workspaceDb.prepare('UPDATE workspace_runtime SET command_id=?,prepared_request_json=?').bind(x.c.id,JSON.stringify(prepared)).run();
 assert.equal(await executeProfile(x.env,x.p.profileId,{wait:x.wait}),'unknown');assert.equal(x.writes,0);assert.equal(x.reads,0);
 const after=JSON.parse((await x.f.workspaceDb.prepare('SELECT entry_json FROM workspace_commands').first()).entry_json);assert.equal(after.projectionState,'unknownOutcome');assert.equal(after.lease.token,attemptId);
});

test('hosted Daily execution obtains fresh account and due receipts before the paced score',async t=>{
 const externalId='90000000-0000-4000-8000-000000000023';let writes=0;let remote;
 const x=await setup(t,{habitica:async request=>{
  if(new URL(request.url).pathname==='/api/v3/user')return Response.json({success:true,data:{preferences:{dayStart:0,timezoneOffset:0},needsCron:false,lastCron:new Date().toISOString()}});
  if(request.method==='GET')return Response.json({success:true,data:[remote]});
  assert.ok(request.url.endsWith('/score/up'));writes++;return Response.json({success:true,data:{delta:1}});
 }});
 const {remoteTask}=await import('../../shared/projection.ts');const {commandHash}=await import('../../shared/workspace-commands.ts');
 const archive=await x.exported();const date=new Date().toISOString().slice(0,10);
 remote={id:externalId,userId:'00000000-0000-4000-8000-000000000011',type:'daily',text:'Daily fixture',notes:'',priority:1,attribute:'str',completed:false,isDue:true,checklist:[],group:{},challenge:{}};
 const verified=await remoteTask(remote,remote.userId,true);const document=archive.document;
 document.tasks[0]={...document.tasks[0],kind:'daily',text:remote.text,recurrence:{frequency:'daily',interval:1,startDate:date,timeZoneId:'UTC',weekdays:[],daysOfMonth:[]}};
 document.projections[0]={...document.projections[0],habiticaTaskId:externalId,state:'linked',fingerprint:verified.fingerprint};
 const first={...archive.commands[0],command:{...archive.commands[0].command,operation:'createDaily',patch:{...archive.commands[0].command.patch,text:remote.text,recurrence:document.tasks[0].recurrence},projectToHabitica:false},taskSnapshot:structuredClone(document.tasks[0]),effects:[],projectionState:'notRequested'};first.payloadHash=await commandHash(first.command);
 // Synthetic creation now describes the Daily consistently in the retained source and witness.
 Object.assign(document.activityCoverage[0],{payloadHash:first.payloadHash,kind:'daily',recurrence:structuredClone(document.tasks[0].recurrence)});
 await x.f.workspaceDb.batch([x.f.workspaceDb.prepare('UPDATE workspaces SET document_json=?').bind(JSON.stringify(document)),x.f.workspaceDb.prepare('UPDATE workspace_commands SET entry_json=?').bind(JSON.stringify(first))]);
 const complete={id:crypto.randomUUID(),workspaceId:x.w.workspaceId,expectedRevision:document.revision,operation:'completeTask',taskId:x.c.taskId,occurrenceDate:date,projectToHabitica:true};
 assert.equal((await call(x.f,'/commands',x.w.sessionToken,complete)).status,201);await x.enable();
 assert.equal(await executeProfile(x.env,x.p.profileId,{wait:x.wait}),'applied');assert.equal(writes,1);assert.equal(x.delays.filter(ms=>ms>=29000).length,2);
 const completed=await x.exported();assert.equal(completed.commands[1].projectionState,'confirmed');assert.equal(completed.document.completions.length,1);assert.equal(completed.document.occurrences[0].state,'completed');await journal(completed);
 const source=completed.commands[1],completion=completed.document.completions[0],dispatched=completed.events.find(row=>row.commandId===complete.id&&row.kind==='dispatched');assert.ok(dispatched.attemptToken);
 const connection=await x.f.db.prepare('SELECT connection_id FROM integration_profiles WHERE profile_id=?').bind(x.p.profileId).first();
 const runtime=await x.f.workspaceDb.prepare('SELECT prepared_request_json FROM workspace_runtime').first();assert.equal(runtime.prepared_request_json,null);
 const before=x.observed.length;
 const lookup=await x.env.AUTH_PROJECTION.fetch('https://projection.internal/operation',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({profileId:x.p.profileId,connectionId:connection.connection_id,operation:'scoreReceipt',automated:false,arguments:{attemptId:dispatched.attemptToken,selector:externalId,taskKind:'daily',expectedFingerprint:completed.document.projections[0].fingerprint,sourceContext:{contract:'companion-completion-score-v1',workspaceId:completed.document.id,taskId:complete.taskId,commandId:complete.id,commandPayloadHash:source.payloadHash,completionId:completion.id,effectIndex:0,taskKind:'daily',occurrenceDate:date}}})});
 assert.equal(lookup.status,200);const proof=await lookup.json();assert.equal(proof.verified,true);assert.equal(proof.sourceContext.completionId,completion.id);assert.equal(x.observed.length,before);

});
