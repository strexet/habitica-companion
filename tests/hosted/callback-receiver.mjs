// Ephemeral synthetic fixture. Never bind application credentials or user payloads.
import {createHash,createHmac,timingSafeEqual} from 'node:crypto';
const reply=(body,status=200)=>Response.json(body,{status,headers:{'cache-control':'no-store','x-content-type-options':'nosniff'}});
const equal=(a,b)=>timingSafeEqual(createHash('sha256').update(a).digest(),createHash('sha256').update(b).digest());
async function readBody(request) {
 const reader=request.body?.getReader();if(!reader)throw Error();
 let size=0,timer;const chunks=[];
 const timeout=new Promise((_,reject)=>{timer=setTimeout(()=>{void reader.cancel().catch(()=>{});reject(Error());},5000);});
 try {
  while(true){const {done,value}=await Promise.race([reader.read(),timeout]);if(done)break;size+=value.length;if(size>4096)throw Error();chunks.push(value);}
  return Buffer.concat(chunks);
 } finally {clearTimeout(timer);await reader.cancel().catch(()=>{});reader.releaseLock();}
}
export default {async fetch(request,env) {
 try {
  const expiry=Number(env.QUALIFICATION_EXPIRES_AT),run=env.QUALIFICATION_RUN;
  if(!Number.isSafeInteger(expiry)||expiry<=Date.now()||!run||!env.QUALIFICATION_READ_KEY||!env.QUALIFICATION_KEY_B64)return reply({error:'fixture_unavailable'},503);
  const path=new URL(request.url).pathname,base='/callback-qualification/'+run;
  if(path===base+'/status'&&request.method==='GET'){
   if(!equal(request.headers.get('authorization')||'','Bearer '+env.QUALIFICATION_READ_KEY))return reply({error:'forbidden'},403);
   const rows=await env.DB.prepare('SELECT webhook_id,deliveries,body_hash,signature_valid FROM callback_qualification_observations WHERE run_id=?').bind(run).all();
   return reply({runId:run,observations:rows.results});
  }
  if(path!==base+'/receive'||request.method!=='POST')return reply({error:'not_found'},404);
  if(request.headers.get('content-type')!=='application/json')return reply({error:'invalid_packet'},422);
  const bytes=await readBody(request),id=request.headers.get('webhook-id'),timestamp=request.headers.get('webhook-timestamp')||'',signature=request.headers.get('webhook-signature')||'';
  const hash=createHash('sha256').update(bytes).digest('hex');
  if(id!==env.QUALIFICATION_WEBHOOK_ID||!/^\d{10}$/.test(timestamp)||Math.abs(Date.now()-Number(timestamp)*1000)>60000||hash!==env.QUALIFICATION_EXPECTED_SHA256)return reply({error:'invalid_packet'},422);
  const key=Buffer.from(env.QUALIFICATION_KEY_B64,'base64');if(key.length!==32)return reply({error:'fixture_unavailable'},503);
  const expected='v1,'+createHmac('sha256',key).update(id+'.'+timestamp+'.').update(bytes).digest('base64');
  if(!equal(signature,expected))return reply({error:'invalid_signature'},403);
  const body=JSON.parse(bytes.toString('utf8'));
  if(body.type!=='verification'||typeof body.challenge!=='string'||!body.challenge.startsWith('synthetic-')||body.challenge.length>200||Object.keys(body).sort().join(',')!=='challenge,type')return reply({error:'invalid_packet'},422);
  await env.DB.prepare('INSERT INTO callback_qualification_observations(run_id,webhook_id,deliveries,body_hash,signature_valid) VALUES(?,?,1,?,1) ON CONFLICT(run_id,webhook_id) DO UPDATE SET deliveries=deliveries+1').bind(run,id,hash).run();
  return reply({challenge:body.challenge});
 } catch {return reply({error:'fixture_unavailable'},503);}
}};
