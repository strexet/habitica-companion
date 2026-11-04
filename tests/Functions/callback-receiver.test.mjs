import test from 'node:test';
import assert from 'node:assert/strict';
import {createHash,createHmac,randomUUID} from 'node:crypto';
import {createRequire} from 'node:module';
import {readFileSync} from 'node:fs';
const require=createRequire(new URL('../../workers/chatgpt-auth/package.json',import.meta.url));
const {Miniflare,convertV4MiniflareOptions}=require('miniflare');
const key=Buffer.alloc(32,81),readKey=Buffer.alloc(32,82).toString('base64url'),run=randomUUID(),id=randomUUID();
const body=JSON.stringify({type:'verification',challenge:'synthetic-qualification-only'});
const schema='CREATE TABLE callback_qualification_observations(run_id TEXT NOT NULL,webhook_id TEXT NOT NULL,deliveries INTEGER NOT NULL,body_hash TEXT NOT NULL,signature_valid INTEGER NOT NULL,PRIMARY KEY(run_id,webhook_id))';
async function fixture(t,patch={}) {
 const mf=new Miniflare(convertV4MiniflareOptions({workers:[{name:'receiver',modules:true,script:readFileSync(new URL('../hosted/callback-receiver.mjs',import.meta.url),'utf8'),compatibilityDate:'2026-10-12',compatibilityFlags:['nodejs_compat'],d1Databases:['DB'],bindings:{QUALIFICATION_RUN:run,QUALIFICATION_WEBHOOK_ID:id,QUALIFICATION_KEY_B64:key.toString('base64'),QUALIFICATION_READ_KEY:readKey,QUALIFICATION_EXPIRES_AT:String(Date.now()+60000),QUALIFICATION_EXPECTED_SHA256:createHash('sha256').update(body).digest('hex'),...patch}}]}));
 t.after(()=>mf.dispose());await (await mf.getD1Database('DB','receiver')).exec(schema);const worker=await mf.getWorker('receiver');return {dispatchFetch:(...args)=>worker.fetch(...args),getD1Database:()=>mf.getD1Database('DB','receiver')};
}
function packet(text=body,patch={}) {
 const timestamp=String(Math.floor(Date.now()/1000));return {method:'POST',headers:{'content-type':'application/json','webhook-id':id,'webhook-timestamp':timestamp,'webhook-signature':'v1,'+createHmac('sha256',key).update(id+'.'+timestamp+'.'+text).digest('base64'),...patch},body:text};
}
const url='https://fixture.example.org/callback-qualification/'+run;
test('valid exact signed challenge echoes and records actual duplicate receiver calls in D1',async t=>{
 const mf=await fixture(t);
 for(let n=1;n<=2;n++){
  const r=await mf.dispatchFetch(url+'/receive',packet());assert.equal(r.status,200);assert.deepEqual(await r.json(),{challenge:'synthetic-qualification-only'});
  const status=await mf.dispatchFetch(url+'/status',{headers:{authorization:'Bearer '+readKey}});assert.equal(status.status,200);
  const data=await status.json();assert.equal(data.observations.length,1);assert.equal(data.observations[0].deliveries,n);assert.equal(data.observations[0].signature_valid,1);
  for(const secret of [body,key.toString('base64'),readKey])assert.equal(JSON.stringify(data).includes(secret),false);
 }
});
test('changed bytes, wrong signature, timestamp or identity never record a receiver delivery',async t=>{
 const mf=await fixture(t);
 for(const request of [packet(body+' '),packet(body,{'webhook-signature':'v1,'+Buffer.alloc(32).toString('base64')}),packet(body,{'webhook-timestamp':'1'}),packet(body,{'webhook-id':randomUUID()})])assert.notEqual((await mf.dispatchFetch(url+'/receive',request)).status,200);
 assert.equal((await (await mf.getD1Database('DB')).prepare('SELECT COUNT(*) AS n FROM callback_qualification_observations').first()).n,0);
});
test('unowned run, read access and expired fixtures fail closed',async t=>{
 const mf=await fixture(t);
 assert.equal((await mf.dispatchFetch(url+'/status')).status,403);
 assert.equal((await mf.dispatchFetch(url+'/status',{headers:{authorization:'Bearer invalid'}})).status,403);
 assert.equal((await mf.dispatchFetch('https://fixture.example.org/callback-qualification/'+randomUUID()+'/receive',packet())).status,404);
 const expired=await fixture(t,{QUALIFICATION_EXPIRES_AT:'1'});assert.equal((await expired.dispatchFetch(url+'/receive',packet())).status,503);
});
test('oversized and non-JSON ingress never records a delivery',async t=>{
 const mf=await fixture(t);
 assert.notEqual((await mf.dispatchFetch(url+'/receive',packet('x'.repeat(4097)))).status,200);
 assert.notEqual((await mf.dispatchFetch(url+'/receive',packet(body,{'content-type':'text/plain'}))).status,200);
 assert.equal((await (await mf.getD1Database('DB')).prepare('SELECT COUNT(*) AS n FROM callback_qualification_observations').first()).n,0);
});
