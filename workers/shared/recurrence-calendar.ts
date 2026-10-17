import type {Recurrence} from './workspace-domain.ts';
export interface RecurrenceExpansion {dates:string[];failure:string|null}
export interface RecurrenceLocalDate {date:string|null;failure:string|null}
const dayMs=86400000;
const weekdays=['sunday','monday','tuesday','wednesday','thursday','friday','saturday'];
function date(value:unknown):Date|null{
 if(typeof value!=='string'||!/^\d{4}-\d{2}-\d{2}$/.test(value)||value.startsWith('0000'))return null;
 const parsed=new Date(value+'T00:00:00Z');return Number.isFinite(parsed.getTime())&&parsed.toISOString().slice(0,10)===value?parsed:null;
}
function zone(id:unknown):string|null{
 if(typeof id!=='string'||id.length>128||id!=='UTC'&&!id.includes('/'))return null;
 try{new Intl.DateTimeFormat('en-US',{timeZone:id});return id;}catch{return null;}
}
export function expandRecurrence(rule:Recurrence|null,from:string,through:string):RecurrenceExpansion{
 const fail=(failure:string):RecurrenceExpansion=>({dates:[],failure});
 const first=date(from),last=date(through);
 if(!first||!last||last<first||(last.getTime()-first.getTime())/dayMs+1>366)return fail('invalid_range');
 const start=date(rule?.startDate);
 if(!rule||!start||rule.startDate==='0001-01-01'||!['daily','weekly','monthly'].includes(rule.frequency)||!Number.isInteger(rule.interval)||rule.interval<1||rule.interval>366
  ||typeof rule.timeZoneId!=='string'||rule.timeZoneId.length>128||/^\p{White_Space}*$/u.test(rule.timeZoneId)
  ||!Array.isArray(rule.weekdays)||!Array.isArray(rule.daysOfMonth)
  ||rule.weekdays.some(day=>!weekdays.includes(day))||new Set(rule.weekdays).size!==rule.weekdays.length
  ||rule.daysOfMonth.some(day=>!Number.isInteger(day)||day<1||day>31)||new Set(rule.daysOfMonth).size!==rule.daysOfMonth.length
  ||(rule.frequency==='weekly'?rule.weekdays.length===0:rule.weekdays.length!==0)
  ||(rule.frequency==='monthly'?rule.daysOfMonth.length===0:rule.daysOfMonth.length!==0))return fail('invalid_recurrence');
 if(!zone(rule.timeZoneId))return fail('invalid_time_zone');
 const result:string[]=[];const anchor=start.getTime()-((start.getUTCDay()+6)%7)*dayMs;
 for(let stamp=Math.max(first.getTime(),start.getTime());stamp<=last.getTime();stamp+=dayMs){
  const current=new Date(stamp);
  const due=rule.frequency==='daily'?(stamp-start.getTime())/dayMs%rule.interval===0
   :rule.frequency==='weekly'?Math.floor((stamp-anchor)/dayMs/7)%rule.interval===0&&rule.weekdays.includes(weekdays[current.getUTCDay()])
   :((current.getUTCFullYear()-start.getUTCFullYear())*12+current.getUTCMonth()-start.getUTCMonth())%rule.interval===0&&rule.daysOfMonth.includes(current.getUTCDate());
  if(due)result.push(current.toISOString().slice(0,10));
 }
 return {dates:result,failure:null};
}
export function recurrenceLocalDate(instantUtc:string,timeZoneId:string):RecurrenceLocalDate{
 const invalid=(failure:string):RecurrenceLocalDate=>({date:null,failure});
 if(typeof instantUtc!=='string'||!/^\d{4}-\d{2}-\d{2}T(?:[01]\d|2[0-3]):[0-5]\d:[0-5]\d(?:\.\d{1,7})?Z$/.test(instantUtc)||!date(instantUtc.slice(0,10)))return invalid('invalid_instant');
 if(!zone(timeZoneId))return invalid('invalid_time_zone');
 const stamp=new Date(instantUtc);if(!Number.isFinite(stamp.getTime()))return invalid('invalid_instant');
 const parts=new Intl.DateTimeFormat('en-US-u-ca-gregory-nu-latn',{timeZone:timeZoneId,year:'numeric',month:'2-digit',day:'2-digit',era:'short'}).formatToParts(stamp);
 const part=(type:string)=>parts.find(p=>p.type===type)?.value??'';
 const result=part('year').padStart(4,'0')+'-'+part('month')+'-'+part('day');
 return part('era')==='AD'&&date(result)?{date:result,failure:null}:invalid('invalid_instant');
}
