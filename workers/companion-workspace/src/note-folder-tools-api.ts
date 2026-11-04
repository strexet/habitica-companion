import {folderCommand,folderHash,applyFolder} from '../../shared/notes-folders.ts';
import type {NoteFolderReceipt,NoteFolderCommand} from '../../shared/notes.ts';
import {uuid,workspaceTokenHash,WorkspaceError} from '../../shared/workspace-http.ts';
import {loadNotes,notesAgentContext,notesOperation,projectAuthority,requireProjectRead,type NotesRow} from './notes-api.ts';
import {submitFolder} from './notes-folders-api.ts';
interface Prepared {request_id:string;command_id:string;notebook_id:string;folder_id:string;access_epoch:string;grant_epoch:string;input_hash:string;command_json:string|null;expires_at_utc:string}
export async function noteFolderToolsApi(request:Request,env:WorkspaceEnv,name:string,input:Record<string,unknown>):Promise<Record<string,unknown>>{
 const write=['prepare_note_folder_command','accept_note_folder_command'].includes(name),{row,who}=await notesAgentContext(request,env,write,name==='get_note_folder_command');
 const context={notebookId:row.notebook_id,ownerScope:row.owner_scope,untrustedContent:true};
 if(name==='search_note_folders')return {...await(await notesOperation(env,row,'folders',input,who)).json<Record<string,unknown>>(),untrustedContent:true};
 if(name==='get_note_folder')return {...await(await notesOperation(env,row,'folder',input,who)).json<Record<string,unknown>>(),untrustedContent:true};
 if(input.notebookId!==row.notebook_id)throw new WorkspaceError('notebook_changed',409);
 const lookup=(field:'request_id'|'command_id',id:string)=>env.WORKSPACE_DB.prepare(`SELECT * FROM note_folder_preparations WHERE profile_id=? AND ${field}=?`).bind(row.profile_id,id).first<Prepared>();
 const receipt=async(id:string)=>{
  const found=await env.WORKSPACE_DB.prepare(`SELECT r.receipt_json FROM note_folder_receipts r JOIN notes_notebooks n ON n.profile_id=r.profile_id WHERE r.profile_id=? AND r.command_id=? AND n.notebook_id=? AND n.access_epoch=? AND n.active=1`).bind(row.profile_id,id,row.notebook_id,row.access_epoch).first<{receipt_json:string}>();
  if(!found)return null;const original=JSON.parse(found.receipt_json) as NoteFolderReceipt;const book=await loadNotes(env,row),folder=book.folders?.find(f=>f.id===original.folderId);
  return {...context,commandId:id,receipt:original,saved:true,outcomeUnknown:false,currentFolder:folder??null,metadataAtAcceptedRevision:folder?.revision===original.folderRevision};
 };
 if(name==='get_note_folder_command')return await receipt(uuid(input.commandId))??{...context,commandId:input.commandId,saved:null,outcomeUnknown:true};
 if(typeof who.grantEpoch!=='string')throw new WorkspaceError('unauthorized',401);
 if(name==='prepare_note_folder_command'){
  const requestId=uuid(input.requestId),hash=await workspaceTokenHash(JSON.stringify(input));let prepared=await lookup('request_id',requestId);
  if(prepared&&prepared.input_hash!==hash)throw new WorkspaceError('request_id_reused',409);
  if(prepared){const accepted=await receipt(prepared.command_id);if(accepted)return {...accepted,preparedCommandId:prepared.command_id,requestId,alreadyAccepted:true};}
  if(!prepared){
   const command=folderCommand({id:crypto.randomUUID(),folderId:input.operation==='create'?crypto.randomUUID():input.folderId,operation:input.operation,expectedNotebookRevision:input.expectedNotebookRevision,expectedFolderRevision:input.expectedFolderRevision,ownerScope:row.owner_scope,source:'cloud',name:input.name,...(input.projectId?{project:{projectId:input.projectId,workspaceId:input.projectWorkspaceId,ownerScope:row.owner_scope,source:'cloud'}}:{})});
   if(command.project)requireProjectRead(who);const authority=command.project?await projectAuthority(env,row,command.project):null;
   await applyFolder(await loadNotes(env,row),command,new Date().toISOString(),authority?.authority);
   const now=new Date().toISOString(),expires=new Date(Date.now()+600000).toISOString();
   await env.WORKSPACE_DB.batch([
    env.WORKSPACE_DB.prepare('UPDATE note_folder_preparations SET command_json=NULL WHERE profile_id=? AND (expires_at_utc<=? OR grant_epoch<>? OR access_epoch<>?)').bind(row.profile_id,now,who.grantEpoch,row.access_epoch),
    env.WORKSPACE_DB.prepare('UPDATE notes_preparations SET command_json=NULL WHERE profile_id=? AND (expires_at_utc<=? OR grant_epoch<>? OR access_epoch<>?)').bind(row.profile_id,now,who.grantEpoch,row.access_epoch),
    env.WORKSPACE_DB.prepare(`INSERT INTO note_folder_preparations(profile_id,request_id,command_id,notebook_id,folder_id,access_epoch,grant_epoch,input_hash,command_json,created_at_utc,expires_at_utc)
     SELECT profile_id,?,?,?,?,?,?,?,?,?,? FROM notes_notebooks WHERE profile_id=? AND notebook_id=? AND access_epoch=? AND active=1 AND revision=?
     AND (SELECT count(*) FROM note_folder_preparations WHERE profile_id=?)+(SELECT count(*) FROM notes_preparations WHERE profile_id=?)<8192
     AND (SELECT count(*) FROM note_folder_preparations WHERE profile_id=? AND command_json IS NOT NULL)+(SELECT count(*) FROM notes_preparations WHERE profile_id=? AND command_json IS NOT NULL)<8 ON CONFLICT(profile_id,request_id) DO NOTHING`)
     .bind(requestId,command.id,row.notebook_id,command.folderId,row.access_epoch,who.grantEpoch,hash,JSON.stringify(command),now,expires,row.profile_id,row.notebook_id,row.access_epoch,command.expectedNotebookRevision,row.profile_id,row.profile_id,row.profile_id,row.profile_id)
   ]);
   prepared=await lookup('request_id',requestId);if(!prepared)throw new WorkspaceError('preview_capacity_or_revision_changed',409);if(prepared.input_hash!==hash)throw new WorkspaceError('request_id_reused',409);
  }
  if(prepared.access_epoch!==row.access_epoch||prepared.grant_epoch!==who.grantEpoch||!prepared.command_json||prepared.expires_at_utc<=new Date().toISOString())throw new WorkspaceError('preview_expired_or_revoked',409);
  const command=folderCommand(JSON.parse(prepared.command_json));if(command.project)requireProjectRead(who);const authority=command.project?await projectAuthority(env,row,command.project):null,before=await loadNotes(env,row),after=await applyFolder(before,command,new Date().toISOString(),authority?.authority);
  const old=before.folders?.find(f=>f.id===command.folderId)??null,next=after.book.folders!.find(f=>f.id===command.folderId)!;
  return {...context,preparedCommandId:prepared.command_id,requestId,expiresAtUtc:prepared.expires_at_utc,command,payloadHash:await folderHash(command),before:old,after:next,change:{operation:command.operation,nameBefore:old?.name??null,nameAfter:next.name,projectBefore:old?.project??null,projectAfter:next.project??null,removesEmptyFolder:command.operation==='remove',purgesNotes:false,scopeImmutable:true},saved:false,outcomeUnknown:false};
 }
 const commandId=uuid(input.commandId),accepted=await receipt(commandId);if(accepted)return accepted;
 const prepared=await lookup('command_id',commandId);
 if(!prepared||prepared.notebook_id!==row.notebook_id||prepared.access_epoch!==row.access_epoch||prepared.grant_epoch!==who.grantEpoch||!prepared.command_json||prepared.expires_at_utc<=new Date().toISOString())throw new WorkspaceError('preview_expired_or_revoked',409);
 const command=folderCommand(JSON.parse(prepared.command_json));if(command.project)requireProjectRead(who);
 const response=await submitFolder(env,row,command,{grantEpoch:who.grantEpoch,kind:'folder'},who);
 return {...await response.json<Record<string,unknown>>(),commandId,untrustedContent:true,outcomeUnknown:false};
}
