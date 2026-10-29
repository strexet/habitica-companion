import {workspaceTokenHash} from '../../shared/workspace-http.ts';
import type {CompanionTask,Projection,Project} from '../../shared/workspace-domain.ts';
import type {ConversionState} from '../../shared/checklist-conversion.ts';
import {desiredTagMembership} from '../../shared/tag-membership.ts';
import {scopedTagCorrespondence} from '../../shared/tag-correspondence.ts';
import {journal,type Journal} from './journal.ts';
export interface RetirementTarget {sourceItemId:string;desiredState:ConversionState;task:CompanionTask;mapping:Projection}
export interface RetirementPlan {workspaceId:string;revision:number;conversionId:string;source:CompanionTask;sourceMapping:Projection;project:Project;habiticaProjectTagId:string;targets:RetirementTarget[];contextHash:string}
export interface RetirementReadiness {plan:RetirementPlan|null;blockers:string[];canPrepareNativeReview:boolean;requiresFreshNativeVerification:true;canDelete:false}
const linked=(mapping:Projection|undefined):mapping is Projection=>Boolean(mapping&&mapping.state==='linked'&&!mapping.isArchived&&mapping.habiticaTaskId&&mapping.fingerprint?.length===64);
const stable=(value:unknown):unknown=>Array.isArray(value)?value.map(stable):value&&typeof value==='object'?Object.fromEntries(Object.entries(value).sort(([a],[b])=>a.localeCompare(b)).map(([key,item])=>[key,stable(item)])):value;
function result(plan:RetirementPlan|null,blockers:string[]):RetirementReadiness{return {plan,blockers:[...new Set(blockers)],canPrepareNativeReview:plan!==null&&blockers.length===0,requiresFreshNativeVerification:true,canDelete:false};}
// This portable read policy never archives a source and never grants service dispatch authority.
export async function inspectRetirement(initial:Journal,conversionId:string,expectedAccountId:string|null):Promise<RetirementReadiness>{
 let state:Journal;try{state=await journal(initial);}catch{return result(null,['invalid_workspace']);}
 if(!expectedAccountId||expectedAccountId==='00000000-0000-0000-0000-000000000000')return result(null,['retirement_account_unverified']);
 const document=state.document,conversion=document.checklistConversions?.find(row=>row.id===conversionId&&!row.isArchived),blockers:string[]=[];
 if(!conversion)return result(null,['conversion_unavailable']);
 const source=document.tasks.find(row=>row.id===conversion.sourceTaskId)!;
 if(source.isArchived||source.revision!==conversion.sourceRevision||JSON.stringify(stable(source))!==JSON.stringify(stable(conversion.sourceSnapshot)))return result(null,['conversion_source_changed']);
 const project=document.projects.find(row=>row.id===conversion.projectId&&!row.isArchived&&!row.isRemoved);if(!project)return result(null,['conversion_project_unavailable']);
 if(source.parentTaskId||source.dependsOn.length||document.tasks.some(task=>task.parentTaskId===source.id||task.dependsOn.includes(source.id))||document.goals.some(goal=>goal.taskIds.includes(source.id)))blockers.push('source_relationships_require_review');
 if(state.commands.some(row=>['pending','leased','executing','blocked','unknownOutcome'].includes(row.projectionState)))blockers.push('unresolved_task_effects');
 const sourceMapping=document.projections.find(row=>row.taskId===source.id);
 if(!linked(sourceMapping)||conversion.sourceProjection?.habiticaTaskId&&sourceMapping?.habiticaTaskId!==conversion.sourceProjection.habiticaTaskId)blockers.push('source_mapping_required');
 const targets:RetirementTarget[]=[];
 for(const binding of conversion.items){
  if(!binding.targetTaskId){blockers.push(binding.desiredState==='completedArchived'?'historical_target_required':'conversion_target_required');continue;}
  const task=document.tasks.find(row=>row.id===binding.targetTaskId)!;
  if(task.projectId!==project.id||(binding.desiredState==='active'?task.state!=='active'||task.isArchived:task.state!=='completed'||!task.isArchived))blockers.push('conversion_target_placement_changed');
  const mapping=document.projections.find(row=>row.taskId===task.id);if(!linked(mapping)){blockers.push('target_mapping_required');continue;}
  targets.push({sourceItemId:binding.sourceItemId,desiredState:binding.desiredState,task,mapping});
 }
 const ledger=document.tagLedger;let nativeTag:string|null=null;
 if(ledger?.accountId!==expectedAccountId)blockers.push('retirement_tag_account_unverified');
 else{
  const mapping=ledger.mappings.find(row=>row.entityId===project.id&&row.kind==='project'),group=desiredTagMembership(document).groups.find(row=>row.entityId===project.id&&row.kind==='project');
  if(!group||!scopedTagCorrespondence(document,group,mapping).confirmed)blockers.push('project_tag_membership_requires_review');
  else nativeTag=mapping!.habiticaTagId;

 }
 if(blockers.length)return result(null,blockers);
 const plan:RetirementPlan={workspaceId:document.id,revision:document.revision,conversionId:conversion.id,source,sourceMapping:sourceMapping!,project,habiticaProjectTagId:nativeTag!,targets,contextHash:''};plan.contextHash=await retirementContextHash(plan);return result(plan,[]);
}
export async function retirementContextHash(plan:RetirementPlan):Promise<string>{
 const fields:(string|null)[]=[plan.workspaceId,String(plan.revision),plan.conversionId,plan.project.id,plan.project.name,plan.habiticaProjectTagId,String(plan.targets.length)];
 function task(row:CompanionTask,mapping:Projection){
  fields.push(row.id,String(row.revision),row.text,row.notes,row.kind,row.state,row.priority,String(row.isArchived),row.projectId,row.deadline,row.habiticaDetails?.difficulty==null?null:String(row.habiticaDetails.difficulty),row.habiticaDetails?.attribute??null,mapping.id,String(mapping.revision),mapping.habiticaTaskId,mapping.fingerprint??null);
  const items=row.habiticaDetails?.checklist;fields.push(items==null?null:String(items.length));for(const item of items??[])fields.push(item.id,item.text,String(item.completed));
 }
 task(plan.source,plan.sourceMapping);for(const target of plan.targets){fields.push(target.sourceItemId,target.desiredState);task(target.task,target.mapping);}
 return workspaceTokenHash('companion-conversion-retirement-context-v1\n'+fields.map(field=>field===null?'-1:':new TextEncoder().encode(field).length+':'+field).join(''));
}
