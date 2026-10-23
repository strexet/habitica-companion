import {noteCommand,notesReadScope,notesWriteScope} from './notes.ts';
import {workspaceObject,uuid,WorkspaceError} from './workspace-http.ts';
export const noteToolScopes={search_notes:[notesReadScope],get_note:[notesReadScope],prepare_note_command:[notesReadScope,notesWriteScope],accept_note_command:[notesReadScope,notesWriteScope],get_note_command:[notesReadScope]} as const;
export type NoteTool=keyof typeof noteToolScopes;
export function noteToolInput(name:string,value:unknown):Record<string,unknown>{
 if(!Object.hasOwn(noteToolScopes,name))throw new WorkspaceError('invalid_tool',422);
 if(name==='search_notes'){
  const raw=workspaceObject(value,['query','trash','offset','expectedRevision']);
  if(raw.query!==undefined&&(typeof raw.query!=='string'||raw.query.length>256)||raw.trash!==undefined&&typeof raw.trash!=='boolean'||raw.offset!==undefined&&(!Number.isSafeInteger(raw.offset)||Number(raw.offset)<0||Number(raw.offset)>200)||raw.expectedRevision!==undefined&&(!Number.isSafeInteger(raw.expectedRevision)||Number(raw.expectedRevision)<0))throw new WorkspaceError('invalid_search',422);
  return {query:raw.query??'',trash:raw.trash??false,offset:raw.offset??0,...(raw.expectedRevision===undefined?{}:{expectedRevision:raw.expectedRevision})};
 }
 if(name==='get_note'){
  const raw=workspaceObject(value,['noteId','noteRevision']);if(raw.noteRevision!==undefined&&(!Number.isSafeInteger(raw.noteRevision)||Number(raw.noteRevision)<1))throw new WorkspaceError('invalid_revision',422);
  return {noteId:uuid(raw.noteId),...(raw.noteRevision===undefined?{}:{noteRevision:raw.noteRevision})};
 }
 if(name==='prepare_note_command'){
  const raw=workspaceObject(value,['requestId','notebookId','noteId','operation','expectedNotebookRevision','expectedNoteRevision','title','markdown']);
  if(raw.operation==='create'&&raw.noteId!==undefined)throw new WorkspaceError('server_note_identity_required',422);
  const dummy='11111111-1111-4111-8111-111111111111';const command=noteCommand({id:dummy,noteId:raw.operation==='create'?dummy:raw.noteId,operation:raw.operation,expectedNotebookRevision:raw.expectedNotebookRevision,expectedNoteRevision:raw.expectedNoteRevision,title:raw.title,markdown:raw.markdown});
  return {requestId:uuid(raw.requestId),notebookId:uuid(raw.notebookId),...(raw.operation==='create'?{}:{noteId:command.noteId}),operation:command.operation,expectedNotebookRevision:command.expectedNotebookRevision,expectedNoteRevision:command.expectedNoteRevision,title:command.title,markdown:command.markdown};
 }
 const raw=workspaceObject(value,['notebookId','commandId']);return {notebookId:uuid(raw.notebookId),commandId:uuid(raw.commandId)};
}
