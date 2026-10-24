import test from 'node:test';
import assert from 'node:assert/strict';
import {fixture,enrolled,issuer,authorization,exchange,authPage,accounts,ticket,form,handle} from './fixtures/oauth.mjs';
const context=()=>({workspaceId:crypto.randomUUID(),entityId:crypto.randomUUID(),kind:'project'});
async function setup(t,options={}){
 const tags=[],task={id:crypto.randomUUID(),type:'todo',text:'Synthetic task',notes:'',priority:1,attribute:'str',completed:false,checklist:[],group:{},challenge:{},tags:[]};let reads=0,writes=0,f;
 f=await fixture(t,{workspace:true,projection:true,...options,habitica:async request=>{
  if(request.url.includes('user?userFields=_id,profile'))return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:'Fixture A'}}});
  if(options.beforeRequest)await options.beforeRequest(request,f);
  task.userId??=request.headers.get('x-api-user');
  if(request.method==='GET'){reads++;if(request.url.endsWith('/tags'))return Response.json({success:true,data:tags});return Response.json({success:true,data:task});}
  writes++;if(options.mutate)return options.mutate(request,tags,task);
  if(request.url.endsWith('/tags')){const body=await request.json();tags.push({...body});return Response.json({success:true,data:tags.at(-1)},{status:201});}
  if(request.url.includes('/tasks/')){const tag=request.url.split('/').at(-1);if(request.method==='POST')task.tags.push(tag);else task.tags=task.tags.filter(id=>id!==tag);return Response.json({success:true,data:task});}
  const tag=tags.find(tag=>request.url.endsWith('/'+tag.id));Object.assign(tag,await request.json());return Response.json({success:true,data:tag});
 }});
 const profile=await enrolled(f);const row=await f.db.prepare('SELECT connection_id FROM integration_profiles WHERE profile_id=?').bind(profile.profileId).first();
 const invoke=(operation,args={},extra={})=>f.projection.fetch('https://projection.internal/tags',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({profileId:profile.profileId,connectionId:row.connection_id,context:c,operation,arguments:args,automated:false,...extra})});
 const consent=(enabled=true,p=profile)=>f.auth.fetch(`${issuer}/profiles/${p.profileId}/tag-consent`,{method:'POST',headers:{authorization:`Bearer ${p.managementToken}`,'content-type':'application/json'},body:JSON.stringify({consentVersion:'habitica-tags-v1',enabled})});
 const c=context();const preview=async()=>{const r=await invoke('inspectTags');assert.equal(r.status,200);return r.json();};
 const write=(operation,args,extra={})=>invoke(operation,args,{attemptId:crypto.randomUUID(),attemptAtUtc:new Date().toISOString(),...extra});
 return {f,profile,c,tags,task,invoke,preview,write,consent,connectionId:row.connection_id,get reads(){return reads;},get writes(){return writes;}};
}
test('tag writes require separate owner consent; old organization scopes and public routes grant no private gateway',async t=>{
 const x=await setup(t);const denied=await x.invoke('inspectTags');assert.equal(denied.status,403);assert.equal((await denied.json()).code,'tag_consent_required');assert.equal(x.reads,0);
 const oldPage=await authPage(x.f,'profile:read workspace.organization.write');assert.match(await oldPage.response.text(),/Can change owned Habitica tags/);
 const newPage=await authPage(x.f,'profile:read habitica.tags.write');assert.match(await newPage.response.text(),/Can change owned Habitica tags/);
 assert.ok([401,404].includes((await x.f.auth.fetch(issuer+'/tags',{method:'POST',body:'{}'})).status));
 const oldToken=await(await exchange(x.f,await authorization(x.f,x.profile,'profile:read workspace.organization.write'))).json();
 const identity=await x.f.workspaceIdentity.fetch('https://identity.internal/mcp',{method:'POST',headers:{authorization:`Bearer ${oldToken.access_token}`}});assert.equal((await identity.json()).tagWriteAllowed,false);
 const newToken=await(await exchange(x.f,await authorization(x.f,x.profile,'profile:read habitica.tags.write'))).json();
 const allowed=await x.f.workspaceIdentity.fetch('https://identity.internal/mcp',{method:'POST',headers:{authorization:`Bearer ${newToken.access_token}`}});assert.equal((await allowed.json()).tagWriteAllowed,true);
 assert.equal((await x.invoke('inspectTags')).status,403);assert.equal((await x.consent()).status,200);assert.equal((await x.invoke('inspectTags')).status,200);assert.equal(x.writes,0);
});
test('reviewed creation binds one native tag and recovers the exact original receipt without another POST',async t=>{
 const x=await setup(t);await x.consent();const p=await x.preview(),tagId=crypto.randomUUID(),args={tagId,name:'Native project',checkToken:p.checkToken,expectedFingerprint:p.fingerprint},extra={attemptId:crypto.randomUUID(),attemptAtUtc:new Date().toISOString()};
 const saved=await(await x.write('createTag',args,extra)).json();assert.equal(saved.kind,'applied');assert.equal(saved.tag.id,tagId);assert.equal(x.writes,1);assert.equal(x.tags.length,1);
 assert.deepEqual(await(await x.write('createTag',args,extra)).json(),saved);assert.equal(x.writes,1);
 assert.deepEqual(await(await x.invoke('lookupTagAttempt',{operation:'createTag',arguments:args},extra)).json(),saved);assert.equal(x.reads,1);
 assert.equal((await x.write('createTag',{...args,name:'Changed'},extra)).status,409);assert.equal(x.writes,1);
 const map=await x.f.db.prepare('SELECT * FROM integration_owned_tags').first();assert.equal(map.workspace_id,x.c.workspaceId);assert.equal(map.entity_id,x.c.entityId);assert.equal(map.receipt_id,extra.attemptId);
});
test('explicit adoption and rename preserve unrelated tags; names alone and challenge/group tags cannot authorize mutation',async t=>{
 const x=await setup(t);await x.consent();const adopted={id:crypto.randomUUID(),name:'Same'},external={id:crypto.randomUUID(),name:'Same'},challenge={id:crypto.randomUUID(),name:'Challenge',challenge:true},group={id:crypto.randomUUID(),name:'Group',group:crypto.randomUUID()};x.tags.push(adopted,external,challenge,group);
 let p=await x.preview();assert.equal((await x.write('renameTag',{tagId:adopted.id,name:'Wrong',checkToken:p.checkToken,expectedFingerprint:p.fingerprint})).status,409);
 assert.equal((await x.write('adoptTag',{tagId:challenge.id,checkToken:p.checkToken,expectedFingerprint:p.fingerprint})).status,409);
 assert.equal((await x.write('adoptTag',{tagId:group.id,checkToken:p.checkToken,expectedFingerprint:p.fingerprint})).status,409);
 const result=await(await x.write('adoptTag',{tagId:adopted.id,checkToken:p.checkToken,expectedFingerprint:p.fingerprint})).json();assert.equal(result.code,'adopted');assert.equal(x.writes,0);
 p=await x.preview();assert.equal((await(await x.write('renameTag',{tagId:adopted.id,name:'Renamed',checkToken:p.checkToken,expectedFingerprint:p.fingerprint})).json()).kind,'applied');assert.equal(adopted.name,'Renamed');assert.equal(external.name,'Same');assert.equal(challenge.name,'Challenge');assert.equal(group.name,'Group');assert.equal(x.writes,1);
});
test('individual add/remove preserves unrelated user tags and never scores or deletes native tags',async t=>{
 const x=await setup(t);await x.consent();const managed={id:crypto.randomUUID(),name:'Managed'},external={id:crypto.randomUUID(),name:'External'};x.tags.push(managed,external);x.task.tags=[external.id];let p=await x.preview();await x.write('adoptTag',{tagId:managed.id,checkToken:p.checkToken,expectedFingerprint:p.fingerprint});
 for(const present of [true,true,false]){
  p=await x.preview();const read=await(await x.invoke('inspectMembership',{tagId:managed.id,taskId:x.task.id,tagCheckToken:p.checkToken})).json();
  const response=await x.write('setMembership',{tagId:managed.id,taskId:x.task.id,present,checkToken:read.checkToken,expectedFingerprint:read.fingerprint});assert.equal(response.status,200);assert.equal((await response.json()).kind,'applied');assert.ok(x.task.tags.includes(external.id));assert.equal(x.task.tags.includes(managed.id),present);
 }
 assert.equal(x.writes,2);assert.equal(x.tags.length,2);
});
for(const mode of ['group','challenge','foreign'])test('membership refuses '+mode+' task ownership before dispatch',async t=>{
 const x=await setup(t);await x.consent();const tag={id:crypto.randomUUID(),name:'Managed'};x.tags.push(tag);const p=await x.preview();await x.write('adoptTag',{tagId:tag.id,checkToken:p.checkToken,expectedFingerprint:p.fingerprint});
 if(mode==='group')x.task.group={id:crypto.randomUUID()};if(mode==='challenge')x.task.challenge={id:crypto.randomUUID()};if(mode==='foreign')x.task.userId=accounts[1].habiticaUserId;
 const fresh=await x.preview();const r=await x.invoke('inspectMembership',{tagId:tag.id,taskId:x.task.id,tagCheckToken:fresh.checkToken});assert.equal(r.status,409);assert.equal((await r.json()).code,'task_not_owned');assert.equal(x.writes,0);
});
test('timeout after tag create remains unknown; lookup and a replacement attempt never recreate it',async t=>{
 const x=await setup(t,{mutate:async(request,tags)=>{tags.push(await request.json());return new Response('',{status:502});}});await x.consent();const p=await x.preview(),args={tagId:crypto.randomUUID(),name:'Uncertain',checkToken:p.checkToken,expectedFingerprint:p.fingerprint},extra={attemptId:crypto.randomUUID(),attemptAtUtc:new Date().toISOString()};
 assert.equal((await(await x.write('createTag',args,extra)).json()).kind,'unknown');assert.equal(x.writes,1);
 assert.equal((await(await x.invoke('lookupTagAttempt',{operation:'createTag',arguments:args},extra)).json()).kind,'unknown');assert.equal((await x.write('createTag',args)).status,409);assert.equal(x.writes,1);assert.equal(x.reads,1);
 await x.consent(false);assert.equal((await(await x.invoke('lookupTagAttempt',{operation:'createTag',arguments:args},extra)).json()).kind,'unknown');assert.equal((await x.invoke('inspectTags')).status,403);
});
test('expired checks, wrong fingerprints, revoked consent and quota stop before dispatch',async t=>{
 const x=await setup(t);await x.consent();let p=await x.preview();const args={tagId:crypto.randomUUID(),name:'Checked',checkToken:p.checkToken,expectedFingerprint:'a'.repeat(64)};
 assert.equal((await x.write('createTag',args)).status,409);await x.f.db.prepare("UPDATE integration_tag_checks SET expires_at_utc='2000-01-01T00:00:00Z'").run();assert.equal((await x.write('createTag',{...args,expectedFingerprint:p.fingerprint})).status,409);
 p=await x.preview();await x.consent(false);assert.equal((await x.write('createTag',{...args,checkToken:p.checkToken,expectedFingerprint:p.fingerprint})).status,403);
 await x.consent();p=await x.preview();await x.f.db.prepare('INSERT INTO integration_read_state(profile_id,connection_id,blocked_until_utc) VALUES(?,?,?) ON CONFLICT(profile_id,connection_id) DO UPDATE SET blocked_until_utc=excluded.blocked_until_utc').bind(x.profile.profileId,x.connectionId,new Date(Date.now()+120000).toISOString()).run();
 assert.equal((await x.write('createTag',{...args,checkToken:p.checkToken,expectedFingerprint:p.fingerprint})).status,429);assert.equal(x.writes,0);assert.equal((await x.f.db.prepare('SELECT COUNT(*) AS n FROM integration_projection_attempts').first()).n,0);
});

