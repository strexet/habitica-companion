import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFile,writeFile} from 'node:fs/promises';
import {applyNote,noteCommand,noteHash,notesWireJson} from '../../shared/notes.ts';
import {applyFolder,folderCommand,folderHash} from '../../shared/notes-folders.ts';
import {fixture,enrolled,authorization,exchange,accounts,issuer,ticket,authPage,form,handle} from '../../chatgpt-auth/test/fixtures/oauth.mjs';

const origin='http://localhost:8789';
test('a separately connected Notes owner can discover an empty folder namespace without task access',async t=>{
 const f=await fixture(t,{workspace:true}),n=await connect(f,await enrolled(f));
 const response=await call(f,'/notes/folders',n.sessionToken,{query:''});assert.equal(response.status,200,await response.clone().text());
 const result=await response.json();assert.equal(result.notebookId,n.notebookId);assert.deepEqual(result.folders,[]);assert.equal(result.total,0);
});
const command=(revision=0,noteId=crypto.randomUUID(),operation='create',patch={title:'Measurements',markdown:'# Private\r\n🧥 size L\n'})=>({id:crypto.randomUUID(),noteId,expectedNotebookRevision:revision,expectedNoteRevision:operation==='create'?0:revision,operation,...patch});
const call=(f,path,token,input,method=input===undefined?'GET':'POST')=>f.workspace.fetch(origin+path,{method,headers:{authorization:`Bearer ${token}`,'content-type':'application/json'},body:input===undefined?undefined:JSON.stringify(path==='/notes/command'?{expectedNotebookId:f.notesSessions?.get(token),command:input}:path==='/notes/receipt'?{expectedNotebookId:f.notesSessions?.get(token),...input}:input)});
async function connect(f,p){const r=await call(f,'/notes/connect',p.managementToken,{consentVersion:'notes-v1'});assert.equal(r.status,201,await r.clone().text());const value=await r.json();(f.notesSessions??=new Map()).set(value.sessionToken,value.notebookId);return value;}
async function book(f,n){const r=await call(f,'/notes/export',n.sessionToken);assert.equal(r.status,200,await r.clone().text());return r.json();}
async function save(f,n,c){const r=await call(f,'/notes/command',n.sessionToken,c);assert.ok([200,201].includes(r.status),await r.clone().text());return r.json();}
async function token(f,p,scopes){const r=await exchange(f,await authorization(f,p,scopes));assert.equal(r.status,200);return (await r.json()).access_token;}
async function agent(f,token,operation,args={}){const binding=(await f.mf.getBindings('mcp')).WORKSPACE_SERVER;return binding.fetch('https://workspace.internal/notes',{method:'POST',headers:{authorization:`Bearer ${token}`},body:JSON.stringify({operation,arguments:args})});}
const folderInput=(n,revision,folderId=crypto.randomUUID(),operation='create',folderRevision=0,patch={name:'Hotels 🧥'})=>({id:crypto.randomUUID(),folderId,expectedNotebookRevision:revision,expectedFolderRevision:folderRevision,operation,ownerScope:n.ownerScope,source:'cloud',...patch});
const rawFolder=(f,n,c)=>call(f,'/notes/folder-command',n.sessionToken,{expectedNotebookId:n.notebookId,command:c});
async function saveFolder(f,n,c){const r=await rawFolder(f,n,c);assert.ok([200,201].includes(r.status),await r.clone().text());return r.json();}

