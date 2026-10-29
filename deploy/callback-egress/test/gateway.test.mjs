import test from 'node:test';
import assert from 'node:assert/strict';
import {createHmac,randomUUID} from 'node:crypto';
import {egressGrant,egressHash,egressMac,egressVersion} from '../../../workers/chatgpt-auth/src/callback-egress-protocol.ts';
import {handleGateway,dispatchPayload} from '../src/gateway.mjs';
import {callbackUrl,publicAddress,postPublic} from '../src/transport.mjs';

const env={CALLBACK_EGRESS_ENABLED:'true',CALLBACK_EGRESS_ISSUER:'https://auth.example.org',CALLBACK_EGRESS_AUDIENCE:'https://gateway.example.org/callback-egress',
  CALLBACK_EGRESS_GRANT_KEY:Buffer.alloc(32,61).toString('base64url'),CALLBACK_EGRESS_CLAIM_KEY:Buffer.alloc(32,62).toString('base64url')};
const now=Date.now(),epoch=Math.floor(now/1000);
function envelope(overrides={}) {
  const body=Buffer.from('{"challenge":"synthetic €","line":"\\n"}\n');
  const payload={url:'https://callback.example.org/verify?opaque=synthetic',body:body.toString('base64'),headers:{'content-type':'application/json',
    'webhook-id':'challenge_1','webhook-timestamp':String(epoch),
    'webhook-signature':'v1,'+createHmac('sha256',Buffer.alloc(32,23)).update('challenge_1.'+epoch+'.').update(body).digest('base64')}};
  const grant={version:egressVersion,requestId:randomUUID(),issuer:env.CALLBACK_EGRESS_ISSUER,audience:env.CALLBACK_EGRESS_AUDIENCE,
    operation:'challenge',payloadHash:egressHash(JSON.stringify(payload)),issuedAt:epoch,expiresAt:epoch+30,...overrides};
  return {grant,mac:egressMac(env.CALLBACK_EGRESS_GRANT_KEY,'grant',grant),payload};
}
const request=(body,url=env.CALLBACK_EGRESS_AUDIENCE,headers={})=>new Request(url,{method:'POST',headers:{'content-type':'application/json',...headers},body:JSON.stringify(body)});
function network({result={status:200,body:Buffer.from('{"challenge":"synthetic €"}')},failure,claimFailure}={}) {
  const claims=new Set(),calls=[];
  return {calls,claims,post:async input=>{
    if(input.url===env.CALLBACK_EGRESS_ISSUER+'/callback-egress/claim') {
      const x=JSON.parse(input.body),g=egressGrant(x.grant,env.CALLBACK_EGRESS_ISSUER,env.CALLBACK_EGRESS_AUDIENCE,now);
      assert.equal(x.proof,egressMac(env.CALLBACK_EGRESS_CLAIM_KEY,'claim',g,x.nonce));
      if(claims.has(g.requestId))return {status:409,body:Buffer.from('{}')};
      claims.add(g.requestId); if(claimFailure)throw Error('synthetic lost claim acknowledgement');
      return {status:200,body:Buffer.from(JSON.stringify({requestId:g.requestId,payloadHash:g.payloadHash,nonce:x.nonce,claimed:true,
        proof:egressMac(env.CALLBACK_EGRESS_CLAIM_KEY,'claimed',g,x.nonce)}))};
    }
    calls.push(input); if(failure)throw Error('secret URL and headers must never enter results');return result;
  }};
}
test('all IPv4 special ranges and alternate literal forms fail closed',()=>{
  for(const ip of ['0.1.1.1','10.2.3.4','100.64.0.1','100.127.255.255','127.0.0.1','169.254.169.254','172.16.0.1','172.31.255.255','192.0.0.9','192.0.2.1','192.88.99.1','192.168.1.1','198.18.0.1','198.19.255.255','198.51.100.1','203.0.113.1','224.0.0.1','255.255.255.255','0177.0.0.1','0x7f000001','2130706433','invalid'])assert.equal(publicAddress(ip),false,ip);
  for(const ip of ['1.1.1.1','8.8.8.8','100.63.255.255','100.128.0.0','172.15.255.255','172.32.0.0','93.184.216.34'])assert.equal(publicAddress(ip),true,ip);
});
test('IPv6 global unicast only; mapped, NAT64, tunnel, documentation and zone IDs refuse',()=>{
  for(const ip of ['::','::1','::ffff:127.0.0.1','::ffff:8.8.8.8','::ffff:0808:0808','64:ff9b::808:808','64:ff9b:1::1','100::1','2001::1','2001:2::1','2001:db8::1','2002:0808:0808::1','3fff::1','2620:4f:8000::1','fc00::1','fe80::1','ff02::1','2606:4700::1%en0'])assert.equal(publicAddress(ip),false,ip);
  for(const ip of ['2606:4700:4700::1111','2001:4860:4860::8888','2a00:1450::1','2606:4700:0000:0000:0000:0000:0000:1111'])assert.equal(publicAddress(ip),true,ip);
});
test('URLs refuse non-HTTPS, IP literals, credentials, noncanonical spelling and local names',()=>{
  for(const url of ['http://callback.example.org/','https://127.0.0.1/','https://2130706433/','https://[::1]/','https://u:p@callback.example.org/','https://callback.example.org/#secret','https://callback.example.org:444/','https://host.local/','https://host.internal/','https://host.localhost/','https://host.test/','https://localhost/','https://callback.example.org./','https://CALLBACK.example.org/','https://callback.example.org/a\nb'])assert.throws(()=>callbackUrl(url),url);
});
test('mixed/private/empty/oversized DNS answers refuse before TLS and rebinding cannot trigger a second lookup',async()=>{
  let sends=0,resolves=0;
  for(const answers of [[],['1.1.1.1','127.0.0.1'],['::1'],Array(17).fill('1.1.1.1')])await assert.rejects(postPublic({url:'https://callback.example.org/',signal:AbortSignal.timeout(1000)}, {resolve:async()=>answers,send:async()=>{sends++;}}));
  const input={url:'https://callback.example.org/',signal:AbortSignal.timeout(1000),body:Buffer.from('x'),headers:{}};
  await postPublic(input,{resolve:async()=>{resolves++;return resolves===1?['1.1.1.1']:['127.0.0.1'];},send:async x=>{sends++;assert.equal(x.address,'1.1.1.1');assert.equal(x.url.hostname,'callback.example.org');}});
  assert.equal(resolves,1);assert.equal(sends,1);
});
test('exact signed bytes and challenge response survive; concurrent/restarted replay never sends twice',async()=>{
  const x=envelope(),n=network();
  const results=await Promise.all(Array.from({length:12},()=>handleGateway(request(x),env,{post:n.post,now:()=>now})));
  assert.equal(results.filter(r=>r.status===200).length,1);assert.equal(n.calls.length,1);
  assert.deepEqual(n.calls[0].body,Buffer.from(x.payload.body,'base64'));assert.deepEqual(n.calls[0].headers,x.payload.headers);
  const ok=await results.find(r=>r.status===200).json();assert.equal(ok.requestId,x.grant.requestId);assert.equal(ok.state,'response');assert.equal(Buffer.from(ok.responseBody,'base64').toString(),'{"challenge":"synthetic €"}');
  assert.equal((await handleGateway(request(x),env,{post:n.post,now:()=>now})).status,503);assert.equal(n.calls.length,1);
});
test('tampered payload/destination/signature/operation/audience/expiry never reaches claim',async()=>{
  for(const mutate of [x=>x.payload.url='https://other.example.org/',x=>x.payload.body=Buffer.from('changed').toString('base64'),x=>x.mac='0'.repeat(64),x=>x.grant.operation='event',x=>x.grant.audience='https://other.example.org/callback-egress',x=>x.grant.expiresAt=epoch-1,x=>x.grant.issuedAt=epoch+20,x=>x.grant.unexpected=true,x=>x.extra=true]) {
    const x=envelope(),n=network();mutate(x);
    assert.equal((await handleGateway(request(x),env,{post:n.post,now:()=>now})).status,503);assert.equal(n.claims.size,0);assert.equal(n.calls.length,0);
  }
});
test('disabled, missing/reused keys, CA overrides, Origin, wrong route and content type refuse',async()=>{
  for(const patch of [{CALLBACK_EGRESS_ENABLED:'false'},{CALLBACK_EGRESS_GRANT_KEY:''},{CALLBACK_EGRESS_CLAIM_KEY:env.CALLBACK_EGRESS_GRANT_KEY},{NODE_TLS_REJECT_UNAUTHORIZED:'0'},{NODE_EXTRA_CA_CERTS:'/test-ca'},{SSL_CERT_FILE:'/test-ca'},{NODE_OPTIONS:'--require ./hook'}]) {
    const n=network();assert.equal((await handleGateway(request(envelope()),{...env,...patch},{post:n.post,now:()=>now})).status,503);assert.equal(n.claims.size,0);
  }
  for(const req of [request(envelope(),env.CALLBACK_EGRESS_AUDIENCE,{origin:'https://browser.example.org'}),request(envelope(),env.CALLBACK_EGRESS_AUDIENCE+'?wrong=1'),request(envelope(),env.CALLBACK_EGRESS_AUDIENCE,{'content-type':'text/plain'})])assert.equal((await handleGateway(req,env)).status,503);
});
test('lost claim acknowledgement and transport failures preserve one-use uncertainty without secret leakage',async()=>{
  for(const opts of [{claimFailure:true},{failure:true}]) {
    const n=network(opts),x=envelope();let output='';
    for(let i=0;i<2;i++)output+=await (await handleGateway(request(x),env,{post:n.post,now:()=>now})).text();
    assert.equal(n.claims.size,1);assert.equal(n.calls.length,opts.failure?1:0);
    for(const secret of [x.payload.url,x.payload.body,x.payload.headers['webhook-signature'],env.CALLBACK_EGRESS_GRANT_KEY])assert.equal(output.includes(secret),false);
    if(opts.failure)assert.match(output,/unknown/);
  }
});
test('forged/replayed claim acknowledgement and expired handoff cannot send payload',async()=>{
  for(const mode of ['forged','nonce','expired']) {
    const n=network(),x=envelope();let tick=now;
    const post=async input=>{const r=await n.post(input);if(input.url.endsWith('/claim')) {const b=JSON.parse(r.body);if(mode==='forged')b.proof='0'.repeat(64);if(mode==='nonce')b.nonce=randomUUID();if(mode==='expired')tick=now+31000;return{...r,body:Buffer.from(JSON.stringify(b))};}return r;};
    assert.equal((await handleGateway(request(x),env,{post,now:()=>tick})).status,503);assert.equal(n.calls.length,0);
  }
});
test('redirect status is terminal without following Location; event response bodies are suppressed',async()=>{
  for(const status of [200,302,410,413,429,503]) {
    const n=network({result:{status,body:Buffer.from('private provider message')}}),x=envelope({operation:'event'});
    const r=await (await handleGateway(request(x),env,{post:n.post,now:()=>now})).json();
    assert.equal(r.status,status);assert.equal(r.responseBody,null);assert.equal(n.calls.length,1);assert.equal(r.state,status===302?'redirect_refused':'response');
  }
});
test('bounded envelope, decoded payload, header allowlist and slow ingress fail closed',async()=>{
  const x=envelope();for(const patch of [{body:Buffer.alloc(65537).toString('base64')},{body:'invalid=='},{headers:{...x.payload.headers,authorization:'forbidden'}},{headers:{...x.payload.headers,'webhook-id':'bad\r\nheader'}}])assert.throws(()=>dispatchPayload({...x.payload,...patch}));
  const big=new Request(env.CALLBACK_EGRESS_AUDIENCE,{method:'POST',headers:{'content-type':'application/json'},body:'x'.repeat(100001)});assert.equal((await handleGateway(big,env)).status,503);
  const stream=new ReadableStream({start(){}}),slow=new Request(env.CALLBACK_EGRESS_AUDIENCE,{method:'POST',headers:{'content-type':'application/json'},body:stream,duplex:'half'});
  const began=Date.now();assert.equal((await handleGateway(slow,env,{deadlineMs:50})).status,503);assert.ok(Date.now()-began<500);
});
