import {conversionCommand,conversionHash,conversionDerivedId,type ConversionBinding,type ChecklistConversion} from '../../shared/checklist-conversion.ts';
import {workspaceCommand} from '../../shared/workspace-commands.ts';
import {journal,type Journal} from './journal.ts';
import {accept} from './commands.ts';
import {acceptOrganization,acceptCloudOrganization} from './organization-commands.ts';
export interface ConversionSubmission {disposition:string;snapshot:Journal|null;conversion:ChecklistConversion|null;errorCode:string|null}
export const acceptConversion=(state:Journal,command:unknown,now:string)=>acceptOwned(state,command,now,'local');
export const acceptCloudConversion=(state:Journal,command:unknown,now:string)=>acceptOwned(state,command,now,'cloud');
async function acceptOwned(initial:Journal,input:unknown,now:string,owner:'local'|'cloud'):Promise<ConversionSubmission>{
 const reject=(errorCode:string,disposition='invalid'):ConversionSubmission=>({disposition,snapshot:initial,conversion:null,errorCode});
 let state:Journal,command:ReturnType<typeof conversionCommand>;
 try{state=await journal(initial);command=conversionCommand(input);if(!Number.isFinite(Date.parse(now))||now.startsWith('0001-01-01'))throw Error();}catch{return reject('invalid_conversion_command');}
 const document=state.document,hash=await conversionHash(command);
 for(const conversion of document.checklistConversions??[]){const receipt=conversion.receipts.find(receipt=>receipt.command.id===command.id);if(receipt)return receipt.payloadHash===hash?{disposition:'duplicate',snapshot:state,conversion,errorCode:null}:reject('command_id_reused','idempotencyConflict');}
 if(command.workspaceId!==document.id)return reject('invalid_conversion_command');
 if(state.commands.some(row=>row.command.id===command.id)||document.organizationHistory?.some(row=>row.command.id===command.id))return reject('command_id_reused','idempotencyConflict');
 if(owner==='cloud'?state.authority?.mode!=='cloud'||state.authority.cloudWorkspaceId!==document.id:['cloud','frozen'].includes(state.authority?.mode??''))return reject('workspace_owned_elsewhere');
 if(command.expectedRevision!==document.revision)return reject('stale_revision','revisionConflict');
 const source=document.tasks.find(task=>task.id===command.sourceTaskId),checklist=source?.habiticaDetails?.checklist;
 if(!source||source.kind!=='todo'||source.isArchived||source.revision!==command.sourceRevision||!checklist?.length)return reject('conversion_source_changed');
 if(command.items.length!==checklist.length||!command.items.every((item,index)=>item.sourceItemId===checklist[index].id))return reject('complete_checklist_mapping_required');
 const prior=document.checklistConversions?.find(row=>row.sourceTaskId===source.id),project=document.projects.find(row=>row.id===command.projectId);
 if(prior&&(prior.isArchived||prior.id!==command.conversionId||prior.sourceRevision!==source.revision||prior.projectId!==command.projectId))return reject('resume_original_conversion_required');
 if(!prior&&document.checklistConversions?.some(row=>row.id===command.conversionId))return reject('conversion_id_reused');
 if(project&&(project.isArchived||project.isRemoved||project.name!==command.projectName))return reject('conversion_project_changed');
 if(prior&&!project)return reject('conversion_project_missing');
 let next=structuredClone(state);
 if(!project){
  const created=await(owner==='cloud'?acceptCloudOrganization:acceptOrganization)(next,{id:await conversionDerivedId(command.id,'project'),workspaceId:document.id,expectedRevision:next.document.revision,kind:'project',operation:'create',entityId:command.projectId,patch:{name:command.projectName,categoryId:source.categoryId??document.projects.find(row=>row.id===source.projectId)?.categoryId??null}},now);
  if(created.disposition!=='accepted')return reject(created.errorCode??'conversion_project_unavailable');next=created.snapshot!;
 }
 const bindings:ConversionBinding[]=[];
 for(let index=0;index<command.items.length;index++){
  const item=command.items[index],old=prior?.items[index];
  if(old?.targetTaskId&&(item.targetTaskId!==old.targetTaskId||item.desiredState!==old.desiredState||item.mode==='defer'))return reject('conversion_binding_changed');
  if(old?.targetTaskId&&item.notes!==null&&item.notes!==old.addedNotes)return reject('conversion_notes_changed');
  if(item.mode==='defer'){bindings.push({sourceItemId:item.sourceItemId,targetTaskId:null,desiredState:item.desiredState,createdByConversion:false,addedNotes:null,originalTarget:null,historicalTimeUncertain:item.desiredState==='completedArchived'});continue;}
  if(document.checklistConversions?.some(row=>row.id!==command.conversionId&&row.items.some(binding=>binding.targetTaskId===item.targetTaskId)))return reject('conversion_target_bound_elsewhere');
  let target=next.document.tasks.find(task=>task.id===item.targetTaskId),createdByConversion=old?.createdByConversion===true,newlyCreated=false;
  if(!target){
   if(item.mode!=='create'||old?.targetTaskId)return reject(item.desiredState==='completedArchived'?'historical_target_required':'conversion_target_missing');
   const notes=item.notes===null?source.notes:source.notes===null?item.notes:source.notes+'\n\n'+item.notes;if(notes!==null&&notes.length>10000)return reject('conversion_note_capacity');
   try{
    const created=await accept(next.document,workspaceCommand({id:await conversionDerivedId(command.id,'create:'+item.sourceItemId),workspaceId:document.id,expectedRevision:next.document.revision,operation:'createTodo',taskId:item.targetTaskId,patch:{text:checklist[index].text,notes,priority:source.priority,habiticaDetails:{difficulty:item.gameDetails!.difficulty,attribute:item.gameDetails!.attribute,checklist:[]}},projectToHabitica:true}),now);
    next={...next,document:created.document,storeVersion:next.storeVersion+1,commands:[...next.commands,created.entry],events:[...next.events,created.event]};target=next.document.tasks.find(task=>task.id===item.targetTaskId)!;createdByConversion=newlyCreated=true;
   }catch(error){return reject(error instanceof Error?error.message:'conversion_capture_unavailable');}
  }else if(item.mode==='create'&&!createdByConversion)return reject('conversion_target_already_exists');
  if(old?.createdByConversion&&item.mode==='create'&&(item.gameDetails?.difficulty!==old.originalTarget?.habiticaDetails?.difficulty||item.gameDetails?.attribute!==old.originalTarget?.habiticaDetails?.attribute))return reject('conversion_creation_fields_changed');
  if(target.id===source.id||target.kind!=='todo'||item.desiredState==='active'&&(target.state!=='active'||target.isArchived)||item.desiredState==='completedArchived'&&target.state!=='completed')return reject('conversion_target_state_changed');
  const placed={...target,projectId:command.projectId,sectionId:target.projectId===command.projectId?target.sectionId:null,categoryId:newlyCreated?source.categoryId:target.categoryId,labelIds:newlyCreated?source.labelIds:target.labelIds,isArchived:item.desiredState==='completedArchived',revision:next.document.revision+1,updatedAtUtc:now};
  if(target.projectId!==placed.projectId||target.sectionId!==placed.sectionId||target.categoryId!==placed.categoryId||target.isArchived!==placed.isArchived||JSON.stringify(target.labelIds)!==JSON.stringify(placed.labelIds))next={...next,document:{...next.document,revision:placed.revision,tasks:next.document.tasks.map(task=>task.id===target!.id?placed:task)},storeVersion:next.storeVersion+1};
  bindings.push({sourceItemId:item.sourceItemId,targetTaskId:target.id,desiredState:item.desiredState,createdByConversion,addedNotes:old?.addedNotes??item.notes,originalTarget:structuredClone(old?.originalTarget??target),historicalTimeUncertain:item.desiredState==='completedArchived'});
 }
 const revision=next.document.revision+1;if(revision>Number.MAX_SAFE_INTEGER||next.storeVersion>=Number.MAX_SAFE_INTEGER)return reject('revision_exhausted');
 const conversion:ChecklistConversion={id:command.conversionId,revision,isArchived:false,sourceTaskId:source.id,sourceRevision:source.revision,sourceSnapshot:structuredClone(prior?.sourceSnapshot??source),sourceProjection:structuredClone(prior?.sourceProjection??document.projections.find(row=>row.taskId===source.id)??null),projectId:command.projectId,items:bindings,receipts:[...(prior?.receipts??[]),{command,payloadHash:hash,acceptedAtUtc:now,resultRevision:revision}]};
 next={...next,document:{...next.document,revision,checklistConversions:[...(document.checklistConversions??[]).filter(row=>row.id!==conversion.id),conversion]},storeVersion:next.storeVersion+1};
 try{return {disposition:'accepted',snapshot:await journal(next),conversion,errorCode:null};}catch{return reject('invalid_conversion_result');}
}