test('portable folder C#/Worker wire preserves exact new hashes and the legacy combined journal',async()=>{
 const fixture=JSON.parse(await readFile(new URL('../../../tests/fixtures/notes/notes-folder-wire-v3.json',import.meta.url),'utf8'));let book=fixture.initial;
 for(const row of fixture.rows){const c=row.kind==='folder'?folderCommand(row.command):noteCommand(row.command);assert.equal(await(row.kind==='folder'?folderHash(c):noteHash(c)),row.hash);const result=await(row.kind==='folder'?applyFolder(book,c,row.atUtc):applyNote(book,c,row.atUtc));assert.deepEqual(result.book,row.notebook);book=result.book;}
 assert.equal(book.notes[0].current.folder,undefined);assert.equal(book.notes[0].history[0].folder.folderId,book.folders[0].id);assert.equal(book.folders[0].isRemoved,true);
});
test('D1 folder-only lifecycle and stable discovery preserve duplicate labels and original receipts after removal',async t=>{
 const f=await fixture(t,{workspace:true}),n=await connect(f,await enrolled(f));const first=folderInput(n,0),original=await saveFolder(f,n,first);
 for(let i=1;i<25;i++)await saveFolder(f,n,folderInput(n,i));
 const page=await(await call(f,'/notes/folders',n.sessionToken,{query:'HOTELS'})).json();assert.equal(page.total,25);assert.equal(page.folders.length,20);assert.equal(page.nextOffset,20);assert.equal(new Set(page.folders.map(r=>r.id)).size,20);
 assert.equal((await call(f,'/notes/folders',n.sessionToken,{query:'',offset:20})).status,409);const rest=await(await call(f,'/notes/folders',n.sessionToken,{query:'',offset:20,expectedRevision:25})).json();assert.equal(rest.folders.length,5);
 await saveFolder(f,n,folderInput(n,25,first.folderId,'rename',1,{name:'Interesting places 尾'}));await saveFolder(f,n,folderInput(n,26,first.folderId,'remove',2,{}));
 const recovered=await saveFolder(f,n,first);assert.deepEqual(recovered.receipt,original.receipt);assert.equal(recovered.disposition,'duplicate');
 const receipt=await(await call(f,'/notes/folder-receipt',n.sessionToken,{expectedNotebookId:n.notebookId,commandId:first.id})).json();assert.deepEqual(receipt.receipt,original.receipt);
 const exact=await(await call(f,'/notes/folder',n.sessionToken,{folderId:first.folderId})).json();assert.equal(exact.folder.isRemoved,true);assert.equal(exact.folder.name,'Interesting places 尾');
 const exported=await book(f,n);assert.equal(exported.schemaVersion,2);assert.equal(exported.notes.length,0);assert.equal(exported.folderReceipts.length,27);assert.equal(exported.receipts.length,0);
 assert.equal((await f.workspaceDb.prepare('SELECT count(*) n FROM workspace_commands').first()).n,0);
});
test('folder assignment, trash and detach preserve exact Markdown and independent no-folder filters',async t=>{
 const f=await fixture(t,{workspace:true}),n=await connect(f,await enrolled(f)),folder=folderInput(n,0);await saveFolder(f,n,folder);
 for(let i=0;i<25;i++)await save(f,n,command(i+1,undefined,'create',{title:'Hotel '+i,markdown:'EXACT\r\n🧥\n',...(i<22?{folder:{folderId:folder.folderId,ownerScope:n.ownerScope,source:'cloud'}}:{})}));
 const query={query:'exact',folderId:folder.folderId};const first=await(await call(f,'/notes/search',n.sessionToken,query)).json();assert.equal(first.total,22);assert.equal(first.results.length,20);assert.equal(first.results[0].folder.folderId,folder.folderId);
 const second=await(await call(f,'/notes/search',n.sessionToken,{...query,offset:20,expectedRevision:26})).json();assert.equal(second.results.length,2);assert.equal((await(await call(f,'/notes/search',n.sessionToken,{query:'',noFolder:true})).json()).total,3);
 const id=first.results[0].id;await save(f,n,{...command(26,id,'trash',{}),expectedNoteRevision:1});assert.equal((await rawFolder(f,n,folderInput(n,27,folder.folderId,'remove',1,{}))).status,409);
 await save(f,n,{...command(27,id,'restore',{}),expectedNoteRevision:2});await save(f,n,{...command(28,id,'update',{clearFolder:true}),expectedNoteRevision:3});
 const note=(await book(f,n)).notes.find(n=>n.id===id);assert.equal(note.current.folder,undefined);assert.equal(note.current.markdown,'EXACT\r\n🧥\n');assert.equal(note.history[0].folder.folderId,folder.folderId);
 assert.equal((await call(f,'/notes/search',n.sessionToken,{query:'',folderId:folder.folderId,noFolder:true})).status,422);
});
test('folder receipt failure atomically rolls back metadata and competing commands retain one original',async t=>{
 const f=await fixture(t,{workspace:true}),n=await connect(f,await enrolled(f)),first=folderInput(n,0);
 await f.workspaceDb.exec("CREATE TRIGGER fail_folder_receipt BEFORE INSERT ON note_folder_receipts BEGIN SELECT RAISE(ABORT,'synthetic folder rollback'); END");assert.equal((await rawFolder(f,n,first)).status,503);assert.equal((await book(f,n)).revision,0);assert.equal((await f.workspaceDb.prepare('SELECT count(*) n FROM note_folders').first()).n,0);await f.workspaceDb.exec('DROP TRIGGER fail_folder_receipt');
 const other=folderInput(n,0);const responses=await Promise.all([rawFolder(f,n,first),rawFolder(f,n,other)]);assert.deepEqual(responses.map(r=>r.status).sort(),[201,409]);const winner=responses[0].status===201?first:other;assert.equal((await book(f,n)).folders.length,1);assert.equal((await saveFolder(f,n,winner)).disposition,'duplicate');
 const b=await connect(f,await enrolled(f,accounts[1]));assert.equal((await call(f,'/notes/folder',b.sessionToken,{folderId:winner.folderId})).status,404);assert.equal((await rawFolder(f,n,{...folderInput(n,1),ownerScope:b.ownerScope})).status,403);
 for(const patch of [{source:'browser'},{name:'x'.repeat(257)},{name:'\ud800'},{parentFolderId:winner.folderId}])assert.ok([400,403,422].includes((await rawFolder(f,n,{...folderInput(n,1),...patch})).status));
});
test('project folders and placement require current exact authority, while retained stale membership remains editable',async t=>{
 const f=await fixture(t,{workspace:true}),p=await enrolled(f),n=await connect(f,p),w=await(await call(f,'/connect',p.managementToken,{consentVersion:'workspace-v1'})).json(),projectId=crypto.randomUUID();
 const row=await f.workspaceDb.prepare('SELECT document_json FROM workspaces WHERE profile_id=?').bind(p.profileId).first();const d=JSON.parse(row.document_json);d.projects=[{id:projectId,revision:1,name:'Trip',categoryId:null,isArchived:false}];await f.workspaceDb.prepare('UPDATE workspaces SET document_json=?,store_version=store_version+1 WHERE profile_id=?').bind(JSON.stringify(d),p.profileId).run();const project={projectId,workspaceId:w.workspaceId,source:'cloud',ownerScope:n.ownerScope};
 const c=folderInput(n,0,undefined,'create',0,{name:'Hotels',project});await saveFolder(f,n,c);const membership={folderId:c.folderId,source:'cloud',ownerScope:n.ownerScope};const note=command(1,undefined,'create',{title:'Exact',markdown:'Body\r\n🧥',project,folder:membership});await save(f,n,note);
 d.projects[0].isArchived=true;await f.workspaceDb.prepare('UPDATE workspaces SET document_json=?,store_version=store_version+1 WHERE profile_id=?').bind(JSON.stringify(d),p.profileId).run();
 await save(f,n,{...command(2,note.noteId,'update',{markdown:'Changed\r\n🧥'}),expectedNoteRevision:1});const original=await saveFolder(f,n,c);assert.equal(original.disposition,'duplicate');await save(f,n,{...command(3,note.noteId,'update',{clearFolder:true}),expectedNoteRevision:2});
 const attach={...command(4,note.noteId,'update',{folder:membership}),expectedNoteRevision:3};assert.equal((await call(f,'/notes/command',n.sessionToken,attach)).status,409);assert.equal((await rawFolder(f,n,folderInput(n,4,undefined,'create',0,{name:'New',project}))).status,409);
 const exported=await book(f,n);assert.deepEqual(exported.notes[0].current.project,project);assert.equal(exported.notes[0].current.folder,undefined);assert.equal(exported.notes[0].history[0].markdown,'Body\r\n🧥');
});
test('disconnect retains private folders but explicit notebook deletion removes all folder metadata and receipts',async t=>{
 const f=await fixture(t,{workspace:true}),p=await enrolled(f),n=await connect(f,p),c=folderInput(n,0);await saveFolder(f,n,c);assert.equal((await call(f,'/notes/disconnect',n.sessionToken,{})).status,200);assert.equal((await call(f,'/notes/folders',n.sessionToken,{query:''})).status,401);
 const again=await connect(f,p);assert.equal((await book(f,again)).folders[0].name,c.name);assert.equal((await call(f,'/notes',again.sessionToken,{confirmNotebookId:again.notebookId},'DELETE')).status,200);
 for(const table of ['notes_notebooks','note_folders','note_folder_receipts'])assert.equal((await f.workspaceDb.prepare('SELECT count(*) n FROM '+table).first()).n,0);
 const replacement=await connect(f,p);assert.notEqual(replacement.notebookId,n.notebookId);assert.equal((await call(f,'/notes/folder-command',replacement.sessionToken,{expectedNotebookId:n.notebookId,command:folderInput(replacement,0)})).status,409);
});
test('expired Notes sessions and integration profile deletion fence and erase private folder state',async t=>{
 const f=await fixture(t,{workspace:true}),p=await enrolled(f),n=await connect(f,p);await saveFolder(f,n,folderInput(n,0));
 await f.workspaceDb.prepare("UPDATE notes_sessions SET expires_at_utc='2000-01-01T00:00:00Z' WHERE profile_id=?").bind(p.profileId).run();assert.equal((await call(f,'/notes/folders',n.sessionToken,{query:''})).status,401);
 const fresh=await connect(f,p);assert.equal((await book(f,fresh)).folders.length,1);
 const deleted=await f.auth.fetch(`${issuer}/profiles/${p.profileId}`,{method:'DELETE',headers:{authorization:`Bearer ${p.managementToken}`}});assert.equal(deleted.status,200,await deleted.clone().text());
 for(const table of ['notes_notebooks','note_folders','note_folder_receipts'])assert.equal((await f.workspaceDb.prepare('SELECT count(*) n FROM '+table).first()).n,0);
 assert.equal((await call(f,'/notes/folders',fresh.sessionToken,{query:''})).status,401);
});
test('a project race rolls back a folder commit and lost D1 acknowledgment recovers the original accepted identity',async t=>{
 const {createRequire}=await import('node:module'),{fileURLToPath}=await import('node:url'),require=createRequire(new URL('../../chatgpt-auth/package.json',import.meta.url)),{build}=require('esbuild');
 const bundle=await build({entryPoints:[fileURLToPath(new URL('../src/notes-folders-api.ts',import.meta.url))],bundle:true,format:'esm',platform:'node',write:false,plugins:[{name:'unused-entrypoint',setup(b){b.onResolve({filter:/^cloudflare:workers$/},()=>({path:'entrypoint',namespace:'stub'}));b.onLoad({filter:/.*/,namespace:'stub'},()=>({contents:'export class WorkerEntrypoint {}',loader:'js'}));}}]});
 const {submitFolder}=await import('data:text/javascript;base64,'+Buffer.from(bundle.outputFiles[0].text).toString('base64'));
 const f=await fixture(t,{workspace:true}),p=await enrolled(f),n=await connect(f,p),w=await(await call(f,'/connect',p.managementToken,{consentVersion:'workspace-v1'})).json(),id=crypto.randomUUID();
 const record=await f.workspaceDb.prepare('SELECT document_json FROM workspaces WHERE profile_id=?').bind(p.profileId).first();const document=JSON.parse(record.document_json);document.projects=[{id,revision:1,name:'Trip',isArchived:false,categoryId:null}];
 const projectState=async archived=>{document.projects[0].isArchived=archived;await f.workspaceDb.prepare('UPDATE workspaces SET document_json=?,store_version=store_version+1 WHERE profile_id=?').bind(JSON.stringify(document),p.profileId).run();};await projectState(false);
 const command=folderInput(n,0,undefined,'create',0,{name:'Hotels',project:{projectId:id,workspaceId:w.workspaceId,ownerScope:n.ownerScope,source:'cloud'}}),row=await f.workspaceDb.prepare('SELECT * FROM notes_notebooks WHERE profile_id=?').bind(p.profileId).first(),env=await f.mf.getBindings('workspace');
 const wrap=after=>{const sql=new WeakMap();return{prepare:query=>{const stmt=env.WORKSPACE_DB.prepare(query);return{bind:(...args)=>{const bound=stmt.bind(...args);sql.set(bound,query);return bound;}};},batch:async statements=>{if(sql.get(statements[0])?.startsWith('UPDATE notes_notebooks SET revision='))return after(statements);return env.WORKSPACE_DB.batch(statements);}};};
 let enter,release;const ready=new Promise(r=>enter=r),gate=new Promise(r=>release=r),db=wrap(async statements=>{enter();await gate;return env.WORKSPACE_DB.batch(statements);});
 const pending=submitFolder({...env,WORKSPACE_DB:db},row,command);await Promise.race([ready,pending.then(()=>{throw Error('Folder commit did not enter the explicit SQL write gate');})]);await projectState(true);release();await assert.rejects(pending);assert.equal((await book(f,n)).revision,0);assert.equal((await f.workspaceDb.prepare('SELECT count(*) n FROM note_folders').first()).n,0);
 await projectState(false);const lost=wrap(async statements=>{await env.WORKSPACE_DB.batch(statements);throw Error('Synthetic lost folder acknowledgment');});await assert.rejects(submitFolder({...env,WORKSPACE_DB:lost},row,command));assert.equal((await book(f,n)).revision,1);assert.equal((await saveFolder(f,n,command)).disposition,'duplicate');
});

