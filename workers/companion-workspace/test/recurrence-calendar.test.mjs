import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {expandRecurrence,recurrenceLocalDate} from '../../shared/recurrence-calendar.ts';
const fixture=JSON.parse(await readFile(new URL('../../../tests/fixtures/workspace/recurrence-calendar-v1.json',import.meta.url),'utf8'));
for(const value of fixture.expansions)test('recurrence calendar: '+value.name,()=>{
 const original=JSON.stringify(value.rule);
 assert.deepEqual(expandRecurrence(value.rule,value.from,value.through),{dates:value.dates,failure:value.failure});
 assert.equal(JSON.stringify(value.rule),original);
});
for(const value of fixture.localDates)test('recurrence local date: '+value.name,()=>{
 assert.deepEqual(recurrenceLocalDate(value.instantUtc,value.timeZoneId),{date:value.date,failure:value.failure});
});
test('inclusive range limit accepts leap year and rejects an additional day',()=>{
 const rule={frequency:'daily',interval:1,startDate:'2024-01-01',timeZoneId:'UTC',weekdays:[],daysOfMonth:[]};
 assert.equal(expandRecurrence(rule,'2024-01-01','2024-12-31').dates.length,366);
 assert.deepEqual(expandRecurrence(rule,'2024-01-01','2025-01-01'),{dates:[],failure:'invalid_range'});
 for(const invalid of [{...rule,interval:367},{...rule,frequency:'unknown'},{...rule,weekdays:null},{...rule,daysOfMonth:[1]}])assert.deepEqual(expandRecurrence(invalid,'2024-01-01','2024-01-02'),{dates:[],failure:'invalid_recurrence'});
 for(const date of ['2026-02-30','0000-01-01','invalid'])assert.equal(expandRecurrence(rule,date,'2026-10-17').failure,'invalid_range');
});

test('shared calendar fixture also matches the pinned Workers runtime',async t=>{
 const {createRequire}=await import('node:module');const {fileURLToPath}=await import('node:url');
 const require=createRequire(new URL('../../chatgpt-auth/package.json',import.meta.url));
 const {build}=require('esbuild');const {Miniflare,convertV4MiniflareOptions}=require('miniflare');
 const config=JSON.parse(await readFile(new URL('../wrangler.jsonc',import.meta.url),'utf8'));
 const bundle=await build({stdin:{contents:"import {expandRecurrence,recurrenceLocalDate} from '../../shared/recurrence-calendar.ts'; export default {async fetch(request){const value=await request.json();return Response.json(value.mode==='local'?recurrenceLocalDate(value.instantUtc,value.timeZoneId):expandRecurrence(value.rule,value.from,value.through));}}",resolveDir:fileURLToPath(new URL('.',import.meta.url))},bundle:true,write:false,format:'esm',platform:'browser',target:'es2022'});
 const mf=new Miniflare(convertV4MiniflareOptions({workers:[{name:'calendar',modules:true,script:bundle.outputFiles[0].text,compatibilityDate:config.compatibility_date,outboundService:()=>{throw new Error('unexpected_external_call');}}]}));
 t.after(()=>mf.dispose());const worker=await mf.getWorker('calendar');
 for(const value of fixture.expansions)await t.test(value.name,async()=>{
  const response=await worker.fetch('https://calendar.test/',{method:'POST',body:JSON.stringify(value)});
  assert.equal(response.status,200);assert.deepEqual(await response.json(),{dates:value.dates,failure:value.failure});
 });
 for(const value of fixture.localDates)await t.test(value.name,async()=>{
  const response=await worker.fetch('https://calendar.test/',{method:'POST',body:JSON.stringify({...value,mode:'local'})});
  assert.equal(response.status,200);assert.deepEqual(await response.json(),{date:value.date,failure:value.failure});
 });
});
