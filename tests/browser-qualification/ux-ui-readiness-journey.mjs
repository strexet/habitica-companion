import assert from 'node:assert/strict';
import {resolve} from 'node:path';

const account='00000000-0000-4000-8000-000000000011';
const workspace='00000000-0000-4000-8000-000000000201';

// Native IndexedDB transactions hold actual adapter reads. No production hook,
// rewritten asset or fabricated component event substitutes for the application.
async function holdWorkspace(page) {
  await page.evaluate(({account,workspace})=>new Promise((resolve,reject)=>{
    const request=indexedDB.open('habitica-tool');request.onerror=()=>reject(request.error);
    request.onsuccess=()=>{
      const db=request.result,tx=db.transaction('workspaces','readwrite');
      window.__qualificationReadHold=true;
      tx.oncomplete=tx.onabort=()=>db.close();
      let entered=false;
      function pump(){const read=tx.objectStore('workspaces').get([account,workspace]);
        read.onsuccess=()=>{if(!entered){entered=true;resolve();}if(window.__qualificationReadHold)pump();};}
      pump();
    };
  }),{account,workspace});
}
async function changeDocument(page,invalid) {
  await page.evaluate(({account,workspace,invalid})=>new Promise((resolve,reject)=>{
    const request=indexedDB.open('habitica-tool');request.onerror=()=>reject(request.error);
    request.onsuccess=()=>{
      const db=request.result,tx=db.transaction('workspaces','readwrite'),store=tx.objectStore('workspaces');
      tx.oncomplete=()=>{db.close();resolve();};tx.onerror=()=>{db.close();reject(tx.error);};
      const read=store.get([account,workspace]);read.onsuccess=()=>{
        const row=read.result;
        if(invalid){window.__qualificationOriginalDocument=row.documentJson;const d=JSON.parse(row.documentJson);d.revision=0;row.documentJson=JSON.stringify(d);}
        else row.documentJson=window.__qualificationOriginalDocument;
        store.put(row);
      };
    };
  }),{account,workspace,invalid});
}