test('checks and tag ownership are isolated from other profiles, contexts and revoked credential connections',async t=>{
 const x=await setup(t);await x.consent();const p=await x.preview(),args={tagId:crypto.randomUUID(),name:'Owned',checkToken:p.checkToken,expectedFingerprint:p.fingerprint};
 const other=await enrolled(x.f,accounts[1]),row=await x.f.db.prepare('SELECT connection_id FROM integration_profiles WHERE profile_id=?').bind(other.profileId).first();await x.consent(true,other);
 assert.equal((await x.write('createTag',args,{profileId:other.profileId,connectionId:row.connection_id})).status,409);
 assert.equal((await x.write('createTag',args,{context:{...x.c,entityId:crypto.randomUUID()}})).status,409);assert.equal(x.writes,0);
 await x.f.db.prepare('UPDATE integration_profiles SET connection_id=? WHERE profile_id=?').bind(crypto.randomUUID(),x.profile.profileId).run();
 assert.equal((await x.write('createTag',args)).status,401);assert.equal(x.writes,0);
});
test('shared effect leases and adaptive admission refuse before reserving a tag dispatch',async t=>{
 const x=await setup(t,{adaptivePacing:true});await x.consent();const p=await x.preview(),args={tagId:crypto.randomUUID(),name:'Bounded',checkToken:p.checkToken,expectedFingerprint:p.fingerprint};
 await x.f.db.prepare('INSERT INTO integration_projection_state(profile_id,connection_id,lease_token,lease_until_utc) VALUES(?,?,?,?)').bind(x.profile.profileId,x.connectionId,crypto.randomUUID(),new Date(Date.now()+60000).toISOString()).run();
 assert.equal((await x.write('createTag',args)).status,429);assert.equal(x.writes,0);
 await x.f.db.prepare('UPDATE integration_projection_state SET lease_token=NULL,lease_until_utc=NULL').run();
 await x.f.db.prepare("UPDATE upstream_pacing SET tokens=0,next_ms=? WHERE bucket=?").bind(Date.now()+60000,'profile:'+x.profile.profileId).run();
 assert.equal((await x.write('createTag',args)).status,429);assert.equal(x.writes,0);assert.equal((await x.f.db.prepare('SELECT COUNT(*) AS n FROM integration_projection_attempts').first()).n,0);
});
test('failed durable reservation never sends; lost result persistence keeps the original unknown fence',async t=>{
 const x=await setup(t);await x.consent();let p=await x.preview();const args={tagId:crypto.randomUUID(),name:'Durable',checkToken:p.checkToken,expectedFingerprint:p.fingerprint},extra={attemptId:crypto.randomUUID(),attemptAtUtc:new Date().toISOString()};
 await x.f.db.prepare("CREATE TRIGGER fail_tag_reservation BEFORE INSERT ON integration_projection_attempts BEGIN SELECT RAISE(ABORT,'synthetic reservation outage'); END").run();
 assert.equal((await x.write('createTag',args,extra)).status,503);assert.equal(x.writes,0);await x.f.db.prepare('DROP TRIGGER fail_tag_reservation').run();
 await x.f.db.prepare("CREATE TRIGGER fail_tag_result BEFORE UPDATE ON integration_projection_attempts BEGIN SELECT RAISE(ABORT,'synthetic result outage'); END").run();
 assert.equal((await(await x.write('createTag',args,extra)).json()).kind,'unknown');assert.equal(x.writes,1);assert.equal(x.tags.length,1);await x.f.db.prepare('DROP TRIGGER fail_tag_result').run();
 assert.equal((await(await x.invoke('lookupTagAttempt',{operation:'createTag',arguments:args},extra)).json()).kind,'unknown');assert.equal((await x.write('createTag',args)).status,409);assert.equal(x.writes,1);
});
test('removing unrelated tags in an unexpected upstream response remains uncertain and is never replayed',async t=>{
 const x=await setup(t,{mutate:async(request,tags,task)=>{task.tags=[request.url.split('/').at(-1)];return Response.json({success:true,data:task});}});await x.consent();const managed={id:crypto.randomUUID(),name:'Managed'},external={id:crypto.randomUUID(),name:'External'};x.tags.push(managed,external);x.task.tags=[external.id];const p=await x.preview();await x.write('adoptTag',{tagId:managed.id,checkToken:p.checkToken,expectedFingerprint:p.fingerprint});
 const fresh=await x.preview();const readResponse=await x.invoke('inspectMembership',{tagId:managed.id,taskId:x.task.id,tagCheckToken:fresh.checkToken});assert.equal(readResponse.status,200);const read=await readResponse.json();const args={tagId:managed.id,taskId:x.task.id,present:true,checkToken:read.checkToken,expectedFingerprint:read.fingerprint};
 assert.equal((await(await x.write('setMembership',args)).json()).kind,'unknown');assert.equal((await x.write('setMembership',args)).status,409);assert.equal(x.writes,1);
});
test('disconnect purges private tag context while remote tags survive',async t=>{
 const x=await setup(t);await x.consent();const p=await x.preview(),tag={id:crypto.randomUUID(),name:'Retained remote'};x.tags.push(tag);
 const latest=await x.preview();await x.write('adoptTag',{tagId:tag.id,checkToken:latest.checkToken,expectedFingerprint:latest.fingerprint});
 const r=await x.f.auth.fetch(`${issuer}/profiles/${x.profile.profileId}/disconnect`,{method:'POST',headers:{authorization:`Bearer ${x.profile.managementToken}`}});assert.equal(r.status,200);
 for(const table of ['integration_tag_checks','integration_tag_consents','integration_owned_tags','integration_tag_attempt_details'])assert.equal((await x.f.db.prepare(`SELECT COUNT(*) AS n FROM ${table}`).first()).n,0);
 assert.equal(x.tags.length,1);assert.equal(x.writes,0);
});

