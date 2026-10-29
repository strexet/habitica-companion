import {workspaceObject,WorkspaceError,uuid} from './workspace-http.ts';
import {utcTicks,type CompanionTask,type Entity} from './workspace-domain.ts';
export type ReviewAction='set'|'snooze'|'clear'|'markReviewed'|'setStale'|'clearStale';
export interface TaskReview {action:ReviewAction;timeZoneId:string;reviewAfterUtc:string|null;staleSinceUtc:string|null}
export interface ReviewEvent extends Entity {taskId:string;commandId:string;action:ReviewAction;recordedAtUtc:string;timeZoneId:string;reviewAfterUtc:string|null;staleSinceUtc:string|null}
const fail=():never=>{throw new WorkspaceError('invalid_task_review',422);};
export function reviewZone(value:unknown):string{
 if(typeof value!=='string'||value.length>128||value!=='UTC'&&!value.includes('/'))return fail();
 try{new Intl.DateTimeFormat('en-US',{timeZone:value}).format(0);}catch{return fail();}return value;
}
export function reviewInstant(value:unknown):string|null{
 if(value===undefined||value===null)return null;
 if(utcTicks(value)===utcTicks('0001-01-01T00:00:00Z'))return fail();
 const match=(value as string).match(/^(.*T\d\d:\d\d:\d\d)(?:\.(\d{1,7}))?(?:Z|\+00:00)$/)!;
 return match[1]+'.'+(match[2]??'').padEnd(7,'0')+'Z';
}
export function taskReview(value:unknown):TaskReview|null{
 if(value===undefined||value===null)return null;const row=workspaceObject(value,['action','timeZoneId','reviewAfterUtc','staleSinceUtc']);
 if(typeof row.action!=='string'||!['set','snooze','clear','markReviewed','setStale','clearStale'].includes(row.action))return fail();
 const action=row.action as ReviewAction,timeZoneId=reviewZone(row.timeZoneId),reviewAfterUtc=reviewInstant(row.reviewAfterUtc),staleSinceUtc=reviewInstant(row.staleSinceUtc);
 if(['set','snooze'].includes(action)?reviewAfterUtc===null||staleSinceUtc!==null:action==='setStale'?staleSinceUtc===null||reviewAfterUtc!==null:reviewAfterUtc!==null||staleSinceUtc!==null)return fail();
 return {action,timeZoneId,reviewAfterUtc,staleSinceUtc};
}
export function reviewCanApply(patch:TaskReview,task:CompanionTask,now:string):boolean{return !task.isArchived
 &&(patch.action!=='snooze'||task.reviewAfterUtc!==null&&utcTicks(patch.reviewAfterUtc)>utcTicks(task.reviewAfterUtc)&&utcTicks(patch.reviewAfterUtc)>utcTicks(now))
 &&(patch.action!=='setStale'||utcTicks(patch.staleSinceUtc)<=utcTicks(now));}
export function applyReview(patch:TaskReview,task:CompanionTask):void{
 if(['set','snooze'].includes(patch.action))task.reviewAfterUtc=patch.reviewAfterUtc;
 if(['clear','markReviewed'].includes(patch.action))task.reviewAfterUtc=null;
 if(patch.action==='setStale')task.staleSinceUtc=patch.staleSinceUtc;
 if(patch.action==='clearStale')task.staleSinceUtc=null;
}
export function reviewHistoryInput(input:unknown){
 const value=workspaceObject(input,['taskId','limit','offset','expectedRevision']);
 const limit=value.limit??50,offset=value.offset??0;
 if(!Number.isSafeInteger(limit)||Number(limit)<1||Number(limit)>100||!Number.isSafeInteger(offset)||Number(offset)<0||Number(offset)>100000||value.expectedRevision!==undefined&&(!Number.isSafeInteger(value.expectedRevision)||Number(value.expectedRevision)<1)||Number(offset)>0&&value.expectedRevision===undefined)return fail();
 return {taskId:uuid(value.taskId),limit:Number(limit),offset:Number(offset),expectedRevision:value.expectedRevision as number|undefined};
}
export function queryTaskReviews(document:import('./workspace-domain.ts').WorkspaceDocument,input:unknown){
 const filter=reviewHistoryInput(input);if(filter.expectedRevision!==undefined&&filter.expectedRevision!==document.revision)throw new WorkspaceError('stale_revision',409);
 if(!document.tasks.some(task=>task.id===filter.taskId))throw new WorkspaceError('task_unavailable',422);
 const rows=(document.reviewEvents??[]).filter(row=>row.taskId===filter.taskId).sort((a,b)=>b.revision-a.revision),items=rows.slice(filter.offset,filter.offset+filter.limit),next=filter.offset+items.length;
 return {provenance:'companion-canonical',workspaceId:document.id,revision:document.revision,items,matchedCount:rows.length,truncated:next<rows.length,offset:filter.offset,nextOffset:next<rows.length?next:null};
}
