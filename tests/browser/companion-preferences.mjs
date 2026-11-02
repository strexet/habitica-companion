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
  return checks;
 });
 console.log(JSON.stringify({checks:results.length,results}));
 await page.reload();assert.equal(await page.evaluate(async()=>await(await import('/js/storage/indexedDbStorage.js')).getJson('preferences/companionAccounts')),'{"version":1,"accounts":{}}');
 console.log('PASS: preferences survive reload');
}finally{clearTimeout(deadline);await browser?.close();await new Promise(resolve=>server.close(resolve));}