test('an unknown tag effect fences ordinary task writes through the shared Auth ledger',async t=>{
 const x=await setup(t,{mutate:async()=>new Response('',{status:502})});await x.consent();const p=await x.preview();await x.write('createTag',{tagId:crypto.randomUUID(),name:'Unknown',checkToken:p.checkToken,expectedFingerprint:p.fingerprint});const before=x.writes;
 const response=await x.f.projection.fetch('https://projection.internal/operation',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({profileId:x.profile.profileId,connectionId:x.connectionId,operation:'create',arguments:{selector:`companion_${crypto.randomUUID().replaceAll('-','')}_${crypto.randomUUID().replaceAll('-','')}`,checkToken:'invalid',patch:{text:'Must stay fenced'}},attemptId:crypto.randomUUID(),attemptAtUtc:new Date().toISOString(),automated:false})});
 assert.equal(response.status,409);assert.equal((await response.json()).code,'unknown_outcome_pending');assert.equal(x.writes,before);
});
test('a retained unknown gameplay receipt prevents new tag dispatches',async t=>{
 const x=await setup(t);await x.consent();const p=await x.preview();await x.f.db.prepare("INSERT INTO integration_projection_attempts(profile_id,connection_id,attempt_id,payload_hash,operation,state,created_at_utc) VALUES(?,?,?,?,?,'unknown',?)").bind(x.profile.profileId,x.connectionId,crypto.randomUUID(),'a'.repeat(64),'score',new Date().toISOString()).run();
 const response=await x.write('createTag',{tagId:crypto.randomUUID(),name:'Must stay fenced',checkToken:p.checkToken,expectedFingerprint:p.fingerprint});assert.equal(response.status,409);assert.equal((await response.json()).code,'unknown_outcome_pending');assert.equal(x.writes,0);
});

