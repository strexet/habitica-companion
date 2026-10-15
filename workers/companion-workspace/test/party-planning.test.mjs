import {test} from 'node:test';
import assert from 'node:assert/strict';
import {fixture,enrolled,authorization,exchange,resource,accounts} from '../../chatgpt-auth/test/fixtures/oauth.mjs';
import {Client} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js';
import {StreamableHTTPClientTransport} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/streamableHttp.js';
const party='90000000-0000-4000-8000-000000000061',other='90000000-0000-4000-8000-000000000062';
const origin='http://localhost:8789';
async function setup(t){
 const departed=new Set();let outside=false,afterParty,writes=0;
 const f=await fixture(t,{workspace:true,party:true,habitica:async request=>{
  if(request.method!=='GET'){writes++;return new Response(null,{status:500});}
  const actor=request.headers.get('x-api-user'),i=accounts.findIndex(a=>a.habiticaUserId===actor),name=i===0?'Fixture A':'Fixture B',id=outside&&i===1?other:party;
  if(request.url.includes('/user?'))return Response.json({success:true,data:{_id:actor,profile:{name},party:departed.has(i)?{}:{_id:id},items:{quests:{whale:2,harpy:1}}}});
  assert.ok(request.url.endsWith('/groups/party'));if(afterParty)await afterParty(i);
  return Response.json({success:true,data:{_id:id,type:'party',leader:accounts[0].habiticaUserId,name:'Synthetic <script>party</script>',memberCount:2,quest:{key:'whale',active:false}}});
 }});
 const profiles=[],sessions=[],clients=[];
 for(const account of accounts){const p=await enrolled(f,account);profiles.push(p);
  const res=await f.workspace.fetch(origin+'/connect',{method:'POST',headers:{authorization:`Bearer ${p.managementToken}`,'content-type':'application/json'},body:JSON.stringify({consentVersion:'workspace-v1'})});assert.equal(res.status,201);sessions.push((await res.json()).sessionToken);
  const auth=await exchange(f,await authorization(f,p,'profile:read party:read party.planning.write'));assert.equal(auth.status,200);const token=(await auth.json()).access_token;
  const c=new Client({name:'planning-fixture',version:'1.0.0'});t.after(()=>c.close());await c.connect(new StreamableHTTPClientTransport(new URL(resource),{requestInit:{headers:{authorization:`Bearer ${token}`}},fetch:(url,init)=>f.mcp.fetch(String(url),init)}));clients.push(c);
 }
 const http=(i,path,body,method='POST',management=false)=>f.workspace.fetch(origin+path,{method,headers:{authorization:`Bearer ${management?profiles[i].managementToken:sessions[i]}`,'content-type':'application/json'},...(body!==undefined?{body:JSON.stringify(body)}:{})});
 const tool=(i,name,args={})=>clients[i].callTool({name,arguments:args});
 const consent=async i=>{const response=await http(i,'/party/consent',{consentVersion:'party-planning-v1',expectedPartyId:outside&&i===1?other:party});assert.equal(response.status,200,await response.clone().text());};
 const state=async i=>{const result=await tool(i,'get_quest_planning_state');assert.notEqual(result.isError,true,JSON.stringify(result));return result.structuredContent;};
 const base=s=>({commandId:crypto.randomUUID(),expectedNamespaceId:s.namespaceId,expectedRevision:s.revision});
 return {f,profiles,sessions,clients,http,tool,consent,state,base,departed,setOutside:value=>outside=value,setAfterParty:value=>afterParty=value,writes:()=>writes};
}

