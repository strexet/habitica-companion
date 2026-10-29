import {purgeDiagnosticProfile} from './diagnostic-api';
import {WorkerEntrypoint} from 'cloudflare:workers';
import {applyNote,emptyNotes,noteCommand,noteText,notesConsentVersion,notesLimits,notesReadScope,notesWriteScope,notesWireJson,noteProject,type NoteProjectLink,type NoteProjectAuthority,type NoteReceipt,type NotesNotebook,type NoteVersion} from '../../shared/notes';
import {bearer,uuid,workspaceJson,workspaceObject,WorkspaceError,workspaceSessionToken,workspaceTokenHash} from '../../shared/workspace-http';

export interface NotesIdentity {profileId:string;connectionId:string;scopes?:string[];grantEpoch?:string}
export interface NotesRow {profile_id:string;notebook_id:string;owner_scope:string;connection_id:string;access_epoch:string;consent_version:string;revision:number;active:number}
const headers={'cache-control':'no-store','x-content-type-options':'nosniff'};
const json=(value:unknown,status=200)=>Response.json(value,{status,headers});
const profileId=(value:unknown)=>typeof value==='string'&&/^prf_[A-Za-z0-9_-]{43}$/.test(value);
async function quota(env:WorkspaceEnv,who:NotesIdentity,recovery:boolean):Promise<void>{
 const response=await env.AUTH_IDENTITY.fetch('https://identity.internal/traffic',{method:'POST',body:JSON.stringify({lane:'workspace',profileId:who.profileId,connectionId:who.connectionId,recovery}),signal:AbortSignal.timeout(5000)});
 if(!response.ok)throw new WorkspaceError(response.status===429?'try_later':'notes_unavailable',response.status===429?429:503);
}

