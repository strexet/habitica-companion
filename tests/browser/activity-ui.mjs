// Current production assets and actual hydrated Blazor routes, plus independent Razor-state layout cases.
import assert from 'node:assert/strict';
import {createServer} from 'node:http';
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {join,extname,resolve} from 'node:path';
import {tmpdir} from 'node:os';
import {createRequire} from 'node:module';
import {createHash} from 'node:crypto';
const require=createRequire(import.meta.url),pw=require(process.env.PLAYWRIGHT_MODULE||'/Users/petr/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const [fixtures,web,out,mode='all']=process.argv.slice(2);assert.ok(fixtures&&web&&out);
const names=['activity-series-verified','activity-series-error','activity-series-lower','activity-series-unavailable','activity-series-frequency','activity-series-long','todo-statistics-long'];
const coverage=JSON.parse(await readFile(join(fixtures,'coverage.json')));for(const name of names)assert.ok(coverage[name],name);
const results=[],contexts=new Set(),profiles=[];let origin,lastPage;const browserErrors=[];
const mime={'.html':'text/html','.js':'text/javascript','.mjs':'text/javascript','.json':'application/json','.css':'text/css','.wasm':'application/wasm','.dat':'application/octet-stream','.dll':'application/octet-stream'};
const server=createServer(async(req,res)=>{
 try{
  const path=decodeURIComponent(new URL(req.url,'http://fixture.invalid').pathname);if(path.includes('..')){res.writeHead(400);res.end();return;}
  if(path==='/neutral.json'){res.setHeader('Content-Type','application/json');res.end(await readFile(new URL('../fixtures/workspace/activity-streaks-v1.json',import.meta.url)));return;}
  if(path==='/__seed'){res.setHeader('Content-Type','text/html');res.end('<!doctype html><title>Owned seed</title>');return;}
  let base=web,name=path;
  if(path.startsWith('/matrix/')){base=fixtures;name=path.slice(7);}else if(['/schemes.json','/mud.css','/app.css','/colorSchemes.js','/qualification-frame.mjs','/qualification-core.mjs','/qualification-session.mjs'].includes(path)){base=fixtures;}
  if(['/','/notes','/todo/statistics'].includes(path))name='/index.html';
  const file=resolve(base,'.'+name);assert.ok(file.startsWith(resolve(base)+'/'));
  let body=await readFile(file);if(base===fixtures&&name.endsWith('.html'))body=Buffer.from(body.toString().replace('<details class="notes-folder-management">','<details open class="notes-folder-management">'));
  res.setHeader('Content-Type',mime[extname(file)]||'application/octet-stream');res.setHeader('Cache-Control','no-store');res.end(body);
 }catch{res.writeHead(404);res.end();}
});
await new Promise(r=>server.listen(0,'127.0.0.1',r));origin='http://127.0.0.1:'+server.address().port;
const identity={origin,pid:process.pid,projectRoot:process.cwd(),build: createHash('sha256').update(await readFile(join(web,'_framework/blazor.boot.json'))).digest('hex'),assets:JSON.parse(await readFile(join(fixtures,'asset-fingerprints.json')))};
const deadline=setTimeout(()=>{console.error('Owned UI fixture deadline exceeded');for(const c of contexts)c.close();server.closeAllConnections();server.close();process.exitCode=124;},210000);
async function context(engine){const dir=await mkdtemp(join(tmpdir(),'habitica-notes-ui-'));profiles.push(dir);const c=await pw[engine].launchPersistentContext(dir,{headless:true,serviceWorkers:'block'});contexts.add(c);c.setDefaultTimeout(12000);await c.route('**/*',async route=>{
 const url=route.request().url();if(url.startsWith(origin+'/'))return route.continue();
 // Only synthetic provider responses are fulfilled. No request reaches Habitica or another host.
 if(url.startsWith('https://habitica.com/api/v3/')&&route.request().method()==='GET')return route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({success:true,data:url.endsWith('/user')?{profile:{name:'Synthetic folder owner'},stats:{class:'warrior',lvl:1,hp:50,maxHealth:50,mp:10,maxMP:10},preferences:{dayStart:0,timezoneOffset:0},items:{gear:{equipped:{},owned:{}}}}:url.includes('/tasks')?[]:{gear:{flat:{}}}})});
 return route.abort();
 });return c;}
