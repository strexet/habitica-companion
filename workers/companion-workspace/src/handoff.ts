import { journal } from './journal.ts';
import { cloudAuthority, type WorkspaceRow } from './executor.ts';
import { workspaceJson, workspaceObject, workspaceTokenHash, uuid, WorkspaceError } from '../../shared/workspace-http.ts';
const headers={'cache-control':'no-store','x-content-type-options':'nosniff'};
const bytes=(v:string)=>new TextEncoder().encode(v).length;
export async function handoff(request:Request,env:WorkspaceEnv,row:WorkspaceRow):Promise<Response>{
 const body=workspaceObject(await workspaceJson(request,16*1024*1024),['consentVersion','transferId','archive']);
 if(body.consentVersion!=='workspace-handoff-v1')throw new WorkspaceError('handoff_consent_required',403);
 const transferId=uuid(body.transferId);if(typeof body.archive!=='string')throw new WorkspaceError('invalid_archive',422);
 const hash=await workspaceTokenHash(body.archive);
 const prior=await env.WORKSPACE_DB.prepare('SELECT h.archive_hash,h.workspace_id FROM workspace_handoffs h JOIN workspaces w ON w.profile_id=h.profile_id WHERE h.profile_id=? AND h.transfer_id=? AND w.access_epoch=? AND w.active=1').bind(row.profile_id,transferId,row.access_epoch).first<{archive_hash:string;workspace_id:string}>();
 if(prior){if(prior.archive_hash!==hash||prior.workspace_id!==row.workspace_id)throw new WorkspaceError('transfer_id_reused',409);return Response.json({accepted:true,transferId,workspaceId:prior.workspace_id,executorEnabled:Boolean(row.executor_enabled)},{headers});}
 const incoming=await journal(await workspaceJson(new Request('https://archive.internal/',{method:'POST',body:body.archive}),16*1024*1024));
 if(incoming.authority?.mode!=='frozen'||incoming.authority.transferId!==transferId||incoming.commands.some(e=>['leased','executing'].includes(e.projectionState)))throw new WorkspaceError('ownership_not_frozen',409);
 // A copied uncertain request cannot acquire a fresh execution identity on the server.
 const documentJson=JSON.stringify(incoming.document);const authorityJson=JSON.stringify({...cloudAuthority(incoming.document.id),transferId});
 const size=bytes(documentJson)+bytes(authorityJson)+512+incoming.commands.reduce((n,e)=>n+bytes(JSON.stringify(e))+1,0)+incoming.events.reduce((n,e)=>n+bytes(JSON.stringify(e))+1,0);
 if(bytes(documentJson)>1024*1024||size>8*1024*1024||incoming.storeVersion>=Number.MAX_SAFE_INTEGER)throw new WorkspaceError('workspace_capacity',413);
 const target=JSON.parse(row.document_json);if(row.executor_enabled||row.store_version!==1||Object.values(target).some(v=>Array.isArray(v)&&v.length>0))throw new WorkspaceError('cloud_workspace_not_empty',409);
 const version=Math.max(incoming.storeVersion,incoming.commands.length,incoming.events.length)+1;if(!Number.isSafeInteger(version))throw new WorkspaceError('revision_exhausted',409);
 const collision=await env.WORKSPACE_DB.prepare('SELECT profile_id FROM workspaces WHERE workspace_id=? AND profile_id<>?').bind(incoming.document.id,row.profile_id).first();if(collision)throw new WorkspaceError('workspace_identity_owned',409);
 const tag=crypto.randomUUID();const time=new Date().toISOString();
 const statements=[env.WORKSPACE_DB.prepare(`UPDATE workspaces SET workspace_id=?,document_json=?,authority_json=?,store_version=?,archive_bytes=?,commit_tag=?,updated_at_utc=?
 WHERE profile_id=? AND access_epoch=? AND store_version=1 AND executor_enabled=0 AND active=1
 AND NOT EXISTS(SELECT 1 FROM workspace_commands WHERE profile_id=?)
 AND NOT EXISTS(SELECT 1 FROM workspace_runtime WHERE profile_id=? AND lease_until_utc>?)`).bind(incoming.document.id,documentJson,authorityJson,version,size,tag,time,row.profile_id,row.access_epoch,row.profile_id,row.profile_id,time)];
 // Keep each D1 parameter bounded while retaining a single atomic transaction.
 function append(table:'workspace_commands'|'workspace_events',values:unknown[]){
  let chunk:{sequence:number;value:unknown}[]=[];let length=0;
  function flush(){if(!chunk.length)return;const data=JSON.stringify(chunk);
   statements.push(env.WORKSPACE_DB.prepare(table==='workspace_commands'?`INSERT INTO workspace_commands(profile_id,command_id,sequence,payload_hash,entry_json)
    SELECT w.profile_id,json_extract(j.value,'$.value.command.id'),json_extract(j.value,'$.sequence'),json_extract(j.value,'$.value.payloadHash'),json_extract(j.value,'$.value') FROM workspaces w,json_each(?) j WHERE w.profile_id=? AND w.commit_tag=?`:
    `INSERT INTO workspace_events(profile_id,event_id,sequence,event_json) SELECT w.profile_id,json_extract(j.value,'$.value.id'),json_extract(j.value,'$.sequence'),json_extract(j.value,'$.value') FROM workspaces w,json_each(?) j WHERE w.profile_id=? AND w.commit_tag=?`).bind(data,row.profile_id,tag));chunk=[];length=0;}
  values.forEach((value,i)=>{const n=bytes(JSON.stringify(value))+64;if(length+n>512*1024)flush();chunk.push({sequence:i+1,value});length+=n;});flush();
 }
 append('workspace_commands',incoming.commands);append('workspace_events',incoming.events);
 statements.push(env.WORKSPACE_DB.prepare('INSERT INTO workspace_handoffs(profile_id,transfer_id,archive_hash,workspace_id,accepted_at_utc) SELECT profile_id,?,?,workspace_id,? FROM workspaces WHERE profile_id=? AND commit_tag=?').bind(transferId,hash,time,row.profile_id,tag));
 const result=await env.WORKSPACE_DB.batch(statements);if(!result[0].meta.changes)throw new WorkspaceError('handoff_conflict',409);
 return Response.json({accepted:true,transferId,workspaceId:incoming.document.id,executorEnabled:false},{status:201,headers});
}
