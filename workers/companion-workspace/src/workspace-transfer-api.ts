import {workspaceTransferInput,type WorkspaceTransferTool} from '../../shared/workspace-transfer-tools.ts';
import {splitTransferNotes,joinTransferNotes} from '../../shared/transfer-note-parts.ts';
import {workspaceObject as object,uuid,WorkspaceError,workspaceTokenHash} from '../../shared/workspace-http.ts';
import {parseTransfer,mergeTransfer,transferVersion,transferLimit,byteLength} from '../../shared/workspace-transfer.ts';
import {notesWireJson,notesConsentVersion,type NotesNotebook} from '../../shared/notes.ts';
import {loadNotes,type NotesRow} from './notes-api.ts';
import type {WorkspaceRow} from './executor.ts';
export type TransferTool=WorkspaceTransferTool;
interface Stored {transfer_id:string;artifact_hash:string;artifact_bytes:number;manifest_json:string;access_epoch:string;notes_epoch:string|null;state:string;review_json:string|null;review_hash:string|null;expires_at_utc:string|null;receipt_json:string|null}
interface Review {reviewId:string;version:string;transferId:string;artifactHash:string;workspaceId:string;workspaceStoreVersion:number;notesRevision:number|null;notebookId:string|null;sources:unknown;accessEpoch:string;notesEpoch:string|null;grantEpoch:string|null;expiresAtUtc:string;archiveConflicts:boolean;ready:boolean;conflicts:unknown[];counts:unknown;mappings:unknown;noteFolders?:unknown;noteFolderMemberships?:unknown;pausedTaskIds:string[];sourceArchivesRetained:true;gameEffects:0;reviewHash?:string}
const now=()=>new Date().toISOString();
function fail(code:string,status=409):never {throw new WorkspaceError(code,status);}
async function stored(env:WorkspaceEnv,row:WorkspaceRow,id:string){return env.WORKSPACE_DB.prepare('SELECT * FROM workspace_transfers WHERE profile_id=? AND transfer_id=?').bind(row.profile_id,id).first<Stored>();}
async function notesRow(env:WorkspaceEnv,row:WorkspaceRow,required:boolean){if(!required)return null;const value=await env.WORKSPACE_DB.prepare('SELECT * FROM notes_notebooks WHERE profile_id=? AND connection_id=? AND active=1 AND consent_version=?').bind(row.profile_id,row.connection_id,notesConsentVersion).first<NotesRow>();if(!value)fail('notes_consent_required',403);return value;}
async function artifactParts(env:WorkspaceEnv,row:WorkspaceRow,id:string){const chunks=await env.WORKSPACE_DB.prepare('SELECT part,chunk FROM workspace_transfer_chunks WHERE profile_id=? AND transfer_id=? ORDER BY part,sequence').bind(row.profile_id,id).all<{part:string;chunk:string}>();const parts=new Map<string,string>();for(const chunk of chunks.results)parts.set(chunk.part,(parts.get(chunk.part)??'')+chunk.chunk);return parts;}
async function artifactText(env:WorkspaceEnv,row:WorkspaceRow,id:string){
 const parts=await artifactParts(env,row,id),entry=await stored(env,row,id),manifest=entry?JSON.parse(entry.manifest_json):null,taskText=parts.get('tasks');
 if(!manifest||!taskText||await workspaceTokenHash(taskText)!==manifest.taskArtifactHash)fail('transfer_archive_corrupt');const value=JSON.parse(taskText);
 for(const [noteId,expected] of Object.entries(manifest.noteHashes??{})){const fragment=parts.get('note:'+noteId);if(fragment!==undefined&&await workspaceTokenHash(fragment)!==expected)fail('transfer_archive_corrupt');}
 if(manifest.notesTemplateHash&&await workspaceTokenHash(parts.get('notes-template')??'')!==manifest.notesTemplateHash)fail('transfer_archive_corrupt');
 if(parts.has('notes-template'))value.notes=joinTransferNotes(parts.get('notes-template')!,parts).text;else if(manifest.includesNotes)fail('transfer_source_notes_purged');return JSON.stringify(value);
}