try{
 for(const engine of ['chromium','webkit']){
  const c=await context(engine);const page=await c.newPage();lastPage=page;page.on('pageerror',error=>browserErrors.push(String(error)));page.on('console',msg=>{if(msg.type()==='error')browserErrors.push(msg.text());});
  // Layout/contrast covers actual Razor states, including failures and pending original receipts.
  for(const name of (mode==='journeys'?[]:names))for(const theme of ['gryphy-light','obsidian-glow'])for(const width of [360,1280])for(const text of [100,200]){
   await page.setViewportSize({width,height:900});await page.goto(origin+'/matrix/'+coverage[name].file+`?theme=${theme}&text=${text}&case=${results.length}`,{waitUntil:'domcontentloaded'});
   await page.waitForSelector('#qualification-result',{state:'attached'});const row=JSON.parse(await page.locator('#qualification-result').textContent());
   assert.deepEqual(row.errors,[],JSON.stringify({name,engine,theme,width,text,errors:row.errors}));assert.deepEqual(row.unqualifiedSurfaces,[]);assert.ok(row.primaryControls>0&&row.textSamples>0);
   const focus=page.locator('button:enabled,select:enabled,input:enabled,summary').filter({visible:true}).first();await focus.focus();assert.equal(await focus.evaluate(el=>document.activeElement===el),true);await page.keyboard.press('Tab');assert.ok(await page.evaluate(()=>{const el=document.activeElement;if(!el||el===document.body)return false;const style=getComputedStyle(el);return parseFloat(style.outlineWidth)>0&&style.outlineStyle!=='none';}),'Keyboard focus needs a visible outline');
   results.push({kind:'Razor layout and keyboard',engine,name,theme,width,text,...row});
   if(name==='activity-series-verified'&&width===360&&text===200)await page.screenshot({path:join(out,`${engine}-${theme}.png`),fullPage:true});
  }
  if(mode==='matrix'){await c.close();contexts.delete(c);continue;}
  await page.goto(origin+'/__seed');const account='00000000-0000-4000-8000-000000000011';
  const data=await page.evaluate(async account=>{const row=(await(await fetch('/neutral.json')).json()).cases[0];const db=await import('/js/storage/indexedDbStorage.js');await db.setJson('auth/persistentCredentials',JSON.stringify({userId:account,apiToken:'synthetic-local-only'}));if(!await db.commitWorkspace(account,0,JSON.stringify(row.snapshot)))throw Error('Synthetic workspace seed refused');return {taskId:row.request.taskId,workspaceId:row.snapshot.document.id,from:row.request.fromDay,asOf:row.request.asOfUtc.slice(0,19)};},account);
  await page.goto(origin+'/notes');await page.waitForFunction(()=>document.querySelector('[data-testid=note-save]')?.disabled===false);await page.getByRole('link',{name:'Statistics',exact:true}).click();await page.locator('[data-testid=streak-refresh]').click();await page.locator('[data-testid=streak-task]').selectOption(data.taskId);await page.locator('[data-testid=streak-from]').fill(data.from);await page.locator('[data-testid=streak-from]').press('Tab');await page.locator('[data-testid=streak-asof]').fill(data.asOf.slice(0,16));await page.locator('[data-testid=streak-asof]').press('Tab');await page.waitForFunction(()=>document.querySelector('[data-testid=streak-current]')?.textContent.trim()==='2');
  const before=await page.evaluate(async({account,data})=>{const db=await import('/js/storage/indexedDbStorage.js');return db.getWorkspace(account,data.workspaceId);},{account,data});
  assert.ok(await page.locator('[data-testid=activity-streaks]').textContent().then(s=>s.includes('verified')));await page.locator('[data-testid=streak-zone]').fill('Mars/Olympus');await page.locator('[data-testid=streak-zone]').press('Tab');await page.locator('[data-testid=streak-error]').waitFor();assert.equal(await page.locator('[data-testid=streak-current]').count(),0);await page.locator('[data-testid=streak-zone]').fill('UTC');await page.locator('[data-testid=streak-zone]').press('Tab');await page.locator('[data-testid=streak-current]').waitFor();assert.equal((await page.locator('[data-testid=streak-current]').textContent()).trim(),'2');
  await page.locator('[data-testid=streak-zone]').fill('America/New_York');await page.locator('[data-testid=streak-zone]').press('Tab');await page.waitForFunction(()=>document.querySelector('[data-testid=streak-current]')?.textContent.trim()==='2');assert.ok((await page.locator('[data-testid=activity-streaks]').textContent()).includes('lower bound'));await page.locator('[data-testid=streak-zone]').fill('UTC');await page.locator('[data-testid=streak-zone]').press('Tab');await page.locator('[data-testid=streak-task]').focus();await page.keyboard.press('Tab');assert.equal(await page.evaluate(()=>document.activeElement?.dataset.testid),'streak-zone');assert.ok(await page.evaluate(()=>parseFloat(getComputedStyle(document.activeElement).outlineWidth)>0));
  const after=await page.evaluate(async({account,data})=>{const db=await import('/js/storage/indexedDbStorage.js');return db.getWorkspace(account,data.workspaceId);},{account,data});assert.equal(after,before);assert.equal(await page.locator('#blazor-error-ui').isVisible(),false);assert.equal(browserErrors.some(e=>e.includes('Unhandled exception')),false);
  results.push({kind:'Hydrated Release activity route and real IndexedDB',engine,routes:['/todo/statistics'],checks:['Exact task choice','Explicit historical as-of and timezone','Verified current run from real full journal','Invalid timezone refuses without a false zero','Filter repair preserves historical as-of','Native keyboard focus','Canonical journal remains unchanged'],qualification:'Synthetic provider responses only; actual Release Blazor runtime and adapter; service workers blocked'});
  await c.close();contexts.delete(c);
 }
}catch(error){const overflow=await lastPage?.evaluate(()=>[...document.body.querySelectorAll('*')].map(el=>{const r=el.getBoundingClientRect();return{tag:el.tagName,id:el.dataset.testid,classes:el.className,left:r.left,right:r.right,width:r.width,scroll:el.scrollWidth,client:el.clientWidth,text:el.textContent.slice(0,100)};}).filter(r=>r.right>innerWidth+1||r.left<0||r.scroll>r.client+1)).catch(()=>[]);await writeFile(join(out,'failure.json'),JSON.stringify({error:String(error),results,browserErrors,overflow,url:lastPage?.url()},null,2));if(lastPage){await lastPage.screenshot({path:join(out,'failure.png'),fullPage:true}).catch(()=>{});await writeFile(join(out,'failure.html'),await lastPage.content().catch(()=>''));}throw error;}
finally{clearTimeout(deadline);await Promise.allSettled([...contexts].map(c=>c.close()));server.closeAllConnections();await new Promise(r=>server.close(r));await Promise.all(profiles.map(p=>rm(p,{recursive:true,force:true})));}
const expected=(mode==='journeys'?0:names.length*2*2*2*2)+(mode==='matrix'?0:2);assert.equal(results.length,expected);
const report={identity,results,counts:{expected,discovered:results.length,passed:results.length,failed:0,skipped:0},cleanup:'Owned loopback server and both generated persistent profiles closed and removed. No external request, real login, cloud write, device permission or subscription.',limits:['Physical devices and assistive technologies unqualified','Hosted OAuth and cloud paths covered only by separate synthetic API/MCP controls','Service-worker lifecycle is outside the changed activity UI; full-app checks block service workers']};await writeFile(join(out,'report.json'),JSON.stringify(report,null,2)+'\n');
