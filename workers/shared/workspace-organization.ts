import {savedTaskQuery,savedTaskQueryFields,savedTaskQueriesEqual,type SavedTaskQuery} from './saved-task-views.ts';
import type {WorkspaceDocument} from './workspace-domain.ts';
import {workspaceObject,uuid,WorkspaceError} from './workspace-http.ts';
export type OrganizationKind='category'|'project'|'section'|'label'|'goal'|'savedTaskView';
export type OrganizationOperation='create'|'update'|'archive'|'remove';
export interface ProjectRemoval {taskIds:string[];sectionIds:string[]}
export interface OrganizationPatch {name:string|null;categoryId:string|null;clearCategory:boolean;projectId:string|null;position:number|null;taskIds:string[]|null;query?:SavedTaskQuery;removal?:ProjectRemoval;alias?:string;clearAlias?:boolean}
export interface OrganizationCommand {id:string;workspaceId:string;expectedRevision:number;kind:OrganizationKind;operation:OrganizationOperation;entityId:string;patch:OrganizationPatch|null}
export interface OrganizationSnapshot {kind:OrganizationKind;id:string;revision:number;name:string;isArchived:boolean;categoryId:string|null;projectId:string|null;position:number;taskIds:string[]|null;query?:SavedTaskQuery;isRemoved?:boolean;alias?:string}
export interface OrganizationEntry {command:OrganizationCommand;payloadHash:string;acceptedAtUtc:string;snapshot:OrganizationSnapshot}
export function normalizeProjectAlias(value:string):string {return value.trim().toLowerCase();}
export function projectAlias(value:unknown,canonical=false):string {
 if(typeof value!=='string'||value.length>256||!/^[a-zA-Z0-9-]*$/.test(value.trim()))throw new WorkspaceError('invalid_project_alias',422);
 const normalized=normalizeProjectAlias(value);
 if(!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(normalized)||normalized.length>64||canonical&&value!==normalized)throw new WorkspaceError('invalid_project_alias',422);
 return canonical?normalized:value;
}
export function retainedAliasClear(command:OrganizationCommand):boolean {return command.kind==='project'&&command.operation==='update'&&command.patch?.clearAlias===true&&Object.entries(command.patch).every(([key,value])=>key==='clearAlias'||value===null||value===false);}
const maxRevision=Number.MAX_SAFE_INTEGER;
const invalid=():never=>{throw new WorkspaceError('invalid_organization_history',422);};
function integer(value:unknown,min:number,max:number):number {if(!Number.isSafeInteger(value)||Number(value)<min||Number(value)>max)return invalid();return Number(value);}
function name(value:unknown):string {if(typeof value!=='string'||value.length>256||/^\p{White_Space}*$/u.test(value))return invalid();return value;}
const optionalId=(value:unknown)=>value===null||value===undefined?null:uuid(value);
function identifiers(value:unknown,max:number):string[]|null {if(value===undefined||value===null)return null;if(!Array.isArray(value)||value.length>max)return invalid();const result=value.map(uuid);if(new Set(result).size!==result.length)return invalid();return result;}
function choice<T extends string>(value:unknown,options:readonly T[]):T {if(typeof value!=='string'||!options.includes(value as T))return invalid();return value as T;}
export function projectRemoval(input:unknown):ProjectRemoval {
 const raw=workspaceObject(input,['taskIds','sectionIds']),taskIds=identifiers(raw.taskIds,100000),sectionIds=identifiers(raw.sectionIds,100000);
 if(taskIds===null||sectionIds===null||taskIds.length+sectionIds.length>100000)return invalid();
 return {taskIds,sectionIds};
}
export function projectRemovalFrom(document:WorkspaceDocument,projectId:string):ProjectRemoval {return {taskIds:document.tasks.filter(task=>task.projectId===projectId).map(task=>task.id),sectionIds:document.sections.filter(section=>section.projectId===projectId).map(section=>section.id)};}
export function removalMatches(document:WorkspaceDocument,projectId:string,approval:ProjectRemoval):boolean {const current=projectRemovalFrom(document,projectId);return JSON.stringify(approval.taskIds)===JSON.stringify(current.taskIds)&&JSON.stringify(approval.sectionIds)===JSON.stringify(current.sectionIds);}
export function organizationCommand(input:unknown):OrganizationCommand {
 const value=workspaceObject(input,['id','workspaceId','expectedRevision','kind','operation','entityId','patch']);
 const kind=choice(value.kind,['category','project','section','label','goal','savedTaskView'] as const),operation=choice(value.operation,['create','update','archive','remove'] as const);
 let patch:OrganizationPatch|null=null;
 if(value.patch!==undefined&&value.patch!==null){
  const raw=workspaceObject(value.patch,['name','categoryId','clearCategory','projectId','position','taskIds','query','removal','alias','clearAlias']);
  if(raw.clearAlias!==undefined&&typeof raw.clearAlias!=='boolean'||raw.clearCategory!==undefined&&typeof raw.clearCategory!=='boolean')return invalid();
  patch={name:raw.name==null?null:name(raw.name),categoryId:optionalId(raw.categoryId),clearCategory:raw.clearCategory===true,projectId:optionalId(raw.projectId),position:raw.position==null?null:integer(raw.position,0,2147483647),taskIds:identifiers(raw.taskIds,100),...(raw.query==null?{}:{query:savedTaskQuery(raw.query)}),...(raw.removal==null?{}:{removal:projectRemoval(raw.removal)}),...(raw.alias==null?{}:{alias:projectAlias(raw.alias)}),...(raw.clearAlias===true?{clearAlias:true}:{})};
  if(Object.values(patch).every(value=>value===null||value===false)||patch.clearCategory&&patch.categoryId!==null||patch.clearAlias&&patch.alias!==undefined)return invalid();
  if(kind!=='project'&&(patch.categoryId!==null||patch.clearCategory||patch.alias!==undefined||patch.clearAlias)||kind!=='section'&&(patch.projectId!==null||patch.position!==null)||kind!=='goal'&&patch.taskIds!==null||kind!=='savedTaskView'&&patch.query!==undefined)return invalid();
 }
 if(operation==='remove'){
  if(kind!=='project'||!patch?.removal||Object.entries(patch).some(([key,value])=>key!=='removal'&&value!==null&&value!==false))return invalid();
 }else if(patch?.removal!==undefined)return invalid();
 if(operation==='archive'?patch!==null:patch===null)return invalid();
 if(operation==='create'&&(patch!.name===null||patch!.clearCategory||patch!.clearAlias||kind==='savedTaskView'&&patch!.query===undefined)||kind==='section'&&operation!=='archive'&&(operation==='create'?patch!.projectId===null:patch!.projectId!==null))return invalid();
 return {id:uuid(value.id),workspaceId:uuid(value.workspaceId),expectedRevision:integer(value.expectedRevision,1,maxRevision-1),kind,operation,entityId:uuid(value.entityId),patch};
}
export async function organizationCommandHash(command:OrganizationCommand):Promise<string>{
 const patch=command.patch,fields:(string|null)[]=[command.id,command.workspaceId,String(command.expectedRevision),command.kind,command.operation,command.entityId,patch?'1':'0',patch?.name??null,patch?.categoryId??null,patch?.clearCategory?'1':'0',patch?.projectId??null,patch?.position==null?null:String(patch.position),patch?.taskIds==null?null:String(patch.taskIds.length),...(patch?.taskIds??[])];
 const extended=command.kind==='savedTaskView'||patch?.query!==undefined;if(extended){fields.push(patch?.query?'1':'0');if(patch?.query)fields.push(...savedTaskQueryFields(patch.query));}
 const removal=command.operation==='remove'||patch?.removal!==undefined;if(removal){fields.push(patch?.removal?'1':'0');if(patch?.removal)fields.push(String(patch.removal.taskIds.length),...patch.removal.taskIds,String(patch.removal.sectionIds.length),...patch.removal.sectionIds);}
 const alias=patch?.alias!==undefined||patch?.clearAlias===true;if(alias)fields.push(patch?.alias??null,patch?.clearAlias?'1':'0');
 const encoder=new TextEncoder(),payload=(alias?'companion-organization-command-v4\n':removal?'companion-organization-command-v3\n':extended?'companion-organization-command-v2\n':'companion-organization-command-v1\n')+fields.map(value=>value===null?'-1:':encoder.encode(value).length+':'+value).join('');
 return Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',encoder.encode(payload))),value=>value.toString(16).padStart(2,'0')).join('');
}
export function organizationSnapshot(input:unknown):OrganizationSnapshot {
 const value=workspaceObject(input,['kind','id','revision','name','isArchived','categoryId','projectId','position','taskIds','query','isRemoved','alias']);
 if(value.isArchived!==undefined&&typeof value.isArchived!=='boolean'||value.isRemoved!==undefined&&typeof value.isRemoved!=='boolean')return invalid();
 const snapshot={kind:choice(value.kind,['category','project','section','label','goal','savedTaskView'] as const),id:uuid(value.id),revision:integer(value.revision,1,maxRevision),name:name(value.name),isArchived:value.isArchived===true,categoryId:optionalId(value.categoryId),projectId:optionalId(value.projectId),position:value.position===undefined?0:integer(value.position,0,2147483647),taskIds:identifiers(value.taskIds,100000),...(value.query==null?{}:{query:savedTaskQuery(value.query)}),...(value.isRemoved===true?{isRemoved:true}:{}),...(value.alias==null?{}:{alias:projectAlias(value.alias,true)})};
 if(snapshot.alias!==undefined&&snapshot.kind!=='project'||snapshot.isRemoved===true&&(snapshot.kind!=='project'||!snapshot.isArchived)||snapshot.kind!=='project'&&snapshot.categoryId!==null||snapshot.kind!=='section'&&(snapshot.projectId!==null||snapshot.position!==0)||snapshot.kind==='section'&&snapshot.projectId===null||(snapshot.kind==='goal'?snapshot.taskIds===null:snapshot.taskIds!==null)||(snapshot.kind==='savedTaskView'?snapshot.query===undefined:snapshot.query!==undefined))return invalid();
 return snapshot;
}
export function currentOrganizationSnapshot(document:WorkspaceDocument,kind:OrganizationKind,id:string):OrganizationSnapshot|null {
 const rows=kind==='category'?document.categories:kind==='project'?document.projects:kind==='section'?document.sections:kind==='label'?document.labels:kind==='savedTaskView'?(document.savedTaskViews??[]):document.goals,row=rows.find(row=>row.id===id);
 return row?organizationSnapshot({kind,id:row.id,revision:row.revision,name:row.name,isArchived:row.isArchived,...('categoryId' in row?{categoryId:row.categoryId}:{}),...('projectId' in row?{projectId:row.projectId}:{}),...('position' in row?{position:row.position}:{}),...('taskIds' in row?{taskIds:row.taskIds}:{}),...('query' in row?{query:row.query}:{}),...('isRemoved' in row?{isRemoved:row.isRemoved}:{}),...('alias' in row?{alias:row.alias}:{})}):null;
}
export function applyOrganizationSnapshot(command:OrganizationCommand,current:OrganizationSnapshot|null):OrganizationSnapshot {
 const patch=command.patch,revision=command.expectedRevision+1;
 if(command.operation==='create')return {kind:command.kind,id:command.entityId,revision,name:patch!.name!,isArchived:false,categoryId:patch!.categoryId,projectId:patch!.projectId,position:patch!.position??0,taskIds:command.kind==='goal'?patch!.taskIds??[]:null,...(patch!.query===undefined?{}:{query:patch!.query}),...(patch!.alias===undefined?{}:{alias:normalizeProjectAlias(patch!.alias)})};
 if(!current)return invalid();
 if(command.operation==='archive')return {...current,revision,isArchived:true};
 if(command.operation==='remove')return {...current,revision,isArchived:true,isRemoved:true};
 return {...current,alias:patch!.clearAlias?undefined:patch!.alias===undefined?current.alias:normalizeProjectAlias(patch!.alias),revision,name:patch!.name??current.name,categoryId:patch!.clearCategory?null:patch!.categoryId??current.categoryId,position:patch!.position??current.position,taskIds:patch!.taskIds??current.taskIds,...(patch!.query===undefined?{}:{query:patch!.query})};
}
const equivalent=(left:OrganizationSnapshot,right:OrganizationSnapshot|null)=>right!==null&&(['kind','id','revision','name','isArchived','categoryId','projectId','position'] as const).every(key=>left[key]===right[key])&&left.alias===right.alias&&Boolean(left.isRemoved)===Boolean(right.isRemoved)&&JSON.stringify(left.taskIds)===JSON.stringify(right.taskIds)&&savedTaskQueriesEqual(left.query,right.query);
export function organizationHistory(input:unknown,document:WorkspaceDocument,utc:(value:unknown)=>string):OrganizationEntry[]{
 if(!Array.isArray(input)||input.length>100000)return invalid();
 const entries:OrganizationEntry[]=[],ids=new Set<string>(),latest=new Map<string,OrganizationSnapshot>();let previousRevision=0;
 const activities=[...document.completions,...document.habitScores,...(document.reviewEvents??[]),...(document.unarchives??[]),...(document.occurrenceCorrections??[]),...(document.restorations??[])],taskCommands=new Set(activities.map(row=>row.commandId)),activityRevisions=new Set(activities.map(row=>row.revision));
 const categories=new Set(document.categories.map(row=>row.id)),projects=new Set(document.projects.map(row=>row.id)),tasks=new Set(document.tasks.map(row=>row.id)),labels=new Set(document.labels.map(row=>row.id)),sectionOwners=new Map(document.sections.map(row=>[row.id,row.projectId]));
 for(const item of input){
  const row=workspaceObject(item,['command','payloadHash','acceptedAtUtc','snapshot']),command=organizationCommand(row.command),snapshot=organizationSnapshot(row.snapshot),patch=command.patch;
  if(typeof row.payloadHash!=='string'||!/^[a-f0-9]{64}$/.test(row.payloadHash)||command.workspaceId!==document.id||ids.has(command.id)||taskCommands.has(command.id)||snapshot.id!==command.entityId||snapshot.kind!==command.kind||snapshot.revision!==command.expectedRevision+1||snapshot.revision>document.revision||snapshot.revision<=previousRevision||activityRevisions.has(snapshot.revision))return invalid();
  if(Boolean(snapshot.isRemoved)!==(command.operation==='remove')&&!retainedAliasClear(command)||patch?.removal&&(patch.removal.taskIds.some(id=>!tasks.has(id))||patch.removal.sectionIds.some(id=>sectionOwners.get(id)!==command.entityId)))return invalid();
  const acceptedAtUtc=utc(row.acceptedAtUtc);ids.add(command.id);previousRevision=snapshot.revision;
  if(snapshot.categoryId!==null&&!categories.has(snapshot.categoryId)||snapshot.projectId!==null&&!projects.has(snapshot.projectId)||snapshot.taskIds?.some(id=>!tasks.has(id)))return invalid();
  const f=snapshot.query?.filter;if(f&&(f.categoryId&&!categories.has(f.categoryId)||f.projectId&&!projects.has(f.projectId)||f.labelId&&!labels.has(f.labelId)))return invalid();
  const prior=latest.get(snapshot.id);
  if(prior){if(prior.kind!==snapshot.kind||(prior.isRemoved||prior.isArchived&&command.operation!=='remove')&&!retainedAliasClear(command)||command.operation==='create'||!equivalent(applyOrganizationSnapshot(command,prior),snapshot))return invalid();}
  else if(command.operation==='create'){if(!equivalent(applyOrganizationSnapshot(command,null),snapshot))return invalid();}
  else if(command.operation==='archive'||command.operation==='remove'?!snapshot.isArchived:snapshot.isArchived&&!retainedAliasClear(command)||patch!.alias!==undefined&&normalizeProjectAlias(patch!.alias)!==snapshot.alias||patch!.clearAlias&&snapshot.alias!==undefined||patch!.name!==null&&patch!.name!==snapshot.name||patch!.categoryId!==null&&patch!.categoryId!==snapshot.categoryId||patch!.clearCategory&&snapshot.categoryId!==null||patch!.query!==undefined&&!savedTaskQueriesEqual(patch!.query,snapshot.query)||patch!.position!==null&&patch!.position!==snapshot.position||patch!.taskIds!==null&&JSON.stringify(patch!.taskIds)!==JSON.stringify(snapshot.taskIds))return invalid();
  if(snapshot.alias!==undefined&&[...latest.values()].some(row=>row.id!==snapshot.id&&row.alias===snapshot.alias))return invalid();
  latest.set(snapshot.id,snapshot);entries.push({command,payloadHash:row.payloadHash,acceptedAtUtc,snapshot});
 }
 for(const snapshot of latest.values())if(!equivalent(snapshot,currentOrganizationSnapshot(document,snapshot.kind,snapshot.id)))return invalid();
 return entries;
}

export async function validateOrganizationHistoryHashes(document:WorkspaceDocument):Promise<void> {
 for(const entry of document.organizationHistory??[])if(entry.payloadHash!==await organizationCommandHash(entry.command))invalid();
}
