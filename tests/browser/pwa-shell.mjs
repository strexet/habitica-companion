// Disposable loopback-only service-worker lifecycle qualification. No real account or push enrollment.
import assert from 'node:assert/strict';
import {createServer} from 'node:http';
import {readFile,mkdtemp,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {createHash} from 'node:crypto';
import {createRequire} from 'node:module';
const require=createRequire(import.meta.url);
const playwright=require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const web=new URL('../../output/pwa-shell-redirect/wwwroot/',import.meta.url);
const old=await readFile(new URL('../../.skill-tree/history/validation/pwa-shell-redirect/old-worker.js',import.meta.url),'utf8');
const fixed=await readFile(new URL('service-worker.js',web),'utf8');
const push=await readFile(new URL('js/pushNotifications.js',web));
const html='<!doctype html><meta charset="utf-8"><title>Synthetic PWA</title><p id="shell">Synthetic public shell</p>';
const css='body { color: black; }';
const integrity=body=>'sha256-'+createHash('sha256').update(body).digest('base64');
let stage='old';let offline=false;let hits=[];
const server=createServer((req,res)=>{
 const path=new URL(req.url,'http://fixture.invalid').pathname;hits.push(path);
 if(offline){res.destroy();return;}
 res.setHeader('Cache-Control','no-store');
 if(path==='/index.html'){res.writeHead(302,{Location:'/'});res.end();return;}
 if(path==='/service-worker.js'){res.setHeader('Content-Type','text/javascript');res.end(stage==='old'?old:fixed+(stage==='bad'?'\n// failed update control\n':''));return;}
 if(path==='/service-worker-assets.js'){
  res.setHeader('Content-Type','text/javascript');res.end('self.assetsManifest = '+JSON.stringify({version:stage==='bad'?'broken':'fixture',assets:[{url:'index.html',hash:integrity(html)},{url:'css/app.css',hash:integrity(css)}]})+';');return;
 }
 if(path==='/js/pushNotifications.js'){res.setHeader('Content-Type','text/javascript');res.end(push);return;}
 if(path==='/css/app.css'){res.setHeader('Content-Type','text/css');res.end(stage==='bad'?'invalid integrity':css);return;}
 res.setHeader('Content-Type','text/html; charset=utf-8');res.setHeader('X-Shell','retained');res.end(html);
});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
const origin='http://127.0.0.1:'+server.address().port;
const results=[],ownedContexts=new Set(),ownedProfiles=new Set();
const deadline=setTimeout(async()=>{
 console.error('Browser qualification deadline exceeded');
 await bounded(Promise.allSettled([...ownedContexts].map(context=>context.close())),'deadline cleanup').catch(()=>{});
 for(const profile of ownedProfiles)await rm(profile,{recursive:true,force:true});
 server.closeAllConnections();server.close();process.exit(1);
},135000);
async function bounded(promise,label){
 let timer;
 try{return await Promise.race([promise,new Promise((_,reject)=>{timer=setTimeout(()=>reject(new Error(label+' deadline exceeded')),12000);})]);}
 finally{clearTimeout(timer);}
}
async function register(page){
 await bounded(page.evaluate(async()=>{
  const reg=await navigator.serviceWorker.register('/service-worker.js');
  await navigator.serviceWorker.ready;
  if(reg.active?.state!=='activated')await new Promise(resolve=>reg.active.addEventListener('statechange',()=>{if(reg.active.state==='activated')resolve();}));
 }),'registration');
}
async function update(page){
 return bounded(page.evaluate(async()=>{
  const reg=await navigator.serviceWorker.getRegistration();let found;
  reg.addEventListener('updatefound',()=>{found=reg.installing;},{once:true});
  await reg.update();
  const worker=found || reg.installing || reg.waiting;
  if(!worker)return 'no-update';
  if(['installed','redundant'].includes(worker.state))return worker.state;
  return new Promise(resolve=>worker.addEventListener('statechange',()=>{if(['installed','redundant'].includes(worker.state))resolve(worker.state);}));
 }),'update');
}
async function seed(page){
 return page.evaluate(async()=>{
  localStorage.setItem('synthetic-credential','canary');
  const db=await new Promise((resolve,reject)=>{const r=indexedDB.open('synthetic-notes',1);r.onupgradeneeded=()=>r.result.createObjectStore('records');r.onsuccess=()=>resolve(r.result);r.onerror=()=>reject(r.error);});
  await new Promise((resolve,reject)=>{const tx=db.transaction('records','readwrite');tx.objectStore('records').put('note/task/receipt canary','original');tx.oncomplete=resolve;tx.onerror=()=>reject(tx.error);});db.close();
  await (await caches.open('unrelated-subscription-canary')).put('/canary',new Response('subscription-state canary'));
  // A synthetic subscription marker is sufficient; no platform Push API is invoked.
 });
}
async function canaries(page){
 return page.evaluate(async()=>{
  const db=await new Promise(resolve=>{const r=indexedDB.open('synthetic-notes');r.onsuccess=()=>resolve(r.result);});
  const value=await new Promise(resolve=>{const r=db.transaction('records').objectStore('records').get('original');r.onsuccess=()=>resolve(r.result);});db.close();
  return {credential:localStorage.getItem('synthetic-credential'),record:value,cache:await (await (await caches.open('unrelated-subscription-canary')).match('/canary')).text()};
 });
}
try{
 for(const engine of ['chromium','webkit']){
  const profiles=[];
  const profile=await mkdtemp(join(tmpdir(),'pwa-shell-'+engine+'-'));profiles.push(profile);ownedProfiles.add(profile);
  const context=await playwright[engine].launchPersistentContext(profile,{headless:true,serviceWorkers:'allow',timeout:15000});
  ownedContexts.add(context);const browser=context.browser();
  const contexts=[context];
  try{
   let page=await context.newPage();page.setDefaultTimeout(12000);page.setDefaultNavigationTimeout(12000);
   console.error(engine,'old install');offline=false;stage='old';await bounded(page.goto(origin+'/harness'),'navigation');await register(page);console.error(engine,'registered');await bounded(seed(page),'seed');assert.deepEqual(await bounded(canaries(page),'initial canaries'),{credential:'canary',record:'note/task/receipt canary',cache:'subscription-state canary'});console.error(engine,'seeded');
   await bounded(page.reload(),'reload');
   const redirected=await page.evaluate(async()=>{const r=await (await caches.open('offline-cache-fixture')).match('index.html');return {redirected:r.redirected,url:r.url,status:r.status};});
   assert.equal(redirected.redirected,true);assert.equal(redirected.status,200);assert.equal(redirected.url,origin+'/');
   let oldFailure=false;try{await bounded(page.goto(origin+'/dashboard'),'navigation');}catch{oldFailure=true;}
   assert.equal(oldFailure,true,'pre-fix redirected shell must fail actual browser navigation');
   await page.close();page=await context.newPage();page.setDefaultTimeout(12000);page.setDefaultNavigationTimeout(12000);
   console.error(engine,'upgrade');await bounded(page.goto(origin+'/harness'),'navigation');stage='fixed';assert.equal(await update(page),'installed');
   await page.close();page=await context.newPage();page.setDefaultTimeout(12000);page.setDefaultNavigationTimeout(12000);
   await bounded(page.goto(origin+'/harness'),'navigation');
   await page.waitForFunction(async()=>{const keys=await caches.keys();return keys.includes('offline-cache-fixture-shell-v3')&&!keys.includes('offline-cache-fixture');});
   await bounded(page.goto(origin+'/dashboard?q=preserved'),'navigation');assert.equal(await page.locator('#shell').textContent(),'Synthetic public shell');
   const shell=await page.evaluate(async()=>{const r=await (await caches.open('offline-cache-fixture-shell-v3')).match('index.html');return {redirected:r.redirected,type:r.type,header:r.headers.get('X-Shell'),text:await r.text()};});
   assert.equal(shell.redirected,false);assert.equal(shell.header,'retained');assert.equal(shell.text,html);
   console.error(engine,'upgraded offline');offline=true;
   for(const route of ['/','/todo/tasks?q=offline','/notes']){await bounded(page.goto(origin+route),'navigation');assert.equal(await page.locator('#shell').textContent(),'Synthetic public shell');}
   assert.deepEqual(await canaries(page),{credential:'canary',record:'note/task/receipt canary',cache:'subscription-state canary'});
   console.error(engine,'failed install');offline=false;await bounded(page.goto(origin+'/harness'),'navigation');stage='bad';assert.equal(await update(page),'redundant');
   await bounded(page.goto(origin+'/dashboard'),'navigation');assert.equal(await page.locator('#shell').textContent(),'Synthetic public shell');
   assert.deepEqual(await canaries(page),{credential:'canary',record:'note/task/receipt canary',cache:'subscription-state canary'});
   await page.close();
   // Fresh install uses the same HTML canonicalization and tests a missing-cache network fallback.
   console.error(engine,'fresh install');offline=false;stage='fixed';const freshProfile=await mkdtemp(join(tmpdir(),'pwa-shell-fresh-'));profiles.push(freshProfile);ownedProfiles.add(freshProfile);
   const fresh=await playwright[engine].launchPersistentContext(freshProfile,{headless:true,serviceWorkers:'allow',timeout:15000});contexts.push(fresh);ownedContexts.add(fresh);
   const freshPage=await fresh.newPage();freshPage.setDefaultTimeout(12000);freshPage.setDefaultNavigationTimeout(12000);
   await bounded(freshPage.goto(origin+'/harness'),'navigation');await register(freshPage);await bounded(freshPage.goto(origin+'/todo/tasks'),'navigation');
   assert.equal(await freshPage.locator('#shell').textContent(),'Synthetic public shell');
   offline=true;await bounded(freshPage.reload(),'reload');assert.equal(await freshPage.locator('#shell').textContent(),'Synthetic public shell');
   offline=false;
   assert.equal(await freshPage.evaluate(async()=>await (await caches.open('offline-cache-fixture-shell-v3')).delete('/index.html')),true);
   await bounded(freshPage.goto(origin+'/dashboard'),'navigation');assert.equal(await freshPage.locator('#shell').textContent(),'Synthetic public shell');
   console.error(engine,'offline cache miss');offline=true;let offlineMiss=false;try{await bounded(freshPage.reload(),'reload');}catch{offlineMiss=true;}
   assert.equal(offlineMiss,true,'missing offline shell must remain an explicit network failure');
   console.error(engine,'passed');results.push({engine,version:browser.version(),oldRedirectedShell:redirected,preFixNavigationFailed:oldFailure,workerOnlyUpgradeSameManifest:true,repairedShell:shell,offlineRoutes:3,freshInstall:true,failedIntegrityUpdateRefused:true,missingShellOnlineFallback:true,missingShellOfflineFailure:true,canariesPreserved:true,realPushEnrollment:false,realPushInspection:false});
  }catch(error){console.error(engine,error);throw error;}finally{
   for(const context of contexts)await bounded(context.close(),'context cleanup').catch(error=>console.error(error.message));
   await bounded(browser.close(),'browser cleanup').catch(error=>console.error(error.message));
   for(const context of contexts)ownedContexts.delete(context);
   for(const profile of profiles){await rm(profile,{recursive:true,force:true});ownedProfiles.delete(profile);}
  }
 }
 console.log(JSON.stringify({ok:true,results,loopbackOnly:true,publishedWorkerSha256:createHash('sha256').update(fixed).digest('hex'),requests:hits.length},null,2));
}finally{clearTimeout(deadline);server.closeAllConnections();await new Promise(resolve=>server.close(resolve));}
