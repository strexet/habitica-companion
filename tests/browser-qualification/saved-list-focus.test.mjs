import {test} from 'node:test';
import assert from 'node:assert/strict';
import {revealFocusedElement} from '../../src/Habitica.WebApp/wwwroot/js/field-focus.js';

async function scenario(change) {
  const previous={document:globalThis.document,raf:globalThis.requestAnimationFrame},frames=[],calls=[];
  const field={isConnected:true,disabled:false,matches:selector=>{assert.equal(selector,':disabled');return field.disabled;},scrollIntoView:options=>calls.push(options)};
  globalThis.document={activeElement:field};globalThis.requestAnimationFrame=callback=>frames.push(callback);
  try {const pending=revealFocusedElement(field);assert.deepEqual(calls,[]);if(frames.length){frames.shift()();change?.(field);frames.shift()();}await pending;return {calls,field,active:document.activeElement};}
  finally {if(previous.document===undefined)delete globalThis.document;else globalThis.document=previous.document;if(previous.raf===undefined)delete globalThis.requestAnimationFrame;else globalThis.requestAnimationFrame=previous.raf;}
}

test('the focused connected field is revealed after validation layout settles',async()=>{
  const result=await scenario();assert.deepEqual(result.calls,[{block:'center',inline:'nearest',behavior:'instant'}]);assert.equal(result.active,result.field);
});
test('a user focus change prevents a late viewport jump',async()=>{
  const next={id:'other-control'},result=await scenario(()=>document.activeElement=next);assert.deepEqual(result.calls,[]);assert.equal(result.active,next);
});
test('a field removed during navigation is never scrolled',async()=>{
  const result=await scenario(field=>field.isConnected=false);assert.deepEqual(result.calls,[]);
});
test('an inherited disabled field is never scrolled after readiness changes',async()=>{
  const result=await scenario(field=>field.disabled=true);assert.deepEqual(result.calls,[]);
});
