import assert from 'node:assert/strict';
import {createHmac} from 'node:crypto';
import {fixture,enrolled,authorization,exchange,issuer,accounts,authPage,form,ticket,handle} from './oauth.mjs';
import {emptyWorkspace,workspaceDocument} from '../../../shared/workspace-domain.ts';
export const key=n=>Buffer.alloc(32,n).toString('base64url');
export const secret='whsec_'+Buffer.alloc(32,10).toString('base64');
export const eventBindings={EVENTS_ENABLED:true,CALLBACK_EGRESS_ENABLED:true,CALLBACK_EGRESS_AUDIENCE:'https://gateway.example.org/callback-egress',CALLBACK_EGRESS_GRANT_KEY:key(7),CALLBACK_EGRESS_CLAIM_KEY:key(8),CALLBACK_VAULT_KEY_V1:key(9),CALLBACK_VAULT_CURRENT_KEY_VERSION:'1'};
export async function setup(t,options={}){
 const packets=[];let behavior=options.behavior??'echo';
 const f=await fixture(t,{workspace:true,events:true,eventBindings,eventTransport:async request=>{
  assert.equal(request.url,eventBindings.CALLBACK_EGRESS_AUDIENCE);const packet=await request.json();packets.push(packet);if(options.onPacket)await options.onPacket(packet);const {payload,grant}=packet,body=Buffer.from(payload.body,'base64').toString('utf8'),h=payload.headers;
  assert.equal(h['webhook-signature'].split(' ')[0],'v1,'+createHmac('sha256',Buffer.from((options.secret??secret).slice(6),'base64')).update(`${h['webhook-id']}.${h['webhook-timestamp']}.${body}`).digest('base64'));
  if(behavior==='throw')throw Error('synthetic transport uncertainty');
  return Response.json({requestId:grant.requestId,state:'response',status:grant.operation==='event'?(options.eventStatus??200):behavior==='refuse'?410:200,responseBody:grant.operation==='challenge'?Buffer.from(JSON.stringify({challenge:behavior==='wrong'?'wrong':JSON.parse(body).challenge})).toString('base64'):null});
 },...options.fixture});
 const p=await enrolled(f),connected=await f.workspace.fetch('http://localhost:8789/connect',{method:'POST',headers:{authorization:'Bearer '+p.managementToken},body:JSON.stringify({consentVersion:'workspace-v1'})});assert.equal(connected.status,201);const w=await connected.json();
 const at=options.at??Date.now()+60000,taskId=crypto.randomUUID(),document=workspaceDocument({...emptyWorkspace(w.workspaceId),revision:2,tasks:[{id:taskId,revision:1,text:'Private fixture text',kind:'todo',state:'active',priority:'normal',createdAtUtc:new Date(at-3600000).toISOString(),updatedAtUtc:new Date(at-3600000).toISOString(),reviewAfterUtc:new Date(at).toISOString()}]});
 const tokenResponse=await exchange(f,await authorization(f,p,'profile:read tasks:read events.subscriptions.write'));assert.equal(tokenResponse.status,200);const token=(await tokenResponse.json()).access_token,profile=await f.db.prepare('SELECT * FROM integration_profiles WHERE profile_id=?').bind(p.profileId).first();
 await f.workspaceDb.prepare('UPDATE workspaces SET document_json=?,store_version=2,mcp_grant_epoch=? WHERE profile_id=?').bind(JSON.stringify(document),profile.grant_epoch,p.profileId).run();
 const call=(method,params)=>f.internal.fetch('https://auth.internal/events',{method:'POST',headers:{authorization:'Bearer '+token},body:JSON.stringify({method,params})});
 const consent=async(enabled=true)=>{let r=await call('prepare_event_consent',{requestId:crypto.randomUUID(),taskId,enabled,expectedWorkspaceRevision:2});assert.equal(r.status,200,await r.clone().text());const preview=await r.json();r=await call('accept_event_consent',{commandId:preview.commandId,acceptToken:preview.acceptToken,expectedHash:preview.inputHash});assert.equal(r.status,200,await r.clone().text());return {preview,result:await r.json()};};
 const params={name:'task.reminder.due',arguments:{taskId},delivery:{mode:'webhook',url:'https://receiver.example.org/callback?opaque=synthetic',secret:options.secret??secret},cursor:null,ttlMs:3600000};
 return {f,p,w,token,taskId,document,profile,at,call,consent,params,packets,setEventStatus:value=>{options.eventStatus=value;},setSigningSecret:value=>{options.secret=value;},setBehavior:value=>{behavior=value;}};
}
