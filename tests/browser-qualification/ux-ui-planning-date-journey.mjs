import assert from 'node:assert/strict';
import {resolve} from 'node:path';

export async function runJourney({page,origin,engine,scheme,width,text,web,qualifyNativeDate}) {
  const account='00000000-0000-4000-8000-000000000011',workspace='00000000-0000-4000-8000-000000000201';
  await page.setViewportSize({width,height:900});await page.goto(origin+'/__qualification/seed');
  await page.evaluate(async({account,workspace,scheme})=>{const db=await import('/js/storage/indexedDbStorage.js');await db.setJson('auth/persistentCredentials',JSON.stringify({userId:account,apiToken:'synthetic-local-only'}));await db.setJson('preferences/colorSchemes',JSON.stringify({selectedSchemeId:scheme.id,customSchemes:[],schemaVersion:2,selectedAtUtc:new Date().toISOString()}));if(!await db.getWorkspace(account,workspace))await db.commitWorkspace(account,0,JSON.stringify({document:{schemaVersion:1,id:workspace,revision:1,tasks:[]},storeVersion:1,commands:[],events:[],authority:null}));},{account,workspace,scheme});
  const dates=[],routes=['/todo/plan','/todo/inbox','/todo/statistics'];
  for(const route of routes) {
    await page.goto(origin+route);
    if(route==='/todo/statistics')await page.waitForFunction(()=>document.querySelector('[aria-label="Companion statistics"]')?.textContent.includes('Selected Browser workspace'));
    else await page.locator('[data-readiness="ready"]').waitFor();
    await page.evaluate(({scheme,text})=>{HabiticaColorScheme.applyColorScheme(scheme);document.documentElement.style.fontSize=text===200?'32px':'16px';},{scheme,text});
    const menu=page.getByRole('button',{name:'Main navigation',exact:true});if(width<960&&await menu.getAttribute('aria-expanded')==='true')await menu.click();
    const date=page.getByTestId(route==='/todo/statistics'?'stats-day':'plan-day');
    assert.equal(await date.getAttribute('type'),'date');const limits={min:await date.getAttribute('min'),max:await date.getAttribute('max')};
    await date.fill('2026-01-17');await date.blur();assert.equal(await date.inputValue(),'2026-01-17');
    const paint=await qualifyNativeDate(date);assert.equal(paint.qualification,'pass',route+': complete native selected date');dates.push({route,...paint});
    await date.focus();const focus=await date.evaluate(el=>{const s=getComputedStyle(el),r=el.getBoundingClientRect(),header=document.querySelector('header').getBoundingClientRect();return {active:document.activeElement===el,outline:parseFloat(s.outlineWidth),top:r.top,bottom:r.bottom,headerBottom:header.bottom,height:innerHeight};});
    assert.ok(focus.active&&focus.outline>0&&focus.top>=focus.headerBottom-1&&focus.bottom<=focus.height+1,JSON.stringify({route,focus}));
    assert.deepEqual({min:await date.getAttribute('min'),max:await date.getAttribute('max')},limits);
    if(width===360||width===1280)await page.screenshot({animations:'disabled',path:resolve(web,'../..',`${engine}-${scheme.id}-${width}-${text}-native-${route.split('/').pop()}.png`)});
    assert.equal(await page.locator('#blazor-error-ui').isVisible(),false);
  }
  const journal=await page.evaluate(async({account,workspace})=>{const db=await import('/js/storage/indexedDbStorage.js');return JSON.parse(await db.getWorkspace(account,workspace));},{account,workspace});assert.equal(journal.document.id,workspace);assert.deepEqual(journal.commands,[]);assert.deepEqual(journal.events,[]);assert.deepEqual(journal.document.tasks,[]);
  return {engine,theme:scheme.id,width,text,routes,nativeDate:{qualification:'pass',cases:dates},checks:['three real native civil-date edits','same-engine complete selected-date ink','unchanged native date limits','visible focus and header clearance','exact source and no task/journal writes'],qualification:'Scoped current Release headers and real desktop Chromium/WebKit native controls. Physical pickers, mobile keyboards, other locales and assistive technology remain unqualified.'};
}
