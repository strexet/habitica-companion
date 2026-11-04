// Closure-free DOM collector shared by current Razor captures and hydrated Notes.
export function collectNotesFixedWords(selector='[data-testid="notes-panel"]') {
  const rows=[];
  function visible(el){const rect=el.getBoundingClientRect();if(!rect.width||!rect.height)return false;for(let parent=el;parent;parent=parent.parentElement){const style=getComputedStyle(parent);if(style.display==='none'||style.visibility==='hidden'||style.contentVisibility==='hidden')return false;if(parent.tagName==='DETAILS'&&!parent.open&&!parent.querySelector(':scope > summary')?.contains(el))return false;}return true;}
  for(const root of document.querySelectorAll(selector))for(const button of root.querySelectorAll('button')) {
    // Note titles are user-authored content; fixed action labels have a separate contract.
    if(button.dataset.testid==='note-open'||!visible(button))continue;
    const walker=document.createTreeWalker(button,NodeFilter.SHOW_TEXT);let node;
    while((node=walker.nextNode()))for(const match of node.textContent.matchAll(/\S+/g)){
      const range=document.createRange();range.setStart(node,match.index);range.setEnd(node,match.index+match[0].length);
      const rects=[...range.getClientRects()].filter(rect=>rect.width>0&&rect.height>0);
      rows.push({id:button.dataset.testid||button.textContent.trim().slice(0,60),word:match[0],lines:[...new Set(rects.map(rect=>Math.round(rect.top*2)/2))]});
    }
  }
  return rows;
}
export function assessNotesFixedWords(rows) {
  return rows.filter(row=>!Array.isArray(row.lines)||row.lines.length!==1).map(row=>({kind:'notes-fixed-word-wrap',...row}));
}
