// Owned synthetic Auth/Workspace Workers. No request is forwarded to Habitica.
import assert from 'node:assert/strict';
import {fixture,enrolled,accounts,issuer} from '../../workers/chatgpt-auth/test/fixtures/oauth.mjs';
import {accept} from '../../workers/companion-workspace/src/commands.ts';
import {adoptProjection} from '../../workers/companion-workspace/src/projection-adapter.ts';
import {workspaceCommand} from '../../workers/shared/workspace-commands.ts';
import {emptyWorkspace} from '../../workers/shared/workspace-domain.ts';
import {remoteTask} from '../../workers/shared/projection.ts';
export async function cloudFixture(){
 const cleanup=[];let writes=0;const remote={id:'90000000-0000-4000-8000-000000000001',userId:accounts[0].habiticaUserId,type:'daily',text:'Browser linked Daily fixture',notes:'',priority:1,attribute:'str',completed:false,up:true,down:true,checklist:[],frequency:'daily',everyX:1,startDate:'2026-10-27T00:00:00.000Z',repeat:{su:true,m:true,t:true,w:true,th:true,f:true,s:true},daysOfMonth:[],weeksOfMonth:[],isDue:true};
 const user={_id:accounts[0].habiticaUserId,profile:{name:'Fixture A'},stats:{class:'warrior',lvl:1,hp:50,maxHealth:50,mp:10,maxMP:10},preferences:{dayStart:0,timezoneOffset:0},items:{gear:{equipped:{},owned:{}}},needsCron:false};
 let f;try{
 f=await fixture({after:fn=>cleanup.push(fn)},{workspace:true,projection:true,habitica:request=>{if(request.method!=='GET'){writes++;throw Error('Browser fixture never enables a provider mutation');}return Response.json({success:true,data:request.url.includes('tasks/user?')?[remote]:new URL(request.url).pathname.includes('/tasks/')?remote:user});}});
 const p=await enrolled(f);assert.ok(p.descriptor,'Existing connection descriptor is required');const r=await f.workspace.fetch('http://localhost:8789/connect',{method:'POST',headers:{authorization:'Bearer '+p.managementToken,'content-type':'application/json'},body:JSON.stringify({consentVersion:'workspace-v1'})});assert.equal(r.status,201);const w=await r.json();
 const taskId=crypto.randomUUID(),recurrence={frequency:'daily',interval:1,startDate:'2026-10-27',timeZoneId:'UTC',weekdays:[],daysOfMonth:[]};const created=await accept(emptyWorkspace(w.workspaceId),workspaceCommand({id:crypto.randomUUID(),workspaceId:w.workspaceId,expectedRevision:1,operation:'createDaily',taskId,patch:{text:remote.text,recurrence}}),new Date().toISOString());const observed=await remoteTask(remote,accounts[0].habiticaUserId),document=adoptProjection(created.document,taskId,observed,observed.fingerprint),serialized=JSON.stringify(document),entry=JSON.stringify(created.entry),event=JSON.stringify(created.event);
 await f.workspaceDb.batch([f.workspaceDb.prepare('UPDATE workspaces SET document_json=?,store_version=?,archive_bytes=? WHERE profile_id=?').bind(serialized,document.revision,Buffer.byteLength(serialized+entry+event),p.profileId),f.workspaceDb.prepare('INSERT INTO workspace_commands(profile_id,command_id,sequence,payload_hash,entry_json) VALUES(?,?,?,?,?)').bind(p.profileId,created.entry.command.id,2,created.entry.payloadHash,entry),f.workspaceDb.prepare('INSERT INTO workspace_events(profile_id,event_id,sequence,event_json) VALUES(?,?,?,?)').bind(p.profileId,created.event.id,2,event)]);
 const requests=[];
 return {account:accounts[0].habiticaUserId,credentials:{userId:accounts[0].habiticaUserId,apiToken:accounts[0].apiToken},taskId,workspaceId:w.workspaceId,descriptor:p.descriptor,requests,
  async route(route){const request=route.request(),url=request.url();if(url.startsWith(issuer+'/')||url.startsWith('http://localhost:8789/')){const headers=request.headers(),method=request.method(),body=request.postDataBuffer();const response=await(url.startsWith(issuer+'/')?f.auth:f.workspace).fetch(new Request(url,{method,headers,...(body?{body}:{} )}));requests.push({host:url.startsWith(issuer+'/')?'auth':'workspace',method,path:new URL(url).pathname,status:response.status});return route.fulfill({status:response.status,headers:Object.fromEntries(response.headers),body:Buffer.from(await response.arrayBuffer())});}
   if(url.startsWith('https://habitica.com/api/v3/')&&request.method()==='GET')return route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({success:true,data:url.endsWith('/user')?user:url.includes('/tasks')?[]:{gear:{flat:{}}}})});return route.abort();},
  async snapshot(){const row=await f.workspaceDb.prepare('SELECT document_json FROM workspaces WHERE profile_id=?').bind(p.profileId).first();const commands=await f.workspaceDb.prepare('SELECT entry_json FROM workspace_commands WHERE profile_id=? ORDER BY sequence').bind(p.profileId).all();return {document:JSON.parse(row.document_json),commands:commands.results.map(row=>JSON.parse(row.entry_json)),writes};},
  async close(){for(const fn of cleanup.reverse())await fn();}
 };
 }catch(error){for(const fn of cleanup.reverse())await fn();throw error;}
}
