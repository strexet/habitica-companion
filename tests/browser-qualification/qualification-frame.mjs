import {collectNotesFixedWords,assessNotesFixedWords} from './ux-ui-notes-metrics.mjs';
import {collectEquipmentReadability} from './ux-ui-equipment-metrics.mjs';
import {finishSnapshotTransitions} from './qualification-session.mjs';
import {composite, sourceOver, contrast, threshold, geometry, assessDisabledText, headerClearance, assessEquipmentReadability} from './qualification-core.mjs';
const query = new URLSearchParams(location.search);
const colors = new Map(), rasters = new Map();
// Inspection runs after snapshot settlement. Native value and text-range samples
// share immutable ancestor geometry/style handles for this document only.
const styleCache=new WeakMap(),boxCache=new WeakMap();
function styleFor(element){if(!styleCache.has(element))styleCache.set(element,getComputedStyle(element));return styleCache.get(element);}
function boxFor(element){if(!boxCache.has(element))boxCache.set(element,element.getBoundingClientRect());return boxCache.get(element);}

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
async function ownBackground(el,x,y) {
    const style=styleFor(el),r=boxFor(el);
    let result=color(style.backgroundColor);
    const image=style.backgroundImage;
    if(image==='none')return result;
    const u=Math.max(0,Math.min(1,(x-r.left)/Math.max(1,r.width))),v=Math.max(0,Math.min(1,(y-r.top)/Math.max(1,r.height)));
    const match=image.match(/^url\("?(data:image\/png;[^"\)]+)"?\)(?:,\s*none)*$/);
    if(match){const context=await raster(match[1]);const p=[...context.getImageData(Math.min(255,Math.floor(u*256)),Math.min(255,Math.floor(v*256)),1,1).data];p[3]/=255;result=sourceOver(p,result);}
    else if(image.startsWith('linear-gradient(rgb')||image.startsWith('linear-gradient(180deg,')){
      const stops=image.match(/rgba?\([^)]+\)|color\([^)]+\)|#[\da-f]+/gi);
      if(stops?.length!==2)throw Error('Unqualified gradient stops');
      const a=color(stops[0]),b=color(stops[1]),alpha=a[3]*(1-v)+b[3]*v;
      const p=a.slice(0,3).map((n,i)=>alpha?(n*a[3]*(1-v)+b[i]*b[3]*v)/alpha:0).concat(alpha);
      result=sourceOver(p,result);
    } else throw Error('Unqualified background syntax');
  return result;
}
async function background(element,x,y) {
  const chain=[];for(let el=element;el;el=el.parentElement)chain.unshift(el);
  let result=[255,255,255,1];
  for(const el of chain)result=composite(await ownBackground(el,x,y),result);
  return result;
}
async function disabledPaint(element,x,y,minimum) {
  const chain=[];for(let el=element;el;el=el.parentElement)chain.unshift(el);
  const layers=[];
  for(const el of chain)layers.push({background:await ownBackground(el,x,y),opacity:Number(styleFor(el).opacity)});
  const style=styleFor(element);
  if(style.filter!=='none'||style.mixBlendMode!=='normal'||chain.some(el=>{const s=styleFor(el);return s.filter!=='none'||s.mixBlendMode!=='normal';}))throw Error('Unqualified disabled filter or blend');
  return assessDisabledText(color(style.webkitTextFillColor||style.color),layers,minimum);
}
const visible=el=>{
  const r=boxFor(el);if(!r.width||!r.height)return false;
  for(let ancestor=el;ancestor;ancestor=ancestor.parentElement){
    const s=styleFor(ancestor),clip=boxFor(ancestor);
    if(ancestor.tagName==='DETAILS'&&!ancestor.open&&!ancestor.querySelector(':scope > summary')?.contains(el))return false;
    if(s.contentVisibility==='hidden')return false;
    if(s.visibility==='hidden'||s.display==='none'||Number(s.opacity)===0)return false;
    if(ancestor===el)continue;
    const clipsX=['hidden','clip','auto','scroll'].includes(s.overflowX),clipsY=['hidden','clip','auto','scroll'].includes(s.overflowY);
    if(clipsX&&(!clip.width||r.right<=clip.left||r.left>=clip.right)||clipsY&&(!clip.height||r.bottom<=clip.top||r.top>=clip.bottom))return false;
  }
  return true;
};
const identity=()=>({caseId:query.get('case'),fixture:location.pathname,theme:query.get('theme')||'gryphy-light',textPercent:Number(query.get('text')||100),seed:query.get('seed'),width:innerWidth});
const inspectionStarted=performance.now();
const phase=(name,status,details)=>parent.postMessage({type:'qualification-phase',phase:{...identity(),phase:name,status,elapsedMs:performance.now()-inspectionStarted,...(details?{details}:{})}},location.origin);
function publishResult(result){
  const output=document.createElement('pre');output.id='qualification-result';output.textContent=JSON.stringify(result);output.hidden=true;document.body.append(output);
  parent.postMessage({type:'qualification-result',result},location.origin);
}
async function inspect(){
  phase('theme','waiting');
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
  phase('theme','complete');phase('fonts','waiting');
  await document.fonts.ready;phase('fonts','complete');phase('frame','waiting');
  await new Promise(requestAnimationFrame);phase('frame','complete');
  const finiteAnimations=document.getAnimations().filter(a=>Number.isFinite(a.effect?.getComputedTiming().endTime));
  const settledTransitions=finishSnapshotTransitions(finiteAnimations,globalThis.CSSTransition);
  phase('animations','waiting',finiteAnimations.slice(0,12).map(a=>({name:String(a.animationName??a.transitionProperty??a.id??'').slice(0,80),state:a.playState,pending:a.pending,rate:a.playbackRate,time:a.currentTime,endTime:a.effect?.getComputedTiming().endTime,target:a.effect?.target?.tagName??'',classes:String(a.effect?.target?.className??'').slice(0,160),settlement:settledTransitions.includes(a)?'css-transition-final-state':'natural-finish-wait'})));
  await Promise.allSettled(finiteAnimations.map(a=>a.finished));
  phase('animations','complete');phase('measurement','waiting');
  const elements=[...document.querySelectorAll('button,a,input,select,textarea,summary,[role="button"]')].filter(el=>visible(el)&&!el.closest('[inert]'));
  const controls=elements.map((el,i)=>{const r=boxFor(el);return {id:el.dataset.testid||el.id||el.tagName+'-'+i,element:el,left:r.left,right:r.right,top:r.top,bottom:r.bottom,width:r.width,height:r.height,primary:el.matches('[data-layout-primary],.mud-button-filled:not(.mud-button-filled-size-small),.mud-nav-link')};});
  for(const a of controls)a.contains=controls.filter(b=>a!==b&&a.element.contains(b.element)).map(b=>b.id);
  for(const a of controls){
    for(let ancestor=a.element.parentElement;ancestor&&ancestor!==document.body;ancestor=ancestor.parentElement){
      const style=styleFor(ancestor),r=boxFor(ancestor);
      if(['auto','scroll'].includes(style.overflowX)&&ancestor.scrollWidth>ancestor.clientWidth&&r.left>=0&&r.right<=document.documentElement.clientWidth+1){a.scrollContainer=true;break;}
    }
  }
  const errors=geometry({width:document.documentElement.clientWidth,scrollWidth:document.documentElement.scrollWidth,controls});
  const header=document.querySelector('header.topbar'),main=document.querySelector('#main-content');
  if(header&&main&&scrollY<1){
    const rect=el=>{const r=boxFor(el);return {left:r.left,right:r.right,top:r.top,bottom:r.bottom};};
    const marked=[...main.querySelectorAll('[data-layout-content]')].filter(visible);
    errors.push(...headerClearance({header:rect(header),main:rect(main),content:marked.map(rect)}));
  }
  const unknown=[],ratios=[],disabledSamples=[];let disabledSkipped=0;
  const disabledMinimum=query.has('disabledMinimum')?Number(query.get('disabledMinimum')):null;
  if(disabledMinimum!==null&&(!Number.isFinite(disabledMinimum)||disabledMinimum<3||disabledMinimum>21))throw Error('Invalid disabled text floor');
  async function sampleDisabled(el,text,rects,source){
    let ratio=Infinity;
    try{
      for(const r of rects)for(const [x,y] of [[r.left+r.width/2,r.top+r.height/2],[r.left+1,r.top+1],[r.right-1,r.bottom-1]])
        ratio=Math.min(ratio,(await disabledPaint(el,x,y,disabledMinimum)).ratio);
      if(!Number.isFinite(ratio))throw Error('No disabled text sample rectangle');
      const sample={id:el.dataset.testid||el.id||el.tagName,text:text.slice(0,70),ratio,minimum:disabledMinimum,source};
      disabledSamples.push(sample);
      if(ratio<disabledMinimum)errors.push({kind:'disabled-text-contrast',...sample});
    }catch(error){unknown.push({element:el.tagName,kind:'disabled-text',reason:error.message});}
  }
  const walker=document.createTreeWalker(document.body,NodeFilter.SHOW_TEXT);
  for(let node;node=walker.nextNode();){
    const el=node.parentElement;
    // Native control values are painted by the browser, not DOM text ranges.
    // Sample them once through their control box below, including textareas.
    if(!node.textContent.trim()||!el||el.closest('script,style,svg,input,textarea,select,[aria-hidden="true"]')||!visible(el))continue;
    const style=styleFor(el),range=document.createRange();range.selectNodeContents(node);
    if(el.closest(':disabled,[aria-disabled="true"]')){
      if(disabledMinimum===null)disabledSkipped++;
      else await sampleDisabled(el,node.textContent.trim(),[...range.getClientRects()].filter(r=>r.width&&r.height),'dom-text');
      continue;
    }
    let ratio=Infinity;
    try{
      for(const r of [...range.getClientRects()].filter(r=>r.width&&r.height)){
        for(const [x,y] of [[r.left+r.width/2,r.top+r.height/2],[r.left+1,r.top+1],[r.right-1,r.bottom-1]]){
          const bg=await background(el,x,y);const fg=color(style.color).slice();
          let opacity=1;for(let a=el;a;a=a.parentElement)opacity*=Number(styleFor(a).opacity);
          fg[3]*=opacity;ratio=Math.min(ratio,contrast(composite(fg,bg),bg));
        }
      }
      const minimum=threshold(parseFloat(style.fontSize),parseInt(style.fontWeight)||400);
      if(Number.isFinite(ratio)){ratios.push(ratio);if(ratio<minimum)errors.push({kind:'contrast',id:el.dataset.testid||el.id||el.tagName,text:node.textContent.trim().slice(0,70),ratio,minimum});}
    }catch(e){unknown.push({element:el.tagName,reason:e.message});}
  }
  for(const el of elements.filter(el=>el.matches('input,select,textarea'))){
    if(el.matches('input[type="checkbox"],input[type="radio"],input[type="range"],input[type="color"],input[type="file"],input[type="hidden"]'))continue;
    const text=el.tagName==='SELECT'?el.selectedOptions[0]?.textContent:el.value;
    if(!text?.trim())continue;
    const rect=boxFor(el);
    if(el.matches('.linked-daily-zone select')) {
      const style=styleFor(el),canvas=document.createElement('canvas'),context=canvas.getContext('2d');context.font=style.font;
      const ink=context.measureText(text.trim()).width,available=el.clientWidth-parseFloat(style.paddingLeft)-parseFloat(style.paddingRight)-24;
      if(ink>available)errors.push({kind:'linked-zone-caption-clipped',id:el.dataset.testid,text:text.trim(),ink,available});
    }
    if(el.closest(':disabled,[aria-disabled="true"]')){
      if(disabledMinimum!==null)await sampleDisabled(el,text.trim(),[rect],'control-value');
      else disabledSkipped++;
    }else{
      const style=styleFor(el),minimum=threshold(parseFloat(style.fontSize),parseInt(style.fontWeight)||400);
      try{
        let ratio=Infinity;
        for(const [x,y] of [[rect.left+rect.width/2,rect.top+rect.height/2],[rect.left+1,rect.top+1],[rect.right-1,rect.bottom-1]])
          ratio=Math.min(ratio,(await disabledPaint(el,x,y,minimum)).ratio);
        ratios.push(ratio);if(ratio<minimum)errors.push({kind:'contrast',id:el.dataset.testid||el.id||el.tagName,text:text.trim().slice(0,70),ratio,minimum,source:'control-value'});
      }catch(error){unknown.push({element:el.tagName,kind:'control-value',reason:error.message});}
    }
  }
  const recurringTextElements=[...elements.filter(el=>el.matches('.todo-recurring-editor button')),...document.querySelectorAll('.todo-recurring-editor h2')].filter(el=>visible(el)&&!el.closest('[inert]'));
  for(const el of recurringTextElements){
    const walker=document.createTreeWalker(el,NodeFilter.SHOW_TEXT);let node;
    while((node=walker.nextNode()))for(const match of node.textContent.matchAll(/\S+/g)){
      if(el.tagName==='H2'&&!/^(Daily|schedule:?|and|occurrences:)$/.test(match[0]))continue;
      const range=document.createRange();range.setStart(node,match.index);range.setEnd(node,match.index+match[0].length);
      const lines=new Set([...range.getClientRects()].filter(rect=>rect.width>0&&rect.height>0).map(rect=>Math.round(rect.top*2)/2));
      if(lines.size!==1)errors.push({kind:'recurring-fixed-word-wrap',id:el.dataset.testid||el.tagName,word:match[0],lines:[...lines]});
    }
  }
  const notesFixedWords=collectNotesFixedWords();errors.push(...assessNotesFixedWords(notesFixedWords));
  const nativeDateInputs=query.get('extended')==='1'?elements.filter(el=>el.matches('input[type="date"]')).map(el=>({id:el.dataset.testid||el.id||null,value:el.value,disabled:el.disabled,qualification:'requires-pixel-evidence'})):[];
  const equipmentReadability=collectEquipmentReadability();errors.push(...assessEquipmentReadability(equipmentReadability));
  phase('measurement','complete');
  const result={caseId:query.get('case'),fixture:location.pathname,theme:selected,textPercent:Number(query.get('text')||100),seed:query.get('seed'),width:innerWidth,clientWidth:document.documentElement.clientWidth,scrollWidth:document.documentElement.scrollWidth,controls:controls.length,scrollableControls:controls.filter(c=>c.scrollContainer).map(c=>c.id),primaryControls:controls.filter(c=>c.primary).length,textSamples:ratios.length,minimumMeasuredContrast:Math.min(...ratios),disabledSkipped,disabledSamples,nativeDateInputs,equipmentReadability,notesFixedWords,errors,unqualifiedSurfaces:unknown};
  if(query.get('debug')) result.debugControls=controls.map(control=>({id:control.id,left:control.left,right:control.right,width:control.width,parents:(()=>{const chain=[];for(let el=control.element;el&&chain.length<6;el=el.parentElement){const s=styleFor(el),r=boxFor(el);chain.push({tag:el.tagName,classes:el.className,left:r.left,right:r.right,width:r.width,minWidth:s.minWidth,columns:s.gridTemplateColumns,flex:s.flex});}return chain;})()}));
  if(query.get('debug')) result.debugOverflow=[...document.body.querySelectorAll('*')].filter(visible).map(el=>{const r=boxFor(el),s=styleFor(el);return {tag:el.tagName,classes:el.className,left:r.left,right:r.right,width:r.width,minWidth:s.minWidth,columns:s.gridTemplateColumns,overflow:s.overflowX,text:el.textContent.trim().slice(0,70)};}).filter(row=>row.right>document.documentElement.clientWidth+1||row.left<0).slice(0,80);
  publishResult({...result,inspectionElapsedMs:performance.now()-inspectionStarted});
}
inspect().catch(error=>publishResult({...identity(),harnessError:error.message,inspectionElapsedMs:performance.now()-inspectionStarted}));
