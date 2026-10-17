import {deriveProfileId} from './cryptography.ts';
import {WorkspaceError} from '../../shared/workspace-http.ts';
import {TrafficLimit} from './traffic-error.ts';
import {admitUpstream,observePacing} from './upstream-pacing.ts';
export {TrafficLimit} from './traffic-error.ts';
export const trafficLimits={publicGlobal:600,publicNetwork:120,enrollmentGlobal:30,enrollmentNetwork:10,mcpGlobal:600,mcpNetwork:120,mcpProfile:60,workspaceGlobal:600,workspaceNetwork:120,workspaceProfile:120,recovery:20,upstreamGlobal:300,upstreamProfile:60} as const;
async function metric(env:AuthEnv,name:'local_denied'|'upstream_429',now=Date.now()){
 const day=Math.floor(now/86400000);await env.HABITICA_INTEGRATION_DB.prepare(`INSERT INTO authorization_attempts(bucket,attempts,expires_at_utc) VALUES(?,1,?) ON CONFLICT(bucket) DO UPDATE SET attempts=MIN(attempts+1,2147483647)`).bind('metric:'+name+':'+day,new Date((day+1)*86400000).toISOString()).run();
}
export async function charge(env:AuthEnv,bucket:string,limit:number,now=Date.now()):Promise<void>{
 const window=Math.floor(now/60000);const expires=(window+1)*60000;
 const accepted=await env.HABITICA_INTEGRATION_DB.prepare(`INSERT INTO authorization_attempts(bucket,attempts,expires_at_utc) VALUES(?,1,?) ON CONFLICT(bucket) DO UPDATE SET attempts=attempts+1 WHERE attempts<? RETURNING attempts`).bind('traffic:'+bucket+':'+window,new Date(expires).toISOString(),limit).first();
 if(!accepted){await metric(env,'local_denied',now);throw new TrafficLimit(Math.max(1,Math.ceil((expires-now)/1000)));}
 await env.HABITICA_INTEGRATION_DB.prepare('DELETE FROM authorization_attempts WHERE rowid IN (SELECT rowid FROM authorization_attempts WHERE expires_at_utc<? LIMIT 100)').bind(new Date(now).toISOString()).run();
}
export async function networkBudget(env:AuthEnv,request:Request,lane:'public'|'enrollment'|'mcp'|'workspace'){
 const ip=request.headers.get('cf-connecting-ip')||(env.ENVIRONMENT==='local'?'local':null);
 if(!ip||ip.length>128)throw new TrafficLimit(60);
 await charge(env,lane+':global',trafficLimits[(lane+'Global') as keyof typeof trafficLimits]);
 const hash=await deriveProfileId('traffic-network:'+ip,env.PROFILE_ID_KEY);
 await charge(env,lane+':network:'+hash,trafficLimits[(lane+'Network') as keyof typeof trafficLimits]);
}
export async function profileBudget(env:AuthEnv,id:string,lane:'mcp'|'workspace',recovery=false){await charge(env,lane+':'+(recovery?'recovery:':'profile:')+id,recovery?trafficLimits.recovery:trafficLimits[lane==='mcp'?'mcpProfile':'workspaceProfile']);}
export async function upstreamBudget(env:AuthEnv,profileId?:string){
 const row=profileId?await env.HABITICA_INTEGRATION_DB.prepare('SELECT r.blocked_until_utc AS expires_at_utc,r.credential_repair_required FROM integration_read_state r JOIN integration_profiles p ON p.profile_id=r.profile_id AND p.connection_id=r.connection_id WHERE r.profile_id=?').bind(profileId).first<{expires_at_utc:string|null;credential_repair_required:number}>():await env.HABITICA_INTEGRATION_DB.prepare('SELECT expires_at_utc,0 AS credential_repair_required FROM authorization_attempts WHERE bucket=?').bind('cooldown:upstream:enrollment').first<{expires_at_utc:string|null;credential_repair_required:number}>();
 if(row?.credential_repair_required)throw new WorkspaceError('credential_repair_required',401);
 if(row?.expires_at_utc&&Date.parse(row.expires_at_utc)>Date.now())throw new TrafficLimit(Math.max(1,Math.ceil((Date.parse(row.expires_at_utc)-Date.now())/1000)));
 try{await admitUpstream(env,profileId);}catch(error){if(error instanceof TrafficLimit)await metric(env,'local_denied');throw error;}
 await charge(env,'upstream:global',trafficLimits.upstreamGlobal);if(profileId)await charge(env,'upstream:profile:'+profileId,trafficLimits.upstreamProfile);
}
export async function observeUpstream(env:AuthEnv,response:Response,profileId?:string){
 await observePacing(env,response,profileId);
 if(response.status!==429&&response.headers.get('x-ratelimit-remaining')!=='0')return;
 if(response.status===429)await metric(env,'upstream_429');
 if(profileId)return;
 const raw=response.headers.get('retry-after');const retry=raw&&/^\d{1,9}$/.test(raw)?Number(raw):raw?Math.ceil((Date.parse(raw)-Date.now())/1000):60;const reset=Number(response.headers.get('x-ratelimit-reset'))*1000;
 const end=Math.max(Date.now()+Math.max(1,Number.isFinite(retry)?retry:60)*1000,Number.isFinite(reset)&&reset<8640000000000000?reset:0);
 await env.HABITICA_INTEGRATION_DB.prepare(`INSERT INTO authorization_attempts(bucket,attempts,expires_at_utc) VALUES(?,1,?) ON CONFLICT(bucket) DO UPDATE SET expires_at_utc=MAX(expires_at_utc,excluded.expires_at_utc)`).bind('cooldown:upstream:'+(profileId??'enrollment'),new Date(end).toISOString()).run();
}
export async function trafficSummary(env:AuthEnv){const rows=await env.HABITICA_INTEGRATION_DB.prepare("SELECT bucket,attempts FROM authorization_attempts WHERE bucket IN (?,?) AND expires_at_utc>?").bind('metric:local_denied:'+Math.floor(Date.now()/86400000),'metric:upstream_429:'+Math.floor(Date.now()/86400000),new Date().toISOString()).all<{bucket:string;attempts:number}>();return {localQuotaDenials:rows.results.find(r=>r.bucket.startsWith('metric:local_denied:'))?.attempts??0,upstream429:rows.results.find(r=>r.bucket.startsWith('metric:upstream_429:'))?.attempts??0};}
export function trafficFailure(error:unknown):Response|null{return error instanceof TrafficLimit?Response.json({error:error.code,retryAtUtc:error.retryAtUtc},{status:429,headers:{'cache-control':'no-store','retry-after':String(error.retryAfterSeconds)}}):null;}

// One dispatch only. Accounting failures after a mutation remain unknown to callers.
export async function upstreamFetch(env:AuthEnv,input:string,init:RequestInit,profileId?:string):Promise<Response>{
 try{return await fetch(input,init);}
 catch(error){await observePacing(env,null,profileId);throw error;}
}
