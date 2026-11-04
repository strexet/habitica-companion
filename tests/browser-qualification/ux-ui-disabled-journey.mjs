import assert from 'node:assert/strict';

export async function runJourney({page,origin,engine,scheme,width,text,routes}) {
  await page.setViewportSize({width,height:900});
  const coldStart=new URL(page.url()).origin!==origin||await page.locator('#main-content').count()===0;
  if(coldStart)await page.goto(origin+'/__qualification/seed');
  await page.evaluate(async schemeId=>{
    const db=await import('/js/storage/indexedDbStorage.js');
    await db.setJson('auth/persistentCredentials',JSON.stringify({userId:'00000000-0000-4000-8000-000000000011',apiToken:'synthetic-local-only'}));
    await db.setJson('preferences/colorSchemes',JSON.stringify({selectedSchemeId:schemeId,customSchemes:[],selectedAtUtc:new Date().toISOString(),schemaVersion:2}));
  },scheme.id);
  if(coldStart){await page.goto(origin+routes[0]);await page.locator('[data-readiness="ready"]').waitFor();}
  await page.evaluate(({scheme,text})=>{HabiticaColorScheme.applyColorScheme(scheme);document.documentElement.style.fontSize=text===200?'32px':'16px';},{scheme,text});
  const observed=[],disabled=[];
  for(const route of routes) {
    const menu=page.getByRole('button',{name:'Main navigation',exact:true});
    if(new URL(page.url()).pathname!==route){
      if(await menu.getAttribute('aria-expanded')!=='true')await menu.click();
      const routeLink=page.locator(`#primary-navigation a[href="${route}"]:not(.nav-capture)`),group=routeLink.locator('xpath=ancestor::details[1]');if(await group.count()&&!await group.evaluate(el=>el.open))await group.locator(':scope > summary').click();await routeLink.click();await page.waitForURL(url=>url.pathname===route);
    }
    if(width<960&&await menu.getAttribute('aria-expanded')==='true')await menu.click();
    if(route.startsWith('/todo/'))await page.locator('[data-readiness="ready"]').waitFor();
    if(route==='/notes'){if(!await page.getByTestId('note-title').isVisible())await page.getByTestId('note-new').click();await page.getByTestId('note-title').waitFor();}
    if(route==='/inventory')await page.locator('.inventory-shell').waitFor();
    if(route==='/todo/lists'&&!await page.getByTestId('saved-list-name').isVisible())await page.getByTestId('saved-list-new').click();
    if(route==='/settings'){
      const toggle=page.getByTestId('settings-appearance-toggle');await toggle.waitFor();
      if((await toggle.textContent()).includes('Customize')){
        await toggle.scrollIntoViewIfNeeded();
        assert.equal(await toggle.evaluate(el=>{const r=el.getBoundingClientRect();return el.contains(document.elementFromPoint(r.left+r.width/2,r.top+r.height/2));}),true,'Settings disclosure receives the pointer');
        await toggle.click();await page.waitForFunction(()=>document.querySelector('[data-testid="settings-appearance-toggle"]')?.textContent.includes('Done'));
      }
      await page.getByTestId('color-scheme-select').waitFor();await page.getByTestId('color-scheme-select').selectOption(scheme.id);
    }
    await page.evaluate(()=>new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve))));
    assert.equal(await page.locator('html').getAttribute('data-color-scheme'),scheme.id,'Stored and rendered scheme remain aligned across route/disclosure loads');
    // Query and inspect inside the same page evaluation. Locator.evaluateAll()
    // may resolve its element array in a separate protocol turn, before a real
    // readiness render enables an initially disabled control.
    const paintSamples=await page.evaluate(expected=>[...document.querySelectorAll('#main-content button:disabled,#main-content input:disabled,#main-content textarea:disabled,#main-content select:disabled')].filter(el=>{const r=el.getBoundingClientRect(),s=getComputedStyle(el);return r.width>0&&r.height>0&&s.visibility!=='hidden';}).map(el=>{
      const button=el.tagName==='BUTTON',nativeDisabled=el.matches(':disabled');
      let calls=0;const listener=()=>calls++;el.addEventListener('click',listener);el.click();el.removeEventListener('click',listener);
      const s=getComputedStyle(el),canvas=document.createElement('canvas');canvas.width=canvas.height=1;
      const ctx=canvas.getContext('2d');ctx.fillStyle=expected;ctx.fillRect(0,0,1,1);const target=[...ctx.getImageData(0,0,1,1).data];
      const painted=s.webkitTextFillColor||s.color;ctx.clearRect(0,0,1,1);ctx.fillStyle=painted;ctx.fillRect(0,0,1,1);const actual=[...ctx.getImageData(0,0,1,1).data];
      return {nativeDisabled,calls,button,opacity:Number(s.opacity),actual,target,text:el.textContent?.trim().slice(0,80)};
    }),scheme.tokens.disabledText);
    for(const sample of paintSamples){
      assert.equal(sample.nativeDisabled,true,JSON.stringify({route,engine,width,text,sample}));assert.equal(sample.calls,0,'Native disabled control refuses click events');
      assert.equal(sample.opacity,1,'No compound control opacity is added');assert.deepEqual(sample.actual,sample.target,'Current palette reaches native disabled text paint');
    }
    const samples=paintSamples.length;
    disabled.push({route,samples});observed.push(route);
    // Native form fields participate in WebKit's default macOS Tab mode;
    // hyperlink traversal depends on a separate host keyboard preference.
    const candidates=await page.locator('#main-content input:not(:disabled):is([type="text"],[type="search"],[type="email"],[type="password"],:not([type])),#main-content textarea:not(:disabled),#main-content select:not(:disabled)').all();
    let enabled;for(const candidate of candidates)if(await candidate.isVisible()){enabled=candidate;break;}
    assert.ok(enabled,'A visible enabled native field provides the keyboard control');
    await enabled.focus();await page.keyboard.press(engine==='webkit'?'Alt+Shift+Tab':'Shift+Tab');await page.keyboard.press(engine==='webkit'?'Alt+Tab':'Tab');
    const focus=await enabled.evaluate(el=>({returned:document.activeElement===el,target:el.outerHTML,active:document.activeElement?.outerHTML}));
    assert.equal(focus.returned,true,'Keyboard traversal returns to an enabled native field: '+JSON.stringify(focus));
    assert.equal(await page.locator('#blazor-error-ui').isVisible(),false);
  }
  assert.ok(disabled.some(row=>row.samples>0),'Actual hydrated disabled controls are observed');
  return {engine,theme:scheme.id,width,text,coldStart,routes:observed,disabled,checks:['current palette reaches actual disabled controls','native disabled click refusal','enabled native field keyboard traversal','actual route hydration'],qualification:'Synthetic current Release; cold start once per engine, then the real hydrated app retains its session across styling cases. Real disabled text/control paint and native refusal. Component fixtures qualify the specific frozen, consent, uncertainty and stale-cache states across the complete palette matrix. Hyperlink Tab mode and physical keyboard preferences are separate. No provider writes.'};
}
