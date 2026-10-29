import assert from 'node:assert/strict';
import {fixture,enrolled,issuer,authorization,exchange} from '../../../chatgpt-auth/test/fixtures/oauth.mjs';
import {workspaceDocument} from '../../../shared/workspace-domain.ts';
import {executeProfile} from '../../src/executor.ts';
export async function setupGroupTags(t,options={}){
 const tags=[],external=crypto.randomUUID(),task={id:crypto.randomUUID(),type:'todo',text:'Synthetic task',notes:'',priority:1,attribute:'str',completed:false,checklist:[],group:{},challenge:{},tags:[external]},calls=[];let f;
 f=await fixture(t,{workspace:true,projection:true,executor:true,...options,habitica:async request=>{
  if(request.url.includes('user?userFields=_id,profile'))return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:'Fixture A'}}});
  task.userId??=request.headers.get('x-api-user');calls.push({method:request.method,path:new URL(request.url).pathname});
  if(options.beforeRequest)await options.beforeRequest(request,f);
  if(request.method==='GET')return Response.json({success:true,data:request.url.endsWith('/tags')?tags:task});
  if(request.url.endsWith('/tags')){tags.push(await request.json());return options.unknownCreate?new Response('',{status:502}):Response.json({success:true,data:tags.at(-1)});}
  if(request.url.includes('/tasks/')){const id=request.url.split('/').at(-1);task.tags=request.method==='POST'?[...new Set([...task.tags,id])]:task.tags.filter(value=>value!==id);return Response.json({success:true,data:task});}
  const tag=tags.find(tag=>request.url.endsWith('/'+tag.id));Object.assign(tag,await request.json());return Response.json({success:true,data:tag});
 }});
 const profile=await enrolled(f),connected=await f.workspace.fetch('http://localhost:8789/connect',{method:'POST',headers:{authorization:'Bearer '+profile.managementToken,'content-type':'application/json'},body:JSON.stringify({consentVersion:'workspace-v1'})});assert.equal(connected.status,201);const workspace=await connected.json(),projectId=crypto.randomUUID(),taskId=crypto.randomUUID();
 const row=await f.workspaceDb.prepare('SELECT * FROM workspaces').first(),document=workspaceDocument({...JSON.parse(row.document_json),projects:[{id:projectId,revision:1,isArchived:false,name:'Shared name',categoryId:null}],tasks:[{id:taskId,revision:1,text:task.text,kind:'todo',state:'active',priority:'normal',createdAtUtc:new Date().toISOString(),updatedAtUtc:new Date().toISOString(),projectId}],projections:[{id:crypto.randomUUID(),revision:1,taskId,state:'linked',habiticaTaskId:task.id,fingerprint:'a'.repeat(64)}]});
 const json=JSON.stringify(document);await f.workspaceDb.prepare('UPDATE workspaces SET document_json=?,archive_bytes=?').bind(json,new TextEncoder().encode(json).length).run();
 const http=(path,body)=>f.workspace.fetch('http://localhost:8789'+path,{method:body===undefined?'GET':'POST',headers:{authorization:'Bearer '+workspace.sessionToken,'content-type':'application/json'},body:body===undefined?undefined:JSON.stringify(body)});
 const response=(tool,args={})=>http('/tag-api',{tool,arguments:args});
 const api=async(tool,args={})=>{const r=await response(tool,args);assert.equal(r.status,200,await r.clone().text());return r.json();};
 const consent=(enabled=true)=>f.auth.fetch(`${issuer}/profiles/${profile.profileId}/tag-consent`,{method:'POST',headers:{authorization:'Bearer '+profile.managementToken,'content-type':'application/json'},body:JSON.stringify({consentVersion:'habitica-tags-v1',enabled})});
 const prepare=(operation='reconcile',extra={})=>api('prepare_group_tag_command',{requestId:crypto.randomUUID(),operation,...(operation==='recover'?{}:{kind:'project',entityId:projectId}),...extra});
 const accept=preview=>api('accept_group_tag_command',{commandId:preview.commandId});
 const env=await f.mf.getBindings('workspace'),wait=async()=>{await f.workspaceDb.prepare("UPDATE workspace_runtime SET next_network_at_utc='2000-01-01T00:00:00Z'").run();await f.db.prepare("UPDATE integration_projection_state SET next_automated_at_utc='2000-01-01T00:00:00Z'").run();};
 const wake=()=>executeProfile(env,profile.profileId,{wait}),enable=enabled=>http('/executor',{enabled,consentVersion:'executor-v1'}),exported=async()=>{const r=await http('/export');assert.equal(r.status,200);return r.json();};
 const token=async(scope)=>{const r=await exchange(f,await authorization(f,profile,scope));assert.equal(r.status,200);return (await r.json()).access_token;};
 return {f,profile,workspace,projectId,taskId,tags,task,external,calls,http,response,api,prepare,accept,consent,env,wake,wait,enable,exported,token,get writes(){return calls.filter(call=>call.method!=='GET').length;}};
}
