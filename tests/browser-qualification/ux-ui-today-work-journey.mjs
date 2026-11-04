import assert from 'node:assert/strict';
import {resolve} from 'node:path';

export async function runJourney({page,origin,engine,scheme,width,text,web,qualifyNativeDate}) {
  const account='00000000-0000-4000-8000-000000000011',workspace='00000000-0000-4000-8000-000000000201',task='00000000-0000-4000-8000-000000000202';
  await page.setViewportSize({width,height:900});await page.goto(origin+'/__qualification/seed');
  await page.evaluate(async({account,workspace,task,scheme})=>{
    const db=await import('/js/storage/indexedDbStorage.js');await db.setJson('auth/persistentCredentials',JSON.stringify({userId:account,apiToken:'synthetic-local-only'}));await db.setJson('preferences/colorSchemes',JSON.stringify({selectedSchemeId:scheme.id,customSchemes:[],schemaVersion:2,selectedAtUtc:new Date().toISOString()}));
    if(!await db.getWorkspace(account,workspace))await db.commitWorkspace(account,0,JSON.stringify({document:{schemaVersion:1,id:workspace,revision:1,tasks:[{id:task,revision:1,text:'Synthetic planned work',kind:'todo',state:'active',priority:'normal',createdAtUtc:'2026-01-01T00:00:00Z',updatedAtUtc:'2026-01-01T00:00:00Z',plannedDate:'2026-01-17',reviewAfterUtc:'2026-01-16T00:00:00Z'}]},storeVersion:1,commands:[],events:[],authority:null}));
  },{account,workspace,task,scheme});
  await page.goto(origin+'/todo/today');await page.locator('[data-readiness="ready"]').waitFor();
  await page.evaluate(({scheme,text})=>{HabiticaColorScheme.applyColorScheme(scheme);document.documentElement.style.fontSize=text===200?'32px':'16px';},{scheme,text});
  const menu=page.getByRole('button',{name:'Main navigation',exact:true});if(width<960&&await menu.getAttribute('aria-expanded')==='true')await menu.click();
  const day=page.getByTestId('today-date');await day.fill('2026-01-17');await day.blur();
  const complete=page.getByTestId('today-complete-'+task);await page.waitForFunction(id=>{const el=document.querySelector(`[data-testid="today-complete-${id}"]`);return el&&!el.matches(':disabled');},task);
  const groups=page.getByRole('region',{name:'Tasks for the selected day',exact:true}),reminders=page.getByTestId('todo-reminders'),details=page.getByTestId('reminder-configuration'),toggle=page.getByTestId('reminder-settings-toggle');
  const order=await groups.evaluate(el=>{const reminders=document.querySelector('[data-testid="todo-reminders"]'),readiness=document.querySelector('[data-testid="workspace-readiness"]');return {workBeforeReminders:Boolean(el.compareDocumentPosition(reminders)&Node.DOCUMENT_POSITION_FOLLOWING),readinessBeforeWork:Boolean(readiness.compareDocumentPosition(el)&Node.DOCUMENT_POSITION_FOLLOWING),workBottom:el.getBoundingClientRect().bottom,reminderTop:reminders.getBoundingClientRect().top};});
  assert.ok(order.workBeforeReminders&&order.readinessBeforeWork&&order.workBottom<=order.reminderTop+1,'Daily work precedes optional setup in document and visual order');
  assert.equal(await details.evaluate(el=>el.open),false);assert.equal(await page.getByTestId('reminder-inapp').isVisible(),false);
  await page.waitForFunction(()=>document.querySelector('[data-testid="reminder-summary"]')?.textContent.includes('In-app cues: off'));
  const permission=await page.evaluate(()=>globalThis.Notification?.permission??'unsupported');
  const nativeDate=await qualifyNativeDate(day);assert.equal(nativeDate.qualification,'pass');
  await complete.click();await page.locator('[data-readiness="command-review"]').waitFor();
  assert.equal(await complete.isDisabled(),true);assert.equal(await page.getByTestId('todo-review').isVisible(),true);assert.equal(await details.evaluate(el=>el.open),false);
  assert.equal(await page.getByTestId('todo-review').evaluate(el=>Boolean(el.compareDocumentPosition(document.querySelector('.todo-today-groups'))&Node.DOCUMENT_POSITION_FOLLOWING)),true);
  await page.getByTestId('todo-cancel').click();await page.locator('[data-readiness="ready"]').waitFor();assert.equal(await day.inputValue(),'2026-01-17');
  await complete.focus();for(let step=0;step<8;step++){await page.keyboard.press(engine==='webkit'?'Alt+Tab':'Tab');if(await toggle.evaluate(el=>document.activeElement===el))break;}
  assert.equal(await toggle.evaluate(el=>document.activeElement===el&&parseFloat(getComputedStyle(el).outlineWidth)>0),true,'Keyboard reaches visible reminder disclosure after work');
  await toggle.press('Enter');assert.equal(await details.evaluate(el=>el.open),true);
  await page.getByTestId('reminder-inapp').click();await page.waitForFunction(()=>document.querySelector('[data-testid="reminder-summary"]')?.textContent.includes('1 reached'));
  await toggle.press('Enter');assert.equal(await details.evaluate(el=>el.open),false);assert.match(await page.getByTestId('reminder-summary').innerText(),/In-app cues: enabled/);
  assert.equal(await page.evaluate(()=>globalThis.Notification?.permission??'unsupported'),permission);
  await day.fill('2026-01-18');await day.blur();await page.getByTestId('today-empty').waitFor();assert.equal(await day.inputValue(),'2026-01-18');
  await toggle.press('Enter');assert.match(await page.getByTestId('reminder-inapp').innerText(),/TURN OFF|Turn off/);
  if(width===360||width===1280)await page.screenshot({animations:'disabled',path:resolve(web,'../..',`${engine}-${scheme.id}-${width}-${text}-today-reminders-open.png`)});
  await page.getByTestId('reminder-inapp').click();await page.waitForFunction(()=>document.querySelector('[data-testid="reminder-summary"]')?.textContent.includes('In-app cues: off'));await toggle.press('Enter');
  assert.equal(await details.evaluate(el=>el.open),false);assert.equal(await day.inputValue(),'2026-01-18');
  if(width===360||width===1280)await page.screenshot({animations:'disabled',path:resolve(web,'../..',`${engine}-${scheme.id}-${width}-${text}-today-work-first.png`)});
  const journal=await page.evaluate(async({account,workspace})=>{const db=await import('/js/storage/indexedDbStorage.js');return JSON.parse(await db.getWorkspace(account,workspace));},{account,workspace});assert.deepEqual(journal.commands,[]);assert.deepEqual(journal.events,[]);assert.equal(journal.document.tasks[0].state,'active');assert.equal(journal.document.tasks[0].plannedDate,'2026-01-17');
  assert.equal(await page.locator('#blazor-error-ui').isVisible(),false);
  return {engine,theme:scheme.id,width,text,routes:['/todo/today'],nativeDate,checks:['work/readiness/configuration visual and document order','populated and explicit empty selected day','original preview visible and completion fenced before cancel','keyboard reminder disclosure after work','local in-app preference survives closing/reopening and day edit','no notification permission or task/command writes'],order,qualification:'Current Release with isolated synthetic task and local in-app preference only. Browser notifications are never enabled and permissions remain unchanged. Physical device delivery and assistive technology remain unqualified.'};
}
