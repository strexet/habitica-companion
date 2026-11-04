import {normalizeDailySchedule,readDailySchedule,type DailyAccount,type DailySchedule,dailyRuleFailure,sameDailySchedule} from './daily-projection.ts';
import {workspaceObject,WorkspaceError,uuid} from './workspace-http.ts';
import {utcTicks,type Recurrence} from './workspace-domain.ts';
const names=['sunday','monday','tuesday','wednesday','thursday','friday','saturday'];
const keys=['su','m','t','w','th','f','s'];
export interface LinkedDailyScheduleContext {habiticaTaskId:string;fingerprint:string;observedAtUtc:string;account:DailyAccount;before:DailySchedule;expectedAfter:DailySchedule}
export function reviewLinkedDailySchedule(before:DailySchedule,proposed:Recurrence,account:DailyAccount):{beforeNative:Recurrence;expectedAfter:DailySchedule}{
 before=readDailySchedule(before);if(Number(before.startDate.slice(0,4))<2||Number(before.startDate.slice(0,4))>9998)throw new WorkspaceError('invalid_daily_schedule',409);
 const desired=normalizeDailySchedule(proposed,account);if(Number(desired.startDate.slice(0,4))<2||Number(desired.startDate.slice(0,4))>9998)throw new WorkspaceError('daily_start_unrepresentable',409);
 if(before.frequency==='yearly'||before.everyX<1||before.everyX>366||before.weeksOfMonth.length||before.frequency!=='monthly'&&before.daysOfMonth.length)throw new WorkspaceError('unsupported_retained_daily_schedule',409);
 const local=new Date(Date.parse(before.startDate)-account.timezoneOffset*60000);
 if(local.toISOString().slice(11)!=='00:00:00.000Z')throw new WorkspaceError('daily_start_unrepresentable',409);
 if(before.frequency==='monthly'&&(before.daysOfMonth.length!==1||before.daysOfMonth[0]!==local.getUTCDate())||proposed.frequency==='monthly'&&(proposed.daysOfMonth.length!==1||proposed.daysOfMonth[0]!==Number(proposed.startDate.slice(8))))throw new WorkspaceError('daily_month_update_unrepresentable',409);
 const previous:Recurrence={frequency:before.frequency,interval:before.everyX,startDate:local.toISOString().slice(0,10),timeZoneId:proposed.timeZoneId,weekdays:before.frequency==='weekly'?names.filter((_,i)=>before.repeat[keys[i]]):[],daysOfMonth:before.frequency==='monthly'?[...before.daysOfMonth]:[]};
 const failure=dailyRuleFailure(previous);if(failure)throw new WorkspaceError(failure,409);
 return {beforeNative:previous,expectedAfter:{...desired,repeat:proposed.frequency==='weekly'?desired.repeat:{...before.repeat}}};
}
function contextSchedule(input:unknown):DailySchedule {const row=workspaceObject(input);if(utcTicks(row.startDate)%10000n!==0n||Number(String(row.startDate).slice(0,4))<2||Number(String(row.startDate).slice(0,4))>9998)throw new WorkspaceError('invalid_schedule_context',422);return readDailySchedule({...row,startDate:new Date(String(row.startDate)).toISOString()});}
export function linkedDailyScheduleContext(input:unknown):LinkedDailyScheduleContext|null{
 if(input===undefined||input===null)return null;
 const value=workspaceObject(input,['habiticaTaskId','fingerprint','observedAtUtc','account','before','expectedAfter']);
 if(typeof value.fingerprint!=='string'||!/^[a-f0-9]{64}$/.test(value.fingerprint)||typeof value.observedAtUtc!=='string'||utcTicks(value.observedAtUtc)===utcTicks('0001-01-01T00:00:00Z'))throw new WorkspaceError('invalid_schedule_context',422);
 const account=workspaceObject(value.account,['timezoneOffset','dayStart']);
 if(!Number.isInteger(account.timezoneOffset)||Number(account.timezoneOffset)< -840||Number(account.timezoneOffset)>720||account.dayStart!==0)throw new WorkspaceError('daily_account_day_unrepresentable',409);
 return {habiticaTaskId:uuid(value.habiticaTaskId),fingerprint:value.fingerprint,observedAtUtc:value.observedAtUtc,account:{timezoneOffset:Number(account.timezoneOffset),dayStart:0},before:contextSchedule(value.before),expectedAfter:contextSchedule(value.expectedAfter)};
}
export function scheduleContextMatches(context:LinkedDailyScheduleContext,rule:Recurrence):boolean{
 try{return sameDailySchedule(reviewLinkedDailySchedule(context.before,rule,context.account).expectedAfter,context.expectedAfter);}catch{return false;}
}
// Field sequence is shared with the C# command hash; default and unsupported raw weekdays are retained.
export function scheduleContextFields(value:LinkedDailyScheduleContext):string[]{
 const schedule=(row:DailySchedule)=>[row.frequency,String(row.everyX),utcTicks(row.startDate).toString(),...keys.map(key=>row.repeat[key]?'1':'0'),String(row.daysOfMonth.length),...row.daysOfMonth.map(String),String(row.weeksOfMonth.length),...row.weeksOfMonth.map(String)];
 return [value.habiticaTaskId,value.fingerprint,utcTicks(value.observedAtUtc).toString(),String(value.account.timezoneOffset),String(value.account.dayStart),...schedule(value.before),...schedule(value.expectedAfter)];
}

export interface LinkedDailyScheduleResult {observedAtUtc:string;actual:DailySchedule;matchesExpected:boolean;attemptId:string}
export function linkedDailyScheduleResult(input:unknown,context:LinkedDailyScheduleContext):LinkedDailyScheduleResult{
 const row=workspaceObject(input,['observedAtUtc','actual','matchesExpected','attemptId']);if(typeof row.observedAtUtc!=='string'||utcTicks(row.observedAtUtc)===utcTicks('0001-01-01T00:00:00Z')||typeof row.matchesExpected!=='boolean')throw new WorkspaceError('invalid_schedule_result',422);
 const actual=contextSchedule(row.actual);if(row.matchesExpected!==sameDailySchedule(actual,context.expectedAfter))throw new WorkspaceError('invalid_schedule_result',422);
 return {observedAtUtc:row.observedAtUtc,actual,matchesExpected:row.matchesExpected,attemptId:uuid(row.attemptId)};
}
