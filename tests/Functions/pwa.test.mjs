import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFile,readdir} from 'node:fs/promises';
import vm from 'node:vm';
const root=new URL('../../',import.meta.url),web=new URL('src/Habitica.WebApp/wwwroot/',root);
const read=path=>readFile(new URL(path,web),'utf8');

function shellResponse({redirected=false,url='https://companion.invalid/',type='basic',status=200,body='<html>fixture shell</html>',headers={}}={}) {
 const response=new Response(body,{status,headers:{'Content-Type':'text/html; charset=utf-8','X-Shell':'retained',...headers}});
 Object.defineProperties(response,{redirected:{value:redirected},url:{value:url},type:{value:type}});
 return response;
}

async function worker(options={}){
 const cacheReads=[],network=[],cachedAssets=[],events={};
 const assets=[{url:'index.html'},{url:'css/app.css'},{url:'js/pwa.js'},{url:'vendor/dexie.mjs'},{url:'manifest.webmanifest'},{url:'api/private.json'},{url:'https://other.invalid/foreign.js'},{url:'service-worker.js'}];
 const stored=[],requests=[];
 const context={URL,Response,Headers,console:{info(){}},Request:class extends Request{constructor(url,options){super(new URL(url,'https://companion.invalid/'),options);}},
  self:{origin:'https://companion.invalid',assetsManifest:{version:'fixture',assets},importScripts(){},addEventListener(name,handler){events[name]=handler;}},
  caches:{async open(name){if(options.cacheFailure)throw new Error('Cache refused');return {
   async match(request){cacheReads.push(request);if(options.missing)return undefined;const response=shellResponse(options.shell);response.kind='cached';return response;},
   async addAll(items){requests.push(...items);cachedAssets.push(...items.map(r=>r.url));if(options.installFailure)throw new Error('Integrity failure');},
   async put(key,response){stored.push({name,key,response});}
  };},async keys(){return [];},async delete(){return true;}},
  async fetch(request){network.push(request);return {kind:'network',request};}};
 vm.createContext(context);vm.runInContext(await read('js/pushNotifications.js'),context);vm.runInContext(await read('service-worker.published.js'),context);
 const fetch=async(path,mode='navigate',method='GET')=>{let result;events.fetch({request:{url:new URL(path,'https://companion.invalid').href,mode,method},respondWith(value){result=value;}});return result;};
 return {context,fetch,cacheReads,network,cachedAssets,stored,requests};
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
 assert.ok(w.cachedAssets.includes('https://companion.invalid/vendor/dexie.mjs'),'storage module dependencies must be available for offline startup');
 for(const path of ['api/private.json','other.invalid','service-worker.js'])assert.ok(w.cachedAssets.every(url=>!url.includes(path)));
});


test('redirected public shell becomes navigation-compatible without losing content or security headers',async()=>{
 const w=await worker({shell:{redirected:true,headers:{'Content-Security-Policy':"default-src 'self'",'Content-Encoding':'gzip','Content-Length':'99'}}});
 const response=await w.fetch('/todo/tasks?q=retained');
 assert.equal(response.redirected,false);assert.equal(response.type,'default');assert.equal(response.url,'');
 assert.equal(response.status,200);assert.equal(await response.text(),'<html>fixture shell</html>');
 assert.equal(response.headers.get('Content-Security-Policy'),"default-src 'self'");assert.equal(response.headers.get('X-Shell'),'retained');
 assert.equal(response.headers.get('Content-Encoding'),null);assert.equal(response.headers.get('Content-Length'),null);assert.equal(w.network.length,0);
});

test('invalid or foreign cached shells fall back to the unchanged navigation request',async()=>{
 for(const shell of [{url:'https://foreign.invalid/',redirected:true},{url:'https://companion.invalid/auth/callback',redirected:true},{type:'cors',redirected:true},{type:'opaque'},{type:'opaqueredirect'},{status:404},{headers:{'Content-Type':'application/json'}},{type:'default',redirected:true}]){
  const w=await worker({shell});assert.equal((await w.fetch('/dashboard')).kind,'network');assert.equal(w.network.length,1);assert.equal(w.network[0].mode,'navigate');
 }
 for(const options of [{missing:true},{cacheFailure:true}]){
  const w=await worker(options);assert.equal((await w.fetch('/')).kind,'network');assert.equal(w.network.length,1);
 }
});

test('install isolates the repaired cache and stores a compatible shell with same-origin integrity requests',async()=>{
 const w=await worker({shell:{redirected:true}});await w.context.onInstall({});
 assert.equal(w.stored.length,1);assert.equal(w.stored[0].name,'offline-cache-fixture-shell-v3');assert.equal(w.stored[0].key,'index.html');
 assert.equal(w.stored[0].response.redirected,false);assert.equal(await w.stored[0].response.text(),'<html>fixture shell</html>');
 assert.ok(w.requests.every(r=>r.mode==='same-origin'&&r.cache==='no-cache'));
});

test('failed installation or invalid shell cannot complete installation',async()=>{
 for(const options of [{installFailure:true},{shell:{url:'https://foreign.invalid/',redirected:true}},{missing:true}]){
  const w=await worker(options);await assert.rejects(w.context.onInstall({}));assert.equal(w.stored.length,0);
 }
});
