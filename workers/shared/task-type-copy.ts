import {workspaceObject,uuid,WorkspaceError} from './workspace-http.ts';
import {workspaceDocument,workspaceTask,workspaceRecurrence,utcTicks,type WorkspaceDocument,type CompanionTask,type Recurrence} from './workspace-domain.ts';
import {expandRecurrence} from './recurrence-calendar.ts';
export interface TaskTypeCopy {sourceTaskId:string;sourceRevision:number;targetKind:CompanionTask['kind'];copyOrganization:boolean;recurrence?:Recurrence|null}
export interface TaskCopyLineage {sourceWorkspaceId:string;sourceSnapshot:CompanionTask;targetTaskId:string;targetKind:CompanionTask['kind'];copyOrganization:boolean;initialTargetRevision:number;commandId:string;payloadHash:string;copiedAtUtc:string;recurrence?:Recurrence|null}
export interface TypeCopyReview {source:CompanionTask|null;target:CompanionTask|null;copiedFields:string[];resetFields:string[];failure:string|null}
const kinds=['todo','daily','habit'];
const fail=(code='invalid_type_copy'):never=>{throw new WorkspaceError(code,422);};
const safe=(value:unknown,min=1,max=Number.MAX_SAFE_INTEGER)=>Number.isSafeInteger(value)&&Number(value)>=min&&Number(value)<=max?Number(value):fail();
function optionalRule(input:unknown):Recurrence|null {try{return workspaceRecurrence(input);}catch{return fail();}}
export function taskTypeCopy(input:unknown):TaskTypeCopy|null{
 if(input==null)return null;const row=workspaceObject(input,['sourceTaskId','sourceRevision','targetKind','copyOrganization','recurrence']);
 if(!kinds.includes(String(row.targetKind))||typeof row.copyOrganization!=='boolean')return fail();const recurrence=optionalRule(row.recurrence);
 if(row.targetKind==='daily'?!recurrence:recurrence!==null)return fail();
 return {sourceTaskId:uuid(row.sourceTaskId),sourceRevision:safe(row.sourceRevision,1,Number.MAX_SAFE_INTEGER-1),targetKind:row.targetKind as CompanionTask['kind'],copyOrganization:row.copyOrganization,...(recurrence?{recurrence}:{})};
}
export const sameRule=(a:Recurrence|null|undefined,b:Recurrence|null|undefined)=>a==null?b==null:b!=null&&a.frequency===b.frequency&&a.interval===b.interval&&a.startDate===b.startDate&&a.timeZoneId===b.timeZoneId&&JSON.stringify(a.weekdays)===JSON.stringify(b.weekdays)&&JSON.stringify(a.daysOfMonth)===JSON.stringify(b.daysOfMonth);
function stable(value:unknown):string {if(value===null||typeof value!=='object')return JSON.stringify(value);if(Array.isArray(value))return '['+value.map(stable).join(',')+']';return '{'+Object.keys(value).filter(key=>(value as Record<string,unknown>)[key]!==undefined).sort().map(key=>JSON.stringify(key)+':'+stable((value as Record<string,unknown>)[key])).join(',')+'}';}
const instant=(value:string|null|undefined)=>value==null?null:utcTicks(value).toString();
function sourceValue(task:CompanionTask){const {copyLineage,habiticaDetails,...rest}=task;return {...rest,createdAtUtc:instant(task.createdAtUtc),updatedAtUtc:instant(task.updatedAtUtc),reviewAfterUtc:instant(task.reviewAfterUtc),staleSinceUtc:instant(task.staleSinceUtc),labelIds:task.labelIds??[],dependsOn:task.dependsOn??[],habiticaDetails:{difficulty:habiticaDetails?.difficulty??null,attribute:habiticaDetails?.attribute??null,checklist:habiticaDetails?.checklist??[],up:habiticaDetails?.up??null,down:habiticaDetails?.down??null}};}
export const sameCopySource=(a:CompanionTask,b:CompanionTask)=>stable(sourceValue(a))===stable(sourceValue(b));
export const sameCopyLineage=(a:TaskCopyLineage|null|undefined,b:TaskCopyLineage|null|undefined)=>a==null?b==null:b!=null&&stable({...a,copiedAtUtc:instant(a.copiedAtUtc),sourceSnapshot:sourceValue(a.sourceSnapshot)})===stable({...b,copiedAtUtc:instant(b.copiedAtUtc),sourceSnapshot:sourceValue(b.sourceSnapshot)});
export function copyLineage(input:unknown,target:CompanionTask,revision:number):TaskCopyLineage|null{
 if(input==null)return null;
 const row=workspaceObject(input,['sourceWorkspaceId','sourceSnapshot','targetTaskId','targetKind','copyOrganization','initialTargetRevision','commandId','payloadHash','copiedAtUtc','recurrence']);
 const source=workspaceObject(row.sourceSnapshot);if(source.copyLineage!=null||source.parentTaskId===source.id||Array.isArray(source.dependsOn)&&source.dependsOn.includes(source.id)||source.plannedDate==='0001-01-01'||source.deadline==='0001-01-01')return fail('invalid_task_copy_lineage');
 const initialTargetRevision=safe(row.initialTargetRevision,2,target.revision),sourceSnapshot=workspaceTask(source,initialTargetRevision-1),targetTaskId=uuid(row.targetTaskId),sourceWorkspaceId=uuid(row.sourceWorkspaceId),commandId=uuid(row.commandId),recurrence=optionalRule(row.recurrence);
 if(sourceSnapshot.parentTaskId===sourceSnapshot.id||sourceSnapshot.dependsOn.includes(sourceSnapshot.id))return fail('invalid_task_copy_lineage');
 if(targetTaskId!==target.id||row.targetKind!==target.kind||sourceSnapshot.id===target.id||sourceSnapshot.kind===row.targetKind||typeof row.copyOrganization!=='boolean'||typeof row.payloadHash!=='string'||!/^[a-f0-9]{64}$/.test(row.payloadHash)||typeof row.copiedAtUtc!=='string'||utcTicks(row.copiedAtUtc)!==utcTicks(target.createdAtUtc)||utcTicks(sourceSnapshot.updatedAtUtc)>utcTicks(row.copiedAtUtc)||(target.kind==='daily'?!recurrence:recurrence!==null)||initialTargetRevision>revision)return fail('invalid_task_copy_lineage');
 return {sourceWorkspaceId,sourceSnapshot,targetTaskId,targetKind:target.kind,copyOrganization:row.copyOrganization,initialTargetRevision,commandId,payloadHash:row.payloadHash,copiedAtUtc:row.copiedAtUtc,...(recurrence?{recurrence}:{})};
}
export function validCopyLineages(document:WorkspaceDocument):boolean{
 for(const task of document.tasks){const line=task.copyLineage;if(!line)continue;
  if(line.sourceWorkspaceId===document.id){const current=document.tasks.find(task=>task.id===line.sourceSnapshot.id);if(!current||current.kind!==line.sourceSnapshot.kind||current.revision<line.sourceSnapshot.revision)return false;}
  else if(!document.sourceImports?.some(origin=>origin.sourceWorkspaceId===line.sourceWorkspaceId&&origin.sourceRevision>=line.initialTargetRevision&&origin.recordIds.includes(task.id)))return false;
 }return true;
}
export function initialCopyTarget(source:CompanionTask,choice:TaskTypeCopy,targetId:string,revision:number,now:string):CompanionTask{
 return {id:targetId,revision,isArchived:false,text:source.text,kind:choice.targetKind,state:'active',priority:source.priority,createdAtUtc:now,updatedAtUtc:now,notes:source.notes,categoryId:choice.copyOrganization?source.categoryId:null,projectId:choice.copyOrganization?source.projectId:null,sectionId:choice.copyOrganization?source.sectionId:null,parentTaskId:null,labelIds:choice.copyOrganization?[...source.labelIds]:[],dependsOn:[],plannedDate:null,deadline:null,reviewAfterUtc:null,staleSinceUtc:null,recurrence:choice.recurrence??null,habiticaDetails:null};
}
export function initialCopyMatches(target:CompanionTask,line:TaskCopyLineage):boolean{
 const expected=initialCopyTarget(line.sourceSnapshot,{sourceTaskId:line.sourceSnapshot.id,sourceRevision:line.sourceSnapshot.revision,targetKind:line.targetKind,copyOrganization:line.copyOrganization,recurrence:line.recurrence},line.targetTaskId,line.initialTargetRevision,line.copiedAtUtc);
 return sameCopySource(target,expected);
}
export function reviewTypeCopy(document:WorkspaceDocument,choice:TaskTypeCopy,targetId:string,now:string):TypeCopyReview{
 const refuse=(failure:string,source:CompanionTask|null=null):TypeCopyReview=>({source,target:null,copiedFields:[],resetFields:[],failure});
 try{choice=taskTypeCopy(choice)!;targetId=uuid(targetId);if(!choice||targetId===choice.sourceTaskId)return refuse('invalid_type_copy');}catch{return refuse('invalid_type_copy');}
 try{document=workspaceDocument(document);}catch{return refuse('invalid_workspace');}
 if(document.revision===Number.MAX_SAFE_INTEGER)return refuse('invalid_workspace');
 const source=document.tasks.find(task=>task.id===choice.sourceTaskId);if(!source||source.revision!==choice.sourceRevision)return refuse('source_revision_mismatch',source??null);
 if(source.kind===choice.targetKind)return refuse('different_task_kind_required',source);
 try{if(utcTicks(now)===utcTicks('0001-01-01T00:00:00Z')||utcTicks(now)<utcTicks(source.updatedAtUtc))return refuse('invalid_copy_clock',source);}catch{return refuse('invalid_copy_clock',source);}
 if(choice.recurrence&&expandRecurrence(choice.recurrence,choice.recurrence.startDate,choice.recurrence.startDate).failure)return refuse('invalid_type_copy',source);
 if(choice.copyOrganization&&(source.categoryId&&!document.categories.some(row=>row.id===source.categoryId&&!row.isArchived)||source.projectId&&!document.projects.some(row=>row.id===source.projectId&&!row.isArchived&&!row.isRemoved)||source.sectionId&&!document.sections.some(row=>row.id===source.sectionId&&!row.isArchived&&row.projectId===source.projectId)||source.labelIds.some(id=>!document.labels.some(row=>row.id===id&&!row.isArchived))))return refuse('copy_organization_repair_required',source);
 const target=initialCopyTarget(source,choice,targetId,document.revision+1,now);try{workspaceDocument({...document,revision:target.revision,tasks:[...document.tasks,target]});}catch{return refuse('invalid_type_copy_target',source);}
 return {source,target,copiedFields:choice.copyOrganization?['Name','Notes','Native urgency','Category/project/section/labels']:['Name','Notes','Native urgency'],resetFields:['State becomes active','Creation/update timestamps','Habitica correspondence','Completion/score/occurrence history','Game difficulty/attribute/checklist/direction settings','Planned/deadline/review dates','Todo stage/parent/dependencies/goal membership','Source schedule is retained only on the source; Daily target recurrence is a new explicit choice',choice.copyOrganization?'No organization mutation':'Target category/project/section/labels are empty'],failure:null};
}
