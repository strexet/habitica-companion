import {workspaceObject,workspaceTokenHash,uuid,WorkspaceError} from './workspace-http.ts';
import {utcTicks,type WorkspaceDocument,type CompanionTask,type Projection,type Completion,type Entity} from './workspace-domain.ts';
import {completionScoreBinding,completionScoreSource,type CompletionScoreSource} from './completion-score-source.ts';
import type {Journal} from '../companion-workspace/src/journal.ts';
export interface LinkedRecoveryPlan {
 workspaceId:string;reviewedRevision:number;task:CompanionTask;mapping:Projection;completion:Completion;sourceContext:CompletionScoreSource;sourceAttemptId:string;accountId:string;contextHash:string;
}
export interface LinkedRecoveryReceipt extends Entity {
 taskId:string;completionId:string;sourceAttemptId:string;recoveryAttemptId:string;habiticaTaskId:string;accountId:string;reviewedRevision:number;taskRevision:number;
 mappingId:string;mappingRevision:number;mappingFingerprint:string;contextHash:string;upstreamReviewHash:string;sourceContext:CompletionScoreSource;recoveredAtUtc:string;
}
const fail=():never=>{throw new WorkspaceError('invalid_linked_recovery',422);};
const hash=(input:unknown)=>typeof input==='string'&&/^[a-f0-9]{64}$/.test(input)?input:fail();
function integer(input:unknown,max=Number.MAX_SAFE_INTEGER){if(!Number.isSafeInteger(input)||Number(input)<1||Number(input)>max)return fail();return Number(input);}
export function linkedRecoveryRows(input:unknown,document:WorkspaceDocument,utc:(value:unknown)=>string):LinkedRecoveryReceipt[]|null {
 if(input==null)return null;if(!Array.isArray(input)||input.length>1000)return fail();
 const completions=new Set<string>();
 return input.map(value=>{
  const row=workspaceObject(value,['id','revision','isArchived','taskId','completionId','sourceAttemptId','recoveryAttemptId','habiticaTaskId','accountId','reviewedRevision','taskRevision','mappingId','mappingRevision','mappingFingerprint','contextHash','upstreamReviewHash','sourceContext','recoveredAtUtc']);
  const source=completionScoreSource(row.sourceContext);if(!source)return fail();
  const result:LinkedRecoveryReceipt={id:uuid(row.id),revision:integer(row.revision,document.revision),isArchived:false,taskId:uuid(row.taskId),completionId:uuid(row.completionId),sourceAttemptId:uuid(row.sourceAttemptId),recoveryAttemptId:uuid(row.recoveryAttemptId),habiticaTaskId:uuid(row.habiticaTaskId),accountId:uuid(row.accountId),reviewedRevision:integer(row.reviewedRevision,document.revision),taskRevision:integer(row.taskRevision,document.revision),mappingId:uuid(row.mappingId),mappingRevision:integer(row.mappingRevision,document.revision),mappingFingerprint:hash(row.mappingFingerprint),contextHash:hash(row.contextHash),upstreamReviewHash:hash(row.upstreamReviewHash),sourceContext:source,recoveredAtUtc:utc(row.recoveredAtUtc)};
  if(row.isArchived!==undefined&&row.isArchived!==false||completions.has(result.completionId)||result.recoveryAttemptId!==result.id||result.sourceAttemptId===result.recoveryAttemptId||result.id===source.commandId
   ||result.reviewedRevision+1!==result.revision||result.taskRevision>result.reviewedRevision||result.mappingRevision>result.reviewedRevision
   ||source.workspaceId!==document.id||source.taskId!==result.taskId||source.completionId!==result.completionId)return fail();
  completions.add(result.completionId);
  const completion=document.completions.find(value=>value.id===result.completionId),task=document.tasks.find(value=>value.id===result.taskId);
  if(!completion||!task||completion.taskId!==result.taskId||completion.commandId!==source.commandId||task.kind!==source.taskKind||task.revision<result.revision||completion.revision>result.reviewedRevision||utcTicks(result.recoveredAtUtc)<utcTicks(completion.completedAtUtc))return fail();
  if(source.taskKind==='todo'){
   const native=document.restorations?.filter(row=>row.commandId===result.id)??[];
   if(native.length!==1||native[0].taskId!==result.taskId||native[0].completionId!==result.completionId||native[0].revision!==result.revision||utcTicks(native[0].restoredAtUtc)!==utcTicks(result.recoveredAtUtc))return fail();
  }else{
   const native=document.occurrenceCorrections?.filter(row=>row.commandId===result.id)??[];
   if(native.length!==1||native[0].taskId!==result.taskId||native[0].targetCommandId!==source.commandId||native[0].revision!==result.revision||native[0].previousOccurrence.id!==completion.occurrenceId
    ||native[0].previousOccurrence.revision!==completion.revision||native[0].previousOccurrence.date!==source.occurrenceDate||native[0].previousOccurrence.state!=='completed'||utcTicks(native[0].correctedAtUtc)!==utcTicks(result.recoveredAtUtc))return fail();
  }
  return result;
 });
}
export async function linkedRecoveryContext(fields:{workspaceId:string;reviewedRevision:number;taskId:string;taskRevision:number;completion:Completion;sourceContext:CompletionScoreSource;sourceAttemptId:string;habiticaTaskId:string;mappingId:string;mappingRevision:number;mappingFingerprint:string;accountId:string}):Promise<string>{
 const source=fields.sourceContext;
 const parts=[fields.workspaceId,String(fields.reviewedRevision),fields.taskId,String(fields.taskRevision),fields.completion.id,source.commandId,source.commandPayloadHash,String(source.effectIndex),fields.sourceAttemptId,fields.habiticaTaskId,fields.mappingId,String(fields.mappingRevision),fields.mappingFingerprint,fields.accountId,source.taskKind,fields.completion.occurrenceId,source.occurrenceDate,utcTicks(fields.completion.completedAtUtc).toString()];
 return workspaceTokenHash('companion-linked-recovery-context-v1\n'+parts.map(value=>value===null?'-1:':new TextEncoder().encode(value).length+':'+value).join(''));
}
export async function linkedRecoveryPlan(state:Journal,taskId:string,completionId:string,accountId:string,expectedRevision?:number):Promise<LinkedRecoveryPlan>{
 const document=state.document;if(expectedRevision!==undefined&&expectedRevision!==document.revision)throw new WorkspaceError('stale_revision',409);
 if(state.authority?.mode!=='cloud'||state.authority.cloudWorkspaceId!==document.id)throw new WorkspaceError('workspace_owned_elsewhere',409);
 const task=document.tasks.find(row=>row.id===taskId),completion=document.completions.find(row=>row.id===completionId&&row.taskId===taskId);
 if(!task||task.isArchived||!completion||completion.isArchived||!['todo','daily'].includes(task.kind)||task.kind==='todo'&&task.state!=='completed'||task.kind==='daily'&&task.state!=='active')throw new WorkspaceError('linked_recovery_unavailable',409);
 if(document.restorations?.some(row=>row.completionId===completionId)||document.occurrenceCorrections?.some(row=>row.targetCommandId===completion.commandId)||document.linkedTaskRecoveries?.some(row=>row.completionId===completionId))throw new WorkspaceError('completion_already_recovered',409);
 if(state.commands.some(row=>row.effects.length>0&&!['confirmed','cancelled'].includes(row.projectionState)))throw new WorkspaceError('unresolved_task_effects',409);
 const mapping=document.projections.find(row=>row.taskId===taskId);
 if(!mapping||mapping.isArchived||mapping.state!=='linked'||!mapping.habiticaTaskId||!mapping.fingerprint)throw new WorkspaceError('linked_mapping_required',409);
 const entry=state.commands.find(row=>row.command.id===completion.commandId),index=entry?.effects.indexOf('scoreUp')??-1;
 if(!entry||entry.command.operation!=='completeTask'||entry.projectionState!=='confirmed'||entry.confirmedEffects!==entry.effects.length||index<0||entry.effects.lastIndexOf('scoreUp')!==index)throw new WorkspaceError('confirmed_source_required',409);
 const sourceContext=completionScoreBinding(document,{...entry,confirmedEffects:index});
 const dispatched=state.events.filter(row=>row.commandId===entry.command.id&&row.kind==='dispatched'&&row.confirmedEffects===index&&row.attemptToken).at(-1);
 if(!sourceContext||!dispatched?.attemptToken)throw new WorkspaceError('original_score_proof_unavailable',409);
 if(task.kind==='daily'&&!document.occurrences.some(row=>row.id===completion.occurrenceId&&row.taskId===taskId&&row.state==='completed'&&!row.isArchived&&row.revision===completion.revision))throw new WorkspaceError('occurrence_unavailable',409);
 const plan:LinkedRecoveryPlan={workspaceId:document.id,reviewedRevision:document.revision,task,mapping,completion,sourceContext,sourceAttemptId:dispatched.attemptToken,accountId:uuid(accountId),contextHash:''};
 plan.contextHash=await linkedRecoveryContext({workspaceId:document.id,reviewedRevision:document.revision,taskId,taskRevision:task.revision,completion,sourceContext,sourceAttemptId:plan.sourceAttemptId,habiticaTaskId:mapping.habiticaTaskId,mappingId:mapping.id,mappingRevision:mapping.revision,mappingFingerprint:mapping.fingerprint,accountId:plan.accountId});return plan;
}
export async function validateLinkedRecoveryHashes(document:WorkspaceDocument):Promise<void>{
 for(const row of document.linkedTaskRecoveries??[]){
  const completion=document.completions.find(value=>value.id===row.completionId)!;
  const expected=await linkedRecoveryContext({workspaceId:document.id,reviewedRevision:row.reviewedRevision,taskId:row.taskId,taskRevision:row.taskRevision,completion,sourceContext:row.sourceContext,sourceAttemptId:row.sourceAttemptId,habiticaTaskId:row.habiticaTaskId,mappingId:row.mappingId,mappingRevision:row.mappingRevision,mappingFingerprint:row.mappingFingerprint,accountId:row.accountId});
  if(expected!==row.contextHash)fail();
 }
}
export function linkedRecoveryJournalValid(state:Journal):boolean {
 for(const restoration of state.document.restorations??[]){
  const completion=state.document.completions.find(value=>value.id===restoration.completionId),source=state.commands.find(value=>value.command.id===completion?.commandId);
  if(source&&source.effects.length>0&&!state.document.linkedTaskRecoveries?.some(value=>value.id===restoration.commandId&&value.completionId===restoration.completionId))return false;
 }
 for(const row of state.document.linkedTaskRecoveries??[]){const source=state.commands.find(value=>value.command.id===row.sourceContext.commandId);
  if(!source||source.command.operation!=='completeTask'||source.command.taskId!==row.taskId||source.payloadHash!==row.sourceContext.commandPayloadHash||source.projectionState!=='confirmed'
   ||source.confirmedEffects!==source.effects.length||source.effects[row.sourceContext.effectIndex]!=='scoreUp'
   ||!state.events.some(value=>value.commandId===source.command.id&&value.kind==='dispatched'&&value.confirmedEffects===row.sourceContext.effectIndex&&value.attemptToken===row.sourceAttemptId))return false;
 }
 return true;
}
export async function linkedNativeEventId(id:string):Promise<string>{
 const bytes=new Uint8Array(await crypto.subtle.digest('SHA-256',new TextEncoder().encode('companion-linked-native-event-v1\n'+uuid(id)))).slice(0,16);bytes[6]=(bytes[6]&15)|80;bytes[8]=(bytes[8]&63)|128;const text=Array.from(bytes,value=>value.toString(16).padStart(2,'0')).join('');return `${text.slice(0,8)}-${text.slice(8,12)}-${text.slice(12,16)}-${text.slice(16,20)}-${text.slice(20)}`;
}

