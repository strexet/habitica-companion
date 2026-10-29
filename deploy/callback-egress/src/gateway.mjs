import {randomUUID} from 'node:crypto';
import {egressVersion,egressGrant,egressKeys,egressHash,egressMac,egressMatches,exactObject,serviceUrl} from '../../../workers/chatgpt-auth/src/callback-egress-protocol.ts';
import {callbackUrl,postPublic} from './transport.mjs';

const responseHeaders={'content-type':'application/json','cache-control':'no-store','x-content-type-options':'nosniff'};
const reply=(value,status=200) => new Response(JSON.stringify(value),{status,headers:responseHeaders});
async function boundedBody(request,signal,max=100000) {
  const reader=request.body?.getReader(), chunks=[]; let size=0;
  if (!reader) throw Error('invalid_request');
  const abort=() => { void reader.cancel().catch(()=>{}); };
  signal.addEventListener('abort',abort,{once:true});
  try {
    while (true) {
      signal.throwIfAborted(); const {value,done}=await reader.read();
      signal.throwIfAborted(); if (done) break;
      size += value.length; if (size>max) throw Error('invalid_request'); chunks.push(value);
    }
    return JSON.parse(Buffer.concat(chunks).toString('utf8'));
  } finally { signal.removeEventListener('abort',abort); await reader.cancel().catch(()=>{}); }
}
export function dispatchPayload(value) {
  const v=exactObject(value,['url','body','headers']), url=callbackUrl(v.url).href;
  if (typeof v.body !== 'string' || v.body.length>87384 || !/^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$/.test(v.body)) throw Error('invalid_payload');
  const bytes=Buffer.from(v.body,'base64');
  if (bytes.length<1 || bytes.length>65536 || bytes.toString('base64')!==v.body) throw Error('invalid_payload');
  const h=exactObject(v.headers,['content-type','webhook-id','webhook-timestamp','webhook-signature']);
  if (h['content-type'] !== 'application/json' || typeof h['webhook-id']!=='string' || !/^[A-Za-z0-9_-]{1,100}$/.test(h['webhook-id']) ||
      typeof h['webhook-timestamp']!=='string' || !/^\d{10}$/.test(h['webhook-timestamp']) ||
      typeof h['webhook-signature']!=='string' || !/^v1,[A-Za-z0-9+/]{43}=$/.test(h['webhook-signature'])) throw Error('invalid_payload');
  return {url,body:v.body,headers:{'content-type':h['content-type'],'webhook-id':h['webhook-id'],
    'webhook-timestamp':h['webhook-timestamp'],'webhook-signature':h['webhook-signature']}};
}
function configuration(env) {
  if (env.CALLBACK_EGRESS_ENABLED !== 'true' || process.versions.node.split('.')[0] !== '22' ||
      env.NODE_TLS_REJECT_UNAUTHORIZED === '0' || env.NODE_EXTRA_CA_CERTS || env.SSL_CERT_FILE || env.SSL_CERT_DIR ||
      /--use-(?:openssl|system)-ca|--require|--import/.test(env.NODE_OPTIONS || '')) throw Error();
  egressKeys(env.CALLBACK_EGRESS_GRANT_KEY,env.CALLBACK_EGRESS_CLAIM_KEY);
  return {issuer:serviceUrl(env.CALLBACK_EGRESS_ISSUER,true),audience:serviceUrl(env.CALLBACK_EGRESS_AUDIENCE)};
}
// Dependency injection is confined to tests. Netlify entry invokes defaults only.
export async function handleGateway(request,env,{post=postPublic,now=Date.now,deadlineMs=10000}={}) {
  if (request.method==='GET' && new URL(request.url).pathname.endsWith('/health')) return reply({service:'callback-egress',version:egressVersion,activation:'unqualified'});
  let claimed=false,requestId;
  const controller=new AbortController(),timer=setTimeout(()=>controller.abort(),deadlineMs),signal=controller.signal;
  const cancel=()=>controller.abort(); request.signal.addEventListener('abort',cancel,{once:true});
  try {
    const config=configuration(env);
    if (request.signal.aborted) controller.abort();
    if (request.method!=='POST' || request.url!==config.audience || request.headers.has('origin') ||
        request.headers.get('content-type')!=='application/json') throw Error();
    const input=exactObject(await boundedBody(request,signal),['grant','mac','payload']);
    const grant=egressGrant(input.grant,config.issuer,config.audience,now());
    const payload=dispatchPayload(input.payload);
    if (egressHash(JSON.stringify(payload))!==grant.payloadHash ||
        !egressMatches(egressMac(env.CALLBACK_EGRESS_GRANT_KEY,'grant',grant),input.mac)) throw Error();
    requestId=grant.requestId;
    const nonce=randomUUID();
    // Acknowledgement loss consumes the grant without sending. There is no claim retry.
    const claim=await post({url:config.issuer+'/callback-egress/claim',signal,body:Buffer.from(JSON.stringify({grant,mac:input.mac,nonce,
      proof:egressMac(env.CALLBACK_EGRESS_CLAIM_KEY,'claim',grant,nonce)})),headers:{'content-type':'application/json'}});
    if (claim.status!==200 || claim.body.length>2048) throw Error();
    const ack=exactObject(JSON.parse(claim.body.toString('utf8')),['requestId','payloadHash','nonce','claimed','proof']);
    if (ack.claimed!==true || ack.requestId!==requestId || ack.payloadHash!==grant.payloadHash || ack.nonce!==nonce ||
        !egressMatches(egressMac(env.CALLBACK_EGRESS_CLAIM_KEY,'claimed',grant,nonce),ack.proof)) throw Error();
    claimed=true;
    if (grant.expiresAt*1000<=now()) throw Error();
    signal.throwIfAborted();
    const result=await post({url:payload.url,signal,body:Buffer.from(payload.body,'base64'),headers:payload.headers});
    // Only challenge responses are returned; application response content never leaves the gateway.
    return reply({requestId,state:result.status>=300&&result.status<400?'redirect_refused':'response',status:result.status,
      responseBody:grant.operation==='challenge'&&result.status>=200&&result.status<300?result.body.toString('base64'):null});
  } catch {
    // Refusal and uncertainty deliberately reveal no URL, bytes, signatures or exception detail.
    return reply({...(requestId?{requestId}:{}),state:claimed?'unknown':'unavailable'},503);
  } finally { clearTimeout(timer); request.signal.removeEventListener('abort',cancel); controller.abort(); }
}