async function identify(env:WorkspaceEnv,path:string,authorization?:string,value?:NotesIdentity):Promise<NotesIdentity>{
 const response=await env.AUTH_IDENTITY.fetch('https://identity.internal/'+path,{method:'POST',headers:{'content-type':'application/json',...(authorization?{authorization}:{})},body:value?JSON.stringify({profileId:value.profileId,connectionId:value.connectionId}):undefined,signal:AbortSignal.timeout(5000)});
 if(!response.ok)throw new WorkspaceError('unauthorized',401);
 const who=await response.json<NotesIdentity>();if(!profileId(who.profileId))throw new WorkspaceError('unauthorized',401);uuid(who.connectionId);return who;
}
async function connect(request:Request,env:WorkspaceEnv):Promise<Response>{
 const body=workspaceObject(await workspaceJson(request),['consentVersion']);if(body.consentVersion!==notesConsentVersion)throw new WorkspaceError('notes_consent_required',403);
 const who=await identify(env,'management',`Bearer ${bearer(request)}`),now=new Date().toISOString();
 const scope=await workspaceTokenHash(who.profileId);
 await env.WORKSPACE_DB.prepare(`INSERT INTO notes_notebooks(profile_id,notebook_id,owner_scope,connection_id,access_epoch,consent_version,active,commit_tag,consented_at_utc)
 VALUES(?,?,?,?,?,?,1,?,?) ON CONFLICT(profile_id) DO UPDATE SET access_epoch=CASE WHEN notes_notebooks.connection_id=excluded.connection_id AND notes_notebooks.active=1 THEN notes_notebooks.access_epoch ELSE excluded.access_epoch END,
 connection_id=excluded.connection_id,consent_version=excluded.consent_version,active=1,consented_at_utc=excluded.consented_at_utc`)
 .bind(who.profileId,crypto.randomUUID(),scope,who.connectionId,crypto.randomUUID(),notesConsentVersion,crypto.randomUUID(),now).run();
 const row=await env.WORKSPACE_DB.prepare('SELECT * FROM notes_notebooks WHERE profile_id=? AND connection_id=? AND active=1').bind(who.profileId,who.connectionId).first<NotesRow>();
 if(!row)throw new WorkspaceError('connection_changed',409);
 // Recheck connection identity before issuing a new tab credential.
 await identify(env,'epoch',undefined,who);
 const token=workspaceSessionToken(),hash=await workspaceTokenHash(token),expires=new Date(Date.now()+8*60*60*1000).toISOString();
 const result=await env.WORKSPACE_DB.batch([
  env.WORKSPACE_DB.prepare(`INSERT INTO notes_sessions(token_hash,profile_id,connection_id,access_epoch,created_at_utc,expires_at_utc)
   SELECT ?,profile_id,connection_id,access_epoch,?,? FROM notes_notebooks WHERE profile_id=? AND connection_id=? AND access_epoch=? AND active=1`).bind(hash,now,expires,who.profileId,who.connectionId,row.access_epoch),
  env.WORKSPACE_DB.prepare(`DELETE FROM notes_sessions WHERE profile_id=? AND (access_epoch<>? OR expires_at_utc<=? OR token_hash NOT IN (SELECT token_hash FROM notes_sessions WHERE profile_id=? ORDER BY created_at_utc DESC,rowid DESC LIMIT 8))`).bind(who.profileId,row.access_epoch,now,who.profileId)
 ]);
 if(!result[0].meta.changes)throw new WorkspaceError('connection_changed',409);
 return json({notebookId:row.notebook_id,ownerScope:row.owner_scope,accessEpoch:row.access_epoch,consentVersion:notesConsentVersion,sessionToken:token,expiresAtUtc:expires},201);
}
async function session(request:Request,env:WorkspaceEnv):Promise<NotesRow>{
 const hash=await workspaceTokenHash(bearer(request));
 const row=await env.WORKSPACE_DB.prepare(`SELECT n.* FROM notes_notebooks n JOIN notes_sessions s ON n.profile_id=s.profile_id WHERE s.token_hash=? AND s.expires_at_utc>? AND n.active=1 AND n.connection_id=s.connection_id AND n.access_epoch=s.access_epoch AND n.consent_version=?`).bind(hash,new Date().toISOString(),notesConsentVersion).first<NotesRow>();
 if(!row)throw new WorkspaceError('unauthorized',401);
 await identify(env,'epoch',undefined,{profileId:row.profile_id,connectionId:row.connection_id});return row;
}
export async function loadNotes(env:WorkspaceEnv,row:NotesRow):Promise<NotesNotebook>{
 const results=await env.WORKSPACE_DB.batch<Record<string,unknown>>([
  env.WORKSPACE_DB.prepare('SELECT revision FROM notes_notebooks WHERE profile_id=? AND notebook_id=? AND connection_id=? AND access_epoch=? AND active=1 AND consent_version=?').bind(row.profile_id,row.notebook_id,row.connection_id,row.access_epoch,notesConsentVersion),
  env.WORKSPACE_DB.prepare('SELECT * FROM personal_notes WHERE profile_id=? ORDER BY rowid').bind(row.profile_id),
  env.WORKSPACE_DB.prepare('SELECT note_id,version_json FROM personal_note_versions WHERE profile_id=? ORDER BY note_id,revision').bind(row.profile_id),
  env.WORKSPACE_DB.prepare('SELECT receipt_json FROM personal_note_receipts WHERE profile_id=? ORDER BY sequence').bind(row.profile_id),
 ]);
 const current=results[0].results[0] as {revision:number}|undefined;if(!current)throw new WorkspaceError('unauthorized',401);
 const book=emptyNotes(row.owner_scope);book.revision=current.revision;
 for(const value of results[1].results){
  const note=value as {note_id:string;created_at_utc:string;revision:number;title:string|null;markdown:string|null;is_trashed:number;is_purged:number;at_utc:string;project_json:string|null};
  if(note.is_purged){book.purged.push({id:note.note_id,revision:note.revision});continue;}
  if(note.title===null||note.markdown===null)throw new WorkspaceError('notes_unavailable',503);
  book.notes.push({id:note.note_id,createdAtUtc:note.created_at_utc,current:{revision:note.revision,title:note.title,markdown:note.markdown,isTrashed:Boolean(note.is_trashed),atUtc:note.at_utc,...(note.project_json?{project:noteProject(JSON.parse(note.project_json))}:{})},history:results[2].results.filter(version=>version.note_id===note.note_id).map(version=>JSON.parse(String(version.version_json)) as NoteVersion)});
 }
 for(const note of book.notes)for(const version of [note.current,...note.history])if(version.project){version.project=noteProject(version.project);if(version.project.ownerScope!==row.owner_scope)throw new WorkspaceError('notes_project_ownership_invalid',503);}
 book.receipts=results[3].results.map(value=>JSON.parse(String(value.receipt_json)) as NoteReceipt);
 return book;
}
export async function projectAuthority(env:WorkspaceEnv,row:NotesRow,link:NoteProjectLink){
 if(link.ownerScope!==row.owner_scope||link.source!=='cloud')throw new WorkspaceError('project_owner_or_source_mismatch',403);
 const workspace=await env.WORKSPACE_DB.prepare('SELECT workspace_id,store_version,access_epoch,document_json FROM workspaces WHERE profile_id=? AND connection_id=? AND active=1').bind(row.profile_id,row.connection_id).first<{workspace_id:string;store_version:number;access_epoch:string;document_json:string}>();
 if(!workspace||workspace.workspace_id!==link.workspaceId)throw new WorkspaceError('project_workspace_unavailable',409);
 const document=JSON.parse(workspace.document_json);if(document.id!==workspace.workspace_id||!Array.isArray(document.projects))throw new WorkspaceError('project_workspace_unavailable',409);const authority:NoteProjectAuthority={ownerScope:row.owner_scope,source:'cloud',workspaceId:workspace.workspace_id,projects:document.projects??[]};
 if(!authority.projects.some(p=>p.id===link.projectId&&p.isArchived===false&&(p.isRemoved===undefined||p.isRemoved===false)))throw new WorkspaceError('project_unavailable',409);
 return {authority,workspace};
}
export function requireProjectRead(who:NotesIdentity|undefined){if(who&&!who.scopes?.includes('tasks:read'))throw new WorkspaceError('project_read_scope_required',403);}
const guard='EXISTS(SELECT 1 FROM notes_notebooks WHERE profile_id=? AND commit_tag=? AND active=1)';
export async function submitNote(env:WorkspaceEnv,row:NotesRow,value:unknown,preview?:{grantEpoch:string}):Promise<Response>{
 const command=noteCommand(value),before=await loadNotes(env,row);
 const current=before.notes.find(n=>n.id===command.noteId)?.current.project;
 const changed=command.project&&notesWireJson(command.project)!==notesWireJson(current??null)&&!before.receipts.some(r=>r.commandId===command.id);
 const project=changed?await projectAuthority(env,row,command.project!):null;
 const result=await applyNote(before,command,new Date().toISOString(),project?.authority);
 const output=()=>json({notebookId:row.notebook_id,ownerScope:row.owner_scope,receipt:result.receipt,disposition:result.disposition,saved:true},result.disposition==='accepted'?201:200);
 if(result.disposition==='duplicate')return output();
 const tag=crypto.randomUUID(),parameters=[result.book.revision,tag,row.profile_id,row.notebook_id,row.connection_id,row.access_epoch,before.revision];
 const previewGuard=preview?' AND EXISTS(SELECT 1 FROM notes_preparations p WHERE p.profile_id=notes_notebooks.profile_id AND p.command_id=? AND p.notebook_id=notes_notebooks.notebook_id AND p.access_epoch=notes_notebooks.access_epoch AND p.grant_epoch=? AND p.command_json IS NOT NULL AND p.expires_at_utc>strftime(\'%Y-%m-%dT%H:%M:%fZ\',\'now\'))':'';
 if(preview)parameters.push(command.id,preview.grantEpoch);
 const projectGuard=project?' AND EXISTS(SELECT 1 FROM workspaces w WHERE w.profile_id=notes_notebooks.profile_id AND w.connection_id=notes_notebooks.connection_id AND w.workspace_id=? AND w.store_version=? AND w.access_epoch=? AND w.active=1)':'';
 if(project)parameters.push(project.workspace.workspace_id,project.workspace.store_version,project.workspace.access_epoch);
 const writes=[env.WORKSPACE_DB.prepare('UPDATE notes_notebooks SET revision=?,commit_tag=? WHERE profile_id=? AND notebook_id=? AND connection_id=? AND access_epoch=? AND active=1 AND revision=?'+previewGuard+projectGuard).bind(...parameters)];
 const previous=before.notes.find(note=>note.id===command.noteId);
 if(command.operation==='purge'){
  writes.push(env.WORKSPACE_DB.prepare(`UPDATE personal_notes SET title=NULL,markdown=NULL,project_json=NULL,revision=?,is_purged=1,at_utc=? WHERE profile_id=? AND note_id=? AND ${guard}`).bind(result.receipt.noteRevision,new Date().toISOString(),row.profile_id,command.noteId,row.profile_id,tag));
  writes.push(env.WORKSPACE_DB.prepare(`DELETE FROM personal_note_versions WHERE profile_id=? AND note_id=? AND ${guard}`).bind(row.profile_id,command.noteId,row.profile_id,tag));
 }else{
  const note=result.book.notes.find(note=>note.id===command.noteId)!;
  writes.push(env.WORKSPACE_DB.prepare(`INSERT INTO personal_notes(profile_id,note_id,created_at_utc,revision,title,markdown,is_trashed,at_utc,project_json)
   SELECT profile_id,?,?,?,?,?,?,?,? FROM notes_notebooks WHERE profile_id=? AND commit_tag=? AND active=1
   ON CONFLICT(profile_id,note_id) DO UPDATE SET revision=excluded.revision,title=excluded.title,markdown=excluded.markdown,is_trashed=excluded.is_trashed,at_utc=excluded.at_utc,project_json=excluded.project_json`)
   .bind(note.id,note.createdAtUtc,note.current.revision,note.current.title,note.current.markdown,Number(note.current.isTrashed),note.current.atUtc,note.current.project?JSON.stringify(note.current.project):null,row.profile_id,tag));
  if(previous)writes.push(env.WORKSPACE_DB.prepare(`INSERT INTO personal_note_versions(profile_id,note_id,revision,version_json) SELECT profile_id,?,?,? FROM notes_notebooks WHERE profile_id=? AND commit_tag=? AND active=1`).bind(note.id,previous.current.revision,JSON.stringify(previous.current),row.profile_id,tag));
  writes.push(env.WORKSPACE_DB.prepare(`DELETE FROM personal_note_versions WHERE profile_id=? AND note_id=? AND revision<=? AND ${guard}`).bind(row.profile_id,note.id,note.current.revision-notesLimits.history-1,row.profile_id,tag));
 }
 writes.push(env.WORKSPACE_DB.prepare(`INSERT INTO personal_note_receipts(profile_id,command_id,sequence,receipt_json) SELECT profile_id,?,?,? FROM notes_notebooks WHERE profile_id=? AND commit_tag=? AND active=1`).bind(command.id,result.receipt.notebookRevision,JSON.stringify(result.receipt),row.profile_id,tag));
 // Accepted previews retain only request/command identity; purge also erases every pending copy.
 writes.push(env.WORKSPACE_DB.prepare(`UPDATE notes_preparations SET command_json=NULL WHERE profile_id=? AND ${command.operation==='purge'?'note_id=?':'command_id=?'} AND ${guard}`).bind(row.profile_id,command.operation==='purge'?command.noteId:command.id,row.profile_id,tag));
 const committed=await env.WORKSPACE_DB.batch(writes);
 if(!committed[0].meta.changes){
  const recovered=await applyNote(await loadNotes(env,row),command,new Date().toISOString());
  if(recovered.disposition==='duplicate')return json({notebookId:row.notebook_id,ownerScope:row.owner_scope,receipt:recovered.receipt,disposition:'duplicate',saved:true});
  throw new WorkspaceError('stale_notebook',409);
 }
 return output();
}
export async function notesOperation(env:WorkspaceEnv,row:NotesRow,operation:string,input:unknown,who?:NotesIdentity):Promise<Response>{
 if(operation==='command'){
  const envelope=workspaceObject(input,['expectedNotebookId','command']);if(uuid(envelope.expectedNotebookId)!==row.notebook_id)throw new WorkspaceError('notebook_changed',409);
  if(noteCommand(envelope.command).project)requireProjectRead(who);return submitNote(env,row,envelope.command);
 }
 const allowed=operation==='search'?['query','trash','offset','expectedRevision','projectId','projectWorkspaceId','unassigned']:operation==='get'?['noteId']:operation==='receipt'?['expectedNotebookId','commandId']:[];
 const args=workspaceObject(input,allowed),book=await loadNotes(env,row),context={notebookId:row.notebook_id,ownerScope:row.owner_scope,revision:book.revision};
 if(operation==='export')return new Response(notesWireJson(book),{headers:{...headers,'content-type':'application/json'}});
 if(operation==='get'){
  const id=uuid(args.noteId),note=book.notes.find(note=>note.id===id);if(!note)throw new WorkspaceError('note_unavailable',404);
  return json({...context,note,contentHash:await workspaceTokenHash(note.current.markdown),historyLimit:notesLimits.history,historyTruncated:note.current.revision>notesLimits.history+1});
 }
 if(operation==='receipt'){
  if(uuid(args.expectedNotebookId)!==row.notebook_id)throw new WorkspaceError('notebook_changed',409);
  const id=uuid(args.commandId),receipt=book.receipts.find(value=>value.commandId===id);if(!receipt)throw new WorkspaceError('command_unavailable',404);return json({...context,receipt,saved:true});
 }
 if(operation==='search'){
  const offset=args.offset??0;if(typeof args.query!=='string'||args.query.length>256||!noteText('search',args.query)||args.trash!==undefined&&typeof args.trash!=='boolean'||!Number.isSafeInteger(offset)||Number(offset)<0||Number(offset)>notesLimits.notes)throw new WorkspaceError('invalid_search',422);
  if(args.expectedRevision!==undefined&&args.expectedRevision!==book.revision||Number(offset)>0&&args.expectedRevision!==book.revision)throw new WorkspaceError('stale_notebook',409);
  if(args.unassigned!==undefined&&typeof args.unassigned!=='boolean'||args.unassigned===true&&(args.projectId!==undefined||args.projectWorkspaceId!==undefined)||args.projectId===undefined&&args.projectWorkspaceId!==undefined)throw new WorkspaceError('invalid_project_filter',422);
  const projectId=args.projectId===undefined?undefined:uuid(args.projectId),projectWorkspaceId=projectId===undefined?undefined:uuid(args.projectWorkspaceId);
  const fold=(value:string)=>value.normalize('NFKC').toLowerCase(),query=fold(args.query);
  const matches=book.notes.filter(note=>note.current.isTrashed===(args.trash??false)&&(args.unassigned!==true||!note.current.project)&&(projectId===undefined||note.current.project?.projectId===projectId&&note.current.project.workspaceId===projectWorkspaceId&&note.current.project.source==='cloud')&&fold(note.current.title+'\n'+note.current.markdown).includes(query));
  const results=matches.slice(Number(offset),Number(offset)+20).map(note=>({id:note.id,title:note.current.title,noteRevision:note.current.revision,isTrashed:note.current.isTrashed,atUtc:note.current.atUtc,snippet:Array.from(note.current.markdown).slice(0,320).join(''),contentTruncated:Array.from(note.current.markdown).length>320,...(note.current.project?{project:note.current.project}:{})}));
  return json({...context,results,total:matches.length,nextOffset:Number(offset)+results.length<matches.length?Number(offset)+results.length:null,fullContentIncluded:false});
 }
 throw new WorkspaceError('not_found',404);
}
export async function notesRoute(request:Request,env:WorkspaceEnv):Promise<Response>{
 const path=new URL(request.url).pathname;
 if(path==='/notes/connect'&&request.method==='POST')return connect(request,env);
 const row=await session(request,env);
 await quota(env,{profileId:row.profile_id,connectionId:row.connection_id},path==='/notes/receipt');
 if(path==='/notes/disconnect'&&request.method==='POST'){
  workspaceObject(await workspaceJson(request),[]);
  const result=await env.WORKSPACE_DB.batch([env.WORKSPACE_DB.prepare('UPDATE notes_notebooks SET active=0,access_epoch=? WHERE profile_id=? AND notebook_id=? AND access_epoch=? AND active=1').bind(crypto.randomUUID(),row.profile_id,row.notebook_id,row.access_epoch),env.WORKSPACE_DB.prepare('DELETE FROM notes_sessions WHERE profile_id=? AND access_epoch=?').bind(row.profile_id,row.access_epoch),env.WORKSPACE_DB.prepare('UPDATE notes_preparations SET command_json=NULL WHERE profile_id=? AND access_epoch=?').bind(row.profile_id,row.access_epoch)]);
  if(!result[0].meta.changes)throw new WorkspaceError('unauthorized',401);return json({disconnected:true,retained:true});
 }
 if(path==='/notes'&&request.method==='DELETE'){
  const args=workspaceObject(await workspaceJson(request),['confirmNotebookId']);if(uuid(args.confirmNotebookId)!==row.notebook_id)throw new WorkspaceError('confirmation_required',409);
  const result=await env.WORKSPACE_DB.prepare('DELETE FROM notes_notebooks WHERE profile_id=? AND notebook_id=? AND access_epoch=? AND active=1').bind(row.profile_id,row.notebook_id,row.access_epoch).run();if(!result.meta.changes)throw new WorkspaceError('unauthorized',401);return json({deleted:true});
 }
 const operation=path.slice('/notes/'.length);
 if(request.method==='GET'&&operation==='export')return notesOperation(env,row,'export',{});
 if(request.method==='POST'&&['search','get','receipt','command'].includes(operation))return notesOperation(env,row,operation,await workspaceJson(request,1024*1024));
 throw new WorkspaceError('not_found',404);
}
// Private MCP entry invokes the same owner operations without accepting an account selector.
export async function notesAgentContext(request:Request,env:WorkspaceEnv,write:boolean,recovery=false):Promise<{row:NotesRow;who:NotesIdentity}>{
 const who=await identify(env,'mcp',request.headers.get('authorization')??'');
 const required=write?[notesReadScope,notesWriteScope]:[notesReadScope];
 if(!Array.isArray(who.scopes)||!who.scopes.includes('profile:read')||required.some(scope=>!who.scopes!.includes(scope)))throw new WorkspaceError('insufficient_scope',403);
 await quota(env,who,recovery);
 const row=await env.WORKSPACE_DB.prepare('SELECT * FROM notes_notebooks WHERE profile_id=? AND connection_id=? AND active=1 AND consent_version=?').bind(who.profileId,who.connectionId,notesConsentVersion).first<NotesRow>();
 if(!row)throw new WorkspaceError('notes_consent_required',403);
 return {row,who};
}
export async function agentNotes(request:Request,env:WorkspaceEnv,operation:string,args:unknown):Promise<Response>{
 const {row,who}=await notesAgentContext(request,env,operation==='command',operation==='receipt');return notesOperation(env,row,operation,args,who);
}
// Only Auth receives this binding. No public request dispatches this entrypoint.
export class NotesLifecycleService extends WorkerEntrypoint<WorkspaceEnv>{
 async fetch(request:Request):Promise<Response>{
  try{
   if(request.method!=='POST'||new URL(request.url).pathname!=='/cleanup')throw new WorkspaceError('unauthorized',401);
   const input=workspaceObject(await workspaceJson(request),['profileId','connectionId','deleteProfile']);
   if(!profileId(input.profileId)||typeof input.deleteProfile!=='boolean')throw new WorkspaceError('invalid_request',400);uuid(input.connectionId);
   if(!await purgeDiagnosticProfile(this.env,String(input.profileId)))throw new WorkspaceError('diagnostic_cleanup_pending',503);
   if(input.deleteProfile)await this.env.WORKSPACE_DB.prepare('DELETE FROM notes_notebooks WHERE profile_id=?').bind(input.profileId).run();
   else await this.env.WORKSPACE_DB.batch([this.env.WORKSPACE_DB.prepare('UPDATE notes_notebooks SET active=0,access_epoch=? WHERE profile_id=? AND connection_id=?').bind(crypto.randomUUID(),input.profileId,input.connectionId),this.env.WORKSPACE_DB.prepare('DELETE FROM notes_sessions WHERE profile_id=? AND connection_id=?').bind(input.profileId,input.connectionId),this.env.WORKSPACE_DB.prepare('UPDATE notes_preparations SET command_json=NULL WHERE profile_id=?').bind(input.profileId)]);
   if(input.deleteProfile)await this.env.WORKSPACE_DB.batch(['workspace_reminder_deliveries','workspace_reminder_commands','workspace_reminder_schedules','workspace_reminder_runtime'].map(table=>this.env.WORKSPACE_DB.prepare(`DELETE FROM ${table} WHERE profile_id=?`).bind(input.profileId)));
   else await this.env.WORKSPACE_DB.batch([
    this.env.WORKSPACE_DB.prepare("UPDATE workspace_reminder_schedules SET state='paused',reason='profile_disconnected',next_at_utc=NULL,next_civil_date=NULL WHERE profile_id=? AND connection_id=?").bind(input.profileId,input.connectionId),
    this.env.WORKSPACE_DB.prepare("UPDATE workspace_reminder_deliveries SET state=CASE WHEN state='dispatched' THEN 'unknown' ELSE 'cancelled' END,code='profile_disconnected' WHERE profile_id=? AND state IN ('scheduled','retryable','dispatched') AND EXISTS(SELECT 1 FROM workspace_reminder_schedules s WHERE s.profile_id=workspace_reminder_deliveries.profile_id AND s.schedule_id=workspace_reminder_deliveries.schedule_id AND s.connection_id=?)").bind(input.profileId,input.connectionId),
   ]);
   if(input.deleteProfile)await this.env.WORKSPACE_DB.prepare('DELETE FROM diagnostic_profiles WHERE profile_id=?').bind(input.profileId).run();
   return json({cleaned:true});
  }catch{return json({cleaned:false},503);}
 }
}