test('explicit recovery reviews exact metadata before resolving the original unknown create without a second POST',async t=>{
 const x=await setup(t,{mutate:async(request,tags)=>{tags.push(await request.json());return new Response('',{status:502});}});await x.consent();const p=await x.preview(),args={tagId:crypto.randomUUID(),name:'Observed after timeout',checkToken:p.checkToken,expectedFingerprint:p.fingerprint},extra={attemptId:crypto.randomUUID(),attemptAtUtc:new Date().toISOString()};
 assert.equal((await(await x.write('createTag',args,extra)).json()).kind,'unknown');
 const recovery=await(await x.invoke('prepareTagRecovery',{operation:'createTag',arguments:args},extra)).json();assert.equal(recovery.kind,'review');assert.equal(recovery.code,'original_send_unknown');assert.equal(recovery.tag.id,args.tagId);assert.equal(x.writes,1);
 assert.equal((await(await x.invoke('lookupTagAttempt',{operation:'createTag',arguments:args},extra)).json()).kind,'unknown');
 const accepted=await(await x.invoke('acceptTagRecovery',{operation:'createTag',arguments:args,checkToken:recovery.checkToken,expectedFingerprint:recovery.fingerprint},extra)).json();assert.equal(accepted.kind,'applied');assert.equal(accepted.code,'reviewed_observation');assert.equal(accepted.receiptId,extra.attemptId);assert.equal(x.writes,1);
 assert.deepEqual(await(await x.invoke('lookupTagAttempt',{operation:'createTag',arguments:args},extra)).json(),accepted);
 assert.deepEqual(await(await x.invoke('acceptTagRecovery',{operation:'createTag',arguments:args,checkToken:recovery.checkToken,expectedFingerprint:recovery.fingerprint},extra)).json(),accepted);assert.equal(x.writes,1);
});
test('recovery rejects absence, renamed identity, wrong original payload and revoked review tokens',async t=>{
 const x=await setup(t,{mutate:async()=>new Response('',{status:502})});await x.consent();const p=await x.preview(),args={tagId:crypto.randomUUID(),name:'Unknown',checkToken:p.checkToken,expectedFingerprint:p.fingerprint},extra={attemptId:crypto.randomUUID(),attemptAtUtc:new Date().toISOString()};await x.write('createTag',args,extra);
 assert.equal((await(await x.invoke('prepareTagRecovery',{operation:'createTag',arguments:args},extra)).json()).kind,'unknown');assert.equal((await x.invoke('prepareTagRecovery',{operation:'createTag',arguments:{...args,name:'Different'}},extra)).status,409);
 x.tags.push({id:args.tagId,name:'Other name'});assert.equal((await(await x.invoke('prepareTagRecovery',{operation:'createTag',arguments:args},extra)).json()).kind,'unknown');x.tags[0].name=args.name;
 const recovery=await(await x.invoke('prepareTagRecovery',{operation:'createTag',arguments:args},extra)).json();assert.equal(recovery.kind,'review');await x.consent(false);
 assert.equal((await x.invoke('acceptTagRecovery',{operation:'createTag',arguments:args,checkToken:recovery.checkToken,expectedFingerprint:recovery.fingerprint},extra)).status,403);assert.equal(x.writes,1);
});
test('reviewed membership recovery preserves original unrelated tags and sends no repeated add',async t=>{
 const x=await setup(t,{mutate:async(request,tags,task)=>{task.tags.push(request.url.split('/').at(-1));return new Response('',{status:502});}});await x.consent();const tag={id:crypto.randomUUID(),name:'Managed'},other={id:crypto.randomUUID(),name:'User tag'};x.tags.push(tag,other);x.task.tags=[other.id];let p=await x.preview();await x.write('adoptTag',{tagId:tag.id,checkToken:p.checkToken,expectedFingerprint:p.fingerprint});p=await x.preview();
 const read=await(await x.invoke('inspectMembership',{tagId:tag.id,taskId:x.task.id,tagCheckToken:p.checkToken})).json(),args={tagId:tag.id,taskId:x.task.id,present:true,checkToken:read.checkToken,expectedFingerprint:read.fingerprint},extra={attemptId:crypto.randomUUID(),attemptAtUtc:new Date().toISOString()};await x.write('setMembership',args,extra);
 const recovery=await(await x.invoke('prepareTagRecovery',{operation:'setMembership',arguments:args},extra)).json();assert.equal(recovery.kind,'review');const accepted=await(await x.invoke('acceptTagRecovery',{operation:'setMembership',arguments:args,checkToken:recovery.checkToken,expectedFingerprint:recovery.fingerprint},extra)).json();assert.equal(accepted.code,'reviewed_observation');assert.equal(x.writes,1);assert.ok(x.task.tags.includes(other.id));
});


