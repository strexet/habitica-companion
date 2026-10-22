import test from 'node:test';
import assert from 'node:assert/strict';
import {fixture,enrolled,issuer,authorization,exchange} from '../../chatgpt-auth/test/fixtures/oauth.mjs';
import {workspaceDocument} from '../../shared/workspace-domain.ts';
import {executeProfile} from '../src/executor.ts';
import {journal} from '../src/journal.ts';
const origin='http://localhost:8789';
async function setup(t,options={}){
 const tags=[],external=crypto.randomUUID(),remote={id:crypto.randomUUID(),type:'todo',text:'Synthetic task',notes:'',priority:1,attribute:'str',completed:false,checklist:[],group:{},challenge:{},tags:[external]},calls=[];let f;
 f=await fixture(t,{workspace:true,projection:true,executor:true,...options,habitica:async request=>{
  if(request.url.includes('user?userFields=_id,profile'))return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:'Fixture A'}}});
  remote.userId??=request.headers.get('x-api-user');calls.push({method:request.method,path:new URL(request.url).pathname});
  if(options.beforeRequest)await options.beforeRequest(request,f);
  if(request.method==='GET')return Response.json({success:true,data:request.url.endsWith('/tags')?tags:remote});
  if(request.url.endsWith('/tags')){tags.push(await request.json());if(options.unknownCreate)return new Response('',{status:502});return Response.json({success:true,data:tags.at(-1)});}
  if(request.url.includes('/tasks/')){const id=request.url.split('/').at(-1);remote.tags=request.method==='POST'?[...new Set([...remote.tags,id])]:remote.tags.filter(value=>value!==id);return Response.json({success:true,data:remote});}
  const tag=tags.find(tag=>request.url.endsWith('/'+tag.id));Object.assign(tag,await request.json());return Response.json({success:true,data:tag});
 }});
 const p=await enrolled(f),call=async(path,body)=>f.workspace.fetch(origin+path,{method:body===undefined?'GET':'POST',headers:{authorization:'Bearer '+w.sessionToken,'content-type':'application/json'},body:body===undefined?undefined:JSON.stringify(body)});
 const connect=await f.workspace.fetch(origin+'/connect',{method:'POST',headers:{authorization:'Bearer '+p.managementToken,'content-type':'application/json'},body:JSON.stringify({consentVersion:'workspace-v1'})});assert.equal(connect.status,201);const w=await connect.json();
 const taskId=crypto.randomUUID(),document=workspaceDocument({...JSON.parse((await f.workspaceDb.prepare('SELECT document_json FROM workspaces').first()).document_json),tasks:[{id:taskId,revision:1,kind:'todo',state:'active',text:remote.text,priority:'normal',createdAtUtc:new Date().toISOString(),updatedAtUtc:new Date().toISOString()}],projections:[{id:crypto.randomUUID(),revision:1,taskId,state:'linked',habiticaTaskId:remote.id,fingerprint:'a'.repeat(64)}]});
 const serialized=JSON.stringify(document);await f.workspaceDb.prepare('UPDATE workspaces SET document_json=?,archive_bytes=?').bind(serialized,new TextEncoder().encode(serialized).length).run();
 const consent=(enabled=true)=>f.auth.fetch(`${issuer}/profiles/${p.profileId}/tag-consent`,{method:'POST',headers:{authorization:'Bearer '+p.managementToken,'content-type':'application/json'},body:JSON.stringify({consentVersion:'habitica-tags-v1',enabled})});
 const org=async(tool,args)=>{const r=await call('/organization-api',{tool,arguments:args});assert.equal(r.status,200,await r.clone().text());return r.json();};
 const prepare=(extra={})=>org('prepare_organization_command',{requestId:crypto.randomUUID(),kind:'goal',operation:'create',patch:{name:'Owned goal',taskIds:[taskId]},...extra});
 const accept=id=>org('accept_organization_command',{commandId:id});
 const env=await f.mf.getBindings('workspace');
 const wait=async()=>{await f.workspaceDb.prepare("UPDATE workspace_runtime SET next_network_at_utc='2000-01-01T00:00:00Z'").run();await f.db.prepare("UPDATE integration_projection_state SET next_automated_at_utc='2000-01-01T00:00:00Z'").run();};
 const wake=()=>executeProfile(env,p.profileId,{wait});
 const enable=async()=>assert.equal((await call('/executor',{enabled:true,consentVersion:'executor-v1'})).status,200);
 const exported=async()=>{const r=await call('/export');assert.equal(r.status,200);return r.json();};
 return {f,p,w,call,org,prepare,accept,consent,enable,wake,wait,exported,env,taskId,tags,remote,external,calls};
}
const writes=x=>x.calls.filter(call=>call.method!=='GET');
test('accepted native changes atomically retain review gaps and duplicate receipts without implicit tag consent or scoring',async t=>{
 const x=await setup(t),a=await x.prepare(),r=await x.accept(a.preparedCommandId),before=await x.exported();
 assert.equal(r.savedInCompanion,true);assert.equal((await x.accept(a.preparedCommandId)).commandId,r.commandId);assert.deepEqual(await x.exported(),before);await journal(before);
 const status=await(await x.call('/tag-projection')).json();assert.equal(status.status.gap,'tag_consent_required');assert.equal(status.work.length,2);assert.ok(status.work.every(row=>row.state==='needsReview'));assert.equal(writes(x).length,0);assert.equal(before.commands.length,0);assert.equal(before.document.completions.length,0);
 await x.consent();await x.enable();assert.equal(await x.wake(),'idle');assert.equal(writes(x).length,0); // New consent cannot reinterpret accepted old work.
});
test('complete group reconciliation preserves external tags, receipts and source revision across duplicate wakes',async t=>{
 const x=await setup(t);await x.consent();const a=await x.prepare();await x.accept(a.preparedCommandId);const revision=(await x.exported()).document.revision;await x.enable();
 assert.equal(await x.wake(),'tag_applied');assert.equal(await x.wake(),'tag_applied');assert.equal(await x.wake(),'idle');assert.equal(x.tags.length,1);assert.deepEqual(x.remote.tags,[x.external,x.tags[0].id]);assert.equal(writes(x).length,2);assert.ok(writes(x).every(call=>!call.path.includes('/score')&&!/^\/api\/v3\/tags\//.test(call.path)));
 const archive=await x.exported();assert.equal(archive.document.revision,revision);assert.ok(archive.document.tagLedger.intents.every(row=>row.state==='confirmed'));assert.equal(archive.document.tagLedger.intents[1].members[0].evidence.receiptId.length,36);assert.equal(archive.commands.length,0);await journal(archive);
 const registry=await x.f.db.prepare('SELECT tag_id FROM integration_owned_tags').first();assert.equal(registry.tag_id,x.tags[0].id);
});
test('tag authority is bound to the reviewed consent epoch, not consent acquired or replaced after preview',async t=>{
 const x=await setup(t),a=await x.prepare();await x.consent();await x.accept(a.preparedCommandId);let rows=(await(await x.call('/tag-projection')).json()).work;assert.ok(rows.every(row=>row.state==='needsReview'));await x.enable();assert.equal(await x.wake(),'idle');assert.equal(writes(x).length,0);
 const b=await x.prepare({patch:{name:'Fresh review',taskIds:[x.taskId]}});await x.accept(b.preparedCommandId);await x.consent();assert.equal(await x.wake(),'tag_review');assert.equal(writes(x).length,0);
});
test('MCP organization permission and account consent never substitute for the additive validated tag scope',async t=>{
 const x=await setup(t);await x.consent();const binding=(await x.f.mf.getBindings('mcp')).WORKSPACE_SERVER;
 for(const allowed of [false,true]){
  const token=await(await exchange(x.f,await authorization(x.f,x.p,'profile:read workspace.organization.write'+(allowed?' habitica.tags.write':'')))).json();
  const invoke=async(tool,args)=>{const r=await binding.fetch('https://workspace.internal/tool',{method:'POST',headers:{authorization:'Bearer '+token.access_token,'content-type':'application/json'},body:JSON.stringify({tool,arguments:args})});assert.equal(r.status,200,await r.clone().text());return r.json();};
  const a=await invoke('prepare_organization_command',{requestId:crypto.randomUUID(),kind:'goal',operation:'create',patch:{name:'MCP '+allowed,taskIds:[x.taskId]}});await invoke('accept_organization_command',{commandId:a.preparedCommandId});
  const rows=(await(await x.call('/tag-projection')).json()).work.filter(row=>row.command_id===a.preparedCommandId);assert.ok(rows.every(row=>row.state===(allowed?'pending':'needsReview')));
 }
 assert.equal(writes(x).length,0);
});
test('lost tag acknowledgement is recovered from the original Auth receipt without another mutation',async t=>{
 let f,lose=true;const x=await setup(t,{projectionService:async request=>{const body=await request.clone().json(),response=await f.projection.fetch(request);if(lose&&body.operation==='createTag'){lose=false;throw new Error('synthetic lost acknowledgement');}return response;}});f=x.f;
 await x.consent();await x.accept((await x.prepare()).preparedCommandId);await x.enable();assert.equal(await x.wake(),'tag_unknown');assert.equal(x.tags.length,1);assert.equal(await x.wake(),'tag_applied');assert.equal(x.tags.length,1);assert.equal(writes(x).length,1);assert.equal(await x.wake(),'tag_applied');assert.equal(writes(x).length,2);
});
test('unknown metadata send fences gameplay and repeated wakes, even after source changes and consent revocation',async t=>{
 const x=await setup(t,{unknownCreate:true});await x.consent();await x.accept((await x.prepare()).preparedCommandId);await x.enable();assert.equal(await x.wake(),'tag_unknown');
 const before=await x.exported();const c={id:crypto.randomUUID(),workspaceId:x.w.workspaceId,expectedRevision:before.document.revision,operation:'completeTask',taskId:x.taskId,projectToHabitica:true};assert.equal((await x.call('/commands',c)).status,201);
 await x.consent(false);assert.equal(await x.wake(),'tag_unknown');assert.equal(await x.wake(),'tag_unknown');assert.equal(writes(x).length,1);const after=await x.exported();assert.equal(after.commands[0].projectionState,'pending');assert.equal(after.document.tagLedger.intents[0].state,'unknownOutcome');
});
test('stale selections and missing trusted registry identities require review and never adopt imported IDs',async t=>{
 const x=await setup(t);await x.consent();const a=await x.prepare();await x.accept(a.preparedCommandId);await x.enable();assert.equal(await x.wake(),'tag_applied');
 await x.f.db.prepare('DELETE FROM integration_owned_tags').run();assert.equal(await x.wake(),'tag_review');assert.equal(writes(x).length,1);assert.deepEqual(x.remote.tags,[x.external]);
 const b=await x.prepare({operation:'update',entityId:a.command.entityId,patch:{taskIds:[]}});await x.accept(b.preparedCommandId);assert.equal(await x.wake(),'tag_review');assert.equal(writes(x).length,1);
});
test('shared executor lease rejects concurrent wakes and source changes during inspection stop dispatch',async t=>{
 let enter,release,held=true;const entered=new Promise(resolve=>enter=resolve),gate=new Promise(resolve=>release=resolve);
 const x=await setup(t,{beforeRequest:async request=>{if(held&&request.url.endsWith('/tags')){held=false;enter();await gate;}}});await x.consent();const a=await x.prepare();await x.accept(a.preparedCommandId);await x.enable();
 const first=x.wake();await entered;try{assert.equal(await x.wake(),'busy');const b=await x.prepare({operation:'update',entityId:a.command.entityId,patch:{name:'New name'}});await x.accept(b.preparedCommandId);}finally{release();}
 assert.equal(await first,'tag_review');assert.equal(writes(x).length,0);
});
test('logical membership removal touches only previously managed task tags and archival retains native provenance',async t=>{
 const x=await setup(t);await x.consent();const a=await x.prepare();await x.accept(a.preparedCommandId);await x.enable();assert.equal(await x.wake(),'tag_applied');assert.equal(await x.wake(),'tag_applied');
 const tagId=x.tags[0].id,b=await x.prepare({operation:'update',entityId:a.command.entityId,patch:{taskIds:[]}});assert.deepEqual(b.tagProjection.groups[0].removedTaskIds,[x.taskId]);assert.deepEqual(b.tagProjection.groups[0].taskIds,[]);await x.accept(b.preparedCommandId);assert.equal(await x.wake(),'tag_applied');assert.deepEqual(x.remote.tags,[x.external]);assert.equal(x.tags[0].id,tagId);assert.equal(writes(x).at(-1).method,'DELETE');assert.ok(writes(x).at(-1).path.includes('/tasks/'));
 const archive=await x.prepare({operation:'archive',entityId:a.command.entityId,patch:undefined});await x.accept(archive.preparedCommandId);assert.equal(await x.wake(),'idle');const exported=await x.exported();assert.equal(exported.document.tagLedger.mappings[0].habiticaTagId,tagId);assert.equal(exported.document.tagLedger.intents.length,3);assert.equal(x.tags.length,1);await journal(exported);
});
test('a complete selection above one hundred tasks persists all members and resumes one acknowledged member at a time',async t=>{
 const x=await setup(t);await x.consent();const row=await x.f.workspaceDb.prepare('SELECT document_json FROM workspaces').first(),base=workspaceDocument(JSON.parse(row.document_json)),tasks=Array.from({length:237},(_,i)=>({...base.tasks[0],id:crypto.randomUUID(),text:'Selected '+i}));
 // Synthetic fixture uses one real mapped target and 236 explicitly unmapped targets.
 tasks[0]=base.tasks[0];tasks[0].text='Selected first';const seeded=workspaceDocument({...base,tasks});const serialized=JSON.stringify(seeded);await x.f.workspaceDb.prepare('UPDATE workspaces SET document_json=?,archive_bytes=?').bind(serialized,new TextEncoder().encode(serialized).length).run();
 const a=await x.prepare({kind:'savedTaskView',patch:{name:'Complete set',query:{source:'tasks',filter:{}}}});assert.equal(a.tagProjection.groups[0].taskIds.length,237);await x.accept(a.preparedCommandId);await x.enable();assert.equal(await x.wake(),'tag_applied');assert.equal(await x.wake(),'tag_applied');assert.equal(await x.wake(),'tag_review');assert.equal(writes(x).length,2);
 const after=await x.exported(),intent=after.document.tagLedger.intents.find(row=>row.operation==='setMembership');assert.equal(intent.members.length,237);assert.equal(intent.members.filter(row=>row.state==='confirmed').length,1);assert.equal(intent.state,'needsReview');assert.equal(after.document.tasks.length,237);assert.equal(after.document.completions.length,0);assert.ok(writes(x).every(call=>call.path!=='/api/v3/tasks/user'));
});
test('empty selection is explicitly verified and tag work failure rolls the native save back atomically',async t=>{
 const x=await setup(t);await x.consent();const a=await x.prepare({patch:{name:'Empty',taskIds:[]}});
 await x.f.workspaceDb.prepare("CREATE TRIGGER fixture_tag_save_failure BEFORE INSERT ON workspace_tag_work BEGIN SELECT RAISE(ABORT,'synthetic tag work failure'); END").run();const before=await x.exported();assert.equal((await x.call('/organization-api',{tool:'accept_organization_command',arguments:{commandId:a.preparedCommandId}})).status,503);assert.deepEqual(await x.exported(),before);assert.equal((await x.f.workspaceDb.prepare('SELECT COUNT(*) AS n FROM workspace_tag_work').first()).n,0);
 await x.f.workspaceDb.prepare('DROP TRIGGER fixture_tag_save_failure').run();await x.accept(a.preparedCommandId);await x.enable();assert.equal(await x.wake(),'tag_applied');assert.equal(await x.wake(),'tag_confirmed');assert.equal(await x.wake(),'idle');assert.equal(writes(x).length,1);const after=await x.exported();assert.ok(after.document.tagLedger.intents.every(row=>row.state==='confirmed'));assert.deepEqual(after.document.tagLedger.intents[1].members,[]);
});
test('reviewed task organization changes disclose tag effects separately from zero gameplay effects',async t=>{
 const x=await setup(t);await x.consent();const label=await x.prepare({kind:'label',patch:{name:'Owned label'}});await x.accept(label.preparedCommandId);
 const args={requestId:crypto.randomUUID(),operation:'planTask',taskId:x.taskId,patch:{planning:{labelIds:[label.command.entityId]}},projectToHabitica:false};
 const response=await x.call('/task-api',{tool:'prepare_task_command',arguments:args});assert.equal(response.status,200,await response.clone().text());const preview=await response.json();assert.equal(preview.mayAffectHabitica,true);assert.equal(preview.tagProjection.groups.length,1);assert.deepEqual(preview.tagProjection.groups[0].taskIds,[x.taskId]);
 const result=await x.call('/task-api',{tool:'plan_task',arguments:{commandId:preview.preparedCommandId}});assert.equal(result.status,200,await result.clone().text());const receipt=await result.json();assert.equal(receipt.acceptedEventCount,0);assert.equal(receipt.totalProjectionCount,0);
 const status=await(await x.call('/tag-projection')).json();assert.ok(status.work.some(row=>row.command_id===preview.preparedCommandId&&row.state==='pending'));assert.equal(writes(x).length,0);
});
test('external native tag rename remains a review conflict rather than an automatic overwrite',async t=>{
 const x=await setup(t);await x.consent();const a=await x.prepare();await x.accept(a.preparedCommandId);await x.enable();assert.equal(await x.wake(),'tag_applied');assert.equal(await x.wake(),'tag_applied');
 x.tags[0].name='External edit';const b=await x.prepare({operation:'update',entityId:a.command.entityId,patch:{name:'Canonical edit'}});await x.accept(b.preparedCommandId);assert.equal(await x.wake(),'tag_review');assert.equal(x.tags[0].name,'External edit');assert.equal(writes(x).length,2);const status=await(await x.call('/tag-projection')).json();assert.ok(status.work.some(row=>row.error_code==='tag_external_edit_requires_review'));
});
