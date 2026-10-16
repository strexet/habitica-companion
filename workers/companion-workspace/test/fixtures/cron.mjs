import assert from 'node:assert/strict';
import {fixture,enrolled,authorization,exchange,resource,accounts} from '../../../chatgpt-auth/test/fixtures/oauth.mjs';
import {habiticaDay} from '../../../shared/cron.ts';
import {Client} from '../../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/index.js';
import {StreamableHTTPClientTransport} from '../../../chatgpt-mcp/node_modules/@modelcontextprotocol/sdk/dist/esm/client/streamableHttp.js';
export const party='90000000-0000-4000-8000-000000000091';
export const scope='profile:read account:read tasks:read party:read habitica.cron.execute habitica.spells.execute';
const origin='http://localhost:8789';
export const gate=()=>{let resolve;return {promise:new Promise(r=>resolve=r),release:()=>resolve()};};
export async function setup(t,options={}){
 const dayStart=(new Date().getUTCHours()+12)%24,day=habiticaDay(Date.now(),dayStart,0);
 const state={posts:0,lost:false,unknown:false,refreshFail:false,groupHook:null,party:options.party===false?'':party,questHp:100,needsCron:true,lastCron:new Date(Date.parse(day.dayStartUtc)-86400000).toISOString(),stats:{class:'healer',lvl:20,hp:40,mp:100,int:20,con:20,buffs:{str:5}},task:{id:'90000000-0000-4000-8000-000000000092',type:'daily',text:'Synthetic previous-day Daily',completed:false,isDue:true,checklist:[{id:'90000000-0000-4000-8000-000000000093',text:'Review item',completed:false}],priority:1,value:0}};
 let direct;
 const f=await fixture(t,{workspace:true,cron:options.enabled??true,party:true,spells:true,projection:true,executor:true,cronService:async request=>{
  const body=await request.clone().json();const response=await direct.fetch(request);
  if(state.lost&&body.operation==='execute'){state.lost=false;await response.arrayBuffer();return new Response('',{status:500});}
  return response;
 },habitica:async request=>{
  const actor=request.headers.get('x-api-user'),url=new URL(request.url);
  if(request.method==='GET'&&url.pathname==='/api/v3/user'){
   if(state.refreshFail&&state.posts>0)return new Response('',{status:500});
   return Response.json({success:true,data:{_id:actor,profile:{name:actor===accounts[0].habiticaUserId?'Fixture A':'Fixture B'},lastCron:state.lastCron,needsCron:state.needsCron,preferences:{dayStart,timezoneOffset:0,timezoneOffsetAtLastCron:0,sleep:false,disableClasses:false},stats:structuredClone(state.stats),party:{_id:state.party},items:{gear:{equipped:{weapon:'never-export-gear'}},quests:{}},flags:{}}});
  }
  if(request.method==='GET'&&url.pathname==='/api/v3/tasks/user')return Response.json({success:true,data:[{...structuredClone(state.task),userId:actor}]});
  if(request.method==='GET'&&url.pathname==='/api/v3/groups/party'){
   if(state.groupHook)await state.groupHook();return Response.json({success:true,data:{_id:state.party,type:'party',name:'Synthetic party',leader:accounts[0].habiticaUserId,memberCount:2,quest:{key:'whale',active:true,progress:{hp:state.questHp}}}});
  }
  assert.equal(url.pathname,'/api/v3/cron');assert.equal(request.method,'POST');assert.deepEqual(await request.json(),{});state.posts++;state.needsCron=false;state.lastCron=new Date().toISOString();
  return state.unknown?new Response('never-export-secret',{status:500}):Response.json({success:true,data:{},private:'never-export-private'});
 }});
 direct=(await f.mf.getBindings('mcp')).AUTH_CRON;
 const profiles=[],sessions=[],clients=[],tokens=[];
 const http=(i,path,body,method='POST',management=false)=>f.workspace.fetch(origin+path,{method,headers:{authorization:'Bearer '+(management?profiles[i].managementToken:sessions[i]),'content-type':'application/json'},...(body===undefined?{}:{body:JSON.stringify(body)})});
 async function client(i,granted=options.scope??scope){
  const response=await exchange(f,await authorization(f,profiles[i],granted));assert.equal(response.status,200);const token=(await response.json()).access_token;tokens[i]=token;
  const c=new Client({name:'cron-fixture',version:'1.0.0'});t.after(()=>c.close());await c.connect(new StreamableHTTPClientTransport(new URL(resource),{requestInit:{headers:{authorization:'Bearer '+token}},fetch:(url,init)=>f.mcp.fetch(String(url),init)}));clients[i]=c;return c;
 }
 for(let i=0;i<2;i++){
  profiles.push(await enrolled(f,accounts[i]));const response=await http(i,'/connect',{consentVersion:'workspace-v1'},'POST',true);assert.equal(response.status,201);sessions.push((await response.json()).sessionToken);await client(i);
 }
 const tool=(i,name,args={})=>clients[i].callTool({name,arguments:args});
 const prepare=async(requestId=crypto.randomUUID(),i=0)=>{const r=await tool(i,'prepare_start_new_day',{requestId});assert.notEqual(r.isError,true,JSON.stringify(r));return r.structuredContent;};
 const execute=(p,i=0)=>tool(i,'execute_start_new_day',{actionId:p.actionId,executeToken:p.executeToken});
 return {f,state,profiles,sessions,clients,tokens,http,client,tool,prepare,execute};
}
