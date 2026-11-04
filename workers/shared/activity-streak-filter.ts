import {workspaceObject,uuid,WorkspaceError} from './workspace-http.ts';
import {utcTicks} from './workspace-domain.ts';
import {recurrenceLocalDate} from './recurrence-calendar.ts';
export interface ActivityStreakFilter {taskId:string;fromDay:string;asOfUtc:string;timeZoneId:string;limit:number;offset:number;expectedRevision?:number;expectedContext?:string}
export const activityStreakKeys=['taskId','fromDay','asOfUtc','timeZoneId','limit','offset','expectedRevision','expectedContext'];
export function activityStreakFilter(input:unknown):ActivityStreakFilter{
 const v=workspaceObject(input,activityStreakKeys),fail=():never=>{throw new WorkspaceError('invalid_activity_filter',422);};
 const taskId=uuid(v.taskId);try{utcTicks(v.asOfUtc);}catch{return fail();}
 const fromDay=v.fromDay,timeZoneId=v.timeZoneId;if(typeof fromDay!=='string'||!/^\d{4}-\d{2}-\d{2}$/.test(fromDay)||fromDay<'0002-01-01'||fromDay>'9998-12-31'||(!Number.isFinite(Date.parse(fromDay+'T00:00:00Z'))||new Date(fromDay+'T00:00:00Z').toISOString().slice(0,10)!==fromDay)||typeof timeZoneId!=='string')return fail();
 const raw=v.asOfUtc as string,asOfUtc=raw.slice(0,19)+'.'+(raw.match(/\.(\d+)/)?.[1]??'').padEnd(7,'0')+'Z';const local=recurrenceLocalDate(asOfUtc,timeZoneId).date;
 if(!local||local<'0002-01-01'||local>'9998-12-31'||local<fromDay||(Date.parse(local+'T00:00:00Z')-Date.parse(fromDay+'T00:00:00Z'))/86400000>=366)return fail();
 const limit=v.limit??20,offset=v.offset??0;if(!Number.isSafeInteger(limit)||Number(limit)<1||Number(limit)>100||!Number.isSafeInteger(offset)||Number(offset)<0||Number(offset)>366||Number(offset)>0&&v.expectedRevision===undefined)return fail();
 if(v.expectedRevision!==undefined&&(!Number.isSafeInteger(v.expectedRevision)||Number(v.expectedRevision)<1)||v.expectedContext!==undefined&&(typeof v.expectedContext!=='string'||!(/^[0-9a-f]{64}$/).test(v.expectedContext)))return fail();
 return {taskId,fromDay,asOfUtc,timeZoneId,limit:Number(limit),offset:Number(offset),...(v.expectedRevision===undefined?{}:{expectedRevision:Number(v.expectedRevision)}),...(v.expectedContext===undefined?{}:{expectedContext:v.expectedContext as string})};
}
