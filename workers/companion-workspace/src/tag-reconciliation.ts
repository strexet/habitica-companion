import {workspaceTokenHash,WorkspaceError} from '../../shared/workspace-http.ts';
import {desiredTagMembership} from '../../shared/tag-membership.ts';
import {workspaceDocument,type WorkspaceDocument} from '../../shared/workspace-domain.ts';
import {tagAuthority,type TagOrigin,type TagAuthority} from './tag-authority.ts';
import {planTags,type PlannedTags} from './tag-planner.ts';
import type {WorkspaceRow} from './executor.ts';
export const browserTagOrigin:TagOrigin={source:'browser',tagWriteAllowed:true};
// A canonical save remains possible during an optional metadata-service outage.
export async function reconcileTags(env:WorkspaceEnv,row:WorkspaceRow,before:WorkspaceDocument,after:WorkspaceDocument,commandId:string,origin:TagOrigin,approved?:TagAuthority|null):Promise<PlannedTags>{
 before=workspaceDocument(before);after=workspaceDocument(after);
 if(JSON.stringify(desiredTagMembership(before).groups)===JSON.stringify(desiredTagMembership(after).groups))return {document:after,work:[],gap:null};
 try{let authority=await tagAuthority(env,row,origin);
  if(approved!==undefined){
   const {ledgerFingerprint,...reviewed}=approved as TagAuthority&{ledgerFingerprint?:string}??{};
   if(!approved||ledgerFingerprint!==await workspaceTokenHash(JSON.stringify(before.tagLedger??null))||JSON.stringify(reviewed)!==JSON.stringify(authority))return {document:after,work:[],gap:'tag_preview_changed'};
  }
  return await planTags(after,authority,commandId,before);}catch{return {document:after,work:[],gap:'tag_authority_unavailable'};}
}
// Every statement is guarded by the same canonical acceptance commit. No portable ledger grants work.
export function tagStatements(env:WorkspaceEnv,row:WorkspaceRow,plan:PlannedTags,commitTag:string):D1PreparedStatement[]{
 const now=new Date().toISOString();return [
  env.WORKSPACE_DB.prepare(`INSERT INTO workspace_tag_status(profile_id,source_revision,gap) SELECT profile_id,?,? FROM workspaces WHERE profile_id=? AND commit_tag=? ON CONFLICT(profile_id) DO UPDATE SET source_revision=excluded.source_revision,gap=COALESCE(excluded.gap,workspace_tag_status.gap)`).bind(plan.document.revision,plan.gap,row.profile_id,commitTag),
  ...plan.work.map(work=>env.WORKSPACE_DB.prepare(`INSERT INTO workspace_tag_work(profile_id,work_id,workspace_id,connection_id,access_epoch,command_id,source_revision,intent_id,desired_hash,work_json,state,error_code,created_at_utc,updated_at_utc) SELECT profile_id,?,workspace_id,connection_id,access_epoch,?,?,?,?,?,?,?,?,? FROM workspaces WHERE profile_id=? AND commit_tag=?`).bind(work.id,work.commandId,work.sourceRevision,work.intentId,work.desiredHash,JSON.stringify(work),work.state,work.error,now,now,row.profile_id,commitTag))
 ];
}
export async function tagStatus(env:Pick<WorkspaceEnv,'WORKSPACE_DB'>,row:WorkspaceRow){
 const results=await env.WORKSPACE_DB.batch([
  env.WORKSPACE_DB.prepare("SELECT workspace_id,json_extract(document_json,'$.revision') AS revision FROM workspaces WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1").bind(row.profile_id,row.connection_id,row.access_epoch),
  env.WORKSPACE_DB.prepare('SELECT source_revision,gap FROM workspace_tag_status WHERE profile_id=?').bind(row.profile_id),
  env.WORKSPACE_DB.prepare('SELECT state,COUNT(*) AS count FROM workspace_tag_work WHERE profile_id=? GROUP BY state').bind(row.profile_id),
  env.WORKSPACE_DB.prepare('SELECT work_id,intent_id,command_id,source_revision,state,error_code,retry_at_utc FROM workspace_tag_work WHERE profile_id=? ORDER BY created_at_utc,work_id LIMIT 101').bind(row.profile_id)
 ]);
 const current=results[0].results[0] as {workspace_id:string;revision:number}|undefined;if(!current)throw new WorkspaceError('unauthorized',401);
 return {workspaceId:current.workspace_id,revision:current.revision,status:results[1].results[0]??null,counts:results[2].results,work:results[3].results.slice(0,100),workLimit:100,workTruncated:results[3].results.length>100};
}

export async function previewTagAuthority(env:WorkspaceEnv,row:WorkspaceRow,origin:TagOrigin):Promise<string|null>{try{return JSON.stringify({...await tagAuthority(env,row,origin),ledgerFingerprint:await workspaceTokenHash(JSON.stringify(JSON.parse(row.document_json).tagLedger??null))});}catch{return null;}}
export const tagWorkBytes=(plan:PlannedTags)=>plan.work.reduce((sum,work)=>sum+new TextEncoder().encode(JSON.stringify(work)).length+512,0);

// Trusted game-mapping acknowledgements may change a view's matching set. They do not grant tag writes.
export function tagAcknowledgement(env:Pick<WorkspaceEnv,'WORKSPACE_DB'>,row:WorkspaceRow,before:WorkspaceDocument,after:WorkspaceDocument,commitTag:string):D1PreparedStatement[]{
 before=workspaceDocument(before);after=workspaceDocument(after);
 if(JSON.stringify(desiredTagMembership(before).groups)===JSON.stringify(desiredTagMembership(after).groups))return [];
 return [env.WORKSPACE_DB.prepare(`INSERT INTO workspace_tag_status(profile_id,source_revision,gap) SELECT profile_id,?,? FROM workspaces WHERE profile_id=? AND commit_tag=? ON CONFLICT(profile_id) DO UPDATE SET source_revision=excluded.source_revision,gap=excluded.gap`).bind(after.revision,'tag_membership_changed_after_projection',row.profile_id,commitTag)];
}

export function tagPreview(before:WorkspaceDocument,after:WorkspaceDocument,authorityJson:string|null){
 before=workspaceDocument(before);after=workspaceDocument(after);
 const previous=new Map(desiredTagMembership(before).groups.map(group=>[group.entityId,group]));
 const authority=authorityJson?JSON.parse(authorityJson) as TagAuthority:null;
 return {separateConsentRequired:true,scopeAllowed:authority?.scopeAllowed===true,consentEnabled:authority?.consentEnabled===true,
  groups:desiredTagMembership(after).groups.filter(group=>JSON.stringify(group)!==JSON.stringify(previous.get(group.entityId))).map(group=>{
   const mapping=before.tagLedger?.mappings.find(row=>row.entityId===group.entityId),members=new Set(group.taskIds);
   const removedTaskIds=[...new Set((before.tagLedger?.intents??[]).filter(intent=>intent.mappingId===mapping?.id&&intent.operation==='setMembership').flatMap(intent=>(intent.members??[]).filter(member=>member.state==='confirmed'&&member.present&&!members.has(member.taskId)).map(member=>member.taskId)))];
   return {...group,removedTaskIds};
  })};
}
