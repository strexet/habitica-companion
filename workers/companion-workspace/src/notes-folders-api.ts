import {applyFolder,folderCommand} from '../../shared/notes-folders.ts';
import {noteText,notesWireJson,type NoteFolder} from '../../shared/notes.ts';
import {uuid,workspaceObject,WorkspaceError} from '../../shared/workspace-http.ts';
import {loadNotes,projectAuthority,requireProjectRead,type NotesRow,type NotesIdentity} from './notes-api.ts';
const headers={'cache-control':'no-store','x-content-type-options':'nosniff'};
const json=(v:unknown,status=200)=>Response.json(v,{status,headers});
export async function submitFolder(env:WorkspaceEnv,row:NotesRow,value:unknown,preview?:{grantEpoch:string;kind?:'folder'},who?:NotesIdentity):Promise<Response>{
 const c=folderCommand(value);if(c.source!=='cloud'||c.ownerScope!==row.owner_scope)throw new WorkspaceError('folder_owner_or_source_mismatch',403);
 if(c.project)requireProjectRead(who);
 const before=await loadNotes(env,row),duplicate=before.folderReceipts?.some(r=>r.commandId===c.id),project=c.project&&!duplicate?await projectAuthority(env,row,c.project):null;
 const result=await applyFolder(before,c,new Date().toISOString(),project?.authority);
 const output=()=>json({notebookId:row.notebook_id,ownerScope:row.owner_scope,receipt:result.receipt,disposition:result.disposition,saved:true},result.disposition==='accepted'?201:200);
 if(result.disposition==='duplicate')return output();
 const tag=crypto.randomUUID(),folder=result.book.folders!.find(f=>f.id===c.folderId)!;
 const args:unknown[]=[result.book.revision,tag,row.profile_id,row.notebook_id,row.connection_id,row.access_epoch,before.revision];
 const projectGuard=project?' AND EXISTS(SELECT 1 FROM workspaces w WHERE w.profile_id=notes_notebooks.profile_id AND w.connection_id=notes_notebooks.connection_id AND w.workspace_id=? AND w.store_version=? AND w.access_epoch=? AND w.active=1)':'';
 if(project)args.push(project.workspace.workspace_id,project.workspace.store_version,project.workspace.access_epoch);
 const previewTable=preview?.kind==='folder'?'note_folder_preparations':'notes_preparations';
 const previewGuard=preview?` AND EXISTS(SELECT 1 FROM ${previewTable} p WHERE p.profile_id=notes_notebooks.profile_id AND p.command_id=? AND p.notebook_id=notes_notebooks.notebook_id AND p.access_epoch=notes_notebooks.access_epoch AND p.grant_epoch=? AND p.command_json IS NOT NULL AND p.expires_at_utc>strftime('%Y-%m-%dT%H:%M:%fZ','now'))`:'';
 if(preview)args.push(c.id,preview.grantEpoch);
 const guard='EXISTS(SELECT 1 FROM notes_notebooks WHERE profile_id=? AND commit_tag=? AND active=1)';
 const writes=[
  env.WORKSPACE_DB.prepare('UPDATE notes_notebooks SET revision=?,commit_tag=?,schema_version=2 WHERE profile_id=? AND notebook_id=? AND connection_id=? AND access_epoch=? AND active=1 AND revision=?'+projectGuard+previewGuard).bind(...args),
  env.WORKSPACE_DB.prepare(`INSERT INTO note_folders(profile_id,folder_id,folder_json) SELECT profile_id,?,? FROM notes_notebooks WHERE profile_id=? AND commit_tag=? AND active=1 ON CONFLICT(profile_id,folder_id) DO UPDATE SET folder_json=excluded.folder_json`).bind(folder.id,JSON.stringify(folder),row.profile_id,tag),
  env.WORKSPACE_DB.prepare(`INSERT INTO note_folder_receipts(profile_id,command_id,sequence,receipt_json) SELECT profile_id,?,?,? FROM notes_notebooks WHERE profile_id=? AND commit_tag=? AND active=1`).bind(c.id,result.receipt.notebookRevision,JSON.stringify(result.receipt),row.profile_id,tag),
  env.WORKSPACE_DB.prepare(`UPDATE note_folder_preparations SET command_json=NULL WHERE profile_id=? AND command_id=? AND ${guard}`).bind(row.profile_id,c.id,row.profile_id,tag)
 ];
 const committed=await env.WORKSPACE_DB.batch(writes);
 if(!committed[0].meta.changes){const recovered=await applyFolder(await loadNotes(env,row),c,new Date().toISOString());if(recovered.disposition==='duplicate')return json({notebookId:row.notebook_id,ownerScope:row.owner_scope,receipt:recovered.receipt,disposition:'duplicate',saved:true});throw new WorkspaceError('stale_notebook',409);}
 return output();
}
export async function folderOperation(env:WorkspaceEnv,row:NotesRow,operation:string,input:unknown,who?:NotesIdentity):Promise<Response>{
 if(operation==='folder-command'){const v=workspaceObject(input,['expectedNotebookId','command']);if(uuid(v.expectedNotebookId)!==row.notebook_id)throw new WorkspaceError('notebook_changed',409);return submitFolder(env,row,v.command,undefined,who);}
 const args=workspaceObject(input,operation==='folders'?['query','offset','expectedRevision','projectId','projectWorkspaceId','unassigned','includeRemoved']:operation==='folder'?['folderId']:['expectedNotebookId','commandId']);
 const book=await loadNotes(env,row),context={notebookId:row.notebook_id,ownerScope:row.owner_scope,revision:book.revision};
 if(operation==='folder-receipt'){if(uuid(args.expectedNotebookId)!==row.notebook_id)throw new WorkspaceError('notebook_changed',409);const id=uuid(args.commandId),receipt=book.folderReceipts?.find(r=>r.commandId===id);if(!receipt)throw new WorkspaceError('command_unavailable',404);return json({...context,receipt,saved:true});}
 if(operation==='folder'){const id=uuid(args.folderId),folder=book.folders?.find(f=>f.id===id);if(!folder)throw new WorkspaceError('folder_unavailable',404);return json({...context,folder});}
 const offset=Number(args.offset??0),query=args.query??'';
 if(!Number.isSafeInteger(args.offset??0)||offset<0||offset>200||!noteText('search',query)||String(query).length>256||args.includeRemoved!==undefined&&typeof args.includeRemoved!=='boolean'||args.unassigned!==undefined&&typeof args.unassigned!=='boolean'||args.unassigned===true&&(args.projectId!==undefined||args.projectWorkspaceId!==undefined)||args.projectId===undefined&&args.projectWorkspaceId!==undefined)throw new WorkspaceError('invalid_folder_search',422);
 if(args.expectedRevision!==undefined&&args.expectedRevision!==book.revision||offset>0&&args.expectedRevision!==book.revision)throw new WorkspaceError('stale_notebook',409);
 const projectId=args.projectId===undefined?undefined:uuid(args.projectId),workspaceId=projectId===undefined?undefined:uuid(args.projectWorkspaceId),fold=(s:string)=>s.normalize('NFKC').toLowerCase();
 const matches=(book.folders??[]).filter(f=>(args.includeRemoved===true||!f.isRemoved)&&(args.unassigned!==true||!f.project)&&(projectId===undefined||f.project?.projectId===projectId&&f.project.workspaceId===workspaceId&&f.project.source==='cloud')&&fold(f.name).includes(fold(String(query))));
 return json({...context,folders:matches.slice(offset,offset+20),total:matches.length,nextOffset:offset+20<matches.length?offset+20:null});
}
