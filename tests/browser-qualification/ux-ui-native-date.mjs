import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import {assessNativeDate} from './qualification-core.mjs';
import {textInk} from './ux-ui-contract.mjs';

// A single empty owned reference per context avoids repeated WebKit page-load
// lifecycle churn during dense date matrices. Context cleanup owns the page.
const referencePages=new WeakMap();
async function ownedReference(context,origin) {
  let entry=referencePages.get(context);
  if(entry&&entry.origin!==origin)throw Error('Native date reference origin changed');
  if(!entry){
    const pending=(async()=>{const page=await context.newPage();await page.goto(origin+'/__qualification/reference',{waitUntil:'domcontentloaded'});if(await page.title()!=='Owned synthetic control')throw Error('Unexpected native date reference document');return page;})();
    entry={origin,pending};referencePages.set(context,entry);
  }
  const page=await entry.pending;if(page.isClosed())throw Error('Owned native date reference closed unexpectedly');return page;
}

export async function nativeDateEvidence(input, {context,origin,out,id,PNG}) {
  // Native screenshots must measure the field, not a fixed app bar covering a
  // partially visible field after a review inserts content above the form.
  await input.evaluate(el=>el.scrollIntoView({block:'center',inline:'nearest',behavior:'instant'}));
  await input.evaluate(()=>new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve))));
  const original=await input.evaluate(el=>{
    const style=getComputedStyle(el),properties={};
    for(const key of style)properties[key]=style.getPropertyValue(key);
    let opacity=1;for(let parent=el;parent;parent=parent.parentElement)opacity*=Number(getComputedStyle(parent).opacity);
    const rect=el.getBoundingClientRect();
    return {value:el.value,disabled:el.matches(':disabled'),min:el.min,max:el.max,properties,opacity,font:parseFloat(style.fontSize),height:rect.height,left:rect.left,top:rect.top};
  });
  if(!original.value)return {qualification:'unavailable',kind:'native-date-evidence',reason:'An explicit selected date is required'};
  let reference;
  const paths={};
  async function paintPair(locator) {
    const options={animations:'disabled',caret:'hide',timeout:10000};
    const normal=await locator.screenshot(options);
    const property=await locator.evaluate(el=>({value:el.style.getPropertyValue('-webkit-text-fill-color'),priority:el.style.getPropertyPriority('-webkit-text-fill-color')}));
    try {
      // A stylesheet cannot override the reference's copied inline !important.
      await locator.evaluate(el=>el.style.setProperty('-webkit-text-fill-color','transparent','important'));
      return [normal,await locator.screenshot(options)];
    } finally {
      await locator.evaluate((el,p)=>p.value?el.style.setProperty('-webkit-text-fill-color',p.value,p.priority):el.style.removeProperty('-webkit-text-fill-color'),property);
    }
  }
  try {
    await input.evaluate(el=>el.setAttribute('data-native-probe','active'));
    const [normal,hidden]=await paintPair(input);
    // Preserve the actual backdrop beneath translucent input surfaces. A white
    // reference page changes ink contrast even when every glyph is present.
    const visibility=await input.evaluate(el=>({value:el.style.getPropertyValue('visibility'),priority:el.style.getPropertyPriority('visibility')}));
    let backdrop;
    try {
      const bounds=await input.boundingBox();
      await input.evaluate(el=>el.style.setProperty('visibility','hidden','important'));
      backdrop=await input.page().screenshot({clip:bounds,animations:'disabled',caret:'hide',timeout:10000});
    } finally {
      await input.evaluate((el,p)=>p.value?el.style.setProperty('visibility',p.value,p.priority):el.style.removeProperty('visibility'),visibility);
    }
    reference=await ownedReference(context,origin);
    await reference.setViewportSize({width:1000,height:400});
    await reference.evaluate(({properties,value,disabled,min,max,opacity,font,height,left,top,backdrop})=>{
      const el=document.createElement('input');el.type='date';el.value=value;el.disabled=disabled;el.min=min;el.max=max;
      for(const [key,v] of Object.entries(properties))el.style.setProperty(key,v,'important');
      for(const [key,v] of Object.entries({position:'absolute',left:(16+left-Math.floor(left))+'px',top:(16+top-Math.floor(top))+'px',transform:'none',margin:'0',width:'700px','min-width':'700px','max-width':'700px',height:Math.max(height,font*2)+'px','min-height':Math.max(height,font*2)+'px','max-height':'none',opacity:String(opacity)}))el.style.setProperty(key,v,'important');
      const x=16+left-Math.floor(left),y=16+top-Math.floor(top);
      Object.assign(document.documentElement.style,{background:'white'});
      Object.assign(document.body.style,{margin:'0',minHeight:'400px',backgroundImage:`url(${backdrop})`,backgroundRepeat:'repeat-x',backgroundPosition:`${x}px ${y}px`});
      el.setAttribute('data-native-probe','active');document.body.replaceChildren(el);
    },{...original,backdrop:'data:image/png;base64,'+backdrop.toString('base64')});
    const ref=reference.locator('input');
    if(await ref.evaluate(el=>el.matches(':disabled'))!==original.disabled)throw Error('Native date reference disabled state differs from the measured control');
    const [full,fullHidden]=await paintPair(ref);
    const buffers={actual:normal,'actual-no-text':hidden,'actual-backdrop':backdrop,reference:full,'reference-no-text':fullHidden};
    for(const [name,buffer] of Object.entries(buffers)) {
      const path=join(out,`${id}-${name}.png`);await writeFile(path,buffer);paths[name]=path;
    }
    const actualInk=textInk(PNG.sync.read(normal),PNG.sync.read(hidden));
    const referenceInk=textInk(PNG.sync.read(full),PNG.sync.read(fullHidden));
    return {...assessNativeDate({actualInk,referenceInk,evidenceAvailable:true}),actualInk,referenceInk,value:original.value,disabled:original.disabled,rasterOrigin:{left:original.left,top:original.top},evidence:paths,
      fontFamily:original.properties['font-family'],fontSize:original.font,fontWeight:original.properties['font-weight'],effectiveOpacity:original.opacity,
      method:'Same-engine selected-value ink versus full-width reference; hidden-text pairs isolate native value paint.97-percent retention is required. Not an OCR or assistive-technology result.'};
  } finally {
    await input.evaluate(el=>el.removeAttribute('data-native-probe')).catch(()=>{});
    // The owned context closes the shared reference during verified session cleanup.
  }
}
