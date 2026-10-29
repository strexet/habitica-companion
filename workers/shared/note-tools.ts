import {noteCommand,notesReadScope,notesWriteScope} from './notes.ts';
import {workspaceObject,uuid,WorkspaceError} from './workspace-http.ts';
export const noteToolScopes={search_notes:[notesReadScope],get_note:[notesReadScope],prepare_note_command:[notesReadScope,notesWriteScope],accept_note_command:[notesReadScope,notesWriteScope],get_note_command:[notesReadScope]} as const;
export type NoteTool=keyof typeof noteToolScopes;
export function noteToolInput(name:string,value:unknown):Record<string,unknown>{
 if(!Object.hasOwn(noteToolScopes,name))throw new WorkspaceError('invalid_tool',422);
 if(name==='search_notes'){
  const raw=workspaceObject(value,['query','trash','offset','expectedRevision','projectId','projectWorkspaceId','unassigned']);
  if(raw.query!==undefined&&(typeof raw.query!=='string'||raw.query.length>256)||raw.trash!==undefined&&typeof raw.trash!=='boolean'||raw.offset!==undefined&&(!Number.isSafeInteger(raw.offset)||Number(raw.offset)<0||Number(raw.offset)>200)||raw.expectedRevision!==undefined&&(!Number.isSafeInteger(raw.expectedRevision)||Number(raw.expectedRevision)<0))throw new WorkspaceError('invalid_search',422);
  if(raw.unassigned!==undefined&&typeof raw.unassigned!=='boolean'||raw.unassigned===true&&(raw.projectId!==undefined||raw.projectWorkspaceId!==undefined)||raw.projectId===undefined&&raw.projectWorkspaceId!==undefined)throw new WorkspaceError('invalid_project_filter',422);
  const project=raw.projectId===undefined?{}:{projectId:uuid(raw.projectId),projectWorkspaceId:uuid(raw.projectWorkspaceId)};
  return {...project,...(raw.unassigned===undefined?{}:{unassigned:raw.unassigned}),query:raw.query??'',trash:raw.trash??false,offset:raw.offset??0,...(raw.expectedRevision===undefined?{}:{expectedRevision:raw.expectedRevision})};
 }
 if(name==='get_note'){
  const raw=workspaceObject(value,['noteId','noteRevision']);if(raw.noteRevision!==undefined&&(!Number.isSafeInteger(raw.noteRevision)||Number(raw.noteRevision)<1))throw new WorkspaceError('invalid_revision',422);
  return {noteId:uuid(raw.noteId),...(raw.noteRevision===undefined?{}:{noteRevision:raw.noteRevision})};
 }
 if(name==='prepare_note_command'){
  const raw=workspaceObject(value,['requestId','notebookId','noteId','operation','expectedNotebookRevision','expectedNoteRevision','title','markdown','projectId','projectWorkspaceId','clearProject']);
  if(raw.operation==='create'&&raw.noteId!==undefined)throw new WorkspaceError('server_note_identity_required',422);
  if(raw.clearProject!==undefined&&raw.clearProject!==true||raw.clearProject===true&&(raw.projectId!==undefined||raw.projectWorkspaceId!==undefined)||raw.projectId===undefined&&raw.projectWorkspaceId!==undefined||!['create','update'].includes(String(raw.operation))&&(raw.projectId!==undefined||raw.clearProject!==undefined)||raw.operation==='create'&&raw.clearProject!==undefined)throw new WorkspaceError('invalid_project_patch',422);
  const project=raw.projectId===undefined?{}:{projectId:uuid(raw.projectId),projectWorkspaceId:uuid(raw.projectWorkspaceId)};
  const dummy='11111111-1111-4111-8111-111111111111';const command=noteCommand({id:dummy,noteId:raw.operation==='create'?dummy:raw.noteId,operation:raw.operation,expectedNotebookRevision:raw.expectedNotebookRevision,expectedNoteRevision:raw.expectedNoteRevision,title:raw.title,markdown:raw.markdown,...(raw.clearProject===true?{clearProject:true}:{})});
  return {requestId:uuid(raw.requestId),notebookId:uuid(raw.notebookId),...(raw.operation==='create'?{}:{noteId:command.noteId}),operation:command.operation,expectedNotebookRevision:command.expectedNotebookRevision,expectedNoteRevision:command.expectedNoteRevision,title:command.title,markdown:command.markdown,...project,...(raw.clearProject===true?{clearProject:true}:{})};
 }
 const raw=workspaceObject(value,['notebookId','commandId']);return {notebookId:uuid(raw.notebookId),commandId:uuid(raw.commandId)};
}