// This transition is private to the server finalizer after a known original down receipt.
export async function applyLinkedRecovery(state:Journal,plan:LinkedRecoveryPlan,id:string,upstreamReviewHash:string,at:string):Promise<WorkspaceDocument>{
 const fresh=await linkedRecoveryPlan(state,plan.task.id,plan.completion.id,plan.accountId,plan.reviewedRevision);
 if(fresh.contextHash!==plan.contextHash)throw new WorkspaceError('linked_recovery_source_changed',409);
 const document=structuredClone(state.document),revision=document.revision+1;
 const nativeId=await linkedNativeEventId(id),task=document.tasks.find(row=>row.id===plan.task.id)!;
 task.revision=revision;task.updatedAtUtc=at;document.revision=revision;
 if(task.kind==='todo'){
  task.state='active';document.restorations??=[];
  document.restorations.push({id:nativeId,revision,isArchived:false,taskId:task.id,completionId:plan.completion.id,commandId:id,restoredAtUtc:at});
 }else{
  const occurrence=document.occurrences.find(row=>row.id===plan.completion.occurrenceId)!;
  document.occurrenceCorrections??=[];
  document.occurrenceCorrections.push({id:nativeId,revision,isArchived:false,taskId:task.id,commandId:id,targetCommandId:plan.sourceContext.commandId,previousOccurrence:structuredClone(occurrence),correctedAtUtc:at});
  occurrence.state='pending';occurrence.revision=revision;delete occurrence.terminalCommandId;
 }
 document.linkedTaskRecoveries??=[];
 document.linkedTaskRecoveries.push({id,revision,isArchived:false,taskId:task.id,completionId:plan.completion.id,sourceAttemptId:plan.sourceAttemptId,recoveryAttemptId:id,
  habiticaTaskId:plan.mapping.habiticaTaskId!,accountId:plan.accountId,reviewedRevision:plan.reviewedRevision,taskRevision:plan.task.revision,mappingId:plan.mapping.id,
  mappingRevision:plan.mapping.revision,mappingFingerprint:plan.mapping.fingerprint!,contextHash:plan.contextHash,upstreamReviewHash:hash(upstreamReviewHash),sourceContext:plan.sourceContext,recoveredAtUtc:at});
 return document;
}
