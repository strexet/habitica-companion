import {collectEquipmentReadability} from './ux-ui-equipment-metrics.mjs';
import {assessEquipmentReadability} from './qualification-core.mjs';
import assert from 'node:assert/strict';
import {resolve} from 'node:path';

async function wordLineControls(page) {
  await page.evaluate(()=>{
    const canvas=document.createElement('canvas'),ctx=canvas.getContext('2d');ctx.font='32px Arial';const word='RECOMMENDATION',width=ctx.measureText(word).width;
    const root=document.createElement('div');root.className='inventory-shell';root.style.width='800px';document.body.append(root);
    for(const delta of [4,-.5]){const button=document.createElement('button');Object.assign(button.style,{display:'block',width:(width+delta)+'px',boxSizing:'border-box',padding:'0',border:'0',font:'32px Arial',lineHeight:'1.5',color:'black',background:'white'});const label=document.createElement('span');label.className='mud-button-label';Object.assign(label.style,{display:'block',width:'100%',whiteSpace:'normal',overflowWrap:'anywhere'});label.textContent=word;button.append(label);root.append(button);}
  });
  const result=await page.evaluate(collectEquipmentReadability);assert.equal(result.identities.length,2);
  assert.deepEqual(result.identities[0].wordLineCounts,[1],'Independent full-width action word stays on one line');
  assert.deepEqual(result.identities[1].wordLineCounts,[2],'Independent half-pixel-short action word exposes an actual native line break');
  assert.ok(result.identities[1].wordWidth-result.identities[1].width<2,'Negative control stays inside the old geometry tolerance');
  const errors=assessEquipmentReadability(result);assert.equal(errors.length,1);assert.equal(errors[0].kind,'equipment-name-column');
  return {qualification:'pass',positive:result.identities[0],negative:result.identities[1]};
}

