import {TrafficLimit} from './traffic-error.ts';

interface Bucket {
 bucket:string;version:number;capacity:number;tokens:number;updated_ms:number;next_ms:number;
 reset_ms:number;cooldown_ms:number;recovery:number;
}
const lifetime=86400000;
const profileBucket=(profileId?:string)=>profileId?'profile:'+profileId:'enrollment';
function estimate(row:Bucket,now:number){
 const tokens=Math.min(row.capacity,row.tokens+Math.max(0,now-row.updated_ms)*row.capacity/60000);
 const reserve=Math.min(2,Math.max(0,row.capacity-1));
 const ready=Math.max(row.next_ms,row.cooldown_ms,now+Math.max(0,reserve+1-tokens)*60000/row.capacity);
 const after=tokens-1;
 const depletion=Math.max(0,Math.min(1,(row.capacity*.5-after)/Math.max(1,row.capacity*.5-reserve)));
 let gap=60000/row.capacity*depletion;
 if(after<row.capacity*.5&&row.reset_ms>now)gap=Math.max(gap,(row.reset_ms-now)/Math.max(1,tokens-reserve));
 if(row.recovery>1)gap=Math.max(gap,60000/row.capacity*row.recovery);
 return {tokens:Math.max(0,after),ready,next:now+Math.ceil(gap)};
}
async function initialize(env:AuthEnv,profileId:string|undefined,now:number){
 const db=env.HABITICA_INTEGRATION_DB;
 await db.batch([
  db.prepare('INSERT INTO upstream_pacing(bucket,capacity,tokens,updated_ms,expires_ms) VALUES(\'global\',300,300,?,?) ON CONFLICT(bucket) DO NOTHING').bind(now,now+lifetime),
  db.prepare('INSERT INTO upstream_pacing(bucket,profile_id,capacity,tokens,updated_ms,expires_ms) VALUES(?,?,30,30,?,?) ON CONFLICT(bucket) DO NOTHING').bind(profileBucket(profileId),profileId??null,now,now+lifetime),
 ]);
}
export async function admitUpstream(env:AuthEnv,profileId?:string,now=Date.now()):Promise<void>{
 if(!env.ADAPTIVE_PACING_ENABLED)return;
 await initialize(env,profileId,now);
 const db=env.HABITICA_INTEGRATION_DB;const key=profileBucket(profileId);
 const rows=(await db.prepare('SELECT * FROM upstream_pacing WHERE bucket IN (\'global\',?)').bind(key).all<Bucket>()).results;
 const global=rows.find(r=>r.bucket==='global')!,profile=rows.find(r=>r.bucket===key)!;
 if(!global||!profile)throw new TrafficLimit(1);
 const g=estimate(global,now),p=estimate(profile,now),ready=Math.max(g.ready,p.ready);
 if(ready>now)throw new TrafficLimit(Math.max(1,Math.ceil((ready-now)/1000)));
 const reservation=crypto.randomUUID();
 // A transactional batch cannot interleave. The global CAS verifies both original
 // versions; the profile update requires the exact new reservation. No denied
 // profile spends global capacity, and concurrent Worker instances share this gate.
 const result=await db.batch([
  db.prepare(`UPDATE upstream_pacing SET tokens=?,updated_ms=?,next_ms=?,reservation=?,version=version+1,expires_ms=?
   WHERE bucket='global' AND version=? AND EXISTS(SELECT 1 FROM upstream_pacing WHERE bucket=? AND version=?)`)
   .bind(g.tokens,now,g.next,reservation,now+lifetime,global.version,key,profile.version),
  db.prepare(`UPDATE upstream_pacing SET tokens=?,updated_ms=?,next_ms=?,reservation=?,version=version+1,expires_ms=?
   WHERE bucket=? AND version=? AND EXISTS(SELECT 1 FROM upstream_pacing WHERE bucket='global' AND reservation=?)`)
   .bind(p.tokens,now,p.next,reservation,now+lifetime,key,profile.version,reservation),
 ]);
 if(!result[0].meta.changes||!result[1].meta.changes)throw new TrafficLimit(1);
 await db.prepare('DELETE FROM upstream_pacing WHERE rowid IN (SELECT rowid FROM upstream_pacing WHERE expires_ms<? AND cooldown_ms<=? LIMIT 100)').bind(now,now).run();
}
function headerNumber(response:Response,name:string):number|undefined{
 const raw=response.headers.get(name);if(!raw||!/^\d{1,12}$/.test(raw))return;
 const value=Number(raw);return Number.isSafeInteger(value)?value:undefined;
}
export async function observePacing(env:AuthEnv,response:Response|null,profileId?:string,now=Date.now(),jitter=Math.random()){
 if(!env.ADAPTIVE_PACING_ENABLED)return;
 const failure=!response||response.status===429||response.status>=500;
 const success=Boolean(response?.ok);
 const capacity=response?headerNumber(response,'x-ratelimit-limit'):undefined;
 const remaining=response?headerNumber(response,'x-ratelimit-remaining'):undefined;
 const rawReset=response?headerNumber(response,'x-ratelimit-reset'):undefined;
 const reset=rawReset!==undefined&&rawReset*1000<8640000000000000?rawReset*1000:0;
 const rawRetry=response?.headers.get('retry-after');
 const retry=rawRetry&&/^\d{1,9}$/.test(rawRetry)?now+Number(rawRetry)*1000:rawRetry?Date.parse(rawRetry):0;
 const cooldown=Math.max(Number.isFinite(retry)?retry:0,remaining===0?reset:0,response?.status===429?now+60000:0);
 // Header budgets may describe User ID or IP. Never credit a larger observed
 // remaining value across concurrent reservations; lower both estimates conservatively.
 await env.HABITICA_INTEGRATION_DB.prepare(`UPDATE upstream_pacing SET
  capacity=MIN(capacity,?),tokens=MIN(tokens,?,?),updated_ms=MAX(updated_ms,?),reset_ms=MAX(reset_ms,?),
  cooldown_ms=MAX(cooldown_ms,?,CASE WHEN ? THEN ?+MIN(16,recovery*2)*1000+? ELSE 0 END),
  recovery=CASE WHEN ? THEN MIN(16,recovery*2) WHEN ? THEN MAX(1,recovery-0.125) ELSE recovery END,
  version=version+1,expires_ms=MAX(expires_ms,?) WHERE bucket IN ('global',?)`)
  .bind(capacity&&capacity>0?capacity:Number.MAX_SAFE_INTEGER,remaining??Number.MAX_SAFE_INTEGER,capacity&&capacity>0?capacity:Number.MAX_SAFE_INTEGER,now,reset,cooldown,Number(failure),now,Math.max(0,Math.min(1,jitter))*500,Number(failure),Number(success),now+lifetime,profileBucket(profileId)).run();
}
export async function pacingSummary(env:AuthEnv,now=Date.now()){
 const row=await env.HABITICA_INTEGRATION_DB.prepare('SELECT SUM(CASE WHEN recovery>1 THEN 1 ELSE 0 END) AS recovering,SUM(CASE WHEN cooldown_ms>? THEN 1 ELSE 0 END) AS cooling FROM upstream_pacing WHERE expires_ms>?').bind(now,now).first<{recovering:number;cooling:number|null}>();
 return {recoveringBuckets:row?.recovering??0,coolingBuckets:row?.cooling??0};
}
export async function purgePacing(env:AuthEnv,now=Date.now()){
 await env.HABITICA_INTEGRATION_DB.prepare('DELETE FROM upstream_pacing WHERE rowid IN (SELECT rowid FROM upstream_pacing WHERE expires_ms<? AND cooldown_ms<=? LIMIT 100)').bind(now,now).run();
}
