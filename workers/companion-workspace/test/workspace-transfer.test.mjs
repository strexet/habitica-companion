import {transferSetup} from './fixtures/workspace-transfer.mjs';
import test from 'node:test';
import {writeFile,mkdir} from 'node:fs/promises';
import {join} from 'node:path';
import assert from 'node:assert/strict';
import {fixture,enrolled,accounts} from '../../chatgpt-auth/test/fixtures/oauth.mjs';
import {emptyWorkspace,workspaceDocument} from '../../shared/workspace-domain.ts';
import {workspaceCommand} from '../../shared/workspace-commands.ts';
import {accept} from '../src/commands.ts';
import {emptyNotes,applyNote,noteCommand} from '../../shared/notes.ts';
import {workspaceTokenHash} from '../../shared/workspace-http.ts';
import {validateTransferNotes} from '../../shared/workspace-transfer.ts';
import {seedLegacyCommand} from './fixtures/legacy-todo.mjs';
const origin='http://localhost:8789';

test('populated target retains identity, source bytes, Unicode Notes history and mapped current associations',async t=>{
 const x=await transferSetup(t),original=JSON.stringify(x.artifact),staged=await x.stage();assert.equal(staged.status,200,JSON.stringify(staged));assert.deepEqual(await x.stage(),staged);
 if(process.env.HABITICA_TRANSFER_FIXTURES){await mkdir(process.env.HABITICA_TRANSFER_FIXTURES,{recursive:true});await writeFile(join(process.env.HABITICA_TRANSFER_FIXTURES,'source-artifact.json'),JSON.stringify(x.artifact));await writeFile(join(process.env.HABITICA_TRANSFER_FIXTURES,'target-before-journal.json'),JSON.stringify((await x.call('/export')).body));}
 const prepared=await x.preview();assert.equal(prepared.status,200,JSON.stringify(prepared));assert.equal(prepared.body.ready,true);assert.equal(prepared.body.gameEffects,0);
 const accepted=await x.accept(prepared.body);assert.equal(accepted.status,200,JSON.stringify(accepted));assert.equal(accepted.body.state,'accepted');if(process.env.HABITICA_TRANSFER_FIXTURES){await writeFile(join(process.env.HABITICA_TRANSFER_FIXTURES,'accepted-receipt.json'),JSON.stringify(accepted.body));await writeFile(join(process.env.HABITICA_TRANSFER_FIXTURES,'review.json'),JSON.stringify(prepared.body));}
 const d=workspaceDocument(await x.doc());assert.equal(d.id,x.w.workspaceId);assert.equal(d.tasks.length,2);assert.equal(d.tasks.filter(task=>task.text==='Same title').length,2);assert.ok(d.projects.some(p=>p.id===x.projectId));
 const row=await x.f.workspaceDb.prepare('SELECT * FROM personal_notes WHERE profile_id=? AND note_id=?').bind(x.p.profileId,x.noteId).first();assert.equal(row.markdown,'Later 😀');const link=JSON.parse(row.project_json);assert.equal(link.workspaceId,x.w.workspaceId);assert.equal(link.source,'cloud');assert.notEqual(link.ownerScope,x.local.ownerScope);
 const versions=await x.f.workspaceDb.prepare('SELECT version_json FROM personal_note_versions WHERE profile_id=?').bind(x.p.profileId).all();assert.equal(JSON.parse(versions.results[0].version_json).project.workspaceId,x.w.workspaceId);
 const exported=await x.call('/workspace-transfer/export/'+x.id);assert.equal(exported.body.artifact,original);assert.equal(JSON.parse(exported.body.artifact).notes,JSON.stringify(x.local));assert.deepEqual(exported.body.receipt,accepted.body);
 const current=await x.f.workspaceDb.prepare('SELECT revision FROM notes_notebooks WHERE profile_id=?').bind(x.p.profileId).first();assert.equal(current.revision,1);assert.equal((await x.f.workspaceDb.prepare('SELECT COUNT(*) AS count FROM personal_note_receipts').first()).count,0);
 const notesToken=await x.call('/notes/recover',{},x.p.managementToken);const book=await x.call('/notes/export',undefined,notesToken.body.sessionToken);assert.equal(book.status,200,JSON.stringify(book));validateTransferNotes(book.body);if(process.env.HABITICA_TRANSFER_FIXTURES){await mkdir(process.env.HABITICA_TRANSFER_FIXTURES,{recursive:true});await writeFile(join(process.env.HABITICA_TRANSFER_FIXTURES,'accepted-journal.json'),JSON.stringify((await x.call('/export')).body));await writeFile(join(process.env.HABITICA_TRANSFER_FIXTURES,'accepted-notes.json'),JSON.stringify(book.body));}assert.equal(book.body.notes[0].current.revision,2);assert.equal(book.body.transfers[0].notes[0].revision,2);
 const after=await x.doc();after.revision++;await x.f.workspaceDb.prepare('UPDATE workspaces SET document_json=?,store_version=store_version+1 WHERE profile_id=?').bind(JSON.stringify(after),x.p.profileId).run();assert.deepEqual((await x.accept(prepared.body)).body,accepted.body);assert.deepEqual((await x.api('get_workspace_transfer_receipt',{transferId:x.id})).body,accepted.body);assert.equal(x.writes,0);
});
test('identity collisions need explicit archive review and preserve cloud plus exact source variants',async t=>{
 const x=await transferSetup(t,{collision:true,notes:false});assert.equal((await x.stage()).status,200);const blocked=await x.preview();assert.equal(blocked.body.ready,false);assert.ok(blocked.body.conflicts.some(c=>c.id===x.taskId));assert.notEqual((await x.accept(blocked.body)).status,200);
 const reviewed=await x.preview(true);assert.equal(reviewed.body.ready,true);assert.equal((await x.accept(reviewed.body)).body.state,'accepted');assert.equal((await x.doc()).tasks.length,1);assert.equal(JSON.parse((await x.call('/workspace-transfer/export/'+x.id)).body.artifact).workspaces[0].archive,x.artifact.workspaces[0].archive);assert.equal(x.writes,0);
});
for(const mode of ['hash','stale','expired','lease','notes-epoch','rollback','changed-bytes','owner','unfrozen','capacity'])test('transfer fail-closed control '+mode,async t=>{
 const x=await transferSetup(t),before=await x.doc();
 if(mode==='owner')x.artifact.ownerScope='a'.repeat(64);
 if(mode==='unfrozen'){const source=JSON.parse(x.artifact.workspaces[0].archive);source.authority.mode='local';source.authority.transferId=null;x.artifact.workspaces[0].archive=JSON.stringify(source);}
 if(mode==='capacity')x.artifact.notes='x'.repeat(8*1024*1024);
 const staged=await x.stage();if(['owner','unfrozen','capacity'].includes(mode)){assert.notEqual(staged.status,200);assert.deepEqual(await x.doc(),before);return;}
 assert.equal(staged.status,200,JSON.stringify(staged));const review=await x.preview();assert.equal(review.status,200,JSON.stringify(review));
 if(mode==='stale')await x.f.workspaceDb.prepare('UPDATE workspaces SET store_version=store_version+1').run();
 if(mode==='expired')await x.f.workspaceDb.prepare("UPDATE workspace_transfers SET expires_at_utc='2000-01-01T00:00:00Z',review_json=json_set(review_json,'$.expiresAtUtc','2000-01-01T00:00:00Z')").run();
 if(mode==='lease')await x.f.workspaceDb.prepare('INSERT INTO workspace_runtime(profile_id,access_epoch,lease_until_utc) VALUES(?,?,?) ON CONFLICT(profile_id) DO UPDATE SET lease_until_utc=excluded.lease_until_utc').bind(x.p.profileId,crypto.randomUUID(),new Date(Date.now()+90000).toISOString()).run();
 if(mode==='notes-epoch')await x.f.workspaceDb.prepare('UPDATE notes_notebooks SET access_epoch=?').bind(crypto.randomUUID()).run();
 if(mode==='rollback')await x.f.workspaceDb.prepare("CREATE TRIGGER fixture_transfer_fail BEFORE UPDATE OF receipt_json ON workspace_transfers BEGIN SELECT RAISE(ABORT,'synthetic atomic rollback'); END").run();
 if(mode==='changed-bytes'){x.artifact.notes=JSON.stringify({...x.local,ownerScope:'b'.repeat(64)});assert.notEqual((await x.stage()).status,200);}
 const response=await x.accept(mode==='hash'?{...review.body,reviewHash:'b'.repeat(64)}:review.body);
 if(mode==='changed-bytes')assert.equal(response.body.state,'accepted');else {assert.notEqual(response.body.state,'accepted',JSON.stringify(response));assert.deepEqual(await x.doc(),before);assert.equal((await x.f.workspaceDb.prepare('SELECT COUNT(*) AS count FROM personal_notes').first()).count,0);}
 if(mode==='rollback'){await x.f.workspaceDb.prepare('DROP TRIGGER fixture_transfer_fail').run();assert.equal((await x.accept(review.body)).body.state,'accepted');}
 assert.equal(x.writes,0);
});

