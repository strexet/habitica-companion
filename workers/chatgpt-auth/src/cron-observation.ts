import {activeProfile,credentialKeys} from './vault';
import {decryptCredential} from './cryptography';
import {upstream} from './reads';
import {workspaceObject,uuid,WorkspaceError} from '../../shared/workspace-http';
import {habiticaDay} from '../../shared/cron';
import {spellStateHash as gameplayStateHash} from '../../shared/spells';
import type {AuthProps} from '../../shared/contracts';
const timestamp=(value:unknown)=>typeof value==='string'&&value.length<=128&&Number.isFinite(Date.parse(value))?new Date(value).toISOString():null;
export async function cronUser(env:AuthEnv,who:AuthProps){
 const profile=await activeProfile(env,who.profileId,who.connectionId);
 if(profile.credential_schema_version!==1)throw new WorkspaceError('cron_unavailable',503);
 const credential=await decryptCredential(who.profileId,{ciphertext:profile.credential_ciphertext!,iv:profile.credential_iv!,keyVersion:profile.credential_key_version!,schemaVersion:1},credentialKeys(env));
 const user=workspaceObject((await upstream(env,who.profileId,who.connectionId,'user')).data);
 if(uuid(user._id??user.id)!==credential.habiticaUserId)throw new WorkspaceError('identity_mismatch',502);
 return {user,credential};
}
export async function cronProgress(env:AuthEnv,who:AuthProps,previousLastCron:unknown){
 const {user}=await cronUser(env,who);await activeProfile(env,who.profileId,who.connectionId);
 const lastCron=timestamp(user.lastCron);const previous=timestamp(previousLastCron);
 return {lastCron,needsCron:typeof user.needsCron==='boolean'?user.needsCron:null,dayProgressObserved:lastCron&&previous?lastCron>previous:null,confirmsRequest:false,retrievedAtUtc:new Date().toISOString()};
}
export async function cronObservation(env:AuthEnv,who:AuthProps){
 const {user,credential}=await cronUser(env,who);const prefs=workspaceObject(user.preferences);const stats=workspaceObject(user.stats);
 const day=habiticaDay(Date.now(),prefs.dayStart,prefs.timezoneOffset);const lastCron=timestamp(user.lastCron);
 if(!lastCron||Date.parse(lastCron)>Date.now()+5000||typeof prefs.sleep!=='boolean'||typeof stats.hp!=='number'||!Number.isFinite(stats.hp)||typeof stats.mp!=='number'||!Number.isFinite(stats.mp)||stats.mp<0||!stats.buffs||typeof stats.buffs!=='object'||Array.isArray(stats.buffs)||!Number.isSafeInteger(stats.lvl)||Number(stats.lvl)<1)throw new WorkspaceError('cron_precondition_unknown',409);
 const equipped=workspaceObject(workspaceObject(workspaceObject(user.items).gear).equipped);
 const needsCron=typeof user.needsCron==='boolean'?user.needsCron:null;
 const lastOffset=prefs.timezoneOffsetAtLastCron;
 const reason=needsCron===null?'cron_precondition_unknown':!needsCron?'cron_not_needed':stats.hp<=0?'not_alive':lastCron>=day.dayStartUtc?'cron_state_conflict':lastOffset!==undefined&&lastOffset!==null&&(!Number.isInteger(lastOffset)||Math.abs(Number(lastOffset))>840||lastOffset!==prefs.timezoneOffset)?'timezone_transition_review_required':null;
 const raw=(await upstream(env,who.profileId,who.connectionId,'tasks/user?history=false&dueDate='+encodeURIComponent(day.reviewDateUtc))).data;
 if(!Array.isArray(raw)||raw.length>1000)throw new WorkspaceError('cron_task_capacity',413);
 const seen=new Set<string>();
 const tasks=raw.filter(value=>workspaceObject(value).type!=='reward').map(value=>{
  const t=workspaceObject(value);const id=uuid(t.id??t._id);
  if(seen.has(id)||uuid(t.userId)!==credential.habiticaUserId||!['daily','habit','todo'].includes(String(t.type))||typeof t.text!=='string'||t.text.length>2048||typeof t.completed!=='boolean'||!Array.isArray(t.checklist)||t.checklist.length>100||t.type==='daily'&&typeof t.isDue!=='boolean')throw new WorkspaceError('invalid_cron_task',502);
  seen.add(id);const checklist=t.checklist.map(value=>{const c=workspaceObject(value);if(typeof c.completed!=='boolean'||typeof c.text!=='string'||c.text.length>2048)throw new WorkspaceError('invalid_cron_task',502);return {id:uuid(c.id??c._id),text:c.text,completed:c.completed};});
  return {id,type:String(t.type),text:t.text,completed:t.completed,isDue:t.type==='daily'?t.isDue:null,checklist,...Object.fromEntries(['priority','value','attribute','frequency','everyX','startDate','repeat','daysOfMonth','weeksOfMonth','streak','counterUp','counterDown','up','down','group','challenge'].map(key=>[key,t[key]??null]))};
 }).sort((a,b)=>a.id.localeCompare(b.id));
 if(tasks.length>200)throw new WorkspaceError('cron_task_capacity',413);
 const membership=workspaceObject(user.party??{});let party:Record<string,unknown>|null=null;let quest:Record<string,unknown>|null=null;let privateQuest:Record<string,unknown>|null=null;
 if(membership._id!==undefined&&membership._id!==null&&membership._id!==''){
  const id=uuid(membership._id);const group=workspaceObject((await upstream(env,who.profileId,who.connectionId,'groups/party')).data);
  if(uuid(group._id??group.id)!==id||group.type!=='party')throw new WorkspaceError('party_changed',409);
  const q=workspaceObject(group.quest??{});
  if(q.key!==undefined&&q.key!==null&&(typeof q.key!=='string'||q.key.length>128)||q.active!==undefined&&typeof q.active!=='boolean')throw new WorkspaceError('invalid_cron_quest',502);
  const progress=workspaceObject(q.progress??{});const numeric=(value:unknown)=>{if(value===undefined||value===null)return null;if(typeof value!=='number'||!Number.isFinite(value))throw new WorkspaceError('invalid_cron_quest',502);return value;};
  const rawCollect=workspaceObject(progress.collect??{});if(Object.keys(rawCollect).length>128)throw new WorkspaceError('invalid_cron_quest',502);
  const collect=Object.fromEntries(Object.entries(rawCollect).map(([key,value])=>{if(!/^[A-Za-z0-9_-]{1,128}$/.test(key)||!Number.isSafeInteger(value)||Number(value)<0)throw new WorkspaceError('invalid_cron_quest',502);return [key,value];}));
  privateQuest=q;quest={key:q.key??null,active:q.active??null,progress:{hp:numeric(progress.hp),rage:numeric(progress.rage),collect}};party={id,name:typeof group.name==='string'?group.name.slice(0,256):'',quest};
 }
 const due=tasks.filter(t=>t.type==='daily'&&t.isDue===true&&!t.completed);
 const critical={actorId:credential.habiticaUserId,day:day.day,dayStartUtc:day.dayStartUtc,lastCron,needsCron,preferences:{dayStart:prefs.dayStart,timezoneOffset:prefs.timezoneOffset,timezoneOffsetAtLastCron:lastOffset??null,sleep:prefs.sleep},stats:Object.fromEntries(['class','lvl','hp','mp','exp','gp','str','int','con','per','points','buffs'].map(key=>[key,stats[key]??null])),equipped,ownQuest:membership.quest??null,party,privateQuest,tasks};
 let preconditionHash:string;try{preconditionHash=await gameplayStateHash(critical);}catch{throw new WorkspaceError('invalid_cron_snapshot',502);}
 await activeProfile(env,who.profileId,who.connectionId);
 const freshDay=habiticaDay(Date.now(),prefs.dayStart,prefs.timezoneOffset);if(freshDay.day!==day.day)throw new WorkspaceError('cron_day_changed',409);
 return {credential,preconditionHash,context:{...day,lastCron,needsCron,canPrepare:reason===null&&due.length<=100,unavailableReason:reason??(due.length>100?'cron_review_capacity':null),resting:prefs.sleep,party,taskCount:tasks.length,unfinishedDueDailyCount:due.length,unfinishedDailies:due.slice(0,100).map(t=>({taskId:t.id,text:t.text,completedChecklistCount:t.checklist.filter(c=>c.completed).length,totalChecklistCount:t.checklist.length})),truncated:due.length>100,risks:{personalDamage:'unknown',questEffects:'unknown',rewards:'unknown'},warnings:['Review yesterday unfinished Dailies before approving. Missed Dailies can damage you and the party.','Cron resets task/checklist state, changes temporary buffs and processes accumulated quest progress.','Habit counters and task values can change; old completed Todos may be removed. No exact damage or reward prediction is supplied.','A timeout or failed response can follow partial effects. Never retry this accepted action or infer full success from day progression.'],retrievedAtUtc:new Date().toISOString()}};
}
