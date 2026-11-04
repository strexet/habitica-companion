import assert from 'node:assert/strict';
import {resolve} from 'node:path';

export async function runJourney({page,origin,engine,scheme,width,text,web}) {
  await page.setViewportSize({width,height:900});await page.goto(origin+'/__qualification/seed');
  const account='00000000-0000-4000-8000-000000000011';
  await page.evaluate(async({account,scheme})=>{const db=await import('/js/storage/indexedDbStorage.js');await db.setJson('auth/persistentCredentials',JSON.stringify({userId:account,apiToken:'synthetic-local-only'}));await db.setJson('preferences/colorSchemes',JSON.stringify({selectedSchemeId:scheme.id,customSchemes:[],schemaVersion:2,selectedAtUtc:new Date().toISOString()}));},{account,scheme});
  async function ready(){await page.locator('[data-readiness="ready"]').waitFor();await page.waitForFunction(()=>['todo-title','todo-add'].every(id=>{const el=document.querySelector(`[data-testid="${id}"]`);return el&&!el.matches(':disabled');}));await page.evaluate(({scheme,text})=>{HabiticaColorScheme.applyColorScheme(scheme);document.documentElement.style.fontSize=text===200?'32px':'16px';if(document.documentElement.dataset.colorScheme!==scheme.id)throw Error('Selected category scheme was not applied');},{scheme,text});const menu=page.getByRole('button',{name:'Main navigation',exact:true});if(width<960&&await menu.getAttribute('aria-expanded')==='true')await menu.click();}
  const focus=[];
  async function inspect(category,focused) {
    await ready();const heading=page.locator('#main-content h1');
    assert.equal(await heading.count(),1,category+' exposes one page heading');assert.equal(await heading.innerText(),category==='habits'?'Habits':'Dailies');
    const current=page.getByTestId('todo-category-tabs').locator('[aria-current="page"]');assert.equal(await current.count(),1);assert.equal(await current.getAttribute('href'),'/todo/'+category);
    assert.equal(await page.getByTestId('todo-title').evaluate(el=>Boolean(el.closest('details'))),false);
    if(focused){await page.waitForFunction(()=>document.activeElement===document.querySelector('#main-content h1'));const box=await heading.evaluate(el=>{const r=el.getBoundingClientRect(),header=document.querySelector('header').getBoundingClientRect(),s=getComputedStyle(el);return {top:r.top,bottom:r.bottom,headerBottom:header.bottom,viewport:innerHeight,outline:parseFloat(s.outlineWidth)};});assert.ok(box.outline>0&&box.top>=box.headerBottom-1&&box.bottom<=box.viewport+1,JSON.stringify({category,box}));focus.push({category,...box});}
    if((width===360||width===1280)&&focused)await page.screenshot({animations:'disabled',path:resolve(web,'../..',`${engine}-${scheme.id}-${width}-${text}-category-${category}-focus.png`)});
    assert.equal(await page.locator('#blazor-error-ui').isVisible(),false);
  }
  await page.goto(origin+'/todo/habits');await inspect('habits',false);
  await page.getByTestId('todo-title').fill('Retained unsaved Habit');
  const daily=page.getByTestId('todo-category-tabs').getByRole('link',{name:'Dailies',exact:true});await daily.focus();await daily.press('Enter');await page.waitForURL(url=>url.pathname==='/todo/dailies');await inspect('dailies',true);
  await page.goBack();await inspect('habits',true);assert.equal(await page.getByTestId('todo-title').inputValue(),'Retained unsaved Habit');
  await page.goForward();await inspect('dailies',true);
  await page.reload();await inspect('dailies',false);
  const journal=await page.evaluate(async account=>{const db=await import('/js/storage/indexedDbStorage.js');const ids=await db.listWorkspaces(account);if(ids.length!==1)throw Error('One isolated browser workspace expected');return JSON.parse(await db.getWorkspace(account,ids[0]));},account);
  assert.deepEqual(journal.commands,[]);assert.deepEqual(journal.events,[]);assert.deepEqual(journal.document.tasks,[]);
  return {engine,theme:scheme.id,width,text,routes:['/todo/habits','/todo/dailies'],checks:['direct category loads','one descriptive h1 and active tab','keyboard client navigation','back/forward heading focus and viewport clearance','unsaved capture retained without submission','no accepted commands'],focus,qualification:'Current Release native routing and isolated Browser source; populated, empty and frozen category content is additionally captured from real Razor tests. Physical screen-reader announcements and device history behavior remain unqualified.'};
}
