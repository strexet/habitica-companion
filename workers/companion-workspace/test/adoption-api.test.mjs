import test from 'node:test';
import assert from 'node:assert/strict';
import {Client} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js';
import {StreamableHTTPClientTransport} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/streamableHttp.js';
import {fixture,enrolled,accounts,authorization,exchange,resource} from '../../chatgpt-auth/test/fixtures/oauth.mjs';
import {seedLegacyCommand} from './fixtures/legacy-todo.mjs';
const origin='http://localhost:8789';
async function setup(t,{kind='todo',completed=false,archived=false,...options}={}){
 let reads=0,writes=0,hook=null;
 const native={id:crypto.randomUUID(),userId:accounts[0].habiticaUserId,type:kind,text:'Exact native',notes:'Native notes',priority:1,attribute:'str',completed,up:true,down:true,checklist:[],group:{},challenge:{}};
 const f=await fixture(t,{workspace:true,projection:true,...options,habitica:async request=>{
  if(request.method!=='GET')writes++;
  if(new URL(request.url).pathname.endsWith('/user'))return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:request.headers.get('x-api-user')===accounts[0].habiticaUserId?'Fixture A':'Fixture B'}}});
  reads++;if(hook)await hook();return Response.json({success:true,data:native});
 }}),p=await enrolled(f),connected=await f.workspace.fetch(origin+'/connect',{method:'POST',headers:{authorization:'Bearer '+p.managementToken,'content-type':'application/json'},body:JSON.stringify({consentVersion:'workspace-v1'})});assert.equal(connected.status,201);const w=await connected.json(),taskId=crypto.randomUUID();
 await seedLegacyCommand(f,{id:crypto.randomUUID(),workspaceId:w.workspaceId,expectedRevision:1,operation:{todo:'createTodo',daily:'createDaily',habit:'createHabit'}[kind],taskId,patch:{text:'Canonical record',notes:'Canonical notes'}});
 if(completed)await seedLegacyCommand(f,{id:crypto.randomUUID(),workspaceId:w.workspaceId,expectedRevision:2,operation:'completeTask',taskId});
 if(archived){const row=await f.workspaceDb.prepare('SELECT document_json FROM workspaces WHERE profile_id=?').bind(p.profileId).first(),d=JSON.parse(row.document_json);d.tasks[0].isArchived=true;await f.workspaceDb.prepare('UPDATE workspaces SET document_json=? WHERE profile_id=?').bind(JSON.stringify(d),p.profileId).run();}
 const api=async(tool,args,session=w)=>{const response=await f.workspace.fetch(origin+'/adoption-api',{method:'POST',headers:{authorization:'Bearer '+session.sessionToken,'content-type':'application/json'},body:JSON.stringify({tool,arguments:args})});return {status:response.status,body:await response.json()};};
 const request={requestId:crypto.randomUUID(),taskId,habiticaTaskId:native.id};
 const prepare=async(args=request)=>{const r=await api('prepare_task_adoption',args);assert.equal(r.status,200,JSON.stringify(r));return r.body;};
 const accept=review=>api('accept_task_adoption',{commandId:review.commandId,reviewHash:review.reviewHash});
 const doc=async()=>JSON.parse((await f.workspaceDb.prepare('SELECT document_json FROM workspaces WHERE profile_id=?').bind(p.profileId).first()).document_json);
 return {f,p,w,taskId,native,api,request,prepare,accept,doc,get reads(){return reads;},get writes(){return writes;},set hook(v){hook=v;}};
}
for(const mode of [{kind:'todo'},{kind:'todo',completed:true,archived:true},{kind:'daily'},{kind:'habit'}])test('exact adoption retains fields/history and immutable original receipt '+JSON.stringify(mode),async t=>{
 const x=await setup(t,mode),before=await x.doc(),review=await x.prepare();assert.equal(review.nativeWrites,0);assert.equal(review.gameEffects,0);assert.deepEqual(review.task,before.tasks[0]);assert.equal(review.external.id,x.native.id);
 const retry=await x.prepare();assert.deepEqual(retry,review);assert.equal(x.reads,1);
 const accepted=await x.accept(review);assert.equal(accepted.status,200,JSON.stringify(accepted));assert.equal(accepted.body.state,'accepted');const after=await x.doc();assert.deepEqual(after.tasks,before.tasks);assert.deepEqual(after.completions,before.completions);assert.deepEqual(after.habitScores,before.habitScores);assert.equal(after.projections[0].id,review.mappingAfter.id);
 after.revision++;after.projections[0].revision=after.revision;after.projections[0].fingerprint='b'.repeat(64);await x.f.workspaceDb.prepare('UPDATE workspaces SET document_json=? WHERE profile_id=?').bind(JSON.stringify(after),x.p.profileId).run();
 const readCount=x.reads;assert.deepEqual((await x.accept(review)).body,accepted.body);assert.deepEqual((await x.api('get_task_adoption_command',{commandId:review.commandId})).body,accepted.body);assert.equal(x.reads,readCount);assert.equal(x.writes,0);
});
for(const mode of ['kind','completed','owner','exact-id','shared','duplicate','conflicting'])test('adoption refuses uncertain native identity '+mode,async t=>{
 const x=await setup(t);
 if(mode==='kind')x.native.type='habit';if(mode==='completed')x.native.completed=true;if(mode==='owner')x.native.userId=accounts[1].habiticaUserId;if(mode==='exact-id')x.native.id=crypto.randomUUID();if(mode==='shared')x.native.group={id:crypto.randomUUID()};
 if(['duplicate','conflicting'].includes(mode)){
  const d=await x.doc();let mapped=x.taskId;
  if(mode==='duplicate'){const id=crypto.randomUUID();d.tasks.push({...d.tasks[0],id});mapped=id;}
  d.projections=[{id:crypto.randomUUID(),revision:d.revision,taskId:mapped,state:'linked',habiticaTaskId:mode==='duplicate'?x.native.id:crypto.randomUUID(),fingerprint:'a'.repeat(64),isArchived:false}];
  await x.f.workspaceDb.prepare('UPDATE workspaces SET document_json=? WHERE profile_id=?').bind(JSON.stringify(d),x.p.profileId).run();
 }
 const before=await x.doc(),r=await x.api('prepare_task_adoption',x.request);assert.notEqual(r.status,200,JSON.stringify(r));assert.deepEqual(await x.doc(),before);assert.equal(x.writes,0);
});
for(const mode of ['native','revision','expired','access','grant','unknown','lease','hash','rollback'])test('adoption fresh review and atomic controls '+mode,async t=>{
 const x=await setup(t),review=await x.prepare(),before=await x.doc();
 if(mode==='native')x.native.notes='Changed';
 if(mode==='revision'){const d=await x.doc();d.revision++;await x.f.workspaceDb.prepare('UPDATE workspaces SET document_json=? WHERE profile_id=?').bind(JSON.stringify(d),x.p.profileId).run();}
 if(mode==='expired')await x.f.workspaceDb.prepare("UPDATE workspace_task_adoptions SET expires_at_utc='2000-01-01T00:00:00Z'").run();
 if(mode==='access')await x.f.workspaceDb.prepare('UPDATE workspaces SET access_epoch=?').bind(crypto.randomUUID()).run();
 if(mode==='grant')await x.f.workspaceDb.prepare("UPDATE workspace_task_adoptions SET grant_epoch='changed'").run();
 if(mode==='unknown'){const row=await x.f.workspaceDb.prepare('SELECT command_id,entry_json FROM workspace_commands').first();const entry=JSON.parse(row.entry_json);entry.projectionState='unknownOutcome';await x.f.workspaceDb.prepare('UPDATE workspace_commands SET entry_json=? WHERE command_id=?').bind(JSON.stringify(entry),row.command_id).run();}
 if(mode==='lease')await x.f.workspaceDb.prepare('UPDATE workspace_runtime SET lease_token=?,lease_until_utc=?').bind(crypto.randomUUID(),new Date(Date.now()+90000).toISOString()).run();
 if(mode==='rollback')await x.f.workspaceDb.prepare("CREATE TRIGGER fixture_adoption_fail BEFORE UPDATE OF receipt_json ON workspace_task_adoptions BEGIN SELECT RAISE(ABORT,'synthetic receipt rollback'); END").run();
 const r=mode==='hash'?await x.api('accept_task_adoption',{commandId:review.commandId,reviewHash:'b'.repeat(64)}):await x.accept(review);
 assert.notEqual(r.body.state,'accepted',JSON.stringify(r));assert.equal(x.writes,0);assert.deepEqual((await x.doc()).projections,before.projections);
 if(['native','revision','expired','grant','unknown'].includes(mode)){assert.equal(r.body.state,'refused');assert.equal(r.body.outcomeUnknown,false);assert.deepEqual((await x.api('get_task_adoption_command',{commandId:review.commandId})).body,r.body);}
 if(mode==='rollback'){assert.equal(r.status,503);assert.equal((await x.f.workspaceDb.prepare('SELECT state FROM workspace_task_adoptions').first()).state,'prepared');await x.f.workspaceDb.prepare('DROP TRIGGER fixture_adoption_fail').run();assert.equal((await x.accept(review)).body.state,'accepted');}
});
test('adoption detects owner/access or revision changes during fresh native inspection',async t=>{
 const x=await setup(t),r=await x.prepare();x.hook=async()=>{await x.f.workspaceDb.prepare('UPDATE workspaces SET store_version=store_version+1,access_epoch=?').bind(crypto.randomUUID()).run();};assert.notEqual((await x.accept(r)).body.state,'accepted');assert.equal(x.writes,0);
});
test('adoption MCP SDK schemas, scope, two profiles, durable acknowledgement recovery and no selectors',async t=>{
 const x=await setup(t);
 const client=async(p,scope='profile:read tasks:read habitica.tasks.write')=>{const token=await exchange(x.f,await authorization(x.f,p,scope));assert.equal(token.status,200);const c=new Client({name:'adoption-fixture',version:'1.0.0'});t.after(()=>c.close());await c.connect(new StreamableHTTPClientTransport(new URL(resource),{requestInit:{headers:{authorization:'Bearer '+(await token.json()).access_token}},fetch:(url,init)=>x.f.mcp.fetch(String(url),init)}));return c;};
 const c=await client(x.p),tools=(await c.listTools()).tools;for(const name of ['prepare_task_adoption','accept_task_adoption','get_task_adoption_command'])assert.ok(tools.some(t=>t.name===name));
 const invoke=async(name,args)=>{const r=await c.callTool({name,arguments:args});assert.notEqual(r.isError,true,JSON.stringify(r));return r.structuredContent;};
 const review=await invoke('prepare_task_adoption',x.request);const accepted=await invoke('accept_task_adoption',{commandId:review.commandId,reviewHash:review.reviewHash});assert.equal(accepted.state,'accepted');assert.deepEqual(await invoke('get_task_adoption_command',{commandId:review.commandId}),accepted);
 assert.equal((await c.callTool({name:'prepare_task_adoption',arguments:{...x.request,profileId:crypto.randomUUID()}})).isError,true);
 const other=await enrolled(x.f,accounts[1]),connected=await x.f.workspace.fetch(origin+'/connect',{method:'POST',headers:{authorization:'Bearer '+other.managementToken,'content-type':'application/json'},body:JSON.stringify({consentVersion:'workspace-v1'})});assert.equal(connected.status,201);const otherW=await connected.json();
 const foreign=await x.api('get_task_adoption_command',{commandId:review.commandId},otherW);assert.equal(foreign.body.state,'unavailable');assert.equal(foreign.body.outcomeUnknown,true);assert.equal(JSON.stringify(foreign).includes(x.taskId),false);
 assert.notEqual((await x.api('prepare_task_adoption',{...x.request,requestId:crypto.randomUUID()},otherW)).status,200);
 const read=await client(x.p,'profile:read tasks:read');await assert.rejects(read.callTool({name:'accept_task_adoption',arguments:{commandId:review.commandId,reviewHash:review.reviewHash}}),/403|insufficient_scope/);assert.equal(x.writes,0);
});