async function taskArtifact(env:WorkspaceEnv,row:WorkspaceRow,entry:Stored){const parts=await artifactParts(env,row,entry.transfer_id),text=parts.get('tasks');if(!text||await workspaceTokenHash(text)!==JSON.parse(entry.manifest_json).taskArtifactHash)fail('transfer_archive_corrupt');return parseTransfer(text);}

export async function stageWorkspaceTransfer(env:WorkspaceEnv,row:WorkspaceRow,text:string){
 const parsed=await parseTransfer(text),a=parsed.artifact;if(JSON.stringify(a)!==text)fail('transfer_canonical_bytes_required',422);if(a.targetWorkspaceId!==row.workspace_id)fail('workspace_mismatch');
 // Existing owner proof is internal and ephemeral; no credential or raw account ID enters the ledger.
 const proof=await env.AUTH_IDENTITY.fetch('https://identity.internal/task-recovery-authority',{method:'POST',body:JSON.stringify({profileId:row.profile_id,connectionId:row.connection_id}),signal:AbortSignal.timeout(5000)});
 if(!proof.ok)fail('existing_owner_access_required',403);const who=await proof.json<{profileId:string;connectionId:string;accountId:string}>();
 if(who.profileId!==row.profile_id||who.connectionId!==row.connection_id||typeof who.accountId!=='string'||await workspaceTokenHash(uuid(who.accountId))!==a.ownerScope)fail('source_owner_mismatch',403);
 const legacy=await env.WORKSPACE_DB.prepare('SELECT 1 FROM workspace_handoffs WHERE profile_id=? AND transfer_id=?').bind(row.profile_id,a.transferId).first();if(legacy)fail('transfer_id_reused');
 const book=await notesRow(env,row,a.notes!==null),prior=await stored(env,row,a.transferId);
 if(prior){if(prior.artifact_hash!==parsed.hash)fail('transfer_id_reused');return {transferId:a.transferId,artifactHash:parsed.hash,state:prior.state,staged:true};}
 const usage=await env.WORKSPACE_DB.prepare("SELECT COALESCE(SUM(CASE WHEN state<>'accepted' THEN artifact_bytes ELSE 0 END),0) AS bytes,COUNT(*) AS count FROM workspace_transfers WHERE profile_id=?").bind(row.profile_id).first<{bytes:number;count:number}>();
 if((usage?.count??0)>=32||row.archive_bytes+(usage?.bytes??0)+byteLength(text)>transferLimit)fail('transfer_capacity',413);
 const taskText=JSON.stringify({...a,notes:null}),noteParts=parsed.notes?await splitTransferNotes(a.notes!,parsed.notes):null;
 const manifest={taskArtifactHash:await workspaceTokenHash(taskText),noteHashes:noteParts?.hashes??{},notesTemplateHash:noteParts?await workspaceTokenHash(noteParts.parts[0].text):null,version:transferVersion,transferId:a.transferId,workspaceId:a.targetWorkspaceId,ownerScope:a.ownerScope,includesNotes:a.notes!==null,sources:await Promise.all(parsed.sources.map(async source=>({workspaceId:source.document.id,revision:source.document.revision,storeVersion:source.storeVersion,archiveHash:await workspaceTokenHash(a.workspaces.find(value=>value.workspaceId===source.document.id)!.archive),tasks:source.document.tasks.length,commands:source.commands.length,events:source.events.length}))),notes:parsed.notes?{revision:parsed.notes.revision,notes:parsed.notes.notes.length,purged:parsed.notes.purged.length,receipts:parsed.notes.receipts.length,folders:parsed.notes.folders?.length??0,folderReceipts:parsed.notes.folderReceipts?.length??0}:null};
 const writes=[env.WORKSPACE_DB.prepare(`INSERT INTO workspace_transfers(profile_id,transfer_id,artifact_hash,artifact_bytes,manifest_json,access_epoch,notes_epoch,state,created_at_utc)
 SELECT profile_id,?,?,?,?,?,?,'staged',? FROM workspaces WHERE profile_id=? AND access_epoch=? AND connection_id=? AND active=1 AND archive_bytes+?+(SELECT COALESCE(SUM(artifact_bytes),0) FROM workspace_transfers WHERE profile_id=workspaces.profile_id AND state<>'accepted')<=? AND (SELECT COUNT(*) FROM workspace_transfers WHERE profile_id=workspaces.profile_id)<32`).bind(a.transferId,parsed.hash,byteLength(text),JSON.stringify(manifest),row.access_epoch,book?.access_epoch??null,now(),row.profile_id,row.access_epoch,row.connection_id,byteLength(text),transferLimit)];
 // Store exact Note fragments separately so explicit Note purge erases every retained body copy.
 const parts=[{part:'tasks',text:taskText},...(noteParts?.parts??[])];
 for(const part of parts){let sequence=0,chunk='',size=0;const chunks:string[]=[];for(const character of part.text){const n=byteLength(character);if(size+n>512*1024){chunks.push(chunk);chunk='';size=0;}chunk+=character;size+=n;}if(chunk)chunks.push(chunk);
  for(const chunk of chunks)writes.push(env.WORKSPACE_DB.prepare('INSERT INTO workspace_transfer_chunks(profile_id,transfer_id,part,sequence,chunk) SELECT profile_id,transfer_id,?,?,? FROM workspace_transfers WHERE profile_id=? AND transfer_id=? AND artifact_hash=?').bind(part.part,sequence++,chunk,row.profile_id,a.transferId,parsed.hash));
 }
 try{const results=await env.WORKSPACE_DB.batch(writes);if(!results[0].meta.changes)fail('connection_changed');}catch(error){const existing=await stored(env,row,a.transferId);if(!existing||existing.artifact_hash!==parsed.hash)throw error;}
 return {transferId:a.transferId,artifactHash:parsed.hash,state:'staged',staged:true};
}
async function context(env:WorkspaceEnv,row:WorkspaceRow,entry:Stored){
 const text=await artifactText(env,row,entry.transfer_id);if(await workspaceTokenHash(text)!==entry.artifact_hash)fail('transfer_archive_corrupt');const parsed=await parseTransfer(text);
 if(parsed.artifact.targetWorkspaceId!==row.workspace_id)fail('workspace_mismatch');const note=await notesRow(env,row,parsed.notes!==null);return {parsed,note,book:note?await loadNotes(env,note):null};
}
export async function workspaceTransferApi(env:WorkspaceEnv,row:WorkspaceRow,tool:TransferTool,input:unknown,grantEpoch:string|null=null):Promise<unknown>{
 const args=workspaceTransferInput(tool,input);
 if(tool==='list_workspace_transfers'){
  const offset=Number(args.offset??0),result=await env.WORKSPACE_DB.prepare("SELECT transfer_id AS transferId,artifact_hash AS artifactHash,state FROM workspace_transfers WHERE profile_id=? AND json_extract(manifest_json,'$.workspaceId')=? ORDER BY created_at_utc,transfer_id LIMIT 21 OFFSET ?").bind(row.profile_id,row.workspace_id,offset).all<{transferId:string;artifactHash:string;state:string}>();
  return {workspaceId:row.workspace_id,items:result.results.slice(0,20),nextOffset:result.results.length>20?offset+20:null,browserInventory:'unknown',localAcknowledgement:'owner_browser_required'};
 }

 const id=uuid(args.transferId),entry=await stored(env,row,id);
 if(!entry){if(['get_workspace_transfer_receipt','get_workspace_transfer_status'].includes(tool)){const legacy=await env.WORKSPACE_DB.prepare('SELECT archive_hash,workspace_id FROM workspace_handoffs WHERE profile_id=? AND transfer_id=? AND workspace_id=?').bind(row.profile_id,id,row.workspace_id).first<{archive_hash:string;workspace_id:string}>();if(legacy)return {version:'workspace-handoff-v1',state:'accepted',legacy:true,transferId:id,workspaceId:legacy.workspace_id,archiveHash:legacy.archive_hash,notesIncluded:false,localAcknowledgement:'owner_browser_required'};}
  return {state:'unavailable',transferId:id,prerequisite:'owner_browser_capture_required',outcomeUnknown:true};}

 const manifest=JSON.parse(entry.manifest_json);const taskOnly=tool==='get_workspace_transfer_record'&&!['noteReceipt','folderReceipt'].includes(String(args.kind));const note=await notesRow(env,row,manifest.includesNotes===true&&!taskOnly);
 if(tool==='get_workspace_transfer_record'){
  if(entry.state!=='accepted')fail('transfer_not_accepted');const commandId=uuid(args.commandId);
  if(args.kind==='folderReceipt'){const parts=await artifactParts(env,row,id),template=parts.get('notes-template');if(!template||!manifest.notesTemplateHash)return {transferId:id,original:null,outcomeUnknown:true};if(await workspaceTokenHash(template)!==manifest.notesTemplateHash)fail('transfer_archive_corrupt');const source=JSON.parse(joinTransferNotes(template,parts,true).text) as NotesNotebook,original=source.folderReceipts?.find(r=>r.commandId===commandId);return {transferId:id,artifactHash:entry.artifact_hash,ownerScope:source.ownerScope,original:original??null,outcomeUnknown:!original};}
  const parsed=['noteReceipt','folderReceipt'].includes(String(args.kind))?await parseTransfer(await artifactText(env,row,id)):await taskArtifact(env,row,entry);
  if(args.kind==='taskCommand'||args.kind==='organizationCommand'){const sourceId=uuid(args.sourceWorkspaceId),source=parsed.sources.find(source=>source.document.id===sourceId),original=args.kind==='organizationCommand'?source?.document.organizationHistory?.find(entry=>entry.command.id===commandId):source?.commands.find(entry=>entry.command.id===commandId);return {transferId:id,artifactHash:entry.artifact_hash,sourceWorkspaceId:sourceId,original:original??null,outcomeUnknown:!original};}
  if(['noteReceipt','folderReceipt'].includes(String(args.kind))&&args.sourceWorkspaceId===undefined){const original=(args.kind==='folderReceipt'?parsed.notes?.folderReceipts:parsed.notes?.receipts)?.find(receipt=>receipt.commandId===commandId);return {transferId:id,artifactHash:entry.artifact_hash,ownerScope:parsed.notes?.ownerScope??null,original:original??null,outcomeUnknown:!original};}
  fail('invalid_source_record',422);
 }
 if(tool==='get_workspace_transfer_receipt')return entry.receipt_json?JSON.parse(entry.receipt_json):{transferId:id,state:entry.state,outcomeUnknown:true,artifactHash:entry.artifact_hash};
 if(tool==='get_workspace_transfer_status')return {transferId:id,state:entry.state,artifactHash:entry.artifact_hash,manifest,review:entry.review_json?JSON.parse(entry.review_json):null,receipt:entry.receipt_json?JSON.parse(entry.receipt_json):null,prerequisite:entry.state==='accepted'?'browser_acknowledgement_if_pending':'review_required'};
 if(entry.receipt_json){const receipt=JSON.parse(entry.receipt_json);if(tool==='accept_workspace_transfer'&&args.reviewHash!==receipt.reviewHash)fail('review_mismatch');return receipt;}
 if(row.linked_recovery_fence||row.retirement_fence)fail('workspace_recovery_pending');
 if(entry.access_epoch!==row.access_epoch||entry.notes_epoch!==(note?.access_epoch??null))fail('transfer_connection_changed');
 const runtime=await env.WORKSPACE_DB.prepare('SELECT lease_until_utc FROM workspace_runtime WHERE profile_id=? AND lease_until_utc>?').bind(row.profile_id,now()).first();if(runtime)fail('workspace_execution_busy');
 if(tool==='preview_workspace_transfer'){
  if(args.archiveConflicts!==undefined&&typeof args.archiveConflicts!=='boolean')fail('invalid_conflict_policy',422);
  const {parsed,book}=await context(env,row,entry),merged=mergeTransfer(JSON.parse(row.document_json),book,parsed.sources,parsed.notes,id,args.archiveConflicts===true);
  const review:Review={reviewId:crypto.randomUUID(),version:transferVersion,transferId:id,artifactHash:entry.artifact_hash,workspaceId:row.workspace_id,workspaceStoreVersion:row.store_version,notesRevision:book?.revision??null,notebookId:note?.notebook_id??null,sources:manifest.sources,accessEpoch:row.access_epoch,notesEpoch:note?.access_epoch??null,grantEpoch,expiresAtUtc:new Date(Date.now()+15*60000).toISOString(),archiveConflicts:args.archiveConflicts===true,ready:merged.ready,conflicts:merged.conflicts,counts:{existingTargetTasks:JSON.parse(row.document_json).tasks.length,existingTargetNotes:book?.notes.length??0,existingTargetFolders:book?.folders?.length??0,folders:parsed.notes?.folders?.length??0,folderReceipts:parsed.notes?.folderReceipts?.length??0,sources:parsed.sources.length,tasks:parsed.sources.reduce((n,s)=>n+s.document.tasks.length,0),notes:parsed.notes?.notes.length??0,purgedNotes:parsed.notes?.purged.length??0,sourceCommands:parsed.sources.reduce((n,s)=>n+s.commands.length,0),sourceEvents:parsed.sources.reduce((n,s)=>n+s.events.length,0),retainedSourceVariants:merged.conflicts.length},mappings:merged.mappings,noteFolders:merged.notes?.folders??[],noteFolderMemberships:merged.notes?.notes.filter(n=>parsed.notes?.notes.some(s=>s.id===n.id)).map(n=>({noteId:n.id,currentFolderId:n.current.folder?.folderId??null,historicalFolderIds:n.history.map(v=>v.folder?.folderId??null)}))??[],pausedTaskIds:merged.fences,sourceArchivesRetained:true,gameEffects:0};
  const hash=await workspaceTokenHash(JSON.stringify(review));review.reviewHash=hash;
  const saved=await env.WORKSPACE_DB.prepare("UPDATE workspace_transfers SET state='prepared',review_json=?,review_hash=?,expires_at_utc=? WHERE profile_id=? AND transfer_id=? AND state<>'accepted' AND (review_hash=? OR review_hash IS NULL AND ? IS NULL)").bind(JSON.stringify(review),hash,review.expiresAtUtc,row.profile_id,id,entry.review_hash,entry.review_hash).run();
  if(!saved.meta.changes){const latest=await stored(env,row,id);if(latest?.receipt_json)return JSON.parse(latest.receipt_json);fail('transfer_review_changed');}return review;
 }
 if(typeof args.reviewHash!=='string'||args.reviewHash!==entry.review_hash||!entry.review_json)fail('review_mismatch');
 const review=JSON.parse(entry.review_json) as Review;const {reviewHash:ignored,...bytes}=review;
 if(await workspaceTokenHash(JSON.stringify(bytes))!==args.reviewHash)fail('review_bytes_changed');
 if(!review.ready||review.grantEpoch!==grantEpoch||review.accessEpoch!==row.access_epoch||review.notesEpoch!==(note?.access_epoch??null)||review.workspaceStoreVersion!==row.store_version||review.expiresAtUtc<=now())fail('review_stale');
 const {parsed,book}=await context(env,row,entry);if(review.notesRevision!==(book?.revision??null))fail('review_stale');
 const merged=mergeTransfer(JSON.parse(row.document_json),book,parsed.sources,parsed.notes,id,review.archiveConflicts);if(!merged.document)fail('conflict_resolution_required');
 const document=JSON.stringify(merged.document),newBook=merged.notes;const noteBytes=newBook?byteLength(notesWireJson(newBook)):0;
 const usage=await env.WORKSPACE_DB.prepare("SELECT COALESCE(SUM(artifact_bytes),0) AS bytes FROM workspace_transfers WHERE profile_id=? AND state<>'accepted' AND transfer_id<>?").bind(row.profile_id,id).first<{bytes:number}>();
 const archiveBytes=row.archive_bytes-byteLength(row.document_json)+byteLength(document)+entry.artifact_bytes;
 if(byteLength(document)>1024*1024||archiveBytes+(usage?.bytes??0)>transferLimit||noteBytes>14*1024*1024||row.store_version>=Number.MAX_SAFE_INTEGER)fail('transfer_capacity',413);
 const nextStoreVersion=Math.max(row.store_version+1,merged.document.revision);const tag=crypto.randomUUID(),receipt={version:transferVersion,transferId:id,artifactHash:entry.artifact_hash,reviewHash:args.reviewHash,workspaceId:row.workspace_id,notebookId:note?.notebook_id??null,state:'accepted',acceptedAtUtc:now(),workspaceRevision:merged.document.revision,workspaceStoreVersion:nextStoreVersion,notesRevision:newBook?.revision??null,counts:review.counts,mappings:merged.mappings,pausedTaskIds:merged.fences,sourceArchivesRetained:true,gameEffects:0};
 const writes=[env.WORKSPACE_DB.prepare(`UPDATE workspaces SET document_json=?,store_version=?,archive_bytes=?,commit_tag=?,updated_at_utc=? WHERE profile_id=? AND store_version=? AND access_epoch=? AND connection_id=? AND active=1
 AND ?+(SELECT COALESCE(SUM(artifact_bytes),0) FROM workspace_transfers WHERE profile_id=workspaces.profile_id AND state<>'accepted' AND transfer_id<>?)<=?
 AND (? IS NULL OR mcp_grant_epoch=?)
 AND NOT EXISTS(SELECT 1 FROM workspace_runtime WHERE profile_id=workspaces.profile_id AND lease_until_utc>?)
 AND EXISTS(SELECT 1 FROM workspace_transfers WHERE profile_id=workspaces.profile_id AND transfer_id=? AND state='prepared' AND review_hash=? AND expires_at_utc>strftime('%Y-%m-%dT%H:%M:%fZ','now'))
 AND (? IS NULL OR EXISTS(SELECT 1 FROM notes_notebooks WHERE profile_id=workspaces.profile_id AND active=1 AND access_epoch=? AND revision=?))`).bind(document,nextStoreVersion,archiveBytes,tag,now(),row.profile_id,row.store_version,row.access_epoch,row.connection_id,archiveBytes,id,transferLimit,grantEpoch,grantEpoch,now(),id,args.reviewHash,note?.access_epoch??null,note?.access_epoch??null,book?.revision??null)];
 const guard='EXISTS(SELECT 1 FROM workspaces WHERE profile_id=? AND commit_tag=?)';
 if(note&&newBook&&parsed.notes){
  writes.push(env.WORKSPACE_DB.prepare(`UPDATE notes_notebooks SET revision=?,schema_version=?,commit_tag=? WHERE profile_id=? AND access_epoch=? AND active=1 AND ${guard}`).bind(newBook.revision,newBook.schemaVersion,tag,row.profile_id,note.access_epoch,row.profile_id,tag));
  const marker=newBook.transfers!.at(-1)!;
  for(const baseline of marker.notes){const current=newBook.notes.find(n=>n.id===baseline.id);if(current){
   writes.push(env.WORKSPACE_DB.prepare(`INSERT INTO personal_notes(profile_id,note_id,created_at_utc,revision,title,markdown,is_trashed,is_purged,at_utc,project_json,folder_json) SELECT ?,?,?,?,?,?,?,0,?,?,? WHERE ${guard}`).bind(row.profile_id,current.id,current.createdAtUtc,current.current.revision,current.current.title,current.current.markdown,Number(current.current.isTrashed),current.current.atUtc,current.current.project?JSON.stringify(current.current.project):null,current.current.folder?JSON.stringify(current.current.folder):null,row.profile_id,tag));
   for(const version of current.history)writes.push(env.WORKSPACE_DB.prepare(`INSERT INTO personal_note_versions(profile_id,note_id,revision,version_json) SELECT ?,?,?,? WHERE ${guard}`).bind(row.profile_id,current.id,version.revision,JSON.stringify(version),row.profile_id,tag));
  }else writes.push(env.WORKSPACE_DB.prepare(`INSERT INTO personal_notes(profile_id,note_id,created_at_utc,revision,title,markdown,is_trashed,is_purged,at_utc) SELECT ?,?,'1970-01-01T00:00:00Z',?,NULL,NULL,0,1,'1970-01-01T00:00:00Z' WHERE ${guard}`).bind(row.profile_id,baseline.id,baseline.revision,row.profile_id,tag));}
  for(const baseline of marker.folders??[]){const folder=newBook.folders!.find(f=>f.id===baseline.id)!;writes.push(env.WORKSPACE_DB.prepare(`INSERT INTO note_folders(profile_id,folder_id,folder_json) SELECT ?,?,? WHERE ${guard}`).bind(row.profile_id,folder.id,JSON.stringify(folder),row.profile_id,tag));}
  writes.push(env.WORKSPACE_DB.prepare(`INSERT INTO notes_transfer_markers(profile_id,transfer_id,notebook_revision,marker_json) SELECT ?,?,?,? WHERE ${guard}`).bind(row.profile_id,id,marker.notebookRevision,JSON.stringify(marker),row.profile_id,tag));
 }
 for(const task of merged.fences)writes.push(env.WORKSPACE_DB.prepare(`INSERT OR IGNORE INTO workspace_transfer_fences(profile_id,task_id,transfer_id) SELECT ?,?,? WHERE ${guard}`).bind(row.profile_id,task,id,row.profile_id,tag));
 writes.push(env.WORKSPACE_DB.prepare(`UPDATE workspace_transfers SET state='accepted',receipt_json=? WHERE profile_id=? AND transfer_id=? AND review_hash=? AND ${guard}`).bind(JSON.stringify(receipt),row.profile_id,id,args.reviewHash,row.profile_id,tag));
 const results=await env.WORKSPACE_DB.batch(writes);if(!results[0].meta.changes){const current=await stored(env,row,id);if(current?.receipt_json)return JSON.parse(current.receipt_json);fail('transfer_conflict');}return receipt;
}
export async function exportWorkspaceTransfer(env:WorkspaceEnv,row:WorkspaceRow,id:string){const entry=await stored(env,row,uuid(id));if(!entry)fail('transfer_unavailable',404);await notesRow(env,row,JSON.parse(entry.manifest_json).includesNotes===true);return {version:'companion-adoption-backup-v1',artifact:await artifactText(env,row,id),receipt:entry.receipt_json?JSON.parse(entry.receipt_json):null};}

export async function transferContainsNotes(env:WorkspaceEnv,row:WorkspaceRow,id:string){const entry=await stored(env,row,uuid(id));return !!entry&&JSON.parse(entry.manifest_json).includesNotes===true;}
