// Real IndexedDB, synthetic records, isolated loopback origin; no app account access.
import assert from 'node:assert/strict';
import {createServer} from 'node:http';
import {readFile} from 'node:fs/promises';
import {createRequire} from 'node:module';
const require=createRequire(import.meta.url);
const {chromium}=require(process.env.PLAYWRIGHT_MODULE || '/Users/petr/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root=new URL('../../src/Habitica.WebApp/wwwroot/',import.meta.url);
const server=createServer(async(req,res)=>{try{const path=new URL(req.url,'http://fixture.invalid').pathname; if(path==='/'){res.setHeader('Content-Type','text/html');res.end('<!doctype html><title>Synthetic storage</title>');return;}if(!['/js/storage/indexedDbStorage.js','/vendor/dexie.mjs'].includes(path)){res.writeHead(404);res.end();return;}res.setHeader('Content-Type','text/javascript');res.end(await readFile(new URL(path.slice(1),root)));}catch{res.writeHead(500);res.end();}});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
let browser;const deadline=setTimeout(()=>process.exit(124),45000);
try{
 browser=await chromium.launch({headless:true});const page=await browser.newPage();await page.goto('http://127.0.0.1:'+server.address().port);
 const results=await page.evaluate(async()=>{
  const api=await import('/js/storage/indexedDbStorage.js');const checks=[];
  function check(condition,label){if(!condition)throw Error(label);checks.push(label);}
  const a='00000000-0000-4000-8000-000000000011',b='00000000-0000-4000-8000-000000000012';
  const empty=id=>JSON.stringify({document:{schemaVersion:1,id,revision:1,tasks:[]},storeVersion:1,commands:[],events:[],authority:null});
  const first='10000000-0000-4000-8000-000000000001',second='10000000-0000-4000-8000-000000000002';
  const race=await Promise.all([api.ensureDefaultWorkspace(a,empty(first)),api.ensureDefaultWorkspace(a,empty(second))]);
  check(race[0].workspaceId===race[1].workspaceId&&(await api.listWorkspaces(a)).length===1,'Concurrent first-use tabs create exactly one default Workspace');
  check((await api.listWorkspaces(b)).length===0,'Default selection isolates accounts');
  await api.commitWorkspace(a,0,empty(second===race[0].workspaceId?first:second));
  const ambiguity=await api.ensureDefaultWorkspace(a,empty(crypto.randomUUID()));
  check(ambiguity.requiresConsolidation&&ambiguity.workspaceId===null&&(await api.listWorkspaces(a)).length===2,'Multiple legacy IDs remain unchanged and require consolidation');
  const frozen=JSON.parse(empty(crypto.randomUUID()));frozen.authority={mode:'frozen',transferId:crypto.randomUUID(),cloudWorkspaceId:null};
  await api.commitWorkspace(b,0,JSON.stringify(frozen));
  const recovery=await api.ensureDefaultWorkspace(b,empty(crypto.randomUUID()));
  check(recovery.requiresRecovery&&recovery.workspaceId===null&&(await api.listWorkspaces(b)).length===1,'Frozen authority cannot spawn a writable default');
  const pref=JSON.stringify({version:1,accounts:{}});
  const cas=await Promise.all([api.commitCompanionPreferences(null,pref),api.commitCompanionPreferences(null,pref)]);
  check(cas.filter(Boolean).length===1,'Preference compare-and-swap admits exactly one concurrent writer');
  let refused=false;try{await api.commitCompanionPreferences(pref,JSON.stringify({version:1,accounts:{x:{apiToken:'private'}}}));}catch{refused=true;}
  check(refused&&await api.getJson('preferences/companionAccounts')===pref,'Private capability fields leave stored preferences unchanged');
  const original=IDBObjectStore.prototype.put;IDBObjectStore.prototype.put=function(...args){if(this.name==='kv')throw new DOMException('Synthetic quota failure','QuotaExceededError');return original.apply(this,args);};
  let failed=false;try{await api.commitCompanionPreferences(pref,JSON.stringify({version:1,accounts:{changed:true}}));}catch{failed=true;}finally{IDBObjectStore.prototype.put=original;}
  check(failed&&await api.getJson('preferences/companionAccounts')===pref,'Aborted preference transaction preserves the previous record');
  const digest=await crypto.subtle.digest('SHA-256',new TextEncoder().encode(a));const owner=Array.from(new Uint8Array(digest),v=>v.toString(16).padStart(2,'0')).join('');
  const noteId=crypto.randomUUID(),receiptId=crypto.randomUUID(),created='2026-10-01T00:00:00Z';
  const book={schemaVersion:1,ownerScope:owner,revision:1,notes:[{id:noteId,createdAtUtc:created,current:{revision:1,title:'Заметка 😀',markdown:'Preserve original body',isTrashed:false,atUtc:created},history:[]}],receipts:[{commandId:receiptId,noteId,payloadHash:'a'.repeat(64),noteRevision:1,notebookRevision:1}],purged:[]};
  check(await api.commitNotes(owner,0,JSON.stringify(book)),'Synthetic Notes fixture commits before transfer');
  const inventory=await api.getWorkspaceTransferInventory(a,owner),transferId=crypto.randomUUID(),target=crypto.randomUUID();
  const plans=inventory.journals.map(beforeJson=>{const journal=JSON.parse(beforeJson);journal.storeVersion++;journal.authority={mode:'frozen',transferId,cloudWorkspaceId:null};return{beforeJson,frozenJson:JSON.stringify(journal)};});
  const originalPut=IDBObjectStore.prototype.put;let quotaInjected=false;
  IDBObjectStore.prototype.put=function(...args){if(this.name==='kv'&&args[0]?.key?.startsWith('companion-adoption-v1:')){quotaInjected=true;throw new DOMException('Synthetic freeze failure','QuotaExceededError');}return originalPut.apply(this,args);};
  let aborted=false;try{await api.freezeWorkspaceTransfer(a,owner,transferId,target,plans);}catch{aborted=true;}finally{IDBObjectStore.prototype.put=originalPut;}
  const rolled=await api.getWorkspaceTransferInventory(a,owner);
  check(aborted&&quotaInjected&&rolled.pendingJson===null&&JSON.stringify(rolled.journals)===JSON.stringify(inventory.journals)&&rolled.notesJson===inventory.notesJson,'Two-store freeze rolls back every task, journal, Notes and authority write on quota failure');
  const frozenState=await api.freezeWorkspaceTransfer(a,owner,transferId,target,plans),frozenData=JSON.parse(frozenState),artifact=JSON.parse(frozenData.artifact);
  check(artifact.transferId===transferId&&artifact.notes===inventory.notesJson&&artifact.workspaces.length===2,'Atomic freeze captures both legacy Workspaces and exact Notes bytes');
  check(await api.freezeWorkspaceTransfer(a,owner,crypto.randomUUID(),target,[])===frozenState,'Lost capture acknowledgement recovers the original immutable transfer');
  let workspaceDenied=false,notesDenied=false;const next=JSON.parse(plans[0].frozenJson);next.storeVersion++;
  try{await api.commitWorkspace(a,next.storeVersion-1,JSON.stringify(next));}catch{workspaceDenied=true;}
  try{await api.commitNotes(owner,1,JSON.stringify({...book,revision:2,receipts:[...book.receipts,{...book.receipts[0],commandId:crypto.randomUUID(),noteRevision:2,notebookRevision:2}]}));}catch{notesDenied=true;}
  check(workspaceDenied&&notesDenied,'Frozen task journals and Notes cannot acquire writable authority');
  const artifactDigest=await crypto.subtle.digest('SHA-256',new TextEncoder().encode(frozenData.artifact));const artifactHash=Array.from(new Uint8Array(artifactDigest),v=>v.toString(16).padStart(2,'0')).join('');
  const receipt={state:'accepted',transferId,workspaceId:target,artifactHash};let wrongDenied=false;
  try{await api.acknowledgeWorkspaceTransfer(a,owner,transferId,'b'.repeat(64),JSON.stringify(receipt));}catch{wrongDenied=true;}
  check(wrongDenied&&JSON.parse((await api.getWorkspaceTransferInventory(a,owner)).pendingJson).acknowledged===false,'A mismatched receipt cannot acknowledge another source');
  check(await api.acknowledgeWorkspaceTransfer(a,owner,transferId,artifactHash,JSON.stringify(receipt)),'Exact original receipt acknowledges local recovery evidence');
  const acknowledged=JSON.parse((await api.getWorkspaceTransferInventory(a,owner)).pendingJson);
  check(acknowledged.acknowledged&&acknowledged.artifact===frozenData.artifact&&(await api.ensureDefaultWorkspace(a,empty(crypto.randomUUID()))).workspaceId===null,'Acknowledgement retains exact archives and cannot reveal a writable Browser option');
  const c='00000000-0000-4000-8000-000000000013',cloudId=crypto.randomUUID(),oldTransfer=crypto.randomUUID();
  const cDigest=await crypto.subtle.digest('SHA-256',new TextEncoder().encode(c)),cOwner=Array.from(new Uint8Array(cDigest),v=>v.toString(16).padStart(2,'0')).join('');
  const owned=JSON.parse(empty(cloudId));owned.authority={mode:'cloud',transferId:oldTransfer,cloudWorkspaceId:cloudId};const ownedJson=JSON.stringify(owned);await api.commitWorkspace(c,0,ownedJson);
  const retained=await api.getWorkspace(c,cloudId);const notesOnly=JSON.parse(await api.freezeWorkspaceTransfer(c,cOwner,crypto.randomUUID(),cloudId,[{beforeJson:retained,frozenJson:retained,retainCloud:true}]));
  check(JSON.parse(notesOnly.artifact).workspaces.length===0&&await api.getWorkspace(c,cloudId)===retained,'Legacy cloud-owned task recovery bytes remain unchanged during remaining Notes adoption');
  return checks;
 });
 console.log(JSON.stringify({checks:results.length,results}));
 await page.reload();assert.equal(await page.evaluate(async()=>await(await import('/js/storage/indexedDbStorage.js')).getJson('preferences/companionAccounts')),'{"version":1,"accounts":{}}');
 console.log('PASS: preferences survive reload');
}finally{clearTimeout(deadline);await browser?.close();await new Promise(resolve=>server.close(resolve));}