test('lost MCP acknowledgement recovers the original immutable adoption without another inspect or save',async t=>{
 let x,lose=true; x=await setup(t,{workspaceServer:async request=>{
  const body=await request.clone().json(),binding=(await x.f.mf.getBindings('mcp')).WORKSPACE_TEST_DIRECT,response=await binding.fetch(request);
  if(body.tool==='accept_task_adoption'&&response.ok&&lose){lose=false;throw new Error('Synthetic lost adoption acknowledgement');}return response;
 }});
 const token=await exchange(x.f,await authorization(x.f,x.p,'profile:read tasks:read habitica.tasks.write')),c=new Client({name:'lost-adoption-fixture',version:'1.0.0'});t.after(()=>c.close());await c.connect(new StreamableHTTPClientTransport(new URL(resource),{requestInit:{headers:{authorization:'Bearer '+(await token.json()).access_token}},fetch:(url,init)=>x.f.mcp.fetch(String(url),init)}));
 const prepared=await c.callTool({name:'prepare_task_adoption',arguments:x.request});assert.notEqual(prepared.isError,true,JSON.stringify(prepared));const review=prepared.structuredContent;
 const uncertain=await c.callTool({name:'accept_task_adoption',arguments:{commandId:review.commandId,reviewHash:review.reviewHash}});assert.equal(uncertain.isError,true);assert.equal(uncertain.structuredContent.outcomeUnknown,true);
 const before=await x.doc(),reads=x.reads,recovered=await c.callTool({name:'get_task_adoption_command',arguments:{commandId:review.commandId}});assert.notEqual(recovered.isError,true);assert.equal(recovered.structuredContent.state,'accepted');assert.equal(recovered.structuredContent.receipt.revision,before.revision);assert.equal(x.reads,reads);assert.deepEqual(await x.doc(),before);assert.equal(x.writes,0);
});
test('prepared adoption binds grant epoch and accepted original receipt survives grant renewal',async t=>{
 const x=await setup(t),env=await x.f.mf.getBindings('workspace');
 const {adoptionApi}=await import('../src/adoption-api.ts');
 let row=await x.f.workspaceDb.prepare('SELECT * FROM workspaces WHERE profile_id=?').bind(x.p.profileId).first();
 await x.f.workspaceDb.prepare("UPDATE workspaces SET mcp_grant_epoch='1' WHERE profile_id=?").bind(x.p.profileId).run();row={...row,mcp_grant_epoch:'1'};
 // A stale grant is rejected by Auth before native inspection, independently of the Workspace cache.
 await x.f.db.prepare("UPDATE integration_profiles SET grant_epoch='2' WHERE profile_id=?").bind(x.p.profileId).run();
 await assert.rejects(adoptionApi(env,row,'prepare_task_adoption',x.request,{source:'mcp',grantEpoch:'1'}));assert.equal(x.writes,0);
 const review=await x.prepare();await x.f.workspaceDb.prepare("UPDATE workspace_task_adoptions SET origin='mcp',grant_epoch='1' WHERE profile_id=?").bind(x.p.profileId).run();
 await x.f.workspaceDb.prepare("UPDATE workspaces SET mcp_grant_epoch='2' WHERE profile_id=?").bind(x.p.profileId).run();row={...row,mcp_grant_epoch:'2'};
 const refused=await adoptionApi(env,row,'accept_task_adoption',{commandId:review.commandId,reviewHash:review.reviewHash},{source:'mcp',grantEpoch:'2'});assert.equal(refused.state,'refused');
 const fresh=await x.prepare({...x.request,requestId:crypto.randomUUID()}),accepted=(await x.accept(fresh)).body;assert.equal(accepted.state,'accepted');
 await x.f.workspaceDb.prepare("UPDATE workspaces SET mcp_grant_epoch='3' WHERE profile_id=?").bind(x.p.profileId).run();row={...row,mcp_grant_epoch:'3'};
 assert.deepEqual(await adoptionApi(env,row,'get_task_adoption_command',{commandId:fresh.commandId},{source:'mcp',grantEpoch:'3'}),accepted);assert.equal(x.writes,0);
});

