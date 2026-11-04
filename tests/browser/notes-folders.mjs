// Actual IndexedDB adapter, synthetic journals, isolated loopback origin and persistent profiles.
import assert from 'node:assert/strict';
import {createServer} from 'node:http';
import {readFile,mkdtemp,rm,writeFile} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {createHash} from 'node:crypto';
import {createRequire} from 'node:module';
const require=createRequire(import.meta.url);
const playwright=require(process.env.PLAYWRIGHT_MODULE||'/Users/petr/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root=new URL('../../',import.meta.url),storagePath='src/Habitica.WebApp/wwwroot/js/storage/indexedDbStorage.js',fixturePath='tests/fixtures/notes/notes-folder-wire-v3.json';
const allowed=new Map([['/storage.js',storagePath],['/vendor/dexie.mjs','src/Habitica.WebApp/wwwroot/vendor/dexie.mjs'],['/fixture.json',fixturePath]]);
// Preserve the production adapter's relative Dexie import.
allowed.set('/js/storage/indexedDbStorage.js',storagePath);
const hash=async path=>createHash('sha256').update(await readFile(new URL(path,root))).digest('hex');
const identity={projectRoot:new URL('.',root).pathname,storageSha256:await hash(storagePath),fixtureSha256:await hash(fixturePath),node:process.version,pid:process.pid,startedAt:new Date().toISOString()};
const server=createServer(async(req,res)=>{try{const path=new URL(req.url,'http://fixture.invalid').pathname;if(path==='/'){res.setHeader('Content-Type','text/html');res.end('<!doctype html><title>Isolated Notes folders storage</title>');return;}const file=allowed.get(path);if(!file){res.writeHead(404);res.end();return;}res.setHeader('Content-Type',path.endsWith('.json')?'application/json':'text/javascript');res.end(await readFile(new URL(file,root)));}catch{res.writeHead(500);res.end();}});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));const origin='http://127.0.0.1:'+server.address().port;identity.origin=origin;
const directory=await mkdtemp(join(tmpdir(),'habitica-notes-folders-'));let context;const results=[];const timer=setTimeout(()=>{console.error('Owned fixture deadline exceeded');process.exitCode=124;context?.close();server.closeAllConnections();server.close();},90000);
try{
 for(const engine of ['chromium','webkit']){
  context=await playwright[engine].launchPersistentContext(join(directory,engine),{headless:true});await context.route('**/*',route=>route.request().url().startsWith(origin+'/')?route.continue():route.abort());const page=await context.newPage();await page.goto(origin);
  const checks=await page.evaluate(async()=>{
   const {default:Dexie}=await import('/vendor/dexie.mjs');const legacy=new Dexie('habitica-tool');legacy.version(1).stores({kv:'&key, updatedAtUtc'});await legacy.table('kv').put({key:'unrelated-folder-canary',jsonText:'preserved'});legacy.close();
   const api=await import('/js/storage/indexedDbStorage.js');const {rows,initial}=await(await fetch('/fixture.json')).json();const scope=initial.ownerScope;const checks=[];const check=(ok,label)=>{if(!ok)throw Error(label);checks.push(label);};
   check(await api.getJson('unrelated-folder-canary')==='preserved','Legacy IndexedDB upgrade preserves unrelated store data');
   let lost=false;try{await api.commitNotes(scope,0,JSON.stringify(rows[0].notebook));throw Error('synthetic acknowledgment lost');}catch{lost=true;}
   const recovered=JSON.parse(await api.getNotes(scope));check(lost&&recovered.folderReceipts[0].commandId===rows[0].command.id&&recovered.notes.length===0,'Lost folder-only acknowledgment retains the original receipt without a fabricated note');
   check(!await api.commitNotes(scope,0,JSON.stringify(rows[0].notebook)),'Original folder-only revision cannot commit twice');
   check(await api.commitNotes(scope,1,JSON.stringify(rows[1].notebook)),'Exact note membership commits after a folder revision');
   const before=await api.getNotes(scope),put=IDBObjectStore.prototype.put;
   IDBObjectStore.prototype.put=function(...args){if(this.name==='notes')throw new DOMException('Synthetic folder quota failure','QuotaExceededError');return put.apply(this,args);};let aborted=false;
   try{await api.commitNotes(scope,2,JSON.stringify(rows[2].notebook));}catch{aborted=true;}finally{IDBObjectStore.prototype.put=put;}
   check(aborted&&await api.getNotes(scope)===before,'An aborted folder rename preserves the complete previous journal');
   const race=await Promise.all([api.commitNotes(scope,2,JSON.stringify(rows[2].notebook)),api.commitNotes(scope,2,JSON.stringify(rows[2].notebook))]);check(race.filter(Boolean).length===1,'Concurrent folder writers admit one original revision');
   for(const row of rows.slice(3))check(await api.commitNotes(scope,row.notebook.revision-1,JSON.stringify(row.notebook)),'Portable '+row.kind+' revision '+row.notebook.revision+' commits');
   const final=rows.at(-1).notebook;const next=structuredClone(final);next.revision++;const note=next.notes[0];note.history.push(note.current);note.current={...note.current,revision:note.current.revision+1};next.receipts.push({...next.receipts.at(-1),commandId:crypto.randomUUID(),noteRevision:note.current.revision,notebookRevision:next.revision});
   const mutate=async(value,label)=>{let refused=false;try{await api.commitNotes(scope,final.revision,JSON.stringify(value));}catch{refused=true;}check(refused&&await api.getNotes(scope)===JSON.stringify(final),label);};
   const rewrite=structuredClone(next);rewrite.folderReceipts[0].payloadHash='f'.repeat(64);await mutate(rewrite,'Accepted folder receipts remain immutable after removal');
   const downgrade=structuredClone(next);downgrade.schemaVersion=1;delete downgrade.folders;delete downgrade.folderReceipts;await mutate(downgrade,'A legacy writer cannot drop folder metadata');
   const resurrection=structuredClone(next);resurrection.folders[0].isRemoved=false;await mutate(resurrection,'Removed folder tombstones cannot be resurrected by a later writer');
   check(await api.getNotes('f'.repeat(64))===null,'Folder journals remain account separated');
   // A separate account exercises the actual transactional project check, including a changed Workspace version.
   const account='synthetic-browser-folder-project';const digest=await crypto.subtle.digest('SHA-256',new TextEncoder().encode(account));const owner=Array.from(new Uint8Array(digest),b=>b.toString(16).padStart(2,'0')).join('');const workspaceId=crypto.randomUUID(),projectId=crypto.randomUUID(),folderId=crypto.randomUUID();
   const document={schemaVersion:1,id:workspaceId,revision:1,tasks:[],projects:[{id:projectId,revision:1,name:'Synthetic project',isArchived:false}]};
   const journal={document,storeVersion:1,commands:[],events:[],authority:null};check(await api.commitWorkspace(account,0,JSON.stringify(journal)),'Synthetic canonical project authority is isolated and persisted');
   const project={projectId,workspaceId,source:'browser',ownerScope:owner};const projectBook={...rows[0].notebook,ownerScope:owner,folders:[{...rows[0].notebook.folders[0],id:folderId,project}],folderReceipts:[{...rows[0].notebook.folderReceipts[0],folderId}]};const guard={accountScope:account,workspaceId,projectId,folderId,workspaceStoreVersion:2};
   check(!await api.commitNotes(owner,0,JSON.stringify(projectBook),guard)&&await api.getNotes(owner)===null,'Stale Workspace authority rolls back a new project folder');
   check(await api.commitNotes(owner,0,JSON.stringify(projectBook),{...guard,workspaceStoreVersion:1}),'Exact current canonical authority permits a project-folder commit');
   const renamed={...projectBook,revision:2,folders:[{...projectBook.folders[0],name:'Renamed',revision:2}],folderReceipts:[...projectBook.folderReceipts,{...projectBook.folderReceipts[0],commandId:crypto.randomUUID(),folderRevision:2,notebookRevision:2}]};
   const archived={...journal,storeVersion:2,document:{...document,projects:[{...document.projects[0],isArchived:true}]}};check(await api.commitWorkspace(account,1,JSON.stringify(archived)),'A concurrent canonical project archival persists');
   check(!await api.commitNotes(owner,1,JSON.stringify(renamed),{...guard,workspaceStoreVersion:2})&&JSON.parse(await api.getNotes(owner)).revision===1,'Archived project authority refuses a guarded folder write without changing Notes');
   let foreign=false;try{await api.commitNotes(owner,1,JSON.stringify(renamed),{...guard,accountScope:'foreign-owner',workspaceStoreVersion:2});}catch{foreign=true;}check(foreign,'Foreign account proof cannot authorize project folders');
   window.folderExpected=JSON.stringify(final);window.folderScope=scope;return checks;
  });
  const expected=await page.evaluate(()=>({json:window.folderExpected,scope:window.folderScope}));await page.reload();const restored=await page.evaluate(async scope=>{const api=await import('/js/storage/indexedDbStorage.js');return{json:await api.getNotes(scope),canary:await api.getJson('unrelated-folder-canary')};},expected.scope);assert.equal(restored.json,expected.json);assert.equal(restored.canary,'preserved');checks.push('Exact Markdown, historical folder references and removed identities survive reload');
  await page.evaluate(async()=>{const api=await import('/js/storage/indexedDbStorage.js');await api.clearNotes();if(await api.getJson('unrelated-folder-canary')!=='preserved')throw Error('Unrelated store was cleared');});checks.push('Explicit Notes cleanup preserves unrelated stores');
  results.push({engine,checks});await context.close();context=null;
 }
}finally{clearTimeout(timer);await context?.close();server.closeAllConnections();await new Promise(resolve=>server.close(resolve));await rm(directory,{recursive:true,force:true});}
const count=results.reduce((n,row)=>n+row.checks.length,0);const report={identity,results,counts:{expected:count,discovered:count,passed:count,failed:0,skipped:0},cleanup:'Owned loopback server and both persistent browser contexts closed; generated temporary profiles removed. No actual user database, credentials, network service or device permission used.'};
report.artifactPath='.skill-tree/history/validation/notes-folders-2026-11-02/notes-indexeddb-'+Date.now()+'-'+process.pid+'.json';
await writeFile(new URL(report.artifactPath,root),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report));
