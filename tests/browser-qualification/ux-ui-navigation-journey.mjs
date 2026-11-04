import assert from 'node:assert/strict';
import {headerClearance} from './qualification-core.mjs';

export async function runJourney({page,origin,engine,scheme,width,text,routes=['/dashboard','/todo/today','/todo/tasks','/settings']}) {
  await page.setViewportSize({width,height:900});
  await page.goto(origin+'/__qualification/seed');
  await page.evaluate(async()=>{const db=await import('/js/storage/indexedDbStorage.js');await db.remove('auth/persistentCredentials');});
  await page.goto(origin+'/sign-in');await page.locator('#app-sign-in').waitFor();
  assert.equal(await page.locator('#primary-navigation').count(),0);
  const signedOutSkip=page.getByRole('link',{name:'Skip to main content',exact:true});await signedOutSkip.focus();await signedOutSkip.press('Enter');
  await page.waitForFunction(()=>document.activeElement?.id==='main-content');
  await page.goto(origin+'/__qualification/seed');
  await page.evaluate(async scheme=>{const db=await import('/js/storage/indexedDbStorage.js');await db.setJson('auth/persistentCredentials',JSON.stringify({userId:'00000000-0000-4000-8000-000000000011',apiToken:'synthetic-local-only'}));await db.setJson('preferences/colorSchemes',JSON.stringify({selectedSchemeId:scheme.id,customSchemes:[],schemaVersion:2,selectedAtUtc:new Date().toISOString()}));},scheme);
  await page.goto(origin+'/dashboard');await page.getByRole('button',{name:'Main navigation',exact:true}).waitFor();
  await page.evaluate(({scheme,text})=>{HabiticaColorScheme.applyColorScheme(scheme);document.documentElement.style.fontSize=text===200?'32px':'16px';},{scheme,text});
  await page.evaluate(()=>{window.__qualificationFocus=[];document.addEventListener('focusin',event=>{window.__qualificationFocus.push({time:performance.now(),tag:event.target.tagName,id:event.target.id,label:event.target.getAttribute('aria-label'),text:event.target.textContent?.trim().slice(0,80)});if(window.__qualificationFocus.length>20)window.__qualificationFocus.shift();});});
  const checks=['signed-out shell has no private navigation','signed-out skip reaches main'];
  async function waitDrawer(open,focus=false){
    try{await page.waitForFunction(({open,focus})=>{const drawer=document.querySelector('#primary-navigation'),menu=document.querySelector('[aria-label="Main navigation"]');return drawer&&menu&&menu.getAttribute('aria-expanded')===String(open)&&drawer.getAttribute('aria-hidden')===String(!open)&&drawer.hasAttribute('inert')===!open&&(!focus||document.activeElement===menu);},{open,focus});}
    catch(error){const state=await page.evaluate(()=>({menu:document.querySelector('[aria-label="Main navigation"]')?.getAttribute('aria-expanded'),hidden:document.querySelector('#primary-navigation')?.getAttribute('aria-hidden'),inert:document.querySelector('#primary-navigation')?.hasAttribute('inert'),focusHistory:window.__qualificationFocus}));throw new Error(error.message+' '+JSON.stringify({expectedOpen:open,expectedMenuFocus:focus,state}),{cause:error});}
  }

  for(const route of routes) {
    let menu=page.getByRole('button',{name:'Main navigation',exact:true});
    if(new URL(page.url()).pathname!==route) {
      if(await menu.getAttribute('aria-expanded')!=='true')await menu.click();
      const routeLink=page.locator(`#primary-navigation a[href="${route}"]:not(.nav-capture)`),group=routeLink.locator('xpath=ancestor::details[1]');if(await group.count()&&!await group.evaluate(el=>el.open))await group.locator(':scope > summary').click();await routeLink.click();
      await page.waitForURL(url=>url.pathname===route);
    }
    menu=page.getByRole('button',{name:'Main navigation',exact:true});
    const clearance=await page.evaluate(()=>{
      window.scrollTo(0,0);
      const rect=el=>{const r=el.getBoundingClientRect();return {left:r.left,right:r.right,top:r.top,bottom:r.bottom};};
      const main=document.querySelector('#main-content');
      const content=[...main.querySelectorAll('h1,button,a,input,p')].find(el=>{const r=el.getBoundingClientRect();return r.width>4&&r.height>4&&getComputedStyle(el).visibility!=='hidden';});
      return {header:rect(document.querySelector('header.topbar')),main:rect(main),content:content?[rect(content)]:[]};
    });
    assert.deepEqual(headerClearance(clearance),[],JSON.stringify({route,width,text,clearance}));
    assert.equal(await menu.getAttribute('aria-controls'),'primary-navigation');
    if(await menu.getAttribute('aria-expanded')==='true')await menu.click();
    await waitDrawer(false);
    await menu.focus();await menu.press('Enter');
    await waitDrawer(true);
    const drawer=page.getByRole('navigation',{name:'Primary navigation',exact:true});
    await drawer.getByRole('link').first().focus();await page.keyboard.press('Escape');
    await waitDrawer(false,true);
    await menu.press('Space');await waitDrawer(true);
    const overlay=page.locator('.mud-drawer-overlay');
    if(width<960) {
      await overlay.waitFor();
      const point=await page.evaluate(()=>{
        const backdrop=document.querySelector('.mud-drawer-overlay').getBoundingClientRect();
        const drawer=document.querySelector('#primary-navigation').getBoundingClientRect();
        const header=document.querySelector('.topbar').getBoundingClientRect();
        const x=(Math.max(backdrop.left,drawer.right)+Math.min(backdrop.right,document.documentElement.clientWidth))/2;
        const y=Math.min(backdrop.bottom-20,Math.max(backdrop.top,header.bottom)+32);
        return {x,y,hit:Boolean(document.elementFromPoint(x,y)?.closest('.mud-drawer-overlay'))};
      });
      assert.equal(point.hit,true,'The backdrop click must hit the scrim, outside drawer/header/scrollbars');
      await page.mouse.click(point.x,point.y);
      await waitDrawer(false,true);
    } else {
      assert.equal(await overlay.isVisible(),false);await menu.click();await waitDrawer(false,true);
    }
    const skip=page.getByRole('link',{name:'Skip to main content',exact:true});await skip.focus();
    const skipState=await skip.evaluate(el=>{const r=el.getBoundingClientRect(),s=getComputedStyle(el);return {focused:document.activeElement===el,top:r.top,bottom:r.bottom,height:innerHeight,outline:parseFloat(s.outlineWidth),focusHistory:window.__qualificationFocus};});
    assert.ok(skipState.focused&&skipState.top>=0&&skipState.bottom<=skipState.height&&skipState.outline>0,JSON.stringify({route,width,text,engine,skipState}));
    await skip.press('Enter');await page.waitForFunction(()=>document.activeElement?.id==='main-content');
    assert.equal(await page.locator('#blazor-error-ui').isVisible(),false);
    checks.push(route+': route, drawer name/state, Escape/focus, responsive backdrop, visible skip focus');
  }
  return {engine,width,text,theme:scheme.id,routes,checks,qualification:'Hydrated current Release with synthetic account only; all four shell routes and signed-out skip behavior.'};
}
