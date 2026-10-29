import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
const source=await readFile(new URL('../../src/Habitica.WebApp/wwwroot/js/pwaPush.js',import.meta.url),'utf8');
const {createPushBrowser}=await import('data:text/javascript;base64,'+Buffer.from(source).toString('base64'));
const bytes=Uint8Array.from({length:65},(_,i)=>i===0?4:i),publicKey=Buffer.from(bytes).toString('base64url');
function fixture(options={}){
 const calls={permission:0,subscribe:0,unsubscribe:0,inspect:0,worker:0};let existing=options.existing??null;
 const subscription={endpoint:'https://web.push.apple.com/synthetic',options:{applicationServerKey:bytes.buffer,userVisibleOnly:true},toJSON(){return {endpoint:this.endpoint,expirationTime:null,keys:{p256dh:publicKey,auth:'fixture-auth'}};},async unsubscribe(){calls.unsubscribe++;return true;}};
 const host={isSecureContext:true,location:{origin:'https://companion.invalid'},navigator:{standalone:true,userActivation:{isActive:true},serviceWorker:{async getRegistration(){calls.worker++;return {scope:'https://companion.invalid/',active:true,pushManager:{async getSubscription(){calls.inspect++;return existing;},async subscribe(params){calls.subscribe++;assert.equal(params.userVisibleOnly,true);assert.deepEqual(params.applicationServerKey,bytes);return existing=subscription;}}};}}},PushManager:{},Notification:{permission:'default',async requestPermission(){calls.permission++;assert.equal(calls.worker,0,'permission invoked before first async worker operation');return options.permission??'granted';}},crypto,atob,btoa,...options.host};
 return {host,calls,subscription,browser:createPushBrowser(host),setExisting(value){existing=value;}};
}

test('import and capability inspection never request permission or touch a subscription',()=>{
 const f=fixture();assert.deepEqual(f.browser.capability(),{state:'available',permission:'default'});assert.equal(f.calls.permission,0);assert.equal(f.calls.worker,0);assert.equal(f.calls.subscribe,0);
 for(const [host,state] of [[{isSecureContext:false},'insecure'],[{navigator:{standalone:false}},'installation-required'],[{PushManager:undefined},'unsupported']]){const x=fixture({host});assert.equal(x.browser.capability().state,state);assert.equal(x.calls.permission,0);}
});

test('consent and direct user activation are mandatory before permission; denied and dismissed create nothing',async()=>{
 for(const [consent,active,state] of [[false,true,'consent-required'],[true,false,'gesture-required']]){const f=fixture();f.host.navigator.userActivation.isActive=active;assert.equal((await f.browser.subscribe(publicKey,consent)).state,state);assert.equal(f.calls.permission,0);}
 for(const [permission,state] of [['denied','denied'],['default','dismissed']]){const f=fixture({permission});assert.equal((await f.browser.subscribe(publicKey,true)).state,state);assert.equal(f.calls.permission,1);assert.equal(f.calls.subscribe,0);assert.equal(f.calls.worker,0);}
 const invalid=fixture();assert.equal((await invalid.browser.subscribe('invalid',true)).state,'invalid-application-key');assert.equal(invalid.calls.permission,0);
});

test('grant uses userVisibleOnly and returns a transient upload; existing correct subscription is reused',async()=>{
 const f=fixture(),result=await f.browser.subscribe(publicKey,true);assert.equal(result.state,'subscribed');assert.match(result.fingerprint,/^[A-Za-z0-9_-]{43}$/);assert.equal(result.subscription.endpoint,f.subscription.endpoint);assert.equal(f.calls.subscribe,1);
 const current=await f.browser.current();assert.equal(current.exists,true);assert.equal(current.fingerprint,result.fingerprint);assert.ok(!JSON.stringify(current).includes(f.subscription.endpoint));
 const reused=fixture();reused.setExisting(reused.subscription);assert.equal((await reused.browser.subscribe(publicKey,true)).state,'subscribed');assert.equal(reused.calls.subscribe,0);
});

test('a different VAPID binding is refused without replacing the browser subscription',async()=>{
 const f=fixture();f.setExisting({...f.subscription,options:{applicationServerKey:new Uint8Array(65).buffer,userVisibleOnly:true}});assert.equal((await f.browser.subscribe(publicKey,true)).state,'key-changed');assert.equal(f.calls.subscribe,0);assert.equal(f.calls.unsubscribe,0);
 const invisible=fixture();invisible.setExisting({...invisible.subscription,options:{applicationServerKey:bytes.buffer,userVisibleOnly:false}});assert.equal((await invisible.browser.subscribe(publicKey,true)).state,'user-visible-required');assert.equal(invisible.calls.subscribe,0);
});

test('unsubscribe requires gesture and exact local endpoint fingerprint; late replacement remains untouched',async()=>{
 const f=fixture();f.setExisting(f.subscription);const current=await f.browser.current();
 f.host.navigator.userActivation.isActive=false;assert.equal((await f.browser.unsubscribe(current.fingerprint)).state,'gesture-required');assert.equal(f.calls.unsubscribe,0);
 f.host.navigator.userActivation.isActive=true;f.setExisting({...f.subscription,endpoint:'https://fcm.googleapis.com/replacement'});assert.equal((await f.browser.unsubscribe(current.fingerprint)).state,'subscription-changed');assert.equal(f.calls.unsubscribe,0);
 f.setExisting(f.subscription);assert.equal((await f.browser.unsubscribe(current.fingerprint)).state,'unsubscribed');assert.equal(f.calls.unsubscribe,1);
 f.setExisting(null);assert.equal((await f.browser.unsubscribe(current.fingerprint)).state,'already-absent');
});

test('outside-app permission revocation is observable without another prompt',async()=>{
 const f=fixture();f.setExisting(f.subscription);f.host.Notification.permission='denied';
 const current=await f.browser.current();assert.equal(current.permission,'denied');assert.equal(f.calls.permission,0);assert.equal(f.calls.subscribe,0);
});

test('missing worker and platform exceptions are sanitized without leaking endpoint or provider details',async()=>{
 const f=fixture();f.host.navigator.serviceWorker.getRegistration=async()=>undefined;assert.equal((await f.browser.subscribe(publicKey,true)).state,'subscription-unavailable');
 const x=fixture();x.host.navigator.serviceWorker.getRegistration=async()=>{throw new Error('fixture-secret-endpoint');};const result=await x.browser.current();assert.equal(result.state,'worker-unavailable');assert.ok(!JSON.stringify(result).includes('fixture-secret'));
});
