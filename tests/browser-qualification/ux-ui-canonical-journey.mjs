import assert from 'node:assert/strict';
import {resolve} from 'node:path';

export async function runJourney({page,origin,engine,scheme,width,text,web,qualifyNativeDate}) {
  const account='00000000-0000-4000-8000-000000000011',workspace='00000000-0000-4000-8000-000000000201',project='00000000-0000-4000-8000-000000000211';
  await page.setViewportSize({width,height:900});await page.goto(origin+'/__qualification/seed');
  await page.evaluate(async({account,workspace,project,scheme})=>{
    const db=await import('/js/storage/indexedDbStorage.js');await db.setJson('auth/persistentCredentials',JSON.stringify({userId:account,apiToken:'synthetic-local-only'}));await db.setJson('preferences/colorSchemes',JSON.stringify({selectedSchemeId:scheme.id,customSchemes:[],schemaVersion:2,selectedAtUtc:new Date().toISOString()}));
    if(!await db.getWorkspace(account,workspace))await db.commitWorkspace(account,0,JSON.stringify({document:{schemaVersion:1,id:workspace,revision:1,tasks:[],projects:[{id:project,revision:1,name:'Synthetic scoped project'}]},storeVersion:1,commands:[],events:[],authority:null}));
  },{account,workspace,project,scheme});
  const observed=new Set(),nativeDates=[];
  const tab=engine==='webkit'?'Alt+Tab':'Tab',backTab=engine==='webkit'?'Alt+Shift+Tab':'Shift+Tab';
  async function activate(link){await link.focus();await page.keyboard.press(backTab);await page.keyboard.press(tab);assert.equal(await link.evaluate(el=>document.activeElement===el&&parseFloat(getComputedStyle(el).outlineWidth)>0),true,'Destination link has visible native keyboard focus');const r=await link.boundingBox();assert.ok(r.width>=44&&r.height>=44,'Destination link retains a primary target');await link.press('Enter');}
  async function ready(route) {
    await page.waitForURL(url=>url.pathname===route);
    if(route==='/todo/statistics')await page.locator(`[data-testid="stats-project"] option[value="${project}"]`).waitFor({state:'attached'});
    else await page.locator('[data-readiness="ready"]').waitFor();
    await page.evaluate(({scheme,text})=>{HabiticaColorScheme.applyColorScheme(scheme);document.documentElement.style.fontSize=text===200?'32px':'16px';},{scheme,text});
    const menu=page.getByRole('button',{name:'Main navigation',exact:true});if(width<960&&await menu.getAttribute('aria-expanded')==='true')await menu.click();
    assert.equal(await page.locator('#blazor-error-ui').isVisible(),false);observed.add(route);
  }
  const history=`/todo/history?projectId=${project}&workspaceId=${workspace}&source=browser`;
  await page.goto(origin+'/todo/plan');await ready('/todo/plan');
  const before=await page.evaluate(async({account,workspace})=>{const db=await import('/js/storage/indexedDbStorage.js');return await db.getWorkspace(account,workspace);},{account,workspace});
  for(const path of ['/todo/plan','/todo/inbox',history,'/todo/statistics']) {
    const route=new URL(path,origin).pathname;
    if(new URL(page.url()).pathname!==route) {await page.goto(origin+path);await ready(route);}
    if(route==='/todo/history')assert.equal(await page.getByTestId('history-project').inputValue(),project);
    const date=page.getByTestId(route==='/todo/statistics'?'stats-day':'plan-day');
    if(await date.count()){await date.blur();const evidence=await qualifyNativeDate(date);assert.equal(evidence.qualification,'pass',route+': selected native date is readable');nativeDates.push({route,...evidence});}
    const header=page.locator(route==='/todo/statistics'?'.todo-stat-section':route==='/todo/history'?'.todo-history-header':'.todo-planning-header').first();
    const link=header.getByRole('link',{name:route.startsWith('/todo/plan')||route==='/todo/inbox'?'Add task':'Open tasks',exact:true});
    assert.equal(await link.getAttribute('href'),'/todo/tasks');
    await activate(link);await ready('/todo/tasks');
    await page.waitForFunction(()=>{const el=document.querySelector('[data-testid="todo-title"]');return el&&!el.matches(':disabled');});
    assert.match(await page.getByTestId('todo-active-source').textContent(),new RegExp(workspace));
    assert.equal(await page.locator('.tasks-header h1').count(),0,'Canonical destination does not expose the provider-cache panel');
    await page.goBack();await ready(route);
    if(route==='/todo/history'){assert.equal(await page.getByTestId('history-project').inputValue(),project);assert.equal(new URL(page.url()).searchParams.get('workspaceId'),workspace);}
    if(width===360||width===1280)await page.screenshot({animations:'disabled',path:resolve(web,'../..',`${engine}-${scheme.id}-${width}-${text}-canonical-${route.split('/').pop()}.png`)});
  }
  const menu=page.getByRole('button',{name:'Main navigation',exact:true});if(await menu.getAttribute('aria-expanded')!=='true')await menu.click();
  const legacy=page.locator('#primary-navigation a[href="/tasks"]');assert.match(await legacy.innerText(),/Habitica tasks/);
  await activate(legacy);await ready('/tasks');await page.getByRole('heading',{name:'Habitica tasks',exact:true}).waitFor();
  assert.equal(await page.locator('.tasks-header').count(),1);await page.goBack();await ready('/todo/statistics');
  const after=await page.evaluate(async({account,workspace})=>{const db=await import('/js/storage/indexedDbStorage.js');return await db.getWorkspace(account,workspace);},{account,workspace});assert.equal(after,before,'Link activation preserves the exact selected workspace and journal');
  return {engine,theme:scheme.id,width,text,routes:[...observed],checks:['four ordinary capture/return links reach canonical tasks','keyboard activation and native history','explicit Habitica cache destination remains separate','selected source and scoped history parameters retained','no task or command writes'],nativeDate:{qualification:'pass',cases:nativeDates},qualification:'Current Release and isolated synthetic Browser workspace/project. No provider effects, account recovery or publication; cloud and physical-device navigation remain unqualified.'};
}
