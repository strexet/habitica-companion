import type {WorkspaceDocument,Entity} from './workspace-domain.ts';
import {workspaceObject,uuid,WorkspaceError} from './workspace-http.ts';
export interface SourceRetirement extends Entity {conversionId:string;sourceTaskId:string;sourceRevision:number;reviewedRevision:number;accountId:string;habiticaTaskId:string;contextHash:string;outcome:'originalReceipt'|'alreadyAbsent'|'observedAbsentAfterUnknown';nativeAttemptId:string|null;verifiedAtUtc:string;retiredAtUtc:string}
const fail=():never=>{throw new WorkspaceError('invalid_source_retirement',422);};
const revision=(value:unknown,max:number)=>{if(!Number.isSafeInteger(value)||Number(value)<1||Number(value)>max)return fail();return Number(value);};
const stable=(value:unknown):unknown=>Array.isArray(value)?value.map(stable):value&&typeof value==='object'?Object.fromEntries(Object.entries(value).sort(([a],[b])=>a.localeCompare(b)).map(([key,item])=>[key,stable(item)])):value;
export function sourceRetirements(input:unknown,document:WorkspaceDocument,utc:(value:unknown)=>string,ticks:(value:unknown)=>bigint):SourceRetirement[]|null{
 if(input==null)return null;if(!Array.isArray(input)||input.length>1000)return fail();
 const sources=new Set<string>(),conversions=new Set<string>(),commands=new Set([...(document.organizationHistory??[]).map(row=>row.command.id),...(document.checklistConversions??[]).flatMap(row=>row.receipts.map(receipt=>receipt.command.id)),...document.completions.map(row=>row.commandId),...document.habitScores.map(row=>row.commandId),...(document.unarchives??[]).map(row=>row.commandId),...(document.occurrenceCorrections??[]).map(row=>row.commandId),...(document.restorations??[]).map(row=>row.commandId)]);
 return input.map(value=>{
  const row=workspaceObject(value,['id','revision','isArchived','conversionId','sourceTaskId','sourceRevision','reviewedRevision','accountId','habiticaTaskId','contextHash','outcome','nativeAttemptId','verifiedAtUtc','retiredAtUtc']);
  const id=uuid(row.id),rev=revision(row.revision,document.revision),conversionId=uuid(row.conversionId),sourceTaskId=uuid(row.sourceTaskId),sourceRevision=revision(row.sourceRevision,rev-1),reviewedRevision=revision(row.reviewedRevision,rev-1),accountId=uuid(row.accountId),habiticaTaskId=uuid(row.habiticaTaskId),nativeAttemptId=row.nativeAttemptId==null?null:uuid(row.nativeAttemptId),verifiedAtUtc=utc(row.verifiedAtUtc),retiredAtUtc=utc(row.retiredAtUtc);
  if(commands.has(id)||sources.has(sourceTaskId)||conversions.has(conversionId)||row.isArchived!==undefined&&row.isArchived!==false||document.tagLedger&&document.tagLedger.accountId!==accountId
   ||typeof row.contextHash!=='string'||!/^[a-f0-9]{64}$/.test(row.contextHash)||!['originalReceipt','alreadyAbsent','observedAbsentAfterUnknown'].includes(String(row.outcome))||sourceRevision>reviewedRevision
   ||(row.outcome==='alreadyAbsent'?nativeAttemptId!==null:nativeAttemptId===null)||ticks(verifiedAtUtc)>ticks(retiredAtUtc))return fail();
  commands.add(id);sources.add(sourceTaskId);conversions.add(conversionId);
  const conversion=document.checklistConversions?.find(value=>value.id===conversionId),source=document.tasks.find(task=>task.id===sourceTaskId),mapping=document.projections.find(value=>value.taskId===sourceTaskId);
  if(!conversion||conversion.sourceTaskId!==sourceTaskId||conversion.sourceRevision!==sourceRevision||conversion.revision>reviewedRevision||conversion.items.some(item=>!item.targetTaskId)
   ||!source||source.kind!=='todo'||!source.isArchived||source.revision!==rev||ticks(source.updatedAtUtc)!==ticks(retiredAtUtc)||!mapping||mapping.state!=='linked'||!mapping.isArchived||mapping.habiticaTaskId!==habiticaTaskId||mapping.revision!==rev||!mapping.fingerprint)return fail();
  const restored={...source,revision:conversion.sourceSnapshot.revision,updatedAtUtc:conversion.sourceSnapshot.updatedAtUtc,isArchived:conversion.sourceSnapshot.isArchived};if(JSON.stringify(stable(restored))!==JSON.stringify(stable(conversion.sourceSnapshot)))return fail();
  return {id,revision:rev,isArchived:false,conversionId,sourceTaskId,sourceRevision,reviewedRevision,accountId,habiticaTaskId,contextHash:row.contextHash,outcome:row.outcome as SourceRetirement['outcome'],nativeAttemptId,verifiedAtUtc,retiredAtUtc};
 });
}
