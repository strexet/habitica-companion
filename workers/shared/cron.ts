import {WorkspaceError} from './workspace-http.ts';
export const cronReadScopes=['profile:read','account:read','tasks:read','party:read'] as const;
export const cronScopes={status:cronReadScopes,execute:[...cronReadScopes,'habitica.cron.execute'],lookup:cronReadScopes,progress:cronReadScopes} as const;
export function habiticaDay(at:number,dayStart:unknown,offset:unknown){
 if(!Number.isFinite(at)||!Number.isInteger(dayStart)||Number(dayStart)<0||Number(dayStart)>23||!Number.isInteger(offset)||Math.abs(Number(offset))>840)throw new WorkspaceError('cron_precondition_unknown',409);
 const local=at-Number(offset)*60000-Number(dayStart)*3600000;
 const day=new Date(local).toISOString().slice(0,10);const start=Date.parse(day+'T00:00:00Z')+Number(offset)*60000+Number(dayStart)*3600000;
 return {day,dayStartUtc:new Date(start).toISOString(),reviewDateUtc:new Date(start-1).toISOString()};
}
