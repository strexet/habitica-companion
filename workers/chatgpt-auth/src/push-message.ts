import {encode,decode} from './cryptography';
import {pushConfiguration,pushMaterial,type PushEnv,type PushMaterial} from './push-cryptography';
import {VaultError} from './vault';

const text=new TextEncoder();
function join(...parts:Uint8Array[]):Uint8Array<ArrayBuffer>{const result=new Uint8Array(parts.reduce((n,p)=>n+p.length,0));let offset=0;for(const part of parts){result.set(part,offset);offset+=part.length;}return result;}
async function hmac(key:Uint8Array<ArrayBuffer>,data:Uint8Array<ArrayBuffer>):Promise<Uint8Array<ArrayBuffer>> {
 const imported=await crypto.subtle.importKey('raw',key,{name:'HMAC',hash:'SHA-256'},false,['sign']);return new Uint8Array(await crypto.subtle.sign('HMAC',imported,data));
}
// RFC8291 single-record aes128gcm. Ephemeral ECDH keys are independent of VAPID.
export async function encryptPushMessage(material:PushMaterial,payload:string):Promise<Uint8Array<ArrayBuffer>> {
 const plain=text.encode(payload);if(plain.length>2048)throw new VaultError('push_payload_capacity',413);
 await pushMaterial(material);
 const ephemeral=await crypto.subtle.generateKey({name:'ECDH',namedCurve:'P-256'},true,['deriveBits']);
 if(!('publicKey' in ephemeral)||!('privateKey' in ephemeral))throw new VaultError('push_crypto_unavailable',503);
 const exported=await crypto.subtle.exportKey('raw',ephemeral.publicKey);
 if(!(exported instanceof ArrayBuffer))throw new VaultError('push_crypto_unavailable',503);
 const sender=new Uint8Array(exported),receiver=decode(material.keys.p256dh);
 const publicKey=await crypto.subtle.importKey('raw',receiver,{name:'ECDH',namedCurve:'P-256'},false,[]);
 // This pinned Workers type names the optional field $public; Web Crypto's runtime field is public.
 const algorithm={name:'ECDH',public:publicKey};
 const shared=new Uint8Array(await crypto.subtle.deriveBits(algorithm,ephemeral.privateKey,256));
 const prkKey=await hmac(decode(material.keys.auth),shared),ikm=await hmac(prkKey,join(text.encode('WebPush: info\0'),receiver,sender,new Uint8Array([1])));
 const salt=crypto.getRandomValues(new Uint8Array(16)),prk=await hmac(salt,ikm);
 const cek=(await hmac(prk,join(text.encode('Content-Encoding: aes128gcm\0'),new Uint8Array([1])))).slice(0,16);
 const nonce=(await hmac(prk,join(text.encode('Content-Encoding: nonce\0'),new Uint8Array([1])))).slice(0,12);
 const key=await crypto.subtle.importKey('raw',cek,'AES-GCM',false,['encrypt']);
 const encrypted=new Uint8Array(await crypto.subtle.encrypt({name:'AES-GCM',iv:nonce,tagLength:128},key,join(plain,new Uint8Array([2]))));
 const header=new Uint8Array(21);header.set(salt);new DataView(header.buffer).setUint32(16,4096,false);header[20]=sender.length;
 return join(header,sender,encrypted);
}
export function pushContact(env:PushEnv):string {
 const subject=env.PUSH_VAPID_SUBJECT;
 if(typeof subject!=='string'||subject.length>512)throw new VaultError('push_contact_required',503);
 let contact;try{contact=new URL(subject);}catch{throw new VaultError('push_contact_required',503);}
 if(!['https:','mailto:'].includes(contact.protocol)||contact.username||contact.password||contact.hash||contact.protocol==='https:'&&(!contact.hostname||contact.search)||contact.protocol==='mailto:'&&(!contact.pathname.includes('@')||contact.search))throw new VaultError('push_contact_required',503);
 return subject;
}
export async function pushDispatchAvailable(env:PushEnv):Promise<boolean>{
 try{if(!env.PUSH_DISPATCH_ENABLED)return false;await pushConfiguration(env);pushContact(env);return true;}catch{return false;}
}
export async function pushMessage(env:PushEnv,material:PushMaterial,payload:string,expiresAtMs:number,nowMs=Date.now()):Promise<{headers:Headers;body:Uint8Array<ArrayBuffer>}> {
 const publicKey=await pushConfiguration(env),subject=pushContact(env);
 if(!Number.isSafeInteger(expiresAtMs)||expiresAtMs<=nowMs||expiresAtMs>nowMs+3600000)throw new VaultError('push_expired',409);
 const header=encode(text.encode(JSON.stringify({typ:'JWT',alg:'ES256'}))),claims=encode(text.encode(JSON.stringify({aud:new URL(material.endpoint).origin,exp:Math.floor(nowMs/1000)+12*3600,sub:subject})));
 const key=await crypto.subtle.importKey('jwk',JSON.parse(env.PUSH_VAPID_PRIVATE_JWK!),{name:'ECDSA',namedCurve:'P-256'},false,['sign']);
 const signature=encode(new Uint8Array(await crypto.subtle.sign({name:'ECDSA',hash:'SHA-256'},key,text.encode(header+'.'+claims))));
 const headers=new Headers({'content-type':'application/octet-stream','content-encoding':'aes128gcm',authorization:'vapid t='+header+'.'+claims+'.'+signature+',k='+publicKey,TTL:String(Math.max(1,Math.ceil((expiresAtMs-nowMs)/1000))),Urgency:'normal'});
 return {headers,body:await encryptPushMessage(material,payload)};
}
