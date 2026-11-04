import assert from 'node:assert/strict';
import {resolve} from 'node:path';

export async function runJourney({page,origin,engine,scheme,width,text,web,qualifyNativeDate},{dailyOnly=false,recurringOnly=false,settingsOnly=false}={}) {
  const account='00000000-0000-4000-8000-000000000011',workspace='00000000-0000-4000-8000-000000000201';
  await page.setViewportSize({width,height:900});await page.goto(origin+'/__qualification/seed');
  const tasks=await page.evaluate(async()=>{
    const journals=await Promise.all(['todo','habit','daily'].map(async kind=>{const response=await fetch('/matrix/workspace-names-'+kind+'-journal.json');if(!response.ok)throw Error('Missing owned task journal');return response.json();}));
    return journals.flatMap(journal=>journal.document.tasks);
  });
  assert.equal(tasks.length,6);assert.equal(new Set(tasks.map(task=>task.id)).size,6);
  await page.evaluate(async({account,workspace,scheme,tasks})=>{
    const db=await import('/js/storage/indexedDbStorage.js');await db.setJson('auth/persistentCredentials',JSON.stringify({userId:account,apiToken:'synthetic-local-only'}));
    await db.setJson('preferences/colorSchemes',JSON.stringify({selectedSchemeId:scheme.id,customSchemes:[],schemaVersion:2,selectedAtUtc:new Date().toISOString()}));
    if(!await db.getWorkspace(account,workspace))await db.commitWorkspace(account,0,JSON.stringify({document:{schemaVersion:1,id:workspace,revision:Math.max(...tasks.map(task=>task.revision)),tasks},storeVersion:Math.max(...tasks.map(task=>task.revision)),commands:[],events:[],authority:null}));
  },{account,workspace,scheme,tasks});
  const readJournal=()=>page.evaluate(async({account,workspace})=>{const db=await import('/js/storage/indexedDbStorage.js');return JSON.parse(await db.getWorkspace(account,workspace));},{account,workspace});
  await page.goto(origin+(dailyOnly?'/todo/dailies':recurringOnly?'/todo/habits':'/todo/tasks'));await page.locator('[data-readiness="ready"]').waitFor();const before=await readJournal();
  const observed=[],dates=[],nameWidths=[],actionWords=[];
  const normalize=name=>name.replace(/\s+/g,' ').trim();
  async function appearance(){await page.evaluate(({scheme,text})=>{HabiticaColorScheme.applyColorScheme(scheme);document.documentElement.style.fontSize=text===200?'32px':'16px';},{scheme,text});const menu=page.getByRole('button',{name:'Main navigation',exact:true});if(width<960&&await menu.getAttribute('aria-expanded')==='true')await menu.click();}
  async function focus(el){await el.focus();await page.keyboard.press(engine==='webkit'?'Alt+Shift+Tab':'Shift+Tab');await page.keyboard.press(engine==='webkit'?'Alt+Tab':'Tab');assert.equal(await el.evaluate(el=>document.activeElement===el&&parseFloat(getComputedStyle(el).outlineWidth)>0),true,'Visible native keyboard focus');}
  async function navigate(route){if(new URL(page.url()).pathname!==route){const tab=page.getByTestId('todo-category-tabs').locator(`a[href="${route}"]`);if(await tab.count())await tab.click();else{const menu=page.getByRole('button',{name:'Main navigation',exact:true});if(await menu.getAttribute('aria-expanded')!=='true')await menu.click();const routeLink=page.locator(`#primary-navigation a[href="${route}"]:not(.nav-capture)`),group=routeLink.locator('xpath=ancestor::details[1]');if(await group.count()&&!await group.evaluate(el=>el.open))await group.locator(':scope > summary').click();await routeLink.click();}await page.waitForURL(url=>url.pathname===route);}}
  for(const [kind,route] of [['todo','/todo/tasks'],['habit','/todo/habits'],['daily','/todo/dailies']].filter(([kind])=>(!dailyOnly||kind==='daily')&&(!recurringOnly||kind!=='todo')&&!settingsOnly)) {
    await navigate(route);await page.locator('[data-readiness="ready"]').waitFor();await appearance();
    const selected=tasks.filter(task=>task.kind===kind);assert.equal(selected.length,2);const rows=page.locator(kind==='todo'?'[data-testid^="tree-row-"]':'.todo-direct-list .todo-direct-row');assert.equal(await rows.count(),2);
    for(const task of selected){const row=page.getByTestId((kind==='todo'?'tree-row-':'todo-row-')+task.id);assert.match(await row.innerText(),new RegExp(task.id));
      for(const button of await row.locator('.todo-row-actions button,.todo-row-actions a').all()){
        const name=normalize(await button.getAttribute('aria-label'));assert.ok(name.includes(task.id)&&name.includes(normalize(task.text)));assert.equal(await page.getByRole(await button.evaluate(el=>el.tagName==='A'?'link':'button'),{name,exact:true}).count(),1);
      }
      const summary=row.locator('details summary').filter({hasText:'Notes'}),region=page.locator('[id="'+await summary.getAttribute('aria-controls')+'"]');assert.equal(await region.isVisible(),false);await focus(summary);await summary.press('Enter');assert.equal(await region.isVisible(),true);assert.match(await region.innerText(),/Retained private task notes/);await summary.press('Space');assert.equal(await region.isVisible(),false);
      if(kind==='daily'){const date=page.getByTestId('todo-occurrence-'+task.id);assert.match(await date.getAttribute('aria-label'),new RegExp(task.id));await date.fill('2026-11-04');await date.blur();}
    }
    const widths=await rows.locator('.todo-row-content > strong').evaluateAll(els=>els.map(el=>{const s=getComputedStyle(el),r=el.getBoundingClientRect(),canvas=document.createElement('canvas'),ctx=canvas.getContext('2d');ctx.font=`${s.fontStyle} ${s.fontWeight} ${s.fontSize} ${s.fontFamily}`;return {text:el.textContent,width:r.width,available:el.closest('.todo-direct-row').clientWidth,word:Math.max(...el.textContent.trim().split(/\s+/).map(word=>ctx.measureText(word).width))};}));
    assert.ok(widths.every(row=>row.width>=Math.min(row.available,row.word)-2),JSON.stringify({taskNameColumns:widths}));nameWidths.push({kind,widths});
    if(kind==='daily'){const start=page.getByTestId('daily-start');if(await start.count()){await start.fill('2026-11-03');await start.blur();}for(const task of selected){const result=await qualifyNativeDate(page.getByTestId('todo-occurrence-'+task.id));assert.equal(result.qualification,'pass',JSON.stringify(result));dates.push(result);}}
    const task=selected[1],action=page.getByTestId((kind==='habit'?'todo-down-':kind==='todo'?'tree-complete-':'todo-complete-')+task.id);await focus(action);await action.press('Enter');await page.getByTestId('todo-review').waitFor();
    assert.equal(await page.getByTestId('todo-title').isDisabled(),true);for(const button of await rows.locator('.todo-row-actions button').all())assert.equal(await button.isDisabled(),true);
    assert.equal(await action.getAttribute('aria-label').then(name=>name.includes(task.id)),true);
    const labels=await page.locator('.todo-direct-list .todo-row-actions .mud-button-label,.todo-task-panel .todo-command-review .mud-button-label').evaluateAll(els=>els.map(el=>{
      const words=[],walker=document.createTreeWalker(el,NodeFilter.SHOW_TEXT);
      for(let node;node=walker.nextNode();)for(const match of node.textContent.matchAll(/\S+/g)){
        const range=document.createRange();range.setStart(node,match.index);range.setEnd(node,match.index+match[0].length);
        words.push({text:match[0],lines:new Set([...range.getClientRects()].filter(r=>r.width&&r.height).map(r=>Math.round(r.top))).size});
      }
      return {text:el.textContent.trim(),words};
    }));
    assert.ok(labels.length&&labels.every(label=>label.words.length&&label.words.every(word=>word.lines===1)),JSON.stringify({splitActionWords:labels}));actionWords.push({kind,labels});
    if(width===360||width===1280)await page.screenshot({animations:'disabled',path:resolve(web,'../..',`${engine}-${scheme.id}-${width}-${text}-workspace-names-${kind}.png`),fullPage:true});
    await page.getByTestId('todo-cancel').click();await page.locator('[data-readiness="ready"]').waitFor();assert.equal(await action.isEnabled(),true);observed.push(route);
  }
  if(!dailyOnly&&!recurringOnly){
  await navigate('/settings');await page.getByTestId('cloud-sync-status').waitFor();await appearance();
  const checkboxes=page.locator('.sync-section-row input[type="checkbox"]');assert.ok(await checkboxes.count()>1);
  const labels=await checkboxes.evaluateAll(els=>els.map(el=>el.getAttribute('aria-label')));assert.equal(new Set(labels).size,labels.length);assert.ok(labels.every(label=>/^Exclude .+ from private device sync$/.test(label)));
  const original=await checkboxes.evaluateAll(els=>els.map(el=>({id:el.id,checked:el.checked,name:el.getAttribute('aria-label')})));
  for(const item of original){const control=page.getByLabel(item.name,{exact:true});assert.equal(await control.getAttribute('id'),item.id);assert.ok(await control.evaluate(el=>[...el.labels].some(label=>label.htmlFor===el.id)));}
  const first=page.getByLabel(original[0].name,{exact:true});await focus(first);await first.press('Space');assert.equal(await first.isChecked(),!original[0].checked);
  assert.deepEqual(await checkboxes.evaluateAll(els=>els.slice(1).map(el=>el.checked)),original.slice(1).map(row=>row.checked));await first.press('Space');assert.deepEqual(await checkboxes.evaluateAll(els=>els.map(el=>el.checked)),original.map(row=>row.checked));
  if(width===360||width===1280)await page.screenshot({animations:'disabled',path:resolve(web,'../..',`${engine}-${scheme.id}-${width}-${text}-workspace-names-sync.png`),fullPage:true});
  observed.push('/settings');
  const settingWords=await page.locator('.settings-shell .mud-button-label').evaluateAll(els=>els.filter(el=>el.getClientRects().length&&getComputedStyle(el).visibility!=='hidden').map(el=>{
    const words=[],walker=document.createTreeWalker(el,NodeFilter.SHOW_TEXT);
    for(let node;node=walker.nextNode();)for(const match of node.textContent.matchAll(/\S+/g)){const range=document.createRange();range.setStart(node,match.index);range.setEnd(node,match.index+match[0].length);words.push({text:match[0],lines:new Set([...range.getClientRects()].filter(r=>r.width&&r.height).map(r=>Math.round(r.top))).size});}
    return {text:el.textContent.trim(),words};
  }));
  assert.ok(settingWords.length&&settingWords.every(label=>label.words.length&&label.words.every(word=>word.lines===1)),JSON.stringify({splitSettingsWords:settingWords}));
  const sectionWidths=await page.locator('.sync-section-row strong').evaluateAll(els=>els.map(el=>{
    const s=getComputedStyle(el),owner=el.closest('.settings-card'),os=getComputedStyle(owner),canvas=document.createElement('canvas'),ctx=canvas.getContext('2d');ctx.font=`${s.fontStyle} ${s.fontWeight} ${s.fontSize} ${s.fontFamily}`;
    return {text:el.textContent,width:el.getBoundingClientRect().width,available:owner.clientWidth-parseFloat(os.paddingLeft)-parseFloat(os.paddingRight),word:Math.max(...el.textContent.trim().split(/\s+/).map(word=>ctx.measureText(word).width))};
  }));
  assert.ok(sectionWidths.length&&sectionWidths.every(row=>row.width>=Math.min(row.available,row.word)-2),JSON.stringify({privateSectionColumns:sectionWidths}));
  actionWords.push({kind:'settings',labels:settingWords});nameWidths.push({kind:'settings',widths:sectionWidths});
  const geometry=await page.evaluate(()=>({width:document.documentElement.clientWidth,scrollWidth:document.documentElement.scrollWidth}));assert.ok(geometry.scrollWidth<=geometry.width+1,JSON.stringify(geometry));
  }
  const after=await readJournal();assert.deepEqual(after,before,'Task reviews/cancellation and private section toggles preserve the original task journal');assert.equal(await page.locator('#blazor-error-ui').isVisible(),false);
  return {engine,theme:scheme.id,width,text,routes:observed,nameWidths,actionWords,nativeDate:{qualification:'pass',cases:dates},checks:['browser-normalized same-name task actions retain exact record references','native Notes disclosures have stable controlled content','keyboard exact action prepares review and disables row actions','review cancelled without task, command or event writes',...(!dailyOnly&&!recurringOnly?['labelled exact private sections toggle independently and restore original selections']:[]),'visible native focus, intact row-action words and selected Daily date paint'],qualification:'Current Release with isolated Browser journal assembled from reviewed C# synthetic fixtures. No confirmation, provider write, consent or sync upload is executed. Physical devices and assistive technology remain unqualified.'};
}