test('multiple source Workspaces, removed projects, trash and purge retain original histories and receipts',async t=>{
 const x=await transferSetup(t);const {acceptOrganization}=await import('../src/organization-commands.ts');
 let source={...x.source,authority:null};const removed=await acceptOrganization(source,{id:crypto.randomUUID(),workspaceId:x.sourceId,expectedRevision:source.document.revision,kind:'project',operation:'remove',entityId:x.projectId,patch:{removal:{taskIds:[],sectionIds:[]}}},'2026-10-02T00:00:00Z');assert.equal(removed.disposition,'accepted');source=removed.snapshot;source.authority=x.source.authority;
 const second=emptyWorkspace();second.categories=[{id:crypto.randomUUID(),revision:1,name:'Other source',isArchived:false}];
 x.artifact.workspaces=[{workspaceId:x.sourceId,archive:JSON.stringify(source)},{workspaceId:second.id,archive:JSON.stringify({document:second,storeVersion:2,commands:[],events:[],authority:x.source.authority})}];
 let book=x.local;book=(await applyNote(book,noteCommand({id:crypto.randomUUID(),noteId:x.noteId,expectedNotebookRevision:book.revision,expectedNoteRevision:2,operation:'trash'}),'2026-10-02T00:00:00Z')).book;
 const purged=crypto.randomUUID();for(const operation of ['create','trash','purge'])book=(await applyNote(book,noteCommand({id:crypto.randomUUID(),noteId:purged,expectedNotebookRevision:book.revision,expectedNoteRevision:operation==='create'?0:operation==='trash'?1:2,operation,...(operation==='create'?{title:'Purge marker',markdown:'Sensitive fixture'}:{})}),'2026-10-02T01:00:00Z')).book;
 x.artifact.notes=JSON.stringify(book);assert.equal((await x.stage()).status,200);const review=await x.preview();assert.equal(review.status,200,JSON.stringify(review));const accepted=await x.accept(review.body);assert.equal(accepted.body.state,'accepted',JSON.stringify(accepted));const d=workspaceDocument(await x.doc());assert.equal(d.projects.find(p=>p.id===x.projectId).isRemoved,true);assert.equal(d.sourceImports.length,2);assert.equal(d.categories.length,1);if(process.env.HABITICA_TRANSFER_FIXTURES)await writeFile(join(process.env.HABITICA_TRANSFER_FIXTURES,'removed-journal.json'),JSON.stringify((await x.call('/export')).body));
 const exported=await x.call('/workspace-transfer/export/'+x.id);assert.equal(JSON.parse(exported.body.artifact).workspaces[0].archive,JSON.stringify(source));
 const original=await x.api('get_workspace_transfer_record',{transferId:x.id,kind:'taskCommand',sourceWorkspaceId:x.sourceId,commandId:x.source.commands[0].command.id});assert.deepEqual(original.body.original,x.source.commands[0]);const organization=await x.api('get_workspace_transfer_record',{transferId:x.id,kind:'organizationCommand',sourceWorkspaceId:x.sourceId,commandId:removed.entry.command.id});assert.deepEqual(organization.body.original,removed.entry);
 const receipt=await x.api('get_workspace_transfer_record',{transferId:x.id,kind:'noteReceipt',commandId:book.receipts.at(-1).commandId});assert.deepEqual(receipt.body.original,book.receipts.at(-1));
 const n=await x.call('/notes/recover',{},x.p.managementToken),cloud=await x.call('/notes/export',undefined,n.body.sessionToken);validateTransferNotes(cloud.body);assert.equal(cloud.body.notes[0].current.isTrashed,true);assert.equal(cloud.body.purged[0].id,purged);assert.equal(x.writes,0);
});
test('imported pending effect intents remain cold and block replacement game writes',async t=>{
 const x=await transferSetup(t,{notes:false});const d=emptyWorkspace(x.sourceId);const pending=await accept(d,workspaceCommand({id:crypto.randomUUID(),workspaceId:x.sourceId,expectedRevision:1,taskId:x.taskId,operation:'createTodo',patch:{text:'Pending import'},projectToHabitica:true}),'2026-10-01T00:00:00Z');
 x.artifact.workspaces=[{workspaceId:x.sourceId,archive:JSON.stringify({document:pending.document,storeVersion:3,commands:[pending.entry],events:[pending.event],authority:x.source.authority})}];assert.equal((await x.stage()).status,200);const review=await x.preview();assert.deepEqual(review.body.pausedTaskIds,[x.taskId]);assert.equal((await x.accept(review.body)).body.state,'accepted');
 const journals=await x.f.workspaceDb.prepare('SELECT command_id FROM workspace_commands').all();assert.ok(!journals.results.some(row=>row.command_id===pending.entry.command.id));
 const document=await x.doc();const change=await x.call('/commands',{id:crypto.randomUUID(),workspaceId:x.w.workspaceId,expectedRevision:document.revision,taskId:x.taskId,operation:'updateTask',patch:{text:'Do not replay'},projectToHabitica:true});assert.equal(change.body.error,'imported_effect_review_required');assert.equal(x.writes,0);
});
test('occurrence correction current history maps revisions while immutable source command bytes remain original',async t=>{
 const x=await transferSetup(t,{notes:false});let d=emptyWorkspace(x.sourceId),commands=[],events=[];const taskId=crypto.randomUUID(),at='2026-10-01T01:00:00Z',date='2026-10-01';
 async function apply(operation,extra={}){const command=workspaceCommand({id:crypto.randomUUID(),workspaceId:d.id,expectedRevision:d.revision,operation,taskId,...extra}),result=await accept(d,command,at,commands);d=result.document;commands.push(result.entry);events.push(result.event);return command;}
 await apply('createDaily',{patch:{text:'Daily correction',recurrence:{frequency:'daily',interval:1,startDate:date,timeZoneId:'UTC',weekdays:[],daysOfMonth:[]}}});const completed=await apply('completeTask',{occurrenceDate:date}),previous=d.occurrences[0];await apply('correctOccurrence',{patch:{correctOccurrence:{occurrenceId:previous.id,occurrenceRevision:previous.revision,targetCommandId:completed.id}}});
 const source={document:d,commands,events,storeVersion:d.revision+1,authority:x.source.authority};x.artifact.workspaces=[{workspaceId:d.id,archive:JSON.stringify(source)}];assert.equal((await x.stage()).status,200);const review=await x.preview();assert.equal(review.status,200,JSON.stringify(review));assert.equal((await x.accept(review.body)).body.state,'accepted');const snapshot=(await x.call('/export')).body;assert.equal(snapshot.document.occurrenceCorrections.length,1);assert.equal(snapshot.document.occurrences[0].state,'pending');const {journal}=await import('../src/journal.ts');await journal(snapshot);
 if(process.env.HABITICA_TRANSFER_FIXTURES){await mkdir(process.env.HABITICA_TRANSFER_FIXTURES,{recursive:true});await writeFile(join(process.env.HABITICA_TRANSFER_FIXTURES,'corrected-journal.json'),JSON.stringify(snapshot));}
 const original=await x.api('get_workspace_transfer_record',{transferId:x.id,kind:'taskCommand',sourceWorkspaceId:d.id,commandId:completed.id});assert.deepEqual(original.body.original,commands[1]);assert.equal(x.writes,0);
});
test('explicit Note purge erases every retained body fragment while exact task receipts remain readable',async t=>{
 const x=await transferSetup(t);assert.equal((await x.stage()).status,200);const review=await x.preview();assert.equal((await x.accept(review.body)).body.state,'accepted');const n=await x.call('/notes/recover',{},x.p.managementToken);
 const command=(operation,notebookRevision,noteRevision)=>x.call('/notes/command',{expectedNotebookId:n.body.notebookId,command:{id:crypto.randomUUID(),noteId:x.noteId,expectedNotebookRevision:notebookRevision,expectedNoteRevision:noteRevision,operation}},n.body.sessionToken);
 assert.equal((await command('trash',1,2)).status,201);assert.equal((await command('purge',2,3)).status,201);
 const parts=await x.f.workspaceDb.prepare('SELECT part,chunk FROM workspace_transfer_chunks WHERE profile_id=?').bind(x.p.profileId).all();assert.ok(!parts.results.some(p=>p.part==='note:'+x.noteId));assert.ok(!JSON.stringify(parts.results).includes('Later 😀'));assert.ok(!JSON.stringify(parts.results).includes('Body Unicode Ж'));
 assert.equal((await x.call('/workspace-transfer/export/'+x.id)).body.error,'transfer_source_notes_purged');
 const original=await x.api('get_workspace_transfer_record',{transferId:x.id,kind:'taskCommand',sourceWorkspaceId:x.sourceId,commandId:x.source.commands[0].command.id});assert.deepEqual(original.body.original,x.source.commands[0]);assert.equal(x.writes,0);
});
test('project alias collisions preserve exact canonical IDs and require reviewed source-alias archival',async t=>{
 const x=await transferSetup(t,{notes:false});const cloud=await x.doc();cloud.projects.push({id:crypto.randomUUID(),revision:cloud.revision,name:'Cloud project',categoryId:null,isArchived:false,alias:'same'});await x.f.workspaceDb.prepare('UPDATE workspaces SET document_json=? WHERE profile_id=?').bind(JSON.stringify(cloud),x.p.profileId).run();
 const source=JSON.parse(x.artifact.workspaces[0].archive);source.document.projects[0].alias='same';x.artifact.workspaces[0].archive=JSON.stringify(source);
 assert.equal((await x.stage()).status,200);const first=await x.preview();assert.equal(first.body.ready,false);assert.equal(first.body.conflicts[0].kind,'projectAlias');const review=await x.preview(true);assert.equal((await x.accept(review.body)).body.state,'accepted');const result=await x.doc();assert.equal(result.projects.find(p=>p.id===x.projectId).name,'Project Ж');assert.equal(result.projects.find(p=>p.id===x.projectId).alias,undefined);assert.equal(result.projects.filter(p=>p.alias==='same').length,1);assert.equal(x.writes,0);
});
test('Notebook deletion erases archived personal Notes without deleting source task provenance',async t=>{
 const x=await transferSetup(t);assert.equal((await x.stage()).status,200);const review=await x.preview();assert.equal((await x.accept(review.body)).body.state,'accepted');const n=await x.call('/notes/recover',{},x.p.managementToken);
 const deleted=await x.f.workspace.fetch(origin+'/notes',{method:'DELETE',headers:{authorization:'Bearer '+n.body.sessionToken,'content-type':'application/json'},body:JSON.stringify({confirmNotebookId:n.body.notebookId})});assert.equal(deleted.status,200);
 const parts=await x.f.workspaceDb.prepare('SELECT part FROM workspace_transfer_chunks WHERE profile_id=?').bind(x.p.profileId).all();assert.ok(parts.results.length>0);assert.ok(parts.results.every(part=>part.part==='tasks'));
 const original=await x.api('get_workspace_transfer_record',{transferId:x.id,kind:'taskCommand',sourceWorkspaceId:x.sourceId,commandId:x.source.commands[0].command.id});assert.deepEqual(original.body.original,x.source.commands[0]);assert.equal(x.writes,0);
});

