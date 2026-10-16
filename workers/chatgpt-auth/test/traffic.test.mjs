import test from 'node:test';import assert from 'node:assert/strict';
import {fixture,enrolled,authorization,exchange,resource,accounts} from './fixtures/oauth.mjs';
import {charge,TrafficLimit,trafficSummary} from '../src/traffic.ts';
async function exhaust(db,bucket,limit){for(const window of [Math.floor(Date.now()/60000),Math.floor(Date.now()/60000)+1])await db.prepare('INSERT OR REPLACE INTO authorization_attempts(bucket,attempts,expires_at_utc) VALUES(?,?,?)').bind('traffic:'+bucket+':'+window,limit,new Date((window+1)*60000).toISOString()).run();}
const quota=(f,body)=>f.internal.fetch('https://auth.internal/traffic',{method:'POST',body:JSON.stringify(body)});
test('atomic quota admits exactly the cap, records anonymous denials and resets next window',async t=>{
 const f=await fixture(t);const env={HABITICA_INTEGRATION_DB:f.db};const now=Date.now();const results=await Promise.allSettled(Array.from({length:30},()=>charge(env,'synthetic-concurrency',7,now)));
 assert.equal(results.filter(r=>r.status==='fulfilled').length,7);assert.ok(results.filter(r=>r.status==='rejected').every(r=>r.reason instanceof TrafficLimit&&r.reason.status===429&&r.reason.retryAfterSeconds>0));assert.equal((await trafficSummary(env)).localQuotaDenials,23);await charge(env,'synthetic-concurrency',7,(Math.floor(now/60000)+1)*60000);
 assert.deepEqual(Object.keys(await trafficSummary(env)).sort(),['localQuotaDenials','upstream429']);
});
test('verified profile throttling preserves separate receipt recovery and another profile',async t=>{
 const f=await fixture(t);const a=await enrolled(f),b=await enrolled(f,accounts[1]);const row=async p=>f.db.prepare('SELECT connection_id FROM integration_profiles WHERE profile_id=?').bind(p.profileId).first();const ca=await row(a),cb=await row(b);
 await exhaust(f.db,'mcp:profile:'+a.profileId,60);
 const limited=await quota(f,{lane:'mcp',profileId:a.profileId,connectionId:ca.connection_id,recovery:false});assert.equal(limited.status,429);assert.ok(Number(limited.headers.get('retry-after'))>0);
 assert.equal((await quota(f,{lane:'mcp',profileId:a.profileId,connectionId:ca.connection_id,recovery:true})).status,204);assert.equal((await quota(f,{lane:'mcp',profileId:b.profileId,connectionId:cb.connection_id,recovery:false})).status,204);
 assert.equal((await quota(f,{lane:'mcp',profileId:a.profileId,connectionId:cb.connection_id,recovery:false})).status,401);
 assert.equal((await f.auth.fetch('http://localhost:8787/traffic',{method:'POST',body:JSON.stringify({lane:'mcp'})})).status!==204,true);
});
test('MCP ingress returns one bounded 429 before token processing or tool dispatch',async t=>{
 const f=await fixture(t);await exhaust(f.db,'mcp:global',600);
 const response=await f.mcp.fetch(resource,{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({jsonrpc:'2.0',id:1,method:'initialize',params:{protocolVersion:'2025-03-26',capabilities:{},clientInfo:{name:'fixture',version:'1'}}})});assert.equal(response.status,429);assert.ok(response.headers.has('retry-after'));assert.equal((await response.json()).error,'rate_limited');
});
test('read quota prevents upstream HTTP and upstream 429 persists cooldown without identifying logs',async t=>{
 let hits=0;const f=await fixture(t,{habitica:request=>{
  if(request.url.includes('userFields='))return Response.json({success:true,data:{_id:request.headers.get('x-api-user'),profile:{name:'Fixture A'}}});hits++;return new Response('private upstream payload',{status:429,headers:{'retry-after':'90'}});
 }});const p=await enrolled(f);const tokens=await(await exchange(f,await authorization(f,p,'profile:read account:read'))).json();
 await exhaust(f.db,'upstream:profile:'+p.profileId,60);const call=()=>f.internal.fetch('https://auth.internal/read',{method:'POST',headers:{authorization:'Bearer '+tokens.access_token},body:JSON.stringify({tool:'get_account_status',arguments:{}})});
 assert.equal((await call()).status,429);assert.equal(hits,0);await f.db.prepare("DELETE FROM authorization_attempts WHERE bucket LIKE 'traffic:upstream:profile:%'").run();assert.equal((await call()).status,429);assert.equal(hits,1);assert.equal((await call()).status,429);assert.equal(hits,1);assert.equal((await trafficSummary({HABITICA_INTEGRATION_DB:f.db})).upstream429,1);
 assert.ok(!f.messages.join('\n').includes(p.profileId));assert.ok(!f.messages.join('\n').includes(accounts[0].apiToken));assert.ok(!f.messages.join('\n').includes('private upstream payload'));
});
