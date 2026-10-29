import type {WorkspaceDocument,Entity} from './workspace-domain.ts';
import {workspaceObject as object,uuid,WorkspaceError} from './workspace-http.ts';
export type TagState='pending'|'confirmed'|'unknownOutcome'|'needsReview';
export type TagOperation='ensureTag'|'setMembership';
export type TagGroupKind='category'|'project'|'section'|'label'|'goal'|'savedTaskView';
export interface TagEvidence {receiptId:string;confirmedAtUtc:string;fingerprint:string}
export interface TagMember {taskId:string;habiticaTaskId:string|null;present:boolean;state:TagState;evidence:TagEvidence|null}
export interface TagMapping extends Entity {entityId:string;kind:TagGroupKind;state:TagState;desiredRevision:number;correlationId:string;correlationAlias:string;desiredName:string;habiticaTagId:string|null;evidence:TagEvidence|null}
export interface TagIntent extends Entity {mappingId:string;commandId:string;correlationId:string;desiredRevision:number;operation:TagOperation;state:TagState;habiticaTagId:string|null;desiredName:string|null;members:TagMember[]|null;evidence:TagEvidence|null}
export interface TagLedger {accountId:string;mappings:TagMapping[];intents:TagIntent[]}
const fail=():never=>{throw new WorkspaceError('invalid_tag_ledger',422);};
const kinds:TagGroupKind[]=['category','project','section','label','goal','savedTaskView'];
const states:TagState[]=['pending','confirmed','unknownOutcome','needsReview'];
function choice<T extends string>(value:unknown,values:T[]):T{if(typeof value!=='string'||!values.includes(value as T))return fail();return value as T;}
function revision(value:unknown,max:number):number{if(!Number.isSafeInteger(value)||Number(value)<1||Number(value)>max)return fail();return Number(value);}
function name(value:unknown):string{if(typeof value!=='string'||/^\p{White_Space}*$/u.test(value)||value.length>256)return fail();return value;}
function remote(value:unknown):string|null{return value==null?null:uuid(value);}
export function tagAlias(workspaceId:string,kind:TagGroupKind,entityId:string):string{return `companion_tag_${workspaceId.replaceAll('-','')}_${kind}_${entityId.replaceAll('-','')}`;}

export function tagLedger(value:unknown,document:WorkspaceDocument,utc:(value:unknown)=>string):TagLedger|null{
 if(value==null)return null;
 const ledger=object(value,['accountId','mappings','intents']);const accountId=uuid(ledger.accountId);
 if(!Array.isArray(ledger.mappings)||!Array.isArray(ledger.intents)||ledger.mappings.length+ledger.intents.length>100000)return fail();
 function evidence(value:unknown):TagEvidence|null{if(value==null)return null;const row=object(value,['receiptId','confirmedAtUtc','fingerprint']);if(typeof row.fingerprint!=='string'||!/^[a-f0-9]{64}$/.test(row.fingerprint))return fail();return {receiptId:uuid(row.receiptId),confirmedAtUtc:utc(row.confirmedAtUtc),fingerprint:row.fingerprint};}
 function entity(row:Record<string,unknown>):Entity{if(row.isArchived!==undefined&&typeof row.isArchived!=='boolean')return fail();return {id:uuid(row.id),revision:revision(row.revision,document.revision),isArchived:row.isArchived===true};}
 const knownGroups=new Set(kinds.flatMap(kind=>({category:document.categories,project:document.projects,section:document.sections,label:document.labels,goal:document.goals,savedTaskView:document.savedTaskViews??[]})[kind].map(row=>kind+':'+row.id)));
 const mappingKeys=new Set<string>(),remoteIds=new Set<string>();
 const mappings:TagMapping[]=ledger.mappings.map(value=>{const row=object(value,['id','revision','isArchived','entityId','kind','state','desiredRevision','correlationId','correlationAlias','desiredName','habiticaTagId','evidence']);
  const base=entity(row),kind=choice(row.kind,kinds),entityId=uuid(row.entityId),state=choice(row.state,states),habiticaTagId=remote(row.habiticaTagId),proof=evidence(row.evidence);
  if(!knownGroups.has(kind+':'+entityId)||mappingKeys.has(entityId)||row.correlationAlias!==tagAlias(document.id,kind,entityId)
    ||habiticaTagId&&remoteIds.has(habiticaTagId)||state==='confirmed'&&(!habiticaTagId||!proof)||proof&&!habiticaTagId)return fail();
  mappingKeys.add(entityId);if(habiticaTagId)remoteIds.add(habiticaTagId);
  return {...base,entityId,kind,state,desiredRevision:revision(row.desiredRevision,base.revision),correlationId:uuid(row.correlationId),correlationAlias:row.correlationAlias as string,desiredName:name(row.desiredName),habiticaTagId,evidence:proof};
 });
 const mappingMap=new Map(mappings.map(row=>[row.id,row]));if(mappingMap.size!==mappings.length)return fail();
 const tasks=new Set(document.tasks.map(row=>row.id)),commandKeys=new Set<string>();
 const intents:TagIntent[]=ledger.intents.map(value=>{const row=object(value,['id','revision','isArchived','mappingId','commandId','correlationId','desiredRevision','operation','state','habiticaTagId','desiredName','members','evidence']);
  const base=entity(row),mappingId=uuid(row.mappingId),mapping=mappingMap.get(mappingId),commandId=uuid(row.commandId),operation=choice<TagOperation>(row.operation,['ensureTag','setMembership']),state=choice(row.state,states),proof=evidence(row.evidence),habiticaTagId=remote(row.habiticaTagId),key=`${commandId}:${mappingId}:${operation}`;
  if(!mapping||commandKeys.has(key)||state==='confirmed'&&(!proof||!habiticaTagId)||proof&&!habiticaTagId)return fail();commandKeys.add(key);
  let members:TagMember[]|null=null,desiredName:string|null=null;
  if(operation==='ensureTag'){desiredName=name(row.desiredName);if(row.members!=null)return fail();}
  else {if(row.desiredName!=null||!Array.isArray(row.members)||row.members.length>100000)return fail();members=row.members.map(value=>{const member=object(value,['taskId','habiticaTaskId','present','state','evidence']),taskId=uuid(member.taskId),remoteTaskId=remote(member.habiticaTaskId),memberState=choice(member.state,states),memberEvidence=evidence(member.evidence);if(!tasks.has(taskId)||typeof member.present!=='boolean'||memberEvidence&&(!remoteTaskId||!habiticaTagId)||memberState==='confirmed'&&(!memberEvidence||!remoteTaskId||!habiticaTagId)||state==='confirmed'&&memberState!=='confirmed')return fail();return {taskId,habiticaTaskId:remoteTaskId,present:member.present,state:memberState,evidence:memberEvidence};});if(new Set(members.map(row=>row.taskId)).size!==members.length||new Set(members.filter(row=>row.habiticaTaskId).map(row=>row.habiticaTaskId)).size!==members.filter(row=>row.habiticaTaskId).length)return fail();}
  return {...base,mappingId,commandId,correlationId:uuid(row.correlationId),desiredRevision:revision(row.desiredRevision,Math.min(base.revision,mapping.desiredRevision)),operation,state,habiticaTagId,desiredName,members,evidence:proof};
 });
 return {accountId,mappings,intents};
}
