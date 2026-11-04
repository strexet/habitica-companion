import assert from 'node:assert/strict';
import {resolve} from 'node:path';

async function holdNotes(page,scope){
  await page.evaluate(scope=>new Promise((resolve,reject)=>{
    const request=indexedDB.open('habitica-tool');request.onerror=()=>reject(request.error);
    request.onsuccess=()=>{const db=request.result,tx=db.transaction('notes','readwrite');window.__qualificationNotesHold=true;let entered=false;
      tx.oncomplete=tx.onabort=()=>db.close();tx.onerror=()=>reject(tx.error);
      const pump=()=>{const read=tx.objectStore('notes').get(scope);read.onsuccess=()=>{if(!entered){entered=true;resolve();}if(window.__qualificationNotesHold)pump();};};pump();};
  }),scope);
}
export async function runJourney({page,origin,engine,scheme,width,text,web}){
  const account='00000000-0000-4000-8000-000000000011',coldStart=new URL(page.url()).pathname==='/__qualification/seed';
  await page.setViewportSize({width,height:900});
  if(coldStart){await page.evaluate(async({account,scheme})=>{const db=await import('/js/storage/indexedDbStorage.js');await db.setJson('auth/persistentCredentials',JSON.stringify({userId:account,apiToken:'synthetic-local-only'}));await db.setJson('preferences/colorSchemes',JSON.stringify({selectedSchemeId:scheme.id,customSchemes:[],schemaVersion:2,selectedAtUtc:new Date().toISOString()}));},{account,scheme});await page.goto(origin+'/todo/tasks');}
  async function navigate(route){if(new URL(page.url()).pathname!==route){const menu=page.getByRole('button',{name:'Main navigation',exact:true});if(await menu.getAttribute('aria-expanded')!=='true')await menu.click();await page.locator(`#primary-navigation a[href="${route}"]:not(.nav-capture)`).click();await page.waitForURL(url=>url.pathname===route);}}
  async function appearance(){await page.evaluate(({scheme,text})=>{HabiticaColorScheme.applyColorScheme(scheme);document.documentElement.style.fontSize=text===200?'32px':'16px';},{scheme,text});const menu=page.getByRole('button',{name:'Main navigation',exact:true});if(width<960&&await menu.getAttribute('aria-expanded')==='true')await menu.click();}
  await navigate('/todo/tasks');await page.locator('[data-readiness="ready"]').waitFor();await appearance();
  const stored=await page.evaluate(async account=>{const db=await import('/js/storage/indexedDbStorage.js'),digest=await crypto.subtle.digest('SHA-256',new TextEncoder().encode(account)),scope=[...new Uint8Array(digest)].map(b=>b.toString(16).padStart(2,'0')).join(''),ids=await db.listWorkspaces(account);return {scope,notes:await db.getNotes(scope),workspaceId:ids[0],workspace:await db.getWorkspace(account,ids[0])};},account);
  await holdNotes(page,stored.scope);
  try{
    await navigate('/notes');const panel=page.getByTestId('notes-panel');await panel.locator('[data-testid="notes-initializing"]').waitFor();await appearance();assert.equal(await panel.getAttribute('aria-busy'),'true');
    const entry=page.getByTestId('note-new');assert.equal(await entry.isDisabled(),true);assert.match(await page.getByTestId('notes-initializing').innerText(),/Loading Notes.*Wait/);
    const refusal=await entry.evaluate(el=>{let calls=0;const listener=()=>calls++;el.addEventListener('click',listener);el.click();el.removeEventListener('click',listener);return {disabled:el.matches(':disabled'),calls};});assert.deepEqual(refusal,{disabled:true,calls:0});assert.equal(await page.getByTestId('notes-workbench').evaluateAll(els=>els[0]?.dataset.notesMode??null)==='editor',false);
    if(width===360||width===1280)await page.screenshot({animations:'disabled',path:resolve(web,'../..',`${engine}-${scheme.id}-${width}-${text}-notes-initial-loading.png`)});
  }finally{await page.evaluate(()=>{window.__qualificationNotesHold=false;});}
  await page.waitForFunction(()=>document.querySelector('[data-testid="notes-panel"]')?.getAttribute('aria-busy')==='false'&&!document.querySelector('[data-testid="note-new"]')?.matches(':disabled'));
  const entry=page.getByTestId('note-new');await entry.focus();await page.keyboard.press(engine==='webkit'?'Alt+Shift+Tab':'Shift+Tab');await page.keyboard.press(engine==='webkit'?'Alt+Tab':'Tab');assert.equal(await entry.evaluate(el=>document.activeElement===el&&parseFloat(getComputedStyle(el).outlineWidth)>0),true);
  await entry.press('Enter');await page.waitForFunction(()=>document.querySelector('[data-testid="notes-workbench"]')?.dataset.notesMode==='editor');const title=page.getByTestId('note-title');await title.waitFor();await page.waitForFunction(()=>document.activeElement===document.querySelector('.notes-editor h2'));
  await title.fill('Exact unsaved native entry');await page.getByTestId('note-body').fill('Retain <>& 🧥 without saving');assert.equal(await page.getByTestId('notes-workbench').getAttribute('data-notes-mode'),'editor');assert.equal(await title.inputValue(),'Exact unsaved native entry');
  if(width===360||width===1280)await page.screenshot({animations:'disabled',path:resolve(web,'../..',`${engine}-${scheme.id}-${width}-${text}-notes-entry-ready.png`)});
  await entry.click();await page.getByTestId('note-discard').click();await title.waitFor();assert.equal(await title.inputValue(),'');
  const after=await page.evaluate(async({account,scope,workspaceId})=>{const db=await import('/js/storage/indexedDbStorage.js');return {notes:await db.getNotes(scope),workspace:await db.getWorkspace(account,workspaceId)};},{account,...stored});assert.deepEqual(after,{notes:stored.notes,workspace:stored.workspace});assert.equal(await page.locator('#blazor-error-ui').isVisible(),false);
  return {engine,theme:scheme.id,width,text,coldStart,routes:['/todo/tasks','/notes'],checks:['native IndexedDB held read exposes true component initialization','disabled New note has a readable reason and refuses native click','release enables one keyboard entry with visible focus','editor retains exact unsaved text without notebook/journal writes','original dirty-draft discard remains explicit'],qualification:'Owned current Release, real Notes storage read held without writes and released in finally. Native Chromium/WebKit and actual component states. No provider write, save, consent, permission, publication, service-worker, physical-device or assistive-technology qualification.'};
}