test('portable C# Notes commands have identical hashes, source text, lifecycle and receipt history',async()=>{
 const fixture=JSON.parse(await readFile(new URL('../../../tests/fixtures/notes/notes-wire-v1.json',import.meta.url),'utf8'));let book=fixture.initial;
 for(const row of fixture.rows){const c=noteCommand(row.command);assert.equal(await noteHash(c),row.hash);const result=await applyNote(book,c,row.atUtc);assert.deepEqual(result.book,row.notebook);book=result.book;assert.deepEqual(JSON.parse(notesWireJson(book)),book);}
 assert.equal(JSON.stringify(book).includes('Measurements'),false);
});
test('Notes require independent consent and sessions, isolate accounts, and never enter task exports',async t=>{
 const f=await fixture(t,{workspace:true}),a=await enrolled(f),b=await enrolled(f,accounts[1]);
 assert.equal((await call(f,'/notes/connect',a.managementToken,{})).status,403);
 assert.equal((await call(f,'/notes/export',a.managementToken)).status,401);
 const na=await connect(f,a),nb=await connect(f,b);assert.notEqual(na.notebookId,nb.notebookId);assert.notEqual(na.ownerScope,nb.ownerScope);
 const c=command();await save(f,na,c);assert.equal((await book(f,nb)).notes.length,0);
 assert.equal((await call(f,'/notes/get',nb.sessionToken,{noteId:c.noteId})).status,404);
 const task=await (await call(f,'/connect',a.managementToken,{consentVersion:'workspace-v1'})).json();
 assert.equal((await call(f,'/notes/export',task.sessionToken)).status,401);
 const taskExport=await (await call(f,'/export',task.sessionToken)).text();assert.ok(!taskExport.includes('Measurements'));assert.ok(!taskExport.includes(c.noteId));
 assert.equal((await call(f,'/workspace',na.sessionToken)).status,401);
 assert.ok(!f.messages.join('\n').includes('size L'));
});
test('original receipts survive lost responses and later edits, with atomic revision exclusion',async t=>{
 const f=await fixture(t,{workspace:true}),n=await connect(f,await enrolled(f)),c=command();const original=await save(f,n,c);
 const append=command(1,c.noteId,'append',{markdown:'\r\nextra\\'});await save(f,n,append);
 const duplicate=await save(f,n,c);assert.equal(duplicate.disposition,'duplicate');assert.deepEqual(duplicate.receipt,original.receipt);
 assert.equal((await call(f,'/notes/command',n.sessionToken,{...c,title:'different'})).status,409);
 const contenders=[command(2,c.noteId,'append',{markdown:'a'}),command(2,c.noteId,'append',{markdown:'b'})];
 assert.deepEqual((await Promise.all(contenders.map(c=>call(f,'/notes/command',n.sessionToken,c)))).map(r=>r.status).sort(),[201,409]);
 const recovered=await (await call(f,'/notes/receipt',n.sessionToken,{commandId:c.id})).json();assert.deepEqual(recovered.receipt,original.receipt);
 const current=await book(f,n);assert.equal(current.revision,3);assert.equal(current.receipts.length,3);assert.ok(current.notes[0].current.markdown.startsWith(c.markdown+'\r\nextra\\'));
});
test('failed receipt insertion rolls back content and revisions; retry recovers without partial history',async t=>{
 const f=await fixture(t,{workspace:true}),n=await connect(f,await enrolled(f)),c=command();
 await f.workspaceDb.exec("CREATE TRIGGER notes_receipt_failure BEFORE INSERT ON personal_note_receipts BEGIN SELECT RAISE(ABORT, 'synthetic_notes_failure'); END");
 assert.equal((await call(f,'/notes/command',n.sessionToken,c)).status,503);assert.equal((await book(f,n)).revision,0);
 assert.equal((await f.workspaceDb.prepare('SELECT count(*) n FROM personal_notes').first()).n,0);
 await f.workspaceDb.exec('DROP TRIGGER notes_receipt_failure');await save(f,n,c);assert.equal((await book(f,n)).notes.length,1);
});
test('search pages are bounded and revision-bound; trash, restore and purge retain only original metadata',async t=>{
 const f=await fixture(t,{workspace:true}),n=await connect(f,await enrolled(f));
 for(let i=0;i<22;i++)await save(f,n,command(i,crypto.randomUUID(),'create',{title:'Journal '+i,markdown:'secret-body-'+i+'🧥'.repeat(350)}));
 let r=await call(f,'/notes/search',n.sessionToken,{query:'SECRET-BODY',trash:false,offset:0});assert.equal(r.status,200);const page=await r.json();assert.equal(page.results.length,20);assert.equal(page.nextOffset,20);assert.equal(page.fullContentIncluded,false);assert.equal(page.results[0].contentTruncated,true);
 assert.equal((await call(f,'/notes/search',n.sessionToken,{query:'',offset:20})).status,409);
 const last=await (await call(f,'/notes/search',n.sessionToken,{query:'secret-body',offset:20,expectedRevision:22})).json();assert.equal(last.results.length,2);
 const id=page.results[0].id;await save(f,n,{...command(22,id,'trash',{}),expectedNoteRevision:1});
 assert.equal((await (await call(f,'/notes/search',n.sessionToken,{query:'secret-body-0',trash:false})).json()).total,0);
 assert.equal((await (await call(f,'/notes/search',n.sessionToken,{query:'secret-body-0',trash:true})).json()).total,1);
 await save(f,n,{...command(23,id,'restore',{}),expectedNoteRevision:2});
 await save(f,n,{...command(24,id,'trash',{}),expectedNoteRevision:3});
 await save(f,n,{...command(25,id,'purge',{}),expectedNoteRevision:4});
 const exported=await book(f,n);assert.equal(exported.purged.length,1);assert.ok(!JSON.stringify(exported).includes('secret-body-0'));
 assert.equal((await f.workspaceDb.prepare('SELECT count(*) n FROM personal_note_versions WHERE note_id=?').bind(id).first()).n,0);
 assert.equal((await f.workspaceDb.prepare('SELECT markdown FROM personal_notes WHERE note_id=?').bind(id).first()).markdown,null);
});
test('old grants, missing Notes consent, read-only grants and revoked epochs fail closed',async t=>{
 const f=await fixture(t,{workspace:true}),p=await enrolled(f),old=await token(f,p,'profile:read tasks:read habitica.tasks.write');
 assert.equal((await agent(f,old,'search',{query:''})).status,403);
 const read=await token(f,p,'profile:read notes.read');assert.equal((await agent(f,read,'search',{query:''})).status,403);
 const n=await connect(f,p),c=command();assert.equal((await agent(f,read,'command',c)).status,403);
 await save(f,n,c);
 assert.equal((await agent(f,read,'get',{noteId:c.noteId})).status,200);
 const write=await token(f,p,'profile:read notes.read notes.write');const accepted=await agent(f,write,'command',{expectedNotebookId:n.notebookId,command:command(1,c.noteId,'append',{markdown:'saved'})});assert.equal(accepted.status,201,await accepted.clone().text());
 await f.auth.fetch(`${issuer}/profiles/${p.profileId}/revoke-grants`,{method:'POST',headers:{authorization:`Bearer ${p.managementToken}`},body:JSON.stringify({confirm:'revoke-chatgpt-grants'})});
 assert.equal((await agent(f,write,'search',{query:''})).status,401);assert.equal((await book(f,n)).notes.length,1);
 await call(f,'/notes/disconnect',n.sessionToken,{});const fresh=await token(f,p,'profile:read notes.read');assert.equal((await agent(f,fresh,'search',{query:''})).status,403);
});
test('Notes disconnect revokes all old tabs; retained reconnect and profile deletion clean separate tables',async t=>{
 const f=await fixture(t,{workspace:true}),p=await enrolled(f),n=await connect(f,p),n2=await connect(f,p);const c=command();await save(f,n,c);
 assert.equal((await call(f,'/notes/disconnect',n2.sessionToken,{})).status,200);assert.equal((await call(f,'/notes/export',n.sessionToken)).status,401);
 const n3=await connect(f,p);assert.equal(n3.notebookId,n.notebookId);assert.equal((await book(f,n3)).notes.length,1);
 const disconnected=await f.auth.fetch(`${issuer}/profiles/${p.profileId}/disconnect`,{method:'POST',headers:{authorization:`Bearer ${p.managementToken}`}});assert.equal(disconnected.status,200,await disconnected.clone().text());
 assert.equal((await call(f,'/notes/export',n3.sessionToken)).status,401);assert.equal((await f.workspaceDb.prepare('SELECT active FROM notes_notebooks').first()).active,0);
 const current=await enrolled(f),n4=await connect(f,current);assert.equal((await book(f,n4)).notes.length,1);
 const deleted=await f.auth.fetch(`${issuer}/profiles/${p.profileId}`,{method:'DELETE',headers:{authorization:`Bearer ${current.managementToken}`}});assert.equal(deleted.status,200,await deleted.clone().text());
 for(const table of ['notes_notebooks','notes_sessions','personal_notes','personal_note_versions','personal_note_receipts'])assert.equal((await f.workspaceDb.prepare(`SELECT count(*) n FROM ${table}`).first()).n,0);
 assert.equal((await f.workspace.fetch(origin+'/cleanup',{method:'POST',body:JSON.stringify({profileId:p.profileId,connectionId:crypto.randomUUID(),deleteProfile:true})})).status,401);
});
test('a delayed request cannot use an old Notes epoch after disconnect and reconnect',async t=>{
 const f=await fixture(t,{workspace:true,identityGate:true}),p=await enrolled(f),n=await connect(f,p),gate=await f.mf.getWorker('identity-gate');
 await gate.fetch('http://gate.test/__gate/arm',{method:'POST'});const entered=gate.fetch('http://gate.test/__gate/entered');const pending=call(f,'/notes/command',n.sessionToken,command());
 try{assert.equal((await entered).status,204);assert.equal((await call(f,'/notes/disconnect',n.sessionToken,{})).status,200);const next=await connect(f,p);await gate.fetch('http://gate.test/__gate/release',{method:'POST'});assert.equal((await pending).status,401);assert.equal((await book(f,next)).revision,0);}finally{await gate.fetch('http://gate.test/__gate/release',{method:'POST'});}
});
test('failed cross-service profile deletion blocks access and reenrollment until original cleanup succeeds',async t=>{
 const f=await fixture(t,{workspace:true}),p=await enrolled(f),n=await connect(f,p);await save(f,n,command());
 await f.workspaceDb.exec("CREATE TRIGGER notes_delete_failure BEFORE DELETE ON notes_notebooks BEGIN SELECT RAISE(ABORT, 'synthetic_cleanup_failure'); END");
 const remove=()=>f.auth.fetch(`${issuer}/profiles/${p.profileId}`,{method:'DELETE',headers:{authorization:`Bearer ${p.managementToken}`}});
 const pending=await remove();assert.equal(pending.status,202);assert.deepEqual(await pending.json(),{accessBlocked:true,pendingCleanup:true});
 assert.equal((await call(f,'/notes/export',n.sessionToken)).status,401);
 const reenroll=await f.auth.fetch(issuer+'/enroll',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({...accounts[0],consentVersion:'integration-v1'})});assert.equal(reenroll.status,409);assert.equal((await reenroll.json()).error,'cleanup_pending');
 await f.workspaceDb.exec('DROP TRIGGER notes_delete_failure');assert.equal((await remove()).status,200);
 assert.equal((await f.workspaceDb.prepare('SELECT count(*) n FROM personal_notes').first()).n,0);
});
test('strict command fields and content limits cannot create notes, history or receipts',async t=>{
 const f=await fixture(t,{workspace:true}),n=await connect(f,await enrolled(f));
 for(const c of [{...command(),profileId:'foreign'},{...command(),markdown:'x'.repeat(128*1024+1)},{...command(),markdown:'\ud800'},{...command(),title:'x'.repeat(257)},{...command(),operation:'scoreHabit'}])assert.ok([400,422].includes((await call(f,'/notes/command',n.sessionToken,c)).status));
 const c=command();const response=await f.workspace.fetch(origin+'/notes/command',{method:'POST',headers:{authorization:`Bearer ${n.sessionToken}`},body:JSON.stringify({expectedNotebookId:n.notebookId,command:c}).replace('"operation":"create"','"operation":"update","operation":"create"')});assert.equal(response.status,400);
 assert.equal((await book(f,n)).revision,0);assert.equal((await f.workspaceDb.prepare('SELECT count(*) n FROM personal_note_receipts').first()).n,0);
});
test('a command reviewed for a deleted notebook cannot reach a replacement notebook at revision zero',async t=>{
 const f=await fixture(t,{workspace:true}),p=await enrolled(f),n=await connect(f,p),c=command();
 assert.equal((await call(f,'/notes',n.sessionToken,{confirmNotebookId:n.notebookId},'DELETE')).status,200);
 const replacement=await connect(f,p);assert.notEqual(replacement.notebookId,n.notebookId);
 const stale=await f.workspace.fetch(origin+'/notes/command',{method:'POST',headers:{authorization:`Bearer ${replacement.sessionToken}`},body:JSON.stringify({expectedNotebookId:n.notebookId,command:c})});
 assert.equal(stale.status,409);assert.equal((await book(f,replacement)).revision,0);
});
test('OAuth consent names Notes permissions and independent readable storage explicitly',async t=>{
 const f=await fixture(t,{workspace:true}),p=await enrolled(f),code=await ticket(f,p),scope='profile:read notes.read notes.write',page=await authPage(f,scope),first=await page.response.text();
 assert.match(first,/Can search and read sensitive Markdown, revisions and trash after separate Notes cloud consent/);
 const response=await form(f,page,{handle:handle(first),decision:'identify',code:code.code,scope});assert.equal(response.status,200);const html=await response.text();
 assert.match(html,/Personal Notes can contain sensitive personal facts/);
 assert.match(html,/The server can read stored titles, Markdown and retained revisions until permanent deletion/);
 assert.match(html,/name="scope" value="notes.read"/);assert.match(html,/name="scope" value="notes.write"/);assert.match(html,/Existing task access does not grant Notes access/);
 if(process.env.HABITICA_NOTES_CONSENT_HTML)await writeFile(process.env.HABITICA_NOTES_CONSENT_HTML,html.replace(/name="handle" value="[^"]+"/g,'name="handle" value="synthetic-unusable-handle"'));
});
