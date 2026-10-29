import type {ActivityInterval} from '../../shared/activity-coverage.ts';
import {utcTicks,type WorkspaceDocument} from '../../shared/workspace-domain.ts';
import {WorkspaceError} from '../../shared/workspace-http.ts';
import {journal} from './journal.ts';
export async function readActivityCoverage(snapshot:unknown,taskId:string,fromUtc:string,throughUtc:string) {
 const valid=await journal(snapshot);return activityIntervals(valid.document,taskId,fromUtc,throughUtc);
}

const fail=():never=>{throw new WorkspaceError('invalid_activity_coverage',422);};
function activityIntervals(document:WorkspaceDocument,taskId:string,fromUtc:string,throughUtc:string):ActivityInterval[] {
 const start=utcTicks(fromUtc),end=utcTicks(throughUtc);if(start===utcTicks('0001-01-01T00:00:00Z')||end<=start||!document.tasks.some(task=>task.id===taskId))return fail();
 const rows=(document.activityCoverage??[]).filter(row=>row.taskId===taskId).sort((a,b)=>a.revision-b.revision);
 if(rows.some((row,index)=>index>0&&utcTicks(rows[index-1].recordedAtUtc)>utcTicks(row.recordedAtUtc)))return [{fromUtc,throughUtc,isKnown:false,unknownReason:'clock_order_unknown',schedule:null}];
 const result:ActivityInterval[]=[];let cursor=fromUtc;
 for(let index=0;index+1<rows.length;index++){
  const row=rows[index],next=rows[index+1];if(next.previousTaskRevision!==row.revision||utcTicks(next.recordedAtUtc)<=utcTicks(cursor)||utcTicks(row.recordedAtUtc)>=end)continue;
  const from=utcTicks(row.recordedAtUtc)>utcTicks(cursor)?row.recordedAtUtc:cursor,through=utcTicks(next.recordedAtUtc)<end?next.recordedAtUtc:throughUtc;
  if(utcTicks(from)>utcTicks(cursor))result.push({fromUtc:cursor,throughUtc:from,isKnown:false,unknownReason:'unproven_history',schedule:null});
  if(utcTicks(through)>utcTicks(from)){result.push({fromUtc:from,throughUtc:through,isKnown:true,unknownReason:null,schedule:row});cursor=through;}
 }
 if(utcTicks(cursor)<end)result.push({fromUtc:cursor,throughUtc,isKnown:false,unknownReason:'unproven_history',schedule:null});return result;
}