export async function runJourney({page,origin,engine,scheme,width,text,web,partyOnly=false,inventoryOnly=false}) {
  await page.setViewportSize({width,height:900});await page.goto(origin+'/__qualification/seed');
  const wordLineCanaries=await wordLineControls(page);
  const uid='00000000-0000-4000-8000-000000000011';
  await page.evaluate(async({scheme,uid})=>{
    const db=await import('/js/storage/indexedDbStorage.js');
    await db.setJson('auth/persistentCredentials',JSON.stringify({userId:uid,apiToken:'synthetic-local-only'}));
    await db.setJson('preferences/colorSchemes',JSON.stringify({selectedSchemeId:scheme.id,customSchemes:[],schemaVersion:2,selectedAtUtc:new Date().toISOString()}));
    // Retained same-named presets exercise safe record context even if an older
    // or externally seeded store contains names current SaveAsync would reject.
    await db.setJson('inventory/equipmentPresets',JSON.stringify(['one','two'].map((id,i)=>({id:'preset-'+id,userId:uid,kind:0,name:i?'Same  casting  preset':'Same casting preset',createdAtUtc:'2026-11-03T00:00:00Z',slots:{weapon:i?'weapon_int_12':'weapon_str_10'}}))));
  },{scheme,uid});
  await page.goto(origin+'/party');
  const buttons=page.getByTestId('member-details');await buttons.nth(1).waitFor();
  await page.waitForFunction(()=>[...document.querySelectorAll('.inline-banner')].some(el=>el.textContent.includes('Party data is up to date')));
  // A second explicit load qualifies retained Party data present at first render,
  // the path that reads shared quest state and exposes the local endpoint warning.
  await page.reload();await buttons.nth(1).waitFor();
  const appearance=async()=>{
    await page.evaluate(({scheme,text})=>{HabiticaColorScheme.applyColorScheme(scheme);document.documentElement.style.fontSize=text===200?'32px':'16px';},{scheme,text});
    const menu=page.getByRole('button',{name:'Main navigation',exact:true});if(width<960&&await menu.getAttribute('aria-expanded')==='true')await menu.click();
  };
  await appearance();
  const before=await page.evaluate(async()=>{const db=await import('/js/storage/indexedDbStorage.js');return Promise.all(['user/latestSnapshot','inventory/equipmentPresets'].map(key=>db.getJson(key)));});
  const names=await buttons.evaluateAll(els=>els.map(el=>{
    return el.getAttribute('aria-label').replace(/\s+/g,' ').trim();
  }));
  assert.equal(new Set(names).size,2);assert.ok(names.every(name=>name.includes('Same member')&&name.includes('member reference')));
  const ids=await buttons.evaluateAll(els=>els.map(el=>el.getAttribute('aria-controls')));assert.equal(new Set(ids).size,2);
  for(let i=0;i<2;i++){assert.equal(await buttons.nth(i).getAttribute('aria-expanded'),'false');assert.equal(await page.locator('[id="'+ids[i]+'"]').isVisible(),false);}
  const focus=async button=>{await button.focus();await page.keyboard.press(engine==='webkit'?'Alt+Shift+Tab':'Shift+Tab');await page.keyboard.press(engine==='webkit'?'Alt+Tab':'Tab');assert.equal(await button.evaluate(el=>document.activeElement===el&&parseFloat(getComputedStyle(el).outlineWidth)>0),true,'Native action focus remains visible');};
  await focus(buttons.nth(1));await buttons.nth(1).press('Enter');assert.equal(await buttons.nth(1).getAttribute('aria-expanded'),'true');
  assert.equal(await page.locator('[id="'+ids[1]+'"]').isVisible(),true);assert.equal(await page.locator('[id="'+ids[0]+'"]').isVisible(),false);assert.match(await buttons.nth(1).getAttribute('aria-label'),/^Hide details/);
  await buttons.nth(1).press('Space');assert.equal(await buttons.nth(1).getAttribute('aria-controls'),ids[1]);assert.equal(await buttons.nth(1).getAttribute('aria-expanded'),'false');
  const nameGeometry=await page.locator('.party-member-identity strong').evaluateAll(els=>els.map(el=>{const r=el.getBoundingClientRect(),s=getComputedStyle(el),canvas=document.createElement('canvas'),ctx=canvas.getContext('2d');ctx.font=`${s.fontStyle} ${s.fontWeight} ${s.fontSize} ${s.fontFamily}`;return {width:r.width,word:Math.max(...el.textContent.trim().split(/\s+/).map(word=>ctx.measureText(word).width)),available:el.closest('.party-member-card').clientWidth};}));
  assert.ok(nameGeometry.every(row=>row.width>=Math.min(row.available,row.word)-2),JSON.stringify({narrowMemberNames:nameGeometry}));
  const runtimeWarning=page.locator('.shell-content > .inline-banner .mud-alert-message');await runtimeWarning.waitFor();assert.match(await runtimeWarning.innerText(),/Party sync endpoint/);
  const documentGeometry=await page.evaluate(()=>({viewport:document.documentElement.clientWidth,documentWidth:document.documentElement.scrollWidth}));
  assert.ok(documentGeometry.documentWidth<=documentGeometry.viewport+1,JSON.stringify({partyDocument:documentGeometry}));
  if(partyOnly){
    if(width===360||width===1280)await page.screenshot({animations:'disabled',path:resolve(web,'../..',`${engine}-${scheme.id}-${width}-${text}-party-reflow.png`),fullPage:true});
    return {engine,theme:scheme.id,width,text,routes:['/party'],documentGeometry,checks:['hydrated Party including runtime warning stays within viewport','distinct contextual member names','stable closed/open regions via native Enter/Space','visible native keyboard focus','ordinary-word member identity width'],nameGeometry,qualification:'Current Release; synthetic hashed GETs only. No Party mutation. Physical devices and assistive technology unqualified.'};
  }
  // Use the actual SPA navigation destination and preserve the seeded account.
  const menu=page.getByRole('button',{name:'Main navigation',exact:true});if(width<960)await menu.click();
  await page.locator('a[href="/inventory"]').click();await page.getByTestId('battle-preset-name').waitFor();await appearance();
  assert.equal(await page.getByRole('button',{name:'Save battle gear preset',exact:true}).isDisabled(),true);
  for(const [id,label] of [['battle-preset-name','Battle preset name'],['optimizer-preset-name','Recommendation preset name']]) {
    const input=page.getByLabel(label,{exact:true});assert.equal(await input.getAttribute('id'),id);await input.fill('Retained descriptive name');assert.equal(await page.getByLabel(label,{exact:true}).inputValue(),'Retained descriptive name');
  }
  await page.getByTestId('toggle-other-items-Weapon').click();
  for(const key of ['weapon_str_10','weapon_int_12']){
    const action=page.getByTestId('equip-battle-'+key);const name=await action.getAttribute('aria-label');assert.ok(name.includes(key)&&name.toLowerCase().includes((await action.innerText()).trim().toLowerCase()),'Visible action remains within exact record name');
    assert.equal(await page.getByRole('button',{name:name.replace(/\s+/g,' ').trim(),exact:true}).count(),1);
  }
  const bestNames=await page.locator('[data-testid^="equip-best-"]').evaluateAll(els=>els.map(el=>el.getAttribute('aria-label')));assert.equal(new Set(bestNames).size,bestNames.length);assert.ok(bestNames.every(name=>/item reference weapon_/.test(name)));
  for(const id of ['preset-one','preset-two'])for(const action of ['equip','rename','remove']) {
    const button=page.getByTestId(action+'-preset-'+id),name=await button.getAttribute('aria-label');assert.ok(name.includes(id)&&name.toLowerCase().includes((await button.innerText()).trim().toLowerCase()));assert.equal(await page.getByRole('button',{name:name.replace(/\s+/g,' ').trim(),exact:true}).count(),1);
  }
  const rename=page.getByTestId('rename-preset-preset-two');await focus(rename);await rename.press('Enter');
  const input=page.getByTestId('rename-preset-name-preset-two');assert.ok(await input.evaluate(el=>el.labels.length>0&&el.labels[0].textContent.trim().length>0));await input.fill('Retained second name');assert.equal(await input.inputValue(),'Retained second name');
  const save=page.getByTestId('save-rename-preset-preset-two');assert.match(await save.getAttribute('aria-label'),/Save name.*preset-two/);assert.equal(await save.isEnabled(),true);
  assert.match(await page.getByTestId('equip-optimizer-recommendation').getAttribute('aria-label'),/Equip recommendation for Intelligence/);
  await page.getByTestId('remove-preset-preset-one').click();assert.match(await page.getByTestId('confirm-remove-preset-preset-one').getAttribute('aria-label'),/Confirm remove.*preset-one/);await page.getByRole('button',{name:/^Cancel removal.*preset-one/}).click();assert.equal(await page.getByTestId('confirm-remove-preset-preset-one').count(),0);
  const equipmentReadability=await page.evaluate(collectEquipmentReadability);assert.ok(equipmentReadability.identities.length>0&&equipmentReadability.stats.length>0);assert.deepEqual(assessEquipmentReadability(equipmentReadability),[],JSON.stringify({equipmentReadability}));
  const inventoryGeometry=await page.evaluate(()=>({viewport:document.documentElement.clientWidth,documentWidth:document.documentElement.scrollWidth}));
  assert.ok(inventoryGeometry.documentWidth<=inventoryGeometry.viewport+1,JSON.stringify({inventoryDocument:inventoryGeometry}));
  if(width===360||width===1280)await page.screenshot({animations:'disabled',path:resolve(web,'../..',`${engine}-${scheme.id}-${width}-${text}-game-control-names.png`),fullPage:true});
  const after=await page.evaluate(async()=>{const db=await import('/js/storage/indexedDbStorage.js');return Promise.all(['user/latestSnapshot','inventory/equipmentPresets'].map(key=>db.getJson(key)));});assert.deepEqual(after,before,'Disclosure, drafts and removal cancellation preserve snapshots/presets');
  assert.equal(await page.locator('#blazor-error-ui').isVisible(),false);
  return {engine,theme:scheme.id,width,text,routes:inventoryOnly?['/inventory']:['/party','/inventory'],wordLineCanaries,equipmentReadability,checks:['same-named member controls have distinct public context','stable hidden regions and Enter/Space disclosure states','native visible keyboard focus','same-named gear and retained preset names target exact records','persistent new and rename input labels after typing','recommendation goal and disabled equipped action','removal preview cancelled; exact stores unchanged'],qualification:'Current Release; reviewed hashed synthetic GET responses and isolated IndexedDB. No equipment mutation or preset save executed. Existing UI exposes battle actions only. Physical devices and assistive technology unqualified.'};
}