export async function runJourney({page,origin,engine,scheme,width,text,routes,web,qualifyNativeDate}) {
  await page.setViewportSize({width,height:900});
  await page.goto(origin+'/__qualification/seed');
  await page.evaluate(async({account,workspace,scheme})=>{
    const db=await import('/js/storage/indexedDbStorage.js');
    await db.setJson('auth/persistentCredentials',JSON.stringify({userId:account,apiToken:'synthetic-local-only'}));
    await db.setJson('preferences/colorSchemes',JSON.stringify({selectedSchemeId:scheme.id,customSchemes:[],schemaVersion:2,selectedAtUtc:new Date().toISOString()}));
    if(!await db.getWorkspace(account,workspace))await db.commitWorkspace(account,0,JSON.stringify({document:{schemaVersion:1,id:workspace,revision:1,tasks:[]},storeVersion:1,commands:[],events:[],authority:null}));
  },{account,workspace,scheme});
  await page.goto(origin+routes[0]);
  await page.locator('[data-readiness="ready"]').waitFor();
  await page.evaluate(({scheme,text})=>{HabiticaColorScheme.applyColorScheme(scheme);document.documentElement.style.fontSize=text===200?'32px':'16px';},{scheme,text});
  const observed=[];let nativeDate;
  const tab=engine==='webkit'?'Alt+Tab':'Tab',backTab=engine==='webkit'?'Alt+Shift+Tab':'Shift+Tab';
  for(const route of routes) {
    const menu=page.getByRole('button',{name:'Main navigation',exact:true});
    if(new URL(page.url()).pathname!==route) {
      if(await menu.getAttribute('aria-expanded')!=='true')await menu.click();
      const routeLink=page.locator(`#primary-navigation a[href="${route}"]:not(.nav-capture)`),group=routeLink.locator('xpath=ancestor::details[1]');if(await group.count()&&!await group.evaluate(el=>el.open))await group.locator(':scope > summary').click();await routeLink.click();
      await page.waitForURL(url=>url.pathname===route);
    }
    await page.locator('[data-readiness="ready"]').waitFor();
    if(width<960&&await menu.getAttribute('aria-expanded')==='true')await menu.click();
    if(route==='/todo/today'){const day=page.getByTestId('today-date');await day.blur();nativeDate=await qualifyNativeDate(day);assert.equal(nativeDate.qualification,'pass');}
    const status=page.getByTestId('workspace-readiness');
    assert.match(await status.innerText(),/Browser workspace.*Loaded/s);
    const details=page.locator('.todo-source-picker');
    if(!await details.evaluate(el=>el.open))await details.locator('summary').click();
    const reload=details.getByRole('button',{name:'Reload Workspace',exact:true});
    await holdWorkspace(page);
    try {
      await reload.click();
      await page.locator('[data-readiness="operation-in-progress"]').waitFor();
      assert.match(await status.innerText(),/Wait/);
      assert.equal(await status.locator('button,a').count(),0,'Loading offers a wait instruction');
      assert.equal(await reload.isDisabled(),true,'Actual reload handler owns busy state');
    } finally {await page.evaluate(()=>{window.__qualificationReadHold=false;});}
    await page.locator('[data-readiness="ready"]').waitFor();
    await changeDocument(page,true);await reload.click();
    await page.locator('[data-readiness="reload-required"]').waitFor();
    assert.match(await status.innerText(),/unavailable/);
    assert.doesNotMatch(await page.getByTestId('todo-direct-panel').innerText(),/check its receipt before another save/);
    assert.equal(await status.locator('button,a').count(),1,'Terminal load failure exposes one recovery action');
    const recovery=status.getByRole('button',{name:'Reload selected workspace',exact:true});
    await recovery.focus();
    await page.keyboard.press(backTab);await page.keyboard.press(tab);
    assert.equal(await recovery.evaluate(el=>{const s=getComputedStyle(el);return document.activeElement===el&&parseFloat(s.outlineWidth)>0&&s.outlineStyle!=='none';}),true,'Recovery has visible keyboard focus');
    if((width===360||width===1280)&&(route==='/todo/today'||route==='/todo/tasks'))await page.screenshot({path:resolve(web,'../..',`${engine}-${scheme.id}-${width}-${text}-readiness-${route.split('/').pop()}.png`),fullPage:true});
    await changeDocument(page,false);await recovery.press('Enter');
    await page.locator('[data-readiness="ready"]').waitFor();
    assert.match(await status.innerText(),/Task editing is available/);
    const title=page.getByTestId('todo-title');if(await title.count())assert.equal(await title.isDisabled(),false);
    assert.equal(await page.locator('#blazor-error-ui').isVisible(),false);
    if(route==='/todo/tasks') {
      await page.getByTestId('todo-title').fill('Synthetic retained review');
      await page.getByTestId('todo-add').click();await page.locator('[data-readiness="command-review"]').waitFor();
      const original=await page.getByTestId('todo-review').innerText();
      const reviewAction=page.getByTestId('workspace-readiness-action');await reviewAction.focus();await reviewAction.press('Enter');
      const heading=page.getByTestId('original-request-title');
      assert.equal(await heading.evaluate(el=>document.activeElement===el&&parseFloat(getComputedStyle(el).outlineWidth)>0),true,'Original request heading receives visible keyboard focus');
      const focusBox=await heading.evaluate(el=>{const r=el.getBoundingClientRect(),header=document.querySelector('header')?.getBoundingClientRect();return {top:r.top,bottom:r.bottom,headerBottom:header?.bottom??0,height:innerHeight};});
      assert.ok(focusBox.top>=focusBox.headerBottom-1&&focusBox.bottom<=focusBox.height+1,JSON.stringify({originalRequestFocus:focusBox}));
      if(width===360||width===1280)await page.screenshot({path:resolve(web,'../..',`${engine}-${scheme.id}-${width}-${text}-original-review-focus.png`)});
      assert.equal(await page.getByTestId('todo-review').innerText(),original,'Focusing review preserves its original command');
      assert.equal(await page.getByTestId('todo-title').isDisabled(),true);
      await page.getByTestId('todo-cancel').click();await page.locator('[data-readiness="ready"]').waitFor();
      assert.equal(await page.getByTestId('todo-title').inputValue(),'Synthetic retained review');
      assert.equal(await heading.count(),0);
    }
    observed.push(route);
  }
  const journal=await page.evaluate(async({account,workspace})=>{const db=await import('/js/storage/indexedDbStorage.js');return JSON.parse(await db.getWorkspace(account,workspace));},{account,workspace});
  assert.deepEqual(journal.commands,[]);assert.deepEqual(journal.events,[]);assert.deepEqual(journal.document.tasks,[]);
  return {engine,width,text,theme:scheme.id,routes:observed,nativeDate,checks:['native IndexedDB lock exposes actual loading','busy controls refuse repeat','invalid stored revision yields terminal failure','single recovery above source disclosure','keyboard reload restores exact source','original preview focus and cancel retain draft without acceptance','same-engine native Today date ink','no command or task writes'],qualification:'Hydrated current Release with isolated native IndexedDB; synthetic lock/corruption restored before continuation. No provider writes or access recovery.'};
}
