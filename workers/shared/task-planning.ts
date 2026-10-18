import type {CompanionTask,WorkspaceDocument} from './workspace-domain.ts';
import {workspaceObject,uuid} from './workspace-http.ts';
export interface TaskPlanning {plannedDate:string|null;clearPlannedDate:boolean;categoryId:string|null;clearCategory:boolean;projectId:string|null;clearProject:boolean;sectionId:string|null;clearSection:boolean;labelIds:string[]|null}
export function taskPlanning(input:unknown):TaskPlanning|null {
 if(input===undefined||input===null)return null;
 const row=workspaceObject(input,['plannedDate','clearPlannedDate','categoryId','clearCategory','projectId','clearProject','sectionId','clearSection','labelIds']);
 const fail=():never=>{throw new Error('invalid_command');};
 const bool=(value:unknown)=>value===undefined?false:typeof value==='boolean'?value:fail();
 const id=(value:unknown)=>value===undefined||value===null?null:uuid(value);
 const date=row.plannedDate===undefined||row.plannedDate===null?null:row.plannedDate;
 if(date!==null&&(typeof date!=='string'||!/^\d{4}-\d{2}-\d{2}$/.test(date)||date.startsWith('0000')||date==='0001-01-01'||!Number.isFinite(Date.parse(date))||new Date(date).toISOString().slice(0,10)!==date))fail();
 let labels:string[]|null=null;
 if(row.labelIds!==undefined&&row.labelIds!==null){if(!Array.isArray(row.labelIds)||row.labelIds.length>100)fail();labels=(row.labelIds as unknown[]).map(uuid);if(new Set(labels).size!==labels.length)fail();}
 const result={plannedDate:date as string|null,clearPlannedDate:bool(row.clearPlannedDate),categoryId:id(row.categoryId),clearCategory:bool(row.clearCategory),projectId:id(row.projectId),clearProject:bool(row.clearProject),sectionId:id(row.sectionId),clearSection:bool(row.clearSection),labelIds:labels};
 if(result.clearPlannedDate&&result.plannedDate!==null||result.clearCategory&&result.categoryId!==null||result.clearProject&&result.projectId!==null||result.clearSection&&result.sectionId!==null||Object.values(result).every(value=>value===null||value===false))fail();
 return result;
}
export function applyPlanning(task:CompanionTask,plan:TaskPlanning):CompanionTask{return {...task,plannedDate:plan.clearPlannedDate?null:plan.plannedDate??task.plannedDate,categoryId:plan.clearCategory?null:plan.categoryId??task.categoryId,projectId:plan.clearProject?null:plan.projectId??task.projectId,sectionId:plan.clearSection?null:plan.sectionId??task.sectionId,labelIds:plan.labelIds??task.labelIds};}
export function planningReferencesAvailable(document:WorkspaceDocument,task:CompanionTask,plan:TaskPlanning):boolean{
 if(plan.categoryId&&!document.categories.some(row=>row.id===plan.categoryId&&!row.isArchived)||plan.projectId&&!document.projects.some(row=>row.id===plan.projectId&&!row.isArchived)||plan.sectionId&&!document.sections.some(row=>row.id===plan.sectionId&&!row.isArchived)||plan.labelIds?.some(id=>!document.labels.some(row=>row.id===id&&!row.isArchived)))return false;
 const planned=applyPlanning(task,plan);
 if(plan.sectionId&&!document.projects.some(row=>row.id===planned.projectId&&!row.isArchived))return false;
 return planned.sectionId===null||document.sections.some(row=>row.id===planned.sectionId&&row.projectId===planned.projectId);
}
export function planningMatches(plan:TaskPlanning,task:Record<string,unknown>):boolean{
 return (plan.plannedDate===null||task.plannedDate===plan.plannedDate)&&(!plan.clearPlannedDate||task.plannedDate==null)
  &&(plan.categoryId===null||task.categoryId===plan.categoryId)&&(!plan.clearCategory||task.categoryId==null)
  &&(plan.projectId===null||task.projectId===plan.projectId)&&(!plan.clearProject||task.projectId==null)
  &&(plan.sectionId===null||task.sectionId===plan.sectionId)&&(!plan.clearSection||task.sectionId==null)
  &&(plan.labelIds===null||Array.isArray(task.labelIds)&&JSON.stringify(plan.labelIds)===JSON.stringify(task.labelIds));
}
