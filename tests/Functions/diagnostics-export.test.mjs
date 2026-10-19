import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
const source=await readFile(new URL('../../src/Habitica.WebApp/wwwroot/js/diagnosticsExport.js',import.meta.url),'utf8');
const {copyText}=await import('data:text/javascript;base64,'+Buffer.from(source).toString('base64'));
test('copy forwards exact safe representation and exposes unavailable or rejected clipboard',async()=>{
  const previous=Object.getOwnPropertyDescriptor(globalThis,'navigator');
  try {
    const copied=[];
    Object.defineProperty(globalThis,'navigator',{configurable:true,value:{clipboard:{writeText:async text=>{copied.push(text);}}}});
    const entry='{"operation":"workspace-accept","message":"Safe summary"}\n';
    await copyText(entry);assert.deepEqual(copied,[entry]);
    Object.defineProperty(globalThis,'navigator',{configurable:true,value:{}});
    await assert.rejects(copyText(entry),/clipboard_unavailable/);
    Object.defineProperty(globalThis,'navigator',{configurable:true,value:{clipboard:{writeText:async()=>{throw new Error('denied');}}}});
    await assert.rejects(copyText(entry),/denied/);
  } finally {if(previous)Object.defineProperty(globalThis,'navigator',previous);else delete globalThis.navigator;}
});
