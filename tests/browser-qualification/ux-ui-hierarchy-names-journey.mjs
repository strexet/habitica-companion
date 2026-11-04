import assert from 'node:assert/strict';
import {resolve} from 'node:path';

export async function runJourney({page,origin,engine,scheme,width,text,web}) {
  const account='00000000-0000-4000-8000-000000000011',workspace='00000000-0000-4000-8000-000000000201';
  await page.setViewportSize({width,height:900});await page.goto(origin+'/__qualification/seed');
  const journal=await page.evaluate(async()=>{const response=await fetch('/matrix/hierarchy-names-tasks-journal.json');if(!response.ok)throw Error('Missing owned hierarchy journal');return response.json();});
  journal.document.id=workspace;const tasks=journal.document.tasks,roots=tasks.filter(task=>!task.parentTaskId),child=tasks.find(task=>task.parentTaskId),project=journal.document.projects[0].id;assert.equal(roots.length,2);assert.ok(child);
  await page.evaluate(async({account,workspace,scheme,journal})=>{
    const db=await import('/js/storage/indexedDbStorage.js');await db.setJson('auth/persistentCredentials',JSON.stringify({userId:account,apiToken:'synthetic-local-only'}));await db.setJson('preferences/colorSchemes',JSON.stringify({selectedSchemeId:scheme.id,customSchemes:[],schemaVersion:2,selectedAtUtc:new Date().toISOString()}));
    if(!await db.getWorkspace(account,workspace))await db.commitWorkspace(account,0,JSON.stringify(journal));
  },{account,workspace,scheme,journal});
  const read=()=>page.evaluate(async({account,workspace})=>{const db=await import('/js/storage/indexedDbStorage.js');return JSON.parse(await db.getWorkspace(account,workspace));},{account,workspace});
  const normalize=name=>name.replace(/\s+/g,' ').trim(),observed=[];
  async function focus(control){await control.focus();await page.keyboard.press(engine==='webkit'?'Alt+Shift+Tab':'Shift+Tab');await page.keyboard.press(engine==='webkit'?'Alt+Tab':'Tab');assert.equal(await control.evaluate(el=>document.activeElement===el&&parseFloat(getComputedStyle(el).outlineWidth)>0),true,'Visible native keyboard focus');}
  await page.goto(origin+'/todo/tasks');await page.locator('[data-readiness="ready"]').waitFor();const before=await read();
  for(const route of ['/todo/tasks','/todo/projects']){
    if(new URL(page.url()).pathname!==route){await page.goto(origin+route);await page.locator('[data-readiness="ready"]').waitFor();}
    await page.evaluate(({scheme,text})=>{HabiticaColorScheme.applyColorScheme(scheme);document.documentElement.style.fontSize=text===200?'32px':'16px';},{scheme,text});const menu=page.getByRole('button',{name:'Main navigation',exact:true});if(width<960&&await menu.getAttribute('aria-expanded')==='true')await menu.click();
    if(route.endsWith('projects'))await page.getByTestId('hierarchy-panel').getByTestId('plan-project').selectOption(project);
    const rows=page.locator('[data-testid^="tree-row-"]');await rows.first().waitFor();assert.equal(await rows.count(),2);
    for(const task of roots){const row=page.getByTestId('tree-row-'+task.id);assert.ok((await row.innerText()).includes('Task reference: '+task.id));
      for(const action of await row.locator('.todo-row-actions button,.todo-row-actions a').all()){const name=normalize(await action.getAttribute('aria-label'));assert.ok(name.includes(task.id)&&name.includes(normalize(task.text)));const role=await action.evaluate(el=>el.tagName==='A'?'link':'button');assert.equal(await page.getByRole(role,{name,exact:true}).count(),1);}
      for(const summary of await row.locator('summary').all()){const id=await summary.getAttribute('aria-controls'),region=page.locator('[id="'+id+'"]');assert.equal(await region.count(),1);assert.equal(await region.isVisible(),false);await focus(summary);await summary.press('Enter');assert.equal(await region.isVisible(),true);await summary.press('Space');assert.equal(await region.isVisible(),false);}
    }
    const complete=page.getByTestId('tree-complete-'+roots[1].id);await focus(complete);await complete.press('Enter');await page.getByTestId('todo-review').waitFor();assert.ok((await page.getByTestId('todo-review').innerText()).includes(roots[1].id));
    for(const action of await rows.locator('.todo-row-actions button').all())assert.equal(await action.isDisabled(),true);
    if(width===360||width===1280)await page.screenshot({animations:'disabled',path:resolve(web,'../..',`${engine}-${scheme.id}-${width}-${text}-hierarchy-names-${route.split('/').pop()}-frozen.png`),fullPage:true});
    await page.getByTestId('todo-cancel').click();assert.equal(await complete.isEnabled(),true);
    const expand=page.getByTestId('tree-expand-'+roots[0].id);assert.equal(await expand.getAttribute('aria-expanded'),null,'Branch navigation does not invent disclosure state');await focus(expand);await expand.press('Enter');
    const context=page.getByTestId('tree-context');await context.waitFor();assert.ok((await context.innerText()).includes(roots[0].id));
    for(const action of await context.locator('.todo-row-actions button,.todo-row-actions a').all())assert.ok((await action.getAttribute('aria-label')).includes(roots[0].id));
    const parent=page.getByTestId('tree-open-parent-'+child.id);assert.ok((await parent.getAttribute('aria-label')).includes(roots[0].id));await focus(parent);await parent.press('Enter');await context.waitFor();assert.ok((await context.innerText()).includes(roots[0].id));
    const edit=page.getByTestId('tree-edit-'+child.id);await focus(edit);await edit.press('Enter');await page.waitForFunction(value=>document.querySelector('[data-testid="todo-title"]')?.value===value,child.text);await page.getByRole('button',{name:'New task',exact:true}).click();
    const geometry=await page.evaluate(()=>({width:document.documentElement.clientWidth,scrollWidth:document.documentElement.scrollWidth}));assert.ok(geometry.scrollWidth<=geometry.width+1,JSON.stringify(geometry));
    if(width===360||width===1280)await page.screenshot({animations:'disabled',path:resolve(web,'../..',`${engine}-${scheme.id}-${width}-${text}-hierarchy-names-${route.split('/').pop()}.png`),fullPage:true});
    assert.deepEqual(await read(),before,'Review cancellation, branch navigation and edit cancellation preserve exact original journal');assert.equal(await page.locator('#blazor-error-ui').isVisible(),false);observed.push(route);
  }
  return {engine,theme:scheme.id,width,text,routes:observed,checks:['distinct browser-normalized task action names','native Notes and References expansion and stable targets','exact completion review and original disabled fences','branch navigation and parent return retain exact records','exact edit draft cancelled without writes','visible keyboard focus and full-route width'],qualification:'Owned current Release and C# synthetic Browser journal. No commands accepted, provider writes, consent, or publication. Physical devices and assistive technology remain unqualified.'};
}