test('consent, owned-scroll queue, explicit votes and durable receipts remain isolated from legacy claims',async t=>{
 const s=await setup(t);assert.equal((await s.tool(0,'get_quest_planning_state')).isError,true);
 assert.equal((await s.http(0,'/party/consent',{consentVersion:'party-planning-v1',expectedPartyId:other})).status,409);
 await s.consent(0);await s.consent(1);let state=await s.state(0);assert.deepEqual(state.queue,[]);assert.ok(state.ownScrolls.some(q=>q.questKey==='whale'&&q.availableToQueue===2));
 for(const key of ['unowned','constructor','__proto__'])assert.equal((await s.tool(0,'queue_quest',{...s.base(state),questKey:key})).isError,true);
 const input={...s.base(state),questKey:'whale'};const queued=await s.tool(0,'queue_quest',input);assert.equal(queued.structuredContent.savedInCompanion,true);assert.equal(queued.structuredContent.habiticaEffects,'none');
 const receipt=queued.structuredContent;assert.deepEqual((await s.tool(0,'queue_quest',input)).structuredContent,receipt);
 assert.equal((await s.tool(1,'get_party_command',{commandId:input.commandId})).structuredContent.savedInCompanion,null);
 assert.equal((await s.tool(0,'queue_quest',{...input,questKey:'harpy'})).isError,true);
 state=await s.state(1);assert.equal(state.queue[0].ownedByYou,false);assert.equal(state.queue[0].contributorLabel,'Fixture A');
 const vote={...s.base(state),queueItemId:receipt.queueItemId,voted:true};const voted=await s.tool(1,'vote_quest',vote);assert.equal(voted.structuredContent.voted,true);
 assert.deepEqual((await s.tool(1,'vote_quest',vote)).structuredContent,voted.structuredContent);state=await s.state(1);assert.equal(state.queue[0].voteCount,1);assert.equal(state.queue[0].votedByYou,true);
 assert.equal((await s.tool(1,'vote_quest',{...s.base(state),queueItemId:receipt.queueItemId,voted:false})).structuredContent.voted,false);state=await s.state(1);assert.equal(state.queue[0].voteCount,0);
 const attempt=await s.http(1,'/party/settings',{expectedNamespaceId:state.namespaceId,expectedRevision:state.revision,membersCanQueue:false});assert.equal(attempt.status,403);
 const settings=await s.http(0,'/party/settings',{expectedNamespaceId:state.namespaceId,expectedRevision:state.revision,membersCanQueue:false});assert.equal(settings.status,200);state=await s.state(1);
 assert.equal((await s.tool(1,'queue_quest',{...s.base(state),questKey:'harpy'})).isError,true);
 assert.equal((await s.tool(1,'vote_quest',{...s.base(state),queueItemId:receipt.queueItemId,voted:true})).structuredContent.savedInCompanion,true);
 assert.equal(s.writes(),0);
});

test('revision, concurrent acceptance, outside-party and namespace deletion fences fail closed',async t=>{
 const s=await setup(t);await s.consent(0);await s.consent(1);let state=await s.state(0);
 const inputs=['whale','harpy'].map(questKey=>({...s.base(state),questKey}));const results=await Promise.all(inputs.map(input=>s.tool(0,'queue_quest',input)));assert.equal(results.filter(r=>r.isError!==true).length,1);
 state=await s.state(0);assert.equal(state.queue.length,1);
 s.setOutside(true);assert.equal((await s.tool(1,'get_quest_planning_state')).isError,true);await s.consent(1);assert.equal((await s.state(1)).queue.length,0);
 assert.equal((await s.tool(1,'vote_quest',{...s.base(state),queueItemId:state.queue[0].id,voted:true})).isError,true);
 s.setOutside(false);assert.equal((await s.tool(1,'get_quest_planning_state')).isError,true);await s.consent(1);
 assert.equal((await s.http(1,'/party/planning',{expectedNamespaceId:state.namespaceId,expectedRevision:state.revision,confirmNamespaceId:state.namespaceId},'DELETE')).status,403);
 const removed=await s.http(0,'/party/planning',{expectedNamespaceId:state.namespaceId,expectedRevision:state.revision,confirmNamespaceId:state.namespaceId},'DELETE');assert.equal(removed.status,200,await removed.clone().text());
 for(const table of ['authenticated_party_commands','authenticated_party_consents'])assert.equal((await s.f.workspaceDb.prepare(`SELECT COUNT(*) AS n FROM ${table} WHERE party_id=?`).bind(party).first()).n,0);
 assert.equal((await s.tool(1,'get_quest_planning_state')).isError,true);await s.consent(0);const replacement=await s.state(0);assert.notEqual(replacement.namespaceId,state.namespaceId);
 assert.equal((await s.tool(0,'queue_quest',{...s.base(state),questKey:'whale'})).isError,true);assert.equal(JSON.parse((await s.f.workspaceDb.prepare('SELECT document_json FROM authenticated_party_plans WHERE party_id=?').bind(party).first()).document_json).queue.length,0);assert.equal(s.writes(),0);
});

