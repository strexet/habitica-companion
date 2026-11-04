import assert from 'node:assert/strict';
import {resolve} from 'node:path';

export async function runJourney({page,origin,engine,scheme,width,text,web,appearanceOnly=false}) {
  await page.setViewportSize({width,height:900});await page.goto(origin+'/__qualification/seed');
  await page.evaluate(async scheme=>{const db=await import('/js/storage/indexedDbStorage.js');await db.setJson('auth/persistentCredentials',JSON.stringify({userId:'00000000-0000-4000-8000-000000000011',apiToken:'synthetic-local-only'}));await db.setJson('preferences/colorSchemes',JSON.stringify({selectedSchemeId:scheme.id,customSchemes:[],schemaVersion:2,selectedAtUtc:new Date().toISOString()}));},scheme);
  await page.goto(origin+'/dashboard');await page.getByTestId('dashboard-account-readiness').waitFor();
  await page.waitForFunction(()=>document.querySelector('[data-testid="dashboard-account-readiness"]')?.textContent.includes('up to date')&&!document.querySelector('#app-refresh')?.matches(':disabled'));
  await page.evaluate(({scheme,text})=>{HabiticaColorScheme.applyColorScheme(scheme);document.documentElement.style.fontSize=text===200?'32px':'16px';},{scheme,text});
  const menu=page.getByRole('button',{name:'Main navigation',exact:true});if(width<960&&await menu.getAttribute('aria-expanded')==='true')await menu.click();
  const readiness=page.getByTestId('dashboard-account-readiness'),account=page.getByTestId('dashboard-refresh-UserProfile');
  const originalStamp=await readiness.locator('time').getAttribute('datetime');assert.ok(Date.parse(originalStamp));assert.doesNotMatch(await account.innerText(),/not refreshed/);
  const toggle=page.getByTestId('dashboard-appearance-toggle');await toggle.focus();await page.keyboard.press(engine==='webkit'?'Alt+Shift+Tab':'Shift+Tab');await page.keyboard.press(engine==='webkit'?'Alt+Tab':'Tab');assert.equal(await toggle.evaluate(el=>document.activeElement===el&&parseFloat(getComputedStyle(el).outlineWidth)>0),true);
  const geometry=await toggle.evaluate(el=>{const r=el.getBoundingClientRect(),label=el.querySelector('.mud-button-label')||el,s=getComputedStyle(label),canvas=document.createElement('canvas'),ctx=canvas.getContext('2d');ctx.font=`${s.fontStyle} ${s.fontWeight} ${s.fontSize} ${s.fontFamily}`;return {left:r.left,right:r.right,width:r.width,height:r.height,labelWidth:label.clientWidth,word:Math.max(...label.textContent.trim().split(/\s+/).map(w=>ctx.measureText(w).width))};});
  assert.ok(geometry.left>=0&&geometry.right<=width+1&&geometry.width>=44&&geometry.height>=44&&geometry.labelWidth>=geometry.word-1,JSON.stringify({appearanceGeometry:geometry,width,text}));
  await toggle.press('Enter');await page.getByTestId('color-scheme-select').waitFor();await toggle.press('Enter');assert.equal(await page.getByTestId('color-scheme-select').count(),0);
  if(appearanceOnly){await toggle.scrollIntoViewIfNeeded();if(width===360||width===1280)await page.screenshot({animations:'disabled',path:resolve(web,'../..',`${engine}-${scheme.id}-${width}-${text}-dashboard-appearance.png`)});return {engine,theme:scheme.id,width,text,routes:['/dashboard'],checks:['full Appearance row retains a readable label and44-pixel primary target','native keyboard focus and open/close behavior','loaded account retrieval time retained'],geometry,qualification:'Current Release with isolated synthetic GETs; physical devices and assistive technology unqualified.'};}
  let release;const gate=new Promise(resolve=>release=resolve);let intercepted=0;
  const handler=async route=>{intercepted++;await gate;await route.fulfill({status:400,contentType:'application/json',body:JSON.stringify({success:false,message:'Synthetic unavailable account refresh'})});};
  await page.route('https://habitica.com/api/v3/user',handler);
  await page.locator('#app-refresh').click();await page.waitForFunction(()=>document.querySelector('[data-testid="dashboard-refresh-UserProfile"]')?.textContent.includes('refreshing'));
  assert.equal(await readiness.locator('time').getAttribute('datetime'),originalStamp);release();
  await page.waitForFunction(()=>document.querySelector('[data-testid="dashboard-refresh-UserProfile"]')?.textContent.includes('failed'));
  assert.ok(intercepted>0);assert.match(await readiness.innerText(),/up to date/);assert.equal(await readiness.locator('time').getAttribute('datetime'),originalStamp);assert.equal(await account.locator('time').count(),0);assert.equal(await account.evaluate(el=>el.classList.contains('danger-pill')),true);
  await readiness.scrollIntoViewIfNeeded();
  if(width===360||width===1280)await page.screenshot({animations:'disabled',path:resolve(web,'../..',`${engine}-${scheme.id}-${width}-${text}-dashboard-failed.png`)});
  await page.unroute('https://habitica.com/api/v3/user',handler);await page.locator('#app-refresh').click();await page.waitForFunction(()=>document.querySelector('[data-testid="dashboard-refresh-UserProfile"]')?.textContent.includes('completed'));
  assert.equal(await account.locator('time').count(),1);assert.ok(Date.parse(await account.locator('time').getAttribute('datetime')));assert.equal(await account.evaluate(el=>el.classList.contains('success-pill')),true);
  assert.equal(await page.locator('#blazor-error-ui').isVisible(),false);
  return {engine,theme:scheme.id,width,text,routes:['/dashboard'],checks:['real loaded snapshot retrieval provenance','keyboard appearance disclosure','held synthetic GET exposes fetching with retained snapshot','failed refresh remains explicit beside fresh retained cache','retry completes with real refreshed timestamp'],qualification:'Current Release with isolated synthetic provider GETs. No provider writes, account consent or publication. Stale/missing/sign-out/account-switch and absent timestamps additionally covered by real Razor component transitions; physical devices and assistive technology unqualified.'};
}
