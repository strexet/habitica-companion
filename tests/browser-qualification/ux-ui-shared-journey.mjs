import assert from 'node:assert/strict';

// Real Blazor event handlers and native IndexedDB; no synthetic HTML handlers
// substitute for application routing or selected-day state transitions.
export async function runJourney({page,origin,engine,scheme,width,text,web,qualifyNativeDate}) {
  await page.goto(origin+'/__qualification/seed');
  const account='00000000-0000-4000-8000-000000000011',workspace='00000000-0000-4000-8000-000000000201';
  await page.evaluate(async({account,workspace})=>{
    const db=await import('/js/storage/indexedDbStorage.js');
    await db.setJson('auth/persistentCredentials',JSON.stringify({userId:account,apiToken:'synthetic-local-only'}));
    if(!await db.getWorkspace(account,workspace))await db.commitWorkspace(account,0,JSON.stringify({document:{schemaVersion:1,id:workspace,revision:1,tasks:[]},storeVersion:1,commands:[],events:[],authority:null}));
  },{account,workspace});
  await page.goto(origin+'/todo/today');
  const input=page.getByTestId('today-date');await input.waitFor();
  await page.evaluate(({scheme,text})=>{
    HabiticaColorScheme.applyColorScheme(scheme);
    document.documentElement.style.fontSize=text===200?'32px':'16px';
  },{scheme,text});
  await page.setViewportSize({width,height:900});
  const overlay=page.locator('.mud-drawer-overlay');
  if(await overlay.isVisible())await overlay.click({position:{x:5,y:5}});
  const today=await page.evaluate(()=>{const d=new Date();return `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`;});
  await input.fill('2026-01-17');await input.press('Tab');assert.equal(await input.inputValue(),'2026-01-17');
  await page.getByRole('button',{name:"Use today's device date",exact:true}).click();
  await page.waitForFunction(date=>document.querySelector('[data-testid="today-date"]')?.value===date,today);
  await input.blur();const pixels=await qualifyNativeDate(input);
  await page.locator('#main-content').getByRole('link',{name:'Add task',exact:true}).click();
  await page.getByRole('heading',{name:'Tasks and subtasks',exact:true}).waitFor();assert.equal(new URL(page.url()).pathname,'/todo/tasks');
  const link=page.locator('.shell-content').getByRole('link',{name:'Today',exact:true}).first();await link.focus();await page.keyboard.press('Tab');
  assert.ok(await page.evaluate(()=>{const el=document.activeElement;if(!el||el===document.body)return false;const s=getComputedStyle(el);return parseFloat(s.outlineWidth)>0&&s.outlineStyle!=='none';}),'Visible focus after keyboard navigation');
  await page.goBack();await page.getByTestId('today-date').waitFor();
  assert.equal(await page.locator('#blazor-error-ui').isVisible(),false);
  return {engine,width,text,theme:scheme.id,routes:['/todo/today','/todo/tasks'],checks:['actual native date edit','hydrated Today reset','canonical link route without assuming writable capture','visible keyboard focus','browser back'],nativeDate:pixels,qualification:'Synthetic current Release handlers and isolated IndexedDB; date pixels are a separate product baseline observation. Native disabled refusal is asserted in independent paint controls.'};
}
