import type {Recurrence} from './workspace-domain.ts';
import {expandRecurrence} from './recurrence-calendar.ts';
import {WorkspaceError,workspaceObject} from './workspace-http.ts';
export interface DailyAccount {timezoneOffset:number;dayStart:number}
export interface DailySchedule {frequency:'daily'|'weekly'|'monthly'|'yearly';everyX:number;startDate:string;repeat:Record<string,boolean>;daysOfMonth:number[];weeksOfMonth:number[]}
const names=['sunday','monday','tuesday','wednesday','thursday','friday','saturday'];
const keys=['su','m','t','w','th','f','s'];
// Only explicitly fixed zones can promise indefinite parity with Habitica's fixed offset.
export function dailyRuleFailure(rule:Recurrence|null|undefined):string|null{
 if(!rule)return 'daily_schedule_required';
 if(expandRecurrence(rule,rule.startDate,rule.startDate).failure)return 'invalid_recurrence';
 if(rule.timeZoneId!=='UTC'&&!/^Etc\/GMT(?:[+-](?:[1-9]|1[0-4]))?$/.test(rule.timeZoneId))return 'daily_timezone_unrepresentable';
 if(rule.frequency==='weekly'&&rule.interval>1&&new Date(rule.startDate+'T00:00:00Z').getUTCDay()!==1)return 'daily_week_anchor_unrepresentable';
 return null;
}
export function normalizeDailySchedule(rule:Recurrence,account:DailyAccount):DailySchedule{
 const failure=dailyRuleFailure(rule);if(failure)throw new WorkspaceError(failure,422);
 if(!Number.isInteger(account.timezoneOffset)||account.timezoneOffset< -840||account.timezoneOffset>720||account.dayStart!==0)throw new WorkspaceError('daily_account_day_unrepresentable',409);
 const match=/^Etc\/GMT([+-])(\d+)$/.exec(rule.timeZoneId);
 const offset=match?(match[1]==='+'?1:-1)*Number(match[2])*60:0;
 if(offset!==account.timezoneOffset)throw new WorkspaceError('daily_timezone_mismatch',409);
 const startDate=new Date(Date.parse(rule.startDate+'T00:00:00Z')+offset*60000).toISOString();
 return {frequency:rule.frequency,everyX:rule.interval,startDate,repeat:Object.fromEntries(keys.map((key,i)=>[key,rule.frequency==='weekly'&&rule.weekdays.includes(names[i])])),daysOfMonth:[...rule.daysOfMonth].sort((a,b)=>a-b),weeksOfMonth:[]};
}
// Normalize the complete upstream schedule for fingerprints, including unsupported retained rules.
export function readDailySchedule(input:unknown):DailySchedule{
 const row=workspaceObject(input);
 if(!['daily','weekly','monthly','yearly'].includes(String(row.frequency))||!Number.isInteger(row.everyX)||Number(row.everyX)<0||Number(row.everyX)>9999||typeof row.startDate!=='string'||!/^\d{4}-\d{2}-\d{2}T(?:[01]\d|2[0-3]):[0-5]\d:[0-5]\d(?:\.\d{3})?Z$/.test(row.startDate)||row.startDate.startsWith('0000')||!Number.isFinite(Date.parse(row.startDate))||new Date(row.startDate).toISOString().slice(0,10)!==row.startDate.slice(0,10))throw new WorkspaceError('invalid_daily_schedule',502);
 const repeat=workspaceObject(row.repeat);if(keys.some(key=>typeof repeat[key]!=='boolean')||Object.keys(repeat).some(key=>!keys.includes(key)))throw new WorkspaceError('invalid_daily_schedule',502);
 const numbers=(value:unknown,max:number)=>{if(!Array.isArray(value)||value.some(v=>!Number.isInteger(v)||v<1||v>max)||new Set(value).size!==value.length)throw new WorkspaceError('invalid_daily_schedule',502);return [...value as number[]].sort((a,b)=>a-b);};
 return {frequency:row.frequency as DailySchedule['frequency'],everyX:Number(row.everyX),startDate:new Date(row.startDate).toISOString(),repeat:Object.fromEntries(keys.map(key=>[key,repeat[key] as boolean])),daysOfMonth:numbers(row.daysOfMonth,31),weeksOfMonth:numbers(row.weeksOfMonth,5)};
}
export function sameDailySchedule(a:DailySchedule,b:DailySchedule):boolean{return JSON.stringify(a)===JSON.stringify(b);}
