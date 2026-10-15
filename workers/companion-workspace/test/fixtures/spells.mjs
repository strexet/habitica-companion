import assert from 'node:assert/strict';
import {fixture,enrolled,authorization,exchange,resource,accounts} from '../../../chatgpt-auth/test/fixtures/oauth.mjs';
import {Client} from '../../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js';
import {StreamableHTTPClientTransport} from '../../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/streamableHttp.js';
const origin='http://localhost:8789',party='90000000-0000-4000-8000-000000000001';
const scope='profile:read account:read party:read habitica.spells.execute';
export async function setup(t,options={}){
 const state={posts:0,failAt:0,unknown:false,lost:false,slow:false,changeAfterFirst:false,beforeGroup:null};
 const stats=[0,1].map(()=>({class:'healer',lvl:20,hp:35,mp:100,int:20,con:30,buffs:{}}));let direct;
 const f=await fixture(t,{workspace:true,party:true,spells:options.enabled??true,projection:true,executor:true,spellService:async request=>{
  const body=await request.clone().json();const response=await direct.fetch(request);
  if(body.operation==='cast'&&state.lost){state.lost=false;await response.arrayBuffer();return new Response('',{status:500});}
  if(body.operation==='cast'&&state.slow){state.slow=false;const buffered=new Response(await response.arrayBuffer(),{status:response.status,headers:new Headers(response.headers)});await new Promise(r=>setTimeout(r,21000));return buffered;}
  return response;
 },habitica:async request=>{
  const actor=request.headers.get('x-api-user'),i=accounts.findIndex(a=>a.habiticaUserId===actor),path=new URL(request.url).pathname;
  const user=()=>({_id:actor,profile:{name:i===0?'Fixture A':'Fixture B'},stats:structuredClone(stats[i]),party:{_id:party},flags:{needsCron:false},preferences:{disableClasses:false,sleep:false},items:{gear:{equipped:{weapon:'fixture-healer'}},quests:{whale:1}},lastCron:'2026-10-15T00:00:00Z'});
  if(request.method==='GET'&&path==='/api/v3/user'){
   if(state.changeAfterFirst&&state.posts===1&&request.url.includes(',stats,')){stats[i].mp--;state.changeAfterFirst=false;}
   return Response.json({success:true,data:user()});
  }
  if(request.method==='GET'&&path==='/api/v3/groups/party'){
   if(state.beforeGroup)await state.beforeGroup();
   return Response.json({success:true,data:{_id:party,type:'party',name:'Synthetic <script>party</script>',leader:accounts[0].habiticaUserId,memberCount:2,quest:{key:'whale',active:false}}});
  }
  assert.equal(path,'/api/v3/user/class/cast/healAll');assert.equal(request.method,'POST');assert.deepEqual(await request.json(),{quantity:1});state.posts++;
  if(state.failAt===state.posts)return new Response('private fixture-a-token',{status:state.unknown?500:403});
  stats[i].mp-=25;stats[i].hp=Math.min(50,stats[i].hp+3);return Response.json({success:true,data:{user:user(),partyMembers:[{private:'never-export'}]}});
 }});
 direct=(await f.mf.getBindings('mcp')).AUTH_SPELL;
 const profiles=[],sessions=[],clients=[],tokens=[];
 const http=(i,path,body,method='POST',management=false)=>f.workspace.fetch(origin+path,{method,headers:{authorization:'Bearer '+(management?profiles[i].managementToken:sessions[i]),'content-type':'application/json'},...(body===undefined?{}:{body:JSON.stringify(body)})});
 async function client(i,granted=scope){
  const response=await exchange(f,await authorization(f,profiles[i],granted));assert.equal(response.status,200);const token=(await response.json()).access_token;tokens[i]=token;
  const c=new Client({name:'spell-fixture',version:'1.0.0'});t.after(()=>c.close());await c.connect(new StreamableHTTPClientTransport(new URL(resource),{requestInit:{headers:{authorization:'Bearer '+token}},fetch:(url,init)=>f.mcp.fetch(String(url),init)}));clients[i]=c;return c;
 }
 const consent=async i=>{assert.equal((await http(i,'/party/consent',{consentVersion:'party-planning-v1',expectedPartyId:party})).status,200);};
 for(let i=0;i<2;i++){
  profiles.push(await enrolled(f,accounts[i]));const response=await http(i,'/connect',{consentVersion:'workspace-v1'},'POST',true);assert.equal(response.status,201);sessions.push((await response.json()).sessionToken);await client(i);
  if(options.consent!==false)await consent(i);
 }
 const tool=(i,name,args={})=>clients[i].callTool({name,arguments:args});
 const prepare=async(count=1,requestId=crypto.randomUUID(),i=0)=>{const result=await tool(i,'prepare_spell_cast',{requestId,spellId:'healAll',count});assert.notEqual(result.isError,true,JSON.stringify(result));return result.structuredContent;};
 const execute=(p,i=0)=>tool(i,'execute_spell_cast',{actionId:p.actionId,executeToken:p.executeToken});
 return {f,state,stats,profiles,sessions,clients,tokens,http,tool,prepare,execute,consent,client};
}

