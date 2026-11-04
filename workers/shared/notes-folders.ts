import {workspaceObject,uuid,workspaceTokenHash,WorkspaceError} from './workspace-http.ts';
import {noteProject,noteText,notesLimits,notesWireJson,type NoteFolderCommand,type NoteFolder,type NoteFolderReceipt,type NotesNotebook,type NoteProjectAuthority} from './notes.ts';
export function folderCommand(input:unknown):NoteFolderCommand {
 const v=workspaceObject(input,['id','folderId','expectedNotebookRevision','expectedFolderRevision','operation','ownerScope','source','name','project']);
 if(!Number.isSafeInteger(v.expectedNotebookRevision)||Number(v.expectedNotebookRevision)<0||!Number.isSafeInteger(v.expectedFolderRevision)||Number(v.expectedFolderRevision)<0||!['create','rename','remove'].includes(String(v.operation))||typeof v.ownerScope!=='string'||!/^[a-f0-9]{64}$/.test(v.ownerScope)||!['browser','cloud'].includes(String(v.source)))throw new WorkspaceError('invalid_folder_command',422);
 const c:NoteFolderCommand={id:uuid(v.id),folderId:uuid(v.folderId),expectedNotebookRevision:Number(v.expectedNotebookRevision),expectedFolderRevision:Number(v.expectedFolderRevision),operation:v.operation as NoteFolderCommand['operation'],ownerScope:v.ownerScope,source:v.source as 'browser'|'cloud',name:v.name as string??null};
 if(v.project!==undefined)c.project=noteProject(v.project);
 if(c.operation==='remove'?c.name!==null:!noteText(c.name,''))throw new WorkspaceError('invalid_folder_name',422);
 if(c.project&&(c.operation!=='create'||c.project.source!==c.source))throw new WorkspaceError('invalid_folder_project',422);
 return c;
}
export const folderHash=(command:NoteFolderCommand)=>workspaceTokenHash(notesWireJson(command));
export async function applyFolder(book:NotesNotebook,c:NoteFolderCommand,now:string,authority?:NoteProjectAuthority):Promise<{book:NotesNotebook;receipt:NoteFolderReceipt;disposition:'accepted'|'duplicate'}>{
 if(c.ownerScope!==book.ownerScope)throw new WorkspaceError('folder_owner_mismatch',403);
 const hash=await folderHash(c),original=book.folderReceipts?.find(r=>r.commandId===c.id);
 if(original){if(original.payloadHash!==hash)throw new WorkspaceError('command_id_reused',409);return {book,receipt:original,disposition:'duplicate'};}
 if(book.receipts.some(r=>r.commandId===c.id))throw new WorkspaceError('command_id_reused',409);
 if(c.expectedNotebookRevision!==book.revision)throw new WorkspaceError('stale_notebook',409);
 const folders=book.folders??[],folder=folders.find(f=>f.id===c.folderId),create=c.operation==='create';
 if(create?!!folder||c.expectedFolderRevision!==0:!folder||folder.revision!==c.expectedFolderRevision)throw new WorkspaceError('stale_folder',409);
 if(folder?.isRemoved)throw new WorkspaceError('folder_removed',409);
 if(!Number.isFinite(Date.parse(now))||folder&&(folder.source!==c.source||Date.parse(now)<Date.parse(folder.updatedAtUtc)||folder.revision>=Number.MAX_SAFE_INTEGER))throw new WorkspaceError('invalid_folder_identity',422);
 if(folders.some(f=>f.source!==c.source))throw new WorkspaceError('folder_source_mismatch',403);
 if(c.project&&(c.project.ownerScope!==book.ownerScope||!authority||authority.ownerScope!==book.ownerScope||authority.source!==c.source||authority.workspaceId!==c.project.workspaceId||!authority.projects.some(p=>p.id===c.project!.projectId&&!p.isArchived&&!p.isRemoved)))throw new WorkspaceError('project_unavailable',409);
 if(create&&folders.length>=notesLimits.folders)throw new WorkspaceError('folders_quota',413);
 if(c.operation==='remove'&&book.notes.some(n=>n.current.folder?.folderId===c.folderId))throw new WorkspaceError('folder_not_empty',409);
 const count=book.receipts.length+(book.folderReceipts?.length??0);
 if(book.revision>=Number.MAX_SAFE_INTEGER||count>=notesLimits.receipts+2*notesLimits.notes+notesLimits.folders||count>=notesLimits.receipts&&c.operation!=='remove')throw new WorkspaceError('notebook_quota',413);
 const changed:NoteFolder=create?{id:c.folderId,revision:1,name:c.name!,source:c.source,createdAtUtc:now,updatedAtUtc:now,isRemoved:false,...(c.project?{project:c.project}:{})}:{...folder!,revision:folder!.revision+1,name:c.name??folder!.name,updatedAtUtc:now,isRemoved:c.operation==='remove'};
 const receipt:NoteFolderReceipt={commandId:c.id,payloadHash:hash,folderId:c.folderId,folderRevision:changed.revision,notebookRevision:book.revision+1};
 const next:NotesNotebook={...book,schemaVersion:2,revision:book.revision+1,folders:create?[...folders,changed]:folders.map(f=>f.id===c.folderId?changed:f),folderReceipts:[...(book.folderReceipts??[]),receipt]};
 if(new TextEncoder().encode(notesWireJson(next)).length>(c.operation==='remove'?notesLimits.bytes:notesLimits.bytes-2*1024*1024))throw new WorkspaceError('notebook_quota',413);
 return {book:next,receipt,disposition:'accepted'};
}
