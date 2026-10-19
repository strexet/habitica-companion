import {test} from 'node:test';
import assert from 'node:assert/strict';
import {contrast, composite, geometry, threshold} from '../browser-qualification/qualification-core.mjs';

test('contrast keeps exact thresholds and composites translucent foregrounds',()=>{
  assert.equal(contrast([0,0,0],[255,255,255]),21);
  assert.equal(contrast([255,255,255],[255,255,255]),1);
  assert.deepEqual(composite([0,0,0,.5],[255,255,255,1]),[127.5,127.5,127.5,1]);
  assert.equal(threshold(16,400),4.5);assert.equal(threshold(24,400),3);
  assert.equal(threshold(18,700),4.5);assert.equal(threshold(19,700),3);
});
test('seeded geometry catches overflow, overlap and undersized primary controls',()=>{
  const a={id:'a',left:0,right:50,top:0,bottom:40,width:50,height:40,primary:true};
  const b={...a,id:'b',left:30,right:80,primary:false};
  assert.deepEqual(geometry({width:70,scrollWidth:80,controls:[a,b]}).map(e=>e.kind),['overflow','primary-target','overlap','control-bounds']);
  assert.deepEqual(geometry({width:100,scrollWidth:100,controls:[{...a,height:44,bottom:44},{...b,top:45,bottom:89}]}),[]);
});

test('an intentional contained horizontal table scroller remains distinct from page overflow',()=>{
  assert.deepEqual(geometry({width:100,scrollWidth:100,controls:[{id:'table-action',left:110,right:160,top:0,bottom:44,width:50,height:44,primary:true,scrollContainer:true}]}),[]);
});