test('unknown Auth attempts and reused request IDs refuse adoption before any native mutation',async t=>{
 const x=await setup(t),review=await x.prepare();
 assert.equal((await x.api('prepare_task_adoption',{...x.request,habiticaTaskId:crypto.randomUUID()})).status,409);
 await x.f.db.prepare("INSERT INTO integration_projection_attempts(profile_id,connection_id,attempt_id,payload_hash,operation,state,created_at_utc) VALUES(?,?,?,?,?,'unknown',?)").bind(x.p.profileId,(await x.f.workspaceDb.prepare('SELECT connection_id FROM workspaces WHERE profile_id=?').bind(x.p.profileId).first()).connection_id,crypto.randomUUID(),'a'.repeat(64),'score',new Date().toISOString()).run();
 const response=await x.accept(review);assert.equal(response.body.state,'refused',JSON.stringify(response));assert.equal(response.body.error,'unknown_outcome_pending');assert.equal(x.writes,0);
});

test('adoption review capacity and archive accounting stay bounded and workspace deletion cascades receipts',async t=>{
 const x=await setup(t),before=await x.f.workspaceDb.prepare('SELECT archive_bytes FROM workspaces WHERE profile_id=?').bind(x.p.profileId).first();
 const reviews=[];for(let i=0;i<8;i++)reviews.push(await x.prepare({...x.request,requestId:crypto.randomUUID()}));
 assert.equal((await x.api('prepare_task_adoption',{...x.request,requestId:crypto.randomUUID()})).status,409);
 const row=await x.f.workspaceDb.prepare('SELECT archive_bytes FROM workspaces WHERE profile_id=?').bind(x.p.profileId).first(),sizes=await x.f.workspaceDb.prepare('SELECT sum(length(CAST(review_json AS BLOB))+1024) AS size FROM workspace_task_adoptions WHERE profile_id=?').bind(x.p.profileId).first();assert.equal(row.archive_bytes-before.archive_bytes,sizes.size);
 await x.f.workspaceDb.prepare('UPDATE workspaces SET archive_bytes=8388608 WHERE profile_id=?').bind(x.p.profileId).run();assert.notEqual((await x.accept(reviews[0])).body.state,'accepted');
 await x.f.workspaceDb.prepare('DELETE FROM workspaces WHERE profile_id=?').bind(x.p.profileId).run();assert.equal((await x.f.workspaceDb.prepare('SELECT count(*) AS n FROM workspace_task_adoptions WHERE profile_id=?').bind(x.p.profileId).first()).n,0);assert.equal(x.writes,0);
});

test('altered persisted adoption review cannot bypass the original reviewed hash',async t=>{
 const x=await setup(t),review=await x.prepare(),before=await x.doc(),stored=await x.f.workspaceDb.prepare('SELECT review_json FROM workspace_task_adoptions WHERE profile_id=?').bind(x.p.profileId).first(),payload=JSON.parse(stored.review_json);payload.mappingAfter.id=crypto.randomUUID();await x.f.workspaceDb.prepare('UPDATE workspace_task_adoptions SET review_json=? WHERE profile_id=?').bind(JSON.stringify(payload),x.p.profileId).run();
 const refused=await x.accept(review);assert.equal(refused.body.state,'refused');assert.equal(refused.body.error,'adoption_review_corrupt');assert.deepEqual(await x.doc(),before);assert.equal(x.writes,0);
});