test('verified tag consent disclosure keeps selectable scope and safe callback policy',async t=>{
 const x=await setup(t),code=await ticket(x.f,x.profile),page=await authPage(x.f,'profile:read workspace.organization.write habitica.tags.write'),first=await page.response.text();
 const response=await form(x.f,page,{handle:handle(first),decision:'identify',code:code.code,scope:'profile:read workspace.organization.write habitica.tags.write'});assert.equal(response.status,200);const html=await response.text();
 assert.match(html,/Tag access is separate/);assert.match(html,/name="scope" value="habitica.tags.write" checked/);assert.match(response.headers.get('content-security-policy'),/form-action 'self' http:\/\/localhost:9999/);
 if(process.env.HABITICA_AUTH_BROWSER_EVIDENCE){const {mkdir,writeFile}=await import('node:fs/promises');await mkdir(process.env.HABITICA_AUTH_BROWSER_EVIDENCE,{recursive:true});await writeFile(process.env.HABITICA_AUTH_BROWSER_EVIDENCE+'/tag-consent.html',html);await writeFile(process.env.HABITICA_AUTH_BROWSER_EVIDENCE+'/tag-identify.html',first);}
});

test('oversized complete native tag observations refuse without first-page truncation or effects',async t=>{
 const x=await setup(t);await x.consent();for(let i=0;i<1200;i++)x.tags.push({id:crypto.randomUUID(),name:'x'.repeat(240)});
 const r=await x.invoke('inspectTags');assert.equal(r.status,413);assert.equal((await r.json()).code,'tag_check_capacity');assert.equal(x.writes,0);assert.equal((await x.f.db.prepare('SELECT COUNT(*) AS n FROM integration_tag_checks').first()).n,0);
});
test('private tag observations expose only the exact registered context and queued authority epochs are enforced',async t=>{
 const x=await setup(t);await x.consent();let p=await x.preview();assert.equal(p.mappedTagId,null);
 const consent=await x.f.db.prepare('SELECT consent_epoch FROM integration_tag_consents').first();
 for(const guard of [{expectedConsentEpoch:crypto.randomUUID()},{requiredGrantEpoch:'revoked-epoch'}]){
  const before=x.reads,r=await x.invoke('inspectTags',{},guard);assert.equal(r.status,409);assert.equal((await r.json()).code,'tag_authority_changed');assert.equal(x.reads,before);
 }
 const tagId=crypto.randomUUID(),result=await(await x.write('createTag',{tagId,name:'Registered',checkToken:p.checkToken,expectedFingerprint:p.fingerprint},{expectedConsentEpoch:consent.consent_epoch,requiredGrantEpoch:'legacy'})).json();assert.equal(result.kind,'applied');p=await x.preview();assert.equal(p.mappedTagId,tagId);
 const other=await x.invoke('inspectTags',{}, {context:{...x.c,entityId:crypto.randomUUID()}});assert.equal((await other.json()).mappedTagId,null);
});
