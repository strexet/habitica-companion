import assert from 'node:assert/strict';
import {resolve} from 'node:path';

export async function runJourney({page,origin,engine,scheme,width,text,web}) {
  await page.setViewportSize({width,height:900});await page.goto(origin+'/__qualification/seed');
  await page.evaluate(async scheme=>{const db=await import('/js/storage/indexedDbStorage.js');await db.setJson('auth/persistentCredentials',JSON.stringify({userId:'00000000-0000-4000-8000-000000000011',apiToken:'synthetic-local-only'}));await db.setJson('preferences/colorSchemes',JSON.stringify({selectedSchemeId:scheme.id,customSchemes:[],schemaVersion:2,selectedAtUtc:new Date().toISOString()}));await db.setJson('preferences/petsMountsPage',JSON.stringify({foldedGroups:{base:false,'magic-potion':false,quest:true,premium:true,wacky:true,special:true,'hatching-potions':true}}));},scheme);
  await page.goto(origin+'/pets-mounts');await page.getByTestId('companion-search-results').waitFor();
  await page.waitForFunction(()=>[...document.querySelectorAll('.inline-banner')].some(el=>el.textContent.includes('Account data is up to date')));
  await page.evaluate(({scheme,text})=>{HabiticaColorScheme.applyColorScheme(scheme);document.documentElement.style.fontSize=text===200?'32px':'16px';},{scheme,text});
  const menu=page.getByRole('button',{name:'Main navigation',exact:true});if(width<960&&await menu.getAttribute('aria-expanded')==='true')await menu.click();
  const inventory=await page.evaluate(async()=>{const db=await import('/js/storage/indexedDbStorage.js');return JSON.parse(await db.getJson('user/latestSnapshot')).inventory;});
  const group=page.getByTestId('pets-mounts-group-magic-potion');
  const cards=kind=>group.locator(`[data-testid^="${kind}-card-"]`);
  assert.equal(await cards('pet').count(),20);assert.equal(await cards('mount').count(),20);
  const first=await cards('pet').evaluateAll(els=>els.map(el=>el.dataset.testid));
  const next=page.getByTestId('companion-next-magic-potion-pets');await next.focus();await page.keyboard.press(engine==='webkit'?'Alt+Shift+Tab':'Shift+Tab');await page.keyboard.press(engine==='webkit'?'Alt+Tab':'Tab');
  assert.equal(await next.evaluate(el=>document.activeElement===el&&parseFloat(getComputedStyle(el).outlineWidth)>0),true,'Pagination has visible native keyboard focus');await next.press('Enter');
  assert.match(await page.getByTestId('companion-range-magic-potion-pets').innerText(),/21–40/);
  const second=await cards('pet').evaluateAll(els=>els.map(el=>el.dataset.testid));assert.equal(second.length,20);assert.equal(second.some(id=>first.includes(id)),false);
  assert.match(await page.getByTestId('companion-range-magic-potion-mounts').innerText(),/1–20/);
  const nameWidths=await cards('pet').evaluateAll(cards=>cards.map(card=>{const title=card.querySelector('.habitica-item-identity strong'),identity=title.closest('.habitica-item-identity'),style=getComputedStyle(title),canvas=document.createElement('canvas'),ctx=canvas.getContext('2d');ctx.font=`${style.fontStyle} ${style.fontWeight} ${style.fontSize} ${style.fontFamily}`;const spacing=parseFloat(style.letterSpacing)||0;const word=Math.max(...title.textContent.trim().split(/\s+/).map(word=>ctx.measureText(word).width+Math.max(0,word.length-1)*spacing));return {name:title.textContent,copyWidth:title.parentElement.clientWidth,required:Math.min(identity.clientWidth,word)};}));
  assert.ok(nameWidths.every(row=>row.copyWidth>=row.required-1),JSON.stringify({narrowCompanionNames:nameWidths.filter(row=>row.copyWidth<row.required-1)}));
  if(width===360||width===1280)await page.screenshot({animations:'disabled',path:resolve(web,'../..',`${engine}-${scheme.id}-${width}-${text}-companions-page-two.png`)});
  const key=second[0].slice('pet-card-'.length);await page.getByTestId('select-hatch-'+key).click();await page.getByTestId('hatch-queue-card-'+key).waitFor();
  const addAll=page.getByTestId('add-group-hatch-queue-magic-potion');const remaining=Number((await addAll.textContent()).match(/\d+/)[0]);assert.ok(remaining>20,'All matching candidates extend beyond the rendered page');
  await addAll.click();const queued=page.locator('[data-testid^="hatch-queue-card-"]');assert.equal(await queued.count(),remaining+1);assert.equal(new Set(await queued.evaluateAll(els=>els.map(el=>el.dataset.testid))).size,remaining+1);
  await page.getByTestId('execute-hatch-queue').click();const confirm=page.getByTestId('confirm-hatch-queue');await confirm.waitFor();await confirm.locator('..').getByRole('button',{name:'Cancel',exact:true}).click();
  assert.equal(await confirm.count(),0);assert.equal(await queued.count(),remaining+1);await page.getByTestId('clear-hatch-queue').click();assert.equal(await queued.count(),0);
  const sale=await page.locator('.bulk-sell-list').textContent();const search=page.getByTestId('pets-mounts-search');await search.fill('no-synthetic-match-anywhere');
  assert.equal(await page.getByTestId('companion-no-results').count(),1);assert.equal(await page.locator('[data-testid^="pet-card-"],[data-testid^="mount-card-"],[data-testid^="hatching-potion-"]').count(),0);assert.equal(await page.locator('.bulk-sell-list').textContent(),sale);
  await search.fill('Wolf');await page.getByTestId('pet-type-filter').selectOption('Wolf');await page.getByTestId('mount-type-filter').selectOption('Wolf');assert.match(await page.getByTestId('companion-range-magic-potion-pets').innerText(),/1–9/);
  await page.getByTestId('toggle-pets-mounts-group-magic-potion').click();assert.equal(await cards('pet').count(),0);
  await search.fill('Wolf-White');assert.equal(await cards('pet').count(),1);assert.equal(await cards('mount').count(),1);
  const reset=page.getByTestId('companion-reset-filters');await reset.focus();await page.keyboard.press(engine==='webkit'?'Alt+Shift+Tab':'Shift+Tab');await page.keyboard.press(engine==='webkit'?'Alt+Tab':'Tab');assert.equal(await reset.evaluate(el=>document.activeElement===el&&parseFloat(getComputedStyle(el).outlineWidth)>0),true);await reset.press('Enter');
  assert.equal(await search.inputValue(),'');assert.equal(await page.getByTestId('pet-type-filter').inputValue(),'');assert.equal(await page.getByTestId('mount-type-filter').inputValue(),'');assert.match(await page.getByTestId('companion-range-magic-potion-pets').innerText(),/1–20/);
  const after=await page.evaluate(async()=>{const db=await import('/js/storage/indexedDbStorage.js');return JSON.parse(await db.getJson('user/latestSnapshot')).inventory;});assert.deepEqual(after,inventory,'Filtering, local queue and preview cancellation leave provider inventory unchanged');
  assert.equal(await page.locator('#blazor-error-ui').isVisible(),false);
  return {engine,theme:scheme.id,width,text,routes:['/pets-mounts'],checks:['retained expanded collection is bounded','independent stable pet/mount paging','visible keyboard pager/reset focus','off-page queue selections and all matching candidates','explicit preview cancelled before provider execution','single no-results/reset and separate sale scope','filtered group folding and query reset','unchanged cached provider inventory'],matchingHatchCandidates:remaining+1,qualification:'Current Release with a hashed owned synthetic provider GET fixture. No hatch, feed, sale or equip request is authorized; the harness blocks every provider write. Physical devices and assistive technology remain unqualified.'};
}
