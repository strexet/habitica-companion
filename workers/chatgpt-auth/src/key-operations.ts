import {timingSafeEqual} from 'node:crypto';
import {encryptCredential,decryptCredential,validateKeySeparation,decode} from './cryptography.ts';
import {workspaceObject,workspaceJson,uuid,WorkspaceError} from '../../shared/workspace-http.ts';
type Env=Pick<AuthEnv,'HABITICA_INTEGRATION_DB'|'CREDENTIAL_KEY_V1'|'CREDENTIAL_KEY_V2'|'CREDENTIAL_CURRENT_KEY_VERSION'|'PROFILE_ID_KEY'>;
interface Job {job_id:string;from_version:string;to_version:string;last_profile_id:string|null;scanned:number;migrated:number;skipped:number;failures:number;status:string;failure_code:string|null}
interface Envelope {profile_id:string;credential_ciphertext:string;credential_iv:string;credential_key_version:string;credential_schema_version:1;generation:number}
const now=()=>new Date().toISOString();
const versions=['1','2'];
const keys=(env:Env)=>({'1':env.CREDENTIAL_KEY_V1,'2':env.CREDENTIAL_KEY_V2});
const version=(value:unknown)=>{if(typeof value!=='string'||!versions.includes(value))throw new WorkspaceError('unsupported_key_version',422);return value;};
const summary=(job:Job)=>({jobId:job.job_id,fromVersion:job.from_version,toVersion:job.to_version,scanned:job.scanned,migrated:job.migrated,skipped:job.skipped,failures:job.failures,status:job.status,failureCode:job.failure_code});
const load=(env:Env,id:string)=>env.HABITICA_INTEGRATION_DB.prepare('SELECT * FROM credential_key_jobs WHERE job_id=?').bind(id).first<Job>();
function configured(env:Env,from:string,to:string){
 if(to!==env.CREDENTIAL_CURRENT_KEY_VERSION)throw new WorkspaceError('current_key_changed',409);
 if(!keys(env)[from as '1'|'2']||!keys(env)[to as '1'|'2'])throw new WorkspaceError('required_key_unavailable',409);
 try{validateKeySeparation(env.PROFILE_ID_KEY,keys(env));if(timingSafeEqual(decode(keys(env)[from as '1'|'2']!),decode(keys(env)[to as '1'|'2']!)))throw new Error('distinct encryption keys required');}catch{throw new WorkspaceError('invalid_key_configuration',409);}
}
export async function purgeKeyJobs(env:Env){
 await env.HABITICA_INTEGRATION_DB.prepare('DELETE FROM credential_key_jobs WHERE job_id IN (SELECT job_id FROM credential_key_jobs WHERE updated_at_utc<? AND (lease_until_utc IS NULL OR lease_until_utc<=?) LIMIT 100)').bind(new Date(Date.now()-30*86400000).toISOString(),now()).run();
}
export async function keyInventory(env:Env){
 const rows=await env.HABITICA_INTEGRATION_DB.prepare('SELECT credential_key_version AS version,COUNT(*) AS count FROM integration_profiles WHERE credential_ciphertext IS NOT NULL GROUP BY credential_key_version').all<{version:string;count:number}>();
 const references:Record<string,number>={'1':0,'2':0,unsupported:0};
 for(const row of rows.results)references[versions.includes(row.version)?row.version:'unsupported']+=row.count;
 const unfinished=await env.HABITICA_INTEGRATION_DB.prepare("SELECT COUNT(*) AS count FROM credential_key_jobs WHERE status<>'complete'").first<{count:number}>();
 return {currentVersion:version(env.CREDENTIAL_CURRENT_KEY_VERSION),references,configured:{'1':Boolean(env.CREDENTIAL_KEY_V1),'2':Boolean(env.CREDENTIAL_KEY_V2)},missingReferencedKeys:versions.filter(v=>references[v]>0&&!keys(env)[v as '1'|'2']),unfinishedJobs:unfinished?.count??0};
}
export async function assertKeyRemovable(env:Env,keyVersion:string){
 const selected=version(keyVersion),inventory=await keyInventory(env);
 if(inventory.references.unsupported>0)throw new WorkspaceError('key_inventory_incomplete',409);
 const active=await env.HABITICA_INTEGRATION_DB.prepare("SELECT 1 FROM credential_key_jobs WHERE status<>'complete' AND (from_version=? OR to_version=?) LIMIT 1").bind(selected,selected).first();
 if(selected===inventory.currentVersion||inventory.references[selected]>0||active)throw new WorkspaceError('key_still_required',409);
 return {keyVersion:selected,removable:true,notice:'Stop old-version writers and repeat inventory immediately before separately authorized secret removal.'};
}
export async function startKeyJob(env:Env,jobId:string,fromVersion:string){
 await purgeKeyJobs(env);
 const id=uuid(jobId),from=version(fromVersion),to=version(env.CREDENTIAL_CURRENT_KEY_VERSION);if(from===to)throw new WorkspaceError('source_is_current_key',409);configured(env,from,to);
 const existing=await load(env,id);if(existing){if(existing.from_version!==from||existing.to_version!==to)throw new WorkspaceError('job_id_reused',409);return summary(existing);}
 const time=now();await env.HABITICA_INTEGRATION_DB.prepare("INSERT OR IGNORE INTO credential_key_jobs(job_id,from_version,to_version,status,created_at_utc,updated_at_utc) SELECT ?,?,?,'running',?,? WHERE NOT EXISTS(SELECT 1 FROM credential_key_jobs WHERE status<>'complete' AND from_version=?)").bind(id,from,to,time,time,from).run();
 const job=await load(env,id);if(!job)throw new WorkspaceError('key_job_busy',409);if(job.from_version!==from||job.to_version!==to)throw new WorkspaceError('job_id_reused',409);return summary(job);
}
export async function stepKeyJob(env:Env,jobId:string,limit=10){
 await purgeKeyJobs(env);
 const id=uuid(jobId);if(!Number.isInteger(limit)||limit<1||limit>25)throw new WorkspaceError('invalid_batch_limit',422);
 const initial=await load(env,id);if(!initial)throw new WorkspaceError('key_job_not_found',404);if(initial.status==='complete')return summary(initial);configured(env,initial.from_version,initial.to_version);
 const lease=crypto.randomUUID(),deadline=Date.now()+30000;
 const acquired=await env.HABITICA_INTEGRATION_DB.prepare("UPDATE credential_key_jobs SET lease_token=?,lease_until_utc=?,status='running',failure_code=NULL WHERE job_id=? AND status<>'complete' AND (lease_until_utc IS NULL OR lease_until_utc<=?)").bind(lease,new Date(Date.now()+90000).toISOString(),id,now()).run();
 if(!acquired.meta.changes)throw new WorkspaceError('key_job_busy',409);
 try{
  const job=(await load(env,id))!;
  const rows=await env.HABITICA_INTEGRATION_DB.prepare('SELECT profile_id,credential_ciphertext,credential_iv,credential_key_version,credential_schema_version,generation FROM integration_profiles WHERE credential_key_version=? AND credential_ciphertext IS NOT NULL AND profile_id>? ORDER BY profile_id LIMIT ?').bind(job.from_version,job.last_profile_id??'',limit).all<Envelope>();
  let processed=0;
  for(const row of rows.results){
   if(Date.now()>=deadline)break;
   let encrypted;
   try{
    const credential=await decryptCredential(row.profile_id,{ciphertext:row.credential_ciphertext,iv:row.credential_iv,keyVersion:row.credential_key_version,schemaVersion:row.credential_schema_version},keys(env));
    encrypted=await encryptCredential(row.profile_id,credential,job.to_version,keys(env));
   }catch{
    await env.HABITICA_INTEGRATION_DB.prepare("UPDATE credential_key_jobs SET status='blocked',failure_code='credential_unreadable',failures=failures+1,updated_at_utc=? WHERE job_id=? AND lease_token=? AND lease_until_utc>?").bind(now(),id,lease,now()).run();return summary((await load(env,id))!);
   }
   // A D1 transaction couples each generation-checked envelope write with its cursor/counters.
   const leaseCheck=now();
   const changed=await env.HABITICA_INTEGRATION_DB.batch([
    env.HABITICA_INTEGRATION_DB.prepare(`UPDATE integration_profiles SET credential_ciphertext=?,credential_iv=?,credential_key_version=?,credential_schema_version=?,generation=generation+1 WHERE profile_id=? AND generation=? AND generation<9007199254740991 AND credential_ciphertext=? AND credential_key_version=? AND EXISTS(SELECT 1 FROM credential_key_jobs WHERE job_id=? AND lease_token=? AND lease_until_utc>?)`).bind(encrypted.ciphertext,encrypted.iv,encrypted.keyVersion,encrypted.schemaVersion,row.profile_id,row.generation,row.credential_ciphertext,job.from_version,id,lease,leaseCheck),
    env.HABITICA_INTEGRATION_DB.prepare('UPDATE credential_key_jobs SET scanned=scanned+1,migrated=migrated+changes(),skipped=skipped+1-changes(),last_profile_id=?,updated_at_utc=? WHERE job_id=? AND lease_token=? AND lease_until_utc>?').bind(row.profile_id,leaseCheck,id,lease,leaseCheck),
   ]);
   if(!changed[1].meta.changes)throw new WorkspaceError('key_job_lease_lost',409);processed++;
  }
  if(processed===rows.results.length&&rows.results.length<limit){
   const remaining=await env.HABITICA_INTEGRATION_DB.prepare('SELECT COUNT(*) AS count FROM integration_profiles WHERE credential_key_version=? AND credential_ciphertext IS NOT NULL').bind(job.from_version).first<{count:number}>();
   await env.HABITICA_INTEGRATION_DB.prepare("UPDATE credential_key_jobs SET status=?,last_profile_id=NULL,updated_at_utc=? WHERE job_id=? AND lease_token=? AND lease_until_utc>?").bind(remaining?.count?'running':'complete',now(),id,lease,now()).run();
  }
  return summary((await load(env,id))!);
 }finally{await env.HABITICA_INTEGRATION_DB.prepare('UPDATE credential_key_jobs SET lease_token=NULL,lease_until_utc=NULL WHERE job_id=? AND lease_token=?').bind(id,lease).run();}
}
export async function keyOperation(request:Request,env:AuthEnv){
 const headers={'cache-control':'no-store'};
 try{
  if(!env.KEY_OPERATIONS_ENABLED||request.method!=='POST')throw new WorkspaceError('key_operations_unavailable',503);
  const raw=workspaceObject(await workspaceJson(request,4096));const operation=raw.operation;
  const fields=operation==='inventory'?['operation']:operation==='start'?['operation','jobId','fromVersion']:operation==='step'?['operation','jobId','limit']:operation==='assert-removable'?['operation','keyVersion']:null;
  if(!fields)throw new WorkspaceError('invalid_key_operation',422);const input=workspaceObject(raw,fields);if(operation==='step'&&input.limit!==undefined&&typeof input.limit!=='number')throw new WorkspaceError('invalid_batch_limit',422);
  const result=operation==='inventory'?await keyInventory(env):operation==='start'?await startKeyJob(env,uuid(input.jobId),version(input.fromVersion)):operation==='step'?await stepKeyJob(env,uuid(input.jobId),input.limit===undefined?10:Number(input.limit)):await assertKeyRemovable(env,version(input.keyVersion));
  return Response.json(result,{headers});
 }catch(error){return Response.json({error:error instanceof WorkspaceError?error.code:'key_operation_unavailable'},{status:error instanceof WorkspaceError?error.status:503,headers});}
}
