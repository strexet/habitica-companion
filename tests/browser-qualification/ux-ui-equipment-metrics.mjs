// DOM collection is shared by real Razor captures and hydrated native journeys.
// The function is closure-free so Playwright evaluates the same collector.
export function collectEquipmentReadability() {
  const canvas=document.createElement('canvas'),ctx=canvas.getContext('2d');
  const visible=el=>el.getClientRects().length&&getComputedStyle(el).visibility!=='hidden'&&!el.closest('[hidden]');
  function ink(el,text){const s=getComputedStyle(el);if(s.textTransform==='uppercase')text=text.toUpperCase();else if(s.textTransform==='lowercase')text=text.toLowerCase();ctx.font=`${s.fontStyle} ${s.fontWeight} ${s.fontSize} ${s.fontFamily}`;return ctx.measureText(text).width+Math.max(0,text.length-1)*(parseFloat(s.letterSpacing)||0);}
  const identities=[...document.querySelectorAll('.inventory-shell .habitica-item-identity strong')].filter(visible).map(el=>{
    const parent=el.closest('.habitica-item-identity'),r=el.getBoundingClientRect();
    return {text:el.textContent.trim(),width:r.width,availableWidth:parent.clientWidth,wordWidth:Math.max(...el.textContent.trim().split(/\s+/).map(word=>ink(el,word)))};
  });
  for(const label of [...document.querySelectorAll('.inventory-shell .mud-button-label')].filter(visible)){
    const button=label.closest('button,a');if(!button||!label.textContent.trim())continue;
    const owner=button.closest('.action-row,.preset-save-row,.preset-mini-item')??button.parentElement,s=getComputedStyle(button);
    const wordLineCounts=[],walker=document.createTreeWalker(label,NodeFilter.SHOW_TEXT);
    for(let node;node=walker.nextNode();)for(const match of node.textContent.matchAll(/\S+/g)){const range=document.createRange();range.setStart(node,match.index);range.setEnd(node,match.index+match[0].length);wordLineCounts.push(new Set([...range.getClientRects()].filter(r=>r.width&&r.height).map(r=>Math.round(r.top))).size);}
    identities.push({kind:'action-label',wordLineCounts,text:label.textContent.trim(),width:label.getBoundingClientRect().width,availableWidth:owner.clientWidth-parseFloat(s.paddingLeft)-parseFloat(s.paddingRight)-parseFloat(s.borderLeftWidth)-parseFloat(s.borderRightWidth),wordWidth:Math.max(...label.textContent.trim().split(/\s+/).map(word=>ink(label,word)))});
  }
  for(const el of [...document.querySelectorAll('.inventory-shell .habitica-icon-count-chip > span:last-child')].filter(visible)){
    const chip=el.parentElement,s=getComputedStyle(chip),r=el.getBoundingClientRect(),wordLineCounts=[];
    const walker=document.createTreeWalker(el,NodeFilter.SHOW_TEXT);for(let node;node=walker.nextNode();)for(const m of node.textContent.matchAll(/\S+/g)){const range=document.createRange();range.setStart(node,m.index);range.setEnd(node,m.index+m[0].length);wordLineCounts.push(new Set([...range.getClientRects()].filter(r=>r.width&&r.height).map(r=>Math.round(r.top))).size);}
    identities.push({kind:'count-label',text:el.textContent.trim(),width:r.width,availableWidth:chip.clientWidth-parseFloat(s.paddingLeft)-parseFloat(s.paddingRight),wordWidth:Math.max(...el.textContent.trim().split(/\s+/).map(word=>ink(el,word))),wordLineCounts});
  }
  const stats=[...document.querySelectorAll('.inventory-shell .inventory-stat-value,.inventory-shell .habitica-icon-count-chip > strong')].filter(visible).map(el=>{
    const r=el.getBoundingClientRect(),s=getComputedStyle(el),range=document.createRange();range.selectNodeContents(el);
    const rects=[...range.getClientRects()].filter(r=>r.width&&r.height),text=el.textContent.trim();
    return {text,width:r.width-parseFloat(s.paddingLeft)-parseFloat(s.paddingRight),inkWidth:ink(el,text),lineCount:new Set(rects.map(r=>Math.round(r.top))).size};
  });
  return {identities,stats};
}
