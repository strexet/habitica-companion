import {workspaceObject,uuid,workspaceTokenHash,WorkspaceError} from './workspace-http.ts';
import {remoteTask} from './projection.ts';
export const tagConsentVersion='habitica-tags-v1';
export const tagWriteScope='habitica.tags.write';
export interface NativeTag {id:string;name:string;native:boolean}
export interface TagGatewayContext {workspaceId:string;entityId:string;kind:'category'|'project'|'section'|'label'|'goal'|'savedTaskView'}
export function tagContext(value:unknown):TagGatewayContext {
 const row=workspaceObject(value,['workspaceId','entityId','kind']);
 if(typeof row.kind!=='string'||!['category','project','section','label','goal','savedTaskView'].includes(row.kind))throw new WorkspaceError('invalid_tag_context',422);
 return {workspaceId:uuid(row.workspaceId),entityId:uuid(row.entityId),kind:row.kind as TagGatewayContext['kind']};
}
export function tagName(value:unknown):string{if(typeof value!=='string'||/^\p{White_Space}*$/u.test(value)||value.length>256)throw new WorkspaceError('invalid_tag_name',422);return value;}
export function nativeTags(input:unknown):NativeTag[]{
 if(!Array.isArray(input)||input.length>10000)throw new WorkspaceError('invalid_upstream_tags',502);
 const ids=new Set<string>();return input.map(value=>{const row=workspaceObject(value),id=uuid(row.id);if(ids.has(id)||row.challenge!==undefined&&typeof row.challenge!=='boolean'||row.group!==undefined&&row.group!==null&&typeof row.group!=='string')throw new WorkspaceError('invalid_upstream_tags',502);ids.add(id);return {id,name:tagName(row.name),native:row.challenge!==true&&!row.group};});
}
export async function tagsFingerprint(tags:NativeTag[]):Promise<string>{return workspaceTokenHash(JSON.stringify([...tags].sort((a,b)=>a.id.localeCompare(b.id))));}
export async function taggedTask(input:unknown,userId:string){
 const task=await remoteTask(input,userId),raw=workspaceObject(input);if(!Array.isArray(raw.tags)||raw.tags.length>10000)throw new WorkspaceError('invalid_upstream_tags',502);
 const tags=raw.tags.map(uuid);if(new Set(tags).size!==tags.length)throw new WorkspaceError('invalid_upstream_tags',502);
 return {task,tags,fingerprint:await workspaceTokenHash(JSON.stringify({id:task.id,kind:task.kind,taskFingerprint:task.fingerprint,tags:[...tags].sort()}))};
}
// Stable field order for new tag attempts only. Legacy task attempt identity is unchanged.
export async function tagAttemptHash(operation:string,args:unknown,at:unknown):Promise<string>{
 const stable=(value:unknown):unknown=>Array.isArray(value)?value.map(stable):value&&typeof value==='object'?Object.fromEntries(Object.entries(value).sort(([a],[b])=>a.localeCompare(b)).map(([key,item])=>[key,stable(item)])):value;
 return workspaceTokenHash('companion-tag-attempt-v1\n'+JSON.stringify(stable({operation,arguments:args,attemptAtUtc:at})));
}
