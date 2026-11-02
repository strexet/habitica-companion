import {workspaceObject as object,uuid,WorkspaceError,workspaceTokenHash} from './workspace-http.ts';
import {workspaceDocument,type WorkspaceDocument,type Entity} from './workspace-domain.ts';
import {emptyNotes,noteText,noteProject,notesLimits,notesWireJson,type NotesNotebook,type NotesTransferMarker,type NoteVersion} from './notes.ts';
import {journal,type Journal} from '../companion-workspace/src/journal.ts';
export const transferVersion='companion-adoption-v1';
export const transferLimit=8*1024*1024;
export interface TransferArtifact {version:typeof transferVersion;transferId:string;ownerScope:string;targetWorkspaceId:string;workspaces:{workspaceId:string;archive:string}[];notes:string|null}
export interface TransferConflict {kind:string;id:string;sourceWorkspaceId:string;retainedSource:string}
export const byteLength=(value:string)=>new TextEncoder().encode(value).length;
function require(ok:unknown,code='invalid_transfer'):asserts ok {if(!ok)throw new WorkspaceError(code,422);}
export function validateTransferNotes(value:unknown):NotesNotebook{
 const row=object(value,['schemaVersion','ownerScope','revision','notes','receipts','purged','transfers']);
 require(row.schemaVersion===1&&typeof row.ownerScope==='string'&&/^[a-f0-9]{64}$/.test(row.ownerScope)&&Number.isSafeInteger(row.revision)&&Number(row.revision)>=0&&Array.isArray(row.notes)&&Array.isArray(row.receipts)&&Array.isArray(row.purged));
 require(row.notes.length<=notesLimits.notes&&row.receipts.length<=notesLimits.receipts+2*notesLimits.notes&&row.purged.length<=notesLimits.receipts);
 const ids=new Set<string>(),versions=new Map<string,number>();
 const version=(input:unknown)=>{const v=object(input,['revision','title','markdown','isTrashed','atUtc','project']);require(Number.isSafeInteger(v.revision)&&Number(v.revision)>0&&noteText(v.title,v.markdown)&&typeof v.isTrashed==='boolean'&&typeof v.atUtc==='string'&&/^\d{4}-.*(?:Z|\+00:00)$/.test(v.atUtc)&&Number.isFinite(Date.parse(v.atUtc)));if(v.project!=null)require(noteProject(v.project).ownerScope===row.ownerScope);return v;};
 for(const input of row.notes){const note=object(input,['id','createdAtUtc','current','history']),id=uuid(note.id),current=version(note.current);require(!ids.has(id)&&Array.isArray(note.history)&&note.history.length===Math.min(Number(current.revision)-1,notesLimits.history)&&typeof note.createdAtUtc==='string'&&Number.isFinite(Date.parse(note.createdAtUtc)));ids.add(id);let revision=Number(current.revision)-note.history.length,at=Date.parse(note.createdAtUtc);for(const input of [...note.history,note.current]){const v=version(input);require(v.revision===revision++&&Date.parse(String(v.atUtc))>=at);at=Date.parse(String(v.atUtc));}}
 for(const input of row.purged){const item=object(input,['id','revision']),id=uuid(item.id);require(!ids.has(id)&&Number.isSafeInteger(item.revision)&&Number(item.revision)>=2);ids.add(id);}
 const transfers=row.transfers??[];require(Array.isArray(transfers)&&transfers.length<=32);
 const changes=[...row.receipts.map(input=>({kind:'receipt',input})),...transfers.map(input=>({kind:'transfer',input}))].sort((a,b)=>Number((a.input as {notebookRevision:number}).notebookRevision)-Number((b.input as {notebookRevision:number}).notebookRevision));
 const commands=new Set<string>(),transferIds=new Set<string>();let revision=0;
 for(const change of changes){if(change.kind==='transfer'){const marker=object(change.input,['transferId','notebookRevision','notes']),id=uuid(marker.transferId);require(!transferIds.has(id)&&marker.notebookRevision===++revision&&Array.isArray(marker.notes));transferIds.add(id);for(const input of marker.notes){const baseline=object(input,['id','revision']),id=uuid(baseline.id);require(ids.has(id)&&!versions.has(id)&&Number.isSafeInteger(baseline.revision)&&Number(baseline.revision)>0);versions.set(id,Number(baseline.revision));}}else{const r=object(change.input,['commandId','payloadHash','noteId','noteRevision','notebookRevision']),id=uuid(r.commandId),noteId=uuid(r.noteId);require(!commands.has(id)&&ids.has(noteId)&&typeof r.payloadHash==='string'&&/^[a-f0-9]{64}$/.test(r.payloadHash)&&r.notebookRevision===++revision&&r.noteRevision===(versions.get(noteId)??0)+1);commands.add(id);versions.set(noteId,Number(r.noteRevision));}}
 require(revision===row.revision);for(const note of row.notes as NotesNotebook['notes'])require(versions.get(note.id)===note.current.revision);for(const item of row.purged as NotesNotebook['purged'])require(versions.get(item.id)===item.revision);
 require(byteLength(notesWireJson(row))<=notesLimits.bytes,'notebook_capacity');return row as unknown as NotesNotebook;
}
export async function parseTransfer(text:string):Promise<{artifact:TransferArtifact;sources:Journal[];notes:NotesNotebook|null;hash:string}>{
 require(byteLength(text)<=transferLimit,'transfer_capacity');let input:unknown;try{input=JSON.parse(text);}catch{throw new WorkspaceError('invalid_transfer',422);}
 const row=object(input,['version','transferId','ownerScope','targetWorkspaceId','workspaces','notes']);
 require(row.version===transferVersion&&typeof row.ownerScope==='string'&&/^[a-f0-9]{64}$/.test(row.ownerScope)&&Array.isArray(row.workspaces)&&row.workspaces.length<=32&&(row.notes===null||typeof row.notes==='string'));
 const transferId=uuid(row.transferId),targetWorkspaceId=uuid(row.targetWorkspaceId),ids=new Set<string>(),sources:Journal[]=[];
 for(const input of row.workspaces){const source=object(input,['workspaceId','archive']),id=uuid(source.workspaceId);require(!ids.has(id)&&typeof source.archive==='string');ids.add(id);const parsed=await journal(JSON.parse(source.archive));require(!parsed.document.sourceImports?.length,'nested_transfer_requires_original_archives');require(parsed.document.id===id&&parsed.authority?.mode==='frozen'&&parsed.authority.transferId===transferId&&!parsed.commands.some(entry=>['leased','executing'].includes(entry.projectionState)),'ownership_not_frozen');sources.push(parsed);}
 const notes=row.notes===null?null:validateTransferNotes(JSON.parse(String(row.notes)));require(notes===null||notes.ownerScope===row.ownerScope,'source_owner_mismatch');
 return {artifact:{version:transferVersion,transferId,ownerScope:row.ownerScope,targetWorkspaceId,workspaces:row.workspaces as TransferArtifact['workspaces'],notes:row.notes as string|null},sources,notes,hash:await workspaceTokenHash(text)};
}
// Current state is mapped; original journals and receipts stay byte-for-byte in immutable chunks.
export function mergeTransfer(target:WorkspaceDocument,targetNotes:NotesNotebook|null,sources:Journal[],localNotes:NotesNotebook|null,transferId:string,archiveConflicts:boolean){
 const document=structuredClone(target),conflicts:TransferConflict[]=[],mappings:{sourceWorkspaceId:string;targetWorkspaceId:string;revisionOffset:number}[]=[],fences=new Set<string>();
 const fields=['categories','projects','sections','labels','goals','tasks','occurrences','completions','restorations','habitScores','projections','savedTaskViews','reviewEvents','unarchives','occurrenceCorrections'] as const;
 const aliases=new Map(target.projects.filter(project=>project.alias).map(project=>[project.alias!.toLowerCase(),{source:'cloud',id:project.id}]));
 const owners=new Map<string,string>();for(const field of fields)for(const entity of document[field]??[])owners.set(entity.id,'cloud');
 for(const source of [...sources].sort((a,b)=>a.document.id.localeCompare(b.document.id))){
  const offset=document.revision;require(Number.isSafeInteger(offset+source.document.revision+1),'revision_exhausted');document.revision=offset+source.document.revision+1;
  mappings.push({sourceWorkspaceId:source.document.id,targetWorkspaceId:target.id,revisionOffset:offset});
  const recordIds:string[]=[];
  for(const field of fields){const merged:Entity[]=[...(document[field]??[])];for(const entity of source.document[field]??[]){const existing=owners.get(entity.id);if(existing){conflicts.push({kind:field,id:entity.id,sourceWorkspaceId:source.document.id,retainedSource:existing});continue;}owners.set(entity.id,source.document.id);const mapped={...entity,revision:entity.revision+offset};if(field==='projections')Object.assign(mapped,{correlationAlias:null});if(field==='projects'){const alias=(entity as {alias?:string}).alias;if(alias){const existing=aliases.get(alias.toLowerCase());if(existing){conflicts.push({kind:'projectAlias',id:entity.id,sourceWorkspaceId:source.document.id,retainedSource:existing.source+':'+existing.id});delete (mapped as {alias?:string}).alias;}else aliases.set(alias.toLowerCase(),{source:source.document.id,id:entity.id});}}if(field==='unarchives')Object.assign(mapped,{archivedRevision:(entity as {archivedRevision:number}).archivedRevision+offset});if(field==='occurrenceCorrections'){const previous=(entity as {previousOccurrence:Entity}).previousOccurrence;Object.assign(mapped,{previousOccurrence:{...previous,revision:previous.revision+offset}});}merged.push(mapped);recordIds.push(entity.id);}Object.assign(document,{[field]:merged});}
  document.sourceImports=[...(document.sourceImports??[]),{transferId,sourceWorkspaceId:source.document.id,sourceRevision:source.document.revision,revisionOffset:offset,recordIds}];
  for(const retired of source.document.sourceRetirements??[])fences.add(retired.sourceTaskId);
  for(const entry of source.commands)if(entry.effects.length>entry.confirmedEffects&&!['cancelled','notRequested'].includes(entry.projectionState)&&owners.get(entry.command.taskId)===source.document.id)fences.add(entry.command.taskId);
 }
 let notes=targetNotes?structuredClone(targetNotes):null;
 if(localNotes){require(notes!==null,'notes_consent_required');const marker:NotesTransferMarker={transferId,notebookRevision:notes.revision+1,notes:[]};
  require(Number.isSafeInteger(marker.notebookRevision)&&((notes.transfers??[]).length<32),'revision_exhausted');
  const existing=new Set([...notes.notes.map(note=>note.id),...notes.purged.map(note=>note.id)]);
  const rebind=(version:NoteVersion):NoteVersion=>{if(!version.project)return version;const link=version.project;require(link.ownerScope===localNotes.ownerScope&&link.source==='browser'&&(sources.some(source=>source.document.id===link.workspaceId&&source.document.projects.some(project=>project.id===link.projectId))||link.workspaceId===target.id&&target.projects.some(project=>project.id===link.projectId)),'note_project_source_missing');require(document.projects.some(project=>project.id===link.projectId),'note_project_target_missing');return {...version,project:{...link,source:'cloud',ownerScope:notes!.ownerScope,workspaceId:target.id}};};
  for(const note of localNotes.notes){if(existing.has(note.id)){conflicts.push({kind:'notes',id:note.id,sourceWorkspaceId:'browser-notes',retainedSource:'cloud'});continue;}existing.add(note.id);notes.notes.push({...note,current:rebind(note.current),history:note.history.map(rebind)});marker.notes.push({id:note.id,revision:note.current.revision});}
  for(const purged of localNotes.purged){if(existing.has(purged.id)){conflicts.push({kind:'purged',id:purged.id,sourceWorkspaceId:'browser-notes',retainedSource:'cloud'});continue;}existing.add(purged.id);notes.purged.push(purged);marker.notes.push(purged);}
  notes.revision++;notes.transfers=[...(notes.transfers??[]),marker];validateTransferNotes(notes);
 }
 require(conflicts.length<=512,'transfer_conflict_capacity');
 // Refused collisions are still reviewable; candidate validation waits for explicit archive policy.
 const valid=conflicts.length&&!archiveConflicts?null:workspaceDocument(document);
 return {document:valid,notes,conflicts,mappings,fences:[...fences],ready:conflicts.length===0||archiveConflicts};
}
