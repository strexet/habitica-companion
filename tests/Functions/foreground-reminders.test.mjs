import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import vm from 'node:vm';
const source=await readFile(new URL('../../src/Habitica.WebApp/wwwroot/js/foregroundReminders.js',import.meta.url),'utf8');
const {createForegroundReminders}=await import('data:text/javascript;base64,'+Buffer.from(source).toString('base64'));
const account='00000000-0000-4000-8000-000000000011',workspace='00000000-0000-4000-8000-000000000012',task='00000000-0000-4000-8000-000000000013';
const owner={accountId:account,workspaceId:workspace,source:'cloud'};
const instant='2026-10-26T09:00:00.0000000Z',cue={taskId:task,reviewAfterUtc:instant,scheduleRevision:10};
async function storageFixture(){
 const rows=new Map();let gate=Promise.resolve();
 const kv={get:async key=>structuredClone(rows.get(key)),put:async row=>rows.set(row.key,structuredClone(row)),where:()=>({startsWith:prefix=>({delete:async()=>{for(const key of rows.keys())if(key.startsWith(prefix))rows.delete(key);}})})};
 const database={table:()=>kv,version:()=>({stores(){}}),transaction:(...args)=>{const result=gate.then(args.at(-1));gate=result.catch(()=>{});return result;}};
 const source=(await readFile(new URL('../../src/Habitica.WebApp/wwwroot/js/storage/indexedDbStorage.js',import.meta.url),'utf8')).replace(/^import .*;$/m,'').replace('const database = new Dexie("habitica-tool");','').replaceAll('export async function','async function').replaceAll('export function','function');
 const context=vm.createContext({database,Date,structuredClone});vm.runInContext(source,context);
 return {rows,store:Object.fromEntries(['readReminderState','configureReminderState','claimReminder','finishReminder','clearReminderStates'].map(name=>[name,(...args)=>context[name](...args)]))};
}
async function fixture(shared){
 const storage=shared??await storageFixture(),timers=new Map(),listeners=new Map();let wall=Date.parse(instant)-1000,mono=0,serial=0;
 const calls={permission:0,notifications:[],registrations:0};
 const registration={active:true,scope:'https://companion.invalid/',async showNotification(title,options){calls.notifications.push({title,options});}};
 const host={Date:{now:()=>wall},performance:{now:()=>mono},setTimeout:(f,ms)=>{timers.set(++serial,{f,ms});return serial;},clearTimeout:id=>timers.delete(id),isSecureContext:true,crypto,location:{href:'https://companion.invalid/todo/today',origin:'https://companion.invalid'},document:{visibilityState:'visible',addEventListener:(n,f)=>listeners.set('doc:'+n,f),removeEventListener:n=>listeners.delete('doc:'+n)},addEventListener:(n,f)=>listeners.set(n,f),removeEventListener:n=>listeners.delete(n),navigator:{userActivation:{isActive:true},serviceWorker:{async getRegistration(){calls.registrations++;return registration;}}},Notification:{permission:'default',requestPermission(){calls.permission++;return Promise.resolve(host.Notification.permission='granted');}}};
 const published=[],browser=createForegroundReminders(host,storage.store,s=>published.push(s));
 return {browser,host,calls,timers,listeners,registration,published,...storage,advance(ms,clockOnly=false){wall+=ms;if(!clockOnly)mono+=ms;},async enable(version=1){await browser.configure('inApp',true,version);await browser.requestPermission(version);}};
}
test('binding reads reviewed cues without asking permission; in-app opt-in and permission are separate direct actions',async()=>{
 const f=await fixture();let s=await f.browser.bind(owner,[cue],1);assert.equal(s.inApp,false);assert.equal(f.calls.permission,0);assert.equal(f.calls.registrations,0);
 await f.browser.requestPermission(1);assert.equal(f.calls.permission,0);
 await f.browser.configure('inApp',true,1);s=f.browser.snapshot();assert.equal(s.inApp,true);assert.equal(s.browser,false);
 f.host.navigator.userActivation.isActive=false;assert.equal((await f.browser.requestPermission(1)).delivery,'gesture-required');assert.equal(f.calls.permission,0);
 f.host.navigator.userActivation.isActive=true;let calledBeforeReturn=false;f.host.Notification.requestPermission=()=>{calledBeforeReturn=true;return Promise.resolve(f.host.Notification.permission='granted');};const request=f.browser.requestPermission(1);assert.equal(calledBeforeReturn,true);await request;assert.equal(f.browser.snapshot().browser,true);assert.equal(f.calls.notifications.length,0);f.browser.stop();
});
test('fake-clock foreground delivery is deduplicated across tabs, reopening and unrelated snapshot revisions',async()=>{
 const shared=await storageFixture(),a=await fixture(shared),b=await fixture(shared);await a.browser.bind(owner,[cue],1);await a.enable();await b.browser.bind(owner,[cue],1);
 a.advance(1000);b.advance(1000);b.host.Notification.permission='granted';await Promise.all([a.browser.tick(),b.browser.tick()]);assert.equal(a.calls.notifications.length+b.calls.notifications.length,1);
 assert.equal(a.calls.notifications[0]?.options.renotify??b.calls.notifications[0].options.renotify,false);
 a.browser.stop();const reopened=await fixture(shared);reopened.advance(1000);reopened.host.Notification.permission='granted';await reopened.browser.bind(owner,[cue],1);assert.equal(reopened.calls.notifications.length,0);assert.equal(reopened.browser.snapshot().inApp,true);
 await reopened.browser.bind(owner,[cue],2);assert.equal(reopened.calls.notifications.length,0);
 await reopened.browser.bind(owner,[{...cue,scheduleRevision:20}],3);assert.equal(reopened.calls.notifications.length,1,'a separately accepted reschedule has a new identity');
 assert.ok([...shared.rows.values()].every(row=>!row.jsonText.includes('Companion review cue')&&!row.jsonText.includes('task text')));b.browser.stop();reopened.browser.stop();
});
test('snooze, removal, backward/forward clocks and hidden tabs preserve exact current-cue scheduling',async()=>{
 const f=await fixture();await f.browser.bind(owner,[cue],1);await f.enable();f.advance(2000,true);f.host.document.visibilityState='hidden';await f.browser.tick();assert.equal(f.calls.notifications.length,0);assert.equal(f.browser.snapshot().clockChanged,false);
 await f.browser.bind(owner,[{...cue,reviewAfterUtc:'2026-10-26T10:00:00.0000000Z',scheduleRevision:11}],2);f.host.document.visibilityState='visible';await f.browser.tick();assert.equal(f.calls.notifications.length,0);
 f.advance(-3600000,true);await f.browser.tick();assert.equal(f.browser.snapshot().clockChanged,true);f.advance(7200000,true);await f.browser.tick();assert.equal(f.calls.notifications.length,1);
 await f.browser.bind(owner,[],3);await f.browser.tick();assert.equal(f.calls.notifications.length,1);f.browser.stop();assert.equal(f.timers.size,0);assert.equal(f.listeners.size,0);
});
test('denied, dismissed, unsupported, missing worker and revoked permission remain explicit without subscription or fetch',async()=>{
 for(const answer of ['denied','default']){const f=await fixture();await f.browser.bind(owner,[cue],1);await f.browser.configure('inApp',true,1);f.host.Notification.requestPermission=()=>Promise.resolve(f.host.Notification.permission=answer);const s=await f.browser.requestPermission(1);assert.equal(s.delivery,answer==='denied'?'denied':'dismissed');assert.equal(s.inApp,true);assert.equal(s.browser,false);f.browser.stop();}
 const f=await fixture();f.host.Notification=undefined;await f.browser.bind(owner,[cue],1);await f.browser.configure('inApp',true,1);assert.equal((await f.browser.requestPermission(1)).delivery,'unsupported');assert.equal(f.calls.notifications.length,0);f.browser.stop();
 const x=await fixture();await x.browser.bind(owner,[cue],1);await x.enable();x.advance(2000);x.host.navigator.serviceWorker.getRegistration=async()=>undefined;assert.equal((await x.browser.tick()).delivery,'worker-unavailable');x.host.Notification.permission='denied';await x.browser.tick();assert.equal(x.calls.notifications.length,0);assert.equal(x.browser.snapshot().inApp,true);x.browser.stop();
});
test('unknown dispatch and lost claim acknowledgement consume original identity without retry',async()=>{
 for(const point of ['claim','dispatch']){const f=await fixture();await f.browser.bind(owner,[cue],1);await f.enable();f.advance(2000);
 if(point==='claim'){const original=f.store.claimReminder;f.store.claimReminder=async(...args)=>{const result=await original(...args);if(result==='claimed')throw Error('synthetic lost acknowledgement');return result;};}else f.registration.showNotification=async()=>{f.calls.notifications.push('uncertain');throw Error('synthetic lost response');};
 await f.browser.tick();await f.browser.tick();assert.equal(f.calls.notifications.length,point==='claim'?0:1);const data=JSON.parse([...f.rows.values()][0].jsonText);assert.equal(data.receipts.length,1);assert.equal(data.receipts[0].state,point==='claim'?'attempted':'unknown');f.browser.stop();}
});
test('late permission and worker responses cannot cross account/source changes; disabled cues remain visible only as saved metadata',async()=>{
 const f=await fixture();await f.browser.bind(owner,[cue],1);await f.browser.configure('inApp',true,1);let grant;f.host.Notification.requestPermission=()=>new Promise(resolve=>{grant=resolve;});const pending=f.browser.requestPermission(1);await f.browser.bind({...owner,accountId:'00000000-0000-4000-8000-000000000099'},[cue],2);grant('granted');await pending;assert.equal(f.browser.snapshot().browser,false);assert.equal(f.browser.snapshot().inApp,false);
 await f.browser.bind(owner,[cue],3);f.host.Notification.permission='granted';await f.enable(3);f.advance(2000);let release;f.host.navigator.serviceWorker.getRegistration=()=>new Promise(resolve=>{release=resolve;});const flight=f.browser.tick();while(!release)await new Promise(resolve=>setImmediate(resolve));await f.browser.bind({...owner,source:'browser'},[cue],4);release(f.registration);await flight;assert.equal(f.calls.notifications.length,0);
 await f.browser.bind(null,[],5);assert.equal(f.browser.snapshot().contextKey,null);assert.equal(f.timers.size,0);await f.browser.bind(owner,[cue],4);assert.equal(f.browser.snapshot().contextKey,null,'old binding rejected');f.browser.stop();
});
test('production storage transactions reserve once, retain receipts on disable and refuse full/corrupt storage',async()=>{
 const f=await fixture(),key=account+':cloud:'+workspace;await f.browser.bind(owner,[cue],1);await f.enable();const id='a'.repeat(64);assert.deepEqual(await Promise.all([f.store.claimReminder(key,id,1),f.store.claimReminder(key,id,1)]),['claimed','duplicate']);await f.store.configureReminderState(key,'inApp',false);let state=await f.store.readReminderState(key);assert.equal(state.browser,false);assert.equal(state.receipts.length,1);await f.store.configureReminderState(key,'inApp',true);await f.store.configureReminderState(key,'browser',true);
 const row=[...f.rows.values()][0],full=JSON.parse(row.jsonText);full.receipts=Array.from({length:2048},(_,i)=>({id:i.toString(16).padStart(64,'0'),state:'attempted',atMs:1}));row.jsonText=JSON.stringify(full);assert.equal(await f.store.claimReminder(key,'f'.repeat(64),2),'receipt-capacity');row.jsonText='broken';await assert.rejects(f.store.readReminderState(key));assert.equal((await f.browser.tick()).storage,'unavailable');await f.browser.clear();assert.equal(f.rows.size,0);assert.equal(f.timers.size,0);f.browser.stop();
});
