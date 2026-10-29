import {reminderIntent,nextReminder,type ReminderIntent} from '../../shared/reminder-calendar.ts';
import {workspaceDocument} from '../../shared/workspace-domain.ts';
import {workspaceObject,uuid} from '../../shared/workspace-http.ts';
import {reminderPushStatus,reminderAuthority,reminderTask,reminderCueRevision,type ReminderSchedule} from './reminder-api.ts';
import type {WorkspaceRow} from './executor.ts';
interface Delivery {profile_id:string;occurrence_id:string;schedule_id:string;schedule_revision:number;civil_date:string;at_utc:string;expires_at_ms:number;device_id:string;device_revision:number;subscription_id:string;state:string;code:string;attempt:number;dispatch_id:string;retry_after_ms:number|null;http_status:number|null;attempts_json:string}
interface PushReceipt {requestId:string;state:string;code:string;httpStatus:number|null;retryAfterMs:number|null;acceptedByPushService:boolean;shown:null;read:null}
function pushReceipt(value:unknown):PushReceipt {
 const v=workspaceObject(value);
 if(typeof v.state!=='string'||!['accepted','refused','unknown','retryable'].includes(v.state)||typeof v.code!=='string'||!/^[a-z_]{1,64}$/.test(v.code)
  ||v.httpStatus!==null&&(!Number.isInteger(v.httpStatus)||Number(v.httpStatus)<100||Number(v.httpStatus)>599)||v.retryAfterMs!==null&&(!Number.isSafeInteger(v.retryAfterMs)||Number(v.retryAfterMs)<0)
  ||v.acceptedByPushService!==(v.state==='accepted')||v.shown!==null||v.read!==null)throw Error('Invalid reduced push receipt.');
 return {requestId:uuid(v.requestId),state:v.state,code:v.code,httpStatus:v.httpStatus as number|null,retryAfterMs:v.retryAfterMs as number|null,acceptedByPushService:v.state==='accepted',shown:null,read:null};
}
const text=(ms:number)=>new Date(ms).toISOString();
const leaseGuard=`EXISTS(SELECT 1 FROM workspace_reminder_runtime r JOIN workspaces w ON w.profile_id=r.profile_id WHERE r.profile_id=? AND r.access_epoch=? AND r.lease_token=? AND r.lease_until_ms>? AND w.active=1 AND w.connection_id=? AND w.access_epoch=r.access_epoch)`;
async function pushCall(env:WorkspaceEnv,path:string,body:unknown):Promise<{response:Response;value:Record<string,unknown>}> {
 const response=await env.AUTH_PUSH.fetch('https://push.internal/'+path,{method:'POST',body:JSON.stringify(body),signal:AbortSignal.timeout(10000)});
 return {response,value:await response.json<Record<string,unknown>>()};
}
export async function scheduledReminders(env:WorkspaceEnv,options:{now?:()=>number}={}):Promise<{profiles:number;attempts:number}> {
 if(!env.WORKSPACE_ENABLED||!env.PUSH_REMINDERS_ENABLED)return {profiles:0,attempts:0};
 const clock=options.now??Date.now,started=Date.now(),deadline=started+25000;
 const profiles=await env.WORKSPACE_DB.prepare(`SELECT due.profile_id FROM (
  SELECT profile_id FROM workspace_reminder_schedules WHERE state='active' AND next_at_utc<=?
  UNION SELECT profile_id FROM workspace_reminder_deliveries WHERE state IN ('scheduled','dispatched','retryable') AND (retry_after_ms IS NULL OR retry_after_ms<=?)
 ) due LEFT JOIN workspace_reminder_runtime r ON r.profile_id=due.profile_id GROUP BY due.profile_id ORDER BY COALESCE(r.last_wake_ms,0),due.profile_id LIMIT 20`).bind(text(clock()),clock()).all<{profile_id:string}>();
 let attempts=0,processed=0;
 for(const {profile_id:profile} of profiles.results){
  if(Date.now()>=deadline||attempts>=20)break;
  const row=await env.WORKSPACE_DB.prepare('SELECT * FROM workspaces WHERE profile_id=?').bind(profile).first<WorkspaceRow>();
  if(!row)continue;
  const token=crypto.randomUUID(),acquired=await env.WORKSPACE_DB.prepare(`INSERT INTO workspace_reminder_runtime(profile_id,access_epoch,lease_token,lease_until_ms,last_wake_ms) VALUES(?,?,?,?,?)
   ON CONFLICT(profile_id) DO UPDATE SET access_epoch=excluded.access_epoch,lease_token=excluded.lease_token,lease_until_ms=excluded.lease_until_ms,last_wake_ms=excluded.last_wake_ms WHERE workspace_reminder_runtime.lease_until_ms<=?`).bind(profile,row.access_epoch,token,Date.now()+45000,Date.now(),Date.now()).run();
  if(!acquired.meta.changes)continue;processed++;
  const guardArgs=()=>[profile,row.access_epoch,token,Date.now(),row.connection_id];
  const permitted=async()=>Boolean(await env.WORKSPACE_DB.prepare('SELECT 1 WHERE '+leaseGuard).bind(...guardArgs()).first());
  async function pause(id:string,revision:number,reason:string){await env.WORKSPACE_DB.prepare(`UPDATE workspace_reminder_schedules SET state='paused',reason=?,next_at_utc=NULL,next_civil_date=NULL,updated_at_utc=? WHERE profile_id=? AND schedule_id=? AND revision=?`).bind(reason,text(clock()),profile,id,revision).run();await env.WORKSPACE_DB.prepare("UPDATE workspace_reminder_deliveries SET state='cancelled',code=?,updated_at_utc=? WHERE profile_id=? AND schedule_id=? AND schedule_revision=? AND state IN ('scheduled','retryable')").bind(reason,text(clock()),profile,id,revision).run();}
  async function context(s:ReminderSchedule):Promise<boolean>{
   const w=await env.WORKSPACE_DB.prepare('SELECT * FROM workspaces WHERE profile_id=?').bind(profile).first<WorkspaceRow>();
   if(!w||!reminderAuthority(w)||s.connection_id!==w.connection_id||s.access_epoch!==w.access_epoch){await pause(s.schedule_id,s.revision,'workspace_context_changed');return false;}
   try {const document=workspaceDocument(JSON.parse(w.document_json)),intent=reminderIntent(JSON.parse(s.intent_json));reminderTask(document,intent);if(reminderCueRevision(document,s.task_id)!==s.cue_revision)throw Error();}
   catch {await pause(s.schedule_id,s.revision,'review_cue_changed');return false;}
   return permitted();
  }
  try {
   if(!reminderAuthority(row)){await env.WORKSPACE_DB.batch([env.WORKSPACE_DB.prepare("UPDATE workspace_reminder_schedules SET state='paused',reason='workspace_context_changed',next_at_utc=NULL,next_civil_date=NULL WHERE profile_id=? AND state='active'").bind(profile),env.WORKSPACE_DB.prepare("UPDATE workspace_reminder_deliveries SET state='unknown',code='authority_changed_after_dispatch' WHERE profile_id=? AND state='dispatched'").bind(profile),env.WORKSPACE_DB.prepare("UPDATE workspace_reminder_deliveries SET state='cancelled',code='workspace_context_changed' WHERE profile_id=? AND state IN ('scheduled','retryable')").bind(profile)]);continue;}
   let push=null;try{push=await reminderPushStatus(env,row);}catch {/* Expired cues and original-identity recovery can still progress without current status. */}
   const schedules=await env.WORKSPACE_DB.prepare("SELECT * FROM workspace_reminder_schedules WHERE profile_id=? AND state='active' AND next_at_utc<=? ORDER BY next_at_utc,schedule_id LIMIT 20").bind(profile,text(clock())).all<ReminderSchedule>();
   for(const s of schedules.results){
    if(Date.now()>=deadline||!await permitted())break;
    if(!await context(s))continue;
    const intent=reminderIntent(JSON.parse(s.intent_json)),at=s.next_at_utc!,expires=Date.parse(at)+3600000;
    if(expires>clock()){
     if(push===null)continue;
     if(!push.dispatchAvailable)continue;
     if(!push.enabled||push.consentEpoch!==s.consent_epoch){await pause(s.schedule_id,s.revision,'push_consent_changed');continue;}
     if(intent.targets.some(t=>!push.devices.some(d=>d.deviceId===t.deviceId&&d.revision===t.revision&&d.subscriptionId===t.subscriptionId&&d.state==='active'&&(d.expiresAtMs===null||d.expiresAtMs>clock())))){await pause(s.schedule_id,s.revision,'push_device_changed');continue;}
    }
    const count=await env.WORKSPACE_DB.prepare('SELECT COUNT(*) AS n FROM workspace_reminder_deliveries WHERE profile_id=?').bind(profile).first<{n:number}>();
    if(!count||count.n+intent.targets.length>8192){await pause(s.schedule_id,s.revision,'receipt_capacity');continue;}
    const occurrence=crypto.randomUUID(),next=nextReminder(intent,at),statements=[];
    for(const target of intent.targets)statements.push(env.WORKSPACE_DB.prepare(`INSERT OR IGNORE INTO workspace_reminder_deliveries(profile_id,occurrence_id,schedule_id,schedule_revision,civil_date,at_utc,expires_at_ms,device_id,device_revision,subscription_id,state,code,attempt,dispatch_id,updated_at_utc)
     SELECT ?,?,?,?,?,?,?,?,?,?,?,?,?,?,? WHERE ${leaseGuard} AND EXISTS(SELECT 1 FROM workspace_reminder_schedules WHERE profile_id=? AND schedule_id=? AND revision=? AND state='active' AND next_at_utc=?)`)
     .bind(profile,occurrence,s.schedule_id,s.revision,s.next_civil_date!,at,expires,target.deviceId,target.revision,target.subscriptionId,expires<=clock()?'expired':'scheduled',expires<=clock()?'cue_expired':'scheduled',0,crypto.randomUUID(),text(clock()),...guardArgs(),profile,s.schedule_id,s.revision,at));
    statements.push(env.WORKSPACE_DB.prepare(`UPDATE workspace_reminder_schedules SET next_at_utc=?,next_civil_date=?,updated_at_utc=? WHERE profile_id=? AND schedule_id=? AND revision=? AND state='active' AND next_at_utc=? AND ${leaseGuard}`).bind(next?.atUtc??null,next?.civilDate??null,text(clock()),profile,s.schedule_id,s.revision,at,...guardArgs()));
    await env.WORKSPACE_DB.batch(statements);
   }
   const deliveries=await env.WORKSPACE_DB.prepare("SELECT * FROM workspace_reminder_deliveries WHERE profile_id=? AND state IN ('scheduled','dispatched','retryable') AND (retry_after_ms IS NULL OR retry_after_ms<=?) ORDER BY at_utc,occurrence_id,device_id LIMIT 20").bind(profile,clock()).all<Delivery>();
   for(let d of deliveries.results){
    if(Date.now()>=deadline||attempts>=20||!await permitted())break;
    const s=await env.WORKSPACE_DB.prepare('SELECT * FROM workspace_reminder_schedules WHERE profile_id=? AND schedule_id=?').bind(profile,d.schedule_id).first<ReminderSchedule>();
    if(!s)continue;
    if(d.state!=='dispatched'&&(s.state!=='active'||s.revision!==d.schedule_revision||!await context(s))){await env.WORKSPACE_DB.prepare("UPDATE workspace_reminder_deliveries SET state='cancelled',code='schedule_changed',updated_at_utc=? WHERE profile_id=? AND occurrence_id=? AND device_id=? AND state IN ('scheduled','retryable')").bind(text(clock()),profile,d.occurrence_id,d.device_id).run();continue;}
    if(d.state!=='dispatched'&&d.expires_at_ms<=clock()){await env.WORKSPACE_DB.prepare("UPDATE workspace_reminder_deliveries SET state='expired',code='cue_expired',updated_at_utc=? WHERE profile_id=? AND occurrence_id=? AND device_id=? AND state IN ('scheduled','retryable')").bind(text(clock()),profile,d.occurrence_id,d.device_id).run();continue;}
    let result:PushReceipt|null=null;
    if(d.state==='dispatched'){
     try{const original=await pushCall(env,'receipt',{profileId:profile,connectionId:row.connection_id,requestId:d.dispatch_id});if(original.response.ok&&original.value.receipt)result=pushReceipt(original.value.receipt);}catch{/* No resend after lost service response. */}
     if(!result)result={requestId:d.dispatch_id,state:'unknown',code:'original_receipt_unavailable',httpStatus:null,retryAfterMs:null,acceptedByPushService:false,shown:null,read:null};
    } else {
     let latest;try{latest=await reminderPushStatus(env,row);}catch {continue;}
     if(!latest.available||!latest.dispatchAvailable)continue;
     if(!latest.enabled||latest.consentEpoch!==s.consent_epoch||!latest.devices.some(t=>t.deviceId===d.device_id&&t.revision===d.device_revision&&t.subscriptionId===d.subscription_id&&t.state==='active')){await pause(s.schedule_id,s.revision,'push_device_or_consent_changed');continue;}
     if(d.attempt>=3){await env.WORKSPACE_DB.prepare("UPDATE workspace_reminder_deliveries SET state='refused',code='retry_limit' WHERE profile_id=? AND occurrence_id=? AND device_id=?").bind(profile,d.occurrence_id,d.device_id).run();continue;}
     const dispatchId=d.attempt===0?d.dispatch_id:crypto.randomUUID(),attempt=d.attempt+1,history=JSON.parse(d.attempts_json) as Record<string,unknown>[];
     history.push({attempt,requestId:dispatchId,state:'dispatched',atUtc:text(clock())});
     const begun=await env.WORKSPACE_DB.prepare(`UPDATE workspace_reminder_deliveries SET state='dispatched',code='attempted',attempt=?,dispatch_id=?,attempts_json=?,retry_after_ms=NULL,updated_at_utc=? WHERE profile_id=? AND occurrence_id=? AND device_id=? AND state IN ('scheduled','retryable') AND attempt=? AND ${leaseGuard}
      AND EXISTS(SELECT 1 FROM workspace_reminder_schedules WHERE profile_id=? AND schedule_id=? AND revision=? AND state='active')`).bind(attempt,dispatchId,JSON.stringify(history),text(clock()),profile,d.occurrence_id,d.device_id,d.attempt,...guardArgs(),profile,s.schedule_id,s.revision).run();
     if(!begun.meta.changes)continue;
     d={...d,attempt,dispatch_id:dispatchId,attempts_json:JSON.stringify(history),state:'dispatched'};attempts++;
     try {
      const sent=await pushCall(env,'dispatch',{profileId:profile,connectionId:row.connection_id,requestId:dispatchId,deviceId:d.device_id,deviceRevision:d.device_revision,subscriptionId:d.subscription_id,consentEpoch:s.consent_epoch,expiresAtMs:d.expires_at_ms,workspaceId:row.workspace_id,taskId:s.task_id,scheduleId:s.schedule_id,scheduleRevision:s.revision,occurrenceId:d.occurrence_id});
      if(sent.response.ok)result=pushReceipt(sent.value);
      else if(sent.value.outcomeUnknown===false)result={requestId:dispatchId,state:sent.response.status===429?'retryable':'refused',code:typeof sent.value.error==='string'?sent.value.error:'auth_refused',httpStatus:null,retryAfterMs:sent.response.status===429?clock()+60000:null,acceptedByPushService:false,shown:null,read:null};
     } catch {/* Recover only the original Auth attempt on the next wake. */}
    }
    if(!result)continue;
    if(result.requestId!==d.dispatch_id||!['accepted','refused','unknown','retryable'].includes(result.state)){result={requestId:d.dispatch_id,state:'unknown',code:'invalid_original_receipt',httpStatus:null,retryAfterMs:null,acceptedByPushService:false,shown:null,read:null};}
    const history=JSON.parse(d.attempts_json) as Record<string,unknown>[];if(history.length)history[history.length-1]={...history[history.length-1],...result,finishedAtUtc:text(clock())};
    const retry=result.state==='retryable'&&d.attempt<3&&result.retryAfterMs!==null&&result.retryAfterMs<d.expires_at_ms;
    const state=result.state==='retryable'&&!retry?(clock()>=d.expires_at_ms||result.retryAfterMs!==null&&result.retryAfterMs>=d.expires_at_ms?'expired':'refused'):result.state;
    await env.WORKSPACE_DB.prepare(`UPDATE workspace_reminder_deliveries SET state=?,code=?,http_status=?,retry_after_ms=?,attempts_json=?,updated_at_utc=? WHERE profile_id=? AND occurrence_id=? AND device_id=? AND dispatch_id=? AND state='dispatched' AND ${leaseGuard}`).bind(state,result.code,result.httpStatus,retry?result.retryAfterMs:null,JSON.stringify(history),text(clock()),profile,d.occurrence_id,d.device_id,d.dispatch_id,...guardArgs()).run();
   }
   await env.WORKSPACE_DB.prepare(`UPDATE workspace_reminder_schedules SET state='finished',reason='occurrences_finished',updated_at_utc=? WHERE profile_id=? AND state='active' AND next_at_utc IS NULL AND NOT EXISTS(SELECT 1 FROM workspace_reminder_deliveries d WHERE d.profile_id=workspace_reminder_schedules.profile_id AND d.schedule_id=workspace_reminder_schedules.schedule_id AND d.schedule_revision=workspace_reminder_schedules.revision AND d.state IN ('scheduled','dispatched','retryable')) AND ${leaseGuard}`).bind(text(clock()),profile,...guardArgs()).run();
  } finally {await env.WORKSPACE_DB.prepare("UPDATE workspace_reminder_runtime SET lease_token='',lease_until_ms=0 WHERE profile_id=? AND lease_token=?").bind(profile,token).run();}
 }
 return {profiles:processed,attempts};
}
