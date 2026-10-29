import {applyNote,noteCommand,noteHash,type NoteCommand,type NoteReceipt,type NoteVersion} from '../../shared/notes';
import {noteToolInput,type NoteTool} from '../../shared/note-tools';
import {uuid,workspaceTokenHash,WorkspaceError} from '../../shared/workspace-http';
import {loadNotes,notesAgentContext,notesOperation,submitNote,projectAuthority,requireProjectRead,type NotesRow} from './notes-api';
interface Preparation {request_id:string;command_id:string;notebook_id:string;note_id:string;access_epoch:string;grant_epoch:string;input_hash:string;command_json:string|null;created_at_utc:string;expires_at_utc:string}
async function original(env:WorkspaceEnv,row:NotesRow,id:string):Promise<NoteReceipt|null>{
 const found=await env.WORKSPACE_DB.prepare(`SELECT r.receipt_json FROM personal_note_receipts r JOIN notes_notebooks n ON n.profile_id=r.profile_id WHERE r.profile_id=? AND r.command_id=? AND n.notebook_id=? AND n.access_epoch=? AND n.active=1`).bind(row.profile_id,id,row.notebook_id,row.access_epoch).first<{receipt_json:string}>();return found?JSON.parse(found.receipt_json):null;
}
function changedLines(before:string,after:string){
 const a=before.split('\n'),b=after.split('\n');let start=0,end=0;
 while(start<a.length&&start<b.length&&a[start]===b[start])start++;
 while(end<a.length-start&&end<b.length-start&&a[a.length-end-1]===b[b.length-end-1])end++;
 return {firstChangedLine:start+1,removedLines:a.slice(start,a.length-end),addedLines:b.slice(start,b.length-end),rangeKind:'contiguous-replacement',truncated:false};
}
async function preview(before:NoteVersion|null,after:NoteVersion|null,command:NoteCommand){
 return {operation:command.operation,titleBefore:before?.title??null,titleAfter:after?.title??null,markdownBeforeHash:await workspaceTokenHash(before?.markdown??''),markdownAfterHash:after?await workspaceTokenHash(after.markdown):null,
  markdownChange:changedLines(before?.markdown??'',after?.markdown??''),trashBefore:before?.isTrashed??false,trashAfter:after?.isTrashed??null,purgesRetainedVersions:command.operation==='purge',...((before?.project||after?.project||command.clearProject)?{projectBefore:before?.project??null,projectAfter:after?.project??null}:{})};
}
async function noteProjectContext(env:WorkspaceEnv,row:NotesRow,link:NoteVersion['project'],canRead:boolean){
 if(!link)return {state:'unassigned',name:null};
 if(!canRead||link.source!=='cloud'||link.ownerScope!==row.owner_scope)return {state:'unavailable',name:null};
 const w=await env.WORKSPACE_DB.prepare('SELECT workspace_id,document_json FROM workspaces WHERE profile_id=? AND connection_id=? AND active=1').bind(row.profile_id,row.connection_id).first<{workspace_id:string;document_json:string}>();
 if(!w||w.workspace_id!==link.workspaceId)return {state:'unavailable',name:null};
 const p=JSON.parse(w.document_json).projects?.find((p:{id:string})=>p.id===link.projectId);if(!p)return {state:'missing',name:null};
 return {state:p.isRemoved?'removed':p.isArchived?'archived':'active',name:typeof p.name==='string'&&p.name.length<=256?p.name:null};
}
export async function noteToolsApi(request:Request,env:WorkspaceEnv,name:NoteTool,value:unknown):Promise<Record<string,unknown>>{
 const input=noteToolInput(name,value),write=name==='prepare_note_command'||name==='accept_note_command';
 const {row,who}=await notesAgentContext(request,env,write,name==='get_note_command');
 const context={notebookId:row.notebook_id,ownerScope:row.owner_scope,untrustedContent:true};
 if(name==='search_notes')return {...await (await notesOperation(env,row,'search',input)).json<Record<string,unknown>>(),untrustedContent:true};
 if(name==='get_note'){
  const book=await loadNotes(env,row),note=book.notes.find(note=>note.id===input.noteId);if(!note)throw new WorkspaceError('note_unavailable',404);
  const version=input.noteRevision===undefined?note.current:[...note.history,note.current].find(version=>version.revision===input.noteRevision);if(!version)throw new WorkspaceError('note_revision_unavailable',404);
  return {...context,projectContext:await noteProjectContext(env,row,version.project,who.scopes?.includes('tasks:read')===true),revision:book.revision,note:{id:note.id,...version,createdAtUtc:note.createdAtUtc},contentHash:await workspaceTokenHash(version.markdown),currentNoteRevision:note.current.revision,retainedVersions:note.history.map(version=>version.revision),historyTruncated:note.current.revision>17};
 }
 if(input.notebookId!==row.notebook_id)throw new WorkspaceError('notebook_changed',409);
 const lookup=async(field:'request_id'|'command_id',id:string)=>env.WORKSPACE_DB.prepare(`SELECT * FROM notes_preparations WHERE profile_id=? AND ${field}=?`).bind(row.profile_id,id).first<Preparation>();
 const receipt=async(id:string)=>{const found=await original(env,row,id);return found?{...context,commandId:id,receipt:found,saved:true,outcomeUnknown:false}:null;};
 if(name==='get_note_command')return await receipt(uuid(input.commandId))??{...context,commandId:input.commandId,saved:null,outcomeUnknown:true};
 if(typeof who.grantEpoch!=='string')throw new WorkspaceError('unauthorized',401);
 if(name==='prepare_note_command'){
  const requestId=uuid(input.requestId),hash=await workspaceTokenHash(JSON.stringify(input));let prepared=await lookup('request_id',requestId);
  if(prepared?.input_hash!==undefined&&prepared.input_hash!==hash)throw new WorkspaceError('request_id_reused',409);
  if(prepared){const accepted=await receipt(prepared.command_id);if(accepted)return {...accepted,preparedCommandId:prepared.command_id,requestId,alreadyAccepted:true};}
  if(!prepared){
   const command=noteCommand({id:crypto.randomUUID(),noteId:input.operation==='create'?crypto.randomUUID():input.noteId,operation:input.operation,expectedNotebookRevision:input.expectedNotebookRevision,expectedNoteRevision:input.expectedNoteRevision,title:input.title,markdown:input.markdown,...(input.projectId?{project:{projectId:input.projectId,workspaceId:input.projectWorkspaceId,source:'cloud',ownerScope:row.owner_scope}}:{}),...(input.clearProject?{clearProject:true}:{})});
   if(command.project)requireProjectRead(who);const authority=command.project?await projectAuthority(env,row,command.project):null;
   await applyNote(await loadNotes(env,row),command,new Date().toISOString(),authority?.authority);
   const now=new Date().toISOString(),expires=new Date(Date.now()+600000).toISOString();
   await env.WORKSPACE_DB.batch([
    env.WORKSPACE_DB.prepare('UPDATE notes_preparations SET command_json=NULL WHERE profile_id=? AND (expires_at_utc<=? OR grant_epoch<>? OR access_epoch<>?)').bind(row.profile_id,now,who.grantEpoch,row.access_epoch),
    env.WORKSPACE_DB.prepare(`INSERT INTO notes_preparations(profile_id,request_id,command_id,notebook_id,note_id,access_epoch,grant_epoch,input_hash,command_json,created_at_utc,expires_at_utc)
     SELECT profile_id,?,?,?,?,?,?,?,?,?,? FROM notes_notebooks WHERE profile_id=? AND notebook_id=? AND access_epoch=? AND active=1 AND revision=?
     AND (SELECT count(*) FROM notes_preparations WHERE profile_id=?)<8192 AND (SELECT count(*) FROM notes_preparations WHERE profile_id=? AND command_json IS NOT NULL)<8 ON CONFLICT(profile_id,request_id) DO NOTHING`)
     .bind(requestId,command.id,row.notebook_id,command.noteId,row.access_epoch,who.grantEpoch,hash,JSON.stringify(command),now,expires,row.profile_id,row.notebook_id,row.access_epoch,command.expectedNotebookRevision,row.profile_id,row.profile_id)
   ]);
   prepared=await lookup('request_id',requestId);if(!prepared)throw new WorkspaceError('preview_capacity_or_revision_changed',409);
   if(prepared.input_hash!==hash)throw new WorkspaceError('request_id_reused',409);
  }
  if(prepared.access_epoch!==row.access_epoch||prepared.grant_epoch!==who.grantEpoch||!prepared.command_json||prepared.expires_at_utc<=new Date().toISOString())throw new WorkspaceError('preview_expired_or_revoked',409);
  const command=noteCommand(JSON.parse(prepared.command_json));if(command.project)requireProjectRead(who);const authority=command.project?await projectAuthority(env,row,command.project):null;const before=await loadNotes(env,row),after=await applyNote(before,command,new Date().toISOString(),authority?.authority);
  const old=before.notes.find(note=>note.id===command.noteId)?.current??null,next=after.book.notes.find(note=>note.id===command.noteId)?.current??null;
  return {...context,preparedCommandId:prepared.command_id,requestId,expiresAtUtc:prepared.expires_at_utc,command,payloadHash:await noteHash(command),before:old,after:next,change:await preview(old,next,command),saved:false,outcomeUnknown:false};
 }
 const commandId=uuid(input.commandId),accepted=await receipt(commandId);if(accepted)return accepted;
 const prepared=await lookup('command_id',commandId);
 if(!prepared||prepared.notebook_id!==row.notebook_id||prepared.access_epoch!==row.access_epoch||prepared.grant_epoch!==who.grantEpoch||!prepared.command_json||prepared.expires_at_utc<=new Date().toISOString())throw new WorkspaceError('preview_expired_or_revoked',409);
 const command=noteCommand(JSON.parse(prepared.command_json));if(command.project)requireProjectRead(who);
 const saved=await submitNote(env,row,command,{grantEpoch:who.grantEpoch});
 return {...await saved.json<Record<string,unknown>>(),commandId,untrustedContent:true,outcomeUnknown:false};
}
export async function purgeExpiredNotesPreviews(env:WorkspaceEnv):Promise<void>{await env.WORKSPACE_DB.prepare('UPDATE notes_preparations SET command_json=NULL WHERE rowid IN (SELECT rowid FROM notes_preparations WHERE command_json IS NOT NULL AND expires_at_utc<=? ORDER BY expires_at_utc,rowid LIMIT 128)').bind(new Date().toISOString()).run();}
