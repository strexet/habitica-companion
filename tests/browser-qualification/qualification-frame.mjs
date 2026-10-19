import {composite, contrast, threshold, geometry} from './qualification-core.mjs';
const query = new URLSearchParams(location.search);
const colors = new Map(), rasters = new Map();
const pixel = document.createElement('canvas'); pixel.width = pixel.height = 1;
const pixelContext = pixel.getContext('2d', {willReadFrequently:true});
function color(value) {
  if (!colors.has(value)) {
    if (!CSS.supports('color',value)) throw Error('Unsupported color '+value);
    pixelContext.clearRect(0,0,1,1); pixelContext.fillStyle=value; pixelContext.fillRect(0,0,1,1);
    const v=[...pixelContext.getImageData(0,0,1,1).data];v[3]/=255;colors.set(value,v);
  }
  return colors.get(value);
}
async function raster(url) {
  if (!rasters.has(url)) {
    if (!url.startsWith('data:image/png;')) throw Error('Unqualified background image');
    const image = new Image();image.src=url;await image.decode();
    const canvas=document.createElement('canvas');canvas.width=canvas.height=256;
    const context=canvas.getContext('2d',{willReadFrequently:true});context.drawImage(image,0,0,256,256);
    rasters.set(url,context);
  }
  return rasters.get(url);
}
async function background(element,x,y) {
  const chain=[];for(let el=element;el;el=el.parentElement)chain.unshift(el);
  let result=[255,255,255,1];
  for(const el of chain){
    const style=getComputedStyle(el),r=el.getBoundingClientRect();
    result=composite(color(style.backgroundColor),result);
    const image=style.backgroundImage;
    if(image==='none')continue;
    const u=Math.max(0,Math.min(1,(x-r.left)/Math.max(1,r.width))),v=Math.max(0,Math.min(1,(y-r.top)/Math.max(1,r.height)));
    const match=image.match(/^url\("?(data:image\/png;[^"\)]+)"?\)(?:,\s*none)*$/);
    if(match){const context=await raster(match[1]);const p=[...context.getImageData(Math.min(255,Math.floor(u*256)),Math.min(255,Math.floor(v*256)),1,1).data];p[3]/=255;result=composite(p,result);}
    else if(image.startsWith('linear-gradient(rgb')||image.startsWith('linear-gradient(180deg,')){
      const stops=image.match(/rgba?\([^)]+\)|color\([^)]+\)|#[\da-f]+/gi);
      if(stops?.length!==2)throw Error('Unqualified gradient stops');
      const a=color(stops[0]),b=color(stops[1]),alpha=a[3]*(1-v)+b[3]*v;
      const p=a.slice(0,3).map((n,i)=>alpha?(n*a[3]*(1-v)+b[i]*b[3]*v)/alpha:0).concat(alpha);
      result=composite(p,result);
    } else throw Error('Unqualified background syntax');
  }
  return result;
}
const visible=el=>{
  const r=el.getBoundingClientRect();if(!r.width||!r.height)return false;
  for(let ancestor=el;ancestor;ancestor=ancestor.parentElement){
    const s=getComputedStyle(ancestor),clip=ancestor.getBoundingClientRect();
    if(ancestor.tagName==='DETAILS'&&!ancestor.open&&!ancestor.querySelector(':scope > summary')?.contains(el))return false;
    if(s.contentVisibility==='hidden')return false;
    if(s.visibility==='hidden'||s.display==='none'||Number(s.opacity)===0)return false;
    if(ancestor===el)continue;
    const clipsX=['hidden','clip','auto','scroll'].includes(s.overflowX),clipsY=['hidden','clip','auto','scroll'].includes(s.overflowY);
    if(clipsX&&(!clip.width||r.right<=clip.left||r.left>=clip.right)||clipsY&&(!clip.height||r.bottom<=clip.top||r.top>=clip.bottom))return false;
  }
  return true;
};
async function inspect(){
  const selected=query.get('theme')||'gryphy-light';
  const schemes=await(await fetch('/schemes.json')).json();
  if(!schemes[selected])throw Error('Unknown qualification theme');
  window.HabiticaColorScheme.applyColorScheme(schemes[selected]);
  if(query.get('text')==='200')document.documentElement.style.fontSize='32px';
  if(query.get('seed')==='geometry'){
    const box=document.createElement('div');box.innerHTML='<button style="position:absolute;left:0;top:0;width:90px;height:44px">A</button><button style="position:absolute;left:30px;top:0;width:90px;height:44px">B</button>';
    box.style.width='2000px';box.style.height='50px';document.body.append(box);
  }
  if(query.get('seed')==='contrast'){
    const text=document.createElement('p');text.id='contrast-canary';text.style.cssText='color:rgb(125,125,125);background:rgb(125,125,125)';text.textContent='Seeded unreadable contrast canary';document.body.append(text);
  }
  await document.fonts.ready;await new Promise(requestAnimationFrame);
  await Promise.allSettled(document.getAnimations().filter(a=>Number.isFinite(a.effect?.getComputedTiming().endTime)).map(a=>a.finished));
  const elements=[...document.querySelectorAll('button,a,input,select,textarea,summary,[role="button"]')].filter(visible);
  const controls=elements.map((el,i)=>{const r=el.getBoundingClientRect();return {id:el.dataset.testid||el.id||el.tagName+'-'+i,element:el,left:r.left,right:r.right,top:r.top,bottom:r.bottom,width:r.width,height:r.height,primary:el.matches('[data-layout-primary],.mud-button-filled:not(.mud-button-filled-size-small),.mud-nav-link')};});
  for(const a of controls)a.contains=controls.filter(b=>a!==b&&a.element.contains(b.element)).map(b=>b.id);
  for(const a of controls){
    for(let ancestor=a.element.parentElement;ancestor&&ancestor!==document.body;ancestor=ancestor.parentElement){
      const style=getComputedStyle(ancestor),r=ancestor.getBoundingClientRect();
      if(['auto','scroll'].includes(style.overflowX)&&ancestor.scrollWidth>ancestor.clientWidth&&r.left>=0&&r.right<=document.documentElement.clientWidth+1){a.scrollContainer=true;break;}
    }
  }
  const errors=geometry({width:document.documentElement.clientWidth,scrollWidth:document.documentElement.scrollWidth,controls});
  const unknown=[],ratios=[];let disabledSkipped=0;
  const walker=document.createTreeWalker(document.body,NodeFilter.SHOW_TEXT);
  for(let node;node=walker.nextNode();){
    const el=node.parentElement;
    if(!node.textContent.trim()||!el||el.closest('script,style,svg,[aria-hidden="true"]')||!visible(el))continue;
    if(el.closest(':disabled,[aria-disabled="true"]')){disabledSkipped++;continue;}
    const style=getComputedStyle(el),range=document.createRange();range.selectNodeContents(node);
    let ratio=Infinity;
    try{
      for(const r of [...range.getClientRects()].filter(r=>r.width&&r.height)){
        for(const [x,y] of [[r.left+r.width/2,r.top+r.height/2],[r.left+1,r.top+1],[r.right-1,r.bottom-1]]){
          const bg=await background(el,x,y);const fg=color(style.color).slice();
          let opacity=1;for(let a=el;a;a=a.parentElement)opacity*=Number(getComputedStyle(a).opacity);
          fg[3]*=opacity;ratio=Math.min(ratio,contrast(composite(fg,bg),bg));
        }
      }
      const minimum=threshold(parseFloat(style.fontSize),parseInt(style.fontWeight)||400);
      if(Number.isFinite(ratio)){ratios.push(ratio);if(ratio<minimum)errors.push({kind:'contrast',id:el.dataset.testid||el.id||el.tagName,text:node.textContent.trim().slice(0,70),ratio,minimum});}
    }catch(e){unknown.push({element:el.tagName,reason:e.message});}
  }
  const result={caseId:query.get('case'),fixture:location.pathname,theme:selected,textPercent:Number(query.get('text')||100),seed:query.get('seed'),width:innerWidth,clientWidth:document.documentElement.clientWidth,scrollWidth:document.documentElement.scrollWidth,controls:controls.length,scrollableControls:controls.filter(c=>c.scrollContainer).map(c=>c.id),primaryControls:controls.filter(c=>c.primary).length,textSamples:ratios.length,minimumMeasuredContrast:Math.min(...ratios),disabledSkipped,errors,unqualifiedSurfaces:unknown};
  if(query.get('debug')) result.debugControls=controls.map(control=>({id:control.id,left:control.left,right:control.right,width:control.width,parents:(()=>{const chain=[];for(let el=control.element;el&&chain.length<6;el=el.parentElement){const s=getComputedStyle(el),r=el.getBoundingClientRect();chain.push({tag:el.tagName,classes:el.className,left:r.left,right:r.right,width:r.width,minWidth:s.minWidth,columns:s.gridTemplateColumns,flex:s.flex});}return chain;})()}));
  if(query.get('debug')) result.debugOverflow=[...document.body.querySelectorAll('*')].filter(visible).map(el=>{const r=el.getBoundingClientRect(),s=getComputedStyle(el);return {tag:el.tagName,classes:el.className,left:r.left,right:r.right,width:r.width,minWidth:s.minWidth,columns:s.gridTemplateColumns,overflow:s.overflowX,text:el.textContent.trim().slice(0,70)};}).filter(row=>row.right>document.documentElement.clientWidth+1||row.left<0).slice(0,80);
  const output=document.createElement('pre');output.id='qualification-result';output.textContent=JSON.stringify(result);output.hidden=true;document.body.append(output);
  parent.postMessage({type:'qualification-result',result},location.origin);
}
inspect().catch(error=>parent.postMessage({type:'qualification-result',result:{caseId:query.get('case'),fixture:location.pathname,harnessError:error.message}},location.origin));
