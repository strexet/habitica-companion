import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {randomUUID} from 'node:crypto';
import {Miniflare,convertV4MiniflareOptions,Log,LogLevel} from 'miniflare';
import {egressMac,egressVersion} from '../src/callback-egress-protocol.ts';
import {handleGateway} from '../../../deploy/callback-egress/src/gateway.mjs';
import {egressHash} from '../src/callback-egress-protocol.ts';

const issuer='https://auth.example.org',audience='https://gateway.example.org/callback-egress';
const grantKey=Buffer.alloc(32,61).toString('base64url'),claimKey=Buffer.alloc(32,62).toString('base64url');
async function fixture(t,overrides={}) {
  const messages=[];class Capture extends Log {log(message){messages.push(message);}}
  const config=JSON.parse(await readFile(new URL('../wrangler.jsonc',import.meta.url),'utf8'));
  const bindings={...config.vars,ENVIRONMENT:'production',CALLBACK_EGRESS_ENABLED:true,CALLBACK_EGRESS_AUDIENCE:audience,
    CALLBACK_EGRESS_GRANT_KEY:grantKey,CALLBACK_EGRESS_CLAIM_KEY:claimKey,
    AUTH_ISSUER:issuer,PWA_ORIGIN:'https://pwa.example.org',MCP_RESOURCE:'https://mcp.example.org/mcp',WORKSPACE_ORIGIN:'https://workspace.example.org',...overrides};
  let outbound=0;
  const mf=new Miniflare(convertV4MiniflareOptions({log:new Capture(LogLevel.VERBOSE),workers:[{name:'auth',modules:true,
    script:await readFile(new URL('../dist/index.js',import.meta.url),'utf8'),compatibilityDate:config.compatibility_date,
    compatibilityFlags:['nodejs_compat','global_fetch_strictly_public'],bindings,kvNamespaces:['OAUTH_KV'],d1Databases:['HABITICA_INTEGRATION_DB'],
    outboundService:()=>{outbound++;return new Response(null,{status:500});}}]}));
  t.after(()=>mf.dispose());const db=await mf.getD1Database('HABITICA_INTEGRATION_DB','auth');
  for(const sql of (await readFile(new URL('../migrations/0012-callback-egress.sql',import.meta.url),'utf8')).split(';'))if(sql.trim())await db.prepare(sql).run();
  const auth=await mf.getWorker('auth');
  return{auth,db,messages,outbound:()=>outbound,call:body=>auth.fetch(issuer+'/callback-egress/claim',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify(body)})};
}
function grant(overrides={}) {
  const n=Math.floor(Date.now()/1000);
  return{version:egressVersion,requestId:randomUUID(),issuer,audience,operation:'challenge',payloadHash:'a'.repeat(64),issuedAt:n,expiresAt:n+30,...overrides};
}
function claim(g=grant()) {const nonce=randomUUID();return{grant:g,mac:egressMac(grantKey,'grant',g),nonce,proof:egressMac(claimKey,'claim',g,nonce)};}
test('actual D1 atomic claim allows exactly one concurrent winner and stores no callback material',async t=>{
  const f=await fixture(t),x=claim();const replies=await Promise.all(Array.from({length:12},()=>f.call(x)));
  assert.equal(replies.filter(r=>r.status===200).length,1);
  const ack=await replies.find(r=>r.status===200).json();assert.equal(ack.proof,egressMac(claimKey,'claimed',x.grant,x.nonce));
  assert.equal((await f.call(claim({...x.grant,payloadHash:'b'.repeat(64)}))).status,409);
  const refreshed=grant({requestId:x.grant.requestId});assert.equal((await f.call(claim(refreshed))).status,409);
  const rows=await f.db.prepare('SELECT * FROM integration_callback_egress_claims').all();assert.equal(rows.results.length,1);
  assert.deepEqual(Object.keys(rows.results[0]).sort(),['request_id','grant_hash','expires_at_ms','claimed_at_ms'].sort());
  assert.equal(f.outbound(),0);for(const secret of [grantKey,claimKey])assert.equal(JSON.stringify(rows).includes(secret),false);
});
test('invalid MAC, proof, scope, lifetime, shape and browser requests refuse before reservation',async t=>{
  const f=await fixture(t);
  for(const mutate of [x=>x.mac='0'.repeat(64),x=>x.proof='0'.repeat(64),x=>x.grant.audience='https://other.example.org/callback-egress',
    x=>x.grant.expiresAt=x.grant.issuedAt+31,x=>x.grant.expiresAt=x.grant.issuedAt-1,x=>x.grant.operation='arbitrary',x=>x.url='https://private.example.org',x=>x.nonce='bad']) {
    const x=claim();mutate(x);assert.equal((await f.call(x)).status,503);
  }
  for(const extra of [{origin:'https://browser.example.org'},{'content-type':'text/plain'}])assert.ok([403,503].includes((await f.auth.fetch(issuer+'/callback-egress/claim',{method:'POST',headers:{'content-type':'application/json',...extra},body:JSON.stringify(claim())})).status));
  assert.equal((await f.db.prepare('SELECT COUNT(*) AS n FROM integration_callback_egress_claims').first()).n,0);
});
test('off gate, missing keys, key reuse and storage refusal remain unavailable',async t=>{
  for(const overrides of [{CALLBACK_EGRESS_ENABLED:false},{CALLBACK_EGRESS_ENABLED:'false'},{CALLBACK_EGRESS_ENABLED:'true'},{CALLBACK_EGRESS_GRANT_KEY:''},{CALLBACK_EGRESS_CLAIM_KEY:grantKey},{PROFILE_ID_KEY:grantKey}]) {
    const f=await fixture(t,overrides);assert.equal((await f.call(claim())).status,503);assert.equal(f.outbound(),0);
  }
  const f=await fixture(t);await f.db.exec('DROP TABLE integration_callback_egress_claims');assert.equal((await f.call(claim())).status,503);
});
test('slow and oversized claim bodies fail within the ingress deadline without reservation',async t=>{
  const f=await fixture(t);
  const headers={'content-type':'application/json'};
  const large=await f.auth.fetch(issuer+'/callback-egress/claim',{method:'POST',headers,body:'x'.repeat(4097)});
  assert.equal(large.status,503);
  const stream=new ReadableStream({start(controller){controller.enqueue(new TextEncoder().encode('{'));}});
  const began=Date.now();
  const r=await f.auth.fetch(new Request(issuer+'/callback-egress/claim',{method:'POST',headers,body:stream,duplex:'half'}));
  assert.equal(r.status,503);assert.ok(Date.now()-began<7000);
  assert.equal((await f.db.prepare('SELECT COUNT(*) AS n FROM integration_callback_egress_claims').first()).n,0);
});
test('capacity, expired cleanup and original acknowledgement loss preserve single use',async t=>{
  const f=await fixture(t),x=claim();await f.call(x); // discard the original acknowledgement
  assert.equal((await f.call(x)).status,409);
  for(let i=0;i<10;i++)await f.db.prepare('INSERT INTO integration_callback_egress_claims SELECT lower(hex(randomblob(16))),grant_hash,expires_at_ms,claimed_at_ms FROM integration_callback_egress_claims LIMIT ?').bind(Math.min(1000-2**i,2**i)).run();
  assert.equal((await f.db.prepare('SELECT COUNT(*) AS n FROM integration_callback_egress_claims').first()).n,1000);
  assert.equal((await f.call(claim())).status,409);
  await f.db.prepare('UPDATE integration_callback_egress_claims SET expires_at_ms=?').bind(Date.now()-31*86400000).run();
  assert.equal((await f.call(claim())).status,200);assert.equal(f.outbound(),0);
});
test('production Node gateway composes actual Worker/D1 redemption; response loss never redelivers',async t=>{
  const f=await fixture(t),n=Math.floor(Date.now()/1000),sent=[];
  const env={CALLBACK_EGRESS_ENABLED:'true',CALLBACK_EGRESS_ISSUER:issuer,CALLBACK_EGRESS_AUDIENCE:audience,CALLBACK_EGRESS_GRANT_KEY:grantKey,CALLBACK_EGRESS_CLAIM_KEY:claimKey};
  for(const loseClaim of [false,true]) {
    const payload={url:'https://callback.example.org/verify',body:Buffer.from('{"challenge":"synthetic"}').toString('base64'),headers:{'content-type':'application/json','webhook-id':'verify_1','webhook-timestamp':String(n),'webhook-signature':'v1,'+Buffer.alloc(32,23).toString('base64')}};
    const g=grant({payloadHash:egressHash(JSON.stringify(payload))}),input={grant:g,mac:egressMac(grantKey,'grant',g),payload};
    const post=async x=>{
      if(x.url.endsWith('/claim')){const r=await f.auth.fetch(x.url,{method:'POST',headers:x.headers,body:x.body});const body=Buffer.from(await r.arrayBuffer());if(loseClaim)throw Error('lost acknowledgement');return{status:r.status,body};}
      sent.push(x);return{status:200,body:Buffer.from('{"challenge":"synthetic"}')};
    };
    const req=()=>new Request(audience,{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify(input)});
    assert.equal((await handleGateway(req(),env,{post})).status,loseClaim?503:200);
    assert.equal((await handleGateway(req(),env,{post})).status,503);
  }
  assert.equal(sent.length,1);assert.equal(f.outbound(),0);
  assert.equal(f.messages.some(s=>s.includes('synthetic')||s.includes(grantKey)||s.includes(claimKey)),false);
});
