import type {SavedTaskQuery} from './saved-task-views.ts';
import type {CompanionTask} from './workspace-domain.ts';
import {recurrenceLocalDate} from './recurrence-calendar.ts';
import {WorkspaceError} from './workspace-http.ts';
export const relativeDatePresets=['today','beforeToday','nextSevenDays','thisWeek','noDate'] as const;
export type RelativeDatePreset=typeof relativeDatePresets[number];
export interface RelativeDateBounds {preset:RelativeDatePreset;from:string|null;through:string|null}
export interface RelativeDateEvaluation {asOfUtc:string;timeZoneId:string;localDate:string;planned:RelativeDateBounds|null;deadline:RelativeDateBounds|null}
export const hasRelativeDates=(query:SavedTaskQuery)=>query.filter.plannedRelative!==undefined||query.filter.deadlineRelative!==undefined;
export const isRelativeTimeZone=(zone:unknown):zone is string=>typeof zone==='string'&&zone.length>0&&zone.length<=128&&!/^\p{White_Space}*$/u.test(zone)&&(zone==='UTC'||zone.includes('/'));
const fail=(code:string):never=>{throw new WorkspaceError(code,422);};
function add(day:string,count:number):string{const stamp=new Date(day+'T00:00:00Z');stamp.setUTCDate(stamp.getUTCDate()+count);const result=stamp.toISOString().slice(0,10);if(!/^\d{4}-\d{2}-\d{2}$/.test(result)||result.startsWith('0000')||result==='0001-01-01')return fail('relative_bounds_unavailable');return result;}
export function relativeDateBounds(preset:RelativeDatePreset|undefined,day:string):RelativeDateBounds|null{
 if(preset===undefined)return null;let from:string|null=null,through:string|null=null;
 if(preset==='today')from=through=add(day,0);
 else if(preset==='beforeToday')through=add(day,-1);
 else if(preset==='nextSevenDays'){from=add(day,0);through=add(day,6);}
 else if(preset==='thisWeek'){from=add(day,-((new Date(day+'T00:00:00Z').getUTCDay()+6)%7));through=add(from,6);}
 else if(preset!=='noDate')return fail('invalid_saved_task_view');
 return {preset,from,through};
}
export function evaluateRelativeDates(query:SavedTaskQuery,asOfUtc:string,expectedEvaluationDay?:string):RelativeDateEvaluation|null{
 if(!hasRelativeDates(query)){if(expectedEvaluationDay!==undefined)return fail('invalid_evaluation_context');return null;}
 if(/^0001-01-01T00:00:00(?:\.0+)?Z$/.test(asOfUtc))return fail('invalid_evaluation_clock');
 const local=recurrenceLocalDate(asOfUtc,query.timeZoneId!);if(!local.date)return fail(local.failure==='invalid_time_zone'?'relative_timezone_unavailable':'invalid_evaluation_clock');
 if(expectedEvaluationDay!==undefined&&expectedEvaluationDay!==local.date)throw new WorkspaceError('stale_evaluation_day',409);
 return {asOfUtc,timeZoneId:query.timeZoneId!,localDate:local.date,planned:relativeDateBounds(query.filter.plannedRelative,local.date),deadline:relativeDateBounds(query.filter.deadlineRelative,local.date)};
}
export const matchesRelativeMissingDates=(task:CompanionTask,evaluation:RelativeDateEvaluation|null)=>(evaluation?.planned?.preset!=='noDate'||task.plannedDate===null)&&(evaluation?.deadline?.preset!=='noDate'||task.deadline===null);
