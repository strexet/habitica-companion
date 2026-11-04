// Current production assets and actual hydrated Blazor routes, plus independent Razor-state layout cases.
import assert from 'node:assert/strict';
import {cloudFixture} from './linked-daily-fixture.mjs';
import {createServer} from 'node:http';
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {join,extname,resolve} from 'node:path';
import {tmpdir} from 'node:os';
import {createRequire} from 'node:module';
import {createHash} from 'node:crypto';
const require=createRequire(import.meta.url),pw=require(process.env.PLAYWRIGHT_MODULE||'/Users/petr/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const [fixtures,web,out,mode='all']=process.argv.slice(2);assert.ok(fixtures&&web&&out);
const names=['linked-daily-editor','linked-daily-review','linked-daily-pending','linked-daily-unknown','linked-daily-readonly-browser','linked-daily-readonly-unknownoutcome','linked-daily-readonly-externalconflict','linked-daily-long','linked-daily-sync'];
const coverage=JSON.parse(await readFile(join(fixtures,'coverage.json')));for(const name of names)assert.ok(coverage[name],name);
const results=[],contexts=new Set(),profiles=[],clouds=new Set();let origin,lastPage;const browserErrors=[];
const mime={'.html':'text/html','.js':'text/javascript','.mjs':'text/javascript','.json':'application/json','.css':'text/css','.wasm':'application/wasm','.dat':'application/octet-stream','.dll':'application/octet-stream'};
const server=createServer(async(req,res)=>{
 try{
  const path=decodeURIComponent(new URL(req.url,'http://fixture.invalid').pathname);if(path.includes('..')){res.writeHead(400);res.end();return;}
  if(path==='/appsettings.json'){const config=JSON.parse(await readFile(join(web,'appsettings.json')));config.ChatGptIntegration={AuthBaseUrl:'http://localhost:8787/',Enabled:true};config.CompanionWorkspace={...config.CompanionWorkspace,BaseUrl:'http://localhost:8789/'};res.setHeader('Content-Type','application/json');res.end(JSON.stringify(config));return;}
  if(path==='/__seed'){res.setHeader('Content-Type','text/html');res.end('<!doctype html><title>Owned seed</title>');return;}
  let base=web,name=path;
  if(path.startsWith('/matrix/')){base=fixtures;name=path.slice(7);}else if(['/schemes.json','/mud.css','/app.css','/colorSchemes.js','/qualification-frame.mjs','/qualification-core.mjs','/qualification-session.mjs'].includes(path)){base=fixtures;}
  if(['/','/notes','/todo/dailies','/todo/sync','/settings'].includes(path))name='/index.html';
  const file=resolve(base,'.'+name);assert.ok(file.startsWith(resolve(base)+'/'));
  let body=await readFile(file);if(base===fixtures&&name.endsWith('.html'))body=Buffer.from(body.toString().replace('<details class="notes-folder-management">','<details open class="notes-folder-management">'));
  res.setHeader('Content-Type',mime[extname(file)]||'application/octet-stream');res.setHeader('Cache-Control','no-store');res.end(body);
 }catch{res.writeHead(404);res.end();}
});
await new Promise((resolve,reject)=>{server.once('error',reject);server.listen(5081,'localhost',resolve);});origin='http://localhost:5081';
const identity={origin,pid:process.pid,projectRoot:process.cwd(),build: createHash('sha256').update(await readFile(join(web,'_framework/blazor.boot.json'))).digest('hex'),assets:JSON.parse(await readFile(join(fixtures,'asset-fingerprints.json')))};
const deadline=setTimeout(()=>{console.error('Owned UI fixture deadline exceeded');for(const c of contexts)c.close();server.closeAllConnections();server.close();process.exitCode=124;},210000);
async function context(engine,cloud){const dir=await mkdtemp(join(tmpdir(),'habitica-notes-ui-'));profiles.push(dir);const c=await pw[engine].launchPersistentContext(dir,{headless:true,serviceWorkers:'block'});contexts.add(c);c.setDefaultTimeout(12000);await c.route('**/*',async route=>{
 const url=route.request().url();if(url.startsWith(origin+'/'))return route.continue();
 return cloud.route(route);
 });return c;}
try{
 for(const engine of ['chromium','webkit']){
  const cloud=await cloudFixture();clouds.add(cloud);const c=await context(engine,cloud);const page=await c.newPage();lastPage=page;page.on('pageerror',error=>browserErrors.push(String(error)));page.on('console',msg=>{if(msg.type()==='error')browserErrors.push(msg.text());});
  // Layout/contrast covers actual Razor states, including failures and pending original receipts.
  for(const name of (mode==='journeys'?[]:names))for(const theme of ['gryphy-light','obsidian-glow'])for(const width of [360,1280])for(const text of [100,200]){
   await page.setViewportSize({width,height:900});await page.goto(origin+'/matrix/'+coverage[name].file+`?theme=${theme}&text=${text}&case=${results.length}`,{waitUntil:'domcontentloaded'});
   await page.waitForSelector('#qualification-result',{state:'attached'});const row=JSON.parse(await page.locator('#qualification-result').textContent());
   assert.deepEqual(row.errors,[],JSON.stringify({name,engine,theme,width,text,errors:row.errors}));assert.deepEqual(row.unqualifiedSurfaces,[]);assert.ok(row.primaryControls>0&&row.textSamples>0);
   const focus=page.locator('button:enabled,select:enabled,input:enabled,summary').filter({visible:true}).first();await page.keyboard.press('Tab');await focus.focus();assert.equal(await focus.evaluate(el=>document.activeElement===el),true);
   const outlined=()=>page.evaluate(()=>{const el=document.activeElement;if(!el||el===document.body)return false;const style=getComputedStyle(el);return parseFloat(style.outlineWidth)>0&&style.outlineStyle!=='none';});assert.ok(await outlined(),'Initial keyboard-modality focus needs a visible outline');
   await page.keyboard.press('Tab');const tabTarget=await page.evaluate(()=>({tag:document.activeElement?.tagName,id:document.activeElement?.dataset.testid??null}));
   if(tabTarget.tag==='BODY'){await focus.focus();assert.ok(await outlined(),'Native end-of-document return focus must remain visible');}else assert.ok(await outlined(),'Keyboard focus needs a visible outline: '+JSON.stringify(tabTarget));
   results.push({kind:'Razor layout and keyboard',engine,name,theme,width,text,tabTarget,...row});
   if(name==='linked-daily-review'&&width===360&&text===200){await page.screenshot({path:join(out,`${engine}-${theme}.png`),fullPage:true});await page.screenshot({path:join(out,`${engine}-${theme}-viewport.png`),fullPage:false});}
  }
  if(mode==='matrix'){await c.close();contexts.delete(c);continue;}
  await page.goto(origin+'/__seed');
  await page.evaluate(async data=>{const db=await import('/js/storage/indexedDbStorage.js');await db.setJson('auth/persistentCredentials',JSON.stringify(data.credentials));const hash=await crypto.subtle.digest('SHA-256',new TextEncoder().encode(data.account));const scope=Array.from(new Uint8Array(hash),v=>v.toString(16).padStart(2,'0')).join('');await db.setJson('preferences/companionAccounts',JSON.stringify({version:1,accounts:{[scope]:{revision:1,mutationId:crypto.randomUUID(),cloudSelected:true,workspaceId:data.workspaceId,notebookId:null,recovery:data.descriptor,recoverWorkspace:true,recoverNotes:false,conflict:false,revokedConnections:[]}}}));},{account:cloud.account,credentials:cloud.credentials,descriptor:cloud.descriptor,workspaceId:cloud.workspaceId});
  await page.goto(origin+'/todo/dailies');await page.locator(`[data-testid=todo-recurring-${cloud.taskId}]`).waitFor();await page.locator(`[data-testid=todo-recurring-${cloud.taskId}]`).click();
  assert.equal(await page.locator('[data-testid=recurring-interval]').isEnabled(),true);assert.equal(await page.locator('[data-testid=recurring-clear]').isEnabled(),false);assert.equal(await page.locator('[data-testid=recurring-lifecycle]').isEnabled(),false);
  await page.locator('[data-testid=recurring-interval]').fill('3');await page.locator('[data-testid=recurring-interval]').press('Tab');await page.locator('[data-testid=recurring-review-schedule]').click();await page.locator('[data-testid=linked-daily-review]').waitFor();assert.ok((await page.locator('[data-testid=linked-daily-review]').textContent()).includes('Retained Habitica rule'));assert.ok((await page.locator('[data-testid=linked-daily-next]').textContent()).includes('Proposed dates'));assert.equal((await cloud.snapshot()).commands.length,1);
  await page.locator('[data-testid=todo-confirm]').focus();assert.ok(await page.evaluate(()=>parseFloat(getComputedStyle(document.activeElement).outlineWidth)>0));await page.keyboard.press('Enter');await page.locator('[data-testid=linked-daily-receipt]').waitFor();const saved=await cloud.snapshot();assert.equal(saved.commands.length,2);assert.equal(saved.document.tasks[0].recurrence.interval,3);assert.equal(saved.commands[1].command.operation,'editLinkedDailySchedule');assert.equal(saved.commands[1].projectionState,'pending');assert.deepEqual(saved.commands[1].effects,['update']);assert.equal(saved.writes,0);assert.equal(saved.document.completions.length,0);assert.equal(saved.document.habitScores.length,0);
  await page.locator('a[href="/todo/sync"]').first().click();await page.getByRole('heading',{name:'Todo Sync',exact:true}).waitFor();await page.locator('[data-testid=linked-daily-receipt]').waitFor();
  const actualRequests=cloud.requests.filter(r=>r.host==='workspace'&&r.path==='/task-api');assert.ok(actualRequests.length>=2);assert.ok(cloud.requests.some(r=>r.host==='auth'&&r.path==='/recover'&&r.status===200));assert.ok(cloud.requests.some(r=>r.host==='workspace'&&r.path==='/recover'&&r.status===201));assert.equal(await page.locator('#blazor-error-ui').isVisible(),false);assert.equal(browserErrors.some(e=>e.includes('Unhandled exception')),false);
  results.push({kind:'Hydrated Release route, typed client and actual local Auth/Workspace/D1',engine,routes:['/todo/dailies','/todo/sync'],checks:['Persisted account and exact existing cloud recovery','Fresh reviewed full provider schedule','Linked lifecycle and clear stay disabled','Native keyboard confirmation','One original command and native schedule epoch','Pending metadata effect without provider writes, scores or Cron'],requests:cloud.requests,qualification:'Synthetic isolated Miniflare and provider; actual current Release Blazor, native IndexedDB, recovery and typed API; service workers blocked'});
  await c.close();contexts.delete(c);
 }
}catch(error){const overflow=await lastPage?.evaluate(()=>[...document.body.querySelectorAll('*')].map(el=>{const r=el.getBoundingClientRect();return{tag:el.tagName,id:el.dataset.testid,classes:el.className,left:r.left,right:r.right,width:r.width,scroll:el.scrollWidth,client:el.clientWidth,text:el.textContent.slice(0,100)};}).filter(r=>r.right>innerWidth+1||r.left<0||r.scroll>r.client+1)).catch(()=>[]);await writeFile(join(out,'failure.json'),JSON.stringify({error:String(error),results,browserErrors,cloudRequests:[...clouds].map(cloud=>cloud.requests),overflow,url:lastPage?.url()},null,2));if(lastPage){await lastPage.screenshot({path:join(out,'failure.png'),fullPage:true}).catch(()=>{});await writeFile(join(out,'failure.html'),await lastPage.content().catch(()=>''));}throw error;}
finally{clearTimeout(deadline);await Promise.allSettled([...contexts].map(c=>c.close()));server.closeAllConnections();await new Promise(r=>server.close(r));await Promise.all(profiles.map(p=>rm(p,{recursive:true,force:true})));await Promise.allSettled([...clouds].map(cloud=>cloud.close()));}
const expected=(mode==='journeys'?0:names.length*2*2*2*2)+(mode==='matrix'?0:2);assert.equal(results.length,expected);
const report={identity,results,counts:{expected,discovered:results.length,passed:results.length,failed:0,skipped:0},cleanup:'Owned loopback server and both generated persistent profiles closed and removed. All synthetic Worker/D1 instances disposed. No external request, real account, provider write, device permission or subscription.',limits:['Physical devices and assistive technologies unqualified','Real hosted OAuth, production CORS/deployment and physical-device paths remain unqualified','Service-worker lifecycle is outside the changed Daily UI; full-app checks block service workers']};await writeFile(join(out,'report.json'),JSON.stringify(report,null,2)+'\n');
