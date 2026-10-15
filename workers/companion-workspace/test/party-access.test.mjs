import {test} from 'node:test';
import assert from 'node:assert/strict';
import {fixture,enrolled,authorization,exchange,resource,accounts} from '../../chatgpt-auth/test/fixtures/oauth.mjs';
import {Client} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js';
import {StreamableHTTPClientTransport} from '../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/streamableHttp.js';
const party='90000000-0000-4000-8000-000000000071';
for(const change of ['disconnect','replace','delete'])test('late party facts are discarded after workspace '+change,async t=>{
 let f,profile,inFlight=false;
 f=await fixture(t,{workspace:true,party:true,habitica:async request=>{
  const user=request.headers.get('x-api-user');
  if(request.url.includes('/user?'))return Response.json({success:true,data:{_id:user,profile:{name:'Fixture A'},party:{_id:party}}});
  assert.ok(request.url.endsWith('/groups/party'));
  if(inFlight){
   const sql=change==='disconnect'?'UPDATE workspaces SET active=0 WHERE profile_id=?':change==='replace'?'UPDATE workspaces SET access_epoch=lower(hex(randomblob(16))) WHERE profile_id=?':'DELETE FROM workspaces WHERE profile_id=?';
   await f.workspaceDb.prepare(sql).bind(profile.profileId).run();
  }
  return Response.json({success:true,data:{_id:party,type:'party',leader:user,name:'Private late result',memberCount:1}});
 }});
 profile=await enrolled(f,accounts[0]);assert.equal((await f.workspace.fetch('http://localhost:8789/connect',{method:'POST',headers:{authorization:`Bearer ${profile.managementToken}`},body:JSON.stringify({consentVersion:'workspace-v1'})})).status,201);
 const token=(await(await exchange(f,await authorization(f,profile,'profile:read party:read'))).json()).access_token;
 const c=new Client({name:'party-lifecycle',version:'1.0.0'});t.after(()=>c.close());await c.connect(new StreamableHTTPClientTransport(new URL(resource),{requestInit:{headers:{authorization:`Bearer ${token}`}},fetch:(url,init)=>f.mcp.fetch(String(url),init)}));
 assert.equal((await c.callTool({name:'get_party_context',arguments:{}})).structuredContent.party.name,'Private late result');
 inFlight=true;const result=await c.callTool({name:'get_party_context',arguments:{}});assert.equal(result.isError,true);assert.equal(result.structuredContent.party,undefined);assert.equal(JSON.stringify(result).includes('Private late result'),false);
});
