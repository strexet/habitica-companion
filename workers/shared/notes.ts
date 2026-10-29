import {uuid,workspaceObject,workspaceTokenHash,WorkspaceError} from './workspace-http.ts';

export const notesConsentVersion='notes-v1';
export const notesReadScope='notes.read',notesWriteScope='notes.write';
export const notesLimits={bodyBytes:128*1024,history:16,notes:200,receipts:2048,bytes:16*1024*1024};
export interface NoteProjectLink {projectId:string;workspaceId:string;source:'browser'|'cloud';ownerScope:string}
export interface NoteProjectAuthority {ownerScope:string;source:'browser'|'cloud';workspaceId:string;projects:{id:string;isArchived:boolean;isRemoved?:boolean}[]}
export interface NoteVersion {revision:number;title:string;markdown:string;isTrashed:boolean;atUtc:string;project?:NoteProjectLink}
export interface PersonalNote {id:string;current:NoteVersion;createdAtUtc:string;history:NoteVersion[]}
export interface NoteReceipt {commandId:string;payloadHash:string;noteId:string;noteRevision:number;notebookRevision:number}
export interface NotesNotebook {schemaVersion:1;ownerScope:string;revision:number;notes:PersonalNote[];receipts:NoteReceipt[];purged:{id:string;revision:number}[]}
export type NoteOperation='create'|'update'|'append'|'trash'|'restore'|'purge';
export interface NoteCommand {id:string;noteId:string;expectedNotebookRevision:number;expectedNoteRevision:number;operation:NoteOperation;title:string|null;markdown:string|null;project?:NoteProjectLink;clearProject?:true}
const size=(value:string)=>new TextEncoder().encode(value).length;
const revision=(value:unknown)=>Number.isSafeInteger(value)&&Number(value)>=0;
function validText(value:string):boolean {
 for(let i=0;i<value.length;i++){const c=value.charCodeAt(i);if(c>=0xd800&&c<=0xdbff){const n=value.charCodeAt(++i);if(!(n>=0xdc00&&n<=0xdfff))return false;}else if(c>=0xdc00&&c<=0xdfff)return false;}
 return true;
}
export function noteText(title:unknown,markdown:unknown):boolean{return typeof title==='string'&&/[^\u0009-\u000d\u0020\u0085\u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000]/.test(title)&&title.length<=256&&validText(title)&&typeof markdown==='string'&&validText(markdown)&&size(markdown)<=notesLimits.bodyBytes;}
export function noteProject(input:unknown):NoteProjectLink {
 const value=workspaceObject(input,['projectId','workspaceId','source','ownerScope']);
 if(!['browser','cloud'].includes(String(value.source))||typeof value.ownerScope!=='string'||!/^[a-f0-9]{64}$/.test(value.ownerScope))throw new WorkspaceError('invalid_project_link',422);
 return {projectId:uuid(value.projectId),workspaceId:uuid(value.workspaceId),source:value.source as 'browser'|'cloud',ownerScope:value.ownerScope};
}
export function noteCommand(input:unknown):NoteCommand {
 const value=workspaceObject(input,['id','noteId','expectedNotebookRevision','expectedNoteRevision','operation','title','markdown','project','clearProject']);
 if(!revision(value.expectedNotebookRevision)||!revision(value.expectedNoteRevision)||typeof value.operation!=='string'||!['create','update','append','trash','restore','purge'].includes(value.operation))throw new WorkspaceError('invalid_command',422);
 for(const key of ['title','markdown'])if(value[key]!==undefined&&value[key]!==null&&typeof value[key]!=='string')throw new WorkspaceError('invalid_command',422);
 const result:NoteCommand={id:uuid(value.id),noteId:uuid(value.noteId),expectedNotebookRevision:Number(value.expectedNotebookRevision),expectedNoteRevision:Number(value.expectedNoteRevision),operation:value.operation as NoteOperation,title:value.title as string??null,markdown:value.markdown as string??null};
 if(value.project!==undefined)result.project=noteProject(value.project);
 if(value.clearProject!==undefined){if(value.clearProject!==true)throw new WorkspaceError('invalid_project_patch',422);result.clearProject=true;}
 if(result.project&&result.clearProject||!['create','update'].includes(result.operation)&&(result.project||result.clearProject)||result.operation==='create'&&result.clearProject)throw new WorkspaceError('invalid_project_patch',422);
 if(result.title!==null&&!noteText(result.title,'')||result.markdown!==null&&!noteText('note',result.markdown))throw new WorkspaceError('note_content_limit',422);
 return result;
}
// Matches the existing NotesJson command hash, including System.Text.Json's default encoder.
export function notesWireJson(value:unknown):string {
 return JSON.stringify(value).replace(/"(?:\\.|[^"\\])*"/g,token=>{
  return '"'+token.slice(1,-1).replace(/\\(?:["\\/bfnrt]|u[0-9a-fA-F]{4})|[^\x20-\x7e]|[<>&'`+]/g,c=>c==='\\"'?'\\u0022':c.startsWith('\\u')?'\\u'+c.slice(2).toUpperCase():c.startsWith('\\')?c:'\\u'+c.charCodeAt(0).toString(16).toUpperCase().padStart(4,'0'))+'"';
 });
}
export async function noteHash(command:NoteCommand):Promise<string>{return workspaceTokenHash(notesWireJson(command));}
export const emptyNotes=(ownerScope:string):NotesNotebook=>({schemaVersion:1,ownerScope,revision:0,notes:[],receipts:[],purged:[]});
export async function applyNote(book:NotesNotebook,input:NoteCommand,now:string,authority?:NoteProjectAuthority):Promise<{book:NotesNotebook;receipt:NoteReceipt;disposition:'accepted'|'duplicate'}> {
 const hash=await noteHash(input),original=book.receipts.find(row=>row.commandId===input.id);
 if(original){if(original.payloadHash!==hash)throw new WorkspaceError('command_id_reused',409);return {book,receipt:original,disposition:'duplicate'};}
 if(book.revision!==input.expectedNotebookRevision)throw new WorkspaceError('stale_notebook',409);
 if(book.revision===Number.MAX_SAFE_INTEGER||book.receipts.length>=notesLimits.receipts+2*notesLimits.notes||book.receipts.length>=notesLimits.receipts&&!['trash','purge'].includes(input.operation))throw new WorkspaceError('notebook_quota',413);
 if(book.purged.some(note=>note.id===input.noteId))throw new WorkspaceError('note_purged',409);
 const note=book.notes.find(note=>note.id===input.noteId),operation=input.operation;
 if(operation==='create'?note!==undefined||input.expectedNoteRevision!==0:!note||input.expectedNoteRevision!==note.current.revision)throw new WorkspaceError('stale_note',409);
 if(note&&(note.current.revision===Number.MAX_SAFE_INTEGER||Date.parse(now)<Date.parse(note.current.atUtc)))throw new WorkspaceError('invalid_revision_time',409);
 if(['trash','restore','purge'].includes(operation)&&(input.title!==null||input.markdown!==null)||operation==='append'&&(input.title!==null||!input.markdown))throw new WorkspaceError('invalid_patch',422);
 if(operation==='update'&&input.title===null&&input.markdown===null&&!input.project&&!input.clearProject)throw new WorkspaceError('empty_patch',422);
 if(note&&(note.current.isTrashed?!['restore','purge'].includes(operation):['restore','purge'].includes(operation)))throw new WorkspaceError('invalid_lifecycle',409);
 if(input.project){
  const link=noteProject(input.project);if(link.ownerScope!==book.ownerScope)throw new WorkspaceError('project_owner_mismatch',403);
  const same=notesWireJson(note?.current.project??null)===notesWireJson(link);
  if(!same&&(!authority||authority.ownerScope!==book.ownerScope||authority.source!==link.source||authority.workspaceId!==link.workspaceId||!authority.projects.some(p=>p.id===link.projectId&&p.isArchived===false&&(p.isRemoved===undefined||p.isRemoved===false))))throw new WorkspaceError('project_unavailable',409);
 }
 const next={...book,notes:[...book.notes],receipts:[...book.receipts],purged:[...book.purged]},receipt:NoteReceipt={commandId:input.id,payloadHash:hash,noteId:input.noteId,noteRevision:(note?.current.revision??0)+1,notebookRevision:book.revision+1};
 if(operation==='purge'){next.notes=next.notes.filter(note=>note.id!==input.noteId);next.purged.push({id:input.noteId,revision:receipt.noteRevision});}
 else {
  if(operation==='create'&&book.notes.length>=notesLimits.notes)throw new WorkspaceError('notes_quota',413);
  const title=input.title??note?.current.title,markdown=operation==='append'?note!.current.markdown+input.markdown:(input.markdown??note?.current.markdown);
  if(!noteText(title,markdown))throw new WorkspaceError('note_content_limit',422);
  const changed:PersonalNote={id:input.noteId,createdAtUtc:note?.createdAtUtc??now,current:{revision:receipt.noteRevision,title:title!,markdown:markdown!,isTrashed:operation==='trash',atUtc:now,...(!input.clearProject&&(input.project??note?.current.project)?{project:input.project??note?.current.project}:{})},history:note?[...note.history,note.current].slice(-notesLimits.history):[]};
  next.notes=note?next.notes.map(value=>value.id===changed.id?changed:value):[...next.notes,changed];
 }
 next.revision++;next.receipts.push(receipt);
 const limit=['trash','purge'].includes(operation)?notesLimits.bytes:notesLimits.bytes-2*1024*1024;
 if(size(notesWireJson(next))>limit)throw new WorkspaceError('notebook_quota',413);
 return {book:next,receipt,disposition:'accepted'};
}
