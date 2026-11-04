// Resumable controller for the exact synthetic hosted profile. Secrets live in ignored session files.
import fs from 'node:fs';
import {createHmac,randomUUID} from 'node:crypto';
import assert from 'node:assert/strict';
import {egressVersion,egressHash,egressMac} from '../../workers/chatgpt-auth/src/callback-egress-protocol.ts';
import {dispatchPayload} from '../../deploy/callback-egress/src/gateway.mjs';
const [sessionPath,reportPath]=process.argv.slice(2);
const session=JSON.parse(fs.readFileSync(sessionPath,'utf8'));
assert.equal(session.gateway,'https://kaleidoscopic-rolypoly-416a42.netlify.app/callback-egress');
assert.equal(session.issuer,'https://habitica-companion-auth.strexet.workers.dev');
assert.ok(session.receiver.startsWith('https://habitica-companion-callback-qualification.strexet.workers.dev/callback-qualification/'));
assert.ok(Date.now()<session.expiresAt);
const save=report=>fs.writeFileSync(reportPath,JSON.stringify(report,null,2)+'\n');
const report=fs.existsSync(reportPath)?JSON.parse(fs.readFileSync(reportPath,'utf8')):{runId:session.runId,stage:'prepared',requestId:session.requestId};
assert.equal(report.runId,session.runId);assert.equal(report.requestId,session.requestId);
async function call(url,options={}) {
 const r=await fetch(url,{...options,redirect:'error',signal:AbortSignal.timeout(15000)});
 const reader=r.body.getReader();let bytes=0;const chunks=[];
 try{while(true){const {value,done}=await reader.read();if(done)break;bytes+=value.length;if(bytes>16384)throw Error('response_limit');chunks.push(value);}}finally{await reader.cancel();}
 return {status:r.status,body:JSON.parse(Buffer.concat(chunks).toString('utf8'))};
}
const status=()=>call(session.receiver.replace(/\/receive$/,'/status'),{headers:{authorization:'Bearer '+session.readKey}});
try {
 if(report.stage==='prepared'){
  const env=Object.fromEntries(fs.readFileSync(session.serviceEnvFile,'utf8').split(/\r?\n/).filter(x=>/^[A-Z_][A-Z_0-9]*=/.test(x)).map(x=>{const i=x.indexOf('=');return [x.slice(0,i),x.slice(i+1).replace(/^['"]|['"]$/g,'')];}));
  const timestamp=String(Math.floor(Date.now()/1000)),body=Buffer.from(session.body);
  const payload=dispatchPayload({url:session.receiver,body:body.toString('base64'),headers:{'content-type':'application/json','webhook-id':session.webhookId,'webhook-timestamp':timestamp,'webhook-signature':'v1,'+createHmac('sha256',Buffer.from(session.signingKey,'base64')).update(session.webhookId+'.'+timestamp+'.').update(body).digest('base64')}});
  const grant={version:egressVersion,requestId:session.requestId,issuer:session.issuer,audience:session.gateway,operation:'challenge',payloadHash:egressHash(JSON.stringify(payload)),issuedAt:Number(timestamp),expiresAt:Number(timestamp)+30};
  const packet={grant,mac:egressMac(env.CALLBACK_EGRESS_GRANT_KEY,'grant',grant),payload};
  // Retain the original authority privately before any request. A rerun never mints replacement authority.
  fs.writeFileSync(sessionPath,JSON.stringify({...session,originalPacket:packet},null,2)+'\n',{mode:0o600});
  report.stage='dispatch_intent';save(report);
  const started=Date.now(),delivery=await call(session.gateway,{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify(packet)});
  report.delivery={status:delivery.status,state:delivery.body.state,requestId:delivery.body.requestId,receiverStatus:delivery.body.status,wallMs:Date.now()-started};save(report);
  assert.equal(delivery.status,200);assert.equal(delivery.body.state,'response');assert.equal(delivery.body.status,200);assert.equal(delivery.body.requestId,session.requestId);assert.ok(report.delivery.wallMs<15000);
  assert.deepEqual(JSON.parse(Buffer.from(delivery.body.responseBody,'base64').toString('utf8')),{challenge:JSON.parse(session.body).challenge});
  const replay=await call(session.gateway,{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify(packet)});
  report.replay={status:replay.status,state:replay.body.state};save(report);assert.equal(replay.status,503);
  const observation=await status();assert.equal(observation.status,200);assert.equal(observation.body.observations.length,1);
  const row=observation.body.observations[0];assert.equal(row.webhook_id,session.webhookId);assert.equal(row.deliveries,1);assert.equal(row.body_hash,egressHash(body));assert.equal(row.signature_valid,1);
  report.observation=row;report.stage='awaiting_shutdown';save(report);
 } else if(report.stage==='awaiting_shutdown') {
  const packet=session.originalPacket;assert.ok(packet);
  const disabled=await call(session.gateway,{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify(packet)});
  const claim=await call(session.issuer+'/callback-egress/claim',{method:'POST',headers:{'content-type':'application/json'},body:'{}'});
  const observation=await status();assert.equal(disabled.status,503);assert.equal(claim.status,503);assert.equal(observation.status,200);assert.equal(observation.body.observations[0].deliveries,1);
  report.shutdown={gatewayStatus:disabled.status,claimStatus:claim.status,receiverDeliveries:1};report.stage='pass';save(report);
 } else if(report.stage!=='pass') {
  // An uncertain original dispatch is observation-only until a controller resolves the original receipt.
  report.recoveredObservation=(await status()).body;save(report);throw Error('original_dispatch_requires_recovery');
 }
 console.log(JSON.stringify({runId:report.runId,stage:report.stage,requestId:report.requestId}));
} catch(error) {
 report.failureCategory=error?.code==='ERR_ASSERTION'?'assertion_failed':'transport_or_fixture_unavailable';save(report);
 console.error(JSON.stringify({runId:report.runId,stage:report.stage,failureCategory:report.failureCategory}));process.exitCode=1;
}