test('management export/removal works after departure, excludes others and rolls back a stale batch',async t=>{
 const s=await setup(t);await s.consent(0);await s.consent(1);let state=await s.state(0);
 const first=await s.tool(0,'queue_quest',{...s.base(state),questKey:'whale'});state=await s.state(1);
 await s.tool(1,'queue_quest',{...s.base(state),questKey:'harpy'});state=await s.state(1);await s.tool(1,'vote_quest',{...s.base(state),queueItemId:first.structuredContent.queueItemId,voted:true});
 s.departed.add(0);assert.equal((await s.tool(0,'get_quest_planning_state')).isError,true);
 assert.equal((await s.tool(0,'get_party_command',{commandId:first.structuredContent.commandId})).structuredContent.savedInCompanion,true);
 const own=await s.http(0,'/party/contributions',undefined,'GET',true);assert.equal(own.status,200);const archive=await own.json();assert.equal(archive.contributions[0].queue.length,1);assert.equal(archive.contributions[0].queue[0].questKey,'whale');assert.equal(archive.contributions[0].votes.length,0);assert.equal(JSON.stringify(archive).includes('Fixture B'),false);
 const {ownContributions}=await import('../src/party-planning.ts');const db=s.f.workspaceDb;
 await assert.rejects(ownContributions({WORKSPACE_DB:{prepare:sql=>db.prepare(sql),batch:async statements=>{await db.prepare('UPDATE authenticated_party_plans SET revision=revision+1 WHERE party_id=?').bind(party).run();return db.batch(statements);}}},s.profiles[0].profileId,true));
 assert.equal((await db.prepare('SELECT COUNT(*) AS n FROM authenticated_party_commands WHERE profile_id=?').bind(s.profiles[0].profileId).first()).n,1);
 const removal=await s.http(0,'/party/contributions',{confirm:'remove-my-party-contributions'},'POST',true);assert.equal(removal.status,200,await removal.clone().text());
 state=await s.state(1);assert.equal(state.queue.length,1);assert.equal(state.queue[0].questKey,'harpy');assert.equal(state.queue[0].voteCount,0);
 const empty=await(await s.http(0,'/party/contributions',undefined,'GET',true)).json();assert.deepEqual(empty,{contributions:[],commands:[]});assert.equal(s.writes(),0);
});

test('party read discards an in-flight result after workspace access is disconnected',async t=>{
 const s=await setup(t);s.setAfterParty(()=>s.f.workspaceDb.prepare('UPDATE workspaces SET active=0 WHERE profile_id=?').bind(s.profiles[0].profileId).run());
 const result=await s.tool(0,'get_party_context');assert.equal(result.isError,true);assert.equal(result.structuredContent.party,undefined);
});

test('party read discards a result from a replaced workspace access epoch',async t=>{
 const s=await setup(t);s.setAfterParty(()=>s.f.workspaceDb.prepare('UPDATE workspaces SET access_epoch=? WHERE profile_id=?').bind(crypto.randomUUID(),s.profiles[0].profileId).run());
 const result=await s.tool(0,'get_party_context');assert.equal(result.isError,true);assert.equal(result.structuredContent.party,undefined);
});

test('planning write scopes are checked independently by MCP, Workspace and Auth',async t=>{
 const s=await setup(t);await s.consent(0);const state=await s.state(0);
 const input={...s.base(state),questKey:'whale'};
 const response=await exchange(s.f,await authorization(s.f,s.profiles[0],'profile:read party:read'));const token=(await response.json()).access_token;
 const denied=await s.f.mcp.fetch(resource,{method:'POST',headers:{authorization:`Bearer ${token}`,'content-type':'application/json',accept:'application/json, text/event-stream'},body:JSON.stringify({jsonrpc:'2.0',id:1,method:'tools/call',params:{name:'queue_quest',arguments:input}})});assert.equal(denied.status,403);assert.match(denied.headers.get('www-authenticate'),/party.planning.write/);
 const workspace=(await s.f.mf.getBindings('mcp')).WORKSPACE_SERVER;assert.equal((await workspace.fetch('https://workspace.internal/tool',{method:'POST',headers:{authorization:`Bearer ${token}`},body:JSON.stringify({tool:'queue_quest',arguments:input})})).status,403);
 const auth=(await s.f.mf.getBindings('workspace')).AUTH_PARTY;const row=await s.f.db.prepare('SELECT connection_id FROM integration_profiles WHERE profile_id=?').bind(s.profiles[0].profileId).first();
 assert.equal((await auth.fetch('https://party.internal/context',{method:'POST',headers:{authorization:`Bearer ${token}`},body:JSON.stringify({profileId:s.profiles[0].profileId,connectionId:row.connection_id,tool:'queue_quest'})})).status,403);
 assert.equal(JSON.parse((await s.f.workspaceDb.prepare('SELECT document_json FROM authenticated_party_plans WHERE party_id=?').bind(party).first()).document_json).queue.length,0);assert.equal(s.writes(),0);
});
