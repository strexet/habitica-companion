import {activityStreakFilter} from '../../shared/activity-streak-filter.ts';
import {utcTicks} from '../../shared/workspace-domain.ts';
import {expandRecurrence} from '../../shared/recurrence-calendar.ts';
import {workspaceTokenHash,WorkspaceError} from '../../shared/workspace-http.ts';
import {journal} from './journal.ts';
import {activityIntervals} from './activity-coverage-query.ts';
interface Day {day:string;status:string;positiveEvents:number;negativeEvents:number;reason:string|null}
const dayMs=86400000,success=(d:Day)=>d.status==='completed'||d.status==='positive';
export async function readActivityStatistics(snapshot:unknown,input:unknown){
 const f=activityStreakFilter(input),state=await journal(snapshot),doc=state.document;
 if(f.expectedRevision!==undefined&&f.expectedRevision!==doc.revision)throw new WorkspaceError('stale_revision',409);
 const task=doc.tasks.find(t=>t.id===f.taskId);if(!task||task.kind==='todo')throw new WorkspaceError('activity_task_unavailable',422);
 const context=await workspaceTokenHash([doc.id,String(doc.revision),task.id,f.fromDay,f.asOfUtc,f.timeZoneId].join('\n'));
 if(f.expectedContext!==undefined&&f.expectedContext!==context||f.offset>0&&f.expectedContext!==context)throw new WorkspaceError('stale_activity_context',409);
 const format=new Intl.DateTimeFormat('en-US-u-ca-gregory-nu-latn',{timeZone:f.timeZoneId,year:'numeric',month:'2-digit',day:'2-digit',era:'short'});
 const civil=(at:number)=>{if(f.timeZoneId==='UTC')return new Date(at).toISOString().slice(0,10);const p=format.formatToParts(new Date(at)),get=(name:string)=>p.find(v=>v.type===name)?.value??'';return get('year').padStart(4,'0')+'-'+get('month')+'-'+get('day');};
 const boundaries=new Map<string,number>();
 const startDay=(day:string)=>{const cached=boundaries.get(day);if(cached!==undefined)return cached;const midnight=Date.parse(day+'T00:00:00Z');let low=midnight-36*3600000,high=midnight+36*3600000;if(f.timeZoneId==='UTC')low=high=midnight;while(low<high){const mid=low+Math.floor((high-low)/2);if(civil(mid)<day)low=mid+1;else high=mid;}boundaries.set(day,low);return low;};
 const iso=(at:number)=>new Date(at).toISOString(),asOf=utcTicks(f.asOfUtc),throughDay=civil(Date.parse(f.asOfUtc)),start=startDay(f.fromDay);
 const intervals=BigInt(start)*10000n<asOf?activityIntervals(doc,task.id,iso(start),f.asOfUtc):[];
 const creation=(doc.activityCoverage??[]).find(w=>w.taskId===task.id&&w.previousTaskRevision===0&&utcTicks(w.recordedAtUtc)<=asOf),born=creation?civil(Date.parse(creation.recordedAtUtc)):null;
 const occurrences=new Map(doc.occurrences.filter(o=>o.taskId===task.id).map(o=>[o.id,o]));
 for(const correction of [...(doc.occurrenceCorrections??[])].filter(c=>c.taskId===task.id&&utcTicks(c.correctedAtUtc)>asOf).sort((a,b)=>b.revision-a.revision))occurrences.set(correction.previousOccurrence.id,correction.previousOccurrence);
 const commands=new Map(state.commands.map(c=>[c.command.id,c])),terminalDays=new Map<string,typeof doc.occurrences>();
 for(const o of occurrences.values())if(o.state!=='pending'&&o.terminalCommandId&&commands.has(o.terminalCommandId)&&utcTicks(commands.get(o.terminalCommandId)!.acceptedAtUtc)<=asOf){const rows=terminalDays.get(o.date)??[];rows.push(o);terminalDays.set(o.date,rows);}
 const scores=new Map<string,{up:number;down:number}>();for(const score of doc.habitScores)if(score.taskId===task.id&&utcTicks(score.recordedAtUtc)>=BigInt(start)*10000n&&utcTicks(score.recordedAtUtc)<=asOf){const day=civil(Date.parse(score.recordedAtUtc)),row=scores.get(day)??{up:0,down:0};row[score.direction]++;scores.set(day,row);}
 const observedGoal=[...state.commands].filter(c=>c.command.taskId===task.id&&utcTicks(c.acceptedAtUtc)<=asOf&&c.taskSnapshot).sort((a,b)=>b.command.expectedRevision-a.command.expectedRevision)[0]?.taskSnapshot;const taskAtAsOf=observedGoal??task;
 const frequency=task.kind==='habit'&&taskAtAsOf.habiticaDetails?.up===false;let intervalIndex=0,openUnproven=false;const days:Day[]=[];
 for(let stamp=Date.parse(f.fromDay+'T00:00:00Z');stamp<=Date.parse(throughDay+'T00:00:00Z');stamp+=dayMs){
  const day=iso(stamp).slice(0,10),from=startDay(day),until=startDay(iso(stamp+dayMs).slice(0,10)),end=BigInt(until)*10000n<asOf?BigInt(until)*10000n:asOf;
  let positive=task.kind==='habit'?(scores.get(day)?.up??0):0;const negative=task.kind==='habit'?(scores.get(day)?.down??0):0;
  const terminal=(terminalDays.get(day)??[]).sort((a,b)=>b.revision-a.revision)[0];
  if(task.kind==='daily'&&terminal?.terminalCommandId&&commands.get(terminal.terminalCommandId)?.taskSnapshot?.recurrence?.timeZoneId!==f.timeZoneId){days.push({day,status:'unknown',positiveEvents:0,negativeEvents:0,reason:'terminal_timezone_unavailable'});continue;}
  if(task.kind==='daily'&&terminal?.state==='completed')positive=1;
  if(positive>0||frequency&&negative>0){days.push({day,status:frequency?'activity':task.kind==='daily'?'completed':'positive',positiveEvents:positive,negativeEvents:negative,reason:null});continue;}
  if(task.kind==='daily'&&terminal?.state==='skipped'){days.push({day,status:'neutral',positiveEvents:0,negativeEvents:0,reason:'explicit_skip'});continue;}
  if(from===until||born!==null&&day<born){days.push({day,status:'neutral',positiveEvents:0,negativeEvents:negative,reason:'no_eligible_day'});continue;}
  const observedFrom=creation&&utcTicks(creation.recordedAtUtc)>BigInt(from)*10000n&&utcTicks(creation.recordedAtUtc)<end?utcTicks(creation.recordedAtUtc):BigInt(from)*10000n;
  while(intervalIndex<intervals.length&&utcTicks(intervals[intervalIndex].throughUtc)<=observedFrom)intervalIndex++;
  let unknown:string|null=null,disabledGoal=false,covered=observedFrom;const modes=new Set<boolean>();
  for(let i=intervalIndex;i<intervals.length&&utcTicks(intervals[i].fromUtc)<end;i++){
   const slice=intervals[i];if(utcTicks(slice.fromUtc)>covered)unknown='unproven_history';if(utcTicks(slice.throughUtc)>covered)covered=utcTicks(slice.throughUtc);
   if(!slice.isKnown||!slice.schedule){unknown=slice.unknownReason??'unproven_history';continue;}const schedule=slice.schedule;
   if(schedule.kind!==task.kind){unknown='historical_kind_unavailable';continue;}if(schedule.isArchived||schedule.state!=='active'){modes.add(false);continue;}
   if(task.kind==='habit'){const enabled=commands.get(schedule.commandId)?.taskSnapshot?.habiticaDetails?.up!==false;modes.add(enabled);disabledGoal||=!enabled;continue;}if(!schedule.recurrence){unknown='schedule_unavailable';continue;}if(schedule.recurrence.timeZoneId!==f.timeZoneId){unknown='historical_timezone_differs';continue;}
   const expansion=expandRecurrence(schedule.recurrence,day,day);if(expansion.failure){unknown=expansion.failure;continue;}modes.add(expansion.dates.length>0);
  }
  if(covered<end)unknown='unproven_history';if(modes.size>1)unknown='mixed_day_context';
  if(day===throughDay){openUnproven=unknown!==null||modes.size===0;days.push({day,status:'pending',positiveEvents:0,negativeEvents:negative,reason:'current_day_open'});continue;}
  days.push({day,status:unknown!==null||modes.size===0?'unknown':modes.has(true)?frequency?'noObservedActivity':'missed':'neutral',positiveEvents:0,negativeEvents:negative,reason:unknown??(modes.size===0?'unproven_history':modes.has(true)?null:disabledGoal?'positive_goal_disabled':'not_scheduled_or_paused')});
 }
 let run=0,knownBreak=false,lower=false;for(let i=days.length-1;i>=0;i--){const row=days[i];if(success(row))run++;else if(row.status==='missed'){knownBreak=true;break;}else if(row.status==='unknown'){lower=true;break;}}
 if(!knownBreak&&!lower&&(born===null||born<f.fromDay))lower=true;const clockUnknown=intervals.some(i=>i.unknownReason==='clock_order_unknown');
 const currentStreak=frequency||clockUnknown||lower&&run===0?null:run,currentQuality=frequency?'notApplicable':currentStreak===null?'unavailable':lower?'lowerBound':'verified';let longestObserved=0;run=0;
 for(const row of days){if(success(row)){run++;longestObserved=Math.max(longestObserved,run);}else if(['missed','unknown'].includes(row.status))run=0;}
 const unknownDays=days.filter(d=>d.status==='unknown').length,items=days.slice(f.offset,f.offset+f.limit),next=f.offset+items.length;
 return {workspaceId:doc.id,revision:doc.revision,taskId:task.id,kind:task.kind,mode:frequency?'frequency':'streak',modeSource:observedGoal?'commandSnapshot':'currentTask',fromDay:f.fromDay,throughDay,asOfUtc:f.asOfUtc,timeZoneId:f.timeZoneId,context,currentStreak,currentQuality,longestObserved,longestQuality:frequency?'notApplicable':unknownDays===0&&!openUnproven&&!clockUnknown?'verifiedInWindow':'lowerBound',positiveDays:days.filter(d=>d.positiveEvents>0).length,negativeDays:days.filter(d=>d.negativeEvents>0).length,activityDays:days.filter(d=>d.positiveEvents+d.negativeEvents>0).length,positiveEvents:days.reduce((n,d)=>n+d.positiveEvents,0),negativeEvents:days.reduce((n,d)=>n+d.negativeEvents,0),unknownDays,items,total:days.length,nextOffset:next<days.length?next:null,lifetimeBest:null};
}
