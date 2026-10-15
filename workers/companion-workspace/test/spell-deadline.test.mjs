import {test} from 'node:test';
import assert from 'node:assert/strict';
import {setup} from './fixtures/spells.mjs';

test('foreground deadline returns known partial success without dispatching the remainder',async t=>{
 const s=await setup(t);const p=await s.prepare(3);s.state.slow=true;
 const receipt=(await s.execute(p)).structuredContent;assert.equal(receipt.status,'stopped');assert.equal(receipt.failure,'foreground_deadline');assert.equal(receipt.confirmedCount,1);assert.equal(receipt.attemptedCount,1);assert.equal(receipt.refreshSucceeded,false);assert.equal(s.state.posts,1);
});
