import {workspaceObject,uuid,WorkspaceError} from './workspace-http.ts';
import {expandRecurrence,recurrenceLocalDate} from './recurrence-calendar.ts';
import {reviewInstant,reviewZone} from './task-review.ts';
import {workspaceRecurrence,type Recurrence} from './workspace-domain.ts';
export interface ReminderRepeat {rule:Recurrence;time:string;through:string;gap:'skip';overlap:'earlier'|'later'|'skip'}
export interface ReminderTarget {deviceId:string;revision:number;subscriptionId:string}
export interface ReminderIntent {operation:'set'|'cancel';scheduleId:string|null;expectedRevision:number;taskId:string;reviewAfterUtc:string|null;timeZoneId:string;repeat:ReminderRepeat|null;targets:ReminderTarget[];consentVersion:'scheduled-reminders-v1'}
const dayMs=86400000;
function reviewUtc(value:unknown):string {const result=reviewInstant(value);if(result===null)throw new WorkspaceError('invalid_reminder_instant',422);return result;}
function day(value:unknown):string {if(typeof value!=='string'||!/^\d{4}-\d{2}-\d{2}$/.test(value)||value<'2020-01-01'||value>'2100-12-31'||!Number.isFinite(Date.parse(value+'T00:00:00Z'))||new Date(value+'T00:00:00Z').toISOString().slice(0,10)!==value)throw new WorkspaceError('invalid_reminder_date',422);return value;}
export function reminderIntent(value:unknown):ReminderIntent {
 const v=workspaceObject(value,['operation','scheduleId','expectedRevision','taskId','reviewAfterUtc','timeZoneId','repeat','targets','consentVersion']);
 if(!['set','cancel'].includes(String(v.operation))||!Number.isSafeInteger(v.expectedRevision)||Number(v.expectedRevision)<0||Number(v.expectedRevision)>=Number.MAX_SAFE_INTEGER||v.consentVersion!=='scheduled-reminders-v1')throw new WorkspaceError('invalid_reminder_intent',422);
 const scheduleId=v.scheduleId===null?null:uuid(v.scheduleId);if((scheduleId===null)!==(v.expectedRevision===0)||v.operation==='cancel'&&scheduleId===null)throw new WorkspaceError('invalid_reminder_revision',422);
 const taskId=uuid(v.taskId),timeZoneId=reviewZone(v.timeZoneId);
 const cue=v.reviewAfterUtc===null?null:reviewUtc(v.reviewAfterUtc);
 if(v.operation==='set'&&cue===null||!Array.isArray(v.targets)||v.targets.length>4||v.operation==='set'&&v.targets.length===0)throw new WorkspaceError('invalid_reminder_targets',422);
 const targets=v.targets.map(raw=>{const t=workspaceObject(raw,['deviceId','revision','subscriptionId']);if(!Number.isSafeInteger(t.revision)||Number(t.revision)<1||Number(t.revision)>Number.MAX_SAFE_INTEGER)throw new WorkspaceError('invalid_device_revision',422);return {deviceId:uuid(t.deviceId),revision:Number(t.revision),subscriptionId:uuid(t.subscriptionId)};});
 if(new Set(targets.map(t=>t.deviceId)).size!==targets.length)throw new WorkspaceError('duplicate_device',422);
 let repeat:ReminderRepeat|null=null;
 if(v.repeat!==null){
  const r=workspaceObject(v.repeat,['rule','time','through','gap','overlap']);
  const rule=workspaceRecurrence(r.rule);if(!rule)throw new WorkspaceError('invalid_reminder_repeat',422);
  const through=day(r.through),start=day(rule.startDate);
  if(typeof r.time!=='string'||! /^(?:[01]\d|2[0-3]):[0-5]\d$/.test(r.time)||r.gap!=='skip'||!['earlier','later','skip'].includes(String(r.overlap))||rule.timeZoneId!==timeZoneId||new Date(through).getTime()-new Date(start).getTime()>365*dayMs||!expandRecurrence(rule,start,through).dates.length)throw new WorkspaceError('invalid_reminder_repeat',422);
  repeat={rule,time:r.time,through,gap:'skip',overlap:r.overlap as ReminderRepeat['overlap']};
 }
 if(v.operation==='cancel'&&(repeat!==null||targets.length!==0||cue!==null))throw new WorkspaceError('invalid_reminder_cancel',422);
 return {operation:v.operation as 'set'|'cancel',scheduleId,expectedRevision:Number(v.expectedRevision),taskId,reviewAfterUtc:cue,timeZoneId,repeat,targets,consentVersion:'scheduled-reminders-v1'};
}
function civil(formatter:Intl.DateTimeFormat,stamp:number):string {
 const parts=formatter.formatToParts(stamp),get=(name:string)=>parts.find(p=>p.type===name)?.value??'';
 return get('year')+'-'+get('month')+'-'+get('day')+'T'+get('hour')+':'+get('minute')+':'+get('second');
}
export function reminderCivilInstant(date:string,time:string,zone:string,overlap:ReminderRepeat['overlap']):string|null {
 day(date);reviewZone(zone);if(!/^(?:[01]\d|2[0-3]):[0-5]\d$/.test(time)||!['earlier','later','skip'].includes(overlap))throw new WorkspaceError('invalid_reminder_time',422);
 const target=date+'T'+time+':00',nominal=Date.parse(target+'Z');
 const formatter=new Intl.DateTimeFormat('en-CA-u-ca-gregory-nu-latn',{timeZone:zone,year:'numeric',month:'2-digit',day:'2-digit',hour:'2-digit',minute:'2-digit',second:'2-digit',hourCycle:'h23'});
 const offsets=new Set([-36,-24,-12,0,12,24,36].map(h=>{const stamp=nominal+h*3600000;return Date.parse(civil(formatter,stamp)+'Z')-stamp;}));
 const matches=[...offsets].map(offset=>nominal-offset).filter(stamp=>civil(formatter,stamp)===target).sort((a,b)=>a-b);
 if(!matches.length||matches.length>1&&overlap==='skip')return null;
 return reviewUtc(new Date(overlap==='later'?matches.at(-1)!:matches[0]).toISOString());
}
export function nextReminder(intent:ReminderIntent,afterUtc:string):{atUtc:string;civilDate:string}|null {
 if(!intent.repeat)return intent.reviewAfterUtc!==null&&intent.reviewAfterUtc>reviewUtc(afterUtc)?{atUtc:intent.reviewAfterUtc,civilDate:recurrenceLocalDate(intent.reviewAfterUtc,intent.timeZoneId).date!}:null;
 const repeat=intent.repeat,expansion=expandRecurrence(repeat.rule,repeat.rule.startDate,repeat.through);
 if(expansion.failure)throw new WorkspaceError('reminder_calendar_unavailable',422);
 for(const date of expansion.dates){const at=reminderCivilInstant(date,repeat.time,intent.timeZoneId,repeat.overlap);if(at&&at>reviewUtc(afterUtc)&&at>=intent.reviewAfterUtc!)return {atUtc:at,civilDate:date};}
 return null;
}
