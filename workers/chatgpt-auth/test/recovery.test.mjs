import {test} from 'node:test';
import assert from 'node:assert/strict';
import {fixture,enrolled,accounts,issuer,authorization,exchange} from './fixtures/oauth.mjs';

const auth=(f,path,token,body,method=body===undefined?'GET':'POST')=>f.auth.fetch(issuer+path,{method,headers:{'content-type':'application/json',...(token?{authorization:`Bearer ${token}`}:{})},body:body===undefined?undefined:JSON.stringify(body)});
const cloud=(f,path,token,body,method=body===undefined?'GET':'POST')=>f.workspace.fetch('http://localhost:8789'+path,{method,headers:{'content-type':'application/json',...(token?{authorization:`Bearer ${token}`}:{})},body:body===undefined?undefined:JSON.stringify(body)});
const recover=(f,p,extra={})=>auth(f,'/recover',null,{...accounts[0],descriptor:p.descriptor,...extra});
async function accepted(response,status=200){assert.equal(response.status,status,await response.clone().text());return response.json();}
const profile=(f,p)=>f.db.prepare('SELECT * FROM integration_profiles WHERE profile_id=?').bind(p.profileId).first();

test('fresh-tab recovery preserves profile, consent, grants, records and healthy sessions without enrollment',async t=>{
 const f=await fixture(t,{workspace:true}),p=await enrolled(f);
 const w=await accepted(await cloud(f,'/connect',p.managementToken,{consentVersion:'workspace-v1'}),201);
 const n=await accepted(await cloud(f,'/notes/connect',p.managementToken,{consentVersion:'notes-v1'}),201);
 await accepted(await cloud(f,'/notes/command',n.sessionToken,{expectedNotebookId:n.notebookId,command:{id:crypto.randomUUID(),noteId:crypto.randomUUID(),expectedNotebookRevision:0,expectedNoteRevision:0,operation:'create',title:'Retained note',markdown:'Exact\r\nbody'}}),201);
 await accepted(await cloud(f,'/commands',w.sessionToken,{id:crypto.randomUUID(),workspaceId:w.workspaceId,expectedRevision:1,operation:'createTodo',taskId:crypto.randomUUID(),patch:{text:'Retained task'},projectToHabitica:true}),201);
 const before=await profile(f,p),workspace=(await f.workspaceDb.prepare('SELECT * FROM workspaces').all()).results,notes=(await f.workspaceDb.prepare('SELECT * FROM notes_notebooks').all()).results;
 const book=await accepted(await cloud(f,'/notes/export',n.sessionToken));
 const a=await accepted(await recover(f,p)),b=await accepted(await recover(f,p));
 assert.equal(a.recovered,true);assert.deepEqual(a.descriptor,p.descriptor);assert.notEqual(a.managementToken,p.managementToken);assert.notEqual(a.managementToken,b.managementToken);
 const wa=await accepted(await cloud(f,'/recover',a.managementToken,{expectedWorkspaceId:w.workspaceId}),201);
 const na=await accepted(await cloud(f,'/notes/recover',a.managementToken,{expectedNotebookId:n.notebookId}),201);
 assert.equal(wa.workspaceId,w.workspaceId);assert.equal(wa.executorEnabled,false);assert.equal(na.notebookId,n.notebookId);
 assert.deepEqual(await accepted(await cloud(f,'/notes/export',na.sessionToken)),book);
 assert.equal((await cloud(f,'/workspace',w.sessionToken)).status,200);assert.equal((await cloud(f,'/notes/export',n.sessionToken)).status,200);
 for(const token of [p.managementToken,a.managementToken,b.managementToken])assert.deepEqual((await accepted(await auth(f,`/profiles/${p.profileId}/status`,token))).permissions,[]);
 assert.deepEqual(await profile(f,p),before);assert.deepEqual((await f.workspaceDb.prepare('SELECT * FROM workspaces').all()).results,workspace);assert.deepEqual((await f.workspaceDb.prepare('SELECT * FROM notes_notebooks').all()).results,notes);
 assert.equal((await f.db.prepare('SELECT count(*) n FROM linking_tickets').first()).n,0);
});

test('legacy bootstrap is explicit and never creates or reactivates an enrollment',async t=>{
 const f=await fixture(t),p=await enrolled(f),before=await profile(f,p);
 assert.equal((await recover(f,p,{descriptor:undefined})).status,403);
 const result=await accepted(await recover(f,p,{descriptor:undefined,bootstrapAcknowledged:true}));assert.deepEqual(result.descriptor,p.descriptor);assert.deepEqual(await profile(f,p),before);
 assert.equal((await auth(f,'/recover',null,{...accounts[1],bootstrapAcknowledged:true})).status,404);
 await f.db.prepare('UPDATE integration_profiles SET revoked_at_utc=? WHERE profile_id=?').bind(new Date().toISOString(),p.profileId).run();
 assert.equal((await recover(f,p)).status,403);assert.ok((await profile(f,p)).revoked_at_utc);
 await f.db.prepare('DELETE FROM integration_profiles WHERE profile_id=?').bind(p.profileId).run();
 assert.equal((await recover(f,p)).status,404);assert.equal((await f.db.prepare('SELECT count(*) n FROM integration_profiles').first()).n,0);
});

