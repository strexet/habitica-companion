import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFile,readdir} from 'node:fs/promises';
import vm from 'node:vm';
const root=new URL('../../',import.meta.url),web=new URL('src/Habitica.WebApp/wwwroot/',root);
const read=path=>readFile(new URL(path,web),'utf8');

async function worker(){
 const cacheReads=[],network=[],cachedAssets=[],events={};
 const assets=[{url:'index.html'},{url:'css/app.css'},{url:'js/pwa.js'},{url:'manifest.webmanifest'},{url:'api/private.json'},{url:'https://other.invalid/foreign.js'},{url:'service-worker.js'}];
 const context={URL,console:{info(){}},Request:class extends Request{constructor(url,options){super(new URL(url,'https://companion.invalid/'),options);}},
  self:{origin:'https://companion.invalid',assetsManifest:{version:'fixture',assets},importScripts(){},addEventListener(name,handler){events[name]=handler;}},
  caches:{async open(){return {async match(request){cacheReads.push(request);return {kind:'cached',request};},async addAll(requests){cachedAssets.push(...requests.map(r=>r.url));}};},async keys(){return [];},async delete(){return true;}},
  async fetch(request){network.push(request);return {kind:'network',request};}};
 vm.createContext(context);vm.runInContext(await read('js/pushNotifications.js'),context);vm.runInContext(await read('service-worker.published.js'),context);
 const fetch=async(path,mode='navigate',method='GET')=>{let result;events.fetch({request:{url:new URL(path,'https://companion.invalid').href,mode,method},respondWith(value){result=value;}});return result;};
 return {context,fetch,cacheReads,network,cachedAssets};
}

test('PWA identity, actual PNG dimensions and startup registration agree',async()=>{
 const manifest=JSON.parse(await read('manifest.webmanifest')),index=await read('index.html');
 assert.equal(manifest.name,'Habitica Companion');assert.equal(manifest.short_name,'Companion');assert.equal(manifest.display,'standalone');assert.equal(manifest.id,'./');assert.equal(manifest.scope,'./');assert.equal(manifest.start_url,'./');
 for(const icon of manifest.icons){const png=await readFile(new URL(icon.src,web));assert.equal(png.subarray(1,4).toString(),'PNG');assert.equal(`${png.readUInt32BE(16)}x${png.readUInt32BE(20)}`,icon.sizes);assert.match(index,new RegExp(icon.src.replace('.','\\.')));}
 assert.match(index,/src="js\/pwa.js"/);assert.match(index,/apple-mobile-web-app-title" content="Habitica Companion"/);
});

test('registration runs on secure load only and refused offline setup preserves online use',async()=>{
 for(const [secure,supported] of [[true,true],[false,true],[true,false]]){
  let handler,calls=0;const dataset={};
  const context={window:{isSecureContext:secure,addEventListener(name,fn){assert.equal(name,'load');handler=fn;}},document:{documentElement:{dataset}},navigator:supported?{serviceWorker:{async register(path,options){calls++;assert.equal(path,'service-worker.js');assert.equal(options.scope,'./');throw new Error('private refusal');}}}:{}};
  vm.runInNewContext(await read('js/pwa.js'),context);
  assert.equal(calls,0);
  if(secure&&supported){handler();await new Promise(resolve=>setImmediate(resolve));assert.equal(calls,1);assert.equal(dataset.pwaOffline,'unavailable');}else assert.equal(handler,undefined);
 }
});

test('every current app route restores cached shell while sensitive, unknown and foreign routes stay network-only',async()=>{
 const w=await worker();let checked=0;
 for(const name of await readdir(new URL('src/Habitica.WebApp/Pages/',root))){
  if(!name.endsWith('.razor'))continue;
  const source=await readFile(new URL('src/Habitica.WebApp/Pages/'+name,root),'utf8');
  for(const match of source.matchAll(/@page "([^"]+)"/g)){
   const route=match[1].replace(/\{\w+:guid\}/g,'40000000-0000-4000-8000-000000000001');
   assert.equal((await w.fetch(route)).kind,route==='/privacy'?'network':'cached',route);checked++;
  }
 }
 assert.ok(checked>20);
 for(const route of ['/api/sync/private','/auth/callback','/oauth/authorize','/.well-known/oauth-authorization-server','/privacy','/terms','/notes-api','/task-api','/organization-api','/retirement-api','/unknown-route','https://other.invalid/dashboard'])assert.equal((await w.fetch(route)).kind,'network',route);
 assert.equal((await w.fetch('/dashboard','navigate','POST')).kind,'network');
 assert.equal((await w.fetch('/api/sync/private','cors')).kind,'network');
 assert.equal((await w.fetch('/css/app.css','cors')).kind,'cached');
 assert.ok(!w.cacheReads.some(r=>String(r).includes('private')));
});

test('installation caches only same-origin public build assets and includes the manifest',async()=>{
 const w=await worker();await w.context.onInstall({});
 assert.ok(w.cachedAssets.includes('https://companion.invalid/manifest.webmanifest'));
 for(const path of ['api/private.json','other.invalid','service-worker.js'])assert.ok(w.cachedAssets.every(url=>!url.includes(path)));
});
