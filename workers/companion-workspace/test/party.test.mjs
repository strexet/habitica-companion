import {test} from 'node:test';
import assert from 'node:assert/strict';
import {fixture,enrolled,authorization,exchange,resource,accounts} from '../../chatgpt-auth/test/fixtures/oauth.mjs';
import {Client} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js';
import {StreamableHTTPClientTransport} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/streamableHttp.js';
const partyIds=['90000000-0000-4000-8000-000000000051','90000000-0000-4000-8000-000000000052'];
async function setup(t){
 let mode='ok',reads=0,writes=0,afterParty;
 const f=await fixture(t,{workspace:true,party:true,habitica:async request=>{
  if(request.method!=='GET'){writes++;return new Response(null,{status:500});}
  const actor=request.headers.get('x-api-user'),i=accounts.findIndex(a=>a.habiticaUserId===actor);
  const name=i===0?'Fixture A':'Fixture B';
  if(request.url.endsWith('user?userFields=_id,profile'))return Response.json({success:true,data:{_id:actor,profile:{name}}});
  reads++;
  if(mode==='quota')return new Response(null,{status:429,headers:{'retry-after':'120'}});
  if(mode==='credential')return new Response(null,{status:401});
  if(request.url.includes('/user?'))return Response.json({success:true,data:{_id:mode==='wrong-actor'?accounts[1-i].habiticaUserId:actor,profile:{name},party:mode==='no-party'?{}:{_id:partyIds[i]},apiToken:'do-not-forward',inbox:{private:'not-needed'}}});
  assert.ok(request.url.endsWith('/groups/party'));
  if(afterParty)await afterParty();
  return Response.json({success:true,data:{_id:mode==='changed-party'?partyIds[1-i]:partyIds[i],type:mode==='guild'?'guild':'party',leader:mode==='bad-leader'?{_id:actor}:actor,name:'Synthetic Party '+i,memberCount:3,quest:{key:'whale',active:false},chat:[{text:'private chat'}],purchased:{plan:{private:'not-needed'}}}});
 }});
 const profiles=[];
 for(const a of accounts){const p=await enrolled(f,a);const connected=await f.workspace.fetch('http://localhost:8789/connect',{method:'POST',headers:{authorization:`Bearer ${p.managementToken}`,'content-type':'application/json'},body:JSON.stringify({consentVersion:'workspace-v1'})});assert.equal(connected.status,201);profiles.push(p);}
 return {f,profiles,setMode:value=>mode=value,setAfterParty:value=>afterParty=value,counts:()=>({reads,writes})};
}
async function client(t,f,p,scope='profile:read party:read'){
 const result=await exchange(f,await authorization(f,p,scope));assert.equal(result.status,200);const token=(await result.json()).access_token;
 const c=new Client({name:'party-fixture',version:'1.0.0'});t.after(()=>c.close());await c.connect(new StreamableHTTPClientTransport(new URL(resource),{requestInit:{headers:{authorization:`Bearer ${token}`}},fetch:(url,init)=>f.mcp.fetch(String(url),init)}));return {c,token};
}
const request=(f,token,args={},name='get_party_context')=>f.mcp.fetch(resource,{method:'POST',headers:{authorization:`Bearer ${token}`,'content-type':'application/json',accept:'application/json, text/event-stream','x-party-sync-user-id':accounts[1].habiticaUserId,'x-party-sync-party-id':partyIds[1],'x-party-sync-leader-id':accounts[1].habiticaUserId},body:JSON.stringify({jsonrpc:'2.0',id:1,method:'tools/call',params:{name,arguments:args}})});

test('actual SDK party context derives current identities and omits unneeded upstream data',async t=>{
 const s=await setup(t);const {f,profiles}=s;
 for(let i=0;i<2;i++){
  const {c,token}=await client(t,f,profiles[i]);const advertised=(await c.listTools()).tools.find(x=>x.name==='get_party_context');assert.equal(advertised.annotations.readOnlyHint,true);assert.deepEqual(advertised._meta.securitySchemes,[{type:'oauth2',scopes:['profile:read','party:read']}]);
  const response=await request(f,token);assert.equal(response.status,200);const result=await response.json();assert.notEqual(result.result.isError,true);const data=result.result.structuredContent;
  assert.equal(data.party.id,partyIds[i]);assert.equal(data.memberRole,'leader');assert.equal(data.party.quest.key,'whale');assert.equal(data.provenance,'habitica-verified-party');
  const serialized=JSON.stringify(data);for(const secret of [accounts[i].apiToken,'apiToken','inbox','private chat','leaderId','actorUserId'])assert.equal(serialized.includes(secret),false);
 }
 assert.deepEqual(s.counts(),{reads:4,writes:0});
});

test('scope and argument checks deny access at MCP, Workspace and private Auth independently',async t=>{
 const s=await setup(t),{f,profiles}=s;const {token}=await client(t,f,profiles[0],'profile:read');
 assert.equal((await request(f,token)).status,403);
 const bindings=await f.mf.getBindings('mcp');const workspace=bindings.WORKSPACE_SERVER;
 assert.equal((await workspace.fetch('https://workspace.internal/tool',{method:'POST',headers:{authorization:`Bearer ${token}`},body:JSON.stringify({tool:'get_party_context',arguments:{}})})).status,403);
 const auth=(await f.mf.getBindings('workspace')).AUTH_PARTY;
 const proof=await auth.fetch('https://party.internal/context',{method:'POST',headers:{authorization:`Bearer ${token}`},body:JSON.stringify({profileId:profiles[0].profileId,connectionId:(await f.db.prepare('SELECT connection_id FROM integration_profiles WHERE profile_id=?').bind(profiles[0].profileId).first()).connection_id,tool:'get_party_context'})});assert.equal(proof.status,403);
 const full=await client(t,f,profiles[0]);const bad=await full.c.callTool({name:'get_party_context',arguments:{partyId:partyIds[1]}});assert.equal(bad.isError,true);
 assert.equal((await f.auth.fetch('http://localhost:8787/context',{method:'POST'})).status,404);assert.equal(s.counts().reads,0);
});

test('missing/malformed/changed party facts and quota/credential failures never produce membership',async t=>{
 for(const mode of ['no-party','wrong-actor','changed-party','guild','bad-leader','quota','credential']){
  const s=await setup(t);const {c}=await client(t,s.f,s.profiles[0]);s.setMode(mode);
  const result=await c.callTool({name:'get_party_context',arguments:{}});assert.equal(result.isError,true,mode);assert.equal(result.structuredContent.party,undefined);assert.equal(s.counts().writes,0);
  if(mode==='quota')assert.equal(result.structuredContent.rate.retryAfterSeconds,120);
  for(const account of accounts)assert.equal(JSON.stringify(s.f.messages).includes(account.apiToken),false);
 }
});

test('enrollment change while reading party discards the formerly valid proof',async t=>{
 const s=await setup(t);const {f,profiles}=s;const {c}=await client(t,f,profiles[0]);
 s.setAfterParty(()=>f.db.prepare('UPDATE integration_profiles SET connection_id=? WHERE profile_id=?').bind(crypto.randomUUID(),profiles[0].profileId).run());
 const result=await c.callTool({name:'get_party_context',arguments:{}});assert.equal(result.isError,true);assert.equal(result.structuredContent.party,undefined);assert.equal(s.counts().writes,0);
});