test('stale/foreign descriptors, changed credentials, unsupported consent and hostile origins refuse recovery',async t=>{
 const f=await fixture(t),p=await enrolled(f),before=await profile(f,p);
 assert.equal((await recover(f,p,{descriptor:{...p.descriptor,connectionId:crypto.randomUUID()}})).status,409);
 assert.equal((await recover(f,p,{descriptor:{...p.descriptor,profileId:'prf_'+'b'.repeat(43)}})).status,409);
 assert.equal((await recover(f,p,{apiToken:'different-current-key'})).status,409);
 await f.db.prepare("UPDATE integration_profiles SET consent_version='unsupported' WHERE profile_id=?").bind(p.profileId).run();
 assert.equal((await recover(f,p)).status,403);
 const r=await f.auth.fetch(issuer+'/recover',{method:'POST',headers:{origin:'https://hostile.invalid','content-type':'application/json'},body:JSON.stringify({...accounts[0],descriptor:p.descriptor})});assert.equal(r.status,403);
 const after=await profile(f,p);assert.equal(after.management_token_hash,before.management_token_hash);assert.equal(after.credential_ciphertext,before.credential_ciphertext);assert.equal(after.consented_at_utc,before.consented_at_utc);
});

test('expired and management-rotated recovered sessions are unusable; healthy tabs remain independent',async t=>{
 const f=await fixture(t),p=await enrolled(f),a=await accepted(await recover(f,p)),b=await accepted(await recover(f,p));
 await f.db.prepare("UPDATE integration_recovery_sessions SET expires_at_utc='2000-01-01T00:00:00.000Z' WHERE token_hash<>(SELECT token_hash FROM integration_recovery_sessions ORDER BY rowid DESC LIMIT 1)").run();
 assert.equal((await auth(f,`/profiles/${p.profileId}/status`,a.managementToken)).status,401);
 assert.equal((await auth(f,`/profiles/${p.profileId}/status`,b.managementToken)).status,200);
 const rotated=await accepted(await auth(f,`/profiles/${p.profileId}/rotate-management`,b.managementToken,{confirm:'rotate-management-token'}));
 for(const token of [p.managementToken,a.managementToken,b.managementToken])assert.equal((await auth(f,`/profiles/${p.profileId}/status`,token)).status,401);
 assert.equal((await auth(f,`/profiles/${p.profileId}/status`,rotated.managementToken)).status,200);
 const c=await accepted(await recover(f,p));assert.equal((await auth(f,`/profiles/${p.profileId}/status`,c.managementToken)).status,200);
});

test('missing/disconnected/unsupported Workspace and Notes never become active during recovery',async t=>{
 const f=await fixture(t,{workspace:true}),p=await enrolled(f),r=await accepted(await recover(f,p));
 for(const path of ['/recover','/notes/recover'])assert.equal((await cloud(f,path,r.managementToken,{})).status,404);
 for(const table of ['workspaces','notes_notebooks'])assert.equal((await f.workspaceDb.prepare(`SELECT count(*) n FROM ${table}`).first()).n,0);
 const w=await accepted(await cloud(f,'/connect',p.managementToken,{consentVersion:'workspace-v1'}),201),n=await accepted(await cloud(f,'/notes/connect',p.managementToken,{consentVersion:'notes-v1'}),201);
 assert.equal((await cloud(f,'/recover',r.managementToken,{expectedWorkspaceId:crypto.randomUUID()})).status,409);
 assert.equal((await cloud(f,'/notes/recover',r.managementToken,{expectedNotebookId:crypto.randomUUID()})).status,409);
 for(const table of ['workspaces','notes_notebooks'])await f.workspaceDb.prepare(`UPDATE ${table} SET active=0`).run();
 for(const path of ['/recover','/notes/recover'])assert.equal((await cloud(f,path,r.managementToken,{})).status,403);
 for(const table of ['workspaces','notes_notebooks'])await f.workspaceDb.prepare(`UPDATE ${table} SET active=1,consent_version='unsupported'`).run();
 for(const path of ['/recover','/notes/recover'])assert.equal((await cloud(f,path,r.managementToken,{})).status,403);
 assert.equal((await f.workspaceDb.prepare('SELECT workspace_id FROM workspaces').first()).workspace_id,w.workspaceId);assert.equal((await f.workspaceDb.prepare('SELECT notebook_id FROM notes_notebooks').first()).notebook_id,n.notebookId);
});

