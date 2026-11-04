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
const names=['notes-folders-document','notes-folders-trash','notes-folders-review','notes-folders-unknown','notes-folders-stale','project-folders-review','project-folders-document','project-folders-empty'];
const coverage=JSON.parse(await readFile(join(fixtures,'coverage.json')));for(const name of names)assert.ok(coverage[name],name);
const results=[],contexts=new Set(),profiles=[];let origin,lastPage;const browserErrors=[];
const mime={'.html':'text/html','.js':'text/javascript','.mjs':'text/javascript','.json':'application/json','.css':'text/css','.wasm':'application/wasm','.dat':'application/octet-stream','.dll':'application/octet-stream'};
const server=createServer(async(req,res)=>{
 try{
  const path=decodeURIComponent(new URL(req.url,'http://fixture.invalid').pathname);if(path.includes('..')){res.writeHead(400);res.end();return;}
  if(path==='/__seed'){res.setHeader('Content-Type','text/html');res.end('<!doctype html><title>Owned seed</title>');return;}
  let base=web,name=path;
  if(path.startsWith('/matrix/')){base=fixtures;name=path.slice(7);}else if(['/schemes.json','/mud.css','/app.css','/colorSchemes.js','/qualification-frame.mjs','/qualification-core.mjs','/qualification-session.mjs'].includes(path)){base=fixtures;}
  if(['/','/notes','/todo/projects'].includes(path))name='/index.html';
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
   if(name==='project-folders-document'&&width===360&&text===200)await page.screenshot({path:join(out,`${engine}-${theme}.png`),fullPage:true});
  }
  if(mode==='matrix'){await c.close();contexts.delete(c);continue;}
  await page.goto(origin+'/__seed');
  const account='00000000-0000-4000-8000-000000000011',project='00000000-0000-4000-8000-000000000101',workspace='00000000-0000-4000-8000-000000000201';
  await page.evaluate(async({account,project,workspace})=>{const db=await import('/js/storage/indexedDbStorage.js');await db.setJson('auth/persistentCredentials',JSON.stringify({userId:account,apiToken:'synthetic-local-only'}));await db.commitWorkspace(account,0,JSON.stringify({document:{schemaVersion:1,id:workspace,revision:1,tasks:[],projects:[{id:project,revision:1,name:'Altai synthetic project',isArchived:false}]},storeVersion:1,commands:[],events:[],authority:null}));},{account,project,workspace});
  await page.goto(origin+'/notes');await page.locator('[data-testid=note-title]').waitFor();
  await page.locator('.notes-folder-management summary').click();await page.locator('[data-testid=folder-name]').fill('Standalone hotels');await page.locator('[data-testid=folder-create]').click();await page.locator('[data-testid=folder-confirm]').click();
  await page.waitForFunction(()=>document.querySelector('[data-testid=notes-folder-filter]')?.value!=='all');
  await page.locator('[data-testid=note-new]').click();await page.locator('[data-testid=note-title]').fill('Separate hotel');await page.locator('[data-testid=note-body]').fill('# Hotel\nExact details 🧥');await page.locator('[data-testid=note-save]').click();await page.locator('[data-testid=note-project-confirm]').click();
  await page.waitForFunction(()=>document.querySelector('[data-testid=note-new]')?.disabled===false);
  await page.reload();await page.locator('[data-testid=note-open]').click();assert.equal(await page.locator('[data-testid=note-body]').inputValue(),'# Hotel\nExact details 🧥');await page.locator('[data-testid=notes-search]').fill('ＨＯＴＥＬ');await page.locator('[data-testid=note-open]').waitFor();assert.equal(await page.locator('[data-testid=note-open]').count(),1);await page.locator('[data-testid=notes-search]').fill('');
  // Import stays a private, unsaved and unfiled draft, even while browsing a folder.
  await page.locator('[data-testid=note-import]').setInputFiles({name:'place.md',mimeType:'text/markdown',buffer:Buffer.from('# Imported place\nUnchanged 🧥')});await page.waitForFunction(()=>document.querySelector('[data-testid=note-body]')?.value==='# Imported place\nUnchanged 🧥');assert.equal(await page.locator('[data-testid=note-body]').inputValue(),'# Imported place\nUnchanged 🧥');assert.equal(await page.locator('[data-testid=note-folder-picker]').inputValue(),'');await page.locator('[data-testid=note-new]').click();await page.locator('[data-testid=note-discard]').click();
  await page.waitForFunction(()=>document.querySelector('[data-testid=note-save]')?.disabled===false);await page.getByRole('link',{name:'Projects',exact:true}).click();await page.locator('[data-testid=hierarchy-panel] select').selectOption(project);await page.locator('[data-testid=project-notes-toggle]').click();await page.locator('.notes-folder-management summary').click();await page.locator('[data-testid=folder-name]').fill('Interesting places');await page.locator('[data-testid=folder-create]').click();await page.locator('[data-testid=folder-confirm]').click();await page.waitForFunction(()=>document.querySelector('[data-testid=notes-folder-filter]')?.value!=='all');await page.locator('[data-testid=note-new]').click();await page.locator('[data-testid=note-title]').fill('Separate place');await page.locator('[data-testid=note-body]').fill('Exact route and place details');await page.locator('[data-testid=note-save]').click();await page.locator('[data-testid=note-project-confirm]').click();
  await page.waitForFunction(()=>document.querySelector('[data-testid=notes-message]')?.textContent.includes('Saved. Original')) ;assert.equal(await page.locator('#blazor-error-ui').isVisible(),false);assert.equal(browserErrors.some(e=>e.includes('Unhandled exception')),false);
  const stored=await page.evaluate(async account=>{const db=await import('/js/storage/indexedDbStorage.js');const digest=await crypto.subtle.digest('SHA-256',new TextEncoder().encode(account));const scope=Array.from(new Uint8Array(digest),b=>b.toString(16).padStart(2,'0')).join('');return JSON.parse(await db.getNotes(scope));},account);
  assert.equal(stored.notes.length,2);assert.equal(stored.folders.length,2);assert.equal(stored.notes.find(n=>n.current.title==='Separate place').current.project.projectId,project);assert.equal(stored.notes.find(n=>n.current.title==='Separate hotel').current.project,undefined);assert.equal(stored.notes.find(n=>n.current.title==='Separate hotel').current.markdown,'# Hotel\nExact details 🧥');
  results.push({kind:'Hydrated Release routes and real IndexedDB',engine,routes:['/notes','/todo/projects'],checks:['Separate standalone folder and note','Explicit reviewed folder placement','Persistent reload','Markdown import remains unsaved and unfiled','Exact project folder and separate place note','No implicit standalone-project reassociation'],qualification:'Local synthetic provider only; actual released Blazor runtime and adapter; service workers blocked'});
  await c.close();contexts.delete(c);
 }
}catch(error){const overflow=await lastPage?.evaluate(()=>[...document.body.querySelectorAll('*')].map(el=>{const r=el.getBoundingClientRect();return{tag:el.tagName,id:el.dataset.testid,classes:el.className,left:r.left,right:r.right,width:r.width,scroll:el.scrollWidth,client:el.clientWidth,text:el.textContent.slice(0,100)};}).filter(r=>r.right>innerWidth+1||r.left<0||r.scroll>r.client+1)).catch(()=>[]);await writeFile(join(out,'failure.json'),JSON.stringify({error:String(error),results,browserErrors,overflow,url:lastPage?.url()},null,2));if(lastPage){await lastPage.screenshot({path:join(out,'failure.png'),fullPage:true}).catch(()=>{});await writeFile(join(out,'failure.html'),await lastPage.content().catch(()=>''));}throw error;}
finally{clearTimeout(deadline);await Promise.allSettled([...contexts].map(c=>c.close()));server.closeAllConnections();await new Promise(r=>server.close(r));await Promise.all(profiles.map(p=>rm(p,{recursive:true,force:true})));}
const expected=(mode==='journeys'?0:names.length*2*2*2*2)+(mode==='matrix'?0:2);assert.equal(results.length,expected);
const report={identity,results,counts:{expected,discovered:results.length,passed:results.length,failed:0,skipped:0},cleanup:'Owned loopback server and both generated persistent profiles closed and removed. No external request, real login, cloud write, device permission or subscription.',limits:['Physical devices and assistive technologies unqualified','Hosted OAuth and cloud paths covered only by separate synthetic API/MCP controls','Service-worker lifecycle is outside the changed folder UI; full-app checks block service workers']};await writeFile(join(out,'report.json'),JSON.stringify(report,null,2)+'\n');
