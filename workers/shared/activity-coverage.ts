import type {WorkspaceDocument,CompanionTask,Recurrence} from './workspace-domain.ts';
import {utcTicks} from './workspace-domain.ts';
import {workspaceObject,uuid,WorkspaceError} from './workspace-http.ts';
import type {CommandEntry,JournalEvent} from '../companion-workspace/src/commands.ts';

export interface ActivityWitness {
 commandId:string;payloadHash:string;taskId:string;revision:number;previousTaskRevision:number;recordedAtUtc:string;
 kind:CompanionTask['kind'];state:CompanionTask['state'];isArchived:boolean;recurrence:Recurrence|null;
}
export interface ActivityInterval {fromUtc:string;throughUtc:string;isKnown:boolean;unknownReason:string|null;schedule:ActivityWitness|null}
const fail=():never=>{throw new WorkspaceError('invalid_activity_coverage',422);};
export function activityCoverage(input:unknown,document:WorkspaceDocument,recurrence:(value:unknown)=>Recurrence|null,utc:(value:unknown)=>string):ActivityWitness[]|null {
 if(input==null)return null;if(!Array.isArray(input)||input.length>100000)return fail();
 const commands=new Set<string>(),revisions=new Set<number>(),tasks=new Set(document.tasks.map(task=>task.id));
 return input.map(value=>{
  const row=workspaceObject(value,['commandId','payloadHash','taskId','revision','previousTaskRevision','recordedAtUtc','kind','state','isArchived','recurrence']);
  const commandId=uuid(row.commandId),taskId=uuid(row.taskId),revision=Number(row.revision),previousTaskRevision=Number(row.previousTaskRevision);
  if(commands.has(commandId)||revisions.has(revision)||!tasks.has(taskId)||!Number.isSafeInteger(row.revision)||revision<2||revision>document.revision
   ||!Number.isSafeInteger(row.previousTaskRevision)||previousTaskRevision<0||previousTaskRevision>=revision
   ||typeof row.payloadHash!=='string'||!/^[a-f0-9]{64}$/.test(row.payloadHash)||!['todo','daily','habit'].includes(String(row.kind))
   ||!['active','paused','completed','waiting','someday','cancelled'].includes(String(row.state))||row.kind!=='todo'&&['completed','cancelled'].includes(String(row.state))
   ||typeof row.isArchived!=='boolean'||row.recurrence!=null&&row.kind!=='daily')return fail();
  commands.add(commandId);revisions.add(revision);
  return {commandId,payloadHash:row.payloadHash,taskId,revision,previousTaskRevision,recordedAtUtc:utc(row.recordedAtUtc),kind:row.kind as ActivityWitness['kind'],state:row.state as ActivityWitness['state'],isArchived:row.isArchived,recurrence:recurrence(row.recurrence)};
 });
}
export function activityWitness(entry:CommandEntry,previousTaskRevision:number):ActivityWitness {
 const task=entry.taskSnapshot;if(!task)return fail();
 return {commandId:entry.command.id,payloadHash:entry.payloadHash,taskId:task.id,revision:task.revision,previousTaskRevision,recordedAtUtc:entry.acceptedAtUtc,kind:task.kind,state:task.state,isArchived:task.isArchived,recurrence:structuredClone(task.recurrence)};
}
const sameRule=(a:Recurrence|null,b:Recurrence|null)=>a===null?b==null:b!=null&&a.frequency===b.frequency&&a.interval===b.interval&&a.startDate===b.startDate&&a.timeZoneId===b.timeZoneId&&JSON.stringify(a.weekdays)===JSON.stringify(b.weekdays)&&JSON.stringify(a.daysOfMonth)===JSON.stringify(b.daysOfMonth);
export function validateActivityCoverage(document:WorkspaceDocument,commands:CommandEntry[],events:JournalEvent[]):void {
 if(!document.activityCoverage?.length)return;
 const currentTasks=new Map(document.tasks.map(task=>[task.id,task]));
 const entries=new Map(commands.map(entry=>[entry.command.id,entry]));
 const witnessedRevisions=new Set((document.activityCoverage??[]).map(e=>e.taskId+':'+e.revision));
 const acceptedEvents=new Set(events.filter(e=>e.kind==='accepted').map(e=>e.commandId+':'+utcTicks(e.atUtc)));
 const sources=[...commands.map(e=>({taskId:e.command.taskId,revision:e.command.expectedRevision+1})),...(document.linkedTaskRecoveries??[]),...(document.sourceRetirements??[]).map(e=>({taskId:e.sourceTaskId,revision:e.revision})),...document.completions,...document.habitScores,...document.occurrences,...(document.restorations??[]),...(document.unarchives??[]),...(document.occurrenceCorrections??[]),...(document.reviewEvents??[])].sort((a,b)=>a.revision-b.revision);
 const previousSources=new Map<string,number>(),latest=new Map<string,number>();
 for(const source of sources){const key=source.taskId+':'+source.revision;if(previousSources.has(key))continue;previousSources.set(key,latest.get(source.taskId)??0);latest.set(source.taskId,source.revision);}
 const witnesses=new Map(document.activityCoverage.map(e=>[e.taskId+':'+e.revision,e]));
 for(const activity of [...document.completions,...document.habitScores]){const witness=witnesses.get(activity.taskId+':'+activity.revision);if(witness&&witness.commandId!==activity.commandId)return fail();}
 const completions=new Map<string,typeof document.completions>(),scores=new Map<string,typeof document.habitScores>();
 for(const e of document.completions){const rows=completions.get(e.commandId)??[];rows.push(e);completions.set(e.commandId,rows);}
 for(const e of document.habitScores){const rows=scores.get(e.commandId)??[];rows.push(e);scores.set(e.commandId,rows);}
 const terminals=new Map<string,typeof document.occurrences>();
 for(const e of [...document.occurrences,...(document.occurrenceCorrections??[]).map(e=>e.previousOccurrence)])if(e.terminalCommandId){const rows=terminals.get(e.terminalCommandId)??[];rows.push(e);terminals.set(e.terminalCommandId,rows);}
 const taskSnapshots=new Map(commands.filter(e=>e.taskSnapshot).map(e=>[e.command.taskId+':'+(e.command.expectedRevision+1),e.taskSnapshot!]));
 for(const row of document.activityCoverage??[]){
  const entry=entries.get(row.commandId),task=entry?.taskSnapshot,continuesWitness=witnessedRevisions.has(row.taskId+':'+row.previousTaskRevision);
  if(!entry||!task||row.payloadHash!==entry.payloadHash||row.taskId!==entry.command.taskId||row.revision!==entry.command.expectedRevision+1||row.revision!==task.revision
   ||utcTicks(row.recordedAtUtc)!==utcTicks(entry.acceptedAtUtc)||row.kind!==task.kind||row.state!==task.state||row.isArchived!==task.isArchived||!sameRule(row.recurrence,task.recurrence)
   ||!acceptedEvents.has(row.commandId+':'+utcTicks(row.recordedAtUtc))
   ||['createTodo','createDaily','createHabit'].includes(entry.command.operation)!==(row.previousTaskRevision===0)
   ||continuesWitness&&(previousSources.get(row.taskId+':'+row.revision)??0)>row.previousTaskRevision)return fail();
  const current=currentTasks.get(row.taskId)!;if(current.revision<row.revision||current.revision===row.revision&&(current.kind!==row.kind||current.state!==row.state||current.isArchived!==row.isArchived||!sameRule(current.recurrence,row.recurrence)))return fail();
  const creating=['createTodo','createDaily','createHabit'].includes(entry.command.operation);
  if(creating&&(row.state!=='active'||row.isArchived||row.kind!==({createTodo:'todo',createDaily:'daily',createHabit:'habit'} as Record<string,string>)[entry.command.operation]||!sameRule(row.recurrence,entry.command.patch?.recurrence??null)))return fail();
  const before=continuesWitness?taskSnapshots.get(row.taskId+':'+row.previousTaskRevision):undefined,operation=entry.command.operation;
  const expectedState=operation==='pauseTask'?'paused':['resumeTask','restoreTodo'].includes(operation)?'active':operation==='completeTask'&&row.kind==='todo'?'completed':operation==='setTodoState'?entry.command.patch!.todoState!.state:before?.state;
  if(before&&(row.kind!==before.kind||!sameRule(row.recurrence,operation==='updateTask'?entry.command.patch!.clearRecurrence?null:entry.command.patch!.recurrence??before.recurrence:before.recurrence)||row.isArchived!==(operation==='deleteTask'||operation!=='unarchiveTask'&&before.isArchived)||row.state!==expectedState))return fail();
  const completionRows=completions.get(row.commandId)??[],scoreRows=scores.get(row.commandId)??[];
  if(operation==='skipOccurrence'||operation==='completeTask'&&row.kind==='daily'){
   const rows=terminals.get(row.commandId)??[],terminal=rows[0];if(rows.length!==1||terminal.taskId!==row.taskId||terminal.revision!==row.revision||terminal.date!==entry.command.occurrenceDate||terminal.state!==(operation==='skipOccurrence'?'skipped':'completed')||operation==='completeTask'&&completionRows[0]?.occurrenceId!==terminal.id)return fail();
  }
  if(completionRows.length!==(entry.command.operation==='completeTask'?1:0)||completionRows.some(e=>e.taskId!==row.taskId||e.revision!==row.revision||utcTicks(e.completedAtUtc)!==utcTicks(row.recordedAtUtc))
   ||scoreRows.length!==(entry.command.operation==='scoreHabit'?entry.command.count:0)||scoreRows.some(e=>e.taskId!==row.taskId||e.revision!==row.revision||utcTicks(e.recordedAtUtc)!==utcTicks(row.recordedAtUtc)||e.direction!==entry.command.direction))return fail();
 }
}
