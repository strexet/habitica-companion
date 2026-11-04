import {createRequire} from 'node:module';
import {mkdir,writeFile} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
import {resolve,dirname,relative} from 'node:path';
import {randomUUID} from 'node:crypto';
import assert from 'node:assert/strict';
const require=createRequire(import.meta.url);
const pw=require(process.env.PLAYWRIGHT_MODULE || '/Users/petr/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
const out=resolve(root,process.argv[2]||'');
assert.ok(out.startsWith(root+'/.skill-tree/history/validation/integrate-production/'));
const privateOut=root+'/.skill-tree/artifacts/integration-qualification/hosted-browser/'+randomUUID();
const deadline=setTimeout(()=>{console.error('Hosted browser deadline exceeded');process.exit(124);},240000);
await mkdir(out,{recursive:true});
const origin='https://habitica-companion.pages.dev';
const results=[];let failed=false;
await mkdir(privateOut,{recursive:true});
for(const engine of ['chromium','webkit']){
 const browser=await pw[engine].launch({headless:true,timeout:15000});
 const context=await browser.newContext({serviceWorkers:'allow',viewport:{width:390,height:844}});
 const page=await context.newPage();page.setDefaultTimeout(20000);page.setDefaultNavigationTimeout(30000);
 const errors=[],serverErrors=[],writes=[];
 page.on('pageerror',e=>errors.push(String(e).slice(0,300)));
 page.on('response',r=>{if(r.status()>=500)serverErrors.push({url:new URL(r.url()).pathname,status:r.status()});});
 page.on('request',r=>{if(!['GET','HEAD','OPTIONS'].includes(r.method()))writes.push({method:r.method(),path:new URL(r.url()).pathname});});
 const row={engine,routes:[]};
 try{
  let response=await page.goto(origin+'/sign-in',{waitUntil:'domcontentloaded'});assert.equal(response.status(),200);
  await page.locator('#main-content').waitFor();await page.locator('#app-sign-in').waitFor();
  assert.equal(await page.locator('#blazor-error-ui').isVisible(),false);
  row.title=await page.title();row.coldStartup=true;
  await page.screenshot({path:privateOut+'/'+engine+'-phone-startup.png',fullPage:true});
  for(const route of ['/settings','/todo/tasks','/todo/projects','/notes','/inventory']){
   response=await page.goto(origin+route,{waitUntil:'domcontentloaded'});assert.equal(response.status(),200);await page.locator('#main-content').waitFor();assert.equal(await page.locator('#blazor-error-ui').isVisible(),false);row.routes.push({route,status:200,rendered:true,finalPath:new URL(page.url()).pathname});
  }
  await page.setViewportSize({width:1280,height:900});await page.goto(origin+'/sign-in');await page.locator('#main-content').waitFor();await page.screenshot({path:privateOut+'/'+engine+'-desktop-startup.png',fullPage:true});
  row.serviceWorker=await page.evaluate(async()=>{const timeout=new Promise(resolve=>setTimeout(()=>resolve({ready:false}),15000));return Promise.race([navigator.serviceWorker.ready.then(r=>({ready:true,active:r.active?.state,script:r.active?.scriptURL})),timeout]);});
  assert.equal(row.serviceWorker.ready,true);assert.equal(row.serviceWorker.active,'activated');
  await page.reload();await page.locator('#main-content').waitFor();row.controlled=await page.evaluate(()=>Boolean(navigator.serviceWorker.controller));assert.equal(row.controlled,true);
  if(engine==='chromium'){await context.setOffline(true);await page.reload();await page.locator('#main-content').waitFor();assert.equal(await page.locator('#blazor-error-ui').isVisible(),false);row.offlineSignedOutRelaunch=true;await context.setOffline(false);}else{row.offlineSignedOutRelaunch='Not exercised against hosted WebKit; separate local lifecycle checks cover native network refusal.';}
  assert.deepEqual(errors,[]);assert.deepEqual(serverErrors,[]);assert.deepEqual(writes,[]);row.pass=true;
 }catch(e){row.pass=false;row.error=String(e).slice(0,500);failed=true;await page.screenshot({path:privateOut+'/'+engine+'-failure.png',fullPage:true}).catch(()=>{});}
 finally{row.pageErrors=errors;row.serverErrors=serverErrors;row.writes=writes;await context.close();await browser.close();results.push(row);}
}
await writeFile(out+'/hosted-browser-smoke-final.json',JSON.stringify({origin,results,pass:!failed,qualification:'Fresh signed-out startup, routed cold loads and public offline shell only; actual account/device/assistive-technology review remains separate.',cleanup:'Both owned browser contexts and browsers closed.'},null,2)+'\n');
clearTimeout(deadline);const counts={expected:2,discovered:results.length,passed:results.filter(r=>r.pass).length,failed:results.filter(r=>!r.pass).length,skipped:0};console.log(JSON.stringify({pass_result:!failed,report:relative(root,out+'/hosted-browser-smoke-final.json'),counts,results}));process.exitCode=failed?1:0;