test('fresh review fences an earlier unaccepted review without replacing transfer identity',async t=>{
 const x=await transferSetup(t);assert.equal((await x.stage()).status,200);const first=await x.preview(),second=await x.preview();assert.notEqual(first.body.reviewHash,second.body.reviewHash);assert.notEqual((await x.accept(first.body)).body.state,'accepted');assert.equal((await x.accept(second.body)).body.state,'accepted');assert.equal(x.writes,0);
});
test('legacy receipt discovery retains exact old transfer identity and refuses ID reuse',async t=>{
 const x=await transferSetup(t,{notes:false});const hash='a'.repeat(64);
 await x.f.workspaceDb.prepare('INSERT INTO workspace_handoffs(profile_id,transfer_id,archive_hash,workspace_id,accepted_at_utc) VALUES(?,?,?,?,?)').bind(x.p.profileId,x.id,hash,x.w.workspaceId,new Date().toISOString()).run();
 const receipt=await x.api('get_workspace_transfer_receipt',{transferId:x.id});assert.equal(receipt.body.version,'workspace-handoff-v1',JSON.stringify(receipt));assert.equal(receipt.body.archiveHash,hash);assert.equal(receipt.body.transferId,x.id);assert.equal((await x.api('get_workspace_transfer_status',{transferId:x.id})).body.state,'accepted');assert.notEqual((await x.stage()).status,200);assert.equal(x.writes,0);
});
