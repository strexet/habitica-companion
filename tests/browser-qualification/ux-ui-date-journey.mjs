import assert from 'node:assert/strict';

export async function runJourney({page,origin,engine,scheme,width,text,qualifyNativeDate}) {
  await page.setViewportSize({width,height:900});await page.goto(origin+'/__qualification/seed');
  await page.evaluate(async schemeId=>{
    const db=await import('/js/storage/indexedDbStorage.js');
    await db.setJson('auth/persistentCredentials',JSON.stringify({userId:'00000000-0000-4000-8000-000000000011',apiToken:'synthetic-local-only'}));
    await db.setJson('preferences/colorSchemes',JSON.stringify({selectedSchemeId:schemeId,customSchemes:[],schemaVersion:2,selectedAtUtc:new Date().toISOString()}));
  },scheme.id);
  await page.goto(origin+'/todo/today');await page.locator('[data-readiness="ready"]').waitFor();
  await page.evaluate(({scheme,text})=>{HabiticaColorScheme.applyColorScheme(scheme);document.documentElement.style.fontSize=text===200?'32px':'16px';},{scheme,text});
  const menu=page.getByRole('button',{name:'Main navigation',exact:true});
  if(width<960&&await menu.getAttribute('aria-expanded')==='true')await menu.click();
  const day=page.getByTestId('today-date');assert.equal(await day.getAttribute('type'),'date');
  const limits={min:await day.getAttribute('min'),max:await day.getAttribute('max')};
  await day.fill('2031-12-31');await day.blur();assert.equal(await day.inputValue(),'2031-12-31');
  const nativeDate=await qualifyNativeDate(day);assert.equal(nativeDate.qualification,'pass','Every selected civil-date segment remains painted');
  const today=await page.evaluate(()=>{const d=new Date();return `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`;});
  const reset=page.getByRole('button',{name:"Use today's device date",exact:true});
  await day.focus();
  // Native date segments consume Tab; this WebKit host requires Option+Tab
  // to include buttons. The independent native-control trace records both paths.
  for(let step=0;step<8;step++){await page.keyboard.press(engine==='webkit'?'Alt+Tab':'Tab');if(await reset.evaluate(el=>document.activeElement===el))break;}
  assert.equal(await reset.evaluate(el=>document.activeElement===el&&parseFloat(getComputedStyle(el).outlineWidth)>0),true,'Today retains visible keyboard focus');
  await reset.press('Enter');await page.waitForFunction(expected=>document.querySelector('[data-testid="today-date"]')?.value===expected,today);
  assert.deepEqual({min:await day.getAttribute('min'),max:await day.getAttribute('max')},limits);
  const boxes=await page.locator('.todo-day-controls').evaluate(el=>{
    const parent=el.getBoundingClientRect(),input=el.querySelector('input').getBoundingClientRect(),button=el.querySelector('button').getBoundingClientRect();
    return {parent:{left:parent.left,right:parent.right},input:{left:input.left,right:input.right,top:input.top,bottom:input.bottom},button:{left:button.left,right:button.right,top:button.top,bottom:button.bottom,width:button.width,height:button.height}};
  });
  assert.ok(boxes.input.left>=boxes.parent.left-1&&boxes.input.right<=boxes.parent.right+1);
  assert.ok(boxes.button.width>=44&&boxes.button.height>=44);
  assert.ok(Math.min(boxes.input.right,boxes.button.right)-Math.max(boxes.input.left,boxes.button.left)<=1||Math.min(boxes.input.bottom,boxes.button.bottom)-Math.max(boxes.input.top,boxes.button.top)<=1,'Date and Today controls do not overlap');
  assert.equal(await page.locator('#blazor-error-ui').isVisible(),false);
  return {engine,theme:scheme.id,width,text,routes:['/todo/today'],checks:['native ISO civil-date edit','same-engine native selected-value ink','keyboard Today reset to device day','unchanged native type/min/max','bounded nonoverlapping primary controls'],nativeDate,boxes,qualification:'Current Release and real native date input under desktop Chromium/WebKit. English locale; physical calendar pickers, mobile keyboards and other locales remain unqualified.'};
}
