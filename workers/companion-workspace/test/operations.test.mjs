import test from 'node:test';import assert from 'node:assert/strict';
import {fixture,enrolled,accounts} from '../../chatgpt-auth/test/fixtures/oauth.mjs';
const origin='http://localhost:8789';let sequence=1;
async function connect(f,p){const r=await f.workspace.fetch(origin+'/connect',{method:'POST',headers:{authorization:'Bearer '+p.managementToken,'content-type':'application/json'},body:JSON.stringify({consentVersion:'workspace-v1'})});assert.equal(r.status,201);return r.json();}
async function seed(db,p,state,age,lease){const id=crypto.randomUUID();await db.prepare('INSERT INTO workspace_commands(profile_id,command_id,sequence,payload_hash,entry_json) VALUES(?,?,?,?,?)').bind(p.profileId,id,++sequence,'hash',JSON.stringify({projectionState:state,acceptedAtUtc:new Date(Date.now()-age).toISOString(),lease:lease?{expiresAtUtc:new Date(Date.now()-1000).toISOString()}:null,command:{text:'Private task text'}})).run();}
test('own saved status separates stalled, expired and uncertain work with no task or cross-profile data',async t=>{
 const f=await fixture(t,{workspace:true});const a=await enrolled(f),b=await enrolled(f,accounts[1]);const wa=await connect(f,a),wb=await connect(f,b);
 await seed(f.workspaceDb,a,'pending',20*60000,false);await seed(f.workspaceDb,a,'leased',1000,true);await seed(f.workspaceDb,a,'unknownOutcome',1000,false);await seed(f.workspaceDb,a,'blocked',1000,false);await seed(f.workspaceDb,b,'pending',40*60000,false);
 for(const table of ['prepared_spell_actions','prepared_cron_actions']){
  const columns='profile_id,action_id,request_id,input_hash,connection_id,access_epoch,'+(table==='prepared_spell_actions'?'namespace_id,':'')+'status,payload_json,entry_json,created_at_utc,expires_at_utc';
  const row=await f.workspaceDb.prepare('SELECT connection_id,access_epoch FROM workspaces WHERE profile_id=?').bind(a.profileId).first();
  const values=[a.profileId,crypto.randomUUID(),crypto.randomUUID(),'hash',row.connection_id,row.access_epoch,...(table==='prepared_spell_actions'?[crypto.randomUUID()]:[]),'unknownOutcome','{}','{}',new Date().toISOString(),new Date(Date.now()+60000).toISOString()];
  await f.workspaceDb.prepare('INSERT INTO '+table+'('+columns+') VALUES('+values.map(()=>'?').join(',')+')').bind(...values).run();
 }
 const r=await f.workspace.fetch(origin+'/operations',{headers:{authorization:'Bearer '+wa.sessionToken}});assert.equal(r.status,200);const value=await r.json();assert.deepEqual([value.pending,value.stalled,value.uncertain,value.blocked,value.expiredLeases],[2,1,3,1,1]);assert.ok(value.oldestPendingAtUtc);assert.ok(!JSON.stringify(value).includes('Private task text'));assert.ok(!JSON.stringify(value).includes(a.profileId));
 const other=await(await f.workspace.fetch(origin+'/operations',{headers:{authorization:'Bearer '+wb.sessionToken}})).json();assert.deepEqual([other.pending,other.uncertain],[1,0]);assert.equal((await f.workspace.fetch(origin+'/operations')).status,401);
 const disconnected=await f.workspace.fetch(origin+'/disconnect',{method:'POST',headers:{authorization:'Bearer '+wa.sessionToken}});assert.equal(disconnected.status,200);assert.equal((await f.workspace.fetch(origin+'/operations',{headers:{authorization:'Bearer '+wa.sessionToken}})).status,401);
});