for(const [path,table] of [['/recover','workspaces'],['/notes/recover','notes_notebooks']])test(`${path} refuses a session when access changes during the private epoch roundtrip`,async t=>{
 const f=await fixture(t,{workspace:true,identityGate:true}),p=await enrolled(f);
 await accepted(await cloud(f,'/connect',p.managementToken,{consentVersion:'workspace-v1'}),201);await accepted(await cloud(f,'/notes/connect',p.managementToken,{consentVersion:'notes-v1'}),201);
 const gate=await f.mf.getWorker('identity-gate'),control=(path,method='POST')=>gate.fetch('http://gate.test/__gate/'+path,{method});
 await control('arm');const pending=cloud(f,path,p.managementToken,{});
 try {await control('entered','GET');await f.workspaceDb.prepare(`UPDATE ${table} SET active=0,access_epoch=?`).bind(crypto.randomUUID()).run();}
 finally {await control('release');}
 assert.equal((await pending).status,409);
});

test('revocation during credential verification issues no recovered authority',async t=>{
 let f,armed=false;
 f=await fixture(t,{habitica:async request=>{
  if(armed)await f.db.prepare('UPDATE integration_profiles SET revoked_at_utc=?').bind(new Date().toISOString()).run();
  return Response.json({success:true,data:{_id:request.headers.get('x-api-user')}});
 }});
 const p=await enrolled(f);armed=true;assert.equal((await recover(f,p)).status,403);
 assert.equal((await f.db.prepare('SELECT count(*) n FROM integration_recovery_sessions').first()).n,0);
});

test('recovery quota and bounded concurrent-session retention do not rotate the root credential',async t=>{
 const f=await fixture(t),p=await enrolled(f),before=await profile(f,p);const sessions=[];
 for(let i=0;i<9;i++){
  // Advance the fixture admission window without weakening production quota.
  await f.db.prepare("DELETE FROM authorization_attempts WHERE bucket LIKE 'traffic:recovery:profile:%'").run();
  sessions.push(await accepted(await recover(f,p)));
 }
 assert.equal((await f.db.prepare('SELECT count(*) n FROM integration_recovery_sessions').first()).n,8);
 assert.equal((await auth(f,`/profiles/${p.profileId}/status`,sessions[0].managementToken)).status,401);
 assert.equal((await auth(f,`/profiles/${p.profileId}/status`,sessions.at(-1).managementToken)).status,200);
 assert.deepEqual(await profile(f,p),before);
 for(let i=0;i<4;i++)await accepted(await recover(f,p));
 const limited=await recover(f,p);assert.equal(limited.status,429);assert.ok(Number(limited.headers.get('retry-after'))>0);
});

test('upstream outage fails recovery without mutating enrollment',async t=>{
 let unavailable=false;
 const f=await fixture(t,{habitica:request=>unavailable?new Response(null,{status:503}):Response.json({success:true,data:{_id:request.headers.get('x-api-user')}})}),p=await enrolled(f),before=await profile(f,p);
 unavailable=true;assert.equal((await recover(f,p)).status,502);assert.deepEqual(await profile(f,p),before);
});

test('recovery observes retained OAuth permissions and never recreates revoked grants',async t=>{
 const f=await fixture(t),p=await enrolled(f);
 await accepted(await exchange(f,await authorization(f,p,'profile:read tasks:read')));
 const before=await accepted(await auth(f,`/profiles/${p.profileId}/status`,p.managementToken));
 const a=await accepted(await recover(f,p));
 assert.deepEqual((await accepted(await auth(f,`/profiles/${p.profileId}/status`,a.managementToken))).permissions,before.permissions);
 await accepted(await auth(f,`/profiles/${p.profileId}/revoke-grants`,p.managementToken,{confirm:'revoke-chatgpt-grants'}));
 const b=await accepted(await recover(f,p));
 assert.deepEqual((await accepted(await auth(f,`/profiles/${p.profileId}/status`,b.managementToken))).permissions,[]);
});

for(const [path,table,readPath] of [['/recover','workspaces','/workspace'],['/notes/recover','notes_notebooks','/notes/export']])test(`${path} failed stale issuance preserves a newer parallel session`,async t=>{
 const f=await fixture(t,{workspace:true,identityGate:true}),p=await enrolled(f);
 await accepted(await cloud(f,'/connect',p.managementToken,{consentVersion:'workspace-v1'}),201);await accepted(await cloud(f,'/notes/connect',p.managementToken,{consentVersion:'notes-v1'}),201);
 const gate=await f.mf.getWorker('identity-gate'),control=(path,method='POST')=>gate.fetch('http://gate.test/__gate/'+path,{method});
 await control('arm');const stale=cloud(f,path,p.managementToken,{});let fresh;
 try{
  await control('entered','GET');await f.workspaceDb.prepare(`UPDATE ${table} SET access_epoch=?`).bind(crypto.randomUUID()).run();
  fresh=await accepted(await cloud(f,path,p.managementToken,{}),201);
 }finally{await control('release');}
 assert.equal((await stale).status,409);
 assert.equal((await cloud(f,readPath,fresh.sessionToken)).status,200);
});
